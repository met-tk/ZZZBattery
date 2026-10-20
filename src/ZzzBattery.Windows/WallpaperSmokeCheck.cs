using System;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using ZzzBattery.Core;

namespace ZzzBattery.Windows;

internal static class WallpaperSmokeCheck
{
    public static async Task RunAsync(App app, MainWindow window)
    {
        var result = await app.Wallpapers.Catalog.RefreshAsync(true);
        var catalog = result.Catalog;
        if (catalog.Wallpapers.Count < 3) throw new Exception("No wallpaper catalog available.");
        app.Settings.CharacterStyle = true; app.Settings.Mini = false; app.Settings.Expanded = false;
        app.Settings.Wallpaper = catalog.Wallpapers.First(w => w.Id == "1581_0");
        window.ApplySettings(); await window.ApplyWallpaperAsync();
        await Task.Delay(150);
        Capture(window, Path.Combine(app.Store.Root, "character-first-run.png"));
        app.Demo = true; window.Render(); await Task.Delay(100);
        Capture(window, Path.Combine(app.Store.Root, "character-pink.png"));

        // Prime the visible rows, not the entire remote image library.
        await Task.WhenAll(catalog.Wallpapers.Take(15).Select(w => app.Wallpapers.GetImageAsync(w.ImageUrl, 360)));
        var picker = new WallpaperWindow(); picker.Show();
        try
        {
            await Task.Delay(450); picker.UpdateLayout(); picker.LoadVisibleThumbnails();
            await Task.Delay(300);
            Capture(picker, Path.Combine(app.Store.Root, "wallpaper-picker.png"));
            picker.FilterForCheck("203", "2", "安比");
            if (picker.VisibleCount == 0) throw new Exception("Attribute/profession/search combination lost Anby.");
            await Task.Delay(100); Capture(picker, Path.Combine(app.Store.Root, "wallpaper-filtered.png"));
            picker.FilterForCheck(null, null, "this-does-not-exist-zzzbattery");
            if (picker.VisibleCount != 0) throw new Exception("Empty search still shows wallpapers.");
            await Task.Delay(100); Capture(picker, Path.Combine(app.Store.Root, "wallpaper-empty.png"));
            picker.FilterForCheck(null, null, "");
            await picker.SelectForCheck("1011_0"); await window.ApplyWallpaperAsync(); await Task.Delay(100);
            Capture(window, Path.Combine(app.Store.Root, "character-anby.png"));
            var saved = app.Store.Read<WidgetSettings>("settings.json");
            if (saved?.Wallpaper?.Id != "1011_0" || !saved.CharacterStyle || saved.Mini) throw new Exception("Wallpaper selection was not persisted.");
            // The catalog and image must reopen from disk without another request.
            using var reopened = new WallpaperService(app.Store.Root, new OfflineHandler());
            if (reopened.Resolve(saved.Wallpaper).Id != "1011_0" || await reopened.GetImageAsync(saved.Wallpaper.ImageUrl) is null)
                throw new Exception("Offline wallpaper reopen failed.");
            if (!result.Offline)
            {
                var downloaded = catalog.Wallpapers.First(w => w.Id == "1031_3110311");
                if (await reopened.GetImageAsync(downloaded.ImageUrl) is null) throw new Exception("Downloaded costume did not reopen without network.");
                await picker.SelectForCheck(downloaded.Id);
                if (app.Store.Read<WidgetSettings>("settings.json")?.Wallpaper?.Id != downloaded.Id)
                    throw new Exception("Non-default costume selection was not saved.");
            }
            await picker.SelectForCheck("1581_0"); await window.ApplyWallpaperAsync();
            app.Settings.Mini = true; window.ApplySettings(); await Task.Delay(100);
            Capture(window, Path.Combine(app.Store.Root, "character-mini.png"));
            app.Settings.Mini = false;
        }
        finally { picker.Close(); }
        File.WriteAllText(Path.Combine(app.Store.Root, "wallpaper-smoke-result.json"), JsonSerializer.Serialize(new
        {
            passed = true, onlineCatalog = !result.Offline, wallpapers = catalog.Wallpapers.Count,
            elements = catalog.Elements.Count, professions = catalog.Professions.Count,
            filtering = true, emptyState = true, previewAndApply = true, selectionPersisted = true, offlineReopen = true
        }, new JsonSerializerOptions { WriteIndented = true }));
    }
    private sealed class OfflineHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            throw new HttpRequestException("Network deliberately disabled for offline reopen check.");
    }
    private static void Capture(Window window, string path)
    {
        window.UpdateLayout();
        var bitmap = new RenderTargetBitmap((int)Math.Ceiling(window.ActualWidth), (int)Math.Ceiling(window.ActualHeight), 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(window);
        var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var output = File.Create(path); encoder.Save(output);
    }
}
