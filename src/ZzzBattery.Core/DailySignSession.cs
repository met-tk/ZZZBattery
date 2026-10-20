namespace ZzzBattery.Core;

public sealed record DailySignInfo(bool IsSigned, DateOnly Day, DateTimeOffset CheckedAt);
public enum DailySignSubmitResult { Submitted, AlreadySigned }

public interface IDailySignClient
{
    Task<DailySignInfo> GetDailySignInfoAsync(Credential credential, CancellationToken cancellation = default);
    Task<DailySignSubmitResult> SubmitDailySignAsync(Credential credential, CancellationToken cancellation = default);
}

// No credentials, captcha proofs, or server response bodies are persisted here.
// Deadlines are relative to SavedAt on restore so moving the clock backwards cannot
// discard a cooldown. UncertainSubmission is persisted before sending the POST.
public sealed record DailySignCheckpoint(string AccountKey, string RoleKey, DateTimeOffset SavedAt,
    DateOnly? SignedDay = null, DateTimeOffset? LastCheckedAt = null,
    DateTimeOffset? RetryAt = null, DateTimeOffset? NextCheckAt = null,
    FailureKind? FailureKind = null, int? FailureCode = null,
    bool UncertainSubmission = false, int SchemaVersion = 1);

public sealed class DailySignSession(IDailySignClient client, TimeProvider? clock = null) : IDisposable
{
    private static readonly TimeSpan ChinaOffset = TimeSpan.FromHours(8);
    private static readonly TimeSpan MinimumInterval = TimeSpan.FromSeconds(60);
    private static readonly TimeSpan CheckInterval = TimeSpan.FromMinutes(30);
    private static readonly TimeSpan PreflightFreshness = TimeSpan.FromMinutes(2);
    private readonly TimeProvider _clock = clock ?? TimeProvider.System;
    private readonly object _gate = new();
    private CancellationTokenSource _cancel = new();
    private Task _flight = Task.CompletedTask;
    private int _generation;
    private bool _disposed, _signing;
    private DateOnly? _signedDay;
    private DateTimeOffset? _lastCheckedAt;
    private DailySignInfo? _unsignedInfo;
    private long _unsignedStamp;
    private long _retryStamp, _checkStamp, _clockStamp;
    private TimeSpan _retryDelay, _checkDelay;
    private DateTimeOffset _clockAnchor;
    private DateOnly _displayDay;
    private string _lastPresentation = "";

    public Credential? Credential { get; private set; }
    public bool Busy { get; private set; }
    public bool ClockUncertain { get; private set; }
    public bool UncertainSubmission { get; private set; }
    public ApiFailure? Failure { get; private set; }
    public string Message { get; private set; } = "连接米游社后，可领取绝区零每日签到奖励。";
    public DateTimeOffset? LastCheckedAt => _lastCheckedAt;
    public DateTimeOffset RetryAt => _clock.GetUtcNow() + RetryRemaining;
    public DateTimeOffset NextCheckAt => _clock.GetUtcNow() + CheckRemaining;
    public bool IsSignedToday => Credential?.Role is not null && !ClockUncertain &&
        _signedDay == Today && _lastCheckedAt is { } checkedAt && checkedAt <= _clock.GetUtcNow().AddMinutes(2);
    public bool CanSign => !_disposed && !Busy && Credential?.Role is not null &&
        !IsSignedToday && (RetryRemaining == TimeSpan.Zero || HasFreshUnsignedPreflight);
    public bool NeedsAttention => Credential is not null && !IsSignedToday &&
        (UncertainSubmission || ClockUncertain || Failure is not null);
    public string ButtonText => Busy ? (_signing ? "签到中…" : "查询中…") :
        IsSignedToday ? "已签到" : Credential?.Role is null ? "请登录" :
        Failure?.Kind == FailureKind.Verification ? "需验证" :
        Failure?.Kind == FailureKind.Login ? "请登录" :
        UncertainSubmission || ClockUncertain ? "待确认" :
        RetryRemaining > TimeSpan.Zero && !HasFreshUnsignedPreflight || Failure is not null ? "稍后重试" : "今日签到";

    public event Action? Changed;
    public event Action<DailySignCheckpoint>? CheckpointChanged;

    public static DateOnly ChinaDay(DateTimeOffset time) => DateOnly.FromDateTime(time.ToOffset(ChinaOffset).DateTime);
    private DateOnly Today => ChinaDay(_clock.GetUtcNow());
    private bool HasFreshUnsignedPreflight => !ClockUncertain && !UncertainSubmission && Failure is null &&
        _unsignedInfo is { IsSigned: false } info && info.Day == Today &&
        _clock.GetElapsedTime(_unsignedStamp) <= PreflightFreshness &&
        _clock.GetUtcNow() - info.CheckedAt <= PreflightFreshness;
    private TimeSpan RetryRemaining => Remaining(_retryDelay, _retryStamp);
    private TimeSpan CheckRemaining => Remaining(_checkDelay, _checkStamp);
    private TimeSpan Remaining(TimeSpan delay, long stamp) =>
        Nonnegative(delay - _clock.GetElapsedTime(stamp));
    private static TimeSpan Nonnegative(TimeSpan value) => value > TimeSpan.Zero ? value : TimeSpan.Zero;

    public void SetCredential(Credential? credential, DailySignCheckpoint? checkpoint = null)
    {
        lock (_gate)
        {
            if (_disposed) return;
            _generation++;
            _cancel.Cancel(); _cancel.Dispose(); _cancel = new();
            _flight = Task.CompletedTask; Busy = false; _signing = false;
            // A caller can mutate the account-selection object after this call.
            // Keep an immutable identity for in-flight requests and stored results.
            Credential = credential is null ? null : new Credential
            {
                AccountKey = credential.AccountKey, Cookie = credential.Cookie, Role = credential.Role
            };
            _signedDay = null; _lastCheckedAt = null; _unsignedInfo = null;
            Failure = null; UncertainSubmission = false; ClockUncertain = false;
            SetRetry(TimeSpan.Zero); Schedule(TimeSpan.Zero); ResetClockAnchor();
            _displayDay = Today;
            Message = Credential?.Role is null
                ? "连接米游社并选择角色后，可领取绝区零每日签到奖励。"
                : "点击今日签到，领取当前角色的每日签到奖励。";
            Restore(checkpoint);
            SaveCheckpoint();
            NotifyChanged();
        }
    }

    public DailySignCheckpoint? CaptureCheckpoint() => Credential?.Role is { } role
        ? new(Credential.AccountKey, role.Key, _clock.GetUtcNow(), _signedDay, _lastCheckedAt,
            RetryAt, NextCheckAt, Failure?.Kind, Failure?.Code, UncertainSubmission)
        : null;

    public DailySignCheckpoint? ExportCheckpoint() => CaptureCheckpoint();

    private bool SaveCheckpoint()
    {
        try
        {
            if (CaptureCheckpoint() is { } checkpoint) CheckpointChanged?.Invoke(checkpoint);
            return true;
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException) { return false; }
    }

    private void Restore(DailySignCheckpoint? checkpoint)
    {
        if (checkpoint is null || Credential?.Role is null || checkpoint.SchemaVersion != 1 ||
            checkpoint.SavedAt == default || checkpoint.AccountKey != Credential.AccountKey ||
            checkpoint.RoleKey != Credential.Role.Key ||
            (checkpoint.FailureKind is { } kind && !Enum.IsDefined(kind)) ||
            checkpoint.RetryAt is { } retryAt && retryAt < checkpoint.SavedAt ||
            checkpoint.NextCheckAt is { } nextCheckAt && nextCheckAt < checkpoint.SavedAt ||
            checkpoint.LastCheckedAt is { } checkedAt && checkedAt > checkpoint.SavedAt.AddMinutes(2))
            return;
        var elapsed = _clock.GetUtcNow() - checkpoint.SavedAt;
        if (elapsed < TimeSpan.Zero)
        {
            elapsed = TimeSpan.Zero;
            ClockUncertain = true;
        }
        SetRetry(Nonnegative((checkpoint.RetryAt - checkpoint.SavedAt ?? TimeSpan.Zero) - elapsed));
        Schedule(Nonnegative((checkpoint.NextCheckAt - checkpoint.SavedAt ?? TimeSpan.Zero) - elapsed));
        _lastCheckedAt = checkpoint.LastCheckedAt;
        // A stored date must correspond to a confirmed observation, not just a UI flag.
        _signedDay = checkpoint.SignedDay is { } day && _lastCheckedAt is { } at &&
            day == ChinaDay(at) ? day : null;
        UncertainSubmission = checkpoint.UncertainSubmission;
        if (checkpoint.FailureKind is { } failureKind)
            Failure = new(failureKind, FailureMessage(failureKind), checkpoint.FailureCode, RetryRemaining);
        Message = IsSignedToday ? "今日已签到。" : Failure?.Message ??
            (UncertainSubmission ? "上次签到结果尚未确认，下次只查询结果，不会重复提交。" :
            ClockUncertain ? "系统时间发生变化，签到状态将在下次查询时确认。" :
            _lastCheckedAt is not null ? "已恢复上次签到状态。" : Message);
    }

    /// <summary>Updates day/clock-dependent labels; performs no network requests.</summary>
    public void Tick()
    {
        lock (_gate)
        {
            if (_disposed) return;
            DetectClockChange();
            if (_displayDay != Today)
            {
                _displayDay = Today;
                if (!Busy && Failure is null && !UncertainSubmission)
                    Message = "新的一天，可领取今日签到奖励。";
            }
            NotifyChanged(onlyWhenPresentationChanges: true);
        }
    }

    public void InvalidateTime() => Tick();

    private void DetectClockChange()
    {
        if (Math.Abs((_clock.GetUtcNow() - _clockAnchor - _clock.GetElapsedTime(_clockStamp)).TotalSeconds) <= 120) return;
        if (!ClockUncertain)
        {
            ClockUncertain = true;
            if (!Busy && Failure is null)
                Message = "系统时间发生变化，签到状态将在下次查询时确认。";
        }
    }

    private void ResetClockAnchor()
    {
        _clockAnchor = _clock.GetUtcNow(); _clockStamp = _clock.GetTimestamp();
    }

    public Task CheckAsync(bool manual = false) => StartOperation(sign: false, manual);
    public Task SignAsync() => StartOperation(sign: true, manual: true);

    private Task StartOperation(bool sign, bool manual)
    {
        lock (_gate)
        {
            if (_disposed) return Task.CompletedTask;
            Tick();
            if (Busy) return _flight;
            if (Credential?.Role is null || IsSignedToday) return Task.CompletedTask;
            var cachedPreflight = sign && HasFreshUnsignedPreflight ? _unsignedInfo : null;
            if (RetryRemaining > TimeSpan.Zero && cachedPreflight is null)
            {
                if (manual)
                {
                    var delay = RetryRemaining;
                    Message = delay.TotalSeconds < 60 ? $"请在 {Math.Ceiling(delay.TotalSeconds)} 秒后重试签到。" :
                        $"签到查询冷却中，请在 {Math.Ceiling(delay.TotalMinutes)} 分钟后重试。";
                    NotifyChanged();
                }
                return Task.CompletedTask;
            }
            // Restrictions must be acknowledged by a user action, even after the
            // minimum cooldown and across application restarts.
            if (!manual && (CheckRemaining > TimeSpan.Zero || PausesAutomatic(Failure?.Kind)))
                return Task.CompletedTask;

            var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            _flight = completion.Task;
            Busy = true; _signing = sign;
            SetRetry(MinimumInterval);
            Schedule(CheckInterval);
            Message = sign ? "正在确认今日签到状态…" : "正在查询今日签到状态…";
            var generation = _generation;
            var credential = Credential;
            var cancellation = _cancel.Token;
            SaveCheckpoint(); // Crash/restart cannot immediately repeat this request.
            NotifyChanged();
            _ = RunOperationAsync(credential, generation, cancellation, sign, cachedPreflight, completion);
            return completion.Task;
        }
    }

    private async Task RunOperationAsync(Credential credential, int generation, CancellationToken cancellation,
        bool sign, DailySignInfo? cachedPreflight, TaskCompletionSource completion)
    {
        var awaitingSubmission = false;
        try
        {
            bool confirmOnly;
            lock (_gate)
            {
                if (!IsCurrent(generation, cancellation)) return;
                confirmOnly = UncertainSubmission;
            }
            var startedAt = _clock.GetUtcNow();
            var info = cachedPreflight ?? await client.GetDailySignInfoAsync(credential, cancellation);
            lock (_gate)
            {
                if (!IsCurrent(generation, cancellation)) return;
                Validate(info, startedAt);
                ApplyInfo(info);
                if (info.IsSigned || !sign) return;
                if (confirmOnly)
                {
                    Message = "已确认今日尚未签到。再次点击今日签到可提交。";
                    return;
                }

                // Checkpoint the uncertain state BEFORE calling the mutating endpoint.
                // A crash at any point after this event must recover with a GET only.
                UncertainSubmission = true;
                _unsignedInfo = null;
                SetRetry(MinimumInterval);
                Message = "正在提交今日签到…";
                if (!SaveCheckpoint())
                {
                    UncertainSubmission = false;
                    throw new ApiFailure(FailureKind.Protocol, "无法保存签到记录，请检查数据目录后重试。", -91001);
                }
                NotifyChanged();
                if (!IsCurrent(generation, cancellation)) return;
            }
            cancellation.ThrowIfCancellationRequested();
            awaitingSubmission = true;
            var result = await client.SubmitDailySignAsync(credential, cancellation);
            awaitingSubmission = false;
            lock (_gate)
            {
                if (!IsCurrent(generation, cancellation)) return;
                if (!Enum.IsDefined(result)) throw new ApiFailure(FailureKind.Protocol, "签到响应无法确认。");
            }

            startedAt = _clock.GetUtcNow();
            info = await client.GetDailySignInfoAsync(credential, cancellation);
            lock (_gate)
            {
                if (!IsCurrent(generation, cancellation)) return;
                Validate(info, startedAt);
                // An immediate negative read can lag behind a successful POST.
                // Only a later, separate check may clear this uncertainty.
                ApplyInfo(info, clearUncertain: info.IsSigned);
                if (!info.IsSigned)
                    Message = "服务器尚未确认今日签到。请稍后再次点击检查，避免连续提交。";
            }
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested) { }
        catch (ApiFailure error)
        {
            lock (_gate)
            {
                if (!IsCurrent(generation, cancellation)) return;
                // Only a definitive rejection of the POST itself establishes that
                // it was not performed. A subsequent GET rejection does not.
                if (awaitingSubmission &&
                    error.Kind is FailureKind.Login or FailureKind.Verification or FailureKind.Permission or FailureKind.RateLimited)
                    UncertainSubmission = false;
                ApplyFailure(error);
            }
        }
        catch (Exception error) when (error is HttpRequestException or IOException or TimeoutException or OperationCanceledException or System.Text.Json.JsonException)
        {
            lock (_gate)
            {
                if (!IsCurrent(generation, cancellation)) return;
                ApplyFailure(new(error is System.Text.Json.JsonException ? FailureKind.Protocol : FailureKind.Network,
                    "签到结果暂时无法确认。"));
            }
        }
        finally
        {
            lock (_gate)
            {
                if (generation == _generation && !_disposed)
                {
                    Busy = false; _signing = false;
                    SaveCheckpoint(); NotifyChanged();
                }
            }
            completion.TrySetResult();
        }
    }

    private bool IsCurrent(int generation, CancellationToken cancellation) =>
        !_disposed && generation == _generation && !cancellation.IsCancellationRequested;

    private void Validate(DailySignInfo info, DateTimeOffset startedAt)
    {
        var now = _clock.GetUtcNow();
        if (info.CheckedAt == default || info.CheckedAt < startedAt.AddMinutes(-2) ||
            info.CheckedAt > now.AddMinutes(2) || info.Day != ChinaDay(info.CheckedAt) || info.Day != Today)
            throw new ApiFailure(FailureKind.Protocol, "签到日期或响应时间无法确认，请检查系统时间后重试。");
    }

    private void ApplyInfo(DailySignInfo info, bool clearUncertain = true)
    {
        _lastCheckedAt = info.CheckedAt;
        _signedDay = info.IsSigned ? info.Day : null;
        if (clearUncertain) UncertainSubmission = false;
        _unsignedInfo = !info.IsSigned && !UncertainSubmission ? info : null;
        _unsignedStamp = _clock.GetTimestamp();
        Failure = null; ClockUncertain = false;
        ResetClockAnchor(); Schedule(CheckInterval);
        Message = info.IsSigned ? "今日已签到。" : "今日尚未签到，可领取每日奖励。";
    }

    private void ApplyFailure(ApiFailure error)
    {
        _unsignedInfo = null;
        Failure = new(error.Kind, error.Code == -91001 ? error.Message : FailureMessage(error.Kind), error.Code, error.RetryAfter);
        var minimum = PausesAutomatic(error.Kind) ? TimeSpan.FromMinutes(15) : TimeSpan.FromMinutes(5);
        var delay = error.RetryAfter is { } retry && retry > minimum ? retry : minimum;
        SetRetry(delay); Schedule(delay);
        Message = UncertainSubmission
            ? "签到可能已提交，但结果尚未确认。冷却结束后只查询结果，不会自动重复签到。"
            : Failure.Message;
    }

    private static bool PausesAutomatic(FailureKind? kind) => kind is FailureKind.Login or
        FailureKind.Verification or FailureKind.Permission or FailureKind.RateLimited;

    private static string FailureMessage(FailureKind kind) => kind switch
    {
        FailureKind.Login => "签到登录已失效，请重新连接米游社后重试。",
        FailureKind.Verification => "签到需要官方验证，已暂停自动查询。请在官方签到页完成验证。",
        FailureKind.Permission => "签到权限受限，已暂停自动查询。请在官方签到页检查账号与角色。",
        FailureKind.RateLimited => "签到请求受限，已暂停自动查询，请在冷却结束后手动重试。",
        FailureKind.Network => "签到网络连接失败，稍后重试。",
        _ => "暂时无法确认签到结果，请稍后重试。"
    };

    /// <summary>Accepts a fresh, explicitly supplied official status for this exact account/role.</summary>
    public void ObserveSigned(bool value, DateTimeOffset receivedAt, string accountKey, string roleKey)
    {
        lock (_gate)
        {
            if (_disposed || Busy || Credential?.Role is null || Credential.AccountKey != accountKey ||
                Credential.Role.Key != roleKey || receivedAt == default ||
                receivedAt > _clock.GetUtcNow().AddMinutes(2) ||
                _clock.GetUtcNow() - receivedAt > TimeSpan.FromMinutes(5) ||
                ChinaDay(receivedAt) != Today || _lastCheckedAt > receivedAt ||
                !value && IsSignedToday)
                return;
            // A "not signed" widget flag must not clear an unresolved POST or a
            // restriction from the actual sign-in endpoint.
            if (!value && (UncertainSubmission || Failure is not null)) return;
            ApplyInfo(new(value, ChinaDay(receivedAt), receivedAt));
            SaveCheckpoint(); NotifyChanged();
        }
    }

    private void SetRetry(TimeSpan delay)
    {
        _retryStamp = _clock.GetTimestamp(); _retryDelay = delay;
    }

    private void Schedule(TimeSpan delay)
    {
        _checkStamp = _clock.GetTimestamp(); _checkDelay = delay;
    }

    private void NotifyChanged(bool onlyWhenPresentationChanges = false)
    {
        var presentation = $"{ButtonText}|{CanSign}|{NeedsAttention}|{Message}";
        if (onlyWhenPresentationChanges && presentation == _lastPresentation) return;
        _lastPresentation = presentation;
        Changed?.Invoke();
    }

    public void Dispose()
    {
        lock (_gate)
        {
            if (_disposed) return;
            _disposed = true; _generation++;
            _cancel.Cancel(); _cancel.Dispose();
        }
    }
}
