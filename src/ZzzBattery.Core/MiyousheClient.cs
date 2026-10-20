using System.Globalization;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace ZzzBattery.Core;

public interface IMiyousheClient
{
    Task<IReadOnlyList<GameRole>> GetRolesAsync(Credential credential, CancellationToken cancellation = default);
    Task<EnergySnapshot> GetNoteAsync(Credential credential, GameRole role, CancellationToken cancellation = default);
}

public sealed partial class MiyousheClient(HttpClient http, string deviceId, bool includeWidget = false) : IMiyousheClient
{
    public const string NoteEndpoint = "https://api-takumi-record.mihoyo.com/event/game_record_zzz/api/zzz/note";
    public const string RolesEndpoint = "https://api-takumi.mihoyo.com/binding/api/getUserGameRolesByCookie";
    public const string WidgetEndpoint = "https://api-takumi-record.mihoyo.com/event/game_record_zzz/api/zzz/widget";
    public const string WidgetAppVersion = "2.115.0";
    private readonly Dictionary<string, (IReadOnlyList<GameRole> Roles, DateTimeOffset At)> _roles = new();
    private readonly Dictionary<string, DateTimeOffset> _widgetBackoff = new();
    public const string DsSalt = "xV8v4Qu54lUKrEYFZkJhB8cuOh9Asafs";

    public static string Query(IEnumerable<KeyValuePair<string, string>> fields) =>
        string.Join("&", fields.OrderBy(p => p.Key, StringComparer.Ordinal)
            .Select(p => Uri.EscapeDataString(p.Key) + "=" + Uri.EscapeDataString(p.Value)));

    public static string Sign(string query, long timestamp, int nonce, string body = "")
    {
        var digest = MD5.HashData(Encoding.UTF8.GetBytes($"salt={DsSalt}&t={timestamp}&r={nonce}&b={body}&q={query}"));
        return $"{timestamp},{nonce},{Convert.ToHexString(digest).ToLowerInvariant()}";
    }

    private async Task<JsonElement> GetAsync(string endpoint, string query, Credential credential, CancellationToken cancellation,
        string? requestBody = null, IReadOnlyDictionary<string, string>? extraHeaders = null, bool verificationEnvelope = false)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellation);
        deadline.CancelAfter(TimeSpan.FromSeconds(20));
        var token = deadline.Token;
        using var request = new HttpRequestMessage(requestBody is null ? HttpMethod.Get : HttpMethod.Post, endpoint + (query.Length > 0 ? "?" + query : ""));
        if (requestBody is not null) request.Content = new StringContent(requestBody, Encoding.UTF8, "application/json");
        if (credential.Cookie.Length > 0) request.Headers.Add("Cookie", credential.Cookie);
        request.Headers.Add("DS", Sign(query, DateTimeOffset.UtcNow.ToUnixTimeSeconds(), RandomNumberGenerator.GetInt32(100001, 200001), requestBody ?? ""));
        // The widget server gates activity_list by the app version, even when the
        // older profile otherwise returns retcode 0 and valid daily-state fields.
        request.Headers.Add("x-rpc-app_version", endpoint == WidgetEndpoint ? WidgetAppVersion : "2.11.1");
        request.Headers.Add("x-rpc-client_type", "5");
        request.Headers.Add("x-rpc-device_id", deviceId);
        request.Headers.Add("x-rpc-language", "zh-cn");
        request.Headers.Add("x-rpc-platform", "4");
        request.Headers.Referrer = new Uri(endpoint.Contains("game_record_zzz", StringComparison.OrdinalIgnoreCase) || endpoint.Contains("toolcomsrv", StringComparison.OrdinalIgnoreCase)
            ? "https://act.mihoyo.com/app/mihoyo-zzz-game-record/m.html"
            : "https://webstatic.mihoyo.com/");
        request.Headers.Add("Origin", "https://act.mihoyo.com");
        request.Headers.UserAgent.ParseAdd(endpoint == WidgetEndpoint ? $"Mozilla/5.0 miHoYoBBS/{WidgetAppVersion}" :
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 Chrome/131.0.0.0 Safari/537.36");
        request.Headers.Accept.ParseAdd("application/json");
        if (extraHeaders is not null)
            foreach (var header in extraHeaders)
            {
                request.Headers.Remove(header.Key);
                request.Headers.Add(header.Key, header.Value);
            }
        try
        {
            using var response = await http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, token);
            if (response.StatusCode == HttpStatusCode.TooManyRequests)
            {
                var delay = response.Headers.RetryAfter?.Delta ??
                    (response.Headers.RetryAfter?.Date - DateTimeOffset.UtcNow) ?? TimeSpan.FromMinutes(15);
                throw new ApiFailure(FailureKind.RateLimited, "请求受限，稍后自动重试。", 429, delay);
            }
            if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
                throw new ApiFailure(FailureKind.Login, "登录已失效，请重新连接米游社。", (int)response.StatusCode);
            if (!response.IsSuccessStatusCode)
                throw new ApiFailure(FailureKind.Network, $"服务暂不可用（HTTP {(int)response.StatusCode}）。");
            if (response.Content.Headers.ContentLength > 2_000_000)
                throw new ApiFailure(FailureKind.Protocol, "服务器响应过大，请稍后重试。");
            await using var stream = await response.Content.ReadAsStreamAsync(token);
            using var body = new MemoryStream();
            var buffer = new byte[8192];
            int count;
            while ((count = await stream.ReadAsync(buffer, token)) > 0)
            {
                if (body.Length + count > 2_000_000)
                    throw new ApiFailure(FailureKind.Protocol, "服务器响应过大，请稍后重试。");
                body.Write(buffer, 0, count);
            }
            using var doc = JsonDocument.Parse(body.ToArray());
            var root = doc.RootElement;
            var code = JsonValue.Int(root, "retcode");
            // The official risk endpoint also has a flat {challenge: ...} success envelope.
            if (code is null && verificationEnvelope && endpoint == VerifyVerificationEndpoint &&
                IsSafeVerificationValue(JsonValue.String(root, "challenge"))) code = 0;
            if (code is null) throw new ApiFailure(FailureKind.Protocol, "服务器返回了无法识别的数据。");
            if (code != 0)
            {
                var failure = FromRetcode(code.Value);
                failure.VerificationPath = endpoint == WidgetEndpoint ? "/api/zzz/widget" : "/api/zzz/note";
                if (response.Headers.TryGetValues("x-trace-id", out var traces))
                {
                    var trace = traces.FirstOrDefault();
                    if (IsSafeVerificationValue(trace, 256)) failure.VerificationTrace = trace;
                }
                throw failure;
            }
            if (verificationEnvelope) return root.Clone();
            if (!root.TryGetProperty("data", out var data) || data.ValueKind != JsonValueKind.Object)
                throw new ApiFailure(FailureKind.Protocol, "接口没有返回有效数据。");
            return data.Clone();
        }
        catch (OperationCanceledException) when (!cancellation.IsCancellationRequested)
        { throw new ApiFailure(FailureKind.Network, "连接超时，将保留上次数据并稍后重试。"); }
        catch (HttpRequestException)
        { throw new ApiFailure(FailureKind.Network, "无法连接米游社，请检查网络或代理设置。"); }
        catch (IOException)
        { throw new ApiFailure(FailureKind.Network, "网络传输中断，将保留上次数据。"); }
        catch (JsonException)
        { throw new ApiFailure(FailureKind.Protocol, "响应格式已变化，请更新程序或稍后重试。"); }
    }

    public static ApiFailure FromRetcode(int code) => code switch
    {
        -100 or 10001 or -101 => new(FailureKind.Login, "登录已失效或凭据不完整，请重新连接米游社。", code),
        1034 or 10035 => new(FailureKind.Verification, $"电量查询需要验证（{code}）。点击“恢复电量同步”检查；仅在官方页面要求时完成登录或验证码。", code),
        10041 => new(FailureKind.Permission, "米游社暂时限制了查询（10041）。请稍后重试；重新登录不一定能解除限制。", code),
        5003 => new(FailureKind.Permission, "客户端环境校验未通过（5003）。请打开官方绝区零战绩页检查账号状态；此错误不代表登录页一定会出现验证码。", code),
        10102 => new(FailureKind.Permission, "请在米游社的绝区零战绩中开启实时便笺权限，再点击刷新。", code),
        10110 => new(FailureKind.Permission, "没有查询到绝区零角色，请检查米游社绑定。", code),
        10101 or -110 => new(FailureKind.RateLimited, "请求过于频繁，稍后自动重试。", code, TimeSpan.FromMinutes(15)),
        _ => new(FailureKind.Protocol, $"米游社返回错误（代码 {code}），已保留上次数据，稍后重试。", code)
    };

    public async Task<IReadOnlyList<GameRole>> GetRolesAsync(Credential credential, CancellationToken cancellation = default)
    {
        var data = await GetAsync(RolesEndpoint, "game_biz=nap_cn", credential, cancellation);
        var roles = new List<GameRole>();
        if (data.TryGetProperty("list", out var list) && list.ValueKind == JsonValueKind.Array)
            foreach (var item in list.EnumerateArray())
            {
                var biz = JsonValue.String(item, "game_biz");
                if (biz.Length > 0 && biz != "nap_cn") continue;
                var uid = JsonValue.String(item, "game_uid");
                var server = JsonValue.String(item, "region");
                if (uid.Length > 0 && uid.All(char.IsAsciiDigit) && server.StartsWith("prod_", StringComparison.Ordinal))
                    roles.Add(new(uid, server, JsonValue.String(item, "nickname"), JsonValue.String(item, "region_name")));
            }
        if (roles.Count == 0) throw new ApiFailure(FailureKind.Permission, "该账号未绑定绝区零国服角色。请先在米游社确认角色绑定。");
        _roles[credential.AccountKey] = (roles, DateTimeOffset.UtcNow);
        return roles;
    }

    public async Task<EnergySnapshot> GetNoteAsync(Credential credential, GameRole role, CancellationToken cancellation = default)
    {
        string? scopeMessage = null;
        if (includeWidget && Cookies.Parse(credential.Cookie).ContainsKey("stoken"))
        {
            // App credentials have their own widget service. A successful widget read
            // must not depend on probing the independently protected web-note service.
            try
            {
                var data = await GetScopedWidgetAsync(credential, role, cancellation);
                var at = DateTimeOffset.UtcNow;
                return ParseNote(data, credential.AccountKey, role.Key, at) with
                {
                    Notes = NoteParser.Parse(data, at, "官方小组件"), Source = "官方小组件"
                };
            }
            catch (ApiFailure error) when (error.Code == -90001)
            {
                // Only the role-bound note endpoint can safely serve multi-role accounts.
                scopeMessage = error.Message;
            }
            catch (ApiFailure error)
            {
                if (error.Kind == FailureKind.Verification) ClearVerificationProof(credential, role);
                throw; // Do not fan out to more endpoints after a restriction or failure.
            }
        }
        var query = Query(new Dictionary<string, string> { ["role_id"] = role.Uid, ["server"] = role.Server });
        var headers = new Dictionary<string, string>
        {
            ["x-rpc-page"] = "v3.0.15_/zzz"
        };
        var proofHeaders = VerificationHeaders(credential, role);
        if (proofHeaders is not null)
        {
            foreach (var pair in proofHeaders) headers[pair.Key] = pair.Value;
        }
        try
        {
            var data = await GetAsync(NoteEndpoint, query, credential, cancellation, extraHeaders: headers);
            var snapshot = ParseNote(data, credential.AccountKey, role.Key, DateTimeOffset.UtcNow);
            if (scopeMessage is not null) return snapshot with { SupplementMessage = scopeMessage };
            return includeWidget ? await EnrichSnapshotAsync(snapshot, credential, role, cancellation) : snapshot;
        }
        catch (ApiFailure error)
        {
            if (error.Kind == FailureKind.Verification) ClearVerificationProof(credential, role);
            throw;
        }
    }

    private async Task<JsonElement> GetScopedWidgetAsync(Credential credential, GameRole role, CancellationToken cancellation)
    {
        if (_widgetBackoff.TryGetValue(credential.AccountKey, out var retryAt) && DateTimeOffset.UtcNow < retryAt)
            throw new ApiFailure(FailureKind.RateLimited, "小组件接口正在冷却。", retryAfter: retryAt - DateTimeOffset.UtcNow);
        // WV_V2 historically selects the default role and may ignore role_id/server.
        var roles = _roles.TryGetValue(credential.AccountKey, out var cached) && DateTimeOffset.UtcNow - cached.At < TimeSpan.FromMinutes(30)
            ? cached.Roles : await GetRolesAsync(credential, cancellation);
        if (roles.Count != 1 || roles[0].Key != role.Key)
            throw new ApiFailure(FailureKind.Permission, "账号绑定了多个角色，活动补充数据无法确认归属；当前仅显示所选角色的实时便笺。", -90001);
        var query = Query(new Dictionary<string, string> { ["role_id"] = role.Uid, ["server"] = role.Server, ["version"] = "WV_V2" });
        try { return await GetAsync(WidgetEndpoint, query, credential, cancellation, extraHeaders: VerificationHeaders(credential, role)); }
        catch (ApiFailure error) when (error.Kind == FailureKind.RateLimited)
        {
            _widgetBackoff[credential.AccountKey] = DateTimeOffset.UtcNow + (error.RetryAfter ?? TimeSpan.FromMinutes(15));
            throw;
        }
    }

    public async Task<EnergySnapshot> EnrichSnapshotAsync(EnergySnapshot snapshot, Credential credential, GameRole role,
        CancellationToken cancellation = default)
    {
        if (snapshot.AccountKey != credential.AccountKey || snapshot.RoleKey != role.Key)
            throw new ArgumentException("账号或角色不匹配。");
        try
        {
            var cookies = Cookies.Parse(credential.Cookie);
            if (!cookies.ContainsKey("stoken") && !cookies.ContainsKey("stoken_v2"))
                return snapshot with { SupplementMessage = "当前网页登录仅支持实时便笺，咖啡及活动信息需要额外登录凭据。请在账号窗口选择“扫码连接”，用本人米游社 App 扫码后重新同步。" };
            var data = await GetScopedWidgetAsync(credential, role, cancellation);
            var merged = snapshot.Notes.ToList();
            foreach (var note in NoteParser.Parse(data, snapshot.ReceivedAt, "官方小组件"))
            {
                var index = merged.FindIndex(n => n.Id == note.Id);
                if (index < 0) merged.Add(note);
                else if (merged[index].State == NoteState.Unknown && note.State != NoteState.Unknown) merged[index] = note;
            }
            return snapshot with { Notes = merged, SupplementMessage = "" };
        }
        catch (ApiFailure error)
        {
            return snapshot with { SupplementMessage = error.Code == -90001 ? error.Message
                : error.Kind == FailureKind.RateLimited ? "官方小组件接口正在冷却，稍后自动重试；实时便笺正常显示。"
                : error.Kind == FailureKind.Verification
                ? "实时便笺已同步；官方小组件补充信息需要验证，活动信息暂不可用。"
                : error.Kind == FailureKind.Login ? "实时便笺已同步；小组件登录已失效，请在账号窗口重新扫码连接。"
                : "实时便笺已同步；官方小组件补充信息暂不可用，请稍后刷新。" };
        }
    }

    public static bool IsNoteEndpoint(Uri uri)
    {
        var endpoint = new Uri(NoteEndpoint);
        return uri.IsAbsoluteUri && uri.Scheme == "https" && uri.IsDefaultPort && uri.UserInfo.Length == 0 &&
            uri.Host == endpoint.Host && uri.AbsolutePath == endpoint.AbsolutePath;
    }

    public static bool IsNoteRequestForRole(Uri uri, GameRole role)
    {
        if (!IsNoteEndpoint(uri)) return false;
        var fields = uri.Query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries)
            .Select(p => p.Split('=', 2)).Where(p => p.Length == 2)
            .Select(p => (Key: Uri.UnescapeDataString(p[0]), Value: Uri.UnescapeDataString(p[1]))).ToArray();
        return fields.Count(p => p.Key == "role_id") == 1 && fields.Single(p => p.Key == "role_id").Value == role.Uid &&
            fields.Count(p => p.Key == "server") == 1 && fields.Single(p => p.Key == "server").Value == role.Server;
    }

    public static EnergySnapshot ParseNote(JsonElement data, string accountKey, string roleKey, DateTimeOffset receivedAt)
    {
        var energy = JsonValue.Object(data, "energy");
        var progress = JsonValue.Object(energy, "progress");
        var current = JsonValue.Int(progress, "current");
        var maximum = JsonValue.Int(progress, "max");
        if (current is null or < 0 or > 1000000 || maximum is null or <= 0 or > 1000000)
            throw new ApiFailure(FailureKind.Protocol, "电量数据不完整，保留上次记录。");
        var restore = JsonValue.Int(energy, "restore");
        if (restore < 0) restore = null;
        return new(accountKey, roleKey, current.Value, maximum.Value, restore, receivedAt, NoteParser.Parse(data, receivedAt), SchemaVersion: 1);
    }
}

public static class JsonValue
{
    private static readonly JsonElement Empty = JsonSerializer.SerializeToElement(new { });
    public static JsonElement Object(JsonElement parent, string key) =>
        parent.ValueKind == JsonValueKind.Object && parent.TryGetProperty(key, out var value) && value.ValueKind == JsonValueKind.Object ? value : Empty;
    public static int? Int(JsonElement parent, string key)
    {
        if (parent.ValueKind != JsonValueKind.Object || !parent.TryGetProperty(key, out var value)) return null;
        if (value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var number)) return number;
        if (value.ValueKind == JsonValueKind.String && int.TryParse(value.GetString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out number)) return number;
        return null;
    }
    public static string String(JsonElement parent, string key)
    {
        if (parent.ValueKind != JsonValueKind.Object || !parent.TryGetProperty(key, out var value)) return "";
        return value.ValueKind == JsonValueKind.String ? value.GetString() ?? "" : value.ValueKind == JsonValueKind.Number ? value.GetRawText() : "";
    }
}
