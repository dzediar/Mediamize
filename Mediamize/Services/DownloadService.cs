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

                // 1. Récupérer le vrai titre si nécessaire pour le nommage propre
                string rawTitle = job.Title;
                if (string.IsNullOrWhiteSpace(rawTitle) || rawTitle == "Extraction..." || rawTitle == "Inconnu")
                {
                    logger.Report(new LogEntry("Récupération du titre...", Brushes.Gray));
                    rawTitle = await GetVideoTitleAsync(job.Url);
                }

                // 2. Nettoyer le titre
                string cleanTitle = CurrentConfig.RemoveSpecialChars ? StringCleaner.SanitizeForFilename(rawTitle) : rawTitle;

                // 3. Construire le chemin de sortie
                string outputTemplate = Path.Combine(CurrentConfig.OutputPath, $"{cleanTitle}.%(ext)s");

                logger.Report(new LogEntry($"Fichier cible : {cleanTitle}", Brushes.White));

                // 4. Construction des arguments intelligents
                string args = "";

                if (job.SelectedFormat.Id.Contains("bestaudio") || (job.SelectedFormat.IsAudio && job.SelectedFormat.Id.Contains("best")))
                {
                    args = $"-f \"bestaudio/best\" -x --audio-format mp3";
                }
                else if (job.SelectedFormat.Id.Contains("bestvideo") || (!job.SelectedFormat.IsAudio && job.SelectedFormat.Id.Contains("best")))
                {
                    args = $"-f \"bestvideo+bestaudio/best\" --merge-output-format mp4";
                }
                else
                {
                    args = $"-f \"{job.SelectedFormat.Id}\"";
                }

                // Ajout du template de sortie
                args += $" -o \"{outputTemplate}\"";

                // Ajout des métadonnées si coché
                if (CurrentConfig.AddMetadata)
                {
                    args += " --add-metadata";
                }

                // CORRECTION : --no-playlist sans espace, placé AVANT l'URL
                args += " --no-playlist";

                args += $" --js-runtimes deno:\"{CurrentConfig.DenoPath}\"";

                // Ajout du chemin ffmpeg si renseigné
                if (!string.IsNullOrEmpty(CurrentConfig.FfmpegPath))
                    args += $" --ffmpeg-location \"{CurrentConfig.FfmpegPath}\"";

                // Ajout de l'URL à la fin
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

                result.AddRange(commonFormats.Where(f => f.IsAudio).OrderByDescending(f => f.Resolution));

                result.Add(bestVideoFormat);

                result.AddRange(commonFormats.Where(f => !f.IsAudio).OrderByDescending(f => f.Resolution));
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
                        var fmt = new MediaFormat
                        {
                            Id = match.Groups[1].Value,
                            Ext = match.Groups[2].Value,
                            Resolution = match.Groups[3].Value,
                            Note = match.Groups[4].Value.Trim(),
                            IsAudio = match.Groups[3].Value == "audio only" || match.Groups[2].Value == "m4a" || (match.Groups[2].Value == "webm" && line.Contains("audio only"))
                        };

                        if (line.Contains("video only"))
                        {
                            fmt.Note = "Video Only - " + fmt.Note;
                            fmt.IsAudio = false;
                        }

                        formats.Add(fmt);
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
                return x.Id == y.Id;
            }

            public int GetHashCode(MediaFormat obj)
            {
                return obj.Id.GetHashCode();
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
