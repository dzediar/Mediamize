using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Mediamize.Model;
using Mediamize.Services;
using Microsoft.Extensions.DependencyInjection;
using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Diagnostics.Eventing.Reader;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;
using zComp.Core.Helpers;
using zComp.Core.Model;
using zComp.Wpf.ViewModel;

namespace Mediamize.ViewModel
{
    /// <summary>
    /// MainViewModel
    /// </summary>
    public partial class MMMainViewModel : MainViewModel<MMMainViewModel>
    {
        private IDownloadService DownloadService { get => Services.GetService<IDownloadService>(); }

        /// <summary>
        /// Constructor
        /// </summary>
        public MMMainViewModel()
        {
            OpenSettingsCommand = new RelayCommand(OpenSettings);
            AnalyzeUrlCommand = new AsyncRelayCommand(AnalyzeUrlAsync);
            AddJobCommand = new RelayCommand(AddJob, () => SelectedFormat != null);
            AddPlaylistCommand = new AsyncRelayCommand(AddPlaylist, () => IsPlaylist);
            RemoveJobCommand = new RelayCommand(RemoveJob, () => SelectedJob != null);
            StartBatchCommand = new AsyncRelayCommand(StartBatchAsync, () => Jobs.Count > 0);
        }

        /// <summary>
        /// Commands refresh
        /// </summary>
        public override void RefreshAllCommands()
        {
            base.RefreshAllCommands();

            OpenSettingsCommand.NotifyCanExecuteChanged();
            AnalyzeUrlCommand.NotifyCanExecuteChanged();
            AddJobCommand.NotifyCanExecuteChanged();
            AddPlaylistCommand.NotifyCanExecuteChanged();
            RemoveJobCommand.NotifyCanExecuteChanged();
            StartBatchCommand.NotifyCanExecuteChanged();
        }

        /// <summary>
        /// Open settings window
        /// </summary>
        private void OpenSettings()
        {
            var vm = Services.GetRequiredService<SettingsViewModel>();

            if (vm.IsLoaded)
            {
                return;
            }

            if (Messaging.ShowModal(vm) == true)
            {
                vm.SaveToConfig();
            }
        }

        /// <summary>
        /// Analyse an url to extract available formats
        /// </summary>
        /// <returns></returns>
        private async Task AnalyzeUrlAsync()
        {
            if (!MMApplicationViewModel.Instance.Repository.LocalConfiguration.IsValid())
            {
                Messaging.DoDispatcherEvents();
                OpenSettings();
                return;
            }

            AvailableFormats.Clear();

            await AnalyzeUrlInternal();
        }

        /// <summary>
        /// Analyse an url to extract available formats
        /// </summary>
        private uint analyzeUrlCounter = 0;

        private readonly ConcurrentDictionary<CancellationTokenSource, CancellationTokenSource> pendingGetFormats = new ConcurrentDictionary<CancellationTokenSource, CancellationTokenSource>();

        /// <summary>
        /// Analyse an url to extract available formats
        /// </summary>
        /// <returns></returns>
        private async Task AnalyzeUrlInternal()
        {
            uint cpt;

            lock (this)
            {
                cpt = ++analyzeUrlCounter;
            }

            AvailableFormats.Clear();
            AnalyzingUrlTempo = null;

            await Task.Run(async () =>
            {
                Messaging.DispatcherInvoke(() => AnalyzingUrlTempo = 0, false, false);

                for (int i = 0; i < 100; i++)
                {
                    Thread.Sleep(20);

                    if (AnalyzingUrlTempo == null)
                    {
                        return;
                    }                   

                    Messaging.DispatcherInvoke(() => AnalyzingUrlTempo = (AnalyzingUrlTempo ?? 0) + 1, false, false);
                }

                Messaging.DispatcherInvoke(() => AnalyzingUrlTempo = null, false, false);

                lock (this)
                {
                    if (cpt != analyzeUrlCounter)
                    {
                        return;
                    }
                }

                IsAnalyzingUrlCpt += 1;

                try
                {
                    var formats = await DownloadService.GetFormatsAsync(CurrentUrl);

                    Messaging.DispatcherInvoke(() =>
                    {
                        AvailableFormats.Clear();

                        if (formats != null && formats.Count > 0)
                        {
                            foreach (var f in formats)
                            {
                                AvailableFormats.Add(f);
                            }

                            SelectedFormat = AvailableFormats[0];
                        }
                    }, true, true);
                }
                finally
                {
                    IsAnalyzingUrlCpt -= 1;
                }
            });
        }

        /// <summary>
        /// Add a scrapping job to queue
        /// </summary>
        private void AddJob()
        {
            if (SelectedFormat == null)
            {
                return;
            }

            Jobs.Add(new DownloadJob { Url = CurrentUrl, Title = "Extraction...", SelectedFormat = SelectedFormat });

            RefreshAllCommands();
        }

        /// <summary>
        /// Add playlist elements to queue
        /// </summary>
        private async Task AddPlaylist()
        {
            if (!IsPlaylist || string.IsNullOrWhiteSpace(BrowsedUrl))
            {
                return;
            }

            var videos = await DownloadService.GetPlaylistVideoUrlsAsync(BrowsedUrl);

            if (videos.Count > 0)
            {
                foreach (var v in videos)
                {
                    Jobs.Add(new DownloadJob { Url = v, Title = "Extraction...", SelectedFormat = DownloadService.BestAudioFormat });
                }
            }

            RefreshAllCommands();
        }
        

        /// <summary>
        /// Remove a scrapping job from queue
        /// </summary>
        private void RemoveJob()
        {
            if (SelectedJob == null)
            {
                return;
            }

            var i = Jobs.IndexOf(SelectedJob);

            if (i < 0)
            {
                return;
            }

            Jobs.Remove(SelectedJob);

            if (Jobs.Count > 0)
            {
                var maxI = Jobs.Count - 1;
                SelectedJob = i > maxI ? Jobs[maxI] : Jobs[i];
            }

            RefreshAllCommands();
        }

        /// <summary>
        /// Start scrapping
        /// </summary>
        /// <returns></returns>
        private async Task StartBatchAsync()
        {
            if (Jobs.Count == 0)
            {
                return;
            }

            if (!MMApplicationViewModel.Instance.Repository.LocalConfiguration.IsValid())
            {
                OpenSettings();
                return;
            }

            await StartBatchAsyncInternal();
        }

        /// <summary>
        /// Start scrapping
        /// </summary>
        /// <returns></returns>
        private async Task StartBatchAsyncInternal()
        {
            var logVm = Services.GetRequiredService<LogViewModel>();

            var cts = new CancellationTokenSource();
            logVm.Initialize(cts);

            // Affiche la fenêtre sans bloquer (Show au lieu de ShowDialog pour voir la progression, 
            // ou ShowDialog si on veut bloquer l'interaction main window)
            Messaging.Show(logVm);

            var progress = new Progress<LogEntry>(log => logVm.AddLog(log));

            try
            {
                MMApplicationViewModel.Instance.SoundToPlay = "SND_START";

                var jobsToProcess = Jobs.ToList();
                logVm.IsRunning = true;
                await DownloadService.RunDownloadBatchAsync(jobsToProcess, progress, cts.Token);
                
                logVm.AddLog(new LogEntry("Traitement terminé !", Brushes.LimeGreen));
                MMApplicationViewModel.Instance.SoundToPlay = "SND_END";
                logVm.IsRunning = false;

                jobsToProcess.ForEach(j => { if (Jobs.Contains(j)) Jobs.Remove(j); });

                Process.Start("explorer.exe", MMApplicationViewModel.Instance.Repository.LocalConfiguration.OutputPath);
            }
            catch (Exception ex)
            {
                logVm.IsRunning = false;
                logVm.AddLog(new LogEntry($"Erreur fatale : {ex.Message}", Brushes.Red));
                MMApplicationViewModel.Instance.SoundToPlay = "SND_FAILURE";
            }
        }

        /// <summary>
        /// Retrieved audio / video formats
        /// </summary>
        private ObservableCollection<MediaFormat> availableFormats = new();

        /// <summary>
        /// Retrieved audio / video formats
        /// </summary>
        public ObservableCollection<MediaFormat> AvailableFormats
        {
            get
            {
                return availableFormats;
            }
            private set
            {
                availableFormats = value;
                OnPropertyChanged(nameof(AvailableFormats));
            }
        }

        /// <summary>
        /// Selectes audio / video format
        /// </summary>
        private MediaFormat selectedFormat;

        /// <summary>
        /// Selectes audio / video format
        /// </summary>
        public MediaFormat SelectedFormat
        {
            get
            {
                return selectedFormat;
            }
            set
            {
                selectedFormat = value;
                OnPropertyChanged(nameof(SelectedFormat));

                RefreshAllCommands();
            }
        }

        /// <summary>
        /// Scrapping jobs queue
        /// </summary>
        private ObservableCollection<DownloadJob> jobs = new();

        /// <summary>
        /// Scrapping jobs queue
        /// </summary>
        public ObservableCollection<DownloadJob> Jobs
        {
            get
            {
                return jobs;
            }
            private set
            {
                jobs = value;
                OnPropertyChanged(nameof(Jobs));
            }
        }

        /// <summary>
        /// Selected scrapping job
        /// </summary>
        private DownloadJob selectedJob;

        /// <summary>
        /// Selected scrapping job
        /// </summary>
        public DownloadJob SelectedJob
        {
            get
            {
                return selectedJob;
            }
            set
            {
                selectedJob = value;
                OnPropertyChanged(nameof(SelectedJob));

                RefreshAllCommands();
            }
        }

        /// <summary>
        /// Analysing an url ?
        /// </summary>
        private int isAnalyzingUrlCpt = 0;

        /// <summary>
        /// Analysing an url ?
        /// </summary>
        private int IsAnalyzingUrlCpt
        {
            get
            {
                return isAnalyzingUrlCpt;
            }
            set
            {
                isAnalyzingUrlCpt = value;
                IsAnalyzingUrl = value > 0;
            }
        }

        /// <summary>
        /// Analysing an url ?
        /// </summary>
        public bool isAnalyzingUrl = false;

        /// <summary>
        /// Analysing an url ?
        /// </summary>
        public bool IsAnalyzingUrl
        {
            get
            {
                return isAnalyzingUrl;
            }
            private set
            {
                isAnalyzingUrl = value;
                OnPropertyChanged(nameof(IsAnalyzingUrl));
            }
        }

        /// <summary>
        /// Analysing an url ?
        /// </summary>
        public int? analyzingUrlTempo = null;

        /// <summary>
        /// Analysing an url ?
        /// </summary>
        public int? AnalyzingUrlTempo
        {
            get
            {
                return analyzingUrlTempo;
            }
            private set
            {
                analyzingUrlTempo = value;
                OnPropertyChanged(nameof(AnalyzingUrlTempo));
            }
        }

        /// <summary>
        /// Default url
        /// </summary>
        private const string defaultUrl = "https://www.youtube.com";

        /// <summary>
        /// Current url (input in the textbox)
        /// </summary>
        public string currentUrl = defaultUrl;

        /// <summary>
        /// Current url (input in the textbox)
        /// </summary>
        public string CurrentUrl
        {
            get
            {
                return currentUrl;
            }
            set
            {
                if (currentUrl == value)
                {
                    return;
                }

                currentUrl = value;
                OnPropertyChanged(nameof(CurrentUrl));

                //if (!string.IsNullOrWhiteSpace(value) && Uri.TryCreate(value, UriKind.RelativeOrAbsolute, out var uri))
                //{
                //    value = NormalizeUrl(value);
                //
                //    if (Uri.TryCreate(value, UriKind.Absolute, out uri))
                //    {
                //        BrowsedUrl = value;
                //    }
                //}

                BrowsedUrl = value;

                RefreshAllCommands();
            }
        }

        /// <summary>
        /// Browsed url (showed in the webview)
        /// </summary>
        public string browsedUrl = defaultUrl;

        /// <summary>
        /// Browsed url (showed in the webview)
        /// </summary>
        public string BrowsedUrl
        {
            get
            {
                return browsedUrl;
            }
            set
            {
                if (browsedUrl == value)
                {
                    return;
                }

                IsPlaylist = false;

                browsedUrl = value;
                OnPropertyChanged(nameof(BrowsedUrl));

                if (CurrentUrl != browsedUrl)
                {
                    currentUrl = browsedUrl;
                    OnPropertyChanged(nameof(CurrentUrl));
                }

                IsPlaylist = DownloadService.IsYouTubePlaylist(browsedUrl);

                MMApplicationViewModel.Instance.Repository.LocalConfiguration.LastURL = browsedUrl;

                Messaging.DispatcherInvoke(() =>
                {
                    AnalyzeUrlCommand.Execute(null);
                }, true, false);
            }
        }

        /// <summary>
        /// Is current browsed url a playlist ?
        /// </summary>
        public bool isPlaylist = false;

        /// <summary>
        /// Is current browsed url a playlist ?
        /// </summary>
        public bool IsPlaylist
        {
            get
            {
                return isPlaylist;
            }
            private set
            {
                isPlaylist = value;
                OnPropertyChanged(nameof(IsPlaylist));

                RefreshAllCommands();
            }
        }        

        public RelayCommand OpenSettingsCommand { get; private set; }

        public AsyncRelayCommand AnalyzeUrlCommand { get; private set; }

        public RelayCommand AddJobCommand { get; private set; }

        public AsyncRelayCommand AddPlaylistCommand { get; private set; }        

        public RelayCommand RemoveJobCommand { get; private set; }

        public AsyncRelayCommand StartBatchCommand { get; private set; }
    }
}
