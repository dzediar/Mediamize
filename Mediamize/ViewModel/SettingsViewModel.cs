using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Mediamize.Services;
using System.IO;
using System.Windows;
using zComp.Wpf.ViewModel;
// Nécessite Ookii.Dialogs.Wpf pour selection dossier, ou Microsoft.Win32.OpenFileDialog

namespace Mediamize.ViewModel
{
    /// <summary>
    /// SettingsViewModel
    /// </summary>
    public partial class SettingsViewModel : CardViewModel
    {
        public SettingsViewModel()
        {
            Title = "SETTINGS";
            Width = 700;

            LoadFromConfig();
        }

        private void LoadFromConfig()
        {
            var cfg = MMApplicationViewModel.Instance.Repository.LocalConfiguration;
            
            YtDlpPath = cfg.YtDlpPath;
            FfmpegPath = cfg.FfmpegPath;
            DenoPath = cfg.DenoPath;
            OutputPath = cfg.OutputPath;
            AddMetadata = cfg.AddMetadata;
            RemoveSpecialChars = cfg.RemoveSpecialChars;
        }

        public void SaveToConfig()
        {
            var cfg = MMApplicationViewModel.Instance.Repository.LocalConfiguration;

            cfg.YtDlpPath = YtDlpPath;
            cfg.FfmpegPath = FfmpegPath;
            cfg.DenoPath = DenoPath;
            cfg.OutputPath = OutputPath;
            cfg.AddMetadata = AddMetadata;
            cfg.RemoveSpecialChars = RemoveSpecialChars;

            MMApplicationViewModel.Instance.Repository.SaveLocalConfiguration();
        }

        private bool computingOtherPaths = false;

        private void ComputeOtherPaths()
        {
            if (!IsLoaded || computingOtherPaths)
            {
                return;
            }

            computingOtherPaths = true;

            try
            {
                var paths = new List<string>();

                if (!string.IsNullOrWhiteSpace(YtDlpPath))
                {
                    paths.Add(YtDlpPath);
                }

                if (!string.IsNullOrWhiteSpace(FfmpegPath))
                {
                    paths.Add(FfmpegPath);
                }

                if (!string.IsNullOrWhiteSpace(DenoPath))
                {
                    paths.Add(DenoPath);
                }

                if (paths.Count < 3)
                {
                    foreach (var path in paths)
                    {
                        if (string.IsNullOrWhiteSpace(YtDlpPath))
                        {
                            var path2 = Path.Combine(Path.GetDirectoryName(path), "yt-dlp.exe");
                            if (File.Exists(path2))
                            {
                                YtDlpPath = path2;
                            }
                        }

                        if (string.IsNullOrWhiteSpace(FfmpegPath))
                        {
                            var path2 = Path.Combine(Path.GetDirectoryName(path), "ffmpeg.exe");
                            if (File.Exists(path2))
                            {
                                FfmpegPath = path2;
                            }
                        }

                        if (string.IsNullOrWhiteSpace(DenoPath))
                        {
                            var path2 = Path.Combine(Path.GetDirectoryName(path), "deno.exe");
                            if (File.Exists(path2))
                            {
                                DenoPath = path2;
                            }
                        }
                    }
                }
            }
            finally
            {
                computingOtherPaths = false;
            }
        }

        public string ytDlpPath = null;

        public string YtDlpPath
        {
            get
            {
                return ytDlpPath;
            }
            set
            {
                ytDlpPath = value;
                OnPropertyChanged(nameof(YtDlpPath));

                ComputeOtherPaths();
            }
        }

        public string ffmpegPath = null;

        public string FfmpegPath
        {
            get
            {
                return ffmpegPath;
            }
            set
            {
                ffmpegPath = value;
                OnPropertyChanged(nameof(FfmpegPath));

                ComputeOtherPaths();
            }
        }

        public string denoPath = null;

        public string DenoPath
        {
            get
            {
                return denoPath;
            }
            set
            {
                denoPath = value;
                OnPropertyChanged(nameof(DenoPath));

                ComputeOtherPaths();
            }
        }

        public string outputPath = null;

        public string OutputPath
        {
            get
            {
                return outputPath;
            }
            set
            {
                outputPath = value;
                OnPropertyChanged(nameof(OutputPath));

                ComputeOtherPaths();
            }
        }

        public bool addMetadata = true;

        public bool AddMetadata
        {
            get
            {
                return addMetadata;
            }
            set
            {
                addMetadata = value;
                OnPropertyChanged(nameof(AddMetadata));
            }
        }

        public bool removeSpecialChars = true;

        public bool RemoveSpecialChars
        {
            get
            {
                return removeSpecialChars;
            }
            set
            {
                removeSpecialChars = value;
                OnPropertyChanged(nameof(RemoveSpecialChars));
            }
        }
    }
}
