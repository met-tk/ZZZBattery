using System.Net;
using System.Text;
using System.Text.Json;
using Xunit;
using ZzzBattery.Core;

namespace ZzzBattery.Tests;

public sealed class WallpaperTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "ZZZWallpaperTests_" + Guid.NewGuid().ToString("N"));
    private static readonly DateTimeOffset Start = new(2026, 10, 19, 10, 0, 0, TimeSpan.Zero);
    private const string ImageUrl = "https://act-webstatic.mihoyo.com/game_record/zzzv2/avatar.png";
    private static object Skin(object avatar, object skin, string name = "妮可·一点点俏皮", string color = "#e6adaa", string url = ImageUrl) =>
        new { avatar_id = avatar, skin_id = skin, skin_name = name, color, skin_background_url = url, element_type = 205, avatar_profession = "4" };
    private static string Response(object[]? v2 = null, object[]? v1 = null) => JsonSerializer.Serialize(new
    {
        retcode = 0,
        data = new
        {
            avatar_skin_list_v2 = v2 ?? new[] { Skin(1031, 0), Skin("1031", "3110311", "妮可·狡黠甜心") },
            avatar_skin_list = v1 ?? [],
            element_type_list = new[] { new { id = 205, icon_url = ImageUrl, select_icon_url = ImageUrl }, new { id = 999, icon_url = ImageUrl, select_icon_url = ImageUrl } },
            avatar_profession_list = new[] { new { id = "4", icon_url = ImageUrl, select_icon_url = ImageUrl } }
        }
    });

    [Fact]
    public void ParsesNumericAndStringIdsWithoutCollapsingCostumes()
    {
        var catalog = WallpaperCatalogClient.Parse(Response());
        Assert.Equal(new[] { "1031_0", "1031_3110311" }, catalog.Wallpapers.Select(s => s.Id));
        Assert.Equal("999", catalog.Elements[1].Id); // Future attributes remain selectable.
        Assert.True(catalog.Wallpapers[1].Matches("205", "4", " 狡黠 "));
        Assert.False(catalog.Wallpapers[1].Matches("200", "4", null));
        Assert.False(catalog.Wallpapers[1].Matches(null, "1", null));
    }
    [Fact]
    public void PrefersV2ArtworkAndFallsBackToLegacyWhenEmpty()
    {
        Assert.Equal(2, WallpaperCatalogClient.Parse(Response(v1: [Skin(1, 0)])).Wallpapers.Count);
        Assert.Equal("1_0", Assert.Single(WallpaperCatalogClient.Parse(Response([], [Skin(1, 0)])).Wallpapers).Id);
    }
    [Fact]
    public void SkipsBrokenEntriesAndDeduplicatesByCompositeId()
    {
        var result = WallpaperCatalogClient.Parse(Response([Skin(1, 0), Skin(1, 0), Skin(2, 0, color: "garbage"), Skin(3, 0, url: "file:///c:/private.png"), Skin("../", 0)]));
        Assert.Equal("1_0", Assert.Single(result.Wallpapers).Id);
    }
    [Theory]
    [InlineData("https://fastcdn.mihoyo.com/image.png", true)]
    [InlineData("https://act-webstatic.mihoyo.com/image.png", true)]
    [InlineData("http://act-webstatic.mihoyo.com/image.png", false)]
    [InlineData("https://mihoyo.com.evil.example/image.png", false)]
    [InlineData("https://user@fastcdn.mihoyo.com/image.png", false)]
    [InlineData("https://fastcdn.mihoyo.com:444/image.png", false)]
    public void AssetUrlsAreRestrictedToOfficialHttps(string url, bool expected) => Assert.Equal(expected, WallpaperCatalogClient.IsAssetUrl(url));
    [Fact]
    public async Task SendsVerifiedVersionHeadersWithoutAccountCredentials()
    {
        var handler = new Handler(request =>
        {
            Assert.Equal(WallpaperCatalogClient.Endpoint, request.RequestUri!.ToString());
            Assert.Equal("2.114.0", request.Headers.GetValues("x-rpc-app_version").Single());
            Assert.Equal("2", request.Headers.GetValues("x-rpc-client_type").Single());
            Assert.False(request.Headers.Contains("Cookie")); Assert.False(request.Headers.Contains("DS"));
            return Ok(Response());
        });
        using var http = new HttpClient(handler);
        await new WallpaperCatalogClient(http).FetchAsync();
    }
    [Fact]
    public async Task CacheSurvivesNetworkFailureAndInvalidServerResponse()
    {
        var now = Start;
        var handler = new Handler(_ => Ok(Response([Skin(10, 7, "新增壁纸")])));
        using var http = new HttpClient(handler);
        var store = new WallpaperCatalogStore(new(http), _directory, Response(), () => now);
        var fresh = await store.RefreshAsync();
        Assert.False(fresh.Offline); Assert.Equal("10_7", fresh.Catalog.Wallpapers.Single().Id);
        var persisted = File.ReadAllText(Path.Combine(_directory, "wallpaper-catalog.json"));
        handler.Reply = _ => throw new HttpRequestException("offline"); now = now.AddHours(7);
        var offline = await store.RefreshAsync();
        Assert.True(offline.Offline); Assert.Equal(fresh.Catalog, offline.Catalog);
        handler.Reply = _ => Ok("{\"retcode\":0,\"data\":{}}"); now = now.AddHours(1);
        Assert.True((await store.RefreshAsync()).Offline);
        Assert.Equal(persisted, File.ReadAllText(Path.Combine(_directory, "wallpaper-catalog.json")));
        var reopened = new WallpaperCatalogStore(new(http), _directory, Response(), () => now);
        Assert.Equal("10_7", reopened.Current.Catalog.Wallpapers.Single().Id);
    }
    [Fact]
    public async Task CoalescesConcurrentRefreshesAndChecksForNewSkinsAfterSixHours()
    {
        var now = Start;
        var handler = new Handler(_ => Ok(Response()));
        using var http = new HttpClient(handler);
        var store = new WallpaperCatalogStore(new(http), _directory, Response(), () => now);
        await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => store.RefreshAsync(true)));
        Assert.Equal(1, handler.Calls);
        now = now.AddHours(1); await store.RefreshAsync(); Assert.Equal(1, handler.Calls);
        handler.Reply = _ => Ok(Response([Skin(5, 0)])); now = now.AddHours(6);
        Assert.Equal("5_0", (await store.RefreshAsync()).Catalog.Wallpapers.Single().Id);
        Assert.Equal(2, handler.Calls);
    }
    [Fact]
    public void CorruptedCacheUsesBundledListAndOldSettingsMigrate()
    {
        Directory.CreateDirectory(_directory); File.WriteAllText(Path.Combine(_directory, "wallpaper-catalog.json"), "corrupt");
        using var http = new HttpClient(new Handler(_ => Ok(Response())));
        Assert.Equal(2, new WallpaperCatalogStore(new(http), _directory, Response()).Current.Catalog.Wallpapers.Count);
        var settings = JsonSerializer.Deserialize<WidgetSettings>("{\"Theme\":\"Light\",\"Mini\":true}")!;
        Assert.True(settings.CharacterStyle); Assert.True(settings.Mini); Assert.Equal("Light", settings.Theme);
        settings.Wallpaper = WallpaperCatalogClient.Parse(Response()).Wallpapers[1];
        Assert.Equal("1031_3110311", JsonSerializer.Deserialize<WidgetSettings>(JsonSerializer.Serialize(settings))!.Wallpaper!.Id);
    }
    [Fact]
    public async Task RejectsOversizedResponsesBeforeParsing()
    {
        using var content = new StringContent(new string('x', 100));
        await Assert.ThrowsAsync<IOException>(() => WallpaperCatalogClient.ReadBoundedAsync(content, 80, default));
    }
    private static HttpResponseMessage Ok(string json) => new(HttpStatusCode.OK) { Content = new StringContent(json, Encoding.UTF8, "application/json") };
    private sealed class Handler(Func<HttpRequestMessage, HttpResponseMessage> reply) : HttpMessageHandler
    {
        public Func<HttpRequestMessage, HttpResponseMessage> Reply { get; set; } = reply;
        public int Calls { get; private set; }
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        { Calls++; return Task.FromResult(Reply(request)); }
    }
    public void Dispose() { if (Directory.Exists(_directory)) Directory.Delete(_directory, true); }
}
