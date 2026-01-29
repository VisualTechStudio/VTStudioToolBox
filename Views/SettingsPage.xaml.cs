using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.IO;
using Windows.System;

namespace VTStudioToolBox.Views
{
    public sealed partial class SettingsPage : Page
    {
        private const string EULA_FOLDER_NAME = "VTStudioToolBox";
        private const string EULA_FILE_NAME = "Eula.txt";

        public SettingsPage()
        {
            this.InitializeComponent();

            VersionTextBlock.Text = $"版本: {Cfg.AppVersion}";
            LogTextBlock.Text = $"{ChangeLog.Log}";
        }

        private async void WebsiteButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var uri = new Uri(Cfg.Website);
                await Launcher.LaunchUriAsync(uri);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"打开官方网站失败: {ex.Message}");

                var dialog = new ContentDialog
                {
                    Title = "操作失败",
                    Content = "无法打开官方网站，请检查网络连接或稍后重试。",
                    CloseButtonText = "确定",
                    XamlRoot = this.XamlRoot
                };

                await dialog.ShowAsync();
            }
        }

        private async void GitHubButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var uri = new Uri(Cfg.GithubRepo);
                await Launcher.LaunchUriAsync(uri);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"打开GitHub失败: {ex.Message}");

                var dialog = new ContentDialog
                {
                    Title = "操作失败",
                    Content = "无法打开GitHub页面，请检查网络连接或稍后重试。",
                    CloseButtonText = "确定",
                    XamlRoot = this.XamlRoot
                };

                await dialog.ShowAsync();
            }
        }

        private async void GPLV3Button_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var uri = new Uri(Cfg.GPLV3);
                await Launcher.LaunchUriAsync(uri);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"打开GPLv3协议失败: {ex.Message}");

                var dialog = new ContentDialog
                {
                    Title = "操作失败",
                    Content = "无法打开GPLv3协议文档，请检查网络连接或稍后重试。",
                    CloseButtonText = "确定",
                    XamlRoot = this.XamlRoot
                };

                await dialog.ShowAsync();
            }
        }

        private async void RevokeEulaButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                string folderPath = Path.Combine(localAppData, EULA_FOLDER_NAME);
                string filePath = Path.Combine(folderPath, EULA_FILE_NAME);

                if (File.Exists(filePath))
                {
                    File.Delete(filePath);

                    var dialog = new ContentDialog
                    {
                        Title = "您已撤销同意用户协议",
                        Content = "软件即将退出，下次启动将重新显示协议页面，再见",
                        CloseButtonText = "确定",
                        XamlRoot = this.XamlRoot
                    };

                    await dialog.ShowAsync();
                }
                else
                {
                    var dialog = new ContentDialog
                    {
                        Title = "提示",
                        Content = "未找到同意记录，无需撤销。",
                        CloseButtonText = "确定",
                        XamlRoot = this.XamlRoot
                    };

                    await dialog.ShowAsync();
                    return;
                }

                Application.Current.Exit();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"撤销 EULA 失败: {ex.Message}");

                var dialog = new ContentDialog
                {
                    Title = "操作失败",
                    Content = "撤销同意时发生错误，请稍后重试。",
                    CloseButtonText = "确定",
                    XamlRoot = this.XamlRoot
                };

                await dialog.ShowAsync();
            }
        }
    }
}