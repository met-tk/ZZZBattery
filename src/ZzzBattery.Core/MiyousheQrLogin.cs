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
    public const string CookieExchangeEndpoint = "https://passport-api.mihoyo.com/account/auth/api/getCookieAccountInfoBySToken";
    private const string PassportVersion = "2.115.0";
    private static Dictionary<string, string> PassportHeaders(string clientType = "3") => new()
    {
        ["User-Agent"] = $"Mozilla/5.0 miHoYoBBS/{PassportVersion} Capture/2.2.0",
        ["x-rpc-client_type"] = clientType, ["x-rpc-app_version"] = PassportVersion,
        ["x-rpc-sdk_version"] = PassportVersion, ["x-rpc-account_version"] = PassportVersion,
        ["x-rpc-app_id"] = "bll8iq97cem8", ["x-rpc-game_biz"] = "bbs_cn",
        ["x-rpc-device_name"] = "ZZZBattery Windows", ["x-rpc-device_model"] = "Windows PC"
    };

    public async Task<QrLoginTicket> CreateQrLoginAsync(CancellationToken cancellation = default)
    {
        var data = await GetAsync(QrCreateEndpoint, "", new Credential(), cancellation,
            requestBody: "{}", extraHeaders: PassportHeaders());
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
                requestBody: JsonSerializer.Serialize(new { ticket = ticket.Ticket }), extraHeaders: PassportHeaders());
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

    // The app QR may return only stoken. Exchange it on the official passport service
    // so role-bound daily notes and the widget can use one consistent account.
    public async Task<Credential> CompleteQrCredentialAsync(Credential credential, CancellationToken cancellation = default)
    {
        var fields = Cookies.Parse(credential.Cookie);
        if (fields.ContainsKey("cookie_token_v2") || fields.ContainsKey("cookie_token")) return credential;
        if (!fields.TryGetValue("stoken", out var stoken)) throw new ArgumentException("缺少小组件凭据。");
        var data = await GetAsync(CookieExchangeEndpoint, Query(new Dictionary<string, string> { ["stoken"] = stoken }),
            credential, cancellation, extraHeaders: PassportHeaders("2"));
        var id = JsonValue.String(data, "uid");
        var cookie = JsonValue.String(data, "cookie_token");
        if (id != fields.GetValueOrDefault("stuid") || string.IsNullOrEmpty(cookie))
            throw new ApiFailure(FailureKind.Protocol, "登录账号校验不一致，请重新扫码。");
        fields["cookie_token"] = cookie; fields["account_id"] = id;
        try { return Cookies.Create(JsonSerializer.Serialize(fields)); }
        catch (ArgumentException) { throw new ApiFailure(FailureKind.Protocol, "登录凭据格式不正确，请重新扫码。"); }
    }
}
