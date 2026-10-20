namespace ZzzBattery.Core;

public static class EnergyEstimator
{
    public const int SecondsPerPoint = 360;
    public static EnergyProjection Project(EnergySnapshot snapshot, TimeSpan elapsed, bool freeze = false, TimeSpan? maxStaleThreshold = null)
    {
        var limit = maxStaleThreshold ?? TimeSpan.FromHours(1);
        var stale = elapsed < TimeSpan.FromSeconds(-5) || elapsed > limit;
        if (stale || freeze)
            return new(snapshot.Current, snapshot.Maximum, null, false, true, false);
        if (snapshot.Current >= snapshot.Maximum)
            return new(snapshot.Current, snapshot.Maximum, 0, false, false, false);
        if (snapshot.RestoreSeconds is not > 0)
            return new(snapshot.Current, snapshot.Maximum, null, false, false, true);

        long missing = (long)snapshot.Maximum - snapshot.Current;
        var restore = snapshot.RestoreSeconds.Value;
        var consistent = restore > (missing - 1) * SecondsPerPoint - 2 &&
                         restore <= missing * SecondsPerPoint + 2;
        var remaining = (int)Math.Max(0, restore - Math.Max(0, Math.Floor(elapsed.TotalSeconds)));
        if (!consistent)
            return new(snapshot.Current, snapshot.Maximum, remaining, false, false, true);
        int current = Math.Clamp(snapshot.Maximum - (int)Math.Ceiling(remaining / (double)SecondsPerPoint),
            snapshot.Current, snapshot.Maximum);
        return new(current, snapshot.Maximum, remaining, elapsed.TotalSeconds >= 60, false, false);
    }

    public static string Countdown(EnergyProjection p)
    {
        if (p.Stale) return "上次电量 · 等待同步";
        if (p.Full) return p.Current > p.Maximum ? "已超过自然恢复上限" : "电量已回满";
        if (p.RemainingSeconds is null) return "恢复时间暂不可用";
        if (p.Inconsistent && p.RemainingSeconds == 0) return "需要同步确认电量";
        var minutes = (int)Math.Ceiling(p.RemainingSeconds.Value / 60d);
        if (minutes < 1) return "即将回满";
        return minutes >= 60 ? $"{minutes / 60} 小时 {minutes % 60} 分后回满" : $"{minutes} 分后回满";
    }

    public static string FinishAt(DateTimeOffset fullAt, DateTimeOffset now)
    {
        var local = fullAt.ToLocalTime();
        var days = (local.Date - now.ToLocalTime().Date).Days;
        var day = days switch { 0 => "今天", 1 => "明天", _ => local.ToString("M月d日") };
        return $"{day} {local:HH:mm}";
    }
}
