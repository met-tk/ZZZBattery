namespace ZzzBattery.Core;

public sealed class WidgetSession(IMiyousheClient client, TimeProvider? clock = null,
    Func<Credential, CancellationToken, Task<EnergySnapshot>>? officialNoteReader = null) : IDisposable
{
    private readonly TimeProvider _clock = clock ?? TimeProvider.System;
    private CancellationTokenSource _cancel = new();
    private Task _flight = Task.CompletedTask;
    private int _generation, _failures;
    private DateTimeOffset _anchorUtc;
    private long _anchorStamp;
    private TimeSpan _initialAge;
    private long _cooldownStamp;
    private TimeSpan _cooldownDelay;
    private long _scheduleStamp;
    private TimeSpan _scheduleDelay;
    private bool _preferOfficialPage, _automaticRecoveryAttempted;
    public Credential? Credential { get; private set; }
    public EnergySnapshot? Snapshot { get; private set; }
    public ApiFailure? Failure { get; private set; }
    public bool Busy { get; private set; }
    public bool RecoveringInBackground { get; private set; }
    public bool ClockUncertain { get; private set; }
    private TimeSpan ScheduledRemaining => TimeSpan.FromTicks(Math.Max(0,
        (_scheduleDelay - _clock.GetElapsedTime(_scheduleStamp)).Ticks));
    private TimeSpan CooldownRemaining => TimeSpan.FromTicks(Math.Max(0,
        (_cooldownDelay - _clock.GetElapsedTime(_cooldownStamp)).Ticks));
    public DateTimeOffset NextAttempt => _clock.GetUtcNow() + ScheduledRemaining;
    public string Message { get; private set; } = "连接米游社，读取你的电量";
    public event Action? Changed;
    public event Action<EnergySnapshot>? Synced;
    public event Action<SyncCheckpoint>? CheckpointChanged;

    public bool HasSyncedSince(DateTimeOffset since) => !Busy && !ClockUncertain && Failure is null &&
        Credential?.Role is { } role && Snapshot is { } snapshot && snapshot.ReceivedAt >= since &&
        snapshot.AccountKey == Credential.AccountKey && snapshot.RoleKey == role.Key;

    public void SetCredential(Credential? credential, EnergySnapshot? cached = null, SyncCheckpoint? checkpoint = null)
    {
        _generation++;
        _cancel.Cancel(); _cancel.Dispose(); _cancel = new();
        _flight = Task.CompletedTask; Busy = false; RecoveringInBackground = false;
        Credential = credential;
        Snapshot = credential?.Role is not null && cached is { Current: >= 0 and <= 1000000, Maximum: > 0 and <= 1000000, Notes: not null } &&
            cached.Notes.Count <= 150 && cached.Notes.All(n => n is { Name: not null, Value: not null, Id: not null, Group: not null, Detail: not null, Source: not null }) &&
            cached.ReceivedAt != default && cached.AccountKey == credential?.AccountKey &&
            cached.RoleKey == credential.Role?.Key ? cached : null;
        Failure = null; _failures = 0; ClockUncertain = false;
        _preferOfficialPage = Snapshot?.Source == "官方网页便笺";
        _automaticRecoveryAttempted = false;
        Schedule(TimeSpan.Zero);
        SetCooldown(TimeSpan.Zero);
        ResetAnchor();
        Message = credential is null ? "连接米游社，读取你的电量" : "等待同步";
        if (credential?.Role is not null)
        {
            if (!RestoreCheckpoint(checkpoint) && Snapshot is not null)
            {
                // Upgrade/startup with a recent cache must not create another network burst.
                var age = _clock.GetUtcNow() - Snapshot.ReceivedAt;
                if (age >= TimeSpan.Zero)
                {
                    var dailyReset = NoteBoard.NextDailyReset(Snapshot.ReceivedAt).AddSeconds(5);
                    var due = Snapshot.ReceivedAt.AddMinutes(25);
                    if (dailyReset < due) due = dailyReset;
                    Schedule(Nonnegative(due - _clock.GetUtcNow()));
                    SetCooldown(Nonnegative(TimeSpan.FromSeconds(60) - age));
                    Message = "已恢复上次数据，等待定时同步";
                }
                else ClockUncertain = true;
            }
            SaveCheckpoint();
        }
        Changed?.Invoke();
    }

    private static TimeSpan Nonnegative(TimeSpan delay) => delay > TimeSpan.Zero ? delay : TimeSpan.Zero;
    private void SetCooldown(TimeSpan delay)
    {
        _cooldownStamp = _clock.GetTimestamp();
        _cooldownDelay = delay;
    }

    public SyncCheckpoint? CaptureCheckpoint() => Credential?.Role is { } role
        ? new(Credential.AccountKey, role.Key, _clock.GetUtcNow(), ScheduledRemaining, CooldownRemaining,
            Failure?.Kind, Failure?.Code, _failures, VerificationPath: Failure?.VerificationPath,
            PreferOfficialPage: _preferOfficialPage, AutomaticRecoveryAttempted: _automaticRecoveryAttempted) : null;

    private bool SaveCheckpoint()
    {
        try
        {
            if (CaptureCheckpoint() is { } checkpoint) CheckpointChanged?.Invoke(checkpoint);
            return true;
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            Failure = new(FailureKind.Permission, "无法保存同步状态，已暂停请求。请检查数据目录的写入权限后再刷新。");
            Message = Failure.Message;
            return false;
        }
    }

    private bool RestoreCheckpoint(SyncCheckpoint? checkpoint)
    {
        if (checkpoint is null || checkpoint.SchemaVersion != 1 || checkpoint.SavedAt == default ||
            checkpoint.AccountKey != Credential?.AccountKey || checkpoint.RoleKey != Credential?.Role?.Key ||
            checkpoint.RemainingDelay < TimeSpan.Zero || checkpoint.RemainingDelay > TimeSpan.FromDays(1) ||
            checkpoint.MinimumRefreshDelay < TimeSpan.Zero || checkpoint.MinimumRefreshDelay > TimeSpan.FromMinutes(1) ||
            checkpoint.ConsecutiveFailures is < 0 or > 100 ||
            (checkpoint.FailureKind is { } kind && !Enum.IsDefined(kind))) return false;
        var elapsed = _clock.GetUtcNow() - checkpoint.SavedAt;
        // A backwards wall-clock correction cannot expire a saved cooldown.
        if (elapsed < TimeSpan.Zero) { elapsed = TimeSpan.Zero; ClockUncertain = true; }
        Schedule(Nonnegative(checkpoint.RemainingDelay - elapsed));
        SetCooldown(Nonnegative(checkpoint.MinimumRefreshDelay - elapsed));
        _failures = checkpoint.ConsecutiveFailures;
        _preferOfficialPage |= checkpoint.PreferOfficialPage;
        _automaticRecoveryAttempted = checkpoint.AutomaticRecoveryAttempted;
        if (checkpoint.FailureKind is { } failureKind)
        {
            var known = checkpoint.FailureCode is { } code ? MiyousheClient.FromRetcode(code) : null;
            var message = known?.Kind == failureKind ? known.Message : failureKind switch
            {
                FailureKind.Verification => "查询需要验证，自动同步已暂停。请在官方页面完成验证。",
                FailureKind.Login => "登录已失效，请重新连接米游社。",
                FailureKind.Permission => "查询权限受限，自动同步已暂停，请检查官方战绩页。",
                FailureKind.RateLimited => "请求受限，冷却结束后自动重试。",
                FailureKind.Network => "上次同步网络异常，保留缓存并等待重试。",
                _ => "上次同步未完成，保留缓存并等待重试。"
            };
            Failure = new(failureKind, message, checkpoint.FailureCode, ScheduledRemaining);
            Failure.VerificationPath = checkpoint.VerificationPath == "/api/zzz/widget" ? "/api/zzz/widget" : "/api/zzz/note";
            Message = message;
            if (failureKind == FailureKind.Verification && _automaticRecoveryAttempted)
                Failure = new(failureKind, Message = "后台恢复未完成，已暂停自动尝试。可在方便时点击“恢复电量同步”处理。",
                    checkpoint.FailureCode, ScheduledRemaining) { VerificationPath = Failure.VerificationPath };
        }
        else Message = "已恢复上次数据，等待定时同步";
        return true;
    }

    private void Schedule(TimeSpan delay)
    {
        _scheduleStamp = _clock.GetTimestamp();
        _scheduleDelay = delay;
    }

    private void ResetAnchor()
    {
        _anchorUtc = _clock.GetUtcNow();
        _anchorStamp = _clock.GetTimestamp();
        _initialAge = Snapshot is null ? TimeSpan.Zero : _anchorUtc - Snapshot.ReceivedAt;
    }

    public EnergyProjection? Projection()
    {
        if (Snapshot is null) return null;
        var elapsed = _clock.GetElapsedTime(_anchorStamp);
        if (!ClockUncertain && Math.Abs((_clock.GetUtcNow() - _anchorUtc - elapsed).TotalSeconds) > 120)
        {
            ClockUncertain = true;
        }
        return EnergyEstimator.Project(Snapshot, _initialAge + elapsed,
            ClockUncertain || Failure?.Kind == FailureKind.Login, TimeSpan.FromHours(2));
    }

    public void InvalidateTime()
    {
        var elapsed = _clock.GetElapsedTime(_anchorStamp);
        // A normal resume (or a small NTP correction) does not make fresh data stale.
        if (!ClockUncertain && Math.Abs((_clock.GetUtcNow() - _anchorUtc - elapsed).TotalSeconds) <= 120) return;
        ClockUncertain = true;
        // Sleep/wake and clock corrections do not change the monotonic request schedule.
        Message = Failure?.Message ?? "时间状态已变化，将在下次同步时校准"; Changed?.Invoke();
    }

    public void ResumeAfterVerification(EnergySnapshot? verifiedSnapshot = null, bool resetCooldown = false)
    {
        if (verifiedSnapshot is not null)
        {
            if (Credential?.Role is null || verifiedSnapshot.AccountKey != Credential.AccountKey || verifiedSnapshot.RoleKey != Credential.Role.Key) return;
            if (Snapshot is not null && verifiedSnapshot.ReceivedAt < Snapshot.ReceivedAt) return;
            // A response captured by the official page supersedes any older HTTP request.
            _generation++; _cancel.Cancel(); _cancel.Dispose(); _cancel = new();
            _flight = Task.CompletedTask; Busy = false; RecoveringInBackground = false;
            Snapshot = verifiedSnapshot;
            _preferOfficialPage = verifiedSnapshot.Source == "官方网页便笺";
            _automaticRecoveryAttempted = false;
            ResetAnchor();
            ClockUncertain = false;
            Failure = null;
            _failures = 0;
            SetCooldown(TimeSpan.FromSeconds(60));
            ScheduleSuccess();
            Message = "同步成功";
            Synced?.Invoke(verifiedSnapshot);
            SaveCheckpoint();
            Changed?.Invoke();
            return;
        }
        if (Failure?.Kind == FailureKind.RateLimited && ScheduledRemaining > TimeSpan.Zero) return;
        if (Failure?.Kind is FailureKind.Verification or FailureKind.Permission)
        {
            Failure = null;
            _failures = 0;
            _automaticRecoveryAttempted = false;
        }
        if (resetCooldown)
        {
            SetCooldown(TimeSpan.Zero);
        }
        Schedule(TimeSpan.Zero);
        Message = "验证已通过，等待同步电量";
        SaveCheckpoint();
        Changed?.Invoke();
    }

    private void ScheduleSuccess()
    {
        var regular = TimeSpan.FromMinutes(25) + TimeSpan.FromSeconds(Random.Shared.Next(0, 301));
        var dailyReset = NoteBoard.NextDailyReset(_clock.GetUtcNow()) - _clock.GetUtcNow() + TimeSpan.FromSeconds(5);
        Schedule(dailyReset < regular ? dailyReset : regular);
    }

    public Task RefreshAsync(bool manual = false)
    {
        if (!_flight.IsCompleted) return _flight;
        if (Credential?.Role is null) return Task.CompletedTask;
        var wait = CooldownRemaining;
        if (Failure is not null && ScheduledRemaining > wait) wait = ScheduledRemaining;
        if (wait > TimeSpan.Zero)
        {
            if (manual)
            {
                Message = wait.TotalSeconds < 60 ? $"请在 {Math.Ceiling(wait.TotalSeconds)} 秒后刷新"
                    : $"请在 {Math.Ceiling(wait.TotalMinutes)} 分钟后刷新，当前处于同步冷却中";
                Changed?.Invoke();
            }
            return Task.CompletedTask;
        }
        var canRecover = officialNoteReader is not null && Failure?.Kind == FailureKind.Verification && !_automaticRecoveryAttempted;
        if (!manual && (ScheduledRemaining > TimeSpan.Zero || (Failure?.NeedsUser == true && !canRecover))) return Task.CompletedTask;
        _flight = FetchAsync(Credential, _generation, _cancel.Token);
        return _flight;
    }

    private async Task FetchAsync(Credential credential, int generation, CancellationToken cancellation)
    {
        Busy = true; SetCooldown(TimeSpan.FromSeconds(60));
        Schedule(TimeSpan.FromSeconds(60)); // Persist before I/O, including interrupted/crashed requests.
        Message = "正在同步米游社…";
        if (!SaveCheckpoint()) { Busy = false; Changed?.Invoke(); return; }
        Changed?.Invoke();
        try
        {
            var snapshot = await ReadSnapshotAsync(credential, generation, cancellation);
            if (generation != _generation || cancellation.IsCancellationRequested) return;
            Snapshot = snapshot; ResetAnchor(); ClockUncertain = false; Failure = null; _failures = 0;
            _automaticRecoveryAttempted = false;
            ScheduleSuccess();
            Message = "同步成功"; Synced?.Invoke(snapshot);
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested) { }
        catch (ApiFailure error)
        {
            if (generation != _generation) return;
            Failure = error; Message = error.Message;
            _failures = Math.Min(_failures + 1, 100);
            var minutes = new[] { 1, 2, 5, 15 }[Math.Min(_failures - 1, 3)];
            if (error.NeedsUser) minutes = 15;
            var delay = error.RetryAfter ?? TimeSpan.FromMinutes(minutes);
            if (delay < TimeSpan.FromSeconds(60)) delay = TimeSpan.FromSeconds(60);
            if (delay > TimeSpan.FromHours(24)) delay = TimeSpan.FromHours(24);
            Schedule(delay);
        }
        finally
        {
            if (generation == _generation) { Busy = false; RecoveringInBackground = false; SaveCheckpoint(); Changed?.Invoke(); }
        }
    }

    private async Task<EnergySnapshot> ReadSnapshotAsync(Credential credential, int generation, CancellationToken cancellation)
    {
        if (officialNoteReader is null) return await client.GetNoteAsync(credential, credential.Role!, cancellation);
        if (!_preferOfficialPage && Failure?.Kind != FailureKind.Verification)
        {
            try { return await client.GetNoteAsync(credential, credential.Role!, cancellation); }
            catch (ApiFailure error) when (error.Kind == FailureKind.Verification &&
                !_automaticRecoveryAttempted && !(error.RetryAfter > TimeSpan.Zero))
            {
                if (generation != _generation || cancellation.IsCancellationRequested) throw new OperationCanceledException(cancellation);
                Failure = error;
            }
        }
        cancellation.ThrowIfCancellationRequested();
        // Keep using the working browser channel instead of probing the restricted widget each cycle.
        _preferOfficialPage = true;
        _automaticRecoveryAttempted = true;
        RecoveringInBackground = true;
        Message = "正在后台读取官方便笺，不会弹出窗口…";
        Schedule(TimeSpan.FromMinutes(15)); // A crash must not immediately start another page load.
        if (!SaveCheckpoint()) throw Failure!;
        Changed?.Invoke();
        var snapshot = await officialNoteReader(credential, cancellation);
        if (snapshot.AccountKey != credential.AccountKey || snapshot.RoleKey != credential.Role!.Key ||
            snapshot.Source != "官方网页便笺" || snapshot.ReceivedAt < _clock.GetUtcNow().AddMinutes(-1) ||
            snapshot.ReceivedAt > _clock.GetUtcNow().AddMinutes(1))
            throw new ApiFailure(FailureKind.Verification, "后台未取得当前角色的新数据，已暂停自动尝试。可点击“恢复电量同步”处理。");
        return snapshot;
    }

    public void Dispose() { _cancel.Cancel(); _cancel.Dispose(); }
}
