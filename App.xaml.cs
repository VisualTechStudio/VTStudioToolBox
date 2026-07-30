using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using VTStudioToolBox.Auth;
using VTStudioToolBox.Helpers;
using VTStudioToolBox.Services;
using VTStudioToolBox.ViewModels;
using VTStudioToolBox.Views;

namespace VTStudioToolBox
{
    public partial class App : Application
    {
        internal Window? m_window;

        public static IServiceProvider Services { get; private set; } = null!;

        public App()
        {
            this.InitializeComponent();
        }

        protected override void OnLaunched(LaunchActivatedEventArgs args)
        {
            Logger.Init();
            Logger.Info("App", "OnLaunched");

            LanguageHelper.Initialize();
            ThemeHelper.Initialize();

            // ── DI Registration ──
            Services = ConfigureServices();

            FirewallHelper.EnsureFirewallRule();

            m_window = new MainWindow();
            WindowHelper.SetWindow(m_window);

            if (m_window.Content is FrameworkElement rootElement)
            {
                rootElement.RequestedTheme = ThemeHelper.CurrentTheme;
            }

            m_window.Activate();

            AdbCache.Start();

            // Fire-and-forget: track app launch in background
            _ = TrackAppLaunchAsync();
        }

        private static ServiceProvider ConfigureServices()
        {
            var services = new ServiceCollection();

            // Core services (singletons)
            services.AddSingleton<HardwareCollector>();
            services.AddSingleton<IHardwareCollector>(sp => sp.GetRequiredService<HardwareCollector>());
            services.AddSingleton<IAuthService, AuthManager>();
            services.AddSingleton<IAnalyticsService, AnalyticsService>(sp =>
                new AnalyticsService(sp.GetRequiredService<HardwareCollector>()));

            // ViewModels
            services.AddSingleton<UserViewModel>();

            return services.BuildServiceProvider();
        }

        private static async System.Threading.Tasks.Task TrackAppLaunchAsync()
        {
            try
            {
                var hw = Services.GetRequiredService<IHardwareCollector>();
                var analytics = Services.GetRequiredService<IAnalyticsService>();
                analytics.TrackAppLaunch(hw.Collect());
            }
            catch (Exception ex)
            {
                Logger.Warn("App", $"Failed to track launch: {ex.Message}");
            }
        }
    }
}
