using Xunit;
using ZzzBattery.Core;

namespace ZzzBattery.Tests;

public sealed class RecoveryTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.UtcNow;
    private static Credential Account() => new() { AccountKey = "test-account", Role = new("100", "prod_gf_cn", "Test", "国服") };
    private static Uri Note(string query = "role_id=100&server=prod_gf_cn") => new(MiyousheClient.NoteEndpoint + "?" + query);
    private const string ValidNote = """{"retcode":0,"data":{"energy":{"progress":{"current":119,"max":240},"restore":21780}}}""";

    [Fact]
    public void ReadableOfficialNoteNeedsNoCaptchaToBecomeFreshData()
    {
        var credential = Account();
        var result = MiyousheClient.ReadOfficialNoteResponse(Note(), 200, ValidNote, credential, Now);
        Assert.Equal(OfficialNoteCaptureState.Ready, result.State);
        Assert.Equal(119, result.Snapshot!.Current);
        Assert.Equal("官方网页便笺", result.Snapshot.Source);
        Assert.Equal(credential.AccountKey, result.Snapshot.AccountKey);
        Assert.Equal(credential.Role!.Key, result.Snapshot.RoleKey);
        Assert.Equal(Now, result.Snapshot.ReceivedAt);
        var client = new CountingClient();
        using var session = new WidgetSession(client);
        session.SetCredential(credential);
        session.ResumeAfterVerification(result.Snapshot);
        Assert.True(session.HasSyncedSince(Now));
        Assert.Equal(0, client.Calls); // No extra widget request gates this captured success.
    }

    [Theory]
    [InlineData("role_id=101&server=prod_gf_cn")]
    [InlineData("role_id=100&server=prod_qd_cn")]
    [InlineData("role_id=100")]
    [InlineData("role_id=100&server=prod_gf_cn&role_id=101")]
    public void VisibleDataForAnotherOrAmbiguousRoleCannotFinishRecovery(string query)
    {
        var result = MiyousheClient.ReadOfficialNoteResponse(Note(query), 200, ValidNote, Account(), Now);
        Assert.Equal(OfficialNoteCaptureState.RoleMismatch, result.State); Assert.Null(result.Snapshot);
    }

    [Theory]
    [InlineData("https://other.mihoyo.com/event/game_record_zzz/api/zzz/note")]
    [InlineData("https://api-takumi-record.mihoyo.com:8443/event/game_record_zzz/api/zzz/note")]
    [InlineData("http://api-takumi-record.mihoyo.com/event/game_record_zzz/api/zzz/note")]
    [InlineData("https://api-takumi-record.mihoyo.com/event/game_record_zzz/api/zzz/widget")]
    public void UnrelatedEndpointsNeverSupplyNoteData(string endpoint)
    {
        var result = MiyousheClient.ReadOfficialNoteResponse(new Uri(endpoint + "?role_id=100&server=prod_gf_cn"), 200, ValidNote, Account(), Now);
        Assert.Equal(OfficialNoteCaptureState.Ignored, result.State); Assert.Null(result.Snapshot);
    }

    [Theory]
    [InlineData("not JSON")]
    [InlineData("{\"data\":{\"energy\":{\"progress\":{\"current\":119,\"max\":240}}}}")]
    [InlineData("{\"retcode\":0,\"data\":{}}")]
    [InlineData("{\"retcode\":0,\"data\":{\"energy\":{\"progress\":{\"current\":-1,\"max\":240}}}}")]
    public void IncompleteResponsesStayUnconfirmed(string json)
    {
        var result = MiyousheClient.ReadOfficialNoteResponse(Note(), 200, json, Account(), Now);
        Assert.Equal(OfficialNoteCaptureState.InvalidData, result.State); Assert.Null(result.Snapshot);
    }

    [Theory]
    [InlineData(10035)]
    [InlineData(30001)]
    public void ErrorCodesAtTheNoteEndpointAreNotSuccessfulVerification(int code)
    {
        var result = MiyousheClient.ReadOfficialNoteResponse(Note(), 200, $$"""{"retcode":{{code}},"data":null}""", Account(), Now);
        Assert.Equal(OfficialNoteCaptureState.Rejected, result.State); Assert.Equal(code, result.Code); Assert.Null(result.Snapshot);
    }

    [Fact]
    public void HttpFailureCannotUseAnApparentlySuccessfulBody()
    {
        var result = MiyousheClient.ReadOfficialNoteResponse(Note(), 503, ValidNote, Account(), Now);
        Assert.Equal(OfficialNoteCaptureState.HttpError, result.State); Assert.Equal(503, result.Code); Assert.Null(result.Snapshot);
    }

    [Fact]
    public async Task CheckingSyncKeepsRestrictionCooldownAndOldCacheUnconfirmed()
    {
        var account = Account(); var client = new CountingClient();
        using var session = new WidgetSession(client);
        var snapshot = new EnergySnapshot(account.AccountKey, account.Role!.Key, 90, 240, 27000, Now.AddMinutes(-3), []);
        session.SetCredential(account, snapshot, new(account.AccountKey, account.Role.Key, Now,
            TimeSpan.FromMinutes(15), TimeSpan.FromMinutes(1), FailureKind.Verification, 10035, 1));
        await session.RefreshAsync(true);
        Assert.Equal(0, client.Calls); Assert.False(session.HasSyncedSince(Now));
        Assert.Equal(10035, session.Failure?.Code);
        session.ResumeAfterVerification(resetCooldown: true);
        Assert.False(session.HasSyncedSince(Now)); // Clearing a failure without data is insufficient.
        session.ResumeAfterVerification(snapshot with { Current = 119, ReceivedAt = Now });
        Assert.True(session.HasSyncedSince(Now));
        session.SetCredential(null);
        Assert.False(session.HasSyncedSince(Now));
    }

    private sealed class CountingClient : IMiyousheClient
    {
        public int Calls { get; private set; }
        public Task<IReadOnlyList<GameRole>> GetRolesAsync(Credential c, CancellationToken token = default) => throw new InvalidOperationException("Unexpected role request");
        public Task<EnergySnapshot> GetNoteAsync(Credential c, GameRole r, CancellationToken token = default)
        { Calls++; throw new InvalidOperationException("Unexpected note request"); }
    }
}
