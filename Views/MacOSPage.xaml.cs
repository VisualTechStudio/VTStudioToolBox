using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Windows.ApplicationModel.DataTransfer;
using VTStudioToolBox.Helpers;

namespace VTStudioToolBox.Views
{
    public sealed partial class MacOSPage : Page
    {
        private const string DMG_DATA_URL = "https://next.oclpapi.simplehac.cn/DMG/data/dmgs.json";
        private const string AES_KEY_URL = "https://next.oclpapi.simplehac.cn/DMG/data/aeskey.txt";
        private const string SIGN_API_URL = "https://node.oclpapi.simplehac.cn/DMG/api/down.php";
        private const string TIME_API_URL = "https://api.suning.com/api/v1/getSysTime.do";
        private const string ETCHER_URL = "https://www.acutesystems.com/dl_tmac.htm";
        private const int MAX_LATEST_DMGS = 4;
        private const int REQUEST_TIMEOUT = 15;

        private static readonly HttpClient _http = new HttpClient { Timeout = TimeSpan.FromSeconds(REQUEST_TIMEOUT) };

        private string? _aesKey;
        private List<JsonElement> _latestDmgs = new();
        private List<JsonElement> _allDmgs = new();
        private bool _showAll = true;
        private bool _dataLoaded = false;
        private string _lastStatusText = "";
        private int _statusTimerCount = 0;

        private readonly ObservableCollection<DmgItem> _displayItems = new();

        public MacOSPage()
        {
            this.InitializeComponent();
            UpdateLanguage();
            DmgListView.ItemsSource = _displayItems;
            this.Loaded += MacOSPage_Loaded;
        }

        private void UpdateLanguage()
        {
            PageTitle.Text = LanguageHelper.GetString("HackintoshTitle");
            PageSubtitle.Text = LanguageHelper.GetString("HackintoshSubtitle");
            BtnToggleLatestText.Text = LanguageHelper.GetString("ButtonShowLatest");
            BtnRefreshText.Text = LanguageHelper.GetString("ButtonRefresh");
            BtnFlashDmgText.Text = LanguageHelper.GetString("ButtonFlashDMG");
            TxtStatus.Text = LanguageHelper.GetString("StatusClickToFetch");
        }

        private async void MacOSPage_Loaded(object sender, RoutedEventArgs e)
        {
            if (!_dataLoaded)
            {
                await FetchDmgsAsync();
            }
        }

        private void BtnToggleLatest_Click(object sender, RoutedEventArgs e)
        {
            _showAll = !_showAll;
            BtnToggleLatestText.Text = _showAll ? LanguageHelper.GetString("ButtonShowLatest") : LanguageHelper.GetString("ButtonShowAll");
            DisplayDmgs();
        }

        private async void BtnRefresh_Click(object sender, RoutedEventArgs e)
        {
            _dataLoaded = false;
            await FetchDmgsAsync();
        }

        private void BtnFlashDmg_Click(object sender, RoutedEventArgs e)
        {
            Process.Start(new ProcessStartInfo(ETCHER_URL) { UseShellExecute = true });
        }

        private async Task FetchDmgsAsync()
        {
            SetStatus(LanguageHelper.GetString("StatusFetchingDMG"));
            _displayItems.Clear();
            BtnToggleLatest.IsEnabled = false;
            BtnRefresh.IsEnabled = false;

            try
            {
                var dmgResponse = await _http.GetStringAsync(DMG_DATA_URL);
                var aesResponse = await _http.GetStringAsync(AES_KEY_URL);

                _aesKey = aesResponse.Trim();

                using var doc = JsonDocument.Parse(dmgResponse);
                var root = doc.RootElement;
                var files = root.GetProperty("dmgFiles");

                _allDmgs.Clear();
                foreach (var item in files.EnumerateArray())
                {
                    _allDmgs.Add(item.Clone());
                }

                // Deduplicate by build prefix, keep latest per prefix
                var seenPrefixes = new HashSet<string>();
                var latestList = new List<JsonElement>();
                foreach (var item in _allDmgs.AsEnumerable().Reverse())
                {
                    string build = item.GetProperty("build").GetString() ?? "";
                    string prefix = build.Length >= 2 ? build[..2] : build;
                    if (seenPrefixes.Add(prefix))
                    {
                        latestList.Add(item);
                        if (latestList.Count >= MAX_LATEST_DMGS)
                            break;
                    }
                }
                latestList.Reverse();
                _latestDmgs = latestList;

                _dataLoaded = true;
                DisplayDmgs();
            }
            catch (HttpRequestException ex)
            {
                SetStatus(LanguageHelper.GetString("ErrorNetwork", ex.Message));
            }
            catch (Exception ex)
            {
                SetStatus(LanguageHelper.GetString("ErrorFetchFailed", ex.Message));
            }
            finally
            {
                BtnToggleLatest.IsEnabled = true;
                BtnRefresh.IsEnabled = true;
            }
        }

        private void DisplayDmgs()
        {
            _displayItems.Clear();
            var sourceList = (_showAll ? _allDmgs : _latestDmgs);
            var source = sourceList.AsEnumerable().Reverse().ToList();
            string label = _showAll ? LanguageHelper.GetString("LabelAllVersions") : LanguageHelper.GetString("LabelLatestVersion");
            _lastStatusText = LanguageHelper.GetString("StatusDMGCount", source.Count, label);
            TxtStatus.Text = _lastStatusText;

            foreach (var item in source)
            {
                string title = item.TryGetProperty("title", out var t) ? t.GetString() ?? "Unknown" : "Unknown";
                string version = item.TryGetProperty("version", out var v) ? v.GetString() ?? "" : "";
                string build = item.TryGetProperty("build", out var b) ? b.GetString() ?? "" : "";
                string size = item.TryGetProperty("size", out var s) ? s.GetString() ?? "" : "";
                string date = item.TryGetProperty("releaseDate", out var d) ? d.GetString() ?? "" : "";
                if (date.Contains('T'))
                    date = date.Split('T')[0];

                _displayItems.Add(new DmgItem
                {
                    Title = title,
                    Version = version,
                    Build = build,
                    Size = size,
                    Date = date,
                    RawJson = item
                });
            }
        }

        private void SetStatus(string text)
        {
            TxtStatus.Text = text;
            _statusTimerCount = 3;
            _ = RestoreStatusAfterDelay();
        }

        private async Task RestoreStatusAfterDelay()
        {
            while (_statusTimerCount > 0)
            {
                await Task.Delay(1000);
                _statusTimerCount--;
            }
            TxtStatus.Text = _lastStatusText;
        }

        private async void BtnDownloadDmg_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is DmgItem dmg)
            {
                await DownloadDmgAsync(dmg.RawJson);
            }
        }

        private void BtnCopyLink_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is DmgItem dmg)
            {
                _ = CopyLinkToClipboardAsync(dmg);
            }
        }

        private async Task DownloadDmgAsync(JsonElement item)
        {
            if (string.IsNullOrEmpty(_aesKey))
            {
                SetStatus(LanguageHelper.GetString("ErrorAESKeyNotReady"));
                return;
            }

            string downloadUrl = item.TryGetProperty("downloadUrl", out var urlProp) ? urlProp.GetString() ?? "" : "";
            if (string.IsNullOrEmpty(downloadUrl))
            {
                SetStatus(LanguageHelper.GetString("ErrorCannotGetDownloadLink"));
                return;
            }

            string signedUrl = await GenerateSignedUrlAsync(downloadUrl, _aesKey);
            SetStatus(LanguageHelper.GetString("StatusOpeningDownload"));
            Process.Start(new ProcessStartInfo(signedUrl) { UseShellExecute = true });
        }

        private async Task<string> GenerateSignedUrlAsync(string downloadUrl, string aesKey)
        {
            var parsedUri = new Uri(downloadUrl);
            string rawFileName = System.IO.Path.GetFileName(parsedUri.AbsolutePath);
            string fileName = Uri.UnescapeDataString(rawFileName);

            long timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            try
            {
                var timeResponse = await _http.GetAsync(TIME_API_URL);
                if (timeResponse.IsSuccessStatusCode)
                {
                    var timeJson = await timeResponse.Content.ReadAsStringAsync();
                    using var timeDoc = JsonDocument.Parse(timeJson);
                    if (timeDoc.RootElement.TryGetProperty("sysTime", out var sysTimeProp))
                    {
                        string? netTimeStr = sysTimeProp.GetString();
                        if (!string.IsNullOrEmpty(netTimeStr))
                        {
                            var parsedTime = DateTime.ParseExact(netTimeStr, "yyyyMMddHHmmss",
                                System.Globalization.CultureInfo.InvariantCulture);
                            timestamp = new DateTimeOffset(parsedTime).ToUnixTimeSeconds();
                        }
                    }
                }
            }
            catch { }

            long expireTime = timestamp + 300;
            string signData = $"oclpmod{fileName}{expireTime}{aesKey}";

            using var md5 = System.Security.Cryptography.MD5.Create();
            byte[] hash = md5.ComputeHash(Encoding.UTF8.GetBytes(signData));
            string sign = Convert.ToHexString(hash).ToLowerInvariant();

            string encodedFileName = Uri.EscapeDataString(fileName);
            return $"{SIGN_API_URL}?origin={encodedFileName}&sign={sign}&t={expireTime}";
        }

        private async Task CopyLinkToClipboardAsync(DmgItem dmg)
        {
            if (string.IsNullOrEmpty(_aesKey))
            {
                SetStatus(LanguageHelper.GetString("ErrorAESKeyNotReady"));
                return;
            }

            string downloadUrl = dmg.RawJson.TryGetProperty("downloadUrl", out var urlProp) ? urlProp.GetString() ?? "" : "";
            if (string.IsNullOrEmpty(downloadUrl))
            {
                SetStatus(LanguageHelper.GetString("ErrorCannotGetDownloadLink"));
                return;
            }

            string signedUrl = await GenerateSignedUrlAsync(downloadUrl, _aesKey);
            var package = new DataPackage();
            package.SetText(signedUrl);
            Clipboard.SetContent(package);

            SetStatus(LanguageHelper.GetString("StatusLinkCopied"));
        }
    }

    public class DmgItem
    {
        public string Title { get; set; } = "";
        public string Version { get; set; } = "";
        public string Build { get; set; } = "";
        public string Size { get; set; } = "";
        public string Date { get; set; } = "";
        public JsonElement RawJson { get; set; }
    }
}
