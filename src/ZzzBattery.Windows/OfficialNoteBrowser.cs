using System;
using System.IO;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Web.WebView2.Core;
using ZzzBattery.Core;

namespace ZzzBattery.Windows;

// HWND_MESSAGE cannot appear on the desktop or receive foreground focus.
internal sealed class OfficialNoteBrowser(string dataRoot) : IDisposable
{
    private CoreWebView2Controller? _controller;
    private string? _accountKey;
    private int _generation;
    private bool _reading;
    internal bool InvisibleForCheck => _controller is { IsVisible: false, ParentWindow: var parent } && parent == new IntPtr(-3);
    internal static string ProfileName(Credential credential) =>
        "Energy_" + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(credential.AccountKey)))[..24];

    public Task<EnergySnapshot> ReadAsync(Credential credential, CancellationToken cancellation) =>
        ReadCoreAsync(credential, cancellation);

    internal Task<EnergySnapshot> ReadForCheckAsync(Credential credential, Action<CoreWebView2> configure,
        CancellationToken cancellation = default, TimeSpan? timeout = null) =>
        ReadCoreAsync(credential, cancellation, configure, timeout);

    private async Task<EnergySnapshot> ReadCoreAsync(Credential credential, CancellationToken cancellation,
        Action<CoreWebView2>? configure = null, TimeSpan? timeout = null)
    {
        if (credential.Role is null) throw new ArgumentException("请先选择角色。");
        if (_reading) throw new ApiFailure(FailureKind.RateLimited, "后台同步正在进行，请稍候。", retryAfter: TimeSpan.FromMinutes(1));
        _reading = true;
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellation);
        deadline.CancelAfter(timeout ?? TimeSpan.FromSeconds(45));
        var token = deadline.Token;
        CoreWebView2? core = null;
        var completion = new TaskCompletionSource<EnergySnapshot>(TaskCreationOptions.RunContinuationsAsynchronously);
        var generation = _generation;
        var readingResponse = false;
        int? verificationCode = null;
        var verificationResponses = 0;
        ulong? navigationId = null;
        void NeedsUser() => completion.TrySetException(new ApiFailure(FailureKind.Verification,
            "后台恢复未完成，已暂停自动尝试。可在方便时点击“恢复电量同步”处理登录或验证。", verificationCode));
        async void ResponseReceived(object? sender, CoreWebView2WebResourceResponseReceivedEventArgs e)
        {
            if (token.IsCancellationRequested || completion.Task.IsCompleted || readingResponse ||
                !Uri.TryCreate(e.Request.Uri, UriKind.Absolute, out var uri) || !MiyousheClient.IsNoteEndpoint(uri)) return;
            if (!MiyousheClient.IsNoteRequestForRole(uri, credential.Role)) { NeedsUser(); return; }
            readingResponse = true;
            try
            {
                using var stream = await e.Response.GetContentAsync().WaitAsync(token);
                using var body = new MemoryStream();
                var buffer = new byte[8192];
                int count;
                while ((count = await stream.ReadAsync(buffer, token)) > 0)
                {
                    if (body.Length + count > 2_000_000) { NeedsUser(); return; }
                    body.Write(buffer, 0, count);
                }
                if (token.IsCancellationRequested) return;
                var capture = MiyousheClient.ReadOfficialNoteResponse(uri, e.Response.StatusCode,
                    Encoding.UTF8.GetString(body.ToArray()), credential, DateTimeOffset.UtcNow);
                if (capture.Snapshot is { } snapshot)
                    completion.TrySetResult(snapshot with { SupplementMessage = "通过官方网页便笺后台同步；网页未提供的小组件活动信息暂不可用。" });
                else if (capture.State == OfficialNoteCaptureState.Rejected && capture.Code is { } code)
                {
                    var failure = MiyousheClient.FromRetcode(code);
                    if (failure.Kind == FailureKind.Verification)
                    {
                        verificationCode = code;
                        // Give the official page one chance to finish its own non-interactive flow.
                        if (++verificationResponses >= 2) NeedsUser();
                    }
                    else completion.TrySetException(failure);
                }
                else if (capture.State == OfficialNoteCaptureState.HttpError && capture.Code == 429)
                {
                    var delay = TimeSpan.FromMinutes(15);
                    if (e.Response.Headers.Contains("Retry-After"))
                    {
                        var header = e.Response.Headers.GetHeader("Retry-After");
                        if (double.TryParse(header, NumberStyles.None, CultureInfo.InvariantCulture, out var seconds) && double.IsFinite(seconds))
                            delay = TimeSpan.FromSeconds(Math.Clamp(seconds, 60, 86400));
                        else if (DateTimeOffset.TryParse(header, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var until) &&
                            until > DateTimeOffset.UtcNow) delay = until - DateTimeOffset.UtcNow;
                    }
                    completion.TrySetException(new ApiFailure(FailureKind.RateLimited, "官方网页查询正在冷却，稍后再同步。", 429, delay));
                }
                else NeedsUser();
            }
            catch (OperationCanceledException) { }
            catch (Exception error) when (error is IOException or InvalidOperationException or System.Runtime.InteropServices.COMException or ArgumentException)
            { NeedsUser(); }
            finally { readingResponse = false; }
        }
        void NavigationStarting(object? sender, CoreWebView2NavigationStartingEventArgs e)
        {
            if (e.Uri == "about:blank") return;
            if (!Uri.TryCreate(e.Uri, UriKind.Absolute, out var uri) || uri.Scheme != "https" ||
                !uri.IsDefaultPort || uri.UserInfo.Length != 0 || !Cookies.IsOfficialHost(uri.Host))
            { e.Cancel = true; NeedsUser(); }
            else navigationId = e.NavigationId;
        }
        void NavigationCompleted(object? sender, CoreWebView2NavigationCompletedEventArgs e)
        { if (e.NavigationId == navigationId && !e.IsSuccess && !completion.Task.IsCompleted) NeedsUser(); }
        void ProcessFailed(object? sender, CoreWebView2ProcessFailedEventArgs e) => NeedsUser();
        try
        {
            if (_accountKey != credential.AccountKey) { Reset(); generation = _generation; _reading = true; }
            if (_controller is null)
            {
                var environment = await CoreWebView2Environment.CreateAsync(null, Path.Combine(dataRoot, "WebViewProfile")).WaitAsync(token);
                token.ThrowIfCancellationRequested();
                if (generation != _generation) throw new OperationCanceledException(token);
                var options = environment.CreateCoreWebView2ControllerOptions();
                options.ProfileName = ProfileName(credential);
                options.IsInPrivateModeEnabled = true;
                var pending = environment.CreateCoreWebView2ControllerAsync(new IntPtr(-3), options);
                CoreWebView2Controller created;
                try { created = await pending.WaitAsync(token); }
                catch { _ = CloseWhenCreated(pending); throw; }
                if (generation != _generation || token.IsCancellationRequested) { created.Close(); token.ThrowIfCancellationRequested(); throw new OperationCanceledException(); }
                _controller = created;
                _accountKey = credential.AccountKey;
                created.IsVisible = false;
                created.Bounds = new System.Drawing.Rectangle(0, 0, 480, 800);
                var browser = created.CoreWebView2;
                browser.IsMuted = true;
                browser.Settings.IsPasswordAutosaveEnabled = false;
                browser.Settings.IsGeneralAutofillEnabled = false;
                browser.Settings.AreDefaultScriptDialogsEnabled = false;
                browser.Settings.IsStatusBarEnabled = false;
                browser.Settings.AreDevToolsEnabled = false;
                browser.NewWindowRequested += (_, e) => e.Handled = true;
                browser.DownloadStarting += (_, e) => e.Cancel = true;
                browser.PermissionRequested += (_, e) => { e.State = CoreWebView2PermissionState.Deny; e.Handled = true; };
                browser.BasicAuthenticationRequested += (_, e) => e.Cancel = true;
                browser.LaunchingExternalUriScheme += (_, e) => e.Cancel = true;
                InjectCookies(browser, credential);
            }
            core = _controller.CoreWebView2;
            core.MemoryUsageTargetLevel = CoreWebView2MemoryUsageTargetLevel.Normal;
            core.WebResourceResponseReceived += ResponseReceived;
            core.NavigationStarting += NavigationStarting;
            core.NavigationCompleted += NavigationCompleted;
            core.ProcessFailed += ProcessFailed;
            configure?.Invoke(core);
            core.Navigate(MiyousheClient.VerificationPageUrl(credential.Role));
            return await completion.Task.WaitAsync(token);
        }
        catch (OperationCanceledException) when (!cancellation.IsCancellationRequested)
        {
            throw new ApiFailure(FailureKind.Verification,
                "后台未能取得电量，已暂停自动尝试。可在方便时点击“恢复电量同步”检查官方页面。", verificationCode);
        }
        catch (Exception error) when (error is WebView2RuntimeNotFoundException or InvalidOperationException or
            System.Runtime.InteropServices.COMException or IOException or UnauthorizedAccessException)
        {
            throw new ApiFailure(FailureKind.Verification,
                "后台浏览器暂不可用，已暂停自动尝试。可点击“恢复电量同步”检查。");
        }
        finally
        {
            if (core is not null)
            {
                try
                {
                    core.WebResourceResponseReceived -= ResponseReceived;
                    core.NavigationStarting -= NavigationStarting;
                    core.NavigationCompleted -= NavigationCompleted;
                    core.ProcessFailed -= ProcessFailed;
                    core.Stop();
                    // Release the page and its timers, retaining only this account's private session.
                    core.Navigate("about:blank");
                    core.MemoryUsageTargetLevel = CoreWebView2MemoryUsageTargetLevel.Low;
                }
                catch (Exception error) when (error is InvalidOperationException or System.Runtime.InteropServices.COMException) { }
            }
            if (generation == _generation) _reading = false;
        }
    }

    private static async Task CloseWhenCreated(Task<CoreWebView2Controller> pending)
    {
        try { (await pending).Close(); }
        catch (Exception error) when (error is InvalidOperationException or System.Runtime.InteropServices.COMException) { }
    }

    internal static void InjectCookies(CoreWebView2 core, Credential credential)
    {
        foreach (var (name, value) in Cookies.Parse(credential.Cookie))
            foreach (var domain in new[] { ".mihoyo.com", ".miyoushe.com", "act.mihoyo.com" })
            {
                var cookie = core.CookieManager.CreateCookie(name, value, domain, "/");
                cookie.IsSecure = true;
                core.CookieManager.AddOrUpdateCookie(cookie);
            }
    }

    public void Reset()
    {
        _generation++;
        _reading = false;
        _controller?.Close(); _controller = null; _accountKey = null;
    }
    public void Dispose() => Reset();
}
