namespace ZzzBattery.Core;

public sealed record GameRole(string Uid, string Server, string Nickname, string RegionName)
{
    public string Key => Server + ":" + Uid;
    public string DisplayName => $"{Nickname} · {RegionName} · {Uid}";
}

// Deliberately not a record: generated ToString must never print the cookie.
public sealed class Credential
{
    public string Cookie { get; set; } = "";
    public string AccountKey { get; set; } = "";
    public GameRole? Role { get; set; }
    public override string ToString() => "[受保护的米游社凭据]";
}

public enum NoteState { Unknown, Pending, InProgress, Complete, Locked, Stale }
public enum OfficialNoteCaptureState { Ignored, RoleMismatch, HttpError, InvalidData, Rejected, Ready }
public sealed record OfficialNoteCapture(OfficialNoteCaptureState State, EnergySnapshot? Snapshot = null, int? Code = null);
public sealed record NoteLine(string Name, string Value, bool Pending = false,
    string Id = "", NoteState State = NoteState.Unknown, string Group = "日常",
    int? Current = null, int? Target = null, DateTimeOffset? ResetAt = null,
    string Detail = "", string Source = "实时便笺", bool Daily = false);
public sealed record EnergySnapshot(
    string AccountKey, string RoleKey, int Current, int Maximum, int? RestoreSeconds,
    DateTimeOffset ReceivedAt, IReadOnlyList<NoteLine> Notes,
    string SupplementMessage = "", int SchemaVersion = 0, string Source = "实时便笺", bool? CommunitySigned = null);

// Contains no cookies, challenge proofs, or raw server messages. Delays are saved
// relative to SavedAt so a restart preserves both backoff and the manual cooldown.
public sealed record SyncCheckpoint(string AccountKey, string RoleKey, DateTimeOffset SavedAt,
    TimeSpan RemainingDelay, TimeSpan MinimumRefreshDelay, FailureKind? FailureKind = null,
    int? FailureCode = null, int ConsecutiveFailures = 0, int SchemaVersion = 1, string? VerificationPath = null,
    bool PreferOfficialPage = false, bool AutomaticRecoveryAttempted = false);

public sealed record EnergyProjection(int Current, int Maximum, int? RemainingSeconds,
    bool Estimated, bool Stale, bool Inconsistent)
{
    public bool Full => Current >= Maximum;
    public double Ratio => Maximum <= 0 ? 0 : Math.Clamp((double)Current / Maximum, 0, 1);
}

public enum FailureKind { Network, Login, Verification, Permission, RateLimited, Protocol }

public sealed class ApiFailure(FailureKind kind, string message, int? code = null,
    TimeSpan? retryAfter = null) : Exception(message)
{
    public FailureKind Kind { get; } = kind;
    public int? Code { get; } = code;
    public TimeSpan? RetryAfter { get; } = retryAfter;
    // Used only to bind an official verification task to the failed request; never displayed or logged.
    public string? VerificationTrace { get; internal set; }
    public string? VerificationPath { get; internal set; }
    public bool NeedsUser => Kind is FailureKind.Login or FailureKind.Verification or FailureKind.Permission;
}

public sealed record VerificationTask(string Gt, string Challenge, bool NewCaptcha, bool Online,
    string AccountKey, string RoleKey, DateTimeOffset CreatedAt, string? Trace, string Path = "/api/zzz/note");
public sealed record VerificationAnswer(string Challenge, string Validate, string Seccode);
// Short-lived server proof returned after a successful Geetest flow. It is
// safe to persist only when encrypted by the platform store; it contains no
// login cookies and expires quickly.
public sealed record VerificationProof(string AccountKey, string RoleKey, string Value, DateTimeOffset ExpiresAt);

public sealed class WidgetSettings
{
    public bool Mini { get; set; }
    public bool Expanded { get; set; }
    public bool Topmost { get; set; }
    public bool Locked { get; set; }
    public bool ClickThrough { get; set; }
    public bool StartAtLogin { get; set; }
    public bool ReminderEnabled { get; set; }
    public int ReminderMinutes { get; set; } = 30;
    public bool EnergyReminderEnabled { get; set; }
    public int ReminderEnergy { get; set; } = 180;
    public string ReminderSound { get; set; } = "System";
    public string ReminderSoundPath { get; set; } = "";
    public string Theme { get; set; } = "Dark";
    public string Accent { get; set; } = "Lime";
    public bool CharacterStyle { get; set; } = true;
    public WidgetWallpaper? Wallpaper { get; set; }
    public string GameExecutable { get; set; } = "";
    public double? Left { get; set; }
    public double? Top { get; set; }
    public WindowPlacement? Placement { get; set; }
    public string DeviceId { get; set; } = Guid.NewGuid().ToString();
    public string AndroidDeviceFp { get; set; } = "";
    public string WebDeviceFp { get; set; } = "";
    public DateTimeOffset? AndroidDeviceFpUpdatedAt { get; set; }
    public DateTimeOffset? WebDeviceFpUpdatedAt { get; set; }
    public bool MiyoQianEnabled { get; set; } = true;
    public bool MiyoQianAutoStart { get; set; } = true;
    public bool MiyoQianSyncWithDailySign { get; set; } = true;
    public bool MiyoQianHideBar { get; set; }
    public string MiyoQianPath { get; set; } = "";
}

public sealed record ReminderState
{
    public string Key { get; set; } = "";
    public DateTimeOffset? FullAt { get; set; }
    public bool EarlySent { get; set; }
    public bool FullSent { get; set; }
    public string EnergyKey { get; set; } = "";
    public int? EnergyTarget { get; set; }
    public bool EnergyArmed { get; set; }
    public DateTimeOffset? EnergyFullAt { get; set; }

    public string? EvaluateEnergy(EnergySnapshot snapshot, EnergyProjection projection,
        bool healthy, int? target)
    {
        if (target is null || target < 1 || target > snapshot.Maximum)
        {
            EnergyTarget = null; EnergyArmed = false; EnergyFullAt = null;
            return null;
        }
        if (!healthy || projection.Stale || projection.Inconsistent) return null;
        var key = snapshot.AccountKey + ":" + snapshot.RoleKey;
        var fullAt = snapshot.RestoreSeconds > 0
            ? snapshot.ReceivedAt.AddSeconds(snapshot.RestoreSeconds.Value) : (DateTimeOffset?)null;
        if (EnergyKey != key || EnergyTarget != target)
        {
            EnergyKey = key; EnergyTarget = target;
            // Enabling a rule when its target is already reached is not a crossing.
            EnergyArmed = projection.Current < target;
            EnergyFullAt = fullAt;
            return null;
        }
        if (projection.Current < target)
        {
            // A small server-time correction below the target is not another consumption cycle.
            if (!EnergyFullAt.HasValue || fullAt > EnergyFullAt.Value.AddSeconds(120)) EnergyArmed = true;
            if (EnergyArmed) EnergyFullAt = fullAt;
            return null;
        }
        if (!EnergyArmed) return null;
        EnergyArmed = false;
        return $"电量已达到 {target}，当前{(projection.Estimated ? "预计" : "")}为 {projection.Current}/{projection.Maximum}，可以安排上线了。";
    }

    public string? Evaluate(EnergySnapshot snapshot, EnergyProjection projection,
        DateTimeOffset now, bool healthy, int earlyMinutes)
    {
        if (!healthy || projection.Stale || projection.Inconsistent) return null;
        var key = snapshot.AccountKey + ":" + snapshot.RoleKey;
        var target = snapshot.RestoreSeconds > 0
            ? snapshot.ReceivedAt.AddSeconds(snapshot.RestoreSeconds.Value) : (DateTimeOffset?)null;
        if (Key != key || (target.HasValue && (!FullAt.HasValue || Math.Abs((target.Value - FullAt.Value).TotalSeconds) > 120)))
        {
            Key = key; FullAt = target; EarlySent = false; FullSent = false;
        }
        // Starting the app with already-full stamina must not generate a spurious cycle.
        if (!FullAt.HasValue) return null;
        if (projection.Full && !FullSent && now >= FullAt.Value.AddSeconds(-2))
        {
            FullSent = true; EarlySent = true;
            return "电量已回满，可以安排上线了。";
        }
        if (!EarlySent && projection.RemainingSeconds is > 0 &&
            projection.RemainingSeconds <= earlyMinutes * 60)
        {
            EarlySent = true;
            return $"预计 {Math.Ceiling(projection.RemainingSeconds.Value / 60d):0} 分钟后回满电量。";
        }
        return null;
    }
}
