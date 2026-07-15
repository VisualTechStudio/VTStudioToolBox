using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using VTStudioToolBox.Helpers;
using VTStudioToolBox.Views;

namespace VTStudioToolBox
{
    public partial class App : Application
    {
        internal Window? m_window;

        public App()
        {
            this.InitializeComponent();
        }

        protected override void OnLaunched(LaunchActivatedEventArgs args)
        {
            Logger.Init();
            Logger.Info("App", "OnLaunched");

            LanguageHelper.Initialize();

            FirewallHelper.EnsureFirewallRule();

            m_window = new MainWindow();
            WindowHelper.SetWindow(m_window);

            if (m_window.Content is FrameworkElement rootElement)
            {
                rootElement.RequestedTheme = ElementTheme.Dark;
            }

            m_window.Activate();

            AdbCache.Start();
        }
    }
}
