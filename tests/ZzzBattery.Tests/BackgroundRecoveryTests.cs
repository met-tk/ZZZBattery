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

    [Theory]
    [InlineData(10035)]
    [InlineData(10001)]
    public async Task LoginAndVerificationRetryWithBackoffAcrossRestart(int code)
    {
        var clock = new Clock(); var account = Account(); var client = new Client(); var reads = 0;
        Task<EnergySnapshot> Read(Credential c, CancellationToken _)
        {
            reads++;
            if (reads <= 3) throw MiyousheClient.FromRetcode(code);
            return Task.FromResult(Snapshot(c, clock));
        }
        using var first = new WidgetSession(client, clock, Read);
        first.SetCredential(account); await first.RefreshAsync();
        for (var i = 0; i < 100; i++) await first.RefreshAsync();
        Assert.Equal(1, reads);
        clock.Advance(TimeSpan.FromMinutes(15)); await first.RefreshAsync();
        Assert.Equal(2, reads);
        using var restarted = new WidgetSession(client, clock, Read);
        restarted.SetCredential(account, checkpoint: RoundTrip(first));
        clock.Advance(TimeSpan.FromMinutes(29)); await restarted.RefreshAsync(); Assert.Equal(2, reads);
        clock.Advance(TimeSpan.FromMinutes(1)); await restarted.RefreshAsync(); Assert.Equal(3, reads);
        clock.Advance(TimeSpan.FromMinutes(59)); await restarted.RefreshAsync(); Assert.Equal(3, reads);
        clock.Advance(TimeSpan.FromMinutes(1)); await restarted.RefreshAsync();
        Assert.Equal(4, reads); Assert.Null(restarted.Failure);
        Assert.Equal(1, client.Calls);
    }

    [Theory]
    [InlineData(FailureKind.Network)]
    [InlineData(FailureKind.Verification)]
    public async Task OfflineBootKeepsRetryingUntilNetworkIsReady(FailureKind kind)
    {
        var clock = new Clock(); var reads = 0;
        using var session = new WidgetSession(new Client(), clock, (c, _) =>
        {
            if (++reads <= 4) throw new ApiFailure(kind, "Network not ready");
            return Task.FromResult(Snapshot(c, clock));
        });
        session.SetCredential(Account()); await session.RefreshAsync();
        foreach (var minutes in new[] { 1, 2, 5, 15 })
        {
            await session.RefreshAsync();
            var before = reads;
            clock.Advance(TimeSpan.FromMinutes(minutes)); await session.RefreshAsync();
            Assert.Equal(before + 1, reads);
        }
        Assert.Equal(5, reads); Assert.Null(session.Failure);
    }

    [Fact]
    public async Task TransientBackgroundFailureRetriesOnceWithoutUserAction()
    {
        var clock = new Clock(); var account = Account(); var client = new Client(); var reads = 0;
        Task<EnergySnapshot> Read(Credential c, CancellationToken _)
        {
            reads++;
            if (reads == 1) throw new ApiFailure(FailureKind.Verification, "暂时无法读取官方便笺。");
            return Task.FromResult(Snapshot(c, clock));
        }
        using var session = new WidgetSession(client, clock, Read);
        session.SetCredential(account); await session.RefreshAsync();
        Assert.Equal(1, reads);
        Assert.False(session.CaptureCheckpoint()!.AutomaticRecoveryAttempted);

        clock.Advance(TimeSpan.FromMinutes(1)); await session.RefreshAsync();
        Assert.Equal(2, reads);
        Assert.Null(session.Failure);
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
    public async Task InFlightRecoveryResumesAfterPersistedCooldownWithoutARequestBurst()
    {
        var clock = new Clock(); var client = new Client(); var account = Account();
        var pending = new TaskCompletionSource<EnergySnapshot>(); var reads = 0;
        using var session = new WidgetSession(client, clock, (_, _) => pending.Task);
        session.SetCredential(account); var request = session.RefreshAsync();
        var saved = RoundTrip(session);
        Assert.True(saved.AutomaticRecoveryAttempted);
        using var restarted = new WidgetSession(client, clock, (c, _) => { reads++; return Task.FromResult(Snapshot(c, clock)); });
        restarted.SetCredential(account, checkpoint: saved); await restarted.RefreshAsync(); Assert.Equal(0, reads);
        clock.Advance(TimeSpan.FromMinutes(15)); await restarted.RefreshAsync(); Assert.Equal(1, reads);
        pending.SetResult(Snapshot(account, clock)); await request;
        Assert.Equal(1, client.Calls); Assert.Null(restarted.Failure);
    }

    [Fact]
    public async Task ExpiredLoginFallsBackToSavedBrowserSession()
    {
        var clock = new Clock(); var client = new Client { Error = MiyousheClient.FromRetcode(10001) };
        using var session = new WidgetSession(client, clock, (c, _) => Task.FromResult(Snapshot(c, clock)));
        session.SetCredential(Account()); await session.RefreshAsync();
        Assert.Equal(1, client.Calls); Assert.Null(session.Failure); Assert.NotNull(session.Snapshot);
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
