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
        using var http = new HttpClient(new Handler(request =>
        {
            Assert.Equal(new Uri(MiyousheClient.CookieExchangeEndpoint).Host, request.RequestUri!.Host);
            Assert.Equal("2", Assert.Single(request.Headers.GetValues("x-rpc-client_type")));
            return Task.FromResult(JsonSerializer.Serialize(new { retcode = 0, data = new { uid = responseId, cookie_token = "cookie-test" } }));
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
}
