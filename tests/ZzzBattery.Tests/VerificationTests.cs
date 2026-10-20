using System.Net;
using System.Text;
using System.Text.Json;
using Xunit;
using ZzzBattery.Core;

namespace ZzzBattery.Tests;

public sealed class VerificationTests
{
    [Fact]
    public void OfficialVerificationUrlUsesBrowserEntryAndPreservesSelectedRole()
    {
        var uri = new Uri(MiyousheClient.VerificationPageUrl(Account().Role));
        Assert.Equal("https", uri.Scheme);
        Assert.Equal("act.mihoyo.com", uri.Host);
        Assert.Contains("bbs=1", uri.Query);
        Assert.DoesNotContain("role_id", uri.Query);
        Assert.StartsWith("#/zzz/daily-note?", uri.Fragment);
        Assert.Contains("role_id=100000001", uri.Fragment);
        Assert.Contains("server=prod_gf_cn", uri.Fragment);
    }
    private static Credential Account(string id = "123")
    {
        var c = Cookies.Create($"account_id={id}; cookie_token=unit-test");
        c.Role = new("100000001", "prod_gf_cn", "Test", "国服"); return c;
    }
    private const string TaskJson = """{"retcode":0,"data":{"gt":"unit-test-gt","challenge":"unit-test-challenge","new_captcha":true,"success":1}}""";
    private const string NoteJson = """{"retcode":0,"data":{"energy":{"progress":{"current":200,"max":240},"restore":14400}}}""";
    private static readonly VerificationAnswer Answer = new("unit-test-challenge", "human-result", "human-result|jordan");
    private static HttpResponseMessage Json(string body) => new(HttpStatusCode.OK) { Content = new StringContent(body, Encoding.UTF8, "application/json") };
    private sealed class Handler(Func<HttpRequestMessage, Task<HttpResponseMessage>> send) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) => send(request);
    }

    [Fact]
    public async Task RealVerificationContractForwardsTraceSignsAnswerAndScopesProof()
    {
        var noteCalls = 0;
        using var http = new HttpClient(new Handler(async request =>
        {
            var path = request.RequestUri!.AbsolutePath;
            if (path.EndsWith("createGeetest"))
            {
                Assert.Equal(HttpMethod.Get, request.Method);
                Assert.Equal("?app_key=game_record_zzz&is_high=true", request.RequestUri.Query);
                Assert.Equal("8", request.Headers.GetValues("x-rpc-challenge_game").Single());
                Assert.Equal("test-trace", request.Headers.GetValues("x-rpc-challenge_trace").Single());
                return Json(TaskJson);
            }
            if (path.EndsWith("verifyGeetest"))
            {
                Assert.Equal(HttpMethod.Post, request.Method);
                var body = await request.Content!.ReadAsStringAsync();
                using var payload = JsonDocument.Parse(body);
                Assert.Equal("game_record_zzz", payload.RootElement.GetProperty("app_key").GetString());
                Assert.Equal(Answer.Validate, payload.RootElement.GetProperty("geetest_validate").GetString());
                var ds = request.Headers.GetValues("DS").Single().Split(',');
                Assert.Equal(MiyousheClient.Sign("", long.Parse(ds[0]), int.Parse(ds[1]), body), string.Join(",", ds));
                Assert.False(request.Headers.Contains("x-rpc-challenge"));
                return Json("""{"retcode":0,"data":{"challenge":"server-issued-proof"}}""");
            }
            noteCalls++;
            if (noteCalls == 1)
            {
                var response = Json("""{"retcode":10035,"data":null}""");
                response.Headers.Add("x-trace-id", "test-trace"); return response;
            }
            if (noteCalls == 2) Assert.Equal("server-issued-proof", request.Headers.GetValues("x-rpc-challenge").Single());
            else Assert.False(request.Headers.Contains("x-rpc-challenge"));
            return Json(NoteJson);
        }));
        var client = new MiyousheClient(http, "consistent-device"); var account = Account();
        var failure = await Assert.ThrowsAsync<ApiFailure>(() => client.GetNoteAsync(account, account.Role!));
        var task = await client.CreateVerificationAsync(account, failure);
        Assert.NotNull(task);
        Assert.True(task.Online);
        await client.CompleteVerificationAsync(account, task, Answer);
        Assert.Equal(200, (await client.GetNoteAsync(account, account.Role!)).Current);
        var other = Account("456"); await client.GetNoteAsync(other, other.Role!);
        await client.GetNoteAsync(account, account.Role! with { Uid = "100000002" });
        client.ForgetVerification(); await client.GetNoteAsync(account, account.Role!);
    }

    [Fact]
    public async Task IncompleteTaskCannotPretendToBeAReadyCaptcha()
    {
        using var http = new HttpClient(new Handler(_ => Task.FromResult(Json("""{"retcode":0,"data":{}}"""))));
        var failure = await Assert.ThrowsAsync<ApiFailure>(() => new MiyousheClient(http, "device").CreateVerificationAsync(Account()));
        Assert.Equal(FailureKind.Protocol, failure.Kind);
    }

    [Fact]
    public async Task ChangedAccountAndExpiredTaskCannotSubmitVerification()
    {
        var calls = 0;
        using var http = new HttpClient(new Handler(_ => { calls++; return Task.FromResult(Json(TaskJson)); }));
        var client = new MiyousheClient(http, "device"); var account = Account();
        var task = await client.CreateVerificationAsync(account);
        Assert.NotNull(task);
        await Assert.ThrowsAsync<ArgumentException>(() => client.CompleteVerificationAsync(Account("456"), task, Answer));
        await Assert.ThrowsAsync<ArgumentException>(() => client.CompleteVerificationAsync(account, task with { CreatedAt = DateTimeOffset.UtcNow.AddMinutes(-11) }, Answer));
        Assert.Equal(1, calls);
    }

    [Fact]
    public async Task RejectedCaptchaNeverAttachesAnUnverifiedClientAnswer()
    {
        using var http = new HttpClient(new Handler(request =>
        {
            Assert.False(request.Headers.Contains("x-rpc-challenge"));
            return Task.FromResult(Json(request.RequestUri!.AbsolutePath.EndsWith("createGeetest") ? TaskJson :
                request.RequestUri.AbsolutePath.EndsWith("verifyGeetest") ? """{"retcode":-3102,"data":null}""" : NoteJson));
        }));
        var client = new MiyousheClient(http, "device"); var account = Account();
        var task = await client.CreateVerificationAsync(account);
        Assert.NotNull(task);
        await Assert.ThrowsAsync<ApiFailure>(() => client.CompleteVerificationAsync(account, task, Answer));
        await client.GetNoteAsync(account, account.Role!);
    }
    [Theory]
    [InlineData("{\"retcode\":0,\"challenge\":\"verified-flat-proof\"}")]
    [InlineData("{\"challenge\":\"verified-flat-proof\"}")]
    public async Task AcceptsOfficialFlatVerificationEnvelopeOnlyAtVerificationEndpoint(string response)
    {
        using var http = new HttpClient(new Handler(request =>
        {
            var path = request.RequestUri!.AbsolutePath;
            if (path.EndsWith("createGeetest")) return Task.FromResult(Json(TaskJson));
            if (path.EndsWith("verifyGeetest")) return Task.FromResult(Json(response));
            Assert.Equal("verified-flat-proof", request.Headers.GetValues("x-rpc-challenge").Single());
            return Task.FromResult(Json(NoteJson));
        }));
        var client = new MiyousheClient(http, "device"); var account = Account();
        var task = await client.CreateVerificationAsync(account);
        Assert.NotNull(task);
        await client.CompleteVerificationAsync(account, task, Answer);
        await client.GetNoteAsync(account, account.Role!);
    }

    [Theory]
    [InlineData(10035, FailureKind.Verification)]
    [InlineData(10041, FailureKind.Permission)]
    [InlineData(5003, FailureKind.Permission)]
    public void DistinguishesCaptchaFromOtherRestrictions(int code, FailureKind kind)
    {
        var error = MiyousheClient.FromRetcode(code);
        Assert.Equal(kind, error.Kind); Assert.Contains(code.ToString(), error.Message); Assert.True(error.NeedsUser);
    }

    [Fact]
    public async Task VerificationFailureClearsProofImmediately()
    {
        var calls = 0;
        using var http = new HttpClient(new Handler(_ =>
        {
            calls++;
            return Task.FromResult(Json("""{"retcode":10035,"data":null}"""));
        }));
        var client = new MiyousheClient(http, "device");
        var account = Account();
        client.SetVerificationProof(account, account.Role!, "some-proof");
        await Assert.ThrowsAsync<ApiFailure>(() => client.GetNoteAsync(account, account.Role!));
        Assert.Equal(1, calls);
    }

    [Fact]
    public async Task NoteQueryIncludesPageAndRefererHeaders()
    {
        using var http = new HttpClient(new Handler(request =>
        {
            Assert.Equal("v3.0.15_/zzz", request.Headers.GetValues("x-rpc-page").Single());
            Assert.Equal("https://act.mihoyo.com/app/mihoyo-zzz-game-record/m.html", request.Headers.Referrer?.ToString());
            Assert.Equal("https://act.mihoyo.com", request.Headers.GetValues("Origin").Single());
            return Task.FromResult(Json(NoteJson));
        }));
        var client = new MiyousheClient(http, "device");
        var account = Account();
        var snapshot = await client.GetNoteAsync(account, account.Role!);
        Assert.Equal(200, snapshot.Current);
    }

    [Fact]
    public void ResumeAfterVerificationDirectlyAppliesSnapshot()
    {
        var client = new MiyousheClient(new HttpClient(), "device");
        using var session = new WidgetSession(client);
        var account = Account();
        session.SetCredential(account);
        var verified = new EnergySnapshot(account.AccountKey, account.Role!.Key, 230, 240, 600, DateTimeOffset.UtcNow, []);
        EnergySnapshot? synced = null;
        session.Synced += s => synced = s;
        session.ResumeAfterVerification(verified, resetCooldown: true);
        Assert.Equal(230, session.Snapshot?.Current);
        Assert.Same(verified, synced);
        Assert.Null(session.Failure);
    }
}
