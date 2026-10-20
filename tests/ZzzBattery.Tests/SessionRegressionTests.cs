using Xunit;
using ZzzBattery.Core;

namespace ZzzBattery.Tests;

public sealed class SessionRegressionTests
{
    private static Credential Account(string id)
    {
        var credential = Cookies.Create($"account_id={id}; cookie_token=test");
        credential.Role = new("100", "prod_gf_cn", "", ""); return credential;
    }
    private static EnergySnapshot Snapshot(Credential c, int energy = 90) =>
        new(c.AccountKey, c.Role!.Key, energy, 240, (240 - energy) * 360, DateTimeOffset.UtcNow, [], SchemaVersion: 1);
    [Fact]
    public async Task AccountSwitchDoesNotWaitForUncooperativeOldRequest()
    {
        var client = new DeferredClient(); using var session = new WidgetSession(client);
        var first = Account("123"); var second = Account("456");
        session.SetCredential(first); var old = session.RefreshAsync(true);
        session.SetCredential(second); var current = session.RefreshAsync(true);
        Assert.Equal(2, client.Requests.Count); Assert.True(session.Busy);
        client.Requests[0].SetResult(Snapshot(first)); await old;
        Assert.True(session.Busy); Assert.Null(session.Snapshot);
        client.Requests[1].SetResult(Snapshot(second, 120)); await current;
        Assert.False(session.Busy); Assert.Equal(120, session.Snapshot!.Current);
    }
    [Fact]
    public async Task CapturedVerifiedResponseCannotBeOverwrittenByOlderHttpRequest()
    {
        var client = new DeferredClient(); using var session = new WidgetSession(client);
        var account = Account("123"); session.SetCredential(account);
        var old = session.RefreshAsync(true);
        session.ResumeAfterVerification(Snapshot(account, 150));
        client.Requests[0].SetResult(Snapshot(account, 90)); await old;
        Assert.Equal(150, session.Snapshot!.Current); Assert.False(session.Busy);
    }
    [Fact]
    public void VerificationFromAnotherRoleOrSignedOutSessionIsIgnored()
    {
        using var session = new WidgetSession(new DeferredClient());
        var account = Account("123"); session.SetCredential(account);
        session.ResumeAfterVerification(Snapshot(Account("456")));
        Assert.Null(session.Snapshot);
        session.ResumeAfterVerification(Snapshot(account) with { RoleKey = "another-role" });
        Assert.Null(session.Snapshot);
        session.SetCredential(null); session.ResumeAfterVerification(Snapshot(account));
        Assert.Null(session.Snapshot);
    }
    [Fact]
    public void OlderCapturedSnapshotDoesNotMoveClockOrEnergyBackwards()
    {
        using var session = new WidgetSession(new DeferredClient());
        var account = Account("123"); session.SetCredential(account);
        var newest = Snapshot(account, 150); session.ResumeAfterVerification(newest);
        session.ResumeAfterVerification(Snapshot(account, 90) with { ReceivedAt = newest.ReceivedAt.AddSeconds(-10) });
        Assert.Equal(newest, session.Snapshot);
    }
    [Fact]
    public void CorruptedCacheRowsAreDiscardedWithoutBreakingUi()
    {
        using var session = new WidgetSession(new DeferredClient());
        var account = Account("123");
        session.SetCredential(account, Snapshot(account) with { Notes = [null!] }); Assert.Null(session.Snapshot);
        session.SetCredential(account, Snapshot(account) with { Notes = [new(null!, "ok")] }); Assert.Null(session.Snapshot);
        session.SetCredential(account, Snapshot(account) with { Current = int.MaxValue }); Assert.Null(session.Snapshot);
    }
    private sealed class DeferredClient : IMiyousheClient
    {
        public List<TaskCompletionSource<EnergySnapshot>> Requests { get; } = [];
        public Task<IReadOnlyList<GameRole>> GetRolesAsync(Credential c, CancellationToken token = default) => Task.FromResult<IReadOnlyList<GameRole>>([]);
        public Task<EnergySnapshot> GetNoteAsync(Credential c, GameRole r, CancellationToken token = default)
        { var task = new TaskCompletionSource<EnergySnapshot>(); Requests.Add(task); return task.Task; }
    }
}
