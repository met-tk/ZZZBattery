using System.Net;
using System.Text;
using System.Text.Json;
using Xunit;
using ZzzBattery.Core;

namespace ZzzBattery.Tests;

public sealed class CoreTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 19, 8, 0, 0, TimeSpan.Zero);
    private static readonly GameRole Role = new("100000001", "prod_gf_cn", "Test", "国服");
    private static Credential Credential(string id = "123") { var c = Cookies.Create($"account_id={id}; cookie_token=local-test"); c.Role = Role; return c; }
    private static EnergySnapshot Snapshot(int current = 238, int? restore = 450) => new("a", Role.Key, current, 240, restore, Now, []);

    [Theory]
    [InlineData(0, 238, 450)]
    [InlineData(89, 238, 361)]
    [InlineData(90, 239, 360)]
    [InlineData(449, 239, 1)]
    [InlineData(450, 240, 0)]
    public void PreservesRecoveryPhase(int elapsed, int current, int remaining)
    {
        var p = EnergyEstimator.Project(Snapshot(), TimeSpan.FromSeconds(elapsed));
        Assert.Equal(current, p.Current); Assert.Equal(remaining, p.RemainingSeconds);
    }
    [Theory]
    [InlineData(null)]
    [InlineData(0)]
    [InlineData(-1)]
    public void MissingRecoveryNeverBecomesFull(int? restore)
    {
        var p = EnergyEstimator.Project(Snapshot(200, restore), TimeSpan.FromMinutes(1));
        Assert.Equal(200, p.Current); Assert.False(p.Full); Assert.True(p.Inconsistent);
    }
    [Fact]
    public void KeepsOverCapServerValue()
    {
        var p = EnergyEstimator.Project(Snapshot(300, 0), TimeSpan.FromMinutes(1));
        Assert.Equal(300, p.Current); Assert.Equal(1, p.Ratio);
    }
    [Fact]
    public void OldCacheIsFrozen()
    {
        var p = EnergyEstimator.Project(Snapshot(), TimeSpan.FromHours(2));
        Assert.Equal(238, p.Current); Assert.True(p.Stale); Assert.Null(p.RemainingSeconds);
    }
    [Fact]
    public void InconsistentRecoveryDoesNotInventEnergy()
    {
        var p = EnergyEstimator.Project(Snapshot(238, 9000), TimeSpan.FromMinutes(20));
        Assert.True(p.Inconsistent); Assert.Equal(238, p.Current);
    }
    [Fact]
    public void CookieParserStripsUnrelatedTrackingAndSupportsV2()
    {
        var c = Cookies.Create("ltuid_v2=123; cookie_token_v2=token; tracking_id=secret");
        Assert.Contains("account_id_v2=123", c.Cookie); Assert.DoesNotContain("tracking_id", c.Cookie);
        Assert.DoesNotContain("token", c.ToString());
    }
    [Fact]
    public void CookieParserRejectsHeaderInjection() =>
        Assert.Throws<ArgumentException>(() => Cookies.Create("account_id=123; cookie_token=abc\r\nX-Evil:true"));
    [Fact]
    public void AcceptsLegacyCredentialFile()
    {
        var c = Cookies.Create("""{"raw_cookie":"account_id=123; cookie_token=test"}""");
        Assert.Contains("cookie_token=test", c.Cookie);
    }
    [Theory]
    [InlineData("mihoyo.com", true)]
    [InlineData("www.miyoushe.com", true)]
    [InlineData("mihoyo.com.evil.example", false)]
    [InlineData("evilmihoyo.com", false)]
    public void HostAllowListIsBounded(string host, bool allowed) => Assert.Equal(allowed, Cookies.IsOfficialHost(host));

    [Fact]
    public async Task RequestUsesSelectedRoleAndSignsExactQuery()
    {
        using var http = new HttpClient(new Handler(request =>
        {
            Assert.Equal("/event/game_record_zzz/api/zzz/note", request.RequestUri!.AbsolutePath);
            Assert.Equal("?role_id=100000001&server=prod_gf_cn", request.RequestUri.Query);
            var parts = request.Headers.GetValues("DS").Single().Split(',');
            Assert.Equal(MiyousheClient.Sign(request.RequestUri.Query[1..], long.Parse(parts[0]), int.Parse(parts[1])), string.Join(",", parts));
            return Json("""{"retcode":0,"data":{"energy":{"progress":{"current":200,"max":240},"restore":14400},"vitality":{"current":400,"max":400}}}""");
        }));
        var snapshot = await new MiyousheClient(http, "test-device").GetNoteAsync(Credential(), Role);
        Assert.Equal(200, snapshot.Current); Assert.Equal(Role.Key, snapshot.RoleKey);
        Assert.Single(snapshot.Notes);
    }
    [Theory]
    [InlineData(-100, FailureKind.Login)]
    [InlineData(1034, FailureKind.Verification)]
    [InlineData(10102, FailureKind.Permission)]
    public async Task ApiErrorsAreNotDemoSuccess(int code, FailureKind expected)
    {
        using var http = new HttpClient(new Handler(_ => Json($$"""{"retcode":{{code}},"data":null}""")));
        var error = await Assert.ThrowsAsync<ApiFailure>(() => new MiyousheClient(http, "test").GetNoteAsync(Credential(), Role));
        Assert.Equal(expected, error.Kind);
    }
    [Fact]
    public async Task HonorsRetryAfter()
    {
        using var http = new HttpClient(new Handler(_ =>
        {
            var response = new HttpResponseMessage(HttpStatusCode.TooManyRequests);
            response.Headers.RetryAfter = new(System.TimeSpan.FromMinutes(20));
            return response;
        }));
        var error = await Assert.ThrowsAsync<ApiFailure>(() => new MiyousheClient(http, "test").GetNoteAsync(Credential(), Role));
        Assert.Equal(TimeSpan.FromMinutes(20), error.RetryAfter);
    }
    [Fact]
    public void RejectsMissingEnergyRatherThanDisplayingZero()
    {
        using var doc = JsonDocument.Parse("""{"vitality":{"current":400,"max":400}}""");
        Assert.Throws<ApiFailure>(() => MiyousheClient.ParseNote(doc.RootElement, "a", "r", Now));
    }
    [Fact]
    public async Task RepeatedRefreshSharesOneRequestAndCooldown()
    {
        var fake = new FakeClient();
        var clock = new Clock(Now);
        using var session = new WidgetSession(fake, clock);
        session.SetCredential(Credential());
        var first = session.RefreshAsync(true);
        var second = session.RefreshAsync(true);
        Assert.Same(first, second); Assert.Equal(1, fake.Calls);
        fake.Pending.SetResult(Snapshot() with { AccountKey = Credential().AccountKey });
        await first;
        await session.RefreshAsync(true);
        Assert.Equal(1, fake.Calls);
    }
    [Fact]
    public async Task OldAccountResponseCannotOverwriteNewAccount()
    {
        var fake = new FakeClient(); using var session = new WidgetSession(fake, new Clock(Now));
        session.SetCredential(Credential("123"));
        var request = session.RefreshAsync(true);
        session.SetCredential(Credential("456"));
        fake.Pending.SetResult(Snapshot());
        await request;
        Assert.Null(session.Snapshot); Assert.Equal(Credential("456").AccountKey, session.Credential!.AccountKey);
    }
    [Fact]
    public void CacheIsIsolatedByAccountAndRole()
    {
        var c = Credential();
        using var session = new WidgetSession(new FakeClient(), new Clock(Now));
        session.SetCredential(c, Snapshot() with { AccountKey = "different-account" });
        Assert.Null(session.Snapshot);
    }
    [Fact]
    public async Task NetworkFailureRetainsRealCache()
    {
        var fake = new FakeClient();
        using var session = new WidgetSession(fake, new Clock(Now));
        var c = Credential();
        session.SetCredential(c, Snapshot() with { AccountKey = c.AccountKey, ReceivedAt = Now.AddMinutes(-2) });
        var request = session.RefreshAsync(true);
        fake.Pending.SetException(new ApiFailure(FailureKind.Network, "offline"));
        await request;
        Assert.Equal(238, session.Snapshot!.Current);
        Assert.Equal(FailureKind.Network, session.Failure!.Kind);
    }
    [Fact]
    public void ClockJumpFreezesEstimates()
    {
        var clock = new Clock(Now); var c = Credential();
        using var session = new WidgetSession(new FakeClient(), clock);
        session.SetCredential(c, Snapshot() with { AccountKey = c.AccountKey });
        clock.Utc = Now.AddHours(3);
        Assert.True(session.Projection()!.Stale); Assert.True(session.ClockUncertain);
    }
    [Fact]
    public async Task BackwardClockCorrectionPreservesScheduleAndAllowsExplicitRefresh()
    {
        var clock = new Clock(Now); var fake = new FakeClient();
        using var session = new WidgetSession(fake, clock);
        session.SetCredential(Credential());
        var request = session.RefreshAsync(true);
        fake.Pending.SetResult(Snapshot()); await request;
        clock.Stamp = TimeSpan.FromSeconds(61).Ticks;
        clock.Utc = Now.AddHours(-3);
        session.InvalidateTime();
        await session.RefreshAsync();
        Assert.Equal(1, fake.Calls);
        await session.RefreshAsync(true);
        Assert.Equal(2, fake.Calls);
    }
    [Fact]
    public async Task ClockChangeCannotBypassServerRateLimit()
    {
        var clock = new Clock(Now); var fake = new FakeClient();
        using var session = new WidgetSession(fake, clock);
        session.SetCredential(Credential());
        var request = session.RefreshAsync(true);
        fake.Pending.SetException(new ApiFailure(FailureKind.RateLimited, "wait", retryAfter: TimeSpan.FromMinutes(20)));
        await request;
        clock.Stamp = TimeSpan.FromSeconds(61).Ticks;
        clock.Utc = Now.AddHours(3);
        session.InvalidateTime(); await session.RefreshAsync(true);
        Assert.Equal(1, fake.Calls);
        clock.Stamp = TimeSpan.FromMinutes(21).Ticks;
        await session.RefreshAsync(true);
        Assert.Equal(2, fake.Calls);
    }
    [Fact]
    public void MalformedCacheIsDiscarded()
    {
        var c = Credential();
        using var session = new WidgetSession(new FakeClient(), new Clock(Now));
        session.SetCredential(c, Snapshot() with { AccountKey = c.AccountKey, Notes = null! });
        Assert.Null(session.Snapshot);
        session.SetCredential(c, Snapshot() with { AccountKey = c.AccountKey, Maximum = 0 });
        Assert.Null(session.Snapshot);
    }
    [Fact]
    public async Task CompletedVerificationResumesSchedulingWithoutBypassingCooldown()
    {
        var clock = new Clock(Now); var fake = new FakeClient();
        using var session = new WidgetSession(fake, clock);
        session.SetCredential(Credential());
        var request = session.RefreshAsync(true);
        fake.Pending.SetException(MiyousheClient.FromRetcode(10035)); await request;
        session.ResumeAfterVerification(); Assert.Null(session.Failure);
        await session.RefreshAsync(); Assert.Equal(1, fake.Calls);
        clock.Stamp = TimeSpan.FromSeconds(61).Ticks;
        await session.RefreshAsync(); Assert.Equal(2, fake.Calls);
    }
    [Fact]
    public void RemindersAreDeduplicatedAndSuppressedOffline()
    {
        var state = new ReminderState(); var snapshot = Snapshot();
        var p = EnergyEstimator.Project(snapshot, TimeSpan.Zero);
        Assert.Null(state.Evaluate(snapshot, p, Now, false, 30));
        Assert.NotNull(state.Evaluate(snapshot, p, Now, true, 30));
        Assert.Null(state.Evaluate(snapshot, p, Now.AddMinutes(1), true, 30));
        var full = EnergyEstimator.Project(snapshot, TimeSpan.FromSeconds(450));
        Assert.NotNull(state.Evaluate(snapshot, full, Now.AddSeconds(450), true, 30));
        Assert.Null(state.Evaluate(snapshot, full, Now.AddSeconds(451), true, 30));
    }
    private static HttpResponseMessage Json(string json) => new(HttpStatusCode.OK)
    { Content = new StringContent(json, Encoding.UTF8, "application/json") };
    private sealed class Handler(Func<HttpRequestMessage, HttpResponseMessage> handle) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) => Task.FromResult(handle(request));
    }
    private sealed class FakeClient : IMiyousheClient
    {
        public int Calls;
        public TaskCompletionSource<EnergySnapshot> Pending { get; } = new();
        public Task<IReadOnlyList<GameRole>> GetRolesAsync(Credential credential, CancellationToken cancellation = default) => Task.FromResult<IReadOnlyList<GameRole>>([Role]);
        public Task<EnergySnapshot> GetNoteAsync(Credential credential, GameRole role, CancellationToken cancellation = default) { Calls++; return Pending.Task; }
    }
    private sealed class Clock(DateTimeOffset initial) : TimeProvider
    {
        public DateTimeOffset Utc { get; set; } = initial;
        public long Stamp { get; set; }
        public override DateTimeOffset GetUtcNow() => Utc;
        public override long GetTimestamp() => Stamp;
        public override long TimestampFrequency => TimeSpan.TicksPerSecond;
    }
}
