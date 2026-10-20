using System.Net;
using System.Text.Json;

namespace ZzzBattery.Core;

public sealed record WidgetWallpaper(string AvatarId, string SkinId, string Name, string ImageUrl,
    string Color, string ElementId, string ProfessionId)
{
    public string Id => AvatarId + "_" + SkinId;
    public bool Matches(string? element, string? profession, string? search) =>
        (string.IsNullOrEmpty(element) || ElementId == element) &&
        (string.IsNullOrEmpty(profession) || ProfessionId == profession) &&
        (string.IsNullOrWhiteSpace(search) || Name.Contains(search.Trim(), StringComparison.OrdinalIgnoreCase));
}

public sealed record WallpaperFilter(string Id, string IconUrl, string SelectedIconUrl);
public sealed record WallpaperCatalog(IReadOnlyList<WidgetWallpaper> Wallpapers,
    IReadOnlyList<WallpaperFilter> Elements, IReadOnlyList<WallpaperFilter> Professions);
public sealed record WallpaperCatalogCache(DateTimeOffset FetchedAt, string Response);
public sealed record WallpaperCatalogResult(WallpaperCatalog Catalog, DateTimeOffset? FetchedAt,
    bool Offline, string? Error);

// This endpoint is public. Keep it separate from the account client: never send Cookie or DS.
public sealed class WallpaperCatalogClient(HttpClient http)
{
    public const string Endpoint = "https://api-takumi-record.mihoyo.com/event/game_record_zzz/api/zzz/get_widget_avatar_background";
    public const string AppVersion = "2.114.0"; // AndroidManifest.xml in the supplied APK.

    public async Task<string> FetchAsync(CancellationToken cancellation = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, Endpoint);
        request.Headers.Add("x-rpc-app_version", AppVersion);
        request.Headers.Add("x-rpc-client_type", "2");
        request.Headers.Add("x-rpc-language", "zh-cn");
        request.Headers.Accept.ParseAdd("application/json");
        using var response = await http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellation);
        if (response.StatusCode == HttpStatusCode.TooManyRequests)
            throw new HttpRequestException("壁纸服务繁忙，请稍后更新。");
        response.EnsureSuccessStatusCode();
        var bytes = await ReadBoundedAsync(response.Content, 2_000_000, cancellation);
        var json = System.Text.Encoding.UTF8.GetString(bytes);
        Parse(json); // A bad/empty response must never replace the working catalog.
        return json;
    }

    public static WallpaperCatalog Parse(string json)
    {
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        if (JsonValue.Int(root, "retcode") != 0) throw new JsonException("壁纸接口未返回成功数据。");
        var data = JsonValue.Object(root, "data");
        var wallpapers = new List<WidgetWallpaper>();
        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (var key in new[] { "avatar_skin_list_v2", "avatar_skin_list" })
        {
            if (data.TryGetProperty(key, out var array) && array.ValueKind == JsonValueKind.Array)
                foreach (var item in array.EnumerateArray().Take(2000))
                {
                    var skin = new WidgetWallpaper(JsonValue.String(item, "avatar_id"), JsonValue.String(item, "skin_id"),
                        JsonValue.String(item, "skin_name"), JsonValue.String(item, "skin_background_url"),
                        JsonValue.String(item, "color"), JsonValue.String(item, "element_type"), JsonValue.String(item, "avatar_profession"));
                    if (IsValid(skin) && ids.Add(skin.Id)) wallpapers.Add(skin);
                }
            if (wallpapers.Count > 0) break; // Prefer V2 artwork; legacy is a compatibility fallback.
        }
        if (wallpapers.Count == 0) throw new JsonException("壁纸列表为空或格式已变化。");
        IReadOnlyList<WallpaperFilter> Filters(string key)
        {
            var result = new List<WallpaperFilter>();
            if (data.TryGetProperty(key, out var list) && list.ValueKind == JsonValueKind.Array)
                foreach (var item in list.EnumerateArray().Take(100))
                {
                    var filter = new WallpaperFilter(JsonValue.String(item, "id"), JsonValue.String(item, "icon_url"), JsonValue.String(item, "select_icon_url"));
                    if (filter.Id.Length > 0 && IsAssetUrl(filter.IconUrl) && IsAssetUrl(filter.SelectedIconUrl) && !result.Any(f => f.Id == filter.Id))
                        result.Add(filter);
                }
            return result;
        }
        return new(wallpapers, Filters("element_type_list"), Filters("avatar_profession_list"));
    }

    public static bool IsValid(WidgetWallpaper? skin) => skin is not null &&
        IsId(skin.AvatarId) && IsId(skin.SkinId) && !string.IsNullOrWhiteSpace(skin.Name) && skin.Name.Length <= 120 &&
        IsAssetUrl(skin.ImageUrl) && skin.Color is { Length: 7 or 9 } && skin.Color[0] == '#' && skin.Color.AsSpan(1).IndexOfAnyExcept("0123456789abcdefABCDEF") < 0;
    private static bool IsId(string? id) => !string.IsNullOrEmpty(id) && id.Length <= 32 && id.All(char.IsAsciiDigit);
    public static bool IsAssetUrl(string? url) => Uri.TryCreate(url, UriKind.Absolute, out var uri) &&
        uri.Scheme == Uri.UriSchemeHttps && uri.IsDefaultPort && uri.UserInfo.Length == 0 &&
        (uri.Host.Equals("mihoyo.com", StringComparison.OrdinalIgnoreCase) || uri.Host.EndsWith(".mihoyo.com", StringComparison.OrdinalIgnoreCase));

    public static async Task<byte[]> ReadBoundedAsync(HttpContent content, int limit, CancellationToken cancellation)
    {
        if (content.Headers.ContentLength > limit) throw new IOException("资源超出大小限制。");
        await using var input = await content.ReadAsStreamAsync(cancellation);
        using var output = new MemoryStream();
        var buffer = new byte[16384];
        int count;
        while ((count = await input.ReadAsync(buffer, cancellation)) > 0)
        {
            if (output.Length + count > limit) throw new IOException("资源超出大小限制。");
            output.Write(buffer, 0, count);
        }
        return output.ToArray();
    }
}

public sealed class WallpaperCatalogStore
{
    public static readonly TimeSpan UpdateInterval = TimeSpan.FromHours(6);
    private readonly WallpaperCatalogClient _client;
    private readonly string _path;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly Func<DateTimeOffset> _now;
    private DateTimeOffset? _lastAttempt;
    public WallpaperCatalogResult Current { get; private set; }

    public WallpaperCatalogStore(WallpaperCatalogClient client, string directory, string bundledResponse, Func<DateTimeOffset>? now = null)
    {
        _client = client; _path = Path.Combine(directory, "wallpaper-catalog.json"); _now = now ?? (() => DateTimeOffset.UtcNow);
        Current = new(WallpaperCatalogClient.Parse(bundledResponse), null, true, null);
        try
        {
            if (File.Exists(_path) && new FileInfo(_path).Length < 3_000_000 &&
                JsonSerializer.Deserialize<WallpaperCatalogCache>(File.ReadAllText(_path)) is { } cached)
                Current = new(WallpaperCatalogClient.Parse(cached.Response), cached.FetchedAt, true, null);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or JsonException or ArgumentException) { }
    }

    public async Task<WallpaperCatalogResult> RefreshAsync(bool force = false, CancellationToken cancellation = default)
    {
        await _gate.WaitAsync(cancellation);
        try
        {
            var now = _now();
            if (_lastAttempt is { } attempt && now >= attempt && now - attempt < (Current.Error is null || force ? TimeSpan.FromMinutes(1) : TimeSpan.FromMinutes(30)))
                return Current;
            if (!force && Current.FetchedAt is { } fetched && now >= fetched && now - fetched < UpdateInterval) return Current;
            _lastAttempt = now;
            try
            {
                var response = await _client.FetchAsync(cancellation);
                var catalog = WallpaperCatalogClient.Parse(response);
                var stamp = _now();
                var warning = (string?)null;
                try
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
                    var temp = _path + "." + Guid.NewGuid().ToString("N") + ".tmp";
                    try
                    {
                        await File.WriteAllTextAsync(temp, JsonSerializer.Serialize(new WallpaperCatalogCache(stamp, response)), cancellation);
                        File.Move(temp, _path, true);
                    }
                    finally { if (File.Exists(temp)) File.Delete(temp); }
                }
                catch (Exception e) when (e is IOException or UnauthorizedAccessException) { warning = "列表已更新，但本地缓存无法保存。"; }
                Current = new(catalog, stamp, false, warning);
            }
            catch (OperationCanceledException) when (cancellation.IsCancellationRequested) { throw; }
            catch (Exception e) when (e is HttpRequestException or IOException or JsonException or OperationCanceledException)
            {
                Current = Current with { Offline = true, Error = "暂时无法更新，正在使用本地壁纸列表。" };
            }
            return Current;
        }
        finally { _gate.Release(); }
    }
}
