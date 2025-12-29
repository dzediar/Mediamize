using Mediamize.Model;
using Mediamize.Services;
using Mediamize.ViewModel;
using Microsoft.Extensions.DependencyInjection;
using System.Configuration;
using System.Data;
using System.Windows;
using zComp.Wpf.Model;
using zComp.Wpf.ViewModel;

namespace Mediamize
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        public App()
        {
            DispatcherUnhandledException += App_DispatcherUnhandledException;
            zComp.Wpf.ViewModel.MVVMMessageResponses.Instance.RegisterMessageResponses();
            MVVMMessageResponses.Instance.ShowNormalDialogs = true;
            MMApplicationViewModel.Instance.Initialize();
        }

        private void OnStartup(object sender, StartupEventArgs e)
        {
            //var mainWindow = Services.GetRequiredService<MainWindow>();

            //// Vérification config au démarrage
            //while (!MMApplicationViewModel.Instance.LocalConfiguration.IsValid())
            //{
            //    var vm = MMApplicationViewModel.Instance.Services.GetRequiredService<SettingsViewModel>();
            //
            //    if (MVVMMessageNotifications.Instance.ShowModal(vm) == true)
            //    {
            //        vm.SaveToConfig();
            //    }
            //}
                        
            this.MainWindow = new MainWindow(MMApplicationViewModel.Instance.MainViewModel);
            //this.MainWindow.ShowActivated = MMApplicationViewModel.Instance.LocalConfiguration.IsValid() ? true : false;
            this.MainWindow.Show();

            //if (Application.Current.Windows.Count > 1)
            //{
            //    Application.Current.Windows.OfType<Window>().FirstOrDefault(w => w != this.MainWindow).Activate();
            //}
        }

        protected override void OnExit(ExitEventArgs e)
        {
            //GestionDroitsUtilisateursHelper.Quit();

            ApplicationViewModel.Instance.Quit();

            base.OnExit(e);
        }

        private void App_DispatcherUnhandledException(object sender, System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e)
        {
            ApplicationViewModel.Instance.ManageUnhandledException(e);
        }
    }
}
