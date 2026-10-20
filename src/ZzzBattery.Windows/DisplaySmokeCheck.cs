using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using ZzzBattery.Core;

namespace ZzzBattery.Windows;

internal static class DisplaySmokeCheck
{
    public static async Task RunAsync(App app, MainWindow widget)
    {
        var report = new List<object>();
        try
        {
            Require(app.Session.Credential is null, "Display smoke must use an isolated account-free profile.");
            app.Demo = true; app.Settings.CharacterStyle = true; app.Settings.Mini = false;
            widget.ApplySettings(); await widget.ApplyWallpaperAsync(); await Task.Delay(150);
            var monitors = DesktopIntegration.GetMonitors(widget);
            Require(monitors.Count > 0, "No display available.");
            foreach (var monitor in monitors)
            {
                widget.PlacementForCheck.BeginUserMove();
                DesktopIntegration.MoveWindow(widget, monitor.WorkArea.X + 24 * monitor.Scale, monitor.WorkArea.Y + 24 * monitor.Scale);
                widget.PlacementForCheck.EndUserMove();
                await Task.Delay(150); widget.UpdateLayout();
                var current = DesktopIntegration.MonitorFor(widget)!;
                Require(current.DeviceName == monitor.DeviceName, "Widget failed to move to the target monitor.");
                var saved = JsonSerializer.Serialize(app.Settings.Placement);
                var settings = new SettingsWindow { Owner = widget };
                settings.Show(); await Task.Delay(120); settings.UpdateLayout();
                var actual = DesktopIntegration.MonitorFor(settings)!;
                Require(actual.DeviceName == monitor.DeviceName, "Settings opened on the wrong display.");
                RequireContained(settings, actual);
                settings.Close();
                Require(saved == JsonSerializer.Serialize(app.Settings.Placement), "Opening a dialog changed widget placement.");
                report.Add(new { actualDisplay = true, scale = actual.Scale, width = actual.WorkArea.Width, height = actual.WorkArea.Height, originDisplay = true });
            }

            // Real WPF layouts with synthetic logical work areas; this does not
            // claim to change physical monitor DPI or Windows display settings.
            var real = DesktopIntegration.MonitorFor(widget)!;
            var tiny = real with { WorkArea = new(real.WorkArea.X, real.WorkArea.Y, 640 * real.Scale, 480 * real.Scale) };
            var narrow = real with { WorkArea = new(real.WorkArea.X, real.WorkArea.Y, 440 * real.Scale, 500 * real.Scale) };
            var widgetScreen = real with { WorkArea = new(real.WorkArea.X, real.WorkArea.Y, 280 * real.Scale, 460 * real.Scale) };
            var widgetSaved = JsonSerializer.Serialize(app.Settings.Placement);
            foreach (var mode in new[] { "character", "classic", "mini" })
            {
                app.Settings.Mini = mode == "mini"; app.Settings.CharacterStyle = mode == "character";
                app.Settings.Expanded = mode != "mini"; widget.ApplySettings();
                widget.PlacementForCheck.FitDisplayForCheck(widgetScreen); widget.UpdateLayout();
                Require(widget.ActualWidth <= 264.5 && widget.ActualHeight <= 444.5, "Narrow widget exceeds available logical work area.");
                Require(widget.WidgetContent.ActualWidth >= 304, "Readable layout base was overwritten by the temporary clamp.");
                Capture(widget, app, "narrow-widget-" + mode);
                Require(widgetSaved == JsonSerializer.Serialize(app.Settings.Placement), "Temporary width clamp overwrote user placement.");
            }
            widget.PlacementForCheck.FitDisplayForCheck(real);

            var windows = new Window[]
            {
                new SettingsWindow(), new QrLoginWindow(preview: true),
                new VerificationWindow(new Credential(), null, loadPage: false),
                new AccountWindow(loadPage: false),
                new DailySignWindow(new Credential()),
                new WallpaperWindow(loadCatalog: false)
            };
            foreach (var window in windows)
            {
                window.Owner = widget; window.Show(); await Task.Delay(80);
                var controller = AdaptiveWindow.Attach(window);
                controller.FitForCheck(tiny); window.UpdateLayout();
                Require(window.ActualWidth <= 624.5 && window.ActualHeight <= 464.5, window.GetType().Name + " exceeds small work area.");
                Require(window.MinWidth <= window.MaxWidth && window.MinHeight <= window.MaxHeight, "Minimum size overrides display limits.");
                var outer = (ScrollViewer)window.Content;
                outer.ScrollToBottom(); window.UpdateLayout();
                Require(outer.ScrollableHeight <= 0 || outer.VerticalOffset > 0, "Overflow is not reachable.");
                Capture(window, app, "small-" + window.GetType().Name);
                outer.ScrollToTop();
                controller.FitForCheck(narrow); window.UpdateLayout();
                Require(window.ActualWidth <= 424.5, "Narrow dialog remains wider than work area.");
                if (window is WallpaperWindow picker)
                {
                    var toggle = (Button)picker.FindName("PreviewToggle");
                    Require(toggle.IsVisible, "Narrow wallpaper picker has no preview switch.");
                    Require(((Grid)picker.FindName("GalleryPane")).IsVisible, "Wallpaper list missing.");
                    toggle.RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); picker.UpdateLayout();
                    Require(((Border)picker.FindName("PreviewPane")).IsVisible, "Narrow preview unavailable.");
                    var apply = (Button)picker.FindName("ApplyButton");
                    apply.BringIntoView(); picker.UpdateLayout();
                    Capture(picker, app, "narrow-wallpaper-preview");
                    toggle.RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); picker.UpdateLayout();
                    Require(((Grid)picker.FindName("GalleryPane")).IsVisible, "Returning to wallpaper list failed.");
                }
                Capture(window, app, "narrow-" + window.GetType().Name);
                controller.FitForCheck(real); window.UpdateLayout();
                RequireContained(window, real);
                window.Close();
            }
            app.Store.Write("display-smoke-result.json", new
            {
                passed = true, actualMonitors = report, syntheticLayouts = new[] { "640x480 DIP", "440x500 DIP", "280x460 DIP widget" },
                widgetWidthFit = true, dialogOverflowReachable = true, wallpaperCompact = true,
                currentDisplayPlacement = true, originalPlacementPreserved = true,
                changedWindowsDisplaySettings = false, authenticatedRequests = 0
            });
        }
        catch (Exception error)
        {
            app.Store.Write("display-smoke-result.json", new { passed = false, actualMonitors = report, error = error.ToString() });
        }
        finally { app.Quit(); }
    }

    private static void RequireContained(Window window, DesktopMonitor monitor)
    {
        var bounds = DesktopIntegration.GetWindowBounds(window)!;
        Require(bounds.X >= monitor.WorkArea.X - 1 && bounds.Y >= monitor.WorkArea.Y - 1 &&
            bounds.Right <= monitor.WorkArea.Right + 1 && bounds.Bottom <= monitor.WorkArea.Bottom + 1,
            window.GetType().Name + " native bounds are outside work area.");
    }
    private static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
    private static void Capture(Window window, App app, string name)
    {
        var bitmap = new RenderTargetBitmap((int)Math.Ceiling(window.ActualWidth), (int)Math.Ceiling(window.ActualHeight), 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(window);
        var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var output = File.Create(Path.Combine(app.Store.Root, name + ".png")); encoder.Save(output);
    }
}
