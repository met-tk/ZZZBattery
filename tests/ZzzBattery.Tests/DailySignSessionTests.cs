using System.Text.Json;
using Xunit;
using ZzzBattery.Core;

namespace ZzzBattery.Tests;

public sealed class DailySignSessionTests
{
    private static Credential Account(string key = "account", string uid = "100") =>
        new() { AccountKey = key, Cookie = "cookie", Role = new(uid, "prod_gf_cn", "", "") };
    private sealed class Clock : TimeProvider
    {
        public DateTimeOffset Utc = new(2026, 10, 20, 4, 0, 0, TimeSpan.Zero);
        private long _stamp;
        public void Advance(TimeSpan value) { Utc += value; _stamp += value.Ticks; }
        public override DateTimeOffset GetUtcNow() => Utc;
        public override long GetTimestamp() => _stamp;
        public override long TimestampFrequency => TimeSpan.TicksPerSecond;
    }
    private sealed class Client(Clock clock) : IDailySignClient
    {
        public int Gets, Posts;
        public bool Signed, Commit = true;
        public ApiFailure? GetError, PostError;
        public Task<DailySignInfo>? Pending;
        public Action<Credential>? OnPost;
        public DailySignInfo Info(bool value) => new(value, DailySignSession.ChinaDay(clock.Utc), clock.Utc);
        public Task<DailySignInfo> GetDailySignInfoAsync(Credential credential, CancellationToken cancellation = default)
        {
            Gets++; return Pending ?? (GetError is not null ? Task.FromException<DailySignInfo>(GetError) : Task.FromResult(Info(Signed)));
        }
        public Task<DailySignSubmitResult> SubmitDailySignAsync(Credential credential, CancellationToken cancellation = default)
        {
            Posts++; OnPost?.Invoke(credential);
            if (PostError is not null) return Task.FromException<DailySignSubmitResult>(PostError);
            if (Commit) Signed = true;
            return Task.FromResult(DailySignSubmitResult.Submitted);
        }
    }
    private static DailySignCheckpoint RoundTrip(DailySignSession session) =>
        JsonSerializer.Deserialize<DailySignCheckpoint>(JsonSerializer.Serialize(session.CaptureCheckpoint()))!;

    [Fact]
    public async Task SignIsConfirmedAndFurtherClicksAreLocal()
    {
        var clock = new Clock(); var client = new Client(clock);
        using var session = new DailySignSession(client, clock); session.SetCredential(Account());
        await session.SignAsync(); await session.SignAsync(); await session.CheckAsync(true);
        Assert.Equal(2, client.Gets); Assert.Equal(1, client.Posts);
        Assert.True(session.IsSignedToday); Assert.Equal("已签到", session.ButtonText);
        Assert.False(session.UncertainSubmission); Assert.False(session.Busy);
    }
    [Fact]
    public async Task AlreadySignedNeverPosts()
    {
        var clock = new Clock(); var client = new Client(clock) { Signed = true };
        using var session = new DailySignSession(client, clock); session.SetCredential(Account());
        await session.SignAsync();
        Assert.True(session.IsSignedToday); Assert.Equal(1, client.Gets); Assert.Equal(0, client.Posts);
    }
    [Fact]
    public async Task FreshBackgroundCheckCanBeUsedImmediatelyWithoutAnotherPreflight()
    {
        var clock = new Clock(); var client = new Client(clock);
        using var session = new DailySignSession(client, clock); session.SetCredential(Account());
        await session.CheckAsync();
        Assert.Equal("今日签到", session.ButtonText); Assert.True(session.CanSign);
        clock.Advance(TimeSpan.FromSeconds(10)); await session.SignAsync();
        Assert.Equal(2, client.Gets); Assert.Equal(1, client.Posts); Assert.True(session.IsSignedToday);
    }
    [Fact]
    public async Task ConcurrentClicksShareOneOperation()
    {
        var clock = new Clock(); var client = new Client(clock);
        var pending = new TaskCompletionSource<DailySignInfo>(TaskCreationOptions.RunContinuationsAsynchronously);
        client.Pending = pending.Task;
        using var session = new DailySignSession(client, clock); session.SetCredential(Account());
        var first = session.SignAsync(); var second = session.SignAsync();
        Assert.Same(first, second); Assert.True(session.Busy);
        client.Pending = null; pending.SetResult(client.Info(false)); await Task.WhenAll(first, second);
        Assert.Equal(2, client.Gets); Assert.Equal(1, client.Posts);
    }
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task AccountOrRoleSwitchDiscardsOldResponse(bool roleOnly)
    {
        var clock = new Clock(); var client = new Client(clock);
        var pending = new TaskCompletionSource<DailySignInfo>(TaskCreationOptions.RunContinuationsAsynchronously);
        client.Pending = pending.Task;
        using var session = new DailySignSession(client, clock); session.SetCredential(Account());
        var old = session.SignAsync(); session.SetCredential(Account(roleOnly ? "account" : "different", "200"));
        pending.SetResult(client.Info(true)); await old;
        Assert.False(session.IsSignedToday); Assert.Equal(0, client.Posts); Assert.False(session.Busy);
    }
    [Fact]
    public async Task CallerMutationCannotChangeInflightIdentity()
    {
        var clock = new Clock(); var client = new Client(clock);
        var pending = new TaskCompletionSource<DailySignInfo>(TaskCreationOptions.RunContinuationsAsynchronously);
        client.Pending = pending.Task;
        var account = Account();
        using var session = new DailySignSession(client, clock); session.SetCredential(account);
        var op = session.SignAsync();
        account.AccountKey = "changed"; account.Role = new("200", "prod_qd_cn", "", "");
        client.OnPost = credential => { Assert.Equal("account", credential.AccountKey); Assert.Equal("100", credential.Role!.Uid); };
        client.Pending = null; pending.SetResult(client.Info(false)); await op;
        Assert.True(session.IsSignedToday); Assert.Equal(1, client.Posts);
    }
    [Fact]
    public async Task UncertainPostSurvivesRestartAndNextClickOnlyReads()
    {
        var clock = new Clock(); var client = new Client(clock) { PostError = new(FailureKind.Network, "timeout") };
        using var first = new DailySignSession(client, clock); first.SetCredential(Account());
        DailySignCheckpoint? beforePost = null;
        first.CheckpointChanged += state => { if (state.UncertainSubmission && client.Posts == 0) beforePost = state; };
        await first.SignAsync();
        Assert.NotNull(beforePost); Assert.True(first.UncertainSubmission);
        using var restarted = new DailySignSession(client, clock); restarted.SetCredential(Account(), RoundTrip(first));
        await restarted.SignAsync(); Assert.Equal(1, client.Posts); Assert.Equal(1, client.Gets);
        clock.Advance(TimeSpan.FromMinutes(6)); client.PostError = null;
        await restarted.SignAsync();
        Assert.Equal(2, client.Gets); Assert.Equal(1, client.Posts); Assert.False(restarted.UncertainSubmission);
        await restarted.SignAsync(); Assert.Equal(2, client.Posts); Assert.True(restarted.IsSignedToday);
    }
    [Fact]
    public async Task ImmediateNegativeConfirmationDoesNotAllowDuplicatePost()
    {
        var clock = new Clock(); var client = new Client(clock) { Commit = false };
        using var session = new DailySignSession(client, clock); session.SetCredential(Account());
        await session.SignAsync(); await session.SignAsync();
        Assert.Equal(1, client.Posts); Assert.True(session.UncertainSubmission); Assert.False(session.IsSignedToday);
        clock.Advance(TimeSpan.FromSeconds(61)); await session.SignAsync();
        Assert.Equal(1, client.Posts); Assert.False(session.UncertainSubmission);
        client.Commit = true; await session.SignAsync(); Assert.Equal(2, client.Posts); Assert.True(session.IsSignedToday);
    }
    [Theory]
    [InlineData(FailureKind.Verification)]
    [InlineData(FailureKind.Login)]
    [InlineData(FailureKind.Permission)]
    [InlineData(FailureKind.RateLimited)]
    public async Task ConfirmationFailureDoesNotClaimPostWasRejected(FailureKind kind)
    {
        var clock = new Clock(); var client = new Client(clock);
        client.OnPost = _ => client.GetError = new(kind, "error", 10035);
        using var session = new DailySignSession(client, clock); session.SetCredential(Account());
        await session.SignAsync(); Assert.True(session.UncertainSubmission); Assert.False(session.IsSignedToday);
        clock.Advance(TimeSpan.FromDays(1)); await session.CheckAsync();
        Assert.Equal(2, client.Gets); Assert.Equal(1, client.Posts);
        client.GetError = null; await session.CheckAsync(true); Assert.True(session.IsSignedToday);
    }
    [Fact]
    public async Task ActualPostChallengeHaltsAutomaticQueriesAcrossRestart()
    {
        var clock = new Clock(); var client = new Client(clock) { PostError = new(FailureKind.Verification, "challenge", 10035) };
        using var first = new DailySignSession(client, clock); first.SetCredential(Account()); await first.SignAsync();
        Assert.False(first.UncertainSubmission); Assert.Equal("需验证", first.ButtonText);
        using var restarted = new DailySignSession(client, clock); restarted.SetCredential(Account(), RoundTrip(first));
        clock.Advance(TimeSpan.FromHours(1)); await restarted.CheckAsync();
        Assert.Equal(1, client.Gets); Assert.Equal(1, client.Posts);
    }
    [Fact]
    public async Task SignedCacheExpiresAtChinaMidnightNotGameReset()
    {
        var clock = new Clock { Utc = new(2026, 10, 20, 15, 59, 30, TimeSpan.Zero) };
        var client = new Client(clock) { Signed = true };
        using var first = new DailySignSession(client, clock); first.SetCredential(Account()); await first.CheckAsync();
        using var restarted = new DailySignSession(client, clock); restarted.SetCredential(Account(), RoundTrip(first));
        Assert.True(restarted.IsSignedToday);
        clock.Advance(TimeSpan.FromSeconds(61)); restarted.Tick();
        Assert.False(restarted.IsSignedToday);
        client.Signed = false; await restarted.SignAsync();
        Assert.True(restarted.IsSignedToday); Assert.Equal(1, client.Posts);
    }
    [Fact]
    public async Task ClockRollbackAndRestartDoNotBypassRetryAfter()
    {
        var clock = new Clock(); var client = new Client(clock) { GetError = new(FailureKind.RateLimited, "wait", 429, TimeSpan.FromMinutes(25)) };
        using var first = new DailySignSession(client, clock); first.SetCredential(Account()); await first.CheckAsync();
        var state = RoundTrip(first); clock.Utc -= TimeSpan.FromHours(2);
        using var restarted = new DailySignSession(client, clock); restarted.SetCredential(Account(), state);
        Assert.True(restarted.ClockUncertain);
        clock.Advance(TimeSpan.FromMinutes(24)); await restarted.SignAsync(); Assert.Equal(1, client.Gets);
        clock.Advance(TimeSpan.FromMinutes(2)); client.GetError = null; await restarted.SignAsync();
        Assert.Equal(1, client.Posts); Assert.True(restarted.IsSignedToday);
    }
    [Fact]
    public async Task MismatchedCheckpointCannotSignAnotherAccount()
    {
        var clock = new Clock(); var client = new Client(clock) { Signed = true };
        using var first = new DailySignSession(client, clock); first.SetCredential(Account()); await first.CheckAsync();
        using var next = new DailySignSession(client, clock); next.SetCredential(Account("other"), RoundTrip(first));
        Assert.False(next.IsSignedToday); next.SetCredential(Account(uid: "200"), RoundTrip(first)); Assert.False(next.IsSignedToday);
    }
    [Fact]
    public void OnlyFreshCorrectWidgetStatusCanUpdateSign()
    {
        var clock = new Clock(); var client = new Client(clock); var account = Account();
        using var session = new DailySignSession(client, clock); session.SetCredential(account);
        session.ObserveSigned(true, clock.Utc.AddMinutes(-10), account.AccountKey, account.Role!.Key);
        session.ObserveSigned(true, clock.Utc, "other", account.Role.Key);
        session.ObserveSigned(true, clock.Utc, account.AccountKey, "wrong-role");
        Assert.False(session.IsSignedToday);
        session.ObserveSigned(true, clock.Utc, account.AccountKey, account.Role.Key); Assert.True(session.IsSignedToday);
        session.ObserveSigned(false, clock.Utc.AddSeconds(1), account.AccountKey, account.Role.Key); Assert.True(session.IsSignedToday);
        Assert.Equal(0, client.Gets); Assert.Equal(0, client.Posts);
    }
    [Fact]
    public async Task WrongServerDayIsNotSuccessAndDoesNotPost()
    {
        var clock = new Clock(); var client = new Client(clock);
        client.Pending = Task.FromResult(client.Info(true) with { Day = new(2026, 10, 19) });
        using var session = new DailySignSession(client, clock); session.SetCredential(Account()); await session.SignAsync();
        Assert.False(session.IsSignedToday); Assert.Equal(FailureKind.Protocol, session.Failure!.Kind); Assert.Equal(0, client.Posts);
    }
    [Fact]
    public async Task UnwritableCheckpointStopsBeforeMutation()
    {
        var clock = new Clock(); var client = new Client(clock);
        using var session = new DailySignSession(client, clock); session.SetCredential(Account());
        session.CheckpointChanged += _ => throw new IOException("disk full");
        await session.SignAsync(); Assert.Equal(0, client.Posts); Assert.False(session.Busy);
        Assert.Contains("保存", session.Message); Assert.False(session.IsSignedToday);
    }
    [Fact]
    public async Task DisposeCancelsStaleRequestAndNeverPosts()
    {
        var clock = new Clock(); var client = new Client(clock);
        var pending = new TaskCompletionSource<DailySignInfo>(TaskCreationOptions.RunContinuationsAsynchronously);
        client.Pending = pending.Task;
        var session = new DailySignSession(client, clock); session.SetCredential(Account());
        var op = session.SignAsync(); session.Dispose(); pending.SetResult(client.Info(false)); await op;
        Assert.Equal(0, client.Posts);
    }
}
