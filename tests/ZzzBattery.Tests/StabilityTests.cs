using System.Net;
using System.Text;
using System.Text.Json;
using Xunit;
using ZzzBattery.Core;

namespace ZzzBattery.Tests;

public sealed class StabilityTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 19, 12, 0, 0, TimeSpan.Zero);
    private static Credential Account(bool app = true)
    {
        var account = Cookies.Create("account_id=123; cookie_token=test" + (app ? "; stuid=123; stoken=test; mid=test" : ""));
        account.Role = new("100", "prod_gf_cn", "", "");
        return account;
    }
    private static EnergySnapshot Snapshot(Credential account, DateTimeOffset at) =>
        new(account.AccountKey, account.Role!.Key, 120, 240, 43200, at, [], SchemaVersion: 1);
    private static SyncCheckpoint RoundTrip(WidgetSession session) =>
        JsonSerializer.Deserialize<SyncCheckpoint>(JsonSerializer.Serialize(session.CaptureCheckpoint()))!;

    [Fact]
    public async Task SuccessfulScheduleAndManualCooldownSurviveRestart()
    {
        var clock = new Clock(); var account = Account(); var client = new Client(clock);
        using var first = new WidgetSession(client, clock);
        first.SetCredential(account); await first.RefreshAsync();
        var due = first.NextAttempt;
        clock.Advance(TimeSpan.FromSeconds(10));
        using var restarted = new WidgetSession(client, clock);
        restarted.SetCredential(account, first.Snapshot, RoundTrip(first));
        await restarted.RefreshAsync(); await restarted.RefreshAsync(true);
        Assert.Equal(1, client.Calls); Assert.Equal(due, restarted.NextAttempt);
        clock.Advance(TimeSpan.FromMinutes(31));
        await restarted.RefreshAsync(); Assert.Equal(2, client.Calls);
    }

    [Fact]
    public async Task InterruptedRequestIsCheckpointedBeforeNetworkCompletes()
    {
        var clock = new Clock(); var account = Account(); var pending = new TaskCompletionSource<EnergySnapshot>();
        var client = new Client(clock) { Pending = pending.Task };
        using var first = new WidgetSession(client, clock);
        SyncCheckpoint? saved = null; first.CheckpointChanged += state => saved = state;
        first.SetCredential(account); var request = first.RefreshAsync();
        Assert.True(first.Busy); Assert.NotNull(saved);
        Assert.Equal(TimeSpan.FromSeconds(60), saved.MinimumRefreshDelay);
        using var restarted = new WidgetSession(new Client(clock), clock);
        restarted.SetCredential(account, checkpoint: saved);
        await restarted.RefreshAsync(true); Assert.Null(restarted.Snapshot);
        pending.SetResult(Snapshot(account, clock.Utc)); await request;
    }

    [Fact]
    public async Task RestartAndClockRollbackCannotBypassServerRetryAfter()
    {
        var clock = new Clock(); var account = Account();
        var client = new Client(clock) { Error = new(FailureKind.RateLimited, "wait", 429, TimeSpan.FromMinutes(20)) };
        using var first = new WidgetSession(client, clock);
        first.SetCredential(account); await first.RefreshAsync();
        var saved = RoundTrip(first);
        clock.Utc = clock.Utc.AddHours(-2);
        using var restarted = new WidgetSession(client, clock);
        restarted.SetCredential(account, checkpoint: saved);
        Assert.True(restarted.ClockUncertain);
        clock.Advance(TimeSpan.FromMinutes(19));
        restarted.InvalidateTime(); restarted.ResumeAfterVerification(resetCooldown: true);
        await restarted.RefreshAsync(true); Assert.Equal(1, client.Calls);
        clock.Advance(TimeSpan.FromMinutes(2));
        client.Error = null;
        await restarted.RefreshAsync(); Assert.Equal(2, client.Calls);
    }

    [Theory]
    [InlineData(10035)]
    [InlineData(10041)]
    [InlineData(10001)]
    public async Task UserActionErrorsStayPausedAfterRestartAndKeepOldDataHonest(int code)
    {
        var clock = new Clock(); var account = Account(); var client = new Client(clock);
        using var first = new WidgetSession(client, clock);
        first.SetCredential(account); await first.RefreshAsync();
        clock.Advance(TimeSpan.FromMinutes(31)); client.Error = MiyousheClient.FromRetcode(code);
        await first.RefreshAsync();
        var old = first.Snapshot; var saved = RoundTrip(first);
        clock.Advance(TimeSpan.FromDays(1));
        using var restarted = new WidgetSession(client, clock);
        restarted.SetCredential(account, old, saved); await restarted.RefreshAsync();
        Assert.Equal(2, client.Calls); Assert.Same(old, restarted.Snapshot);
        Assert.Equal(code, restarted.Failure!.Code); Assert.True(restarted.Projection()!.Stale);
        await restarted.RefreshAsync(true); Assert.Equal(3, client.Calls);
    }

    [Fact]
    public async Task NetworkBackoffEscalatesAcrossRestartsAndManualClicksCannotShortenIt()
    {
        var clock = new Clock(); var account = Account();
        var client = new Client(clock) { Error = new(FailureKind.Network, "offline") };
        using var first = new WidgetSession(client, clock);
        first.SetCredential(account); await first.RefreshAsync();
        clock.Advance(TimeSpan.FromMinutes(1)); await first.RefreshAsync();
        using var restarted = new WidgetSession(client, clock);
        restarted.SetCredential(account, checkpoint: RoundTrip(first));
        clock.Advance(TimeSpan.FromSeconds(61)); await restarted.RefreshAsync(true);
        Assert.Equal(2, client.Calls);
        clock.Advance(TimeSpan.FromMinutes(1)); await restarted.RefreshAsync();
        Assert.Equal(3, client.Calls);
        Assert.Equal(TimeSpan.FromMinutes(5), restarted.NextAttempt - clock.Utc);
    }

    [Fact]
    public async Task FreshCacheAvoidsStartupBurstButDailyResetStillSchedulesRefresh()
    {
        var clock = new Clock { Utc = new(2026, 10, 19, 19, 59, 30, TimeSpan.Zero) };
        var account = Account(); var client = new Client(clock);
        using var first = new WidgetSession(client, clock);
        first.SetCredential(account, Snapshot(account, clock.Utc.AddMinutes(-2)));
        await first.RefreshAsync(); first.InvalidateTime(); await first.RefreshAsync();
        Assert.Equal(0, client.Calls);
        clock.Advance(TimeSpan.FromSeconds(36)); await first.RefreshAsync();
        Assert.Equal(1, client.Calls);
    }

    [Fact]
    public async Task NormalSleepResumeDoesNotFreezeFreshDataOrTriggerAnotherRequest()
    {
        var clock = new Clock(); var account = Account(); var client = new Client(clock);
        using var session = new WidgetSession(client, clock);
        session.SetCredential(account); await session.RefreshAsync();
        clock.Advance(TimeSpan.FromMinutes(2));
        session.InvalidateTime(); await session.RefreshAsync();
        Assert.False(session.ClockUncertain); Assert.False(session.Projection()!.Stale);
        Assert.Equal(1, client.Calls);
    }

    [Fact]
    public async Task ForeignAndCorruptCheckpointsDoNotPauseAnotherAccount()
    {
        var clock = new Clock(); var account = Account(); var client = new Client(clock);
        var state = new SyncCheckpoint("foreign", account.Role!.Key, Now, TimeSpan.FromMinutes(15), TimeSpan.Zero, FailureKind.Verification, 10035);
        using var session = new WidgetSession(client, clock);
        session.SetCredential(account, checkpoint: state); await session.RefreshAsync();
        Assert.Equal(1, client.Calls);
        session.SetCredential(account, checkpoint: state with { AccountKey = account.AccountKey, RemainingDelay = TimeSpan.MaxValue });
        await session.RefreshAsync(); Assert.Equal(2, client.Calls);
    }

    private const string Roles = """{"retcode":0,"data":{"list":[{"game_uid":"100","region":"prod_gf_cn"}]}}""";
    private const string Note = """{"retcode":0,"data":{"energy":{"progress":{"current":120,"max":240},"restore":43200},"card_sign":"CardSignDone"}}""";
    private static HttpResponseMessage Json(string body) => new(HttpStatusCode.OK) { Content = new StringContent(body, Encoding.UTF8, "application/json") };

    [Fact]
    public async Task WidgetSuccessNeverProbesWebNoteAndReusesRoleBinding()
    {
        var paths = new List<string>(); var account = Account();
        using var http = new HttpClient(new Handler(request =>
        {
            var path = request.RequestUri!.AbsolutePath; paths.Add(path);
            Assert.False(path.EndsWith("/note"));
            return Json(path.EndsWith("getUserGameRolesByCookie") ? Roles : Note);
        }));
        var client = new MiyousheClient(http, "stable-device", includeWidget: true);
        for (var i = 0; i < 2; i++)
        {
            var snapshot = await client.GetNoteAsync(account, account.Role!);
            Assert.Equal("官方小组件", snapshot.Source); Assert.Empty(snapshot.SupplementMessage);
        }
        Assert.Equal(1, paths.Count(p => p.EndsWith("getUserGameRolesByCookie")));
        Assert.Equal(2, paths.Count(p => p.EndsWith("/widget")));
    }

    [Theory]
    [InlineData(10035, FailureKind.Verification)]
    [InlineData(10101, FailureKind.RateLimited)]
    [InlineData(10001, FailureKind.Login)]
    [InlineData(30001, FailureKind.Protocol)]
    public async Task WidgetFailureDoesNotProbeOtherEndpointsOrCreateCaptchas(int code, FailureKind expected)
    {
        var calls = 0; var account = Account();
        using var http = new HttpClient(new Handler(request =>
        {
            calls++;
            var path = request.RequestUri!.AbsolutePath;
            if (path.EndsWith("getUserGameRolesByCookie")) return Json(Roles);
            Assert.EndsWith("/widget", path);
            return Json($$"""{"retcode":{{code}},"data":null}""");
        }));
        var client = new MiyousheClient(http, "stable-device", includeWidget: true);
        var error = await Assert.ThrowsAsync<ApiFailure>(() => client.GetNoteAsync(account, account.Role!));
        Assert.Equal(expected, error.Kind); Assert.Equal(2, calls);
    }

    [Fact]
    public async Task WebOnlyAccountStillUsesRoleBoundNoteWithoutWidgetCalls()
    {
        var calls = 0; var account = Account(app: false);
        using var http = new HttpClient(new Handler(request =>
        {
            calls++; Assert.True(MiyousheClient.IsNoteRequestForRole(request.RequestUri!, account.Role!));
            return Json(Note);
        }));
        var snapshot = await new MiyousheClient(http, "device", includeWidget: true).GetNoteAsync(account, account.Role!);
        Assert.Equal(1, calls); Assert.Equal("实时便笺", snapshot.Source);
    }

    [Fact]
    public async Task NoNeedCaptchaIsNotLoginFailureOrVerificationProofAndCannotBeRepeatedImmediately()
    {
        var calls = 0; var account = Account();
        using var http = new HttpClient(new Handler(request =>
        {
            calls++; Assert.Equal(MiyousheClient.CreateVerificationEndpoint, request.RequestUri!.GetLeftPart(UriPartial.Path));
            return Json("""{"retcode":30001,"message":"no need geetest","data":null}""");
        }));
        var client = new MiyousheClient(http, "device");
        Assert.Null(await client.CreateVerificationAsync(account));
        var cooldown = await Assert.ThrowsAsync<ApiFailure>(() => client.CreateVerificationAsync(account));
        Assert.Equal(FailureKind.RateLimited, cooldown.Kind); Assert.Equal(1, calls);
    }

    [Fact]
    public async Task WidgetVerificationUsesTheFailedRequestPath()
    {
        var account = Account();
        using var http = new HttpClient(new Handler(request =>
        {
            var path = request.RequestUri!.AbsolutePath;
            if (path.EndsWith("getUserGameRolesByCookie")) return Json(Roles);
            if (path.EndsWith("/widget")) return Json("""{"retcode":10035,"data":null}""");
            Assert.EndsWith("createGeetest", path);
            Assert.Equal("/api/zzz/widget", request.Headers.GetValues("x-rpc-challenge_path").Single());
            return Json("""{"retcode":30001,"data":null}""");
        }));
        var client = new MiyousheClient(http, "device", includeWidget: true);
        var error = await Assert.ThrowsAsync<ApiFailure>(() => client.GetNoteAsync(account, account.Role!));
        Assert.Null(await client.CreateVerificationAsync(account, error));
    }

    private sealed class Handler(Func<HttpRequestMessage, HttpResponseMessage> send) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) => Task.FromResult(send(request));
    }
    private sealed class Client(Clock clock) : IMiyousheClient
    {
        public int Calls;
        public ApiFailure? Error;
        public Task<EnergySnapshot>? Pending;
        public Task<IReadOnlyList<GameRole>> GetRolesAsync(Credential credential, CancellationToken cancellation = default) => throw new NotSupportedException();
        public Task<EnergySnapshot> GetNoteAsync(Credential credential, GameRole role, CancellationToken cancellation = default)
        {
            Calls++;
            return Pending ?? (Error is not null ? Task.FromException<EnergySnapshot>(Error) : Task.FromResult(Snapshot(credential, clock.Utc)));
        }
    }
    private sealed class Clock : TimeProvider
    {
        public DateTimeOffset Utc = Now;
        private long _stamp;
        public void Advance(TimeSpan elapsed) { Utc += elapsed; _stamp += elapsed.Ticks; }
        public override DateTimeOffset GetUtcNow() => Utc;
        public override long GetTimestamp() => _stamp;
        public override long TimestampFrequency => TimeSpan.TicksPerSecond;
    }
}
