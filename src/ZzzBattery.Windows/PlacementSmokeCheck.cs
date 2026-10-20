using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using ZzzBattery.Core;

namespace ZzzBattery.Windows;

// Run seed and restore in separate processes with the same isolated --data-dir.
// No display settings, startup registry entries, or real account data are changed.
internal static class PlacementSmokeCheck
{
    private const string ExpectedFile = "placement-expected.json";
    private sealed record ExpectedPlacement(WindowPlacement Placement, double Left, double Top);

    public static async Task RunAsync(App app, MainWindow window, string phase)
    {
        var resultFile = phase == "seed" ? "placement-smoke-seed.json" : "placement-smoke-result.json";
        try
        {
            if (app.Session.Credential is not null)
                throw new InvalidOperationException("Placement checks require an isolated profile without an account.");
            if (phase == "seed")
                await SeedAsync(app, window);
            else if (phase == "restore")
                await RestoreAsync(app, window);
            else
                throw new ArgumentException("Unknown placement check phase.", nameof(phase));
        }
        catch (Exception error)
        {
            app.Store.Write(resultFile, new { passed = false, phase, error = error.ToString() });
        }
        finally
        {
            app.Quit();
        }
    }

    private static async Task SeedAsync(App app, MainWindow window)
    {
        await Task.Delay(300);
        app.Demo = false;
        app.Settings.Mini = true;
        app.Settings.Expanded = false;
        Require(app.SaveSettings(), "Mini settings were not saved.");
        await SettleAsync(window);
        var monitor = DesktopIntegration.MonitorFor(window)
            ?? throw new InvalidOperationException("No monitor contains the test window.");
        var bounds = Bounds(window);
        var work = monitor.WorkArea;
        // Start near the lower-right corner so larger layouts need temporary clamping.
        var left = Math.Max(work.X, work.Right - bounds.Width - 48);
        var top = Math.Max(work.Y, work.Bottom - bounds.Height - 56);
        window.PlacementForCheck.BeginUserMove();
        DesktopIntegration.MoveWindow(window, left, top);
        window.PlacementForCheck.EndUserMove();

        // Read synchronously and quit without yielding to a delayed position-save timer.
        var saved = app.Store.Read<WidgetSettings>("settings.json")?.Placement;
        Require(saved is not null, "Ending a user move did not persist the position immediately.");
        RequireSamePosition(Bounds(window), left, top, "Seed move");
        app.Store.Write(ExpectedFile, new ExpectedPlacement(saved!, left, top));
        app.Store.Write("placement-smoke-seed.json", new
        {
            passed = true,
            phase = "seed",
            immediatePersistence = true,
            savedBeforeExit = true
        });
    }

    private static async Task RestoreAsync(App app, MainWindow window)
    {
        var expected = app.Store.Read<ExpectedPlacement>(ExpectedFile)
            ?? throw new InvalidOperationException("Run the seed phase in this profile first.");
        await Task.Delay(500);
        RequireSamePosition(Bounds(window), expected.Left, expected.Top, "Next-process startup");
        RequireUnchangedPlacement(app, expected);

        // Clicking a draggable surface without moving it must not save a
        // temporarily displaced/clamped position as the user's preference.
        window.PlacementForCheck.BeginUserMove();
        window.PlacementForCheck.EndUserMove();
        RequireUnchangedPlacement(app, expected);

        // Demo data is confined to this test process and exposes real expanded-note layouts.
        app.Demo = true;
        for (var cycle = 0; cycle < 2; cycle++)
        {
            foreach (var mode in new[]
            {
                (Mini: false, Character: true, Expanded: false),
                (Mini: false, Character: true, Expanded: true),
                (Mini: false, Character: false, Expanded: true)
            })
            {
                app.Settings.Mini = mode.Mini;
                app.Settings.CharacterStyle = mode.Character;
                app.Settings.Expanded = mode.Expanded;
                Require(app.SaveSettings(), "Layout settings were not saved.");
                await SettleAsync(window);
                RequireUnchangedPlacement(app, expected);
                window.SetNotice("窗口位置回归检查：提示出现和内容高度变化不能覆盖用户保存的位置。\n这是一条仅用于隔离检查的多行提示。");
                await SettleAsync(window);
                RequireUnchangedPlacement(app, expected);
                window.SetNotice("");
                await SettleAsync(window);
                RequireUnchangedPlacement(app, expected);
            }
            await ReturnToMiniAsync(app, window, expected);
            window.Hide();
            await Task.Delay(100);
            window.Show();
            window.PlacementForCheck.QueueRestore();
            await SettleAsync(window);
            RequireUnchangedPlacement(app, expected);
            RequireSamePosition(Bounds(window), expected.Left, expected.Top, "Hide/show");
        }

        var original = Bounds(window);
        var monitor = DesktopIntegration.MonitorFor(window)
            ?? throw new InvalidOperationException("The test monitor disappeared.");
        var work = monitor.WorkArea;
        var displacedLeft = Math.Max(work.X, original.X - 96);
        var displacedTop = Math.Max(work.Y, original.Y - 112);
        Require(Math.Abs(displacedLeft - original.X) > 1 || Math.Abs(displacedTop - original.Y) > 1,
            "The current work area is too small for the displacement check.");
        // An automatic native move must not become a new user preference.
        DesktopIntegration.MoveWindow(window, displacedLeft, displacedTop);
        RequireSamePosition(Bounds(window), displacedLeft, displacedTop, "Automatic native move");
        RequireUnchangedPlacement(app, expected);
        window.PlacementForCheck.BeginUserMove();
        window.PlacementForCheck.EndUserMove();
        RequireUnchangedPlacement(app, expected);
        window.PlacementForCheck.QueueRestore();
        await SettleAsync(window);
        RequireSamePosition(Bounds(window), expected.Left, expected.Top, "Restore after automatic move");
        RequireUnchangedPlacement(app, expected);

        app.Store.Write("placement-smoke-result.json", new
        {
            passed = true,
            phase = "restore",
            separateProcessRestore = true,
            physicalPixelTolerance = 1,
            layoutChangesPreservePreference = true,
            noticesPreservePreference = true,
            miniReturnsToSavedPosition = true,
            hideShowPreservesPosition = true,
            automaticMoveDoesNotSave = true,
            stationaryClickDoesNotSave = true,
            explicitRestore = true,
            cycles = 2
        });
    }

    private static async Task ReturnToMiniAsync(App app, MainWindow window, ExpectedPlacement expected)
    {
        app.Demo = false;
        app.Settings.Mini = true;
        app.Settings.Expanded = false;
        window.SetNotice("");
        Require(app.SaveSettings(), "Mini settings were not saved.");
        await SettleAsync(window);
        RequireUnchangedPlacement(app, expected);
        RequireSamePosition(Bounds(window), expected.Left, expected.Top, "Return to Mini");
        app.Demo = true;
    }

    private static async Task SettleAsync(MainWindow window)
    {
        window.UpdateLayout();
        await Task.Delay(400);
        window.UpdateLayout();
    }

    private static DesktopRect Bounds(MainWindow window) => DesktopIntegration.GetWindowBounds(window)
        ?? throw new InvalidOperationException("Could not read the native window bounds.");

    private static void RequireUnchangedPlacement(App app, ExpectedPlacement expected)
    {
        var serialized = JsonSerializer.Serialize(expected.Placement);
        Require(JsonSerializer.Serialize(app.Settings.Placement) == serialized,
            "An automatic move or layout change overwrote the in-memory user position.");
        Require(JsonSerializer.Serialize(app.Store.Read<WidgetSettings>("settings.json")?.Placement) == serialized,
            "An automatic move or layout change overwrote the persisted user position.");
    }

    private static void RequireSamePosition(DesktopRect bounds, double left, double top, string stage)
    {
        Require(Math.Abs(bounds.X - left) <= 1 && Math.Abs(bounds.Y - top) <= 1,
            $"{stage}: expected physical position ({left:0}, {top:0}), got ({bounds.X:0}, {bounds.Y:0}).");
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
