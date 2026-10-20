namespace ZzzBattery.Core;

/// <summary>A rectangle in physical desktop pixels; negative screen coordinates are valid.</summary>
public sealed record DesktopRect(double X, double Y, double Width, double Height)
{
    public double Right => X + Width;
    public double Bottom => Y + Height;
}

public sealed record DesktopMonitor(string Id, string DeviceName, DesktopRect WorkArea,
    double Scale = 1, bool Primary = false);

/// <summary>
/// The user's preferred position. Offsets are device-independent pixels relative to
/// the saved monitor's work area; an anchored offset is measured from that edge.
/// A temporary fallback or visibility correction must not replace this value.
/// </summary>
public sealed record WindowPlacement(string MonitorId, string DeviceName, DesktopRect SavedWorkArea,
    double SavedScale, double OffsetX, double OffsetY, bool AnchorRight = false,
    bool AnchorBottom = false, int Version = 1);

public sealed record PlacementResult(DesktopMonitor Monitor, DesktopRect Bounds, bool PreferredMonitorAvailable);

public static class WindowPlacementMath
{
    private const double MaximumCoordinate = 10_000_000;
    private const double MaximumDimension = 1_000_000;
    private const double SnapDistance = 16;
    private static readonly DesktopRect SafeBounds = new(0, 0, 1, 1);

    public static bool IsValid(WindowPlacement? saved) =>
        saved is { Version: 1 } && ValidIdentity(saved.MonitorId, saved.DeviceName) &&
        ValidRect(saved.SavedWorkArea) && ValidScale(saved.SavedScale) &&
        ValidCoordinate(saved.OffsetX) && ValidCoordinate(saved.OffsetY);

    public static WindowPlacement Capture(DesktopRect bounds, DesktopMonitor monitor)
    {
        // Monitor enumeration can be transiently unavailable during session startup.
        // Return an invalid marker instead of persisting unusable coordinates.
        if (!ValidRect(bounds) || !ValidMonitor(monitor))
            return new("", "", new(0, 0, 0, 0), 1, 0, 0, Version: 0);

        var rightGap = monitor.WorkArea.Right - bounds.Right;
        var bottomGap = monitor.WorkArea.Bottom - bounds.Bottom;
        var anchorRight = Math.Abs(rightGap) <= 1;
        var anchorBottom = Math.Abs(bottomGap) <= 1;
        var result = new WindowPlacement(monitor.Id, monitor.DeviceName, monitor.WorkArea, monitor.Scale,
            (anchorRight ? rightGap : bounds.X - monitor.WorkArea.X) / monitor.Scale,
            (anchorBottom ? bottomGap : bounds.Y - monitor.WorkArea.Y) / monitor.Scale,
            anchorRight, anchorBottom);
        return IsValid(result) ? result : result with { Version = 0 };
    }

    public static PlacementResult? Restore(WindowPlacement? saved, IReadOnlyList<DesktopMonitor> monitors,
        double widthPixels, double heightPixels)
    {
        if (monitors is null || !ValidDimension(widthPixels) || !ValidDimension(heightPixels))
            return null;

        var available = monitors.Where(ValidMonitor).ToArray();
        if (available.Length == 0) return null;
        var preferred = IsValid(saved) ? available.FirstOrDefault(monitor =>
            !string.IsNullOrWhiteSpace(saved!.MonitorId)
                ? string.Equals(monitor.Id, saved.MonitorId, StringComparison.OrdinalIgnoreCase)
                : string.Equals(monitor.DeviceName, saved.DeviceName, StringComparison.OrdinalIgnoreCase)) : null;
        var target = preferred ?? available.FirstOrDefault(monitor => monitor.Primary) ?? available[0];
        var area = target.WorkArea;

        var x = preferred is null
            ? area.Right - widthPixels - 28 * target.Scale
            : saved!.AnchorRight
                ? area.Right - widthPixels - saved.OffsetX * target.Scale
                : area.X + saved.OffsetX * target.Scale;
        var y = preferred is null
            ? area.Y + 70 * target.Scale
            : saved!.AnchorBottom
                ? area.Bottom - heightPixels - saved.OffsetY * target.Scale
                : area.Y + saved.OffsetY * target.Scale;

        // DIP division/multiplication can introduce subpixel floating point noise.
        // Preserve physical integer coordinates across arbitrarily many restarts.
        var bounds = new DesktopRect(RemovePixelNoise(x), RemovePixelNoise(y), widthPixels, heightPixels);
        return new(target, Clamp(bounds, area), preferred is not null);
    }

    public static DesktopRect Clamp(DesktopRect bounds, DesktopRect workArea, bool snap = false)
    {
        if (!ValidRect(workArea)) return ValidRect(bounds) ? bounds : SafeBounds;
        if (!ValidRect(bounds))
            return new(workArea.X, workArea.Y,
                bounds is not null && ValidDimension(bounds.Width) ? bounds.Width : 1,
                bounds is not null && ValidDimension(bounds.Height) ? bounds.Height : 1);

        var x = ClampAxis(bounds.X, bounds.Width, workArea.X, workArea.Width, snap);
        var y = ClampAxis(bounds.Y, bounds.Height, workArea.Y, workArea.Height, snap);
        return bounds with { X = x, Y = y };
    }

    private static double ClampAxis(double position, double size, double start, double available, bool snap)
    {
        // A window larger than the work area keeps its top/left controls reachable.
        if (size >= available) return start;
        var end = start + available - size;
        var result = Math.Clamp(position, start, end);
        if (!snap) return result;
        var before = result - start;
        var after = end - result;
        if (before <= SnapDistance && before <= after) return start;
        return after <= SnapDistance ? end : result;
    }

    private static double RemovePixelNoise(double value)
    {
        var pixel = Math.Round(value);
        return Math.Abs(value - pixel) < 0.0000001 ? pixel : value;
    }

    private static bool ValidMonitor(DesktopMonitor? monitor) =>
        monitor is not null && ValidIdentity(monitor.Id, monitor.DeviceName) &&
        ValidRect(monitor.WorkArea) && ValidScale(monitor.Scale);

    private static bool ValidIdentity(string? id, string? deviceName) =>
        id is not null && deviceName is not null && id.Length <= 4096 && deviceName.Length <= 4096 &&
        (!string.IsNullOrWhiteSpace(id) || !string.IsNullOrWhiteSpace(deviceName));

    private static bool ValidRect(DesktopRect? value) =>
        value is not null && ValidCoordinate(value.X) && ValidCoordinate(value.Y) &&
        ValidDimension(value.Width) && ValidDimension(value.Height) &&
        ValidCoordinate(value.Right) && ValidCoordinate(value.Bottom);

    private static bool ValidCoordinate(double value) =>
        double.IsFinite(value) && Math.Abs(value) <= MaximumCoordinate;

    private static bool ValidDimension(double value) =>
        double.IsFinite(value) && value > 0 && value <= MaximumDimension;

    private static bool ValidScale(double value) =>
        double.IsFinite(value) && value is >= 0.25 and <= 16;
}
