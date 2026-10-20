using System;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media.Imaging;
using ZzzBattery.Core;

namespace ZzzBattery.Windows;

public sealed class WallpaperService : IDisposable
{
    private readonly HttpClient _http;
    private readonly SemaphoreSlim _downloads = new(4);
    private readonly CancellationTokenSource _lifetime = new();
    private readonly ConcurrentDictionary<string, BitmapSource> _images = new();
    private readonly ConcurrentDictionary<string, Lazy<Task<BitmapSource?>>> _pending = new();
    private readonly string _directory;
    private readonly WallpaperCatalog _bundled;
    public WallpaperCatalogStore Catalog { get; }

    public WallpaperService(string root) : this(root, null) { }
    internal WallpaperService(string root, HttpMessageHandler? handler)
    {
        _http = new HttpClient(handler ?? new HttpClientHandler
        { AllowAutoRedirect = false, UseCookies = false, AutomaticDecompression = DecompressionMethods.All })
        { Timeout = TimeSpan.FromSeconds(20) };
        _directory = Path.Combine(root, "wallpapers");
        using var resource = Application.GetResourceStream(ResourceUri("catalog.json")).Stream;
        using var reader = new StreamReader(resource);
        var json = reader.ReadToEnd();
        _bundled = WallpaperCatalogClient.Parse(json);
        Catalog = new(new WallpaperCatalogClient(_http), root, json);
    }

    public WidgetWallpaper Resolve(WidgetWallpaper? selection) =>
        Catalog.Current.Catalog.Wallpapers.FirstOrDefault(w => w.Id == selection?.Id) ??
        (WallpaperCatalogClient.IsValid(selection) ? selection! :
            Catalog.Current.Catalog.Wallpapers.FirstOrDefault(w => w.Id == "1581_0") ?? Catalog.Current.Catalog.Wallpapers[0]);

    public BitmapSource? BundledImage(string url, int width = 720)
    {
        string? file = null;
        foreach (var id in new[] { "1581", "1011", "1091" })
            if (_bundled.Wallpapers.Any(s => s.AvatarId == id && s.SkinId == "0" && s.ImageUrl == url)) file = id + ".png";
        file ??= "seed-" + Hash(url) + ".png";
        try
        {
            using var stream = Application.GetResourceStream(ResourceUri(file))?.Stream;
            return stream is null ? null : Decode(stream, width);
        }
        catch (Exception e) when (e is IOException or NotSupportedException or ArgumentException) { return null; }
    }

    private static Uri ResourceUri(string file) => new("pack://application:,,,/Assets/Widget/" + file);
    private static string Hash(string url) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(url))).ToLowerInvariant();

    public async Task<BitmapSource?> GetImageAsync(string url, int width = 720, CancellationToken cancellation = default)
    {
        if (!WallpaperCatalogClient.IsAssetUrl(url)) return null;
        var key = url + "|" + width;
        if (_images.TryGetValue(key, out var hit)) return hit;
        var pending = _pending.GetOrAdd(key, _ => new Lazy<Task<BitmapSource?>>(() => LoadAsync(url, width, key)));
        return await pending.Value.WaitAsync(cancellation);
    }

    private async Task<BitmapSource?> LoadAsync(string url, int width, string key)
    {
        var token = _lifetime.Token;
        try
        {
            var bundled = BundledImage(url, width);
            if (bundled is not null) return Remember(key, bundled);
            await _downloads.WaitAsync(token);
            try
            {
                var path = Path.Combine(_directory, Hash(url) + ".image");
                if (File.Exists(path))
                {
                    try
                    {
                        if (new FileInfo(path).Length <= 12_000_000)
                        {
                            using var stream = File.OpenRead(path);
                            return Remember(key, Decode(stream, width));
                        }
                    }
                    catch (Exception e) when (e is IOException or NotSupportedException or ArgumentException or System.IO.FileFormatException) { }
                    File.Delete(path); // Only the URL-hashed file in this app's image cache.
                }
                using var response = await _http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, token);
                response.EnsureSuccessStatusCode();
                var bytes = await WallpaperCatalogClient.ReadBoundedAsync(response.Content, 12_000_000, token);
                using var input = new MemoryStream(bytes);
                var bitmap = Decode(input, width);
                try
                {
                    Directory.CreateDirectory(_directory);
                    var temp = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
                    try { await File.WriteAllBytesAsync(temp, bytes, token); File.Move(temp, path, true); }
                    finally { if (File.Exists(temp)) File.Delete(temp); }
                }
                catch (Exception e) when (e is IOException or UnauthorizedAccessException) { /* The downloaded image is still usable this session. */ }
                return Remember(key, bitmap);
            }
            finally { _downloads.Release(); }
        }
        catch (Exception e) when (e is HttpRequestException or IOException or UnauthorizedAccessException or
            NotSupportedException or ArgumentException or OperationCanceledException) { return null; }
        finally { _pending.TryRemove(key, out _); }
    }

    private BitmapSource Remember(string key, BitmapSource image)
    {
        if (_images.Count >= 256) _images.Clear();
        _images[key] = image;
        return image;
    }

    private static BitmapSource Decode(Stream stream, int width)
    {
        var bitmap = new BitmapImage();
        bitmap.BeginInit(); bitmap.CacheOption = BitmapCacheOption.OnLoad;
        bitmap.DecodePixelWidth = Math.Clamp(width, 48, 900); bitmap.StreamSource = stream; bitmap.EndInit();
        if (bitmap.PixelHeight > 4096 || bitmap.PixelWidth > 4096) throw new IOException("图片尺寸超出限制。");
        bitmap.Freeze(); return bitmap;
    }
    public void Dispose() { _lifetime.Cancel(); _http.Dispose(); }
}
