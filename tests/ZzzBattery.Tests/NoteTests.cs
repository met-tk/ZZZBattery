using System.Net;
using System.Text;
using System.Text.Json;
using Xunit;
using ZzzBattery.Core;

namespace ZzzBattery.Tests;

public sealed class NoteTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 19, 10, 0, 0, TimeSpan.Zero);
    private static EnergySnapshot Parse(string fields) => MiyousheClient.ParseNote(JsonSerializer.Deserialize<JsonElement>(
        "{\"energy\":{\"progress\":{\"current\":90,\"max\":240},\"restore\":54000}," + fields + "}"), "a", "r", Now);

    [Theory]
    [InlineData("CafeStateDone", NoteState.Complete, "已享用")]
    [InlineData("CafeStateNo", NoteState.Pending, "未享用")]
    [InlineData("CafeStateNewVersion", NoteState.Unknown, "状态未知")]
    public void CoffeeUsesTheRealTopLevelEnum(string raw, NoteState state, string value)
    {
        var line = Assert.Single(Parse($$""" "cafe_state":"{{raw}}" """).Notes);
        Assert.Equal(state, line.State); Assert.Equal(value, line.Value);
    }
    [Theory]
    [InlineData("CardSignDone", NoteState.Complete)]
    [InlineData("CardSignNo", NoteState.Pending)]
    [InlineData("CardSignNotDone", NoteState.Pending)]
    [InlineData("Unknown", NoteState.Unknown)]
    public void ScratchCardUsesStringEnum(string raw, NoteState state) =>
        Assert.Equal(state, Assert.Single(Parse($$""" "card_sign":"{{raw}}" """).Notes).State);
    [Theory]
    [InlineData("SaleStateDoing", NoteState.InProgress, "正在营业")]
    [InlineData("SaleStateDone", NoteState.Pending, "收益待领取")]
    [InlineData("SaleStateNo", NoteState.Pending, "等待营业")]
    public void VideoRevenueIsNotMarkedCompleted(string raw, NoteState state, string value)
    {
        var line = Assert.Single(Parse($$""" "vhs_sale":{"sale_state":"{{raw}}"} """).Notes);
        Assert.Equal(state, line.State); Assert.Equal(value, line.Value);
    }
    [Fact]
    public void CommunitySignInIsNeverUsedForScratchCard()
    {
        var snapshot = Parse("\"has_signed\":true");
        Assert.Empty(snapshot.Notes);
        Assert.Equal(NoteState.Unknown, NoteBoard.WithMissing(snapshot.Notes).Single(n => n.Id == "card").State);
    }
    [Fact]
    public void LiveWidgetDivinationLabelDoesNotCreateADuplicateActivity()
    {
        var notes = Parse(""" "card_sign":"CardSignDone","note_list":[{"name":"饼铺盲盒/刮刮卡/占卜","value":"已完成"}] """).Notes;
        var card = Assert.Single(notes);
        Assert.Equal("card", card.Id); Assert.Equal(NoteState.Complete, card.State); Assert.Equal("日常", card.Group);
        Assert.Equal("饼铺盲盒/刮刮卡/占卜", card.Name);
    }
    [Fact]
    public void NoteListDoesNotSuppressStructuredDailyOrWeeklyFields()
    {
        var snapshot = Parse("""
        "note_list":[{"name":"惊喜放映企划","value":"已完成"},{"name":"咖啡","value":"未享用"}],
        "cafe_state":"CafeStateDone","card_sign":"CardSignNo",
        "weekly_task":{"cur_point":"300","max_point":"2100","refresh_time":"1000"},
        "bounty_commission":{"num":1,"total":3,"refresh_time":2000},
        "survey_points":{"num":400,"total":8000}
        """);
        Assert.Equal(6, snapshot.Notes.Count);
        Assert.Equal(NoteState.Complete, snapshot.Notes.Single(n => n.Id == "cafe").State);
        Assert.Equal("300 / 2100", snapshot.Notes.Single(n => n.Id == "weekly").Value);
        Assert.Equal(Now.AddSeconds(1000), snapshot.Notes.Single(n => n.Id == "weekly").ResetAt);
        Assert.Equal("1 / 3", snapshot.Notes.Single(n => n.Id == "bounty").Value);
        Assert.Equal(NoteState.Unknown, snapshot.Notes.Single(n => n.Name == "惊喜放映企划").State);
    }
    [Fact]
    public void CurrentBountyVersionTakesPriorityAndLockedIsNotZeroCompletion()
    {
        var notes = Parse("""
        "s2_bounty_commission":{"num":0,"total":0,"unlock":false},
        "bounty_commission":{"num":3,"total":3},"weekly_task":{"cur_point":0,"max_point":0}
        """).Notes;
        Assert.Equal(NoteState.Locked, notes.Single(n => n.Id == "bounty").State);
        Assert.Equal(NoteState.Unknown, notes.Single(n => n.Id == "weekly").State);
    }
    [Theory]
    [InlineData("null")]
    [InlineData("[]")]
    [InlineData("42")]
    [InlineData("\"bad\"")]
    public void OptionalMalformedDataNeverDestroysValidEnergy(string raw)
    {
        var snapshot = Parse($"\"weekly_task\":{raw},\"vhs_sale\":{raw},\"note_list\":[null,42],\"activity_list\":[null]");
        Assert.Equal(90, snapshot.Current);
        Assert.Empty(snapshot.Notes);
    }
    [Fact]
    public void AutoManagementSuppressesStaleManualSubstates()
    {
        var notes = Parse("""
        "temple_running":{"current_currency":6000,"weekly_currency_max":6000,"currency_next_refresh_ts":7200,
        "bench_state":"BenchStateCanProduce","auto_work":{"is_auto_work_running":true,"auto_work_ended":false,"left_ts":600}}
        """).Notes;
        Assert.Equal(2, notes.Count);
        Assert.Equal(NoteState.InProgress, notes.Single(n => n.Id == "temple_auto").State);
        Assert.Equal(NoteState.Complete, notes.Single(n => n.Id == "temple_currency").State);
    }
    [Fact]
    public void ActivityRewardProgressAndExpiryArePreserved()
    {
        var snapshot = Parse("""
        "note_list":[{"name":"惊喜放映企划","value":"进行中"}],
        "activity_list":[{"activity_id":33,"name":"惊喜放映企划","state":"STATE_IN_PROGRESS",
        "monochrome_got_cnt":120,"monochrome_cnt":300,"left_end_ts":600}]
        """);
        var line = Assert.Single(snapshot.Notes);
        Assert.Equal(120, line.Current); Assert.Equal(300, line.Target); Assert.Equal(NoteState.Pending, line.State);
        Assert.Equal(NoteState.Stale, Assert.Single(NoteBoard.Project(snapshot, Now.AddMinutes(11))).State);
    }
    [Fact]
    public void DailyResetIsBasedOnCnServerTimeAndDoesNotResetWeeklyProgress()
    {
        var beforeReset = new DateTimeOffset(2026, 10, 19, 19, 59, 0, TimeSpan.Zero);
        var snapshot = Parse(""" "card_sign":"CardSignDone","weekly_task":{"cur_point":2100,"max_point":2100,"refresh_time":86400} """) with { ReceivedAt = beforeReset };
        var notes = NoteBoard.Project(snapshot, beforeReset.AddMinutes(2));
        Assert.Equal(NoteState.Stale, notes.Single(n => n.Id == "card").State);
        Assert.Equal(NoteState.Complete, notes.Single(n => n.Id == "weekly").State);
        Assert.Equal(beforeReset.AddMinutes(1), NoteBoard.NextDailyReset(beforeReset));
    }
    [Fact]
    public void OfflineAndLegacyCachesCannotShowCurrentCompletion()
    {
        var snapshot = Parse(""" "card_sign":"CardSignDone" """);
        Assert.Equal(NoteState.Stale, Assert.Single(NoteBoard.Project(snapshot, Now, true)).State);
        Assert.Equal(NoteState.Stale, Assert.Single(NoteBoard.Project(snapshot, Now.AddMinutes(31))).State);
        var old = snapshot with { SchemaVersion = 0, Notes = [new("占卜 / 刮刮卡", "已完成")] };
        var note = Assert.Single(NoteBoard.Project(old, Now));
        Assert.Equal("card", note.Id); Assert.Equal(NoteState.Stale, note.State);
    }
    [Fact]
    public void ConflictingAccountCookiesAreRejected()
    {
        Assert.Throws<ArgumentException>(() => Cookies.Create("account_id=123; ltuid_v2=456; cookie_token=test"));
        var c = Cookies.Create("account_id_v2=123; stoken_v2=test; account_mid_v2=mid-test");
        Assert.Contains("stuid=123", c.Cookie); Assert.Contains("stoken=test", c.Cookie); Assert.Contains("mid=mid-test", c.Cookie);
    }
    [Theory]
    [InlineData("https://api-takumi-record.mihoyo.com/event/game_record_zzz/api/zzz/note?role_id=100&server=prod_gf_cn", true)]
    [InlineData("https://api-takumi-record.mihoyo.com/event/game_record_zzz/api/zzz/note?role_id=101&server=prod_gf_cn", false)]
    [InlineData("https://api-takumi-record.mihoyo.com/event/game_record_zzz/api/zzz/note?role_id=100&server=prod_gf_cn&role_id=101", false)]
    [InlineData("https://api-takumi-record.mihoyo.com/event/game_record_zzz/api/zzz/note?role_id=100", false)]
    [InlineData("https://evil.example/event/game_record_zzz/api/zzz/note?role_id=100&server=prod_gf_cn", false)]
    [InlineData("http://api-takumi-record.mihoyo.com/event/game_record_zzz/api/zzz/note?role_id=100&server=prod_gf_cn", false)]
    public void WebViewCaptureMustMatchExactRoleAndEndpoint(string url, bool expected) =>
        Assert.Equal(expected, MiyousheClient.IsNoteRequestForRole(new Uri(url), new("100", "prod_gf_cn", "", "")));

    [Theory]
    [InlineData(false, true)]
    [InlineData(true, false)]
    public async Task WidgetSupplementRequiresUnambiguousRole(bool multiple, bool shouldFetch)
    {
        var credential = Cookies.Create("account_id=123; cookie_token=test; stuid=123; stoken=test; mid=test");
        var role = new GameRole("100", "prod_gf_cn", "", ""); credential.Role = role;
        var widgetCalls = 0;
        using var http = new HttpClient(new Handler(request =>
        {
            if (request.RequestUri!.AbsolutePath.EndsWith("getUserGameRolesByCookie"))
                return "{\"retcode\":0,\"data\":{\"list\":[{\"game_uid\":\"100\",\"region\":\"prod_gf_cn\"}" + (multiple ? ",{\"game_uid\":\"101\",\"region\":\"prod_qd_cn\"}" : "") + "]}}";
            widgetCalls++;
            Assert.Contains("version=WV_V2", request.RequestUri.Query);
            Assert.True(Version.Parse(Assert.Single(request.Headers.GetValues("x-rpc-app_version"))) >= new Version(2, 115, 0),
                "The live widget API hides activity_list for the old 2.11.1 profile.");
            Assert.Contains("miHoYoBBS/", request.Headers.UserAgent.ToString());
            return """{"retcode":0,"data":{"cafe_state":"CafeStateDone","weekly_task":{"cur_point":0,"max_point":2100},"note_list":[{"name":"惊喜放映企划","value":"进行中"}]}}""";
        }));
        var client = new MiyousheClient(http, "test");
        var snapshot = Parse(""" "weekly_task":{"cur_point":1400,"max_point":2100} """) with { AccountKey = credential.AccountKey, RoleKey = role.Key };
        var result = await client.EnrichSnapshotAsync(snapshot, credential, role);
        Assert.Equal(shouldFetch ? 1 : 0, widgetCalls);
        Assert.Equal("1400 / 2100", result.Notes.Single(n => n.Id == "weekly").Value);
        if (shouldFetch) Assert.Contains(result.Notes, n => n.Name == "惊喜放映企划");
        else Assert.NotEmpty(result.SupplementMessage);
    }
    [Fact]
    public async Task WebOnlyLoginDoesNotHammerUnsupportedWidgetEndpoint()
    {
        using var http = new HttpClient(new Handler(_ => throw new Exception("Unexpected request")));
        var credential = Cookies.Create("account_id=123; cookie_token=test");
        credential.Role = new("100", "prod_gf_cn", "", "");
        var snapshot = Parse(""" "card_sign":"CardSignDone" """) with { AccountKey = credential.AccountKey, RoleKey = credential.Role.Key };
        var result = await new MiyousheClient(http, "test").EnrichSnapshotAsync(snapshot, credential, credential.Role);
        Assert.Equal(snapshot.Notes, result.Notes); Assert.Contains("额外登录凭据", result.SupplementMessage);
    }
    private sealed class Handler(Func<HttpRequestMessage, string> response) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(response(request), Encoding.UTF8, "application/json") });
    }

    [Theory]
    [InlineData(false, true)]
    [InlineData(true, false)]
    public async Task AppLoginUsesWidgetFirstAndMultiRoleLoginUsesScopedNote(bool multiple, bool succeeds)
    {
        var credential = Cookies.Create("stuid=123; stoken=test; mid=test; account_id=123; cookie_token=test");
        var role = new GameRole("100", "prod_gf_cn", "", ""); credential.Role = role;
        var noteCalls = 0;
        using var http = new HttpClient(new Handler(request =>
        {
            var path = request.RequestUri!.AbsolutePath;
            if (path.EndsWith("/note")) { noteCalls++; return """{"retcode":10035,"data":null}"""; }
            if (path.EndsWith("getUserGameRolesByCookie"))
                return "{\"retcode\":0,\"data\":{\"list\":[{\"game_uid\":\"100\",\"region\":\"prod_gf_cn\"}" + (multiple ? ",{\"game_uid\":\"101\",\"region\":\"prod_qd_cn\"}" : "") + "]}}";
            Assert.False(multiple);
            return """{"retcode":0,"data":{"energy":{"progress":{"current":123,"max":240},"restore":42120},"cafe_state":"CafeStateDone"}}""";
        }));
        var client = new MiyousheClient(http, "test", includeWidget: true);
        if (!succeeds)
        {
            var failure = await Assert.ThrowsAsync<ApiFailure>(() => client.GetNoteAsync(credential, role));
            Assert.Equal(10035, failure.Code);
        }
        else
        {
            var snapshot = await client.GetNoteAsync(credential, role);
            Assert.Equal(123, snapshot.Current); Assert.Equal("官方小组件", Assert.Single(snapshot.Notes).Source);
            Assert.Equal("官方小组件", snapshot.Source);
            Assert.Empty(snapshot.SupplementMessage);
        }
        Assert.Equal(multiple ? 1 : 0, noteCalls);
    }
}
