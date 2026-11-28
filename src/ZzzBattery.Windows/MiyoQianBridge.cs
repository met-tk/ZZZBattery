using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using ZzzBattery.Core;

namespace ZzzBattery.Windows;

internal sealed partial class MiyoQianBridge : IDisposable
{
    private const string PasswordSecretFile = "miyouqian-auth.dpapi";
    private const string DefaultUrl = "http://127.0.0.1:5890";

    private readonly LocalStore _store;
    private readonly Func<WidgetSettings> _settings;
    private readonly CookieContainer _cookies = new();
    private readonly HttpClient _http;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly CancellationTokenSource _shutdown = new();
    private int _disposed;

    private bool IsDisposed => Volatile.Read(ref _disposed) != 0;

    public MiyoQianStatusSnapshot Snapshot { get; private set; } = MiyoQianStatusSnapshot.Disabled();
    public string? ProjectDirectory => MiyoQianLocator.ResolveProjectDirectory(_settings().MiyoQianPath, AppContext.BaseDirectory);
    public event Action? Changed;

    public MiyoQianBridge(LocalStore store, Func<WidgetSettings> settings)
    {
        _store = store;
        _settings = settings;
        var handler = new HttpClientHandler
        {
            UseCookies = true,
            CookieContainer = _cookies,
            UseProxy = false,
            AllowAutoRedirect = false,
            AutomaticDecompression = DecompressionMethods.All
        };
        _http = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(5) };
    }

    public bool HasSavedPassword() => !string.IsNullOrEmpty(_store.ReadProtected<string>(PasswordSecretFile));

    public void SavePassword(string? password)
    {
        if (string.IsNullOrEmpty(password))
            _store.WriteProtected(PasswordSecretFile, "");
        else
            _store.WriteProtected(PasswordSecretFile, password);
    }

    public string ResolveUrl(string? projectDir)
    {
        if (!string.IsNullOrWhiteSpace(projectDir))
        {
            var runtimePath = Path.Combine(projectDir, "data", "web_runtime.json");
            try
            {
                if (File.Exists(runtimePath))
                {
                    var parsed = MiyoQianStatusParser.ParseRuntimeUrl(File.ReadAllText(runtimePath));
                    if (!string.IsNullOrWhiteSpace(parsed))
                        return parsed;
                }
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException) { }

            var configPath = Path.Combine(projectDir, "config.yaml");
            try
            {
                if (File.Exists(configPath))
                {
                    var text = File.ReadAllText(configPath);
                    var match = WebPortRegex().Match(text);
                    if (match.Success && int.TryParse(match.Groups[1].Value, out var port) && port is >= 1 and <= 65535)
                        return $"http://127.0.0.1:{port}";
                }
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException) { }
        }
        return DefaultUrl;
    }

    public async Task<MiyoQianStatusSnapshot> RefreshStatusAsync(bool allowAutoStart = false, CancellationToken ct = default)
    {
        if (IsDisposed)
            return Snapshot;

        using var linkedCts = TryCreateLinkedCts(ct);
        if (linkedCts is null)
            return Snapshot;
        var token = linkedCts.Token;

        var settings = _settings();
        if (!settings.MiyoQianEnabled)
        {
            UpdateSnapshot(MiyoQianStatusSnapshot.Disabled());
            return Snapshot;
        }

        var projectDir = ProjectDirectory;
        var url = ResolveUrl(projectDir);

        var (reachable, needAuth, passwordSet, authErr) = await ProbeAuthAsync(url, token);
        if (!reachable && allowAutoStart && settings.MiyoQianAutoStart && !string.IsNullOrWhiteSpace(projectDir) && !IsDisposed)
        {
            await EnsureRunningAsync(token);
            url = ResolveUrl(projectDir);
            (reachable, needAuth, passwordSet, authErr) = await ProbeAuthAsync(url, token);
        }

        if (IsDisposed)
            return Snapshot;

        if (!reachable)
        {
            UpdateSnapshot(MiyoQianStatusParser.Parse(true, projectDir, url, false, false, false, null, authErr));
            return Snapshot;
        }

        if (needAuth && passwordSet)
        {
            var loggedIn = await TrySessionOrPasswordLoginAsync(url, token);
            if (!loggedIn)
            {
                if (!IsDisposed)
                    UpdateSnapshot(MiyoQianStatusParser.Parse(true, projectDir, url, true, true, true, null));
                return Snapshot;
            }
            needAuth = false;
        }
        else if (needAuth)
        {
            UpdateSnapshot(MiyoQianStatusParser.Parse(true, projectDir, url, true, true, false, null));
            return Snapshot;
        }

        var (statusJson, statusCode, statusErr) = await GetStatusJsonAsync(url, token);
        if (IsDisposed)
            return Snapshot;
        if (statusCode == HttpStatusCode.Unauthorized)
        {
            UpdateSnapshot(MiyoQianStatusParser.Parse(true, projectDir, url, true, true, passwordSet, null));
            return Snapshot;
        }

        string? configJson = null;
        if (!string.IsNullOrWhiteSpace(statusJson) && !statusJson.Contains("\"checkin_summary\"", StringComparison.Ordinal))
            configJson = await GetConfigJsonAsync(url, token);

        if (!IsDisposed)
            UpdateSnapshot(MiyoQianStatusParser.Parse(true, projectDir, url, true, false, passwordSet, statusJson, statusErr, configJson));
        return Snapshot;
    }

    public async Task<(bool Started, string Message)> TriggerRunAsync(CancellationToken ct = default)
    {
        if (IsDisposed)
            return (false, "程序正在关闭。");

        using var linkedCts = TryCreateLinkedCts(ct);
        if (linkedCts is null)
            return (false, "程序正在关闭。");
        var token = linkedCts.Token;

        var settings = _settings();
        if (!settings.MiyoQianEnabled)
            return (false, "未启用米游签联动。");

        var ready = await EnsureRunningAsync(token);
        if (IsDisposed)
            return (false, "程序正在关闭。");

        var projectDir = ProjectDirectory;
        var url = ResolveUrl(projectDir);
        if (!ready)
        {
            await RefreshStatusAsync(false, token);
            return (false, Snapshot.DetailText);
        }

        var (reachable, needAuth, passwordSet, _) = await ProbeAuthAsync(url, token);
        if (!reachable)
        {
            await RefreshStatusAsync(false, token);
            return (false, "未能连接米游签本机服务。");
        }

        if (needAuth)
        {
            if (!passwordSet || !await TrySessionOrPasswordLoginAsync(url, token))
            {
                await RefreshStatusAsync(false, token);
                return (false, Snapshot.DetailText);
            }
        }

        try
        {
            using var content = new StringContent("{}", Encoding.UTF8, "application/json");
            using var response = await _http.PostAsync(url + "/api/run", content, token);
            if (response.StatusCode == HttpStatusCode.Conflict)
            {
                await RefreshStatusAsync(false, token);
                return (true, "米游签任务已在执行中。");
            }
            if (!response.IsSuccessStatusCode)
            {
                await RefreshStatusAsync(false, token);
                return (false, $"触发米游签失败（HTTP {(int)response.StatusCode}）。");
            }

            await RefreshStatusAsync(false, token);
            return (true, "已触发米游签后台签到任务。");
        }
        catch (Exception e) when (e is HttpRequestException or TaskCanceledException or OperationCanceledException or ObjectDisposedException)
        {
            if (!IsDisposed)
                await RefreshStatusAsync(false, token);
            return (false, IsDisposed ? "程序正在关闭。" : "请求米游签服务超时或失败。");
        }
    }

    public async Task<(bool Opened, string Message)> OpenWebConsoleAsync(CancellationToken ct = default)
    {
        if (IsDisposed)
            return (false, "程序正在关闭。");

        using var linkedCts = TryCreateLinkedCts(ct);
        if (linkedCts is null)
            return (false, "程序正在关闭。");
        var token = linkedCts.Token;

        var projectDir = ProjectDirectory;
        if (!string.IsNullOrWhiteSpace(projectDir))
            await EnsureRunningAsync(token);

        if (IsDisposed)
            return (false, "程序正在关闭。");

        var url = ResolveUrl(projectDir);
        if (!MiyoQianStatusParser.IsSafeLoopbackHttpUrl(url))
            return (false, "仅允许打开本机米游签控制台地址。");

        try
        {
            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
            return (true, $"已打开 {url}");
        }
        catch (Exception e) when (e is System.ComponentModel.Win32Exception or InvalidOperationException)
        {
            return (false, "无法打开系统默认浏览器。");
        }
    }

    public async Task<bool> EnsureRunningAsync(CancellationToken ct = default)
    {
        if (IsDisposed)
            return false;

        using var linkedCts = TryCreateLinkedCts(ct);
        if (linkedCts is null)
            return false;
        var token = linkedCts.Token;

        var acquired = false;
        try
        {
            await _gate.WaitAsync(token);
            acquired = true;
            if (IsDisposed)
                return false;

            var projectDir = ProjectDirectory;
            var url = ResolveUrl(projectDir);
            var (reachable, _, _, _) = await ProbeAuthAsync(url, token);
            if (reachable)
                return true;

            if (string.IsNullOrWhiteSpace(projectDir) || IsDisposed)
                return false;

            var runner = ResolveRunner(projectDir);
            if (runner is null)
                return false;

            var psi = new ProcessStartInfo(runner.Value.FileName, runner.Value.Arguments)
            {
                WorkingDirectory = projectDir,
                UseShellExecute = false,
                CreateNoWindow = true,
                WindowStyle = ProcessWindowStyle.Hidden
            };
            using var proc = Process.Start(psi);
            if (proc is null)
                return false;

            for (var i = 0; i < 32 && !IsDisposed; i++)
            {
                await Task.Delay(350, token);
                url = ResolveUrl(projectDir);
                var (ok, _, _, _) = await ProbeAuthAsync(url, token);
                if (ok)
                    return true;
                if (proc.HasExited && i >= 2)
                    break;
            }

            return false;
        }
        catch (Exception e) when (e is IOException or System.ComponentModel.Win32Exception or InvalidOperationException or OperationCanceledException or ObjectDisposedException)
        {
            return false;
        }
        finally
        {
            if (acquired)
            {
                try { _gate.Release(); }
                catch (ObjectDisposedException) { }
            }
        }
    }

    private static (string FileName, string Arguments)? ResolveRunner(string projectDir)
    {
        var venvPythonW = Path.Combine(projectDir, ".venv", "Scripts", "pythonw.exe");
        if (File.Exists(venvPythonW))
            return (venvPythonW, "main.py serve");

        var venvPython = Path.Combine(projectDir, ".venv", "Scripts", "python.exe");
        if (File.Exists(venvPython))
            return (venvPython, "main.py serve");

        var localUv = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".local", "bin", "uv.exe");
        if (File.Exists(localUv))
            return (localUv, "run pythonw main.py serve");

        foreach (var dir in (Environment.GetEnvironmentVariable("PATH") ?? "").Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
        {
            try
            {
                var candidate = Path.Combine(dir.Trim(), "uv.exe");
                if (File.Exists(candidate))
                    return (candidate, "run pythonw main.py serve");
            }
            catch (ArgumentException) { }
        }

        return null;
    }

    private CancellationTokenSource? TryCreateLinkedCts(CancellationToken ct)
    {
        if (IsDisposed)
            return null;
        try
        {
            return CancellationTokenSource.CreateLinkedTokenSource(ct, _shutdown.Token);
        }
        catch (ObjectDisposedException)
        {
            return null;
        }
    }

    private async Task<(bool Reachable, bool NeedAuth, bool PasswordSet, string? Error)> ProbeAuthAsync(string url, CancellationToken ct)
    {
        if (IsDisposed)
            return (false, false, false, null);
        if (!MiyoQianStatusParser.IsSafeLoopbackHttpUrl(url))
            return (false, false, false, "非本机回环地址");
        try
        {
            using var response = await _http.GetAsync(url + "/api/auth/status", ct);
            if (!response.IsSuccessStatusCode)
                return (false, false, false, $"HTTP {(int)response.StatusCode}");
            var json = await response.Content.ReadAsStringAsync(ct);
            var (needAuth, passwordSet) = MiyoQianStatusParser.ParseAuthStatus(json);
            return (true, needAuth, passwordSet, null);
        }
        catch (Exception e) when (e is HttpRequestException or TaskCanceledException or OperationCanceledException or ObjectDisposedException)
        {
            return (false, false, false, null);
        }
    }

    private async Task<bool> TrySessionOrPasswordLoginAsync(string url, CancellationToken ct)
    {
        if (IsDisposed)
            return false;
        // Existing cookie might already be authenticated.
        var (_, probeCode, _) = await GetStatusJsonAsync(url, ct);
        if (probeCode == HttpStatusCode.OK)
            return true;

        var password = _store.ReadProtected<string>(PasswordSecretFile);
        if (string.IsNullOrEmpty(password))
            return false;

        try
        {
            var payload = System.Text.Json.JsonSerializer.Serialize(new { password });
            using var content = new StringContent(payload, Encoding.UTF8, "application/json");
            using var response = await _http.PostAsync(url + "/api/auth/login", content, ct);
            return response.IsSuccessStatusCode;
        }
        catch (Exception e) when (e is HttpRequestException or TaskCanceledException or OperationCanceledException or ObjectDisposedException)
        {
            return false;
        }
    }

    private async Task<(string? Json, HttpStatusCode StatusCode, string? Error)> GetStatusJsonAsync(string url, CancellationToken ct)
    {
        if (IsDisposed)
            return (null, 0, null);
        try
        {
            using var response = await _http.GetAsync(url + "/api/status", ct);
            if (!response.IsSuccessStatusCode)
                return (null, response.StatusCode, $"HTTP {(int)response.StatusCode}");
            return (await response.Content.ReadAsStringAsync(ct), response.StatusCode, null);
        }
        catch (Exception e) when (e is HttpRequestException or TaskCanceledException or OperationCanceledException or ObjectDisposedException)
        {
            return (null, 0, IsDisposed ? null : "读取状态超时");
        }
    }

    private async Task<string?> GetConfigJsonAsync(string url, CancellationToken ct)
    {
        if (IsDisposed)
            return null;
        try
        {
            using var response = await _http.GetAsync(url + "/api/config", ct);
            return response.IsSuccessStatusCode ? await response.Content.ReadAsStringAsync(ct) : null;
        }
        catch (Exception e) when (e is HttpRequestException or TaskCanceledException or OperationCanceledException or ObjectDisposedException)
        {
            return null;
        }
    }

    private void UpdateSnapshot(MiyoQianStatusSnapshot next)
    {
        if (IsDisposed || Equals(Snapshot, next))
            return;
        Snapshot = next;
        Changed?.Invoke();
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
            return;
        try { _shutdown.Cancel(); } catch (ObjectDisposedException) { }
        _http.Dispose();
        try { _gate.Dispose(); } catch (ObjectDisposedException) { }
        _shutdown.Dispose();
    }

    [GeneratedRegex(@"^\s*port:\s*(\d+)", RegexOptions.Multiline)]
    private static partial Regex WebPortRegex();
}
