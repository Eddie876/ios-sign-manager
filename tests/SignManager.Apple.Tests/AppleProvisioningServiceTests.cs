using SignManager.Apple.Developer;
using SignManager.Apple.Tests.Fixtures;
using SignManager.Core.Models;
using SignManager.Core.Policies;

namespace SignManager.Apple.Tests;

public class AppleProvisioningServiceTests
{
    [Fact]
    public async Task EnsureProvisioning_ShouldReturnFreshResult_WhenProfileMovesForward()
    {
        var profileBytes = ProvisioningProfileFixture.AsMobileProvisionBytes();
        var client = new FakeDeveloperClient(Convert.ToBase64String(profileBytes));

        var service = new AppleProvisioningService(
            client,
            new FakeCsrGenerator(),
            new ProvisioningProfileParser(),
            new ProfileFreshnessPolicy(24));

        var previous = new ProvisioningInfo(
            "old",
            new DateTimeOffset(2026, 9, 1, 0, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 9, 8, 0, 0, 0, TimeSpan.Zero),
            "com.eddie.sideload.qrscanner",
            "TEAM123",
            ["UDID-1"]);

        var result = await service.EnsureProvisioningAsync(
            teamId: "TEAM123",
            udid: "UDID-1",
            deviceName: "Eddie iPhone",
            bundleId: "com.eddie.sideload.qrscanner",
            profileName: "qr-profile",
            privateKeyPassword: "password",
            previous: previous,
            now: new DateTimeOffset(2026, 9, 6, 0, 0, 0, TimeSpan.Zero),
            cancellationToken: CancellationToken.None);

        Assert.True(result.IsFresh);
        Assert.Equal("device-1", result.Device.DeviceId);
        Assert.Equal("app-1", result.AppId.AppIdId);
    }

    private sealed class FakeCsrGenerator : ICsrGenerator
    {
        public GeneratedCsr Generate(string commonName, string exportPassword)
            => new("CSR", "ENCRYPTED-KEY");
    }

    private sealed class FakeDeveloperClient(string mobileProvisionBase64) : IAppleDeveloperClient
    {
        public Task<bool> ViewDeveloperAsync(CancellationToken cancellationToken) => Task.FromResult(true);

        public Task<IReadOnlyList<DeveloperTeam>> GetTeamsAsync(CancellationToken cancellationToken)
            => Task.FromResult<IReadOnlyList<DeveloperTeam>>([new("TEAM123", "Personal")]);

        public Task<RegisteredDevice> EnsureDeviceAsync(string teamId, string udid, string deviceName, CancellationToken cancellationToken)
            => Task.FromResult(new RegisteredDevice("device-1", udid, deviceName));

        public Task<DevelopmentCertificate> EnsureCertificateAsync(string teamId, string csrPem, CancellationToken cancellationToken)
            => Task.FromResult(new DevelopmentCertificate("cert-1", "serial-1", "PEM", DateTimeOffset.UtcNow.AddDays(6)));

        public Task<AppIdentifier> EnsureAppIdAsync(string teamId, string bundleId, CancellationToken cancellationToken)
            => Task.FromResult(new AppIdentifier("app-1", bundleId, "App"));

        public Task<ProvisioningProfile> CreateProvisioningProfileAsync(string teamId, string appIdId, string deviceId, string profileName, CancellationToken cancellationToken)
            => Task.FromResult(new ProvisioningProfile(
                ProfileId: "profile-1",
                Uuid: "11111111-2222-3333-4444-555555555555",
                CreationDate: new DateTimeOffset(2026, 9, 6, 0, 0, 0, TimeSpan.Zero),
                ExpirationDate: new DateTimeOffset(2026, 9, 13, 0, 0, 0, TimeSpan.Zero),
                TeamId: teamId,
                BundleId: "com.eddie.sideload.qrscanner",
                MobileProvisionBase64: mobileProvisionBase64));
    }
}
