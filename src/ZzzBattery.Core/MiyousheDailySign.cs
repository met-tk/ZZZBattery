using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace ZzzBattery.Core;

public sealed partial class MiyousheClient
{
    public const string DailySignActivity = "e202406242138391";
    public const string DailySignPage = "https://act.mihoyo.com/bbs/event/signin/zzz/index.html?act_id=" + DailySignActivity;
    public const string DailySignInfoEndpoint = "https://api-takumi.mihoyo.com/event/luna/zzz/info";
    public const string DailySignSubmitEndpoint = "https://api-takumi.mihoyo.com/event/luna/zzz/sign";
    // CN community sign-in uses the DS-v1 profile, not the record API's DS-v2.
    // Keep version and salt together; see artifacts/signin-research/contract.md.
    public const string DailySignAppVersion = "2.70.1";
    private const string DailySignSalt = "LyD1rXqMv2GJhnwdvCBjFOKGiKuLY3aO";

    public static string SignDailyRequest(long timestamp, string nonce)
    {
        if (nonce.Length != 6 || !nonce.All(char.IsAsciiLetter))
            throw new ArgumentException("Invalid signing nonce.", nameof(nonce));
        var digest = MD5.HashData(Encoding.UTF8.GetBytes($"salt={DailySignSalt}&t={timestamp}&r={nonce}"));
        return $"{timestamp},{nonce},{Convert.ToHexString(digest).ToLowerInvariant()}";
    }

    private static Dictionary<string, string> DailySignFields(Credential credential)
    {
        if (credential.Role is not { } role || string.IsNullOrEmpty(role.Uid) ||
            !role.Uid.All(char.IsAsciiDigit) || role.Server is not ("prod_gf_cn" or "prod_qd_cn"))
            throw new ApiFailure(FailureKind.Permission, "请先连接米游社并选择绝区零国服角色。");
        if (string.IsNullOrWhiteSpace(credential.Cookie))
            throw new ApiFailure(FailureKind.Login, "签到登录凭据缺失，请重新连接米游社。");
        return new()
        {
            ["act_id"] = DailySignActivity, ["region"] = role.Server,
            ["uid"] = role.Uid, ["lang"] = "zh-cn"
        };
    }

    private Dictionary<string, string> DailySignHeaders()
    {
        const string letters = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ";
        var nonce = new string(Enumerable.Range(0, 6).Select(_ => letters[RandomNumberGenerator.GetInt32(letters.Length)]).ToArray());
        return new()
        {
            ["DS"] = SignDailyRequest(DateTimeOffset.UtcNow.ToUnixTimeSeconds(), nonce),
            ["x-rpc-signgame"] = "zzz",
            ["x-rpc-app_version"] = DailySignAppVersion,
            ["Referer"] = DailySignPage
        };
    }

    public async Task<DailySignInfo> GetDailySignInfoAsync(Credential credential, CancellationToken cancellation = default)
    {
        var fields = DailySignFields(credential);
        var data = await GetAsync(DailySignInfoEndpoint, Query(fields), credential, cancellation,
            extraHeaders: DailySignHeaders());
        EnsureNoSignChallenge(data);
        if (!data.TryGetProperty("is_sign", out var signed) ||
            signed.ValueKind is not (JsonValueKind.True or JsonValueKind.False) ||
            !DateOnly.TryParseExact(JsonValue.String(data, "today"), ["yyyy-MM-dd", "yyyy-M-d"],
                CultureInfo.InvariantCulture, DateTimeStyles.None, out var day))
            throw new ApiFailure(FailureKind.Protocol, "签到接口没有返回可确认的日期和签到状态。");
        return new(signed.GetBoolean(), day, DateTimeOffset.UtcNow);
    }

    public async Task<DailySignSubmitResult> SubmitDailySignAsync(Credential credential, CancellationToken cancellation = default)
    {
        var fields = DailySignFields(credential);
        try
        {
            var data = await GetAsync(DailySignSubmitEndpoint, "", credential, cancellation,
                JsonSerializer.Serialize(fields), DailySignHeaders());
            EnsureNoSignChallenge(data);
            // This acknowledges the request only. The session must confirm through
            // info before promoting the button to "已签到".
            return DailySignSubmitResult.Submitted;
        }
        catch (ApiFailure error) when (error.Code == -5003)
        {
            // Known duplicate-sign response; never sufficient proof on its own.
            return DailySignSubmitResult.AlreadySigned;
        }
    }

    private static void EnsureNoSignChallenge(JsonElement data)
    {
        if (data.TryGetProperty("is_risk", out var risk))
        {
            if (risk.ValueKind == JsonValueKind.True)
                throw DailySignFailure(1034);
            if (risk.ValueKind != JsonValueKind.False)
                throw new ApiFailure(FailureKind.Protocol, "签到验证状态无法确认，请稍后重试。");
        }
        if (JsonValue.String(data, "challenge").Length > 0 || JsonValue.String(data, "gt").Length > 0)
            throw DailySignFailure(1034);
    }

    private static ApiFailure DailySignFailure(int code) => code switch
    {
        -100 or -101 or 10001 => new(FailureKind.Login, "签到登录已失效，请重新连接米游社。", code),
        1034 or 10035 => new(FailureKind.Verification, "签到需要官方验证，请在米游社 App 的绝区零签到页按提示处理。", code),
        10041 or 5003 => new(FailureKind.Permission, "签到请求暂受限制，请在米游社 App 检查签到状态。", code),
        10101 or -110 => new(FailureKind.RateLimited, "签到请求过于频繁，请稍后重试。", code, TimeSpan.FromMinutes(15)),
        _ => new(FailureKind.Protocol, $"暂时无法确认签到结果（{code}）。", code)
    };
}
