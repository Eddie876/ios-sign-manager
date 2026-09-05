using System.Net;
using System.Text;
using SignManager.Apple.Developer;
using SignManager.Apple.Tests.Fixtures;

namespace SignManager.Apple.Tests;

public class HttpAppleDeveloperClientTests
{
    [Fact]
    public async Task ViewDeveloper_ShouldReturnTrue_WhenResponseIsValid()
    {
        var sut = new HttpAppleDeveloperClient(CreateClient([
            new RouteResponse("/developer/view", "{\"valid\":true}")
        ]));

        var ok = await sut.ViewDeveloperAsync(CancellationToken.None);
        Assert.True(ok);
    }

    [Fact]
    public async Task GetTeams_ShouldMapTeamList()
    {
        var sut = new HttpAppleDeveloperClient(CreateClient([
            new RouteResponse("/developer/teams", "{\"teams\":[{\"teamId\":\"T1\",\"name\":\"Personal\"}]}")
        ]));

        var teams = await sut.GetTeamsAsync(CancellationToken.None);
        Assert.Single(teams);
        Assert.Equal("T1", teams[0].TeamId);
    }

    [Fact]
    public async Task EnsureDevice_ShouldMapResponse()
    {
        var sut = new HttpAppleDeveloperClient(CreateClient([
            new RouteResponse("/developer/devices/ensure", DeveloperFixtures.Device)
        ]));

        var device = await sut.EnsureDeviceAsync("TEAM", "UDID-1", "Eddie", CancellationToken.None);
        Assert.Equal("device-1", device.DeviceId);
    }

    [Fact]
    public async Task EnsureCertificate_ShouldMapResponse()
    {
        var sut = new HttpAppleDeveloperClient(CreateClient([
            new RouteResponse("/developer/certificates/ensure", DeveloperFixtures.Certificate)
        ]));

        var certificate = await sut.EnsureCertificateAsync("TEAM", "CSR", CancellationToken.None);
        Assert.Equal("cert-1", certificate.CertificateId);
    }

    [Fact]
    public async Task EnsureAppId_ShouldMapResponse()
    {
        var sut = new HttpAppleDeveloperClient(CreateClient([
            new RouteResponse("/developer/appids/ensure", DeveloperFixtures.AppId)
        ]));

        var appId = await sut.EnsureAppIdAsync("TEAM", "com.eddie.sideload.qrscanner", CancellationToken.None);
        Assert.Equal("app-1", appId.AppIdId);
    }

    [Fact]
    public async Task CreateProfile_ShouldMapResponse()
    {
        var profilePayload = DeveloperFixtures.Profile(Convert.ToBase64String(ProvisioningProfileFixture.AsMobileProvisionBytes()));
        var sut = new HttpAppleDeveloperClient(CreateClient([
            new RouteResponse("/developer/profiles/create", profilePayload)
        ]));

        var profile = await sut.CreateProvisioningProfileAsync("TEAM123", "app-1", "device-1", "p1", CancellationToken.None);
        Assert.Equal("profile-1", profile.ProfileId);
    }

    private static HttpClient CreateClient(IReadOnlyList<RouteResponse> routes)
    {
        var handler = new StubMessageHandler(request =>
        {
            var path = request.RequestUri?.AbsolutePath;
            var route = routes.FirstOrDefault(x => x.Path == path);
            if (route is null)
            {
                return new HttpResponseMessage(HttpStatusCode.NotFound);
            }

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(route.ResponseJson, Encoding.UTF8, "application/json"),
            };
        });

        return new HttpClient(handler)
        {
            BaseAddress = new Uri("https://apple.local/"),
        };
    }

    private sealed record RouteResponse(string Path, string ResponseJson);

    private sealed class StubMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> responder) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(responder(request));
    }
}
