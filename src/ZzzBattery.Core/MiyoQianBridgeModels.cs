using System;
using System.IO;
using System.Text.Json;

namespace ZzzBattery.Core;

public enum MiyoQianState
{
    Disabled,
    Missing,
    Stopped,
    NeedsSetup,
    NeedsPassword,
    Pending,
    Running,
    Completed,
    PartialFailure,
    Error
}

public sealed record MiyoQianStatusSnapshot(
    MiyoQianState State,
    string BadgeText,
    string DetailText,
    string Url,
    int AccountsLoggedIn,
    int AccountsTotal,
    DateTimeOffset? LastRun,
    bool Running,
    bool CanTrigger)
{
    public static MiyoQianStatusSnapshot Disabled() => new(
        MiyoQianState.Disabled,
        "米游签：已关闭",
        "已在设置中关闭米游签附加签到联动。",
        "",
        0,
        0,
        null,
        false,
        false);
}

public static class MiyoQianLocator
{
    public static string? ResolveProjectDirectory(string? configuredPath, string baseDirectory)
    {
        if (!string.IsNullOrWhiteSpace(configuredPath))
        {
            var candidate = Path.GetFullPath(configuredPath.Trim());
            if (IsProjectDirectory(candidate))
                return candidate;
        }

        if (string.IsNullOrWhiteSpace(baseDirectory))
            return null;

        var current = new DirectoryInfo(Path.GetFullPath(baseDirectory));
        for (var depth = 0; depth < 7 && current is not null; depth++)
        {
            var direct = Path.Combine(current.FullName, "MiyoQian");
            if (IsProjectDirectory(direct))
                return direct;

            var nested = Path.Combine(current.FullName, "plugins", "MiyoQian");
            if (IsProjectDirectory(nested))
                return nested;

            current = current.Parent;
        }

        return null;
    }

    public static bool IsProjectDirectory(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return false;
        try
        {
            return Directory.Exists(path) && File.Exists(Path.Combine(path, "main.py"));
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or ArgumentException)
        {
            return false;
        }
    }
}

public static class MiyoQianStatusParser
{
    public static string? ParseRuntimeUrl(string? runtimeJson)
    {
        if (string.IsNullOrWhiteSpace(runtimeJson))
            return null;
        try
        {
            using var doc = JsonDocument.Parse(runtimeJson);
            if (!doc.RootElement.TryGetProperty("url", out var urlProp))
                return null;
            var raw = urlProp.GetString()?.Trim();
            return IsSafeLoopbackHttpUrl(raw) ? raw : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    public static bool IsSafeLoopbackHttpUrl(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw) || !Uri.TryCreate(raw.Trim(), UriKind.Absolute, out var uri))
            return false;
        if (uri.Scheme is not ("http" or "https"))
            return false;
        if (!string.IsNullOrEmpty(uri.UserInfo) || !string.IsNullOrEmpty(uri.Query) || !string.IsNullOrEmpty(uri.Fragment))
            return false;
        if (uri.AbsolutePath != "/")
            return false;
        return uri.Host.ToLowerInvariant() is "127.0.0.1" or "localhost" or "::1" or "[::1]";
    }

    public static (bool NeedAuth, bool PasswordSet) ParseAuthStatus(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return (false, false);
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            var needAuth = root.TryGetProperty("need_auth", out var na) && na.ValueKind == JsonValueKind.True;
            var passwordSet = root.TryGetProperty("password_set", out var ps) && ps.ValueKind == JsonValueKind.True;
            return (needAuth, passwordSet);
        }
        catch (JsonException)
        {
            return (false, false);
        }
    }

    public static MiyoQianStatusSnapshot Parse(
        bool enabled,
        string? projectDir,
        string url,
        bool reachable,
        bool needAuth,
        bool passwordSet,
        string? statusJson,
        string? errorText = null,
        string? configJson = null)
    {
        if (!enabled)
            return MiyoQianStatusSnapshot.Disabled();

        if (!reachable)
        {
            if (string.IsNullOrWhiteSpace(projectDir))
            {
                return new(
                    MiyoQianState.Missing,
                    "米游签：未检测到目录",
                    "未找到 MiyoQian 子目录，可在设置中手动指定目录。",
                    "",
                    0,
                    0,
                    null,
                    false,
                    false);
            }

            var detail = string.IsNullOrWhiteSpace(errorText)
                ? "米游签后台未运行，点击「执行」或「网页配置」将自动静默启动。"
                : $"米游签后台未运行（{errorText}）。";
            return new(
                MiyoQianState.Stopped,
                "米游签：未启动",
                detail,
                url,
                0,
                0,
                null,
                false,
                true);
        }

        if (needAuth)
        {
            return passwordSet
                ? new(
                    MiyoQianState.NeedsPassword,
                    "米游签：需密码解锁",
                    "请在设置中填写米游签访问密码，或点击网页配置登录后使用。",
                    url,
                    0,
                    0,
                    null,
                    false,
                    false)
                : new(
                    MiyoQianState.NeedsSetup,
                    "米游签：待网页初始化",
                    "首次使用需打开米游签网页设置访问密码并扫码连接账号。",
                    url,
                    0,
                    0,
                    null,
                    false,
                    false);
        }

        if (string.IsNullOrWhiteSpace(statusJson))
        {
            return new(
                MiyoQianState.Error,
                "米游签：状态异常",
                string.IsNullOrWhiteSpace(errorText) ? "未能读取米游签运行状态。" : errorText,
                url,
                0,
                0,
                null,
                false,
                true);
        }

        try
        {
            using var doc = JsonDocument.Parse(statusJson);
            var root = doc.RootElement;
            var running = false;
            var todayCompleted = false;
            var todaySuccess = false;
            var summary = "";
            var accountsTotal = 0;
            var accountsLoggedIn = 0;
            var hasAccountInfo = false;
            DateTimeOffset? lastRun = null;

            if (root.TryGetProperty("checkin_summary", out var cs) && cs.ValueKind == JsonValueKind.Object)
            {
                hasAccountInfo = true;
                running = cs.TryGetProperty("running", out var rp) && rp.ValueKind == JsonValueKind.True;
                todayCompleted = cs.TryGetProperty("today_completed", out var tcp) && tcp.ValueKind == JsonValueKind.True;
                todaySuccess = cs.TryGetProperty("today_success", out var tsp) && tsp.ValueKind == JsonValueKind.True;
                if (cs.TryGetProperty("summary", out var sp) && sp.ValueKind == JsonValueKind.String)
                    summary = sp.GetString() ?? "";
                if (cs.TryGetProperty("accounts_total", out var atp) && atp.TryGetInt32(out var at))
                    accountsTotal = at;
                if (cs.TryGetProperty("accounts_logged_in", out var alp) && alp.TryGetInt32(out var al))
                    accountsLoggedIn = al;
                if (cs.TryGetProperty("last_run", out var lrp) && lrp.ValueKind == JsonValueKind.String &&
                    DateTimeOffset.TryParse(lrp.GetString(), out var parsedTime))
                    lastRun = parsedTime;
            }
            else if (root.TryGetProperty("scheduler", out var sch) && sch.ValueKind == JsonValueKind.Object)
            {
                running = sch.TryGetProperty("running", out var rp) && rp.ValueKind == JsonValueKind.True;
                if (sch.TryGetProperty("last_run", out var lrp) && lrp.ValueKind == JsonValueKind.String &&
                    DateTimeOffset.TryParse(lrp.GetString(), out var parsedTime))
                {
                    lastRun = parsedTime;
                    var bjtToday = DateOnly.FromDateTime(DateTimeOffset.UtcNow.ToOffset(TimeSpan.FromHours(8)).DateTime);
                    var runDay = DateOnly.FromDateTime(parsedTime.ToOffset(TimeSpan.FromHours(8)).DateTime);
                    if (runDay == bjtToday)
                    {
                        todayCompleted = true;
                        todaySuccess = !(sch.TryGetProperty("last_error", out var ep) &&
                            ep.ValueKind == JsonValueKind.String &&
                            !string.IsNullOrWhiteSpace(ep.GetString()));
                    }
                }
                if (!string.IsNullOrWhiteSpace(configJson))
                {
                    try
                    {
                        using var cfgDoc = JsonDocument.Parse(configJson);
                        if (cfgDoc.RootElement.TryGetProperty("accounts", out var accArr) && accArr.ValueKind == JsonValueKind.Array)
                        {
                            hasAccountInfo = true;
                            foreach (var acc in accArr.EnumerateArray())
                            {
                                accountsTotal++;
                                if (acc.TryGetProperty("stuid", out var uidProp) && !string.IsNullOrWhiteSpace(uidProp.GetString()))
                                    accountsLoggedIn++;
                            }
                        }
                    }
                    catch (JsonException) { }
                }
            }

            if (running)
            {
                return new(
                    MiyoQianState.Running,
                    "米游签：签到中",
                    "正在后台执行米游签签到任务，请稍候…",
                    url,
                    accountsLoggedIn,
                    accountsTotal,
                    lastRun,
                    true,
                    false);
            }

            if (hasAccountInfo && (accountsTotal == 0 || accountsLoggedIn == 0))
            {
                return new(
                    MiyoQianState.NeedsSetup,
                    "米游签：待连接账号",
                    "尚未在米游签中连接已登录账号，请点击「配置网页」扫码连接。",
                    url,
                    accountsLoggedIn,
                    accountsTotal,
                    lastRun,
                    false,
                    false);
            }

            var timeText = lastRun is { } lr ? lr.ToLocalTime().ToString("HH:mm") + " " : "";
            if (todayCompleted)
            {
                if (todaySuccess)
                {
                    var detail = string.IsNullOrWhiteSpace(summary)
                        ? (hasAccountInfo
                            ? $"{timeText}今日签到任务已全部完成（已连账号 {accountsLoggedIn}/{accountsTotal}）。"
                            : $"{timeText}今日签到任务已全部完成。")
                        : $"{timeText}{summary}";
                    return new(
                        MiyoQianState.Completed,
                        "米游签：今日已完成",
                        detail,
                        url,
                        accountsLoggedIn,
                        accountsTotal,
                        lastRun,
                        false,
                        true);
                }

                var failDetail = string.IsNullOrWhiteSpace(summary)
                    ? $"{timeText}今日任务存在失败或跳过项，可打开网页查看日志。"
                    : $"{timeText}{summary}";
                return new(
                    MiyoQianState.PartialFailure,
                    "米游签：今日有失败",
                    failDetail,
                    url,
                    accountsLoggedIn,
                    accountsTotal,
                    lastRun,
                    false,
                    true);
            }

            return new(
                MiyoQianState.Pending,
                "米游签：今日待签到",
                hasAccountInfo
                    ? $"已连账号 {accountsLoggedIn}/{accountsTotal}，点击「签到」即可后台完成签到。"
                    : "点击「签到」即可后台完成今日米游签任务。",
                url,
                accountsLoggedIn,
                accountsTotal,
                lastRun,
                false,
                true);
        }
        catch (JsonException)
        {
            return new(
                MiyoQianState.Error,
                "米游签：状态异常",
                "米游签状态数据格式异常。",
                url,
                0,
                0,
                null,
                false,
                true);
        }
    }
}
