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
    string SupplementMessage = "", int SchemaVersion = 0, string Source = "实时便笺");

// Contains no cookies, challenge proofs, or raw server messages. Delays are saved
// relative to SavedAt so a restart preserves both backoff and the manual cooldown.
public sealed record SyncCheckpoint(string AccountKey, string RoleKey, DateTimeOffset SavedAt,
    TimeSpan RemainingDelay, TimeSpan MinimumRefreshDelay, FailureKind? FailureKind = null,
    int? FailureCode = null, int ConsecutiveFailures = 0, int SchemaVersion = 1, string? VerificationPath = null);

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
    public string Theme { get; set; } = "Dark";
    public string Accent { get; set; } = "Lime";
    public bool CharacterStyle { get; set; } = true;
    public WidgetWallpaper? Wallpaper { get; set; }
    public string GameExecutable { get; set; } = "";
    public double? Left { get; set; }
    public double? Top { get; set; }
    public string DeviceId { get; set; } = Guid.NewGuid().ToString();
}

public sealed class ReminderState
{
    public string Key { get; set; } = "";
    public DateTimeOffset? FullAt { get; set; }
    public bool EarlySent { get; set; }
    public bool FullSent { get; set; }

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
