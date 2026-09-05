using System.Net;
using System.Text;
using SignManager.Apple.Developer;

namespace SignManager.Apple.Tests;

public class HttpAppleDeveloperClientTests
{
    [Fact]
    public async Task ViewDeveloper_ShouldReturnTrue_WhenResponseIsValid()
    {
        var client = CreateClient("/developer/view", "{\"valid\":true}");
        var sut = new HttpAppleDeveloperClient(client);

        var ok = await sut.ViewDeveloperAsync(CancellationToken.None);
        Assert.True(ok);
    }

    [Fact]
    public async Task GetTeams_ShouldMapTeamList()
    {
        var client = CreateClient("/developer/teams", "{\"teams\":[{\"teamId\":\"T1\",\"name\":\"Personal\"}]}");
        var sut = new HttpAppleDeveloperClient(client);

        var teams = await sut.GetTeamsAsync(CancellationToken.None);
        Assert.Single(teams);
        Assert.Equal("T1", teams[0].TeamId);
    }

    private static HttpClient CreateClient(string expectedPath, string responseJson)
    {
        var handler = new StubMessageHandler(request =>
        {
            if (request.RequestUri?.AbsolutePath != expectedPath)
            {
                return new HttpResponseMessage(HttpStatusCode.NotFound);
            }

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(responseJson, Encoding.UTF8, "application/json"),
            };
        });

        return new HttpClient(handler)
        {
            BaseAddress = new Uri("https://apple.local/"),
        };
    }

    private sealed class StubMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> responder) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(responder(request));
    }
}
