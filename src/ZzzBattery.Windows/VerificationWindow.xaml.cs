using System;
using System.Diagnostics;
using System.IO;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;
using ZzzBattery.Core;

namespace ZzzBattery.Windows;

public partial class VerificationWindow : Window
{
    private enum Mode { NativeCaptcha, OfficialPage }

    private readonly Credential _credential;
    private readonly ApiFailure? _failure;
    private readonly DateTimeOffset _openedAt = DateTimeOffset.UtcNow;
    private readonly CancellationTokenSource _cancel = new();
    private readonly OfficialPopupHost _popups;
    private CancellationTokenSource? _pageWatch;
    private Mode _mode = Mode.NativeCaptcha;
    private VerificationTask? _challenge;
    private string _nonce = "";
    private bool _closed, _busy, _browserReady;
    private bool _navigationCompleted;
    private ulong _navigationId;
    private bool _submitted;
    private bool _completed, _readingNote;
    private string? _noteFeedback;
    private DateTimeOffset _submittedAt;
    private DateTimeOffset _lastCreation = DateTimeOffset.MinValue;
    private DateTimeOffset _lastPageLoad = DateTimeOffset.MinValue;
    private DispatcherTimer? _autoCloseTimer;
    internal bool PageReady { get; private set; }
    internal bool PageFailed { get; private set; }
    internal bool RecoveryCompletedForCheck => _completed;

    public VerificationWindow(Credential credential, ApiFailure? failure, bool loadPage = true)
    {
        _credential = credential;
        _failure = failure;
        InitializeComponent();
        _popups = new OfficialPopupHost(this, text => { if (!_closed) Status.Text = text; });
        PageViewport.SizeChanged += (_, _) => FitBrowserViewport();
        AdaptiveWindow.Attach(this, 420, 440);
        if (loadPage) Loaded += async (_, _) => await InitializeAndLoad();
        App.Current.Session.Changed += SessionChanged;
        Closed += (_, _) =>
        {
            _closed = true;
            App.Current.Session.Changed -= SessionChanged;
            _autoCloseTimer?.Stop();
            _autoCloseTimer = null;
            _cancel.Cancel();
            _pageWatch?.Cancel(); _pageWatch?.Dispose();
            _popups.CloseAll();
            ChallengeBrowser.Dispose();
        };
    }

    private bool SameAccount() => App.Current.Session.Credential is { } current &&
        current.AccountKey == _credential.AccountKey && current.Role?.Key == _credential.Role?.Key;

    private void FitBrowserViewport()
    {
        if (PageViewport.ActualWidth <= 0 || PageViewport.ActualHeight <= 0) return;
        // This mobile page positions its login button in vw units and hides
        // overflow. Keep a portrait viewport so it remains visible on short screens.
        ChallengeBrowser.Width = _mode == Mode.OfficialPage
            ? Math.Min(PageViewport.ActualWidth, PageViewport.ActualHeight / 1.25)
            : PageViewport.ActualWidth;
    }

    private async Task InitializeAndLoad()
    {
        if (_busy || _closed || _completed) return;
        await EnsureBrowserAsync();
        if (_closed || _completed || !_browserReady) return;
        // Default directly to OfficialPage:
        // The official web record page has genuine browser context and cookies.
        // Let the official page own its verification context; success is confirmed by a role-matched note response.
        await SwitchToMode(Mode.OfficialPage);
    }

    private async Task EnsureBrowserAsync()
    {
        if (_browserReady || _closed) return;
        try
        {
            ChallengeBrowser.CreationProperties = new CoreWebView2CreationProperties
            {
                UserDataFolder = Path.Combine(App.Current.Store.Root, "WebViewProfile"),
                ProfileName = OfficialNoteBrowser.ProfileName(_credential),
                IsInPrivateModeEnabled = true
            };
            await ChallengeBrowser.EnsureCoreWebView2Async().WaitAsync(TimeSpan.FromSeconds(20), _cancel.Token);
            if (_closed || ChallengeBrowser.CoreWebView2 is null) return;
            _browserReady = true;

            var core = ChallengeBrowser.CoreWebView2;
            core.Settings.IsPasswordAutosaveEnabled = false;
            core.Settings.IsGeneralAutofillEnabled = false;
            // Keep WebView2's real browser identity. An miHoYoBBS marker makes the
            // official page wait for native app bridge methods this desktop app lacks.

            core.NavigationStarting += (_, e) =>
            {
                if (!AllowedNavigation(e.Uri))
                {
                    e.Cancel = true;
                    if (!_closed) ShowPageFailure("无法打开此页面", "官方页面尝试跳转到不支持的地址，可使用系统浏览器继续。");
                    return;
                }
                _navigationId = e.NavigationId;
                StartPageWatch();
            };
            core.NavigationCompleted += (_, e) =>
            {
                if (_closed || _completed || e.NavigationId != _navigationId) return;
                _navigationCompleted = e.IsSuccess;
                if (!e.IsSuccess)
                    ShowPageFailure("官方页面加载失败", $"请检查网络后重载页面，也可以使用系统浏览器打开。加载状态：{e.WebErrorStatus}。");
            };
            _popups.Attach(core);
            core.DownloadStarting += (_, e) => e.Cancel = true;
            core.WebMessageReceived += MessageReceived;
            core.WebResourceResponseReceived += ResponseReceived;
            core.ProcessFailed += (_, e) =>
            {
                if (!_closed && !_completed)
                {
                    WriteRecoveryDiagnostic("browser-" + e.ProcessFailedKind);
                    ShowPageFailure("验证浏览器已退出", "请关闭此窗口后重新打开，或使用系统浏览器继续。", canReload: false);
                }
            };

            InjectCookies(core);
        }
        catch (OperationCanceledException) when (_closed) { }
        catch (TimeoutException)
        {
            if (!_closed) ShowPageFailure("验证浏览器启动超时", "请关闭此窗口后重新打开，或检查 WebView2 运行时。", canReload: false);
        }
        catch (Exception e) when (e is WebView2RuntimeNotFoundException or InvalidOperationException or System.Runtime.InteropServices.COMException)
        {
            if (!_closed) ShowPageFailure("无法启动验证浏览器", "请检查 WebView2 运行时，或使用系统浏览器打开。", canReload: false);
        }
    }

    private void ShowPageLoading(string title = "正在打开官方页面", string hint = "首次加载需要几秒，请稍候。")
    {
        if (_closed || _completed) return;
        PageReady = false; PageFailed = false;
        ChallengeBrowser.Visibility = Visibility.Hidden;
        PageStatePanel.Visibility = Visibility.Visible;
        PageStateTitle.Text = title; PageStateHint.Text = hint;
        PageProgress.Visibility = Visibility.Visible;
        PageReloadButton.Visibility = Visibility.Collapsed;
    }

    private void ShowPageFailure(string title, string hint, bool canReload = true)
    {
        if (_closed || _completed) return;
        _pageWatch?.Cancel();
        PageReady = false; PageFailed = true;
        ChallengeBrowser.Visibility = Visibility.Hidden;
        PageStatePanel.Visibility = Visibility.Visible;
        PageStateTitle.Text = title; PageStateHint.Text = hint;
        PageProgress.Visibility = Visibility.Collapsed;
        PageReloadButton.Visibility = canReload ? Visibility.Visible : Visibility.Collapsed;
        Status.Text = hint;
        WritePageDiagnostic("unavailable");
    }

    private void StartPageWatch()
    {
        if (_closed || _completed) return;
        _pageWatch?.Cancel(); _pageWatch?.Dispose();
        _pageWatch = CancellationTokenSource.CreateLinkedTokenSource(_cancel.Token);
        _navigationCompleted = false;
        ShowPageLoading(_mode == Mode.OfficialPage ? "正在打开官方页面" : "正在加载验证码");
        _ = WatchPageAsync(_pageWatch.Token);
    }

    private async Task WatchPageAsync(CancellationToken token)
    {
        try
        {
            // NavigationCompleted only confirms the HTML loaded, not that the SPA
            // rendered. The broken App bridge can return HTTP 200 and stay blank.
            for (var attempt = 0; attempt < 50; attempt++)
            {
                await Task.Delay(400, token);
                if (!_navigationCompleted) continue;
                var length = await ChallengeBrowser.CoreWebView2.ExecuteScriptAsync(
                    "(document.body?.innerText||'').replace(/[\\s\\d]/g,'').length").WaitAsync(TimeSpan.FromSeconds(2), token);
                if (token.IsCancellationRequested || _closed || _completed) return;
                if (int.TryParse(length, out var visibleText) && visibleText >= 6)
                {
                    PageReady = true; PageFailed = false;
                    PageStatePanel.Visibility = Visibility.Collapsed;
                    ChallengeBrowser.Visibility = Visibility.Visible;
                    if (!_completed) Status.Text = _noteFeedback ?? (_mode == Mode.OfficialPage
                        ? "页面已打开。已看到电量但窗口未关闭时，可点“检查同步”；没有验证码提示就不需要额外操作。"
                        : "请点击“开始验证”并手动完成；仅在取得最新电量后才会提示同步成功。");
                    WritePageDiagnostic("rendered");
                    return;
                }
            }
            ShowPageFailure("官方页面暂未显示内容", "页面可能加载缓慢或部分资源不可用。可以重载页面、切换快速验证码，或使用系统浏览器继续。");
        }
        catch (OperationCanceledException) { }
        catch (Exception e) when (e is TimeoutException or InvalidOperationException or System.Runtime.InteropServices.COMException)
        {
            if (!_closed && !token.IsCancellationRequested)
                ShowPageFailure("无法显示官方页面", "页面未能完成加载，请重载页面或使用系统浏览器继续。");
        }
    }

    private void WritePageDiagnostic(string stage)
    {
        try { App.Current.Store.Write("verification-page.json", new { at = DateTimeOffset.UtcNow, stage, mode = _mode.ToString() }); }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException) { }
    }

    internal async Task CapturePageAsync(string path)
    {
        using var output = File.Create(path);
        await ChallengeBrowser.CoreWebView2.CapturePreviewAsync(CoreWebView2CapturePreviewImageFormat.Png, output);
    }
    internal void ShowFailureForCheck() => ShowPageFailure("官方页面暂未显示内容", "页面可能加载缓慢或部分资源不可用。可以重载页面、切换快速验证码，或使用系统浏览器继续。");
    internal void NavigateBlankForCheck() => ChallengeBrowser.CoreWebView2.NavigateToString("<!doctype html><body>1 1</body>");
    internal async Task<bool> OfficialContentFitsForCheck() => await ChallengeBrowser.CoreWebView2.ExecuteScriptAsync(
        "(()=>{const e=document.querySelector('.error-block-layout');return !!e&&e.getBoundingClientRect().bottom<=innerHeight+1})()") == "true";

    private void InjectCookies(CoreWebView2 core)
    {
        try
        {
            OfficialNoteBrowser.InjectCookies(core, _credential);
        }
        catch { }
    }

    private static bool IsGeetestHost(string host) =>
        host.Equals("geetest.com", StringComparison.OrdinalIgnoreCase) ||
        host.EndsWith(".geetest.com", StringComparison.OrdinalIgnoreCase) ||
        host.Equals("geevisit.com", StringComparison.OrdinalIgnoreCase) ||
        host.EndsWith(".geevisit.com", StringComparison.OrdinalIgnoreCase) ||
        host.Equals("geetest.cn", StringComparison.OrdinalIgnoreCase) ||
        host.EndsWith(".geetest.cn", StringComparison.OrdinalIgnoreCase) ||
        host.Equals("geecdn.com", StringComparison.OrdinalIgnoreCase) ||
        host.EndsWith(".geecdn.com", StringComparison.OrdinalIgnoreCase);

    private static bool IsOfficialVendorHost(string host) =>
        host.EndsWith(".volces.com", StringComparison.OrdinalIgnoreCase) ||
        host.EndsWith(".byteoversea.com", StringComparison.OrdinalIgnoreCase) ||
        host.EndsWith(".qbox.me", StringComparison.OrdinalIgnoreCase) ||
        host.Equals("hoyoverse.com", StringComparison.OrdinalIgnoreCase) ||
        host.EndsWith(".hoyoverse.com", StringComparison.OrdinalIgnoreCase) ||
        host.EndsWith(".bilibili.com", StringComparison.OrdinalIgnoreCase) ||
        host.EndsWith(".hdslb.com", StringComparison.OrdinalIgnoreCase);

    private static bool AllowedNavigation(string url)
    {
        if (string.IsNullOrWhiteSpace(url)) return true;
        // Allow inline, data, blob and javascript protocols necessary for Geetest and Vue SPA
        if (url.StartsWith("about:", StringComparison.OrdinalIgnoreCase) ||
            url.StartsWith("data:", StringComparison.OrdinalIgnoreCase) ||
            url.StartsWith("blob:", StringComparison.OrdinalIgnoreCase) ||
            url.StartsWith("javascript:", StringComparison.OrdinalIgnoreCase))
            return true;

        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri)) return false;
        if (uri.Scheme != "https" && uri.Scheme != "http") return false;

        var host = uri.Host;
        return Cookies.IsOfficialHost(host) || IsGeetestHost(host) || IsOfficialVendorHost(host);
    }

    private async Task SwitchToMode(Mode targetMode)
    {
        if (_closed || _completed) return;
        _mode = targetMode;
        FitBrowserViewport();
        if (_mode == Mode.NativeCaptcha)
        {
            SwitchModeButton.Content = "官方便笺页";
            RetryButton.Content = "重新获取验证";
            PageReloadButton.Content = "重新获取验证";
            await LoadNativeChallenge();
        }
        else
        {
            SwitchModeButton.Content = "快速验证码";
            RetryButton.Content = "重载页面";
            PageReloadButton.Content = "重新加载页面";
            await LoadOfficialPage();
        }
    }

    private async Task LoadNativeChallenge()
    {
        if (_busy || _closed || _completed) return;
        if (!SameAccount()) { Status.Text = "账号或角色已变化，请关闭后重新打开验证。"; return; }
        if (DateTimeOffset.UtcNow - _lastCreation < TimeSpan.FromSeconds(60))
        {
            Status.Text = "获取验证任务的最短间隔为 60 秒，请稍后再试。";
            return;
        }

        _busy = true;
        ShowPageLoading("正在获取验证任务", "正在连接米游社，请稍候。");
        RetryButton.IsEnabled = false;
        _challenge = null;
        try
        {
            await EnsureBrowserAsync();
            if (_closed || _completed || !_browserReady) return;
            InjectCookies(ChallengeBrowser.CoreWebView2);

            Status.Text = "正在向米游社获取电量查询的验证任务…";
            _lastCreation = DateTimeOffset.UtcNow;
            var challenge = await App.Current.Client.CreateVerificationAsync(_credential, _failure, _cancel.Token);
            if (_closed || _completed || !SameAccount()) return;
            if (challenge is null)
            {
                ShowPageFailure("当前没有快速验证码", "官方未要求快速验证码（30001）。若官方网页已显示电量，无需再找验证码；可切换官方便笺页或点击“检查同步”确认结果。", canReload: false);
                return;
            }
            _challenge = challenge;
            _nonce = Convert.ToHexString(RandomNumberGenerator.GetBytes(24));
            ChallengeBrowser.CoreWebView2.NavigateToString(VerificationHtml.Render(challenge, _nonce));
            Status.Text = "请点击“开始验证”并手动完成。验证成功后会自动同步电量。";
        }
        catch (OperationCanceledException) { }
        catch (ApiFailure e) when (e.Kind == FailureKind.RateLimited)
        {
            if (!_closed) ShowPageFailure("验证请求正在冷却", e.Message, canReload: false);
        }
        catch (ApiFailure e)
        {
            if (!_closed)
            {
                ShowPageFailure("无法取得快速验证码", e.Message + "也可切换官方便笺页继续。");
            }
        }
        catch (Exception e) when (e is WebView2RuntimeNotFoundException or InvalidOperationException or System.Runtime.InteropServices.COMException)
        {
            if (!_closed) ShowPageFailure("无法打开验证浏览器", "请检查 WebView2，或使用系统浏览器打开。", canReload: false);
        }
        finally
        {
            if (!_closed) { _busy = false; RetryButton.IsEnabled = !_completed; }
        }
    }

    private async Task LoadOfficialPage()
    {
        if (_closed || _completed) return;
        if (DateTimeOffset.UtcNow - _lastPageLoad < TimeSpan.FromSeconds(10))
        { Status.Text = "请稍等几秒后再重载页面。"; return; }
        try
        {
            await EnsureBrowserAsync();
            if (_closed || _completed || !_browserReady) return;
            InjectCookies(ChallengeBrowser.CoreWebView2);
            _lastPageLoad = DateTimeOffset.UtcNow;
            _noteFeedback = null;
            Status.Text = "正在加载官方便笺页面…";
            ChallengeBrowser.CoreWebView2.Navigate(MiyousheClient.VerificationPageUrl(_credential.Role));
        }
        catch (Exception error) when (error is InvalidOperationException or System.Runtime.InteropServices.COMException)
        {
            if (!_closed) ShowPageFailure("官方页面加载失败", "请重载页面或使用系统浏览器继续。");
        }
    }

    private async void MessageReceived(object? sender, CoreWebView2WebMessageReceivedEventArgs e)
    {
        if (_closed || _completed || _busy || _challenge is null || e.Source != "about:blank") return;
        try
        {
            using var doc = JsonDocument.Parse(e.WebMessageAsJson);
            var payload = doc.RootElement;
            if (JsonValue.String(payload, "nonce") != _nonce) return;
            var kind = JsonValue.String(payload, "kind");
            if (kind == "error") { Status.Text = "验证组件加载失败，请检查网络或点击“官方便笺页”。"; return; }
            if (kind == "closed") { Status.Text = "验证尚未完成，可再次点击“开始验证”。"; return; }
            if (kind != "success" || !SameAccount()) return;

            _busy = true;
            RetryButton.IsEnabled = false;
            Status.Text = "正在向米游社确认验证结果…";
            var answer = new VerificationAnswer(JsonValue.String(payload, "challenge"), JsonValue.String(payload, "validate"), JsonValue.String(payload, "seccode"));
            await App.Current.Client.CompleteVerificationAsync(_credential, _challenge, answer, _cancel.Token);
            if (_closed || _completed || !SameAccount()) return;

            _challenge = null;
            _submitted = true;
            _submittedAt = DateTimeOffset.UtcNow;
            App.Current.Session.ResumeAfterVerification(resetCooldown: true);
            await App.Current.Refresh();
            if (_closed) return;
            SessionChanged();
            App.Current.ShowWidget();

            if (App.Current.Session.Snapshot?.ReceivedAt >= _submittedAt)
            {
                TriggerAutoClose();
            }
        }
        catch (OperationCanceledException) { }
        catch (ApiFailure error) { if (!_closed) Status.Text = "服务端未接受验证结果：" + error.Message; }
        catch (ArgumentException error) { if (!_closed) Status.Text = error.Message; }
        catch (JsonException) { if (!_closed) Status.Text = "验证结果无法识别，请重新获取验证。"; }
        catch (Exception error) { if (!_closed) Status.Text = "验证处理异常：" + error.Message; }
        finally { if (!_closed) { _busy = false; RetryButton.IsEnabled = !_completed; } }
    }

    private async void ResponseReceived(object? sender, CoreWebView2WebResourceResponseReceivedEventArgs e)
    {
        if (_closed || _completed || !SameAccount() || _credential.Role is null) return;
        if (!Uri.TryCreate(e.Request.Uri, UriKind.Absolute, out var requestUri)) return;
        if (MiyousheClient.IsNoteEndpoint(requestUri))
        {
            if (_readingNote) return;
            _readingNote = true;
            var receivedAt = DateTimeOffset.UtcNow;
            try
            {
                // Capture exactly the selected role, never scrape numbers from rendered HTML.
                if (!MiyousheClient.IsNoteRequestForRole(requestUri, _credential.Role))
                {
                    ApplyNoteCapture(new(OfficialNoteCaptureState.RoleMismatch));
                    return;
                }
                using var stream = await e.Response.GetContentAsync().WaitAsync(TimeSpan.FromSeconds(5), _cancel.Token);
                using var reader = new StreamReader(stream);
                var response = await reader.ReadToEndAsync(_cancel.Token).WaitAsync(TimeSpan.FromSeconds(5), _cancel.Token);
                ApplyNoteCapture(MiyousheClient.ReadOfficialNoteResponse(requestUri, e.Response.StatusCode, response, _credential, receivedAt));
            }
            catch (OperationCanceledException) when (_closed) { }
            catch (Exception error) when (error is IOException or JsonException or InvalidOperationException or TimeoutException or System.Runtime.InteropServices.COMException)
            {
                if (!_closed && SameAccount())
                {
                    SetNoteFeedback("网页已打开，但程序未能读取便笺响应。请点击“检查同步”确认；不需要反复寻找验证码。");
                    WriteRecoveryDiagnostic("response-unavailable");
                }
            }
            finally { _readingNote = false; }
        }
        else if (requestUri.GetLeftPart(UriPartial.Path) == MiyousheClient.VerifyVerificationEndpoint && e.Response.StatusCode == 200)
        {
            try
            {
                using var stream = await e.Response.GetContentAsync().WaitAsync(TimeSpan.FromSeconds(5), _cancel.Token);
                using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: _cancel.Token);
                if (_closed || !SameAccount()) return;
                var root = doc.RootElement;
                var challenge = JsonValue.String(JsonValue.Object(root, "data"), "challenge");
                if (string.IsNullOrEmpty(challenge)) challenge = JsonValue.String(root, "challenge");
                if (JsonValue.Int(root, "retcode") is null or 0 && MiyousheClient.IsSafeVerificationValue(challenge))
                    App.Current.Client.SetVerificationProof(_credential, _credential.Role, challenge);
            }
            catch (OperationCanceledException) when (_closed) { }
            catch (Exception error) when (error is IOException or JsonException or InvalidOperationException or TimeoutException or System.Runtime.InteropServices.COMException)
            {
                if (!_closed) WriteRecoveryDiagnostic("proof-unavailable");
            }
        }
    }

    private void ApplyNoteCapture(OfficialNoteCapture capture)
    {
        if (_closed || _completed || !SameAccount()) return;
        WriteRecoveryDiagnostic(capture.State.ToString(), capture.Code);
        if (capture.State == OfficialNoteCaptureState.Ready && capture.Snapshot is { } snapshot)
        {
            // The browser already fetched real data. Apply it immediately; do not
            // probe a separately restricted widget endpoint before accepting it.
            snapshot = snapshot with { SupplementMessage = "通过官方网页便笺后台同步；网页未提供的小组件活动信息暂不可用。" };
            App.Current.Session.ResumeAfterVerification(snapshot);
            TriggerAutoClose();
            App.Current.ShowWidget();
            return;
        }
        var message = capture.State switch
        {
            OfficialNoteCaptureState.RoleMismatch => "网页当前角色与小组件所选角色不一致，尚未同步。请切换网页角色，或在账号设置中重新选择角色。",
            OfficialNoteCaptureState.HttpError => $"官方便笺请求未成功（HTTP {capture.Code}）。请稍后检查同步。",
            OfficialNoteCaptureState.InvalidData => "已收到网页响应，但电量数据不完整，尚未同步。请点击“检查同步”确认。",
            OfficialNoteCaptureState.Rejected => $"官方便笺接口尚未恢复（{capture.Code}）。仅在网页出现登录或验证提示时按提示操作。",
            _ => null
        };
        if (message is not null) SetNoteFeedback(message);
    }

    private void SetNoteFeedback(string message)
    {
        _noteFeedback = message;
        Status.Text = message;
    }

    private void WriteRecoveryDiagnostic(string stage, int? code = null)
    {
        try { App.Current.Store.Write("verification-result.json", new { at = DateTimeOffset.UtcNow, stage, code }); }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException) { }
    }

    internal void CaptureNoteForCheck(Uri uri, int status, string json) => ApplyNoteCapture(
        MiyousheClient.ReadOfficialNoteResponse(uri, status, json, _credential, DateTimeOffset.UtcNow));

    internal async Task ExerciseResponsePipelineForCheck(string json)
    {
        await EnsureBrowserAsync();
        if (!_browserReady || _credential.Role is null) throw new InvalidOperationException("Test browser unavailable.");
        var core = ChallengeBrowser.CoreWebView2;
        var request = MiyousheClient.NoteEndpoint + "?role_id=" + Uri.EscapeDataString(_credential.Role.Uid) + "&server=" + Uri.EscapeDataString(_credential.Role.Server);
        var intercepted = false;
        var observed = false;
        core.WebResourceResponseReceived += (_, e) => { if (e.Request.Uri == request) observed = true; };
        core.AddWebResourceRequestedFilter(request, CoreWebView2WebResourceContext.All);
        core.WebResourceRequested += (_, e) =>
        {
            if (e.Request.Uri != request) return;
            intercepted = true;
            e.Response = core.Environment.CreateWebResourceResponse(new MemoryStream(System.Text.Encoding.UTF8.GetBytes(json)),
                200, "OK", "Content-Type: application/json\r\nAccess-Control-Allow-Origin: *\r\nCache-Control: no-store");
        };
        core.NavigateToString("<!doctype html><body>便笺响应管道测试<script>window.testFetch='started';fetch(" + JsonSerializer.Serialize(request) + ").then(async r=>{window.testFetch='received:'+r.status;await r.text();window.testFetch='consumed'}).catch(e=>{window.testFetch=e.name+':'+e.message})</script></body>");
        for (var attempt = 0; attempt < 240 && !_completed; attempt++) await Task.Delay(50);
        if (!intercepted || !_completed)
            throw new InvalidOperationException($"WebView response pipeline: intercepted={intercepted}, observed={observed}, sameAccount={SameAccount()}, reading={_readingNote}, fetch={await core.ExecuteScriptAsync("window.testFetch")}, status={Status.Text}");
    }

    private void TriggerAutoClose()
    {
        if (_closed || _completed || !SameAccount() || !App.Current.Session.HasSyncedSince(_submitted ? _submittedAt : _openedAt)) return;
        _completed = true;
        _pageWatch?.Cancel();
        PageProgress.Visibility = Visibility.Collapsed;
        PageStateTitle.Text = "电量已同步"; PageStateHint.Text = "已取得当前角色的数据，无需继续验证。";
        TitleText.Text = "电量已同步";
        var fromPage = App.Current.Session.Snapshot?.Source == "官方网页便笺";
        DescText.Text = fromPage
            ? "已读取所选角色的官方网页便笺，后续将优先在后台通过此渠道同步。"
            : "已取得所选角色的最新电量，无需继续验证。";
        Status.Text = (fromPage ? "官方网页便笺已同步。" : "自动同步已恢复。") + "窗口将在 3 秒后关闭。";
        RetryButton.IsEnabled = SwitchModeButton.IsEnabled = CheckSyncButton.IsEnabled = false;
        WriteRecoveryDiagnostic(fromPage ? "web-note-synced" : "sync-recovered");
        _autoCloseTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(3) };
        _autoCloseTimer.Tick += (_, _) =>
        {
            _autoCloseTimer.Stop();
            _autoCloseTimer = null;
            if (!_closed) Close();
        };
        _autoCloseTimer.Start();
    }

    private void SessionChanged()
    {
        if (_closed || _completed || !SameAccount()) return;
        // A normal successful sync also finishes recovery, even when no captcha
        // was displayed. Old cached values must never finish this window.
        if (App.Current.Session.HasSyncedSince(_submitted ? _submittedAt : _openedAt)) { TriggerAutoClose(); return; }
        if (!_submitted) return;
        Status.Text = App.Current.Session.Failure is { } error
            ? "验证结果已提交，但同步仍未成功：" + error.Message
            : "验证结果已提交，正在确认实际电量同步…";
    }

    private async void CheckSyncClick(object sender, RoutedEventArgs e)
    {
        if (_busy || _completed || _closed || !SameAccount()) return;
        _busy = true; CheckSyncButton.IsEnabled = false;
        try
        {
            Status.Text = "正在检查电量同步…";
            // No cooldown reset: opening a readable page is not verification proof.
            await App.Current.Refresh();
            if (_closed || !SameAccount()) return;
            SessionChanged();
            if (!_completed)
            {
                var session = App.Current.Session;
                SetNoteFeedback("尚未取得新的电量：" + session.Message +
                    (session.Failure?.VerificationPath == "/api/zzz/widget" ? "。网页可访问不代表小组件接口已恢复。" : ""));
                WriteRecoveryDiagnostic("sync-not-recovered", session.Failure?.Code);
            }
        }
        finally { if (!_closed) { _busy = false; CheckSyncButton.IsEnabled = !_completed; } }
    }
    private async void SwitchModeClick(object sender, RoutedEventArgs e)
    {
        if (_busy) return;
        var target = _mode == Mode.NativeCaptcha ? Mode.OfficialPage : Mode.NativeCaptcha;
        await SwitchToMode(target);
    }

    private async void RetryClick(object sender, RoutedEventArgs e)
    {
        if (_busy) return;
        if (_mode == Mode.NativeCaptcha) await LoadNativeChallenge(); else await LoadOfficialPage();
    }
    private void OfficialClick(object sender, RoutedEventArgs e) => App.Current.OpenOfficialRecord();
    private void CloseClick(object sender, RoutedEventArgs e) => Close();
}

internal static class VerificationHtml
{
    public static string Render(VerificationTask task, string nonce)
    {
        var configuration = JsonSerializer.Serialize(new
        {
            gt = task.Gt,
            challenge = task.Challenge,
            new_captcha = task.NewCaptcha,
            offline = !task.Online,
            product = "bind",
            lang = "zh-cn",
            https = true
        });
        return $$"""
        <!doctype html><html lang="zh-CN"><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1">
        <style>body{margin:0;font:15px 'Microsoft YaHei',sans-serif;background:#f7f8f3;color:#20251b;display:grid;place-content:center;min-height:100vh;text-align:center;padding:0 18px;box-sizing:border-box}button{font:inherit;font-weight:700;padding:14px 32px;border:0;border-radius:10px;background:#d7f26c;cursor:pointer}button:disabled{opacity:.5}p{line-height:1.8;color:#66705d}#caption{max-width:340px}</style>
        <div><h2>电量查询验证</h2><p id="caption">正在加载官方验证组件…</p><button id="begin" disabled>开始验证</button><p>无需再次输入账号密码</p></div>
        <script>
        const nonce={{JsonSerializer.Serialize(nonce)}};
        const send=(kind,extra={})=>window.chrome.webview.postMessage({kind,nonce,...extra});
        const fail=()=>{document.getElementById('caption').textContent='验证组件加载失败，请检查网络后重新获取。';send('error')};
        const timeout=setTimeout(fail,20000);
        function ready(){
          if(typeof initGeetest!=='function'){fail();return}
          initGeetest({{configuration}},captcha=>{
            captcha.onReady(()=>{clearTimeout(timeout);document.getElementById('caption').textContent='点击按钮，按提示完成图案或滑块。';document.getElementById('begin').disabled=false;});
            document.getElementById('begin').onclick=()=>captcha.verify();
            captcha.onError(fail).onClose(()=>send('closed')).onSuccess(()=>{
              const result=captcha.getValidate(); if(!result){fail();return}
              document.getElementById('caption').textContent='验证已完成，正在提交结果…';
              document.getElementById('begin').disabled=true;
              send('success',{challenge:result.geetest_challenge,validate:result.geetest_validate,seccode:result.geetest_seccode});
            });
          });
        }
        </script><script src="https://static.geetest.com/static/tools/gt.js" onload="ready()" onerror="fail()"></script></html>
        """;
    }
}

