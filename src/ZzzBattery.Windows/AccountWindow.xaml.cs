using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Windows;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;
using Microsoft.Win32;
using ZzzBattery.Core;

namespace ZzzBattery.Windows;

public partial class AccountWindow : Window
{
    internal bool BrowserReady { get; private set; }
    internal bool OfficialPageLoaded { get; private set; }
    private readonly CancellationTokenSource _cancel = new();
    private readonly OfficialPopupHost _popups;
    private Credential? _candidate;
    private bool _import, _closed, _initializing, _browserFailed;
    private int _generation;
    public AccountWindow()
    {
        InitializeComponent();
        _popups = new OfficialPopupHost(this, text => { if (!_closed) Status.Text = text; });
        Width = Math.Min(1180, SystemParameters.WorkArea.Width - 40);
        Height = Math.Min(760, SystemParameters.WorkArea.Height - 40);
        Loaded += async (_, _) => await InitializeBrowser();
        Closed += (_, _) => { _closed = true; BrowserReady = false; _cancel.Cancel(); _popups.CloseAll(); Browser.Dispose(); _cancel.Dispose(); };
        CookieInput.PasswordChanged += (_, _) => ResetCandidate();
    }
    private void ResetCandidate() { _generation++; _candidate = null; Roles.ItemsSource = null; Roles.Visibility = Visibility.Collapsed; UseButton.IsEnabled = false; }
    private void QrLoginClick(object sender, RoutedEventArgs e)
    {
        ResetCandidate();
        var login = new QrLoginWindow { Owner = this };
        if (login.ShowDialog() != true || login.Credential is null || _closed) return;
        CookieInput.Clear(); _candidate = login.Credential;
        Roles.ItemsSource = login.Roles; Roles.SelectedIndex = 0; Roles.Visibility = Visibility.Visible;
        UseButton.IsEnabled = true;
        Status.Text = $"扫码成功，已取得小组件登录凭据。找到 {login.Roles.Count} 个角色，选择后点击“使用此角色”。";
    }
    private async System.Threading.Tasks.Task InitializeBrowser()
    {
        if (_initializing || BrowserReady || _closed) return;
        if (_browserFailed)
        {
            Status.Text = "登录浏览器已退出，请关闭此窗口后重新打开，或使用手动导入凭据。";
            return;
        }
        _initializing = true;
        try
        {
            Browser.CreationProperties = new CoreWebView2CreationProperties
            {
                UserDataFolder = Path.Combine(App.Current.Store.Root, "WebViewProfile"),
                IsInPrivateModeEnabled = true
            };
            await Browser.EnsureCoreWebView2Async();
            if (_closed || Browser.CoreWebView2 is null) return;
            BrowserReady = true;
            Browser.ZoomFactor = Math.Clamp(Browser.ActualWidth / 1440d, .4, 1);
            Browser.CoreWebView2.ProcessFailed += (_, e) =>
            {
                if (_closed) return;
                OfficialPageLoaded = false;
                if (e.ProcessFailedKind == CoreWebView2ProcessFailedKind.BrowserProcessExited)
                {
                    BrowserReady = false; _browserFailed = true;
                    Status.Text = "登录浏览器已退出，请关闭此窗口后重新打开，或使用手动导入凭据。";
                }
                else Status.Text = "登录网页暂时无法显示，请点击“重载网页”重试。";
            };
            Browser.CoreWebView2.Settings.IsPasswordAutosaveEnabled = false;
            Browser.CoreWebView2.Settings.IsGeneralAutofillEnabled = false;
            Browser.CoreWebView2.NavigationStarting += (_, e) =>
            {
                if (!AllowedNavigation(e.Uri)) { e.Cancel = true; Status.Text = "已阻止非米游社官方页面跳转。"; }
                else AddressText.Text = new Uri(e.Uri).GetLeftPart(UriPartial.Path);
            };
            _popups.Attach(Browser.CoreWebView2);
            Browser.CoreWebView2.DownloadStarting += (_, e) => e.Cancel = true;
            Browser.CoreWebView2.NavigationCompleted += async (_, e) =>
            {
                if (_closed) return;
                OfficialPageLoaded = e.IsSuccess;
                if (e.IsSuccess) await FitOfficialPage();
                if (_closed) return;
                Status.Text = e.IsSuccess ? "请在网页内完成登录，然后点击“读取游戏角色”。" :
                    "官方网页加载失败，请检查网络后重载，或使用“手动导入凭据”。";
                if (e.IsSuccess && Browser.Source?.AbsolutePath.EndsWith("/crossLogin", StringComparison.OrdinalIgnoreCase) == true)
                    Status.Text = "这是登录中转页。若一直停留，可点击“返回米游社首页”。电量接口的验证请使用小组件中的“恢复电量同步”。";
            };
            Browser.CoreWebView2.Navigate("https://www.miyoushe.com/zzz/");
        }
        catch (Exception error) when (error is WebView2RuntimeNotFoundException or InvalidOperationException or System.Runtime.InteropServices.COMException)
        {
            if (!_closed) Status.Text = "无法启动登录浏览器。请安装或修复 Microsoft Edge WebView2 Runtime，或使用手动导入凭据。";
        }
        finally { _initializing = false; }
    }
    private static bool AllowedNavigation(string url) =>
        Uri.TryCreate(url, UriKind.Absolute, out var uri) && uri.Scheme == "https" && Cookies.IsOfficialHost(uri.Host);
    private async System.Threading.Tasks.Task FitOfficialPage()
    {
        // The desktop site has a minimum width. Keep its login control visible at high DPI.
        try
        {
            var json = await Browser.CoreWebView2.ExecuteScriptAsync(
                "[window.innerWidth, Math.max(document.documentElement.scrollWidth, document.body ? document.body.scrollWidth : 0)]");
            if (_closed || !BrowserReady) return;
            var dimensions = JsonSerializer.Deserialize<double[]>(json);
            if (dimensions is { Length: 2 } && dimensions[0] > 0 && dimensions[1] > dimensions[0])
                Browser.ZoomFactor = Math.Clamp(Browser.ZoomFactor * dimensions[0] / dimensions[1], .5, 1);
        }
        catch (Exception error) when (error is InvalidOperationException or JsonException or System.Runtime.InteropServices.COMException) { }
    }
    private async void BrowserTab(object sender, RoutedEventArgs e)
    {
        _import = false; BrowserPanel.Visibility = Visibility.Visible; ImportPanel.Visibility = Visibility.Collapsed;
        ResetCandidate(); await InitializeBrowser();
        if (BrowserReady) Browser.CoreWebView2.Navigate("https://www.miyoushe.com/zzz/");
    }
    private void ImportTab(object sender, RoutedEventArgs e)
    {
        _import = true; BrowserPanel.Visibility = Visibility.Collapsed; ImportPanel.Visibility = Visibility.Visible;
        ResetCandidate(); CookieInput.Focus(); Status.Text = "粘贴或导入自己的登录凭据后，点击“读取游戏角色”。";
    }
    private async void PassportClick(object sender, RoutedEventArgs e)
    {
        _import = false; BrowserPanel.Visibility = Visibility.Visible; ImportPanel.Visibility = Visibility.Collapsed;
        await InitializeBrowser();
        if (BrowserReady) Browser.CoreWebView2.Navigate("https://user.mihoyo.com/");
    }
    private async void ReloadClick(object sender, RoutedEventArgs e)
    {
        await InitializeBrowser();
        if (BrowserReady) Browser.CoreWebView2.Reload();
    }
    private void ImportFile(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog { Title = "选择自己的登录凭据文件", Filter = "凭据文件 (*.json;*.txt)|*.json;*.txt", CheckFileExists = true };
        if (dialog.ShowDialog(this) != true) return;
        try
        {
            if (new FileInfo(dialog.FileName).Length > 32768) { Status.Text = "文件过大，请选择凭据 JSON 或 Cookie 文本。"; return; }
            CookieInput.Password = File.ReadAllText(dialog.FileName);
            Status.Text = "已读取文件，点击“读取游戏角色”验证。";
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { Status.Text = "无法读取该文件。"; }
    }
    private async void ReadRoles(object sender, RoutedEventArgs e)
    {
        ResetCandidate(); ReadButton.IsEnabled = false; Progress.Visibility = Visibility.Visible;
        var generation = _generation;
        try
        {
            string cookie;
            if (_import) cookie = CookieInput.Password;
            else
            {
                if (!BrowserReady) { Status.Text = "登录网页尚未就绪，请重载或使用手动导入。"; return; }
                // Read only the isolated, user-opened login browser, never installed browser profiles.
                var available = await Browser.CoreWebView2.CookieManager.GetCookiesAsync(null);
                if (_closed || generation != _generation) return;
                cookie = string.Join("; ", available
                    .Where(c => Cookies.IsOfficialHost(c.Domain.TrimStart('.')) && Cookies.Allowed.Contains(c.Name))
                    .GroupBy(c => c.Name).Select(g => g.Last()).Select(c => c.Name + "=" + c.Value));
            }
            var credential = Cookies.Create(cookie);
            Status.Text = "正在验证登录并读取绝区零角色…";
            var roles = await App.Current.Client.GetRolesAsync(credential, _cancel.Token);
            if (_closed || generation != _generation) return;
            CookieInput.Clear();
            _candidate = credential;
            Roles.ItemsSource = roles; Roles.SelectedIndex = 0; Roles.Visibility = Visibility.Visible;
            UseButton.IsEnabled = true;
            Status.Text = $"找到 {roles.Count} 个绝区零角色。选择后点击“使用此角色”。";
        }
        catch (OperationCanceledException) { }
        catch (ArgumentException error) { if (!_closed) Status.Text = error.Message; }
        catch (ApiFailure error) { if (!_closed) Status.Text = error.Message; }
        catch (Exception error) when (error is IOException or InvalidOperationException or System.Runtime.InteropServices.COMException)
        { if (!_closed) Status.Text = "读取未完成，请重载官方网页后再试。"; }
        finally { if (!_closed) { ReadButton.IsEnabled = true; Progress.Visibility = Visibility.Collapsed; } }
    }
    private async void UseRole(object sender, RoutedEventArgs e)
    {
        if (_candidate is null || Roles.SelectedItem is not GameRole role) return;
        _candidate.Role = role;
        UseButton.IsEnabled = false; ReadButton.IsEnabled = false;
        Status.Text = "正在保存加密凭据并同步电量…";
        try
        {
            await App.Current.Connect(_candidate);
            if (_closed) return;
            App.Current.ShowWidget(); Close();
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or System.Security.Cryptography.CryptographicException)
        {
            if (!_closed) { Status.Text = "凭据保存失败，请检查数据目录权限。"; UseButton.IsEnabled = true; ReadButton.IsEnabled = true; }
        }
    }

    internal async System.Threading.Tasks.Task CaptureOfficialPageAsync(string path)
    {
        if (!BrowserReady || !OfficialPageLoaded) return;
        using var file = File.Create(path);
        await Browser.CoreWebView2.CapturePreviewAsync(CoreWebView2CapturePreviewImageFormat.Png, file);
    }
}
