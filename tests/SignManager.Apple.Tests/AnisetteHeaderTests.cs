using System.Net;
using System.Net.Http.Json;
using SignManager.Apple.Anisette;
using SignManager.Apple.Tests.Fixtures;

namespace SignManager.Apple.Tests;

public class AnisetteHeaderTests
{
    [Fact]
    public async Task HttpProvider_ShouldThrow_WhenRequiredHeaderMissing()
    {
        var handler = new StubMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = JsonContent.Create(AnisetteFixtures.MissingRequiredHeaders),
        });

        using var client = new HttpClient(handler)
        {
            BaseAddress = new Uri("http://localhost/"),
        };

        var provider = new HttpAnisetteProvider(client);
        await Assert.ThrowsAsync<InvalidOperationException>(() => provider.GetHeadersAsync(CancellationToken.None));
    }

    [Fact]
    public async Task HttpProvider_ShouldParseHeaders_WhenValid()
    {
        var handler = new StubMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = JsonContent.Create(AnisetteFixtures.ValidHeaders),
        });

        using var client = new HttpClient(handler)
        {
            BaseAddress = new Uri("http://localhost/"),
        };

        var provider = new HttpAnisetteProvider(client);
        var headers = await provider.GetHeadersAsync(CancellationToken.None);

        Assert.Equal("md", headers.XAppleIMd);
        Assert.Equal("client", headers.XMmeClientInfo);
    }

    private sealed class StubMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> responder) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(responder(request));
    }
}