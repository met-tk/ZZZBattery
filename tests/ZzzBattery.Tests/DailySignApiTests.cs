using System.Net;
using System.Text;
using System.Text.Json;
using Xunit;
using ZzzBattery.Core;

namespace ZzzBattery.Tests;

public sealed class DailySignApiTests
{
    private static Credential Account() => new() { Cookie = "account_id=123; cookie_token=test", AccountKey = "test",
        Role = new("100000001", "prod_gf_cn", "", "") };
    private static HttpResponseMessage Json(string text) => new(HttpStatusCode.OK) { Content = new StringContent(text, Encoding.UTF8, "application/json") };
    private sealed class Handler(Func<HttpRequestMessage, Task<HttpResponseMessage>> send) : HttpMessageHandler
    {
        public int Calls;
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token) { Calls++; return send(request); }
    }
    [Fact]
    public async Task ReadsSelectedRoleAndUsesSeparateSignProfile()
    {
        using var handler = new Handler(request =>
        {
            Assert.Equal(HttpMethod.Get, request.Method);
            Assert.Equal(MiyousheClient.DailySignInfoEndpoint, request.RequestUri!.GetLeftPart(UriPartial.Path));
            Assert.Equal("?act_id=e202406242138391&lang=zh-cn&region=prod_gf_cn&uid=100000001", request.RequestUri.Query);
            Assert.Equal("zzz", request.Headers.GetValues("x-rpc-signgame").Single());
            Assert.Equal("persistent-device", request.Headers.GetValues("x-rpc-device_id").Single());
            Assert.Equal(MiyousheClient.DailySignAppVersion, request.Headers.GetValues("x-rpc-app_version").Single());
            Assert.Equal(MiyousheClient.DailySignPage, request.Headers.Referrer!.AbsoluteUri);
            var ds = request.Headers.GetValues("DS").Single().Split(',');
            Assert.Equal(MiyousheClient.SignDailyRequest(long.Parse(ds[0]), ds[1]), string.Join(',', ds));
            return Task.FromResult(Json("""{"retcode":0,"data":{"is_sign":true,"today":"2026-10-20"}}"""));
        });
        using var http = new HttpClient(handler);
        var info = await new MiyousheClient(http, "persistent-device").GetDailySignInfoAsync(Account());
        Assert.True(info.IsSigned); Assert.Equal(new DateOnly(2026, 10, 20), info.Day); Assert.Equal(1, handler.Calls);
    }
    [Fact]
    public async Task PostsOnlySelectedRoleWithExactActivity()
    {
        using var handler = new Handler(async request =>
        {
            Assert.Equal(HttpMethod.Post, request.Method);
            Assert.Equal(MiyousheClient.DailySignSubmitEndpoint, request.RequestUri!.AbsoluteUri);
            using var doc = JsonDocument.Parse(await request.Content!.ReadAsStringAsync());
            var data = doc.RootElement;
            Assert.Equal(MiyousheClient.DailySignActivity, data.GetProperty("act_id").GetString());
            Assert.Equal("prod_gf_cn", data.GetProperty("region").GetString());
            Assert.Equal("100000001", data.GetProperty("uid").GetString());
            Assert.False(request.Headers.Contains("x-rpc-challenge"));
            return Json("""{"retcode":0,"data":{"is_risk":false}}""");
        });
        using var http = new HttpClient(handler);
        Assert.Equal(DailySignSubmitResult.Submitted, await new MiyousheClient(http, "device").SubmitDailySignAsync(Account()));
        Assert.Equal(1, handler.Calls);
    }
    [Theory]
    [InlineData("""{"is_risk":true}""", FailureKind.Verification)]
    [InlineData("""{"is_risk":false,"gt":"challenge-key","challenge":"challenge"}""", FailureKind.Verification)]
    [InlineData("""{"is_risk":"true"}""", FailureKind.Protocol)]
    public async Task RetcodeZeroWithRiskDoesNotMeanSuccess(string data, FailureKind kind)
    {
        using var handler = new Handler(_ => Task.FromResult(Json("""{"retcode":0,"data":""" + data + "}")));
        using var http = new HttpClient(handler);
        var error = await Assert.ThrowsAsync<ApiFailure>(() => new MiyousheClient(http, "device").SubmitDailySignAsync(Account()));
        Assert.Equal(kind, error.Kind); Assert.Equal(1, handler.Calls);
    }
    [Theory]
    [InlineData(10035, FailureKind.Verification)]
    [InlineData(1034, FailureKind.Verification)]
    [InlineData(10041, FailureKind.Permission)]
    [InlineData(5003, FailureKind.Permission)]
    [InlineData(-100, FailureKind.Login)]
    [InlineData(10101, FailureKind.RateLimited)]
    [InlineData(30001, FailureKind.Protocol)]
    public async Task ErrorsStopWithoutFallbackOrRetry(int code, FailureKind expected)
    {
        using var handler = new Handler(_ => Task.FromResult(Json($$"""{"retcode":{{code}},"data":null}""")));
        using var http = new HttpClient(handler);
        var error = await Assert.ThrowsAsync<ApiFailure>(() => new MiyousheClient(http, "device").SubmitDailySignAsync(Account()));
        Assert.Equal(expected, error.Kind); Assert.Equal(code, error.Code);
        Assert.DoesNotContain("电量", error.Message); Assert.Null(error.VerificationPath); Assert.Equal(1, handler.Calls);
    }
    [Theory]
    [InlineData("""{"is_sign":true}""")]
    [InlineData("""{"is_sign":"true","today":"2026-10-20"}""")]
    [InlineData("""{"is_sign":true,"today":"not-a-date"}""")]
    public async Task MissingOrUnclearInfoCannotBePromotedToSigned(string data)
    {
        using var handler = new Handler(_ => Task.FromResult(Json("""{"retcode":0,"data":""" + data + "}")));
        using var http = new HttpClient(handler);
        var error = await Assert.ThrowsAsync<ApiFailure>(() => new MiyousheClient(http, "device").GetDailySignInfoAsync(Account()));
        Assert.Equal(FailureKind.Protocol, error.Kind);
    }
    [Fact]
    public async Task DuplicateResponseRequiresSessionConfirmation()
    {
        using var handler = new Handler(_ => Task.FromResult(Json("""{"retcode":-5003,"data":null}""")));
        using var http = new HttpClient(handler);
        Assert.Equal(DailySignSubmitResult.AlreadySigned, await new MiyousheClient(http, "device").SubmitDailySignAsync(Account()));
        Assert.Equal(1, handler.Calls);
    }
    [Fact]
    public async Task HonorsServerRetryAfter()
    {
        using var handler = new Handler(_ =>
        {
            var response = new HttpResponseMessage(HttpStatusCode.TooManyRequests);
            response.Headers.RetryAfter = new(TimeSpan.FromMinutes(25));
            return Task.FromResult(response);
        });
        using var http = new HttpClient(handler);
        var error = await Assert.ThrowsAsync<ApiFailure>(() => new MiyousheClient(http, "device").SubmitDailySignAsync(Account()));
        Assert.Equal(FailureKind.RateLimited, error.Kind); Assert.Equal(TimeSpan.FromMinutes(25), error.RetryAfter); Assert.Equal(1, handler.Calls);
    }
    [Fact]
    public async Task RejectsUnselectedAndForeignRegionBeforeNetwork()
    {
        using var handler = new Handler(_ => throw new Exception("No network expected."));
        using var http = new HttpClient(handler); var client = new MiyousheClient(http, "device");
        var account = Account(); account.Role = null;
        await Assert.ThrowsAsync<ApiFailure>(() => client.SubmitDailySignAsync(account));
        account.Role = new("100", "prod_gf_us", "", "");
        await Assert.ThrowsAsync<ApiFailure>(() => client.SubmitDailySignAsync(account));
        Assert.Equal(0, handler.Calls);
    }
}
