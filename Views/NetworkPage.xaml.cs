using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using VTStudioToolBox.Helpers;
using VTStudioToolBox.Network;

namespace VTStudioToolBox.Views
{
    public sealed partial class NetworkPage : Page
    {
        public NetworkPage()
        {
            this.InitializeComponent();
            UpdateLanguage();
        }

        private void UpdateLanguage()
        {
            PageTitle.Text = LanguageHelper.GetString("NetworkTitle");
            PageSubtitle.Text = LanguageHelper.GetString("NetworkSubtitle");
            NATTypeHeader.Text = LanguageHelper.GetString("NATTypeDetection");
            STUNServerLabel.Text = LanguageHelper.GetString("STUNServer");
            TestButtonText.Text = LanguageHelper.GetString("ButtonStartTest");
            ReduceNatButtonText.Text = LanguageHelper.GetString("ButtonReduceNAT");

            LabelNATType.Text = LanguageHelper.GetString("LabelNATType");
            LabelLocalAddress.Text = LanguageHelper.GetString("LabelLocalAddress");
            LabelPublicAddress.Text = LanguageHelper.GetString("LabelPublicAddress");

            LabelBindingTest.Text = LanguageHelper.GetString("LabelBindingTest");
            LabelMappingBehavior.Text = LanguageHelper.GetString("LabelMappingBehavior");
            LabelFilteringBehavior.Text = LanguageHelper.GetString("LabelFilteringBehavior");
            LabelRfc5780Local.Text = LanguageHelper.GetString("LabelLocalAddress");
            LabelRfc5780Public.Text = LanguageHelper.GetString("LabelPublicAddress");
            LabelOtherAddress.Text = LanguageHelper.GetString("LabelOtherAddress");
        }

        private async void TestButton_Click(object sender, RoutedEventArgs e)
        {
            var selectedItem = StunServerComboBox.SelectedItem as ComboBoxItem;
            string serverText = selectedItem?.Content?.ToString() ?? "stun.hot-chilli.net:3478";

            TestButton.IsEnabled = false;
            TestProgress.IsActive = true;
            Rfc3489ResultBorder.Visibility = Visibility.Collapsed;
            Rfc5780ResultBorder.Visibility = Visibility.Collapsed;

            Logger.Info("Network", $"Starting NAT type test with server: {serverText}");

            try
            {
                if (!StunServer.TryParse(serverText, out var stunServer))
                {
                    NatTypeText.Text = "Invalid STUN server address";
                    Rfc3489ResultBorder.Visibility = Visibility.Visible;
                    return;
                }

                IPAddress serverIp = await DnsResolver.ResolveAsync(stunServer.Hostname) ?? throw new Exception("Cannot resolve");
                var serverEndPoint = new IPEndPoint(serverIp, stunServer.Port);

                Logger.Info("Network", $"Connecting to {serverEndPoint}");

                // Run both tests in parallel
                var rfc3489Task = RunRfc3489Test(serverEndPoint);
                var rfc5780Task = RunRfc5780Test(serverEndPoint);

                await Task.WhenAll(rfc3489Task, rfc5780Task);

                var rfc3489Result = await rfc3489Task;
                var rfc5780Result = await rfc5780Task;

                DispatcherQueue.TryEnqueue(() =>
                {
                    // RFC 3489 results
                    NatTypeText.Text = GetNatTypeDisplayName(rfc3489Result.NatType);
                    LocalEndPointText.Text = rfc3489Result.LocalEndPoint?.ToString() ?? "Unknown";
                    PublicEndPointText.Text = rfc3489Result.PublicEndPoint?.ToString() ?? "Unknown";

                    NatTypeText.Foreground = rfc3489Result.NatType switch
                    {
                        NatType.OpenInternet or NatType.FullCone
                            => (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["SectionGreenBrush"],
                        NatType.Symmetric or NatType.UdpBlocked or NatType.UnsupportedServer or NatType.Unknown
                            => (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["DangerRedBrush"],
                        _ => (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["SectionOrangeBrush"]
                    };

                    Rfc3489ResultBorder.Visibility = Visibility.Visible;

                    // RFC 5780 results
                    BindingTestText.Text = GetBindingTestDisplayName(rfc5780Result.BindingTestResult);
                    MappingBehaviorText.Text = GetMappingBehaviorDisplayName(rfc5780Result.MappingBehavior);
                    FilteringBehaviorText.Text = GetFilteringBehaviorDisplayName(rfc5780Result.FilteringBehavior);
                    Rfc5780LocalText.Text = rfc5780Result.LocalEndPoint?.ToString() ?? "Unknown";
                    Rfc5780PublicText.Text = rfc5780Result.PublicEndPoint?.ToString() ?? "Unknown";
                    OtherAddressText.Text = rfc5780Result.OtherEndPoint?.ToString() ?? "Unknown";

                    BindingTestText.Foreground = rfc5780Result.BindingTestResult switch
                    {
                        BindingTestResult.Success
                            => (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["SectionGreenBrush"],
                        BindingTestResult.Fail
                            => (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["DangerRedBrush"],
                        _ => (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["SectionOrangeBrush"]
                    };

                    MappingBehaviorText.Foreground = rfc5780Result.MappingBehavior switch
                    {
                        MappingBehavior.Direct or MappingBehavior.EndpointIndependent
                            => (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["SectionGreenBrush"],
                        MappingBehavior.Fail or MappingBehavior.UnsupportedServer
                            => (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["DangerRedBrush"],
                        _ => (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["SectionOrangeBrush"]
                    };

                    FilteringBehaviorText.Foreground = rfc5780Result.FilteringBehavior switch
                    {
                        FilteringBehavior.EndpointIndependent
                            => (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["SectionGreenBrush"],
                        FilteringBehavior.UnsupportedServer
                            => (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["DangerRedBrush"],
                        _ => (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["SectionOrangeBrush"]
                    };

                    Rfc5780ResultBorder.Visibility = Visibility.Visible;

                    Logger.Info("Network", $"Tests completed. RFC3489: {rfc3489Result.NatType}, RFC5780: Mapping={rfc5780Result.MappingBehavior}, Filtering={rfc5780Result.FilteringBehavior}");
                });
            }
            catch (Exception ex)
            {
                Logger.Error("Network", "NAT type test failed", ex);
                DispatcherQueue.TryEnqueue(() =>
                {
                    NatTypeText.Text = $"Test failed: {ex.Message}";
                    NatTypeText.Foreground = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["DangerRedBrush"];
                    Rfc3489ResultBorder.Visibility = Visibility.Visible;
                });
            }
            finally
            {
                DispatcherQueue.TryEnqueue(() =>
                {
                    TestButton.IsEnabled = true;
                    TestProgress.IsActive = false;
                });
            }
        }

        private async Task<ClassicStunResult> RunRfc3489Test(IPEndPoint serverEndPoint)
        {
            await using var client = new StunClient(serverEndPoint);
            return await client.QueryAsync();
        }

        private async Task<Stun5780Result> RunRfc5780Test(IPEndPoint serverEndPoint)
        {
            await using var client = new Stun5780Client(serverEndPoint);
            return await client.QueryAsync();
        }

        private static string GetNatTypeDisplayName(NatType type)
        {
            return type switch
            {
                NatType.OpenInternet => "Open Internet",
                NatType.FullCone => "Full Cone",
                NatType.RestrictedCone => "Restricted Cone",
                NatType.PortRestrictedCone => "Port Restricted Cone",
                NatType.Symmetric => "Symmetric",
                NatType.SymmetricUdpFirewall => "Symmetric UDP Firewall",
                NatType.UdpBlocked => "UDP Blocked",
                NatType.UnsupportedServer => "Unsupported Server",
                _ => "Unknown"
            };
        }

        private static string GetBindingTestDisplayName(BindingTestResult result)
        {
            return result switch
            {
                BindingTestResult.Success => "Success",
                BindingTestResult.Fail => "Fail",
                BindingTestResult.UnsupportedServer => "Unsupported Server",
                _ => "Unknown"
            };
        }

        private static string GetMappingBehaviorDisplayName(MappingBehavior behavior)
        {
            return behavior switch
            {
                MappingBehavior.Direct => "Direct (No NAT)",
                MappingBehavior.EndpointIndependent => "Endpoint Independent",
                MappingBehavior.AddressDependent => "Address Dependent",
                MappingBehavior.AddressAndPortDependent => "Address and Port Dependent",
                MappingBehavior.UnsupportedServer => "Unsupported Server",
                MappingBehavior.Fail => "Fail",
                _ => "Unknown"
            };
        }

        private static string GetFilteringBehaviorDisplayName(FilteringBehavior behavior)
        {
            return behavior switch
            {
                FilteringBehavior.EndpointIndependent => "Endpoint Independent",
                FilteringBehavior.AddressDependent => "Address Dependent",
                FilteringBehavior.AddressAndPortDependent => "Address and Port Dependent",
                FilteringBehavior.UnsupportedServer => "Unsupported Server",
                _ => "Unknown"
            };
        }

        private async void ReduceNatButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                await Windows.System.Launcher.LaunchUriAsync(new Uri(
                    "https://blog.csdn.net/2201_76092265/article/details/154753462?spm=1001.2014.3001.5501"));
            }
            catch (Exception ex)
            {
                Logger.Error("Network", "Failed to open URL", ex);
            }
        }
    }
}
