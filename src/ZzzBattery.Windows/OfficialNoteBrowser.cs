using System;
using System.IO;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Web.WebView2.Core;
using ZzzBattery.Core;

namespace ZzzBattery.Windows;

// HWND_MESSAGE cannot appear on the desktop or receive foreground focus.
internal sealed class OfficialNoteBrowser(LocalStore store) : IDisposable
{
    private CoreWebView2Controller? _controller;
    private string? _accountKey;
    private int _generation;
    private bool _reading;
    private readonly SemaphoreSlim _initializing = new(1, 1);
    private string _phase = "idle";
    public event Action<Credential>? CredentialChanged;
    internal bool InvisibleForCheck => _controller is { IsVisible: false, ParentWindow: var parent } && parent == new IntPtr(-3);
    internal static string ProfileName(Credential credential) =>
        "Energy_" + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(credential.AccountKey)))[..24];

    internal void ClearSessionCookies()
    {
        try { _controller?.CoreWebView2?.CookieManager.DeleteAllCookies(); }
        catch (Exception error) when (error is InvalidOperationException or System.Runtime.InteropServices.COMException) { }
    }

    public Task<EnergySnapshot> ReadAsync(Credential credential, CancellationToken cancellation) =>
        ReadCoreAsync(credential, cancellation);

    internal async Task<bool> TryRefreshCredentialAsync(Credential credential, CancellationToken cancellation)
    {
        if (_reading) return false;
        try
        {
            var (core, generation) = await EnsureBrowserAsync(credential, cancellation, allowReplace: false);
            if (generation != _generation || cancellation.IsCancellationRequested) return false;
            var changed = await OfficialBrowserSession.SaveAsync(core, credential, store, cancellation);
            if (changed && generation == _generation && !cancellation.IsCancellationRequested)
                CredentialChanged?.Invoke(credential);
            return changed;
        }
        catch (OperationCanceledException) when (!cancellation.IsCancellationRequested)
        { return false; }
        catch (Exception error) when (error is ArgumentException or InvalidOperationException or
            System.Runtime.InteropServices.COMException or IOException or UnauthorizedAccessException or
            CryptographicException or WebView2RuntimeNotFoundException)
        { return false; }
    }

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
        var processFailed = false;
        int? verificationCode = null;
        var verificationResponses = 0;
        ulong? navigationId = null;
        void NeedsUser() => completion.TrySetException(new ApiFailure(FailureKind.Verification,
            "官方页面暂未完成恢复，将在冷却后自动重试。", verificationCode));
        void TransientFailure(string message) => completion.TrySetException(new ApiFailure(FailureKind.Network, message));
        async void ResponseReceived(object? sender, CoreWebView2WebResourceResponseReceivedEventArgs e)
        {
            if (token.IsCancellationRequested || completion.Task.IsCompleted || readingResponse ||
                !string.Equals(e.Request.Method, "GET", StringComparison.OrdinalIgnoreCase) ||
                !Uri.TryCreate(e.Request.Uri, UriKind.Absolute, out var uri) || !MiyousheClient.IsNoteEndpoint(uri)) return;
            // The official page can briefly request its remembered/default role before
            // switching to the selected one. Ignore that response and keep listening.
            if (!MiyousheClient.IsNoteRequestForRole(uri, credential.Role)) return;
            readingResponse = true;
            try
            {
                using var stream = await e.Response.GetContentAsync().WaitAsync(token);
                using var body = new MemoryStream();
                var buffer = new byte[8192];
                int count;
                while ((count = await stream.ReadAsync(buffer, token)) > 0)
                {
                    if (body.Length + count > 2_000_000) { TransientFailure("官方页面响应过大，将自动重试。"); return; }
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
                else if (capture.State == OfficialNoteCaptureState.HttpError && capture.Code is 401 or 403)
                {
                    completion.TrySetException(new ApiFailure(FailureKind.Login, "官方网页登录已失效，请重新连接米游社。", capture.Code));
                }
                else if (capture.State == OfficialNoteCaptureState.HttpError)
                {
                    TransientFailure($"官方网页便笺请求暂未成功（HTTP {capture.Code}），将自动重试。");
                }
                else if (capture.State == OfficialNoteCaptureState.InvalidData)
                {
                    TransientFailure("官方网页响应暂不可用，将自动重试。");
                }
                else NeedsUser();
            }
            catch (OperationCanceledException) { }
            catch (Exception error) when (error is IOException or InvalidOperationException or System.Runtime.InteropServices.COMException or ArgumentException)
            { TransientFailure("后台读取官方网页响应受阻，将自动重试。"); }
            finally { readingResponse = false; }
        }
        void NavigationStarting(object? sender, CoreWebView2NavigationStartingEventArgs e)
        {
            if (e.Uri == "about:blank") return;
            if (!OfficialNavigationPolicy.IsAllowed(e.Uri))
            {
                e.Cancel = true;
                if (verificationCode is null) TransientFailure("官方页面跳转到受限地址，将自动重试。");
                else NeedsUser();
            }
            else navigationId = e.NavigationId;
        }
        void NavigationCompleted(object? sender, CoreWebView2NavigationCompletedEventArgs e)
        {
            if (e.NavigationId == navigationId && !e.IsSuccess && !completion.Task.IsCompleted)
                completion.TrySetException(new ApiFailure(FailureKind.Network, "官方页面暂时无法连接，将自动重试。"));
        }
        void ProcessFailed(object? sender, CoreWebView2ProcessFailedEventArgs e)
        {
            processFailed = true;
            completion.TrySetException(new ApiFailure(FailureKind.Network, "后台浏览器已退出，将自动重建并重试。"));
        }
        try
        {
            var (ensuredCore, ensuredGeneration) = await EnsureBrowserAsync(credential, token);
            core = ensuredCore;
            generation = ensuredGeneration;
            if (generation != _generation || token.IsCancellationRequested) throw new OperationCanceledException(token);
            _reading = true;
            core.MemoryUsageTargetLevel = CoreWebView2MemoryUsageTargetLevel.Normal;
            core.WebResourceResponseReceived += ResponseReceived;
            core.NavigationStarting += NavigationStarting;
            core.NavigationCompleted += NavigationCompleted;
            core.ProcessFailed += ProcessFailed;
            configure?.Invoke(core);
            core.Navigate(MiyousheClient.VerificationPageUrl(credential.Role));
            _phase = "reading-note";
            var snapshot = await completion.Task.WaitAsync(token);
            if (generation != _generation || token.IsCancellationRequested) throw new OperationCanceledException(token);
            var saved = await OfficialBrowserSession.SaveAsync(core, credential, store, token);
            if (generation != _generation || token.IsCancellationRequested) throw new OperationCanceledException(token);
            if (saved) CredentialChanged?.Invoke(credential);
            return snapshot;
        }
        catch (OperationCanceledException) when (!cancellation.IsCancellationRequested)
        {
            try { store.Write("background-recovery.json", new { phase = _phase, code = verificationCode, at = DateTimeOffset.UtcNow }); }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException) { }
            throw new ApiFailure(verificationCode is null ? FailureKind.Network : FailureKind.Verification,
                "官方页面读取超时，将在后台自动重试。", verificationCode);
        }
        catch (Exception error) when (error is WebView2RuntimeNotFoundException or InvalidOperationException or
            System.Runtime.InteropServices.COMException or IOException or UnauthorizedAccessException or CryptographicException)
        {
            throw new ApiFailure(FailureKind.Network,
                "后台浏览器或会话存储暂不可用，将自动重试。");
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
            if (processFailed && generation == _generation)
            {
                try { _controller?.Close(); }
                catch (Exception error) when (error is InvalidOperationException or System.Runtime.InteropServices.COMException) { }
                _controller = null; _accountKey = null;
            }
            if (generation == _generation) _reading = false;
        }
    }

    private static async Task CloseWhenCreated(Task<CoreWebView2Controller> pending)
    {
        try { (await pending).Close(); }
        catch (Exception error) when (error is InvalidOperationException or System.Runtime.InteropServices.COMException) { }
    }

    private async Task<(CoreWebView2 Core, int Generation)> EnsureBrowserAsync(Credential credential, CancellationToken token, bool allowReplace = true)
    {
        await _initializing.WaitAsync(token);
        try
        {
            if (_accountKey != credential.AccountKey)
            {
                var reading = _reading;
                Reset();
                _reading = reading;
            }
            var generation = _generation;
            if (_controller is null)
            {
                var profileFolder = store.WebViewProfilePath;
                _phase = "creating-environment";
                var environment = await CoreWebView2Environment.CreateAsync(null, profileFolder).WaitAsync(token);
                token.ThrowIfCancellationRequested();
                if (generation != _generation) throw new OperationCanceledException(token);
                var options = environment.CreateCoreWebView2ControllerOptions();
                options.ProfileName = ProfileName(credential);
                _phase = "creating-controller";
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
            }
            var controller = _controller;
            var core = controller.CoreWebView2;
            _phase = "restoring-session";
            await OfficialBrowserSession.PrepareAsync(core, credential, store, token, allowReplace);
            if (generation != _generation || token.IsCancellationRequested || !ReferenceEquals(_controller, controller))
                throw new OperationCanceledException(token);
            return (core, generation);
        }
        catch
        {
            Reset();
            throw;
        }
        finally { _initializing.Release(); }
    }

    public void Reset()
    {
        _generation++;
        _reading = false;
        var controller = _controller;
        _controller = null;
        _accountKey = null;
        try { controller?.Close(); }
        catch (Exception error) when (error is InvalidOperationException or System.Runtime.InteropServices.COMException) { }
    }
    public void Dispose() => Reset();
}
