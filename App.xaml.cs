using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace VTStudioToolBox
{
    public partial class App : Application
    {
        private Window? m_window;

        public App()
        {
            this.InitializeComponent();
        }

        protected override void OnLaunched(LaunchActivatedEventArgs args)
        {
            m_window = new MainWindow();

            if (m_window.Content is FrameworkElement rootElement)
            {
                rootElement.RequestedTheme = ElementTheme.Dark;
            }

            m_window.Activate();
        }
    }
}