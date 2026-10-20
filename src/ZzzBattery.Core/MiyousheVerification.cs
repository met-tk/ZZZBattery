using System.Collections.Concurrent;
using System.Text.Json;

namespace ZzzBattery.Core;

public sealed partial class MiyousheClient
{
    // These are the create/verify endpoints used by the official ZZZ record page.
    public const string CreateVerificationEndpoint = "https://api-takumi.mihoyo.com/event/toolcomsrv/risk/createGeetest";
    public const string VerifyVerificationEndpoint = "https://api-takumi.mihoyo.com/event/toolcomsrv/risk/verifyGeetest";
    public const string OfficialRecordPage = "https://act.mihoyo.com/app/mihoyo-zzz-game-record/m.html";
    public static string VerificationPageUrl(GameRole? role = null)
    {
        // The official route guard supports this browser entry. Do not advertise
        // miHoYoBBS in a WebView without the real app's JavaScript bridge.
        // The SPA reads the selected role from its hash-route query, including
        // when restoring the last role from browser storage.
        var roleQuery = role is null ? "" : "?" + Query(new Dictionary<string, string>
            { ["role_id"] = role.Uid, ["server"] = role.Server });
        return OfficialRecordPage + "?bbs=1#/zzz/daily-note" + roleQuery;
    }

    public static OfficialNoteCapture ReadOfficialNoteResponse(Uri requestUri, int httpStatus, string response,
        Credential credential, DateTimeOffset receivedAt)
    {
        if (!IsNoteEndpoint(requestUri)) return new(OfficialNoteCaptureState.Ignored);
        if (credential.Role is null || !IsNoteRequestForRole(requestUri, credential.Role)) return new(OfficialNoteCaptureState.RoleMismatch);
        if (httpStatus != 200) return new(OfficialNoteCaptureState.HttpError, Code: httpStatus);
        try
        {
            using var document = JsonDocument.Parse(response);
            var root = document.RootElement;
            var code = JsonValue.Int(root, "retcode");
            if (code is null) return new(OfficialNoteCaptureState.InvalidData);
            if (code != 0) return new(OfficialNoteCaptureState.Rejected, Code: code);
            var data = JsonValue.Object(root, "data");
            var snapshot = ParseNote(data, credential.AccountKey, credential.Role.Key, receivedAt) with
            {
                Source = "官方网页便笺",
                Notes = NoteParser.Parse(data, receivedAt, "官方网页便笺")
            };
            return new(OfficialNoteCaptureState.Ready, snapshot);
        }
        catch (Exception error) when (error is JsonException or ApiFailure)
        {
            return new(OfficialNoteCaptureState.InvalidData);
        }
    }
    private const string VerificationAppKey = "game_record_zzz";
    private readonly ConcurrentDictionary<string, (string Value, DateTimeOffset Expires)> _verification = new();
    private readonly ConcurrentDictionary<string, long> _challengeAttempts = new();

    private static string VerificationKey(Credential credential, GameRole role) => credential.AccountKey + ":" + role.Key;
    private IReadOnlyDictionary<string, string>? VerificationHeaders(Credential credential, GameRole role)
    {
        var key = VerificationKey(credential, role);
        if (_verification.TryGetValue(key, out var proof))
        {
            if (proof.Expires > DateTimeOffset.UtcNow) return new Dictionary<string, string> { ["x-rpc-challenge"] = proof.Value };
            _verification.TryRemove(key, out _);
        }
        return null;
    }
    public void ForgetVerification() => _verification.Clear();
    public void SetVerificationProof(Credential credential, GameRole role, string challenge, TimeSpan? lifetime = null)
    {
        if (IsSafeVerificationValue(challenge))
            _verification[VerificationKey(credential, role)] = (challenge, DateTimeOffset.UtcNow + (lifetime ?? TimeSpan.FromMinutes(30)));
    }
    public void ClearVerificationProof(Credential credential, GameRole role) =>
        _verification.TryRemove(VerificationKey(credential, role), out _);
    public static bool IsSafeVerificationValue(string? value, int limit = 2048) =>
        !string.IsNullOrWhiteSpace(value) && value.Length <= limit && value.All(c => c >= 33 && c <= 126);
    private static Dictionary<string, string> RiskHeaders(string? trace, string? path = null)
    {
        var headers = new Dictionary<string, string>
        {
            ["x-rpc-challenge_game"] = "8",
            ["x-rpc-challenge_path"] = path == "/api/zzz/widget" ? path : "/api/zzz/note"
        };
        if (IsSafeVerificationValue(trace, 256)) headers["x-rpc-challenge_trace"] = trace!;
        return headers;
    }
    public async Task<VerificationTask?> CreateVerificationAsync(Credential credential, ApiFailure? failure = null,
        CancellationToken cancellation = default)
    {
        if (credential.Role is null) throw new ArgumentException("请先选择绝区零角色。");
        var key = VerificationKey(credential, credential.Role);
        var now = Environment.TickCount64;
        if (_challengeAttempts.TryGetValue(key, out var last) && now - last < 60_000)
            throw new ApiFailure(FailureKind.RateLimited, "获取验证任务的最短间隔为 60 秒，请稍后再试。",
                retryAfter: TimeSpan.FromMilliseconds(60_000 - (now - last)));
        _challengeAttempts[key] = now;
        var query = Query(new Dictionary<string, string> { ["app_key"] = VerificationAppKey, ["is_high"] = "true" });
        JsonElement data;
        try
        {
            data = await GetAsync(CreateVerificationEndpoint, query, credential, cancellation,
                extraHeaders: RiskHeaders(failure?.VerificationTrace, failure?.VerificationPath));
        }
        catch (ApiFailure error) when (error.Code == 30001)
        {
            // This code means no captcha task is needed in this endpoint's context.
            // It is neither an expired login nor proof that a note read has succeeded.
            return null;
        }
        var gt = JsonValue.String(data, "gt");
        var challenge = JsonValue.String(data, "challenge");
        if (!IsSafeVerificationValue(gt, 256) || !IsSafeVerificationValue(challenge))
            throw new ApiFailure(FailureKind.Protocol, "官方未返回可用的验证任务。请稍后重试，或打开官方绝区零战绩页。");
        return new(gt, challenge, !data.TryGetProperty("new_captcha", out var fresh) || fresh.ValueKind != JsonValueKind.False,
            JsonValue.Int(data, "success") != 0, credential.AccountKey, credential.Role.Key, DateTimeOffset.UtcNow, failure?.VerificationTrace,
            failure?.VerificationPath ?? "/api/zzz/note");
    }
    public async Task CompleteVerificationAsync(Credential credential, VerificationTask task, VerificationAnswer answer,
        CancellationToken cancellation = default)
    {
        if (credential.Role is null || credential.AccountKey != task.AccountKey || credential.Role.Key != task.RoleKey)
            throw new ArgumentException("账号或角色已经变化，请重新打开验证窗口。");
        if (DateTimeOffset.UtcNow - task.CreatedAt > TimeSpan.FromMinutes(10))
            throw new ArgumentException("验证任务已过期，请重新获取。");
        if (!IsSafeVerificationValue(answer.Challenge) || !IsSafeVerificationValue(answer.Validate) || !IsSafeVerificationValue(answer.Seccode))
            throw new ArgumentException("验证结果不完整，请重新完成验证。");
        var body = JsonSerializer.Serialize(new Dictionary<string, string>
        {
            ["app_key"] = VerificationAppKey,
            ["geetest_challenge"] = answer.Challenge,
            ["geetest_validate"] = answer.Validate,
            ["geetest_seccode"] = answer.Seccode
        });
        var envelope = await GetAsync(VerifyVerificationEndpoint, "", credential, cancellation, body, RiskHeaders(task.Trace, task.Path), verificationEnvelope: true);
        var proof = JsonValue.String(JsonValue.Object(envelope, "data"), "challenge");
        if (proof.Length == 0) proof = JsonValue.String(envelope, "challenge");
        if (!IsSafeVerificationValue(proof)) throw new ApiFailure(FailureKind.Protocol, "服务端尚未确认验证通过，请重新验证。");
        // The result is short-lived and stays in memory; never include it in settings or logs.
        _verification[VerificationKey(credential, credential.Role)] = (proof, DateTimeOffset.UtcNow.AddMinutes(30));
    }
}
