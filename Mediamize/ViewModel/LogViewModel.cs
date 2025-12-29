using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Mediamize.Model;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Media;
using zComp.Wpf.ViewModel;

namespace Mediamize.ViewModel
{
    /// <summary>
    /// ViewModel for scrapping logs
    /// </summary>
    public partial class LogViewModel : CardViewModel
    {
        /// <summary>
        /// CancellationTokenSource
        /// </summary>
        private CancellationTokenSource _cts;

        /// <summary>
        /// Constructor
        /// </summary>
        public LogViewModel()
        {
            Title = "EXTRACTION_PROCESS";
            Width = 1024;
            Height = 600;

            StopCommand = new RelayCommand(Stop, () => IsRunning);
        }

        /// <summary>
        /// Initialization
        /// </summary>
        /// <param name="cts"></param>
        public void Initialize(CancellationTokenSource cts)
        {
            _cts = cts;
            Logs.Clear();
        }

        /// <summary>
        /// Add a log entry
        /// </summary>
        /// <param name="entry"></param>
        public void AddLog(LogEntry entry)
        {
            // Dispatcher nécessaire car appelé depuis thread background
            Messaging.DispatcherInvoke(() =>
            {
                Logs.Add(entry);
            }, false, false);
        }

        /// <summary>
        /// Stops scrapping process
        /// </summary>
        private void Stop()
        {
            _cts?.Cancel();
            AddLog(new LogEntry("Annulation demandée...", Brushes.Orange));
        }

        /// <summary>
        /// Scrapping process is running ?
        /// </summary>
        private bool isRunning;

        /// <summary>
        /// Scrapping process is running ?
        /// </summary>
        public bool IsRunning
        {
            get
            {
                return isRunning;
            }
            set
            {
                isRunning = value;
                OnPropertyChanged(nameof(IsRunning));

                StopCommand.NotifyCanExecuteChanged();
            }
        }

        /// <summary>
        /// Scrapping logs
        /// </summary>
        private ObservableCollection<LogEntry> logs = new();

        /// <summary>
        /// Scrapping logs
        /// </summary>
        public ObservableCollection<LogEntry> Logs
        {
            get
            {
                return logs;
            }
            private set
            {
                logs = value;
                OnPropertyChanged(nameof(Logs));
            }
        }

        /// <summary>
        /// Scrapping process stop command
        /// </summary>
        public RelayCommand StopCommand { get; private set; }
    }
}
