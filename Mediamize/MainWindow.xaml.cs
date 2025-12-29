using Mediamize.ViewModel;
using System.Security.Cryptography;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using zComp.Wpf;
using zComp.Wpf.Helpers;

namespace Mediamize
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : zSmoothWindow
    {
        public MainWindow(MMMainViewModel vm)
        {
            InitializeComponent();
            DataContext = vm;

            lbAvailableFormats.GroupStyle.Clear();
            lbAvailableFormats.GroupStyle.Add(WpfHelper.GetApplicationResource<GroupStyle>("FormatsGroupStyle"));
        }

        private void DialogPresenter_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            var fe = (FrameworkElement)sender;
            if (!fe.IsVisible && !this.IsFocused)
            {
                this.Focus();
            }
        }

        private void MediaElement_MediaOpened(object sender, RoutedEventArgs e)
        {
            mediaElement.Volume = 0.4;
        }

        private void btBack_Click(object sender, RoutedEventArgs e)
        {
            if (webView.WebView?.CanGoBack == true)
            {
                webView.WebView.GoBack();
            }
        }
    }
}