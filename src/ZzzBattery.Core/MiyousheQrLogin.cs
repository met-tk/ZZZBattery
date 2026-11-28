using System.Text.Json;

namespace ZzzBattery.Core;

// These objects intentionally do not generate ToString output containing login tickets.
public sealed class QrLoginTicket(string ticket, Uri url, DateTimeOffset expiresAt)
{
    public string Ticket { get; } = ticket;
    public Uri Url { get; } = url;
    public DateTimeOffset ExpiresAt { get; } = expiresAt;
    public override string ToString() => "[临时扫码登录]";
}
public enum QrLoginState { Waiting, Scanned, Confirmed, Expired }
public sealed class QrLoginResult(QrLoginState state, Credential? credential = null)
{
    public QrLoginState State { get; } = state;
    public Credential? Credential { get; } = credential;
}

public sealed partial class MiyousheClient
{
    public const string QrCreateEndpoint = "https://passport-api.mihoyo.com/account/ma-cn-passport/app/createQRLogin";
    public const string QrQueryEndpoint = "https://passport-api.mihoyo.com/account/ma-cn-passport/app/queryQRLoginStatus";
    public const string TokenExchangeEndpoint = "https://passport-api.mihoyo.com/account/ma-cn-session/app/exchange";
    public const string CookieExchangeEndpoint = TokenExchangeEndpoint;
    public const string LTokenExchangeEndpoint = TokenExchangeEndpoint;
    private const int AccountMismatchCode = -90002;

    private static Dictionary<string, string> PassportHeaders(string clientType = "3")
    {
        var headers = new Dictionary<string, string>
        {
            ["User-Agent"] = $"Mozilla/5.0 miHoYoBBS/{WidgetAppVersion}",
            ["x-rpc-client_type"] = clientType,
            ["x-rpc-app_version"] = WidgetAppVersion,
            ["x-rpc-sdk_version"] = PassportSdkVersion,
            ["x-rpc-account_version"] = PassportSdkVersion,
            ["x-rpc-app_id"] = "bll8iq97cem8",
            ["x-rpc-game_biz"] = "bbs_cn",
            ["x-rpc-lifecycle_id"] = ProcessLifecycleId
        };
        if (clientType == "2")
        {
            headers["x-rpc-device_name"] = "Pixel+8";
            headers["x-rpc-device_model"] = "Pixel 8";
            headers["x-rpc-sys_version"] = "14";
        }
        else
        {
            headers["x-rpc-device_name"] = "ZZZBattery+Windows";
            headers["x-rpc-device_model"] = "x86_64";
            headers["x-rpc-device_os"] = "Windows 11 Version 2009";
        }
        return headers;
    }

    public async Task<QrLoginTicket> CreateQrLoginAsync(CancellationToken cancellation = default)
    {
        var data = await GetAsync(QrCreateEndpoint, "", new Credential(), cancellation,
            requestBody: "{}", extraHeaders: PassportHeaders(), passportProfile: true, allowStokenRecovery: false);
        var ticket = JsonValue.String(data, "ticket");
        var url = JsonValue.String(data, "url");
        if (!IsSafeVerificationValue(ticket, 512) || url.Length > 4096 ||
            !Uri.TryCreate(url, UriKind.Absolute, out var uri) || uri.Scheme != "https" || !uri.IsDefaultPort ||
            uri.UserInfo.Length != 0 || uri.Host is not ("user.mihoyo.com" or "user.miyoushe.com"))
            throw new ApiFailure(FailureKind.Protocol, "未收到有效的官方登录二维码，请重试。");
        return new(ticket, uri, DateTimeOffset.UtcNow.AddMinutes(3));
    }

    public async Task<QrLoginResult> PollQrLoginAsync(QrLoginTicket ticket, CancellationToken cancellation = default)
    {
        if (DateTimeOffset.UtcNow >= ticket.ExpiresAt) return new(QrLoginState.Expired);
        if (!IsSafeVerificationValue(ticket.Ticket, 512)) throw new ArgumentException("无效的扫码会话。");
        JsonElement data;
        try
        {
            data = await GetAsync(QrQueryEndpoint, "", new Credential(), cancellation,
                requestBody: JsonSerializer.Serialize(new { ticket = ticket.Ticket }), extraHeaders: PassportHeaders(),
                passportProfile: true, allowStokenRecovery: false);
        }
        catch (ApiFailure error) when (error.Code == -106) { return new(QrLoginState.Expired); }
        var state = JsonValue.String(data, "status");
        if (state is "Init" or "Created") return new(QrLoginState.Waiting);
        if (state == "Scanned") return new(QrLoginState.Scanned);
        if (state is "Expired" or "Canceled" or "Cancelled") return new(QrLoginState.Expired);
        if (state != "Confirmed") throw new ApiFailure(FailureKind.Protocol, "扫码状态无法识别，请重新生成二维码。");
        if (NoteParser.Bool(data, "need_realperson") == true)
            throw new ApiFailure(FailureKind.Permission, "请先在米游社 App 内完成账号验证，再重新扫码。");
        var user = JsonValue.Object(data, "user_info");
        var id = JsonValue.String(user, "aid");
        var mid = JsonValue.String(user, "mid");
        if (id.Length is 0 or > 30 || !id.All(char.IsAsciiDigit) || !IsSafeVerificationValue(mid, 256))
            throw new ApiFailure(FailureKind.Protocol, "扫码登录缺少账号信息，请重试。");
        var fields = new Dictionary<string, string> { ["stuid"] = id, ["mid"] = mid };
        if (data.TryGetProperty("tokens", out var tokens) && tokens.ValueKind == JsonValueKind.Array)
            foreach (var item in tokens.EnumerateArray().Take(10))
            {
                // App passport tokens use the app cookie names. The `_v2` cookie
                // namespace belongs to web-login responses, not this token array.
                var name = JsonValue.Int(item, "token_type") switch { 1 => "stoken", 2 => "ltoken", 4 => "cookie_token", _ => "" };
                var value = JsonValue.String(item, "token");
                if (name.Length == 0) continue;
                if (value.Length == 0 || fields.ContainsKey(name))
                    throw new ApiFailure(FailureKind.Protocol, "扫码返回的登录凭据不完整，请重新扫码。");
                fields[name] = value;
            }
        if (!fields.ContainsKey("stoken"))
            throw new ApiFailure(FailureKind.Login, "扫码未取得小组件登录凭据，请更新米游社 App 后重试。");
        fields["account_id"] = id;
        if (fields.ContainsKey("ltoken")) fields["ltuid"] = id;
        Credential credential;
        try { credential = Cookies.Create(JsonSerializer.Serialize(fields)); }
        catch (ArgumentException) { throw new ApiFailure(FailureKind.Protocol, "扫码返回的登录凭据格式不正确，请重新扫码。"); }
        return new(QrLoginState.Confirmed, credential);
    }

    private async Task<(string Aid, string Token)> ExchangeTokenByStokenAsync(string stoken, string mid, int dstTokenType, CancellationToken cancellation)
    {
        var body = JsonSerializer.Serialize(new
        {
            dst_token_type = dstTokenType,
            mid,
            src_token = new { token_type = 1, token = stoken }
        });
        async Task<(string Aid, string Token)> ExchangeAsync(string clientType)
        {
            var headers = PassportHeaders(clientType);
            headers["DS"] = SignPassport(body, DateTimeOffset.UtcNow.ToUnixTimeSeconds(), RandomPassportNonce());
            var data = await GetAsync(TokenExchangeEndpoint, "", new Credential(), cancellation,
                requestBody: body, extraHeaders: headers, passportProfile: true, allowStokenRecovery: false, sendCookie: false);
            var tokenObj = JsonValue.Object(data, "token");
            var userObj = JsonValue.Object(data, "user_info");
            return (JsonValue.String(userObj, "aid"), JsonValue.String(tokenObj, "token"));
        }
        try
        {
            return await ExchangeAsync("2");
        }
        catch (ApiFailure error) when (error.Kind == FailureKind.Protocol && !cancellation.IsCancellationRequested)
        {
            return await ExchangeAsync("3");
        }
    }

    // The app QR may return only stoken. Exchange it via passport ma-cn-session/app/exchange
    // so role-bound daily notes and the widget stay on one consistent account.
    public async Task<bool> RefreshCookieTokenByStokenAsync(Credential credential, CancellationToken cancellation = default, bool force = false)
    {
        var initialFields = Cookies.Parse(credential.Cookie);
        if (!force && (initialFields.ContainsKey("cookie_token_v2") || initialFields.ContainsKey("cookie_token"))) return false;
        var stoken = initialFields.GetValueOrDefault("stoken") ?? initialFields.GetValueOrDefault("stoken_v2");
        var mid = initialFields.GetValueOrDefault("mid") ?? initialFields.GetValueOrDefault("account_mid_v2") ?? initialFields.GetValueOrDefault("ltmid_v2");
        if (string.IsNullOrEmpty(stoken) || string.IsNullOrEmpty(mid)) return false;
        var initialCookieTokenV2 = initialFields.GetValueOrDefault("cookie_token_v2");
        var expectedId = initialFields.GetValueOrDefault("stuid") ?? initialFields.GetValueOrDefault("stuid_v2") ??
            initialFields.GetValueOrDefault("account_id") ?? initialFields.GetValueOrDefault("account_id_v2");
        var (id, cookie) = await ExchangeTokenByStokenAsync(stoken, mid, 4, cancellation);
        if (id != expectedId || string.IsNullOrEmpty(cookie))
            throw new ApiFailure(FailureKind.Protocol, "登录账号校验不一致，请重新扫码。", AccountMismatchCode);
        string? ltoken = null;
        var currentFields = Cookies.Parse(credential.Cookie);
        if (!currentFields.ContainsKey("ltoken") && !currentFields.ContainsKey("ltoken_v2"))
        {
            try
            {
                var (ltokenAid, parsed) = await ExchangeTokenByStokenAsync(stoken, mid, 2, cancellation);
                if (ltokenAid == expectedId && !string.IsNullOrEmpty(parsed)) ltoken = parsed;
            }
            catch (OperationCanceledException) when (cancellation.IsCancellationRequested) { throw; }
            catch { /* Best-effort ltoken enrichment; never blocks cookie_token refresh. */ }
        }
        cancellation.ThrowIfCancellationRequested();
        // Re-parse after awaits so concurrent browser rotation on unrelated tokens is never overwritten.
        var latestFields = Cookies.Parse(credential.Cookie);
        var before = credential.Cookie;
        latestFields["cookie_token"] = cookie;
        latestFields["account_id"] = id;
        if (force && !string.IsNullOrEmpty(initialCookieTokenV2) &&
            string.Equals(latestFields.GetValueOrDefault("cookie_token_v2"), initialCookieTokenV2, StringComparison.Ordinal))
            latestFields.Remove("cookie_token_v2");
        if (!string.IsNullOrEmpty(ltoken) && !latestFields.ContainsKey("ltoken") && !latestFields.ContainsKey("ltoken_v2"))
        {
            latestFields["ltoken"] = ltoken;
            latestFields["ltuid"] = id;
        }
        try
        {
            credential.Cookie = Cookies.Create(JsonSerializer.Serialize(latestFields)).Cookie;
            return !string.Equals(before, credential.Cookie, StringComparison.Ordinal);
        }
        catch (ArgumentException)
        {
            throw new ApiFailure(FailureKind.Protocol, "登录凭据格式不正确，请重新扫码。");
        }
    }

    public async Task<Credential> CompleteQrCredentialAsync(Credential credential, CancellationToken cancellation = default)
    {
        var fields = Cookies.Parse(credential.Cookie);
        if (fields.ContainsKey("cookie_token_v2") || fields.ContainsKey("cookie_token")) return credential;
        var hasStoken = fields.ContainsKey("stoken") || fields.ContainsKey("stoken_v2");
        var hasMid = !string.IsNullOrEmpty(fields.GetValueOrDefault("mid")) ||
            !string.IsNullOrEmpty(fields.GetValueOrDefault("account_mid_v2")) ||
            !string.IsNullOrEmpty(fields.GetValueOrDefault("ltmid_v2"));
        if (!hasStoken || !hasMid) throw new ArgumentException("缺少小组件凭据。");
        try
        {
            await RefreshCookieTokenByStokenAsync(credential, cancellation, force: false);
        }
        catch (ApiFailure error) when (error.Code != AccountMismatchCode)
        {
            // Keep stoken + mid when passport exchange refuses conversion; getUserGameRolesByStoken
            // and the Android widget endpoint work directly with stoken + mid.
        }
        return credential;
    }
}
