using System.Text.Json;
using Xunit;
using ZzzBattery.Core;

namespace ZzzBattery.Tests;

public sealed class WindowPlacementTests
{
    private static DesktopMonitor PrimaryMonitor(double scale = 1) =>
        new("monitor-main", @"\\.\DISPLAY1", new(0, 0, 1920, 1040), scale, true);

    [Theory]
    [InlineData(1)]
    [InlineData(1.25)]
    [InlineData(1.5)]
    [InlineData(1.75)]
    [InlineData(2)]
    public void RepeatedCaptureRestoreNeverAccumulatesPixelDrift(double scale)
    {
        var monitor = PrimaryMonitor(scale);
        var desired = new DesktopRect(1133, 217, 503, 611);
        var saved = WindowPlacementMath.Capture(desired, monitor);
        for (var count = 0; count < 1000; count++)
        {
            var actual = WindowPlacementMath.Restore(saved, [monitor], desired.Width, desired.Height)!;
            Assert.True(actual.PreferredMonitorAvailable);
            Assert.Equal(desired, actual.Bounds);
            saved = WindowPlacementMath.Capture(actual.Bounds, monitor);
        }
    }

    [Fact]
    public void NegativeDesktopCoordinatesAndMonitorOffsetsArePreserved()
    {
        var monitor = new DesktopMonitor("left", @"\\.\DISPLAY2", new(-2560, -360, 2560, 1400), 1.25);
        var desired = new DesktopRect(-2013, -212, 600, 850);
        var saved = WindowPlacementMath.Capture(desired, monitor);
        var actual = WindowPlacementMath.Restore(saved, [PrimaryMonitor(), monitor], desired.Width, desired.Height)!;
        Assert.Equal(monitor, actual.Monitor);
        Assert.Equal(desired, actual.Bounds);
    }

    [Fact]
    public void NewDpiScalesTheUserOffsetInsteadOfGlobalScreenCoordinates()
    {
        var oldMonitor = new DesktopMonitor("right", "display", new(1920, 0, 1920, 1040), 1);
        var saved = WindowPlacementMath.Capture(new(2020, 120, 400, 500), oldMonitor);
        var changed = oldMonitor with { WorkArea = new(1920, 0, 2560, 1400), Scale = 1.5 };
        var actual = WindowPlacementMath.Restore(saved, [PrimaryMonitor(), changed], 600, 750)!;
        Assert.Equal(new DesktopRect(2070, 180, 600, 750), actual.Bounds);
    }

    [Fact]
    public void MonitorMovementAndTaskbarChangesUseItsCurrentWorkAreaOrigin()
    {
        var oldMonitor = PrimaryMonitor();
        var saved = WindowPlacementMath.Capture(new(210, 110, 400, 500), oldMonitor);
        var moved = oldMonitor with { WorkArea = new(-1920, 42, 1920, 1038) };
        var actual = WindowPlacementMath.Restore(saved, [moved], 400, 500)!;
        Assert.Equal(new DesktopRect(-1710, 152, 400, 500), actual.Bounds);
    }

    [Fact]
    public void ExactRightAndBottomEdgesStayAnchoredWhenResolutionOrSizeChanges()
    {
        var monitor = PrimaryMonitor();
        var saved = WindowPlacementMath.Capture(new(1420, 440, 500, 600), monitor);
        Assert.True(saved.AnchorRight);
        Assert.True(saved.AnchorBottom);
        var changed = monitor with { WorkArea = new(0, 0, 2560, 1400), Scale = 1.5 };
        var actual = WindowPlacementMath.Restore(saved, [changed], 750, 900)!;
        Assert.Equal(new DesktopRect(1810, 500, 750, 900), actual.Bounds);
    }

    [Fact]
    public void OnePixelEdgeOffsetIsPreservedRatherThanSilentlySnapped()
    {
        var monitor = PrimaryMonitor(1.25);
        var desired = new DesktopRect(1419, 439, 500, 600);
        var saved = WindowPlacementMath.Capture(desired, monitor);
        Assert.True(saved.AnchorRight);
        Assert.True(saved.AnchorBottom);
        Assert.Equal(desired, WindowPlacementMath.Restore(saved, [monitor], 500, 600)!.Bounds);
    }

    [Fact]
    public void PositionNearAnEdgeIsNotAutomaticallyReclassifiedAsAnchored()
    {
        var monitor = PrimaryMonitor();
        var saved = WindowPlacementMath.Capture(new(1405, 425, 500, 600), monitor);
        Assert.False(saved.AnchorRight);
        Assert.False(saved.AnchorBottom);
        var changed = monitor with { WorkArea = new(0, 0, 2560, 1400) };
        Assert.Equal(new DesktopRect(1405, 425, 500, 600),
            WindowPlacementMath.Restore(saved, [changed], 500, 600)!.Bounds);
    }

    [Fact]
    public void MissingMonitorFallsBackWithoutChangingTheSavedPreferenceAndReconnectRestoresIt()
    {
        var monitor = new DesktopMonitor("external", @"\\.\DISPLAY2", new(-1920, 0, 1920, 1040));
        var desired = new DesktopRect(-1500, 150, 400, 500);
        var saved = WindowPlacementMath.Capture(desired, monitor);
        var original = saved with { };
        var fallback = WindowPlacementMath.Restore(saved, [PrimaryMonitor()], 400, 500)!;
        Assert.False(fallback.PreferredMonitorAvailable);
        Assert.Equal(new DesktopRect(1492, 70, 400, 500), fallback.Bounds);
        Assert.Equal(original, saved);
        var restored = WindowPlacementMath.Restore(saved, [PrimaryMonitor(), monitor], 400, 500)!;
        Assert.True(restored.PreferredMonitorAvailable);
        Assert.Equal(desired, restored.Bounds);
    }

    [Fact]
    public void ReusedWindowsDisplayNameDoesNotImpersonateMissingHardware()
    {
        var preferred = new DesktopMonitor("original-panel", @"\\.\DISPLAY2", new(1920, 0, 1920, 1040));
        var saved = WindowPlacementMath.Capture(new(2020, 140, 400, 500), preferred);
        var other = preferred with { Id = "different-panel" };
        var actual = WindowPlacementMath.Restore(saved, [PrimaryMonitor(), other], 400, 500)!;
        Assert.False(actual.PreferredMonitorAvailable);
        Assert.Equal(PrimaryMonitor(), actual.Monitor);
    }

    [Fact]
    public void HardwareIdentitySurvivesDisplayNameRenumbering()
    {
        var monitor = PrimaryMonitor();
        var desired = new DesktopRect(110, 120, 400, 500);
        var saved = WindowPlacementMath.Capture(desired, monitor);
        var renamed = monitor with { DeviceName = @"\\.\DISPLAY7" };
        var actual = WindowPlacementMath.Restore(saved, [renamed], 400, 500)!;
        Assert.True(actual.PreferredMonitorAvailable);
        Assert.Equal(desired, actual.Bounds);
    }

    [Fact]
    public void DeviceNameFallbackIsAvailableOnlyWhenSavedHardwareIdentityWasUnavailable()
    {
        var monitor = PrimaryMonitor();
        var desired = new DesktopRect(110, 120, 400, 500);
        var saved = WindowPlacementMath.Capture(desired, monitor) with { MonitorId = "" };
        var actual = WindowPlacementMath.Restore(saved, [monitor], 400, 500)!;
        Assert.True(actual.PreferredMonitorAvailable);
        Assert.Equal(desired, actual.Bounds);
    }

    [Fact]
    public void TemporaryLargeContentDoesNotOverwriteThePositionForNormalContent()
    {
        var monitor = PrimaryMonitor();
        var normal = new DesktopRect(1300, 650, 400, 300);
        var saved = WindowPlacementMath.Capture(normal, monitor);
        var expanded = WindowPlacementMath.Restore(saved, [monitor], 800, 800)!;
        Assert.Equal(new DesktopRect(1120, 240, 800, 800), expanded.Bounds);
        Assert.Equal(normal, WindowPlacementMath.Restore(saved, [monitor], 400, 300)!.Bounds);
    }

    [Fact]
    public void TemporaryReducedWorkAreaDoesNotOverwritePreferredPosition()
    {
        var monitor = PrimaryMonitor();
        var desired = new DesktopRect(1410, 430, 500, 600);
        var saved = WindowPlacementMath.Capture(desired, monitor);
        var reduced = monitor with { WorkArea = new(0, 0, 1280, 720) };
        Assert.Equal(new DesktopRect(780, 120, 500, 600),
            WindowPlacementMath.Restore(saved, [reduced], 500, 600)!.Bounds);
        Assert.Equal(desired, WindowPlacementMath.Restore(saved, [monitor], 500, 600)!.Bounds);
    }

    [Fact]
    public void OversizeWindowKeepsTopLeftReachableWithoutInvertedClamp()
    {
        var area = new DesktopRect(-1280, -200, 1280, 720);
        var actual = WindowPlacementMath.Clamp(new(-500, 400, 2000, 1200), area, true);
        Assert.Equal(new DesktopRect(-1280, -200, 2000, 1200), actual);
    }

    [Fact]
    public void DefaultPositionUsesPrimaryMonitorAndItsScale()
    {
        var primary = new DesktopMonitor("primary", "display2", new(1920, 60, 2560, 1380), 1.5, true);
        var actual = WindowPlacementMath.Restore(null, [PrimaryMonitor() with { Primary = false }, primary], 600, 800)!;
        Assert.False(actual.PreferredMonitorAvailable);
        Assert.Equal(new DesktopRect(3838, 165, 600, 800), actual.Bounds);
    }

    [Fact]
    public void ClampingDoesNotSnapUntilExplicitlyRequested()
    {
        var area = PrimaryMonitor().WorkArea;
        var desired = new DesktopRect(1410, 9, 500, 600);
        Assert.Equal(desired, WindowPlacementMath.Clamp(desired, area));
        Assert.Equal(new DesktopRect(1420, 0, 500, 600), WindowPlacementMath.Clamp(desired, area, true));
        var outsideSnap = new DesktopRect(1403, 17, 500, 600);
        Assert.Equal(outsideSnap, WindowPlacementMath.Clamp(outsideSnap, area, true));
    }

    [Fact]
    public void CorruptOrUnknownSettingsSafelyUseTheDefault()
    {
        var valid = WindowPlacementMath.Capture(new(120, 150, 400, 500), PrimaryMonitor());
        WindowPlacement?[] invalid =
        [
            null, valid with { Version = 0 }, valid with { Version = 2 },
            valid with { MonitorId = null! }, valid with { DeviceName = null! },
            valid with { MonitorId = "", DeviceName = "" },
            valid with { SavedWorkArea = null! },
            valid with { SavedWorkArea = new(0, 0, 0, 100) },
            valid with { SavedWorkArea = new(double.NaN, 0, 100, 100) },
            valid with { SavedWorkArea = new(0, 0, double.PositiveInfinity, 100) },
            valid with { SavedScale = 0 }, valid with { SavedScale = -1 },
            valid with { SavedScale = double.NaN }, valid with { SavedScale = double.PositiveInfinity },
            valid with { SavedScale = 17 }, valid with { OffsetX = double.NaN },
            valid with { OffsetY = double.NegativeInfinity }, valid with { OffsetX = double.MaxValue },
            valid with { DeviceName = new string('a', 4097) }
        ];
        foreach (var saved in invalid)
        {
            Assert.False(WindowPlacementMath.IsValid(saved));
            var actual = WindowPlacementMath.Restore(saved, [PrimaryMonitor()], 400, 500)!;
            Assert.False(actual.PreferredMonitorAvailable);
            Assert.Equal(new DesktopRect(1492, 70, 400, 500), actual.Bounds);
        }
    }

    [Fact]
    public void StoredPlacementSurvivesJsonRoundTrip()
    {
        var monitor = PrimaryMonitor(1.75);
        var desired = new DesktopRect(1133, 217, 503, 611);
        var saved = WindowPlacementMath.Capture(desired, monitor);
        var read = JsonSerializer.Deserialize<WindowPlacement>(JsonSerializer.Serialize(saved));
        Assert.True(WindowPlacementMath.IsValid(read));
        Assert.Equal(desired, WindowPlacementMath.Restore(read, [monitor], 503, 611)!.Bounds);
    }

    [Fact]
    public void MalformedDeserializedSettingsCannotSupplyAUsablePosition()
    {
        var saved = JsonSerializer.Deserialize<WindowPlacement>("""{"Version":1,"OffsetX":100,"OffsetY":100}""");
        Assert.False(WindowPlacementMath.IsValid(saved));
        Assert.Equal(new DesktopRect(1492, 70, 400, 500),
            WindowPlacementMath.Restore(saved, [PrimaryMonitor()], 400, 500)!.Bounds);
    }

    [Fact]
    public void InvalidMonitorEntriesAreIgnoredAndEmptyEnumerationIsSafe()
    {
        var invalid = PrimaryMonitor() with { Scale = double.NaN };
        Assert.Null(WindowPlacementMath.Restore(null, [], 400, 500));
        Assert.Null(WindowPlacementMath.Restore(null, [invalid], 400, 500));
        Assert.Null(WindowPlacementMath.Restore(null, null!, 400, 500));
        Assert.Equal(PrimaryMonitor(), WindowPlacementMath.Restore(null, [null!, invalid, PrimaryMonitor()], 400, 500)!.Monitor);
    }

    [Theory]
    [InlineData(0, 400)]
    [InlineData(400, -1)]
    [InlineData(double.NaN, 400)]
    [InlineData(400, double.PositiveInfinity)]
    [InlineData(1_000_001, 400)]
    public void InvalidWindowDimensionsDoNotReachNativePlacement(double width, double height) =>
        Assert.Null(WindowPlacementMath.Restore(null, [PrimaryMonitor()], width, height));

    [Fact]
    public void InvalidCaptureProducesAnInvalidMarkerAndClampNeverLeaksNonfiniteCoordinates()
    {
        Assert.False(WindowPlacementMath.IsValid(
            WindowPlacementMath.Capture(new(double.NaN, 0, 400, 500), PrimaryMonitor())));
        Assert.False(WindowPlacementMath.IsValid(WindowPlacementMath.Capture(new(0, 0, 400, 500), null!)));
        Assert.Equal(new DesktopRect(0, 0, 400, 500),
            WindowPlacementMath.Clamp(new(double.NaN, double.PositiveInfinity, 400, 500), PrimaryMonitor().WorkArea));
        Assert.Equal(new DesktopRect(0, 0, 1, 1),
            WindowPlacementMath.Clamp(new(double.NaN, 0, 0, 0), null!));
    }
}
