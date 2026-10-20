namespace ZzzBattery.Core;

public sealed record WindowFit(double Width, double Height, double MinimumWidth, double MinimumHeight,
    double MaximumWidth, double MaximumHeight);

public static class DisplayLayoutMath
{
    // Inputs are physical pixels; window constraints are WPF DIPs. A margin is
    // reduced on extremely small work areas rather than creating negative sizes.
    public static WindowFit Fit(DesktopMonitor monitor, double preferredWidth, double preferredHeight,
        double minimumWidth, double minimumHeight, double margin = 8)
    {
        var scale = double.IsFinite(monitor.Scale) && monitor.Scale is >= .25 and <= 16 ? monitor.Scale : 1;
        static double Positive(double value, double fallback) => double.IsFinite(value) && value > 0 ? value : fallback;
        var width = Positive(monitor.WorkArea.Width, 1) / scale;
        var height = Positive(monitor.WorkArea.Height, 1) / scale;
        var safeMargin = double.IsFinite(margin) ? Math.Max(0, margin) : 8;
        var maxWidth = Math.Max(1, width - Math.Min(safeMargin * 2, width / 10));
        var maxHeight = Math.Max(1, height - Math.Min(safeMargin * 2, height / 10));
        var minWidth = Math.Min(maxWidth, Positive(minimumWidth, 1));
        var minHeight = Math.Min(maxHeight, Positive(minimumHeight, 1));
        return new(Math.Clamp(Positive(preferredWidth, minWidth), minWidth, maxWidth),
            Math.Clamp(Positive(preferredHeight, minHeight), minHeight, maxHeight),
            minWidth, minHeight, maxWidth, maxHeight);
    }

    public static DesktopRect Center(DesktopRect workArea, double widthPixels, double heightPixels) =>
        WindowPlacementMath.Clamp(new(workArea.X + (workArea.Width - widthPixels) / 2,
            workArea.Y + (workArea.Height - heightPixels) / 2, widthPixels, heightPixels), workArea);
}
