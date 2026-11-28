using System.Net;
using System.Text;
using System.Text.Json;
using Xunit;
using ZzzBattery.Core;

namespace ZzzBattery.Tests;

public sealed class QrLoginTests
{
    private static QrLoginTicket Ticket() => new("test-ticket", new Uri("https://user.mihoyo.com/test"), DateTimeOffset.UtcNow.AddMinutes(3));
    private sealed class Handler(Func<HttpRequestMessage, Task<string>> send) : HttpMessageHandler
    {
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            new(HttpStatusCode.OK) { Content = new StringContent(await send(request), Encoding.UTF8, "application/json") };
    }
    private static HttpClient Respond(string data) => new(new Handler(_ => Task.FromResult("{\"retcode\":0,\"data\":" + data + "}")));

    [Fact]
    public async Task QrUsesOfficialAppProfileSignsExactBodyAndNeverSendsSavedCredentials()
    {
        using var http = new HttpClient(new Handler(async request =>
        {
            Assert.Equal(MiyousheClient.QrCreateEndpoint, request.RequestUri!.AbsoluteUri);
            Assert.Equal(HttpMethod.Post, request.Method);
            Assert.False(request.Headers.Contains("Cookie"));
            Assert.Equal("3", Assert.Single(request.Headers.GetValues("x-rpc-client_type")));
            Assert.Equal("consistent-device", Assert.Single(request.Headers.GetValues("x-rpc-device_id")));
            var body = await request.Content!.ReadAsStringAsync(); Assert.Equal("{}", body);
            var ds = Assert.Single(request.Headers.GetValues("DS")).Split(',');
            Assert.Equal(MiyousheClient.Sign("", long.Parse(ds[0]), int.Parse(ds[1]), body), string.Join(',', ds));
            return """{"retcode":0,"data":{"ticket":"test-ticket","url":"https://user.mihoyo.com/test"}}""";
        }));
        var ticket = await new MiyousheClient(http, "consistent-device").CreateQrLoginAsync();
        Assert.DoesNotContain(ticket.Ticket, ticket.ToString());
    }
    [Theory]
    [InlineData("https://evil.example/login")]
    [InlineData("http://user.mihoyo.com/login")]
    [InlineData("https://user.mihoyo.com.evil.example/login")]
    [InlineData("https://name@user.mihoyo.com/login")]
    [InlineData("https://user.mihoyo.com:444/login")]
    public async Task UntrustedQrDestinationsAreRejected(string url)
    {
        using var http = Respond(JsonSerializer.Serialize(new { ticket = "test-ticket", url }));
        await Assert.ThrowsAsync<ApiFailure>(() => new MiyousheClient(http, "device").CreateQrLoginAsync());
    }
    [Theory]
    [InlineData("Init", QrLoginState.Waiting)]
    [InlineData("Created", QrLoginState.Waiting)]
    [InlineData("Scanned", QrLoginState.Scanned)]
    [InlineData("Expired", QrLoginState.Expired)]
    public async Task WaitingStatesContainNoCredential(string state, QrLoginState expected)
    {
        using var http = Respond(JsonSerializer.Serialize(new { status = state }));
        var result = await new MiyousheClient(http, "device").PollQrLoginAsync(Ticket());
        Assert.Equal(expected, result.State); Assert.Null(result.Credential);
    }
    [Fact]
    public async Task LocalExpiryStopsPolling()
    {
        using var http = new HttpClient(new Handler(_ => throw new Exception("Unexpected request")));
        var expired = new QrLoginTicket("test", new Uri("https://user.mihoyo.com/test"), DateTimeOffset.UtcNow.AddSeconds(-1));
        Assert.Equal(QrLoginState.Expired, (await new MiyousheClient(http, "device").PollQrLoginAsync(expired)).State);
    }
    [Fact]
    public async Task OfficialQrExpiryCodeIsNotReportedAsLoginFailure()
    {
        using var http = new HttpClient(new Handler(_ => Task.FromResult("""{"retcode":-106,"data":null}""")));
        Assert.Equal(QrLoginState.Expired, (await new MiyousheClient(http, "device").PollQrLoginAsync(Ticket())).State);
    }
    [Fact]
    public async Task ConfirmedQrKeepsAppAndWebTokensForTheSameAccount()
    {
        using var http = Respond("""{"status":"Confirmed","user_info":{"aid":"123","mid":"mid-test"},"tokens":[{"token_type":1,"token":"stoken-test"},{"token_type":2,"token":"ltoken-test"},{"token_type":4,"token":"cookie-test"}]}""");
        var result = await new MiyousheClient(http, "device").PollQrLoginAsync(Ticket());
        Assert.Equal(QrLoginState.Confirmed, result.State);
        var fields = Cookies.Parse(result.Credential!.Cookie);
        Assert.Equal("stoken-test", fields["stoken"]); Assert.Equal("cookie-test", fields["cookie_token"]);
        Assert.Equal("123", fields["account_id"]); Assert.Equal("123", fields["stuid"]);
        Assert.False(fields.ContainsKey("cookie_token_v2"));
        Assert.Equal("mid-test", fields["mid"]); Assert.DoesNotContain("stoken-test", result.Credential.ToString());
    }
    [Theory]
    [InlineData("{\"status\":\"Confirmed\",\"user_info\":{\"aid\":\"123\",\"mid\":\"test\"},\"tokens\":[]}")]
    [InlineData("{\"status\":\"Confirmed\",\"need_realperson\":true}")]
    [InlineData("{\"status\":\"NewUnknownState\"}")]
    public async Task IncompleteOrUnverifiedResponsesCannotBecomeCredentials(string data)
    {
        using var http = Respond(data);
        await Assert.ThrowsAsync<ApiFailure>(() => new MiyousheClient(http, "device").PollQrLoginAsync(Ticket()));
    }
    [Theory]
    [InlineData("123", true)]
    [InlineData("456", false)]
    public async Task StokenExchangeMustMatchTheConfirmedAccount(string responseId, bool valid)
    {
        using var http = new HttpClient(new Handler(async request =>
        {
            Assert.Equal(MiyousheClient.TokenExchangeEndpoint, request.RequestUri!.AbsoluteUri);
            Assert.Equal(HttpMethod.Post, request.Method);
            Assert.Equal("bll8iq97cem8", Assert.Single(request.Headers.GetValues("x-rpc-app_id")));
            Assert.False(request.Headers.Contains("Origin"));
            Assert.False(request.Headers.Contains("Cookie"));
            var body = await request.Content!.ReadAsStringAsync();
            var ds = Assert.Single(request.Headers.GetValues("DS")).Split(',');
            Assert.Equal(MiyousheClient.SignPassport(body, long.Parse(ds[0]), ds[1]), string.Join(',', ds));
            using var doc = JsonDocument.Parse(body);
            var dst = doc.RootElement.GetProperty("dst_token_type").GetInt32();
            Assert.Equal("mid-test", doc.RootElement.GetProperty("mid").GetString());
            Assert.Equal(1, doc.RootElement.GetProperty("src_token").GetProperty("token_type").GetInt32());
            Assert.Equal("test", doc.RootElement.GetProperty("src_token").GetProperty("token").GetString());
            return JsonSerializer.Serialize(new
            {
                retcode = 0,
                data = new
                {
                    token = new { token_type = dst, token = dst == 4 ? "cookie-test" : "ltoken-test" },
                    user_info = new { aid = responseId }
                }
            });
        }));
        var client = new MiyousheClient(http, "device");
        var credential = Cookies.Create("stuid=123; stoken=test; mid=mid-test");
        if (!valid) await Assert.ThrowsAsync<ApiFailure>(() => client.CompleteQrCredentialAsync(credential));
        else
        {
            var completed = await client.CompleteQrCredentialAsync(credential);
            Assert.Equal(credential.AccountKey, completed.AccountKey);
            Assert.Equal("cookie-test", Cookies.Parse(completed.Cookie)["cookie_token"]);
        }
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task CompleteQrCredentialEnrichesLTokenAndIgnoresLTokenFailure(bool ltokenSucceeds)
    {
        using var http = new HttpClient(new Handler(async request =>
        {
            Assert.Equal(MiyousheClient.TokenExchangeEndpoint, request.RequestUri!.AbsoluteUri);
            using var doc = JsonDocument.Parse(await request.Content!.ReadAsStringAsync());
            var dst = doc.RootElement.GetProperty("dst_token_type").GetInt32();
            if (dst == 4)
                return """{"retcode":0,"data":{"token":{"token_type":4,"token":"cookie-test"},"user_info":{"aid":"123"}}}""";
            Assert.Equal(2, dst);
            return ltokenSucceeds
                ? """{"retcode":0,"data":{"token":{"token_type":2,"token":"ltoken-test"},"user_info":{"aid":"123"}}}"""
                : """{"retcode":-100,"data":null}""";
        }));
        var client = new MiyousheClient(http, "device");
        var completed = await client.CompleteQrCredentialAsync(Cookies.Create("stuid=123; stoken=test; mid=mid-test"));
        var fields = Cookies.Parse(completed.Cookie);
        Assert.Equal("cookie-test", fields["cookie_token"]);
        if (ltokenSucceeds)
        {
            Assert.Equal("ltoken-test", fields["ltoken"]);
            Assert.Equal("123", fields["ltuid"]);
        }
        else
        {
            Assert.False(fields.ContainsKey("ltoken"));
        }
    }

    [Fact]
    public async Task CompleteQrCredentialRetriesExchangeWithDesktopClientTypeOnProtocolFailure()
    {
        var clientTypes = new List<string>();
        using var http = new HttpClient(new Handler(async request =>
        {
            var clientType = Assert.Single(request.Headers.GetValues("x-rpc-client_type"));
            clientTypes.Add(clientType);
            using var doc = JsonDocument.Parse(await request.Content!.ReadAsStringAsync());
            var dst = doc.RootElement.GetProperty("dst_token_type").GetInt32();
            if (clientType == "2")
                return """{"retcode":-464,"message":"risk rejected","data":null}""";
            return JsonSerializer.Serialize(new
            {
                retcode = 0,
                data = new
                {
                    token = new { token_type = dst, token = dst == 4 ? "cookie-desktop" : "ltoken-desktop" },
                    user_info = new { aid = "123" }
                }
            });
        }));
        var client = new MiyousheClient(http, "device");
        var completed = await client.CompleteQrCredentialAsync(Cookies.Create("stuid=123; stoken=test; mid=mid-test"));
        Assert.Equal("2", clientTypes[0]);
        Assert.Equal("3", clientTypes[1]);
        Assert.Equal("cookie-desktop", Cookies.Parse(completed.Cookie)["cookie_token"]);
    }

    [Fact]
    public async Task CompleteQrCredentialFallsBackToStokenAndReadsRolesByStokenWhenExchangeFails()
    {
        var exchangeAttempts = 0;
        var roleCalls = 0;
        using var http = new HttpClient(new Handler(request =>
        {
            var path = request.RequestUri!.AbsolutePath;
            if (path.EndsWith("/app/exchange"))
            {
                exchangeAttempts++;
                return Task.FromResult("""{"retcode":-464,"data":null}""");
            }
            roleCalls++;
            Assert.Equal(MiyousheClient.RolesByStokenEndpoint, request.RequestUri.AbsoluteUri);
            Assert.Equal("2", Assert.Single(request.Headers.GetValues("x-rpc-client_type")));
            var ds = Assert.Single(request.Headers.GetValues("DS")).Split(',');
            Assert.Equal(MiyousheClient.SignAndroidV1(long.Parse(ds[0]), ds[1]), string.Join(',', ds));
            var cookie = Assert.Single(request.Headers.GetValues("Cookie"));
            Assert.Contains("stoken=test-stoken", cookie);
            Assert.Contains("mid=mid-test", cookie);
            return Task.FromResult("""{"retcode":0,"data":{"list":[{"game_biz":"nap_cn","game_uid":"100000001","region":"prod_gf_cn","nickname":"绳匠","region_name":"官方服务器"}]}}""");
        }));
        var client = new MiyousheClient(http, "device");
        var credential = await client.CompleteQrCredentialAsync(Cookies.Create("stuid=123; stoken=test-stoken; mid=mid-test"));
        Assert.Equal(2, exchangeAttempts);
        var fields = Cookies.Parse(credential.Cookie);
        Assert.Equal("test-stoken", fields["stoken"]);
        Assert.False(fields.ContainsKey("cookie_token"));
        var roles = await client.GetRolesAsync(credential);
        Assert.Equal(1, roleCalls);
        Assert.Single(roles);
        Assert.Equal("100000001", roles[0].Uid);
    }

    [Fact]
    public async Task CompleteQrCredentialStillRejectsAccountMismatch()
    {
        using var http = new HttpClient(new Handler(_ =>
            Task.FromResult("""{"retcode":0,"data":{"token":{"token_type":4,"token":"cookie-other"},"user_info":{"aid":"999"}}}""")));
        var client = new MiyousheClient(http, "device");
        await Assert.ThrowsAsync<ApiFailure>(() =>
            client.CompleteQrCredentialAsync(Cookies.Create("stuid=123; stoken=test-stoken; mid=mid-test")));
    }

    [Fact]
    public async Task ExpiredWebCookieWithStokenRefreshesSilentlyWithoutBrowserHook()
    {
        var noteCalls = 0;
        var browserRecoveryCalls = 0;
        using var http = new HttpClient(new Handler(async request =>
        {
            var path = request.RequestUri!.AbsolutePath;
            if (path.EndsWith("/app/exchange"))
            {
                using var doc = JsonDocument.Parse(await request.Content!.ReadAsStringAsync());
                var dst = doc.RootElement.GetProperty("dst_token_type").GetInt32();
                return dst == 4
                    ? """{"retcode":0,"data":{"token":{"token_type":4,"token":"fresh-cookie"},"user_info":{"aid":"123"}}}"""
                    : """{"retcode":0,"data":{"token":{"token_type":2,"token":"fresh-ltoken"},"user_info":{"aid":"123"}}}""";
            }
            Assert.EndsWith("/note", path);
            noteCalls++;
            if (noteCalls == 1) return """{"retcode":-100,"data":null}""";
            var sentCookie = Assert.Single(request.Headers.GetValues("Cookie"));
            Assert.Contains("cookie_token=fresh-cookie", sentCookie);
            Assert.DoesNotContain("cookie_token_v2=stale", sentCookie);
            return """{"retcode":0,"data":{"energy":{"progress":{"current":200,"max":240},"restore":14400}}}""";
        }));
        var client = new MiyousheClient(http, "device")
        {
            CredentialRecovery = (_, _) => { browserRecoveryCalls++; return Task.FromResult(true); }
        };
        Credential? changed = null;
        client.CredentialChanged += c => changed = c;
        var account = Cookies.Create("account_id_v2=123; cookie_token_v2=stale; stuid=123; stoken=v2_valid; mid=mid-test");
        var role = new GameRole("100000001", "prod_gf_cn", "Test", "国服");
        var snapshot = await client.GetNoteAsync(account, role);
        Assert.Equal(200, snapshot.Current);
        Assert.Equal(2, noteCalls);
        Assert.Equal(0, browserRecoveryCalls);
        Assert.NotNull(changed);
        Assert.Contains("cookie_token=fresh-cookie", account.Cookie);
        Assert.Contains("stoken=v2_valid", account.Cookie);
    }

    [Fact]
    public async Task AnonymousPassportRequestsNeverInvokeBrowserRecovery()
    {
        var browserRecoveryCalls = 0;
        using var http = new HttpClient(new Handler(_ => Task.FromResult("""{"retcode":-100,"data":null}""")));
        var client = new MiyousheClient(http, "device")
        {
            CredentialRecovery = (_, _) => { browserRecoveryCalls++; return Task.FromResult(true); }
        };
        var error = await Assert.ThrowsAsync<ApiFailure>(() => client.CreateQrLoginAsync());
        Assert.Equal(FailureKind.Login, error.Kind);
        Assert.Equal(0, browserRecoveryCalls);
    }
}
