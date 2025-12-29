using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;
using Newtonsoft.Json.Linq;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using zComp.Core.Helpers;
using zComp.Wpf.Helpers;

namespace Mediamize.View
{
    /// <summary>
    /// Logique d'interaction pour EnhancedWebBrowser.xaml
    /// </summary>
    public partial class EnhancedWebBrowser : UserControl
    {
        static EnhancedWebBrowser()
        {
            Environment.SetEnvironmentVariable("WEBVIEW2_ADDITIONAL_BROWSER_ARGUMENTS", "--autoplay-policy=no-user-gesture-required");
        }

        public static readonly DependencyProperty WebViewProperty = DependencyProperty.RegisterAttached("WebView", typeof(WebView2),
            typeof(EnhancedWebBrowser), new PropertyMetadata(null, WebViewProperty_PropertyChangedCallback));

        private static void WebViewProperty_PropertyChangedCallback(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ((EnhancedWebBrowser)d).SetWebView((WebView2)e.NewValue);
        }

        private void SetWebView(WebView2 WebView)
        {
        }

        public WebView2 WebView
        {
            get
            {
                return (WebView2)GetValue(WebViewProperty);
            }
            private set
            {
                SetValue(WebViewProperty, value);
            }
        }


        public static readonly DependencyProperty SourceProperty = DependencyProperty.RegisterAttached("Source", typeof(string),
            typeof(EnhancedWebBrowser), new PropertyMetadata(null, SourceProperty_PropertyChangedCallback));

        private static void SourceProperty_PropertyChangedCallback(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ((EnhancedWebBrowser)d).SetSource((string)e.NewValue);
        }

        private void SetSource(string source)
        {
            Navigate();
        }

        public string Source
        {
            get
            {
                return (string)GetValue(SourceProperty);
            }
            set
            {
                SetValue(SourceProperty, value);
            }
        }

        public static readonly DependencyProperty IsActiveProperty = DependencyProperty.RegisterAttached("IsActive", typeof(bool),
            typeof(EnhancedWebBrowser), new PropertyMetadata(false, IsActiveProperty_PropertyChangedCallback));

        private static void IsActiveProperty_PropertyChangedCallback(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ((EnhancedWebBrowser)d).SetIsActive((bool)e.NewValue);
        }

        private void SetIsActive(bool isActive)
        {
            if (isActive)
            {
                Navigate();
            }
            else
            {
                DisposeWebView();
            }
        }

        public bool IsActive
        {
            get
            {
                return (bool)GetValue(IsActiveProperty);
            }
            set
            {
                SetValue(IsActiveProperty, value);
            }
        }


        public static readonly DependencyProperty CanNavigateProperty = DependencyProperty.RegisterAttached("CanNavigate", typeof(bool),
            typeof(EnhancedWebBrowser), new PropertyMetadata(false, CanNavigateProperty_PropertyChangedCallback));

        private static void CanNavigateProperty_PropertyChangedCallback(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ((EnhancedWebBrowser)d).SetCanNavigate((bool)e.NewValue);
        }

        private void SetCanNavigate(bool value)
        {
            if (WebView != null)
            {
                WebView.CoreWebView2.Settings.AreDefaultContextMenusEnabled = value;
            }
        }

        public bool CanNavigate
        {
            get
            {
                return (bool)GetValue(CanNavigateProperty);
            }
            set
            {
                SetValue(CanNavigateProperty, value);
            }
        }



        public static readonly DependencyProperty DefaultBackgroundColorProperty = DependencyProperty.RegisterAttached("DefaultBackgroundColor", typeof(System.Drawing.Color),
            typeof(EnhancedWebBrowser), new PropertyMetadata(System.Drawing.Color.White, DefaultBackgroundColorProperty_PropertyChangedCallback));

        private static void DefaultBackgroundColorProperty_PropertyChangedCallback(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ((EnhancedWebBrowser)d).SetDefaultBackgroundColor((System.Drawing.Color)e.NewValue);
        }

        private void SetDefaultBackgroundColor(System.Drawing.Color value)
        {
            if (WebView != null)
            {
                WebView.DefaultBackgroundColor = value;
            }
        }

        public System.Drawing.Color DefaultBackgroundColor
        {
            get
            {
                return (System.Drawing.Color)GetValue(DefaultBackgroundColorProperty);
            }
            set
            {
                SetValue(DefaultBackgroundColorProperty, value);
            }
        }


        public EnhancedWebBrowser()
        {
            InitializeComponent();
            InitializeAsync();

            this.Unloaded += EnhancedWebBrowser_Unloaded;
            this.Loaded += EnhancedWebBrowser_Loaded;
        }

        private void EnhancedWebBrowser_Loaded(object sender, RoutedEventArgs e)
        {
            //var mainRotation = WpfHelper.FindParentControl<LauncherView>(this)?.LayoutTransform as RotateTransform;
            //
            //if (mainRotation != null)
            //{
            //    this.LayoutTransform = new RotateTransform() { Angle = -mainRotation.Angle };
            //}

            Navigate();
        }

        private void EnhancedWebBrowser_Unloaded(object sender, RoutedEventArgs e)
        {
            DisposeWebView();
        }

        private void DisposeWebView()
        {
            if(WebView != null)
            {
                WebView.Loaded -= WebView_Loaded;
                WebView.Dispose();
                Content = null;
                WebView = null;
            }
        }

        //private WebView2 WebView;

        private static bool webViewInitialized = false;

        private void InitializeAsync()
        {
            //var opt = new CoreWebView2EnvironmentOptions("--autoplay-policy=no-user-gesture-required");
            //var path = @"C:\Program Files (x86)\Microsoft\EdgeWebView\Application\92.0.902.67";

            WebView = new WebView2();
            WebView.DefaultBackgroundColor = DefaultBackgroundColor;
            WebView.Loaded += WebView_Loaded;

            var panel = new System.Windows.Controls.DockPanel();
            panel.HorizontalAlignment = HorizontalAlignment.Stretch;
            panel.VerticalAlignment = VerticalAlignment.Stretch;
            panel.Children.Add(WebView);
            Content = panel;
        }
    
        private async void WebView_Loaded(object sender, RoutedEventArgs e)
        {
            if(WebView == null)
            {
                return;
            }

            WebView.Loaded -= WebView_Loaded;

            var dataFolder = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"{System.IO.Path.GetFileNameWithoutExtension(Assembly.GetEntryAssembly().Location)}.{nameof(WebView2)}");
            var env = await Microsoft.Web.WebView2.Core.CoreWebView2Environment.CreateAsync(userDataFolder: dataFolder).ConfigureAwait(true);

            if(WebView == null)
            {
                return;
            }

            try
            {
                await WebView.EnsureCoreWebView2Async(environment: env).ConfigureAwait(true);
            }
            catch
            {
            }

            if(WebView == null)
            {
                return;
            }

            webViewInitialized = true;

            WpfHelper.DoEvents();

            if(WebView == null)
            {
                return;
            }

            WebView.NavigationStarting += WebView_NavigationStarting;
            WebView.NavigationCompleted += WebView_NavigationCompleted;
            WebView.SourceChanged += WebView_SourceChanged;
            WebView.CoreWebView2.Settings.AreDefaultContextMenusEnabled = CanNavigate;

            Navigate();

            //WpfHelper.DoEvents();

            //if(webView == null)
            //{
            //    return;
            //}
        }


        //private void WebView_ContextMenuOpening(object sender, ContextMenuEventArgs e)
        //{
        //    e.Handled = !CanNavigate;
        //}

        private bool firstNavigation = true;

        private void WebView_NavigationStarting(object sender, CoreWebView2NavigationStartingEventArgs e)
        {
            if (firstNavigation)
            {
                firstNavigation = false;
            }
            else
            {
                e.Cancel = CanNavigate;
            }
        }

        private int settingSource = 0;

        private void WebView_NavigationCompleted(object sender, CoreWebView2NavigationCompletedEventArgs e)
        {
        }

        private void WebView_SourceChanged(object sender, CoreWebView2SourceChangedEventArgs e)
        {
            if (navigating == 0)
            {
                var s = WebView.Source?.ToString();

                if (s?.Equals(Source) == false)
                {
                    settingSource++;

                    try
                    {
                        Source = s;
                    }
                    finally
                    {
                        settingSource--;
                    }
                }
            }
        }

        private int navigating = 0;

        private void Navigate()
        {
            if(!webViewInitialized || WebView?.CoreWebView2 == null || settingSource > 0)
            {
                return;
            }

            navigating++;

            try
            {
                var s = Source;

                if (!string.IsNullOrWhiteSpace(s))
                {
                    firstNavigation = true;

                    if (!string.IsNullOrWhiteSpace(s) && Uri.TryCreate(s, UriKind.RelativeOrAbsolute, out var uri))
                    {
                        s = NetHelper.NormalizeUrl(s);

                        if (s == null)
                        {

                        }

                        if (Uri.TryCreate(s, UriKind.Absolute, out uri))
                        {
                            WebView.Source = uri;
                        }
                        else
                        {
                            WebView.NavigateToString(Source);
                        }
                    }
                    else
                    {
                        WebView.NavigateToString(Source);
                    }

                    //if(Uri.TryCreate(Source, UriKind.Absolute, out var uri))
                    //{
                    //    webView.Source = uri;
                    //}
                    //else
                    //{
                    //    webView.NavigateToString(Source);
                    //}

                    WpfHelper.DoEvents();
                }
            }
            finally
            {
                navigating--;
            }
        }
    }
}
