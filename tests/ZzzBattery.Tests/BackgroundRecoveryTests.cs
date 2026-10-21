using System.Text.Json;
using Xunit;
using ZzzBattery.Core;

namespace ZzzBattery.Tests;

public sealed class BackgroundRecoveryTests
{
    private static Credential Account(string id = "123") => new()
    {
        AccountKey = id, Role = new("100", "prod_gf_cn", "", "")
    };
    private static EnergySnapshot Snapshot(Credential c, Clock clock) =>
        new(c.AccountKey, c.Role!.Key, 120, 240, 43200, clock.GetUtcNow(), [], Source: "官方网页便笺");
    private static SyncCheckpoint RoundTrip(WidgetSession session) =>
        JsonSerializer.Deserialize<SyncCheckpoint>(JsonSerializer.Serialize(session.CaptureCheckpoint()))!;

    [Fact]
    public async Task VerificationRecoversOnceThenUsesTheWorkingChannelAcrossRestart()
    {
        var clock = new Clock(); var account = Account(); var client = new Client();
        var reads = 0; SyncCheckpoint? beforeRead = null;
        using var first = new WidgetSession(client, clock, (c, _) =>
        {
            Assert.True(beforeRead?.AutomaticRecoveryAttempted);
            Assert.True(beforeRead?.PreferOfficialPage);
            reads++; return Task.FromResult(Snapshot(c, clock));
        });
        first.CheckpointChanged += state => beforeRead = state;
        first.SetCredential(account); await first.RefreshAsync();
        Assert.Equal(1, client.Calls); Assert.Equal(1, reads); Assert.Null(first.Failure);
        Assert.False(first.Busy); Assert.False(first.RecoveringInBackground);
        Assert.False(first.CaptureCheckpoint()!.AutomaticRecoveryAttempted);
        clock.Advance(TimeSpan.FromMinutes(31)); await first.RefreshAsync();
        Assert.Equal(1, client.Calls); Assert.Equal(2, reads);
        using var restarted = new WidgetSession(client, clock, (c, _) =>
        { reads++; return Task.FromResult(Snapshot(c, clock)); });
        restarted.SetCredential(account, first.Snapshot, RoundTrip(first));
        await restarted.RefreshAsync(); Assert.Equal(2, reads);
        clock.Advance(TimeSpan.FromMinutes(31)); await restarted.RefreshAsync();
        Assert.Equal(1, client.Calls); Assert.Equal(3, reads);
    }

    [Fact]
    public async Task HumanChallengeStopsAutomaticWorkIncludingAfterRestartButManualRetryRemainsPossible()
    {
        var clock = new Clock(); var account = Account(); var client = new Client(); var reads = 0;
        Task<EnergySnapshot> Read(Credential c, CancellationToken _) { reads++; throw MiyousheClient.FromRetcode(10035); }
        using var first = new WidgetSession(client, clock, Read);
        first.SetCredential(account); await first.RefreshAsync();
        Assert.Equal(1, reads); Assert.Equal(FailureKind.Verification, first.Failure?.Kind);
        for (var i = 0; i < 100; i++) { clock.Advance(TimeSpan.FromMinutes(30)); await first.RefreshAsync(); }
        Assert.Equal(1, reads); Assert.Equal(1, client.Calls);
        using var restarted = new WidgetSession(client, clock, Read);
        restarted.SetCredential(account, checkpoint: RoundTrip(first));
        await restarted.RefreshAsync(); Assert.Equal(1, reads);
        await restarted.RefreshAsync(true); Assert.Equal(2, reads); Assert.Equal(1, client.Calls);
    }

    [Fact]
    public async Task OldVerificationCheckpointGetsOneAttemptAfterExistingCooldown()
    {
        var clock = new Clock(); var account = Account(); var client = new Client(); var reads = 0;
        using var session = new WidgetSession(client, clock, (c, _) => { reads++; return Task.FromResult(Snapshot(c, clock)); });
        session.SetCredential(account, checkpoint: new(account.AccountKey, account.Role!.Key, clock.GetUtcNow(),
            TimeSpan.FromMinutes(15), TimeSpan.FromMinutes(1), FailureKind.Verification, 10035));
        await session.RefreshAsync(true); Assert.Equal(0, reads);
        clock.Advance(TimeSpan.FromMinutes(16)); await session.RefreshAsync();
        Assert.Equal(1, reads); Assert.Equal(0, client.Calls);
    }

    [Theory]
    [InlineData(10001)]
    [InlineData(10041)]
    [InlineData(5003)]
    [InlineData(-110)]
    public async Task OtherFailuresDoNotLaunchBrowser(int code)
    {
        var client = new Client { Error = MiyousheClient.FromRetcode(code) };
        using var session = new WidgetSession(client, officialNoteReader: (_, _) => throw new Exception("Unexpected browser"));
        session.SetCredential(Account()); await session.RefreshAsync();
        Assert.Equal(code, session.Failure?.Code); Assert.Equal(1, client.Calls);
    }

    [Fact]
    public async Task RetryAfterIsRespectedBeforeBrowserRecovery()
    {
        var clock = new Clock(); var client = new Client { Error = new(FailureKind.Verification, "wait", 10035, TimeSpan.FromHours(1)) };
        var reads = 0;
        using var session = new WidgetSession(client, clock, (c, _) => { reads++; return Task.FromResult(Snapshot(c, clock)); });
        session.SetCredential(Account()); await session.RefreshAsync();
        Assert.Equal(0, reads);
        clock.Advance(TimeSpan.FromMinutes(59)); await session.RefreshAsync(true); Assert.Equal(0, reads);
        clock.Advance(TimeSpan.FromMinutes(2)); await session.RefreshAsync(); Assert.Equal(1, reads);
        Assert.Equal(1, client.Calls);
    }

    [Fact]
    public async Task CapturingDataManuallyAlsoPreventsReturningToRestrictedWidget()
    {
        var clock = new Clock(); var client = new Client(); var account = Account(); var reads = 0;
        using var session = new WidgetSession(client, clock, (c, _) => { reads++; return Task.FromResult(Snapshot(c, clock)); });
        session.SetCredential(account); session.ResumeAfterVerification(Snapshot(account, clock));
        clock.Advance(TimeSpan.FromMinutes(31)); await session.RefreshAsync();
        Assert.Equal(1, reads); Assert.Equal(0, client.Calls);
    }

    [Theory]
    [InlineData("wrong-account", "prod_gf_cn:100", 0)]
    [InlineData("123", "another-role", 0)]
    [InlineData("123", "prod_gf_cn:100", -10)]
    [InlineData("123", "prod_gf_cn:100", 10)]
    public async Task WrongRoleOrStaleDataDoesNotFinishRecovery(string account, string role, int ageMinutes)
    {
        var clock = new Clock(); var client = new Client();
        using var session = new WidgetSession(client, clock, (c, _) => Task.FromResult(Snapshot(c, clock) with
        { AccountKey = account, RoleKey = role, ReceivedAt = clock.GetUtcNow().AddMinutes(ageMinutes) }));
        session.SetCredential(Account()); await session.RefreshAsync();
        Assert.Null(session.Snapshot); Assert.Equal(FailureKind.Verification, session.Failure?.Kind);
    }

    [Fact]
    public async Task AccountSwitchDiscardsOldPageAndCancelsItsRead()
    {
        var clock = new Clock(); var client = new Client(); var firstAccount = Account();
        var pending = new TaskCompletionSource<EnergySnapshot>(); CancellationToken observed = default;
        using var session = new WidgetSession(client, clock, (_, ct) => { observed = ct; return pending.Task; });
        session.SetCredential(firstAccount); var request = session.RefreshAsync();
        Assert.True(session.RecoveringInBackground);
        Assert.Same(request, session.RefreshAsync(true));
        session.SetCredential(Account("456")); Assert.True(observed.IsCancellationRequested);
        pending.SetResult(Snapshot(firstAccount, clock)); await request;
        Assert.Null(session.Snapshot); Assert.False(session.RecoveringInBackground);
        Assert.False(session.CaptureCheckpoint()!.PreferOfficialPage);
    }

    [Fact]
    public async Task InFlightRecoveryIsCheckpointedSoCrashCannotLoop()
    {
        var clock = new Clock(); var client = new Client(); var account = Account();
        var pending = new TaskCompletionSource<EnergySnapshot>();
        using var session = new WidgetSession(client, clock, (_, _) => pending.Task);
        session.SetCredential(account); var request = session.RefreshAsync();
        var saved = RoundTrip(session);
        Assert.True(saved.AutomaticRecoveryAttempted); Assert.Equal(FailureKind.Verification, saved.FailureKind);
        clock.Advance(TimeSpan.FromDays(1));
        using var restarted = new WidgetSession(client, clock, (_, _) => throw new Exception("Recovery loop"));
        restarted.SetCredential(account, checkpoint: saved); await restarted.RefreshAsync();
        pending.SetResult(Snapshot(account, clock)); await request;
        Assert.Equal(1, client.Calls);
    }

    [Fact]
    public async Task FailureToPersistRecoveryStopsBeforeOpeningPage()
    {
        var client = new Client();
        using var session = new WidgetSession(client, officialNoteReader: (_, _) => throw new Exception("Unpersisted browser read"));
        session.SetCredential(Account());
        session.CheckpointChanged += state =>
        { if (state.AutomaticRecoveryAttempted) throw new IOException("Disk unavailable"); };
        await session.RefreshAsync();
        Assert.Equal(FailureKind.Permission, session.Failure?.Kind);
        Assert.Contains("保存", session.Message); Assert.False(session.Busy);
        Assert.False(session.RecoveringInBackground);
        await session.RefreshAsync(); Assert.Equal(1, client.Calls);
    }

    [Fact]
    public async Task UnwritableCheckpointStopsBeforeAnyRequest()
    {
        var client = new Client(); using var session = new WidgetSession(client);
        session.CheckpointChanged += _ => throw new UnauthorizedAccessException();
        session.SetCredential(Account()); await session.RefreshAsync(true);
        Assert.Equal(0, client.Calls); Assert.Equal(FailureKind.Permission, session.Failure?.Kind);
    }

    private sealed class Client : IMiyousheClient
    {
        public int Calls;
        public ApiFailure Error = MiyousheClient.FromRetcode(10035);
        public Task<IReadOnlyList<GameRole>> GetRolesAsync(Credential c, CancellationToken cancellation = default) => throw new NotSupportedException();
        public Task<EnergySnapshot> GetNoteAsync(Credential c, GameRole role, CancellationToken cancellation = default)
        { Calls++; return Task.FromException<EnergySnapshot>(Error); }
    }
    private sealed class Clock : TimeProvider
    {
        private DateTimeOffset _utc = DateTimeOffset.UtcNow;
        private long _stamp;
        public void Advance(TimeSpan duration) { _utc += duration; _stamp += duration.Ticks; }
        public override DateTimeOffset GetUtcNow() => _utc;
        public override long GetTimestamp() => _stamp;
        public override long TimestampFrequency => TimeSpan.TicksPerSecond;
    }
}
