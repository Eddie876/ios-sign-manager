using System.Net;
using System.Text;
using SignManager.Apple.Anisette;
using SignManager.Apple.Auth;
using SignManager.Apple.Tests.Fixtures;
using SignManager.Core.Constants;

namespace SignManager.Apple.Tests;

public class GrandSlamHttpClientTests
{
    [Fact]
    public async Task Login_ShouldMapSuccessResponse()
    {
        var client = CreateClient(GrandSlamFixtures.Success);
        var sut = new GrandSlamHttpClient(client, new FakeAnisetteProvider(shouldFail: false));

        var result = await sut.LoginAsync(new AppleLoginRequest("a@b.com", "pw", null), CancellationToken.None);

        Assert.True(result.Success);
        Assert.False(result.RequiresTwoFactor);
        Assert.Equal("123456789", result.Session?.AdsId);
    }

    [Fact]
    public async Task Login_ShouldMapTwoFactorRequired()
    {
        var client = CreateClient(GrandSlamFixtures.TwoFactorRequired);
        var sut = new GrandSlamHttpClient(client, new FakeAnisetteProvider(shouldFail: false));

        var result = await sut.LoginAsync(new AppleLoginRequest("a@b.com", "pw", null), CancellationToken.None);

        Assert.False(result.Success);
        Assert.True(result.RequiresTwoFactor);
    }

    [Fact]
    public async Task Login_ShouldMapFailureCode()
    {
        var client = CreateClient(GrandSlamFixtures.Failure);
        var sut = new GrandSlamHttpClient(client, new FakeAnisetteProvider(shouldFail: false));

        var result = await sut.LoginAsync(new AppleLoginRequest("a@b.com", "pw", null), CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal(StableErrorCodes.AppleLoginFailed, result.ErrorCode);
    }

    [Fact]
    public async Task Login_ShouldReturnAnisetteUnavailable_WhenProviderFails()
    {
        var client = CreateClient(GrandSlamFixtures.Success);
        var sut = new GrandSlamHttpClient(client, new FakeAnisetteProvider(shouldFail: true));

        var result = await sut.LoginAsync(new AppleLoginRequest("a@b.com", "pw", null), CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal(StableErrorCodes.AnisetteUnavailable, result.ErrorCode);
    }

    [Fact]
    public async Task SubmitTwoFactor_ShouldCallTwoFactorEndpoint()
    {
        string? capturedPath = null;
        string? capturedAnisetteHeader = null;

        var handler = new StubMessageHandler(request =>
        {
            capturedPath = request.RequestUri?.AbsolutePath;
            capturedAnisetteHeader = request.Headers.TryGetValues("X-Apple-I-MD", out var values)
                ? values.FirstOrDefault()
                : null;
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(GrandSlamFixtures.Success, Encoding.UTF8, "application/json"),
            };
        });

        using var client = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://example.local/"),
        };

        var sut = new GrandSlamHttpClient(client, new FakeAnisetteProvider(shouldFail: false));
        var result = await sut.SubmitTwoFactorAsync(new AppleTwoFactorRequest("a@b.com", "123456"), CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal("/grandslam/2fa", capturedPath);
        Assert.Equal("md", capturedAnisetteHeader);
    }

    private static HttpClient CreateClient(string json)
    {
        var handler = new StubMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json"),
        });

        return new HttpClient(handler)
        {
            BaseAddress = new Uri("https://example.local/"),
        };
    }

    private sealed class StubMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> responder) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(responder(request));
    }

    private sealed class FakeAnisetteProvider(bool shouldFail) : IAnisetteProvider
    {
        public Task<AnisetteHeaders> GetHeadersAsync(CancellationToken cancellationToken)
        {
            if (shouldFail)
            {
                throw new InvalidOperationException("anisette unavailable");
            }

            return Task.FromResult(new AnisetteHeaders(
                XAppleIMd: "md",
                XAppleIMdM: "mdm",
                XAppleIMdLu: "mdlu",
                XMmeDeviceId: "device",
                XMmeClientInfo: "client"));
        }
    }
}
