using Mediamize.Model;
using Mediamize.ViewModel;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Media;
using static System.Net.Mime.MediaTypeNames;

namespace Mediamize.Services
{
    public interface IDownloadService
    {
        MMLocalConfiguration CurrentConfig { get; set; }
        bool MustRefreshFormats(string url);
        Task<List<MediaFormat>> GetFormatsAsync(string url);
        Task RunDownloadBatchAsync(List<DownloadJob> jobs, IProgress<LogEntry> logger, CancellationToken token);
        bool IsYouTubePlaylist(string url);
        Task<List<string>> GetPlaylistVideoUrlsAsync(string url);
        MediaFormat BestAudioFormat { get; }
        MediaFormat BestVideoFormat { get; }
    }

    public class DownloadService : IDownloadService
    {
        public MMLocalConfiguration CurrentConfig { get; set; }

        private readonly MediaFormat bestAudioFormat = new MediaFormat()
        {
            Id = "bestaudio",
            IsAudio = true,
            Ext = "Best Audio",
            Note = "Best Audio",
            Resolution = null
        };

        public MediaFormat BestAudioFormat { get => bestAudioFormat; }

        private readonly MediaFormat bestVideoFormat = new MediaFormat()
        {
            Id = "bestvideo",
            IsAudio = false,
            Ext = "Best Video",
            Note = "Best Video",
            Resolution = null
        };

        public MediaFormat BestVideoFormat { get => bestVideoFormat; }

        public DownloadService()
        {
        }

        /// <summary>
        /// Détermine si l'URL pointe vers une vidéo ou une playlist valide pour lancer l'analyse.
        /// </summary>
        /// <param name="url">L'URL actuelle du navigateur</param>
        /// <returns>True si on doit récupérer les formats, False sinon</returns>
        public bool MustRefreshFormats(string url)
        {
            if (string.IsNullOrWhiteSpace(url)) return false;

            // Essayer de parser l'URL pour une analyse propre
            if (!Uri.TryCreate(url, UriKind.Absolute, out Uri uri)) return false;

            string host = uri.Host.ToLower();
            string path = uri.AbsolutePath.ToLower();
            string query = uri.Query.ToLower();

            // --- 1. YOUTUBE ---
            // Cas : youtube.com, m.youtube.com, youtu.be
            if (host.Contains("youtube.com") || host.Contains("youtu.be"))
            {
                // Vidéo standard (?v=...)
                if (query.Contains("v=")) return true;

                // Playlist (?list=...)
                if (query.Contains("list=")) return true;

                // Shorts (/shorts/...)
                if (path.StartsWith("/shorts/")) return true;

                // YouTube Live (souvent /live/...)
                if (path.StartsWith("/live/")) return true;

                // Format court youtu.be/ID
                if (host == "youtu.be" && path.Length > 1) return true;

                return false;
            }

            // --- 2. TIKTOK ---
            // Cas : tiktok.com/@user/video/ID
            if (host.Contains("tiktok.com"))
            {
                // Pattern standard vidéo
                if (path.Contains("/video/")) return true;

                // Pattern "vm.tiktok.com" (liens partagés mobiles)
                if (host.Contains("vm.tiktok.com") || host.Contains("vt.tiktok.com")) return true;

                return false;
            }

            // --- 3. FACEBOOK ---
            // Cas : facebook.com/watch, /videos/, /reel/
            if (host.Contains("facebook.com") || host.Contains("fb.watch"))
            {
                // Section Watch
                if (path.Contains("/watch/")) return true; // ex: facebook.com/watch/?v=...

                // URL directe vidéo
                if (path.Contains("/videos/")) return true; // ex: facebook.com/user/videos/123...

                // Reels Facebook
                if (path.Contains("/reel/")) return true;

                return false;
            }

            // --- 4. INSTAGRAM ---
            // Cas : instagram.com/reel/ID, /p/ID (Post)
            if (host.Contains("instagram.com"))
            {
                // Reels
                if (path.Contains("/reel/") || path.Contains("/reels/")) return true;

                // Post classique (souvent une vidéo ou un carrousel)
                // yt-dlp gère les posts "/p/", donc on peut activer l'analyse
                if (path.Contains("/p/")) return true;

                return false;
            }

            // --- 5. TWITTER / X ---
            // Cas : x.com/user/status/ID, twitter.com/user/status/ID
            if (host.Contains("twitter.com") || host.Contains("x.com"))
            {
                // Sur X, une vidéo est toujours dans un "status"
                if (path.Contains("/status/")) return true;

                return false;
            }

            // --- 6. TWITCH ---
            // Cas : twitch.tv/videos/ID, /clip/ID
            if (host.Contains("twitch.tv"))
            {
                // VOD
                if (path.Contains("/videos/")) return true;

                // Clips
                if (path.Contains("/clip/") || host.Contains("clips.twitch.tv")) return true;

                return false;
            }

            // --- 7. VIMEO ---
            // Cas : vimeo.com/123456789
            if (host.Contains("vimeo.com"))
            {
                // Sur Vimeo, l'URL est souvent juste des chiffres à la racine : vimeo.com/123456
                // On vérifie si le premier segment du chemin est un nombre
                string cleanPath = path.Trim('/');
                if (long.TryParse(cleanPath, out _)) return true;

                // Ou pattern /video/ID
                if (path.Contains("/video/")) return true;
            }

            // --- 8. SOUNDCLOUD (Pour la musique) ---
            if (host.Contains("soundcloud.com"))
            {
                // Eviter la home, stream, search, etc.
                // SoundCloud est permissif, mais on veut éviter les pages racines
                if (path.Length > 1
                    && !path.StartsWith("/discover")
                    && !path.StartsWith("/stream")
                    && !path.StartsWith("/search")
                    && !path.StartsWith("/pages"))
                {
                    // Si on est sur une page artiste/titre, c'est bon
                    // ex: /artiste/chanson
                    return true;
                }
            }

            return false;
        }

        private readonly ConcurrentDictionary<CancellationTokenSource, CancellationTokenSource> pendingGetFormats = new ConcurrentDictionary<CancellationTokenSource, CancellationTokenSource>();

        public async Task<List<MediaFormat>> GetFormatsAsync(string url)
        {
            if (pendingGetFormats.Count > 0)
            {
                foreach (var gf in pendingGetFormats.Keys.ToArray())
                {
                    gf.Cancel();
                    pendingGetFormats.TryRemove(gf, out var gf2);
                }
            }

            using (var cts = new CancellationTokenSource())
            {
                pendingGetFormats.TryAdd(cts, cts);

                try
                {
                    var startInfo = new ProcessStartInfo
                    {
                        FileName = CurrentConfig.YtDlpPath,
                        // CORRECTION : --no-playlist sans espace
                        Arguments = $"--retries 10 --fragment-retries 10 --js-runtimes deno:\"{CurrentConfig.DenoPath}\" --no-playlist -F \"{url}\"",
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        UseShellExecute = false,
                        CreateNoWindow =
#if DEBUG
                        false
#else
                        true
#endif
                    };

                    using var process = Process.Start(startInfo);

                    var outputTask = process.StandardOutput.ReadToEndAsync();
                    var errorTask = process.StandardError.ReadToEndAsync();

                    while (!process.HasExited && !cts.IsCancellationRequested)
                    {
                        await Task.Delay(100);
                    }

                    if (!process.HasExited)
                    {
                        process.Kill(true);
                    }

                    if (cts.IsCancellationRequested)
                    {
                        return null;
                    }

                    var output = await outputTask;
                    var error = await errorTask;

                    var finalFormats = ParseAndIntersectFormats(output);

                    return finalFormats;
                }
                finally
                {
                    pendingGetFormats.TryRemove(cts, out var cts2);
                }
            }
        }

        public async Task RunDownloadBatchAsync(List<DownloadJob> jobs, IProgress<LogEntry> logger, CancellationToken token)
        {
            foreach (var job in jobs)
            {
                if (token.IsCancellationRequested) break;

                logger.Report(new LogEntry($"--- Traitement de : {job.Url} ---", Brushes.Cyan));

                // ... (Code de récupération de titre inchangé) ...
                string rawTitle = job.Title;
                if (string.IsNullOrWhiteSpace(rawTitle) || rawTitle == "Extraction..." || rawTitle == "Inconnu")
                {
                    logger.Report(new LogEntry("Récupération du titre...", Brushes.Gray));
                    rawTitle = await GetVideoTitleAsync(job.Url);
                }

                string cleanTitle = CurrentConfig.RemoveSpecialChars ? StringCleaner.SanitizeForFilename(rawTitle) : rawTitle;
                string outputTemplate = Path.Combine(CurrentConfig.OutputPath, $"{cleanTitle}.%(ext)s");

                logger.Report(new LogEntry($"Fichier cible : {cleanTitle}", Brushes.White));

                string args = "";

                // LOGIQUE DE SÉLECTION MISE À JOUR

                // Cas 1 : "Best Audio" générique
                if (job.SelectedFormat.Id == "bestaudio")
                {
                    args = $"-f \"bestaudio/best\" -x --audio-format mp3";
                }
                // Cas 2 : "Best Video" générique
                else if (job.SelectedFormat.Id == "bestvideo")
                {
                    args = $"-f \"bestvideo+bestaudio/best\" --merge-output-format mp4";
                }
                // Cas 3 : L'utilisateur a choisi un format MP3 spécifique (généré par notre parsing)
                else if (job.SelectedFormat.IsAudio && job.SelectedFormat.Ext == "mp3")
                {
                    // On télécharge l'ID spécifique, mais on convertit en mp3
                    args = $"-f \"{job.SelectedFormat.Id}\" -x --audio-format mp3";
                }
                // Cas 4 : Format natif standard (m4a, webm, mp4 spécifique...)
                else
                {
                    args = $"-f \"{job.SelectedFormat.Id}\"";
                }

                // ... (Reste de la méthode : outputTemplate, metadata, ffmpeg location, etc.) ...
                args += $" -o \"{outputTemplate}\"";

                if (CurrentConfig.AddMetadata) args += " --add-metadata";

                // Important : toujours ajouter --no-playlist pour traiter url par url
                args += " --no-playlist";

                args += $" --js-runtimes deno:\"{CurrentConfig.DenoPath}\"";

                if (!string.IsNullOrEmpty(CurrentConfig.FfmpegPath))
                    args += $" --ffmpeg-location \"{CurrentConfig.FfmpegPath}\"";

                args += $" \"{job.Url}\"";

                var startInfo = new ProcessStartInfo
                {
                    FileName = CurrentConfig.YtDlpPath,
                    Arguments = args,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                using var process = new Process { StartInfo = startInfo };

                process.OutputDataReceived += (s, e) => {
                    if (!string.IsNullOrEmpty(e.Data)) logger.Report(new LogEntry(e.Data, Brushes.LightGray));
                };

                process.ErrorDataReceived += (s, e) => {
                    if (!string.IsNullOrEmpty(e.Data))
                    {
                        var color = e.Data.Contains("WARNING") ? Brushes.Yellow : Brushes.Red;
                        if (!e.Data.Contains("[download]") || e.Data.Contains("100%"))
                            logger.Report(new LogEntry(e.Data, color));
                    }
                };

                process.Start();
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();

                try
                {
                    await process.WaitForExitAsync(token);
                    logger.Report(new LogEntry("Téléchargement terminé avec succès.", Brushes.LimeGreen));
                }
                catch (TaskCanceledException)
                {
                    try { process.Kill(); } catch { }
                    logger.Report(new LogEntry("Processus annulé par l'utilisateur.", Brushes.Orange));
                    throw;
                }
            }
        }

        private async Task<string> GetVideoTitleAsync(string url)
        {
            try
            {
                var startInfo = new ProcessStartInfo
                {
                    FileName = CurrentConfig.YtDlpPath,
                    // CORRECTION : --no-playlist sans espace
                    Arguments = $"--js-runtimes deno:\"{CurrentConfig.DenoPath}\" --no-playlist --get-title \"{url}\"",
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                using var process = Process.Start(startInfo);
                var title = await process.StandardOutput.ReadToEndAsync();
                await process.WaitForExitAsync();

                return title.Trim();
            }
            catch
            {
                return "media_extracted_" + DateTime.Now.Ticks;
            }
        }

        public List<MediaFormat> ParseAndIntersectFormats(string fullLogOutput)
        {
            var formatBlocks = Regex.Split(fullLogOutput, @"\[info\] Available formats for");

            if (formatBlocks.Length < 2)
            {
                return new List<MediaFormat>();
            }

            List<MediaFormat> commonFormats = null;

            for (int i = 1; i < formatBlocks.Length; i++)
            {
                string block = formatBlocks[i];
                var videoFormats = ParseFormatsFromTable(block);

                if (videoFormats.Count == 0) continue;

                if (commonFormats == null)
                {
                    commonFormats = videoFormats;
                }
                else
                {
                    commonFormats = commonFormats
                        .Intersect(videoFormats, new MediaFormatEqualityComparer())
                        .ToList();
                }
            }

            var result = new List<MediaFormat>();

            if (commonFormats != null)
            {
                result.Add(bestAudioFormat);

                result.AddRange(commonFormats.Where(f => f.IsAudio).OrderBy(f => f.Ext).OrderByDescending(f => f.Resolution));

                result.Add(bestVideoFormat);

                result.AddRange(commonFormats.Where(f => !f.IsAudio).OrderBy(f => f.Ext).OrderByDescending(f => f.Resolution));
            }

            return result;
        }

        private List<MediaFormat> ParseFormatsFromTable(string tableText)
        {
            var formats = new List<MediaFormat>();

            using (var reader = new StringReader(tableText))
            {
                string line;
                while ((line = reader.ReadLine()) != null)
                {
                    if (line.StartsWith("[") || (formats.Count > 0 && string.IsNullOrWhiteSpace(line))) break;
                    if (line.Contains("ID") && line.Contains("EXT") || line.StartsWith("-")) continue;
                    if (line.Trim().StartsWith("sb")) continue;

                    var match = Regex.Match(line, @"^\s*(\w+)\s+(\w+)\s+((?:\d+x\d+)|audio only)\s+(.*)");

                    if (match.Success)
                    {
                        string id = match.Groups[1].Value;
                        string ext = match.Groups[2].Value;
                        string resolution = match.Groups[3].Value;
                        string note = match.Groups[4].Value.Trim();

                        bool isAudio = resolution == "audio only" || ext == "m4a" || (ext == "webm" && line.Contains("audio only"));

                        // 1. Ajouter le format original (Natif)
                        var fmt = new MediaFormat
                        {
                            Id = id,
                            Ext = ext,
                            Resolution = resolution,
                            Note = note,
                            IsAudio = isAudio
                        };

                        if (line.Contains("video only"))
                        {
                            fmt.Note = "Video Only - " + fmt.Note;
                            fmt.IsAudio = false;
                        }

                        formats.Add(fmt);

                        // 2. Si c'est de l'audio, on ajoute une version virtuelle MP3
                        if (fmt.IsAudio)
                        {
                            // On clone les infos mais on change l'extension et la note
                            formats.Add(new MediaFormat
                            {
                                Id = id, // On garde le même ID youtube
                                Ext = "mp3", // On force l'extension mp3
                                Resolution = resolution,
                                Note = $"{note} (Convert to MP3)", // Indication pour l'utilisateur
                                IsAudio = true
                            });
                        }
                    }
                }
            }
            return formats;
        }

        /// <summary>
        /// Vérifie si l'URL correspond à une playlist YouTube
        /// </summary>
        /// <param name="url">URL à vérifier</param>
        /// <returns>True si c'est une playlist, False sinon</returns>
        public bool IsYouTubePlaylist(string url)
        {
            if (string.IsNullOrWhiteSpace(url))
            {
                return false;
            }

            url = url.ToLower();

            // Pattern 1 : URL de playlist directe (youtube.com/playlist?list=...)
            if (url.Contains("/playlist?list=") || url.Contains("/playlist?"))
            {
                return true;
            }

            // Pattern 2 : URL de vidéo avec paramètre de playlist (&list=...)
            // Mais on vérifie qu'il n'y a PAS de paramètre de vidéo (v=) en premier
            if (url.Contains("&list=") || url.Contains("?list="))
            {
                // Si c'est juste une vidéo dans une playlist, ce n'est pas considéré comme playlist
                // On retourne true uniquement si l'URL commence par la playlist
                var uri = new Uri(url);
                var query = System.Web.HttpUtility.ParseQueryString(uri.Query);

                // Si pas de paramètre 'v' (vidéo), c'est une playlist pure
                return !string.IsNullOrEmpty(query["list"]) && uri.Host == "www.youtube.com";
            }

            return false;
        }

        /// <summary>
        /// Récupère toutes les URLs des vidéos d'une playlist YouTube
        /// </summary>
        /// <param name="url">URL d'une vidéo de la playlist ou URL de la playlist directement</param>
        /// <returns>Liste des URLs de toutes les vidéos de la playlist</returns>
        public async Task<List<string>> GetPlaylistVideoUrlsAsync(string url)
        {
            var videoUrls = new List<string>();

            try
            {
                var startInfo = new ProcessStartInfo
                {
                    FileName = CurrentConfig.YtDlpPath,
                    // --flat-playlist : ne télécharge pas, liste seulement
                    // --get-url : affiche l'URL de chaque vidéo
                    // --yes-playlist : force le traitement de la playlist même si l'URL pointe vers une vidéo
                    Arguments = $"--flat-playlist --get-url --yes-playlist --js-runtimes deno:\"{CurrentConfig.DenoPath}\" \"{url}\"",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow =
#if DEBUG
                        false
#else
                        true
#endif
                };

                using var process = Process.Start(startInfo);

                string line;
                while ((line = await process.StandardOutput.ReadLineAsync()) != null)
                {
                    if (!string.IsNullOrWhiteSpace(line) && (line.StartsWith("http://") || line.StartsWith("https://")))
                    {
                        videoUrls.Add(line.Trim());
                    }
                }

                await process.WaitForExitAsync();

                return videoUrls;
            }
            catch (Exception ex)
            {
                // Log l'erreur si nécessaire
                return new List<string>();
            }
        }

        public class MediaFormatEqualityComparer : IEqualityComparer<MediaFormat>
        {
            public bool Equals(MediaFormat x, MediaFormat y)
            {
                if (x == null || y == null) return false;
                // On compare l'ID ET l'extension pour distinguer le format natif (m4a) du format converti (mp3)
                return x.Id == y.Id && x.Ext == y.Ext;
            }

            public int GetHashCode(MediaFormat obj)
            {
                return HashCode.Combine(obj.Id, obj.Ext);
            }
        }
    }

    public static class StringCleaner
    {
        public static string SanitizeForFilename(string input)
        {
            if (string.IsNullOrWhiteSpace(input))
            {
                return "untitled_media";
            }

            string normalized = input.Normalize(Encoding.UTF8.Equals(Encoding.ASCII) ? NormalizationForm.FormC : NormalizationForm.FormKD);
            var builder = new StringBuilder();

            foreach (char c in normalized)
            {
                if (char.IsLetterOrDigit(c) || char.IsWhiteSpace(c) || "-_()[].,'".Contains(c))
                {
                    builder.Append(c);
                }
            }

            string result = Regex.Replace(builder.ToString(), @"\s+", " ").Trim();

            return string.IsNullOrWhiteSpace(result) ? "media_extracted" : result;
        }
    }
}
