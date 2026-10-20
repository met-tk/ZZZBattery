using System.Text.Json;
using Xunit;
using ZzzBattery.Core;

namespace ZzzBattery.Tests;

public sealed class DisplayAdaptationTests
{
    [Fact]
    public void RotationScalingAndTemporaryDisconnectionNeverRewriteUserPreference()
    {
        var internalPanel = new DesktopMonitor("internal", "DISPLAY1",
            new(0, 0, 1920, 1040), 1, true);
        var external = new DesktopMonitor("external", "DISPLAY2",
            new(-3840, -960, 3840, 2120), 2);
        var originalBounds = new DesktopRect(-1360, 200, 752, 900);
        var saved = WindowPlacementMath.Capture(originalBounds, external);
        var serialized = JsonSerializer.Serialize(saved);

        // Simulate docking, rotation, Windows display renumbering, and changing
        // the primary display while keeping the same saved user preference.
        DesktopMonitor[][] topologies =
        [
            [internalPanel],
            [external with { WorkArea = new(-1080, -720, 1080, 1880), Scale = 1.5 }, internalPanel],
            [internalPanel, external with { DeviceName = "DISPLAY7", WorkArea = new(1920, 0, 1280, 680), Scale = 1.25 }],
            [external with { Primary = true }, internalPanel with { Primary = false }],
            [external, internalPanel]
        ];

        foreach (var monitors in topologies)
        {
            var targetScale = monitors.FirstOrDefault(m => m.Id == external.Id)?.Scale ?? internalPanel.Scale;
            var actual = WindowPlacementMath.Restore(saved, monitors, 376 * targetScale, 450 * targetScale)!;
            AssertInsideWorkArea(actual);
            Assert.Equal(serialized, JsonSerializer.Serialize(saved));
        }

        Assert.Equal(originalBounds,
            WindowPlacementMath.Restore(saved, [internalPanel, external], 752, 900)!.Bounds);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(1.25)]
    [InlineData(1.5)]
    [InlineData(1.75)]
    [InlineData(2)]
    [InlineData(2.25)]
    [InlineData(2.5)]
    [InlineData(3)]
    public void PortraitDisplayAndTopTaskbarKeepWindowVisibleAtMixedDpi(double scale)
    {
        var initial = new DesktopMonitor("portrait", "DISPLAY2",
            new(-2160, -300, 2160, 3800), 2);
        var saved = WindowPlacementMath.Capture(new(-1000, 2200, 752, 900), initial);
        var portrait = initial with
        {
            WorkArea = new(-1080, -880, 1080, 1840),
            Scale = scale
        };
        var primary = new DesktopMonitor("primary", "DISPLAY1",
            new(0, 0, 2560, 1400), 1.25, true);
        // The caller has already constrained its layout to the available width.
        var width = Math.Min(376 * scale, portrait.WorkArea.Width);
        var height = Math.Min(680 * scale, portrait.WorkArea.Height);
        var actual = WindowPlacementMath.Restore(saved, [primary, portrait], width, height)!;

        Assert.True(actual.PreferredMonitorAvailable);
        Assert.Equal(portrait, actual.Monitor);
        AssertInsideWorkArea(actual);
        Assert.Equal(width, actual.Bounds.Width);
        Assert.Equal(height, actual.Bounds.Height);
    }

    [Fact]
    public void MonitorEnumerationOrderDoesNotAffectIdentityOrEdgeAnchors()
    {
        var savedMonitor = new DesktopMonitor("saved", "DISPLAY3",
            new(1920, -1000, 2560, 1400), 1.25);
        var saved = WindowPlacementMath.Capture(new(3980, -200, 500, 600), savedMonitor);
        Assert.True(saved.AnchorRight);
        Assert.True(saved.AnchorBottom);

        var relocated = savedMonitor with
        {
            DeviceName = "DISPLAY8",
            WorkArea = new(-2560, -1440, 2560, 1400),
            Scale = 1.75
        };
        var main = new DesktopMonitor("main", "DISPLAY3", new(0, 0, 1920, 1040), 1, true);
        var third = new DesktopMonitor("third", "DISPLAY2", new(1920, 0, 1280, 720), 1.5);
        DesktopMonitor[][] orders =
        [
            [main, relocated, third], [main, third, relocated],
            [relocated, main, third], [relocated, third, main],
            [third, main, relocated], [third, relocated, main]
        ];

        foreach (var order in orders)
        {
            var actual = WindowPlacementMath.Restore(saved, order, 700, 840)!;
            Assert.True(actual.PreferredMonitorAvailable);
            Assert.Equal(new DesktopRect(-700, -880, 700, 840), actual.Bounds);
        }
    }

    [Theory]
    [InlineData(3840, 2120, 1)]
    [InlineData(3840, 2120, 2)]
    [InlineData(2560, 1400, 1.25)]
    [InlineData(2560, 1400, 1.75)]
    [InlineData(1920, 1040, 1.5)]
    [InlineData(1366, 728, 1.25)]
    [InlineData(1280, 680, 2)]
    [InlineData(1024, 728, 1.5)]
    [InlineData(1080, 1880, 3)]
    [InlineData(720, 1240, 2.25)]
    public void WindowLayoutsFitPhysicalWorkAreaAcrossResolutionAndDpi(double width, double height, double scale)
    {
        var monitor = new DesktopMonitor("test", "DISPLAY2", new(-width, -height, width, height), scale);
        // Widget, settings, wallpaper, and login windows exercise different
        // minimum sizes; even their largest fixed minimum must yield to a small screen.
        (double Width, double Height, double MinWidth, double MinHeight)[] windows =
        [
            (376, 900, 304, 1), (560, 680, 440, 360),
            (980, 790, 800, 580), (1180, 760, 640, 480)
        ];
        foreach (var window in windows)
        {
            var fit = DisplayLayoutMath.Fit(monitor, window.Width, window.Height, window.MinWidth, window.MinHeight);
            AssertValidFit(fit);
            Assert.True(fit.MaximumWidth * scale <= width);
            Assert.True(fit.MaximumHeight * scale <= height);
            var centered = DisplayLayoutMath.Center(monitor.WorkArea, fit.Width * scale, fit.Height * scale);
            AssertInsideWorkArea(new(monitor, centered, true));
        }
    }

    [Fact]
    public void FitPreservesComfortableWindowSizeOnLargeMonitor()
    {
        var monitor = new DesktopMonitor("4k", "DISPLAY1", new(0, 0, 3840, 2120), 2);
        var fit = DisplayLayoutMath.Fit(monitor, 560, 680, 440, 360);
        Assert.Equal(new WindowFit(560, 680, 440, 360, 1904, 1044), fit);
    }

    [Fact]
    public void MinimumWindowSizeYieldsToSmallerWorkArea()
    {
        var monitor = new DesktopMonitor("small", "DISPLAY1", new(1920, 40, 800, 560), 2);
        var fit = DisplayLayoutMath.Fit(monitor, 980, 790, 800, 580);
        Assert.Equal(new WindowFit(384, 264, 384, 264, 384, 264), fit);
    }

    [Fact]
    public void TooSmallPreferredSizeStillHonorsUsableMinimum()
    {
        var monitor = new DesktopMonitor("main", "DISPLAY1", new(0, 0, 1920, 1040));
        var fit = DisplayLayoutMath.Fit(monitor, 100, 100, 440, 360);
        Assert.Equal(440, fit.Width);
        Assert.Equal(360, fit.Height);
        AssertValidFit(fit);
    }

    [Fact]
    public void ExtremelySmallWorkAreaReducesMarginInsteadOfMakingNegativeSize()
    {
        var monitor = new DesktopMonitor("tiny", "DISPLAY1", new(-80, -60, 80, 60));
        var fit = DisplayLayoutMath.Fit(monitor, 560, 680, 440, 360, margin: 20);
        Assert.Equal(new WindowFit(72, 54, 72, 54, 72, 54), fit);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-20)]
    public void NonpositiveMarginAllowsFullWorkArea(double margin)
    {
        var monitor = new DesktopMonitor("small", "DISPLAY1", new(0, 0, 800, 600), 2);
        var fit = DisplayLayoutMath.Fit(monitor, 980, 790, 800, 580, margin);
        Assert.Equal(new WindowFit(400, 300, 400, 300, 400, 300), fit);
    }

    [Theory]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(double.NegativeInfinity)]
    public void NonfiniteMarginUsesSafeDefault(double margin)
    {
        var monitor = new DesktopMonitor("small", "DISPLAY1", new(0, 0, 800, 600), 2);
        var fit = DisplayLayoutMath.Fit(monitor, 980, 790, 800, 580, margin);
        Assert.Equal(new WindowFit(384, 284, 384, 284, 384, 284), fit);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(.2)]
    [InlineData(17)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    public void InvalidScaleFallsBackToOneWithoutInvalidConstraints(double scale)
    {
        var monitor = new DesktopMonitor("bad-scale", "DISPLAY1", new(0, 0, 800, 600), scale);
        var fit = DisplayLayoutMath.Fit(monitor, 980, 790, 800, 580);
        Assert.Equal(new WindowFit(784, 584, 784, 580, 784, 584), fit);
        AssertValidFit(fit);
    }

    [Fact]
    public void InvalidPreferredAndMinimumDimensionsProduceFinitePositiveLayout()
    {
        var monitor = new DesktopMonitor("main", "DISPLAY1", new(0, 0, 1920, 1040));
        var fit = DisplayLayoutMath.Fit(monitor, double.NaN, double.PositiveInfinity, -440, double.NaN);
        Assert.Equal(1, fit.Width);
        Assert.Equal(1, fit.Height);
        AssertValidFit(fit);
    }

    [Fact]
    public void InvalidWorkAreaDimensionsHaveNonzeroFallback()
    {
        var monitor = new DesktopMonitor("transient", "DISPLAY1",
            new(0, 0, double.NaN, double.NegativeInfinity), 1.5);
        var fit = DisplayLayoutMath.Fit(monitor, 560, 680, 440, 360);
        Assert.Equal(new WindowFit(1, 1, 1, 1, 1, 1), fit);
    }

    [Fact]
    public void CenterUsesNegativeMonitorOriginAndReservesEqualSpace()
    {
        var area = new DesktopRect(-2560, -900, 2560, 1400);
        Assert.Equal(new DesktopRect(-1580, -575, 600, 750),
            DisplayLayoutMath.Center(area, 600, 750));
    }

    [Fact]
    public void CenterOfOversizeWindowKeepsTopLeftControlsReachable()
    {
        var area = new DesktopRect(-1280, 40, 1280, 680);
        Assert.Equal(new DesktopRect(-1280, 40, 1800, 1000),
            DisplayLayoutMath.Center(area, 1800, 1000));
    }

    [Fact]
    public void CenterWithInvalidDimensionsDoesNotLeakNonfiniteCoordinates()
    {
        var area = new DesktopRect(-1280, 40, 1280, 680);
        Assert.Equal(new DesktopRect(-1280, 40, 1, 1),
            DisplayLayoutMath.Center(area, double.NaN, double.PositiveInfinity));
    }

    private static void AssertValidFit(WindowFit fit)
    {
        Assert.True(double.IsFinite(fit.MaximumWidth));
        Assert.True(double.IsFinite(fit.MaximumHeight));
        Assert.InRange(fit.MinimumWidth, 1, fit.MaximumWidth);
        Assert.InRange(fit.MinimumHeight, 1, fit.MaximumHeight);
        Assert.InRange(fit.Width, fit.MinimumWidth, fit.MaximumWidth);
        Assert.InRange(fit.Height, fit.MinimumHeight, fit.MaximumHeight);
    }

    private static void AssertInsideWorkArea(PlacementResult actual)
    {
        var area = actual.Monitor.WorkArea;
        Assert.InRange(actual.Bounds.X, area.X, area.Right - actual.Bounds.Width);
        Assert.InRange(actual.Bounds.Y, area.Y, area.Bottom - actual.Bounds.Height);
    }
}
