using Mediamize.Model;
using Mediamize.Services;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.ObjectModel;
using System.Windows;
using zComp.Core.Model;
using zComp.Wpf.ViewModel;

namespace Mediamize.ViewModel
{
    /// <summary>
    /// ApplicationViewModel
    /// </summary>
    public class MMApplicationViewModel : ApplicationViewModel<MMApplicationViewModel, MMMainViewModel, MMApplicationRepository>
    {
        /// <summary>
        /// Download service
        /// </summary>
        private IDownloadService _downloadService;

        /// <summary>
        /// Constructor
        /// </summary>
        public MMApplicationViewModel()
        {
#if DEBUG
            JustInTimeTranslation = new JustInTimeTranslationViewModel();
#endif

            RegistredSounds.Add("SND_START", @"Sounds\started.mp3");
            RegistredSounds.Add("SND_END", @"Sounds\ended.mp3");
            RegistredSounds.Add("SND_SELECT", @"Sounds\select.mp3");
            RegistredSounds.Add("SND_FAILURE", @"Sounds\failure.mp3");
            RegistredSounds.Add("SND_WARNING", @"Sounds\unauthorized.mp3");

            BackgroundSource = null;
            LoginBackgroundSource = null;
        }

        /// <summary>
        /// Initialization of the application
        /// </summary>
        public override void Initialize()
        {
            base.Initialize();

            Repository.ServiceCollection.AddSingleton<IDownloadService, DownloadService>();
            Repository.ServiceCollection.AddSingleton<SettingsViewModel>();
            Repository.ServiceCollection.AddTransient<LogViewModel>();
            Repository.BuildServices();


            //Repository.ServiceCollection.AddSingleton(s => MMApplicationRepository.Instance);
            //Repository.ServiceCollection.AddSingleton(s => MMApplicationViewModel.Instance);
            //Repository.ServiceCollection.AddSingleton(s => MMMainViewModel.Instance);

            //var applicationRepository = MMApplicationRepository.Instance;
            //var applicationViewModel = MMApplicationViewModel.Instance;
            //var mainViewModel = MMMainViewModel.Instance;

            _downloadService = Services.GetService<IDownloadService>();
            _downloadService.CurrentConfig = Repository.LocalConfiguration;

#if DEBUG
            Labels.MissingTranslationAction = s => JustInTimeTranslation?.Add(s);
#endif

            Connect(1, new UserConfiguration());

            if (!string.IsNullOrWhiteSpace(Repository.LocalConfiguration.LastURL))
            {
                MainViewModel.CurrentUrl = Repository.LocalConfiguration.LastURL;
            }

            Title = ApplicationTitle = ApplicationTitleShort = "Mediamize";
        }


#if DEBUG

        private JustInTimeTranslationViewModel justInTimeTranslation;

        public JustInTimeTranslationViewModel JustInTimeTranslation
        {
            get
            {
                return justInTimeTranslation;
            }
            private set
            {
                justInTimeTranslation = value;
                OnPropertyChanged(nameof(JustInTimeTranslation));
            }
        }

#endif

    }
}
