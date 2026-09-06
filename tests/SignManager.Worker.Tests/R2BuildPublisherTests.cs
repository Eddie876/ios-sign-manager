using Microsoft.Extensions.Options;
using System.Security.Cryptography;
using SignManager.Core.Models;
using SignManager.Infrastructure.Ota;
using SignManager.Infrastructure.R2;
using SignManager.Worker.Signing;

namespace SignManager.Worker.Tests;

public class R2BuildPublisherTests
{
    [Fact]
    public async Task PublishAsync_ShouldUseConfiguredPublicBaseUrl()
    {
        var root = CreateTempRoot();

        try
        {
            var signedIpaPath = Path.Combine(root, "signed.ipa");
            var signedIpa = new byte[] { 1, 2, 3, 4 };
            await File.WriteAllBytesAsync(signedIpaPath, signedIpa);
            var sha256 = Convert.ToHexString(SHA256.HashData(signedIpa)).ToLowerInvariant();

            var options = new SchedulerOptions(OtaPublicBaseUrl: "https://cdn.example.com");
            var publisher = new R2BuildPublisher(
                new R2ReleasePublisher(new InMemoryR2ObjectStore(), new R2ObjectKeyPlanner(), new OtaManifestGenerator()),
                new R2ObjectKeyPlanner(),
                Options.Create(options));

            var result = await publisher.PublishAsync(CreateRequest(signedIpaPath, sha256, signedIpa.LongLength), CancellationToken.None);

            Assert.StartsWith("https://cdn.example.com/apps/", result.LatestManifestUrl, StringComparison.Ordinal);
            Assert.Contains("itms-services://", result.InstallUrl, StringComparison.Ordinal);
            Assert.Contains("cdn.example.com", result.InstallUrl, StringComparison.Ordinal);
        }
        finally
        {
            Cleanup(root);
        }
    }

    [Fact]
    public async Task PublishAsync_ShouldThrow_WhenPublicBaseUrlIsNotHttps()
    {
        var root = CreateTempRoot();

        try
        {
            var signedIpaPath = Path.Combine(root, "signed.ipa");
            var signedIpa = new byte[] { 1, 2, 3, 4 };
            await File.WriteAllBytesAsync(signedIpaPath, signedIpa);
            var sha256 = Convert.ToHexString(SHA256.HashData(signedIpa)).ToLowerInvariant();

            var options = new SchedulerOptions(OtaPublicBaseUrl: "http://cdn.example.com");
            var publisher = new R2BuildPublisher(
                new R2ReleasePublisher(new InMemoryR2ObjectStore(), new R2ObjectKeyPlanner(), new OtaManifestGenerator()),
                new R2ObjectKeyPlanner(),
                Options.Create(options));

            var ex = await Assert.ThrowsAsync<SigningWorkflowException>(() =>
                publisher.PublishAsync(CreateRequest(signedIpaPath, sha256, signedIpa.LongLength), CancellationToken.None));

            Assert.Equal("R2_UPLOAD_FAILED", ex.ErrorCode);
            Assert.Contains("HTTPS", ex.Message, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            Cleanup(root);
        }
    }

    private static BuildPublishRequest CreateRequest(string signedIpaPath, string sha256, long sizeBytes)
    {
        var now = new DateTimeOffset(2026, 9, 6, 0, 0, 0, TimeSpan.Zero);
        var job = new SigningJob("job-1", "app1", "sha", SigningJobType.Auto, now, SigningJobStatus.Ready, 1);
        var app = new ManagedAppConfig(
            Id: "app1",
            Name: "App One",
            Enabled: true,
            Source: new SourceArtifact("source.ipa", "sha", now),
            Identity: new BundleIdentity("com.vendor.app", "com.eddie.sideload.app1"),
            Signing: new AppSigningConfig(true),
            Schedule: new AppScheduleConfig(true, 48),
            Publish: new AppPublishConfig("app1"));
        var build = new BuildInfo(
            BuildId: "build-1",
            AppId: "app1",
            Sha256: sha256,
            SizeBytes: sizeBytes,
            CreatedAt: now,
            Provisioning: new ProvisioningInfo(
                Uuid: "profile-uuid",
                CreationDate: now,
                ExpirationDate: now.AddDays(7),
                BundleId: "com.eddie.sideload.app1",
                TeamId: "TEAM123",
                DeviceUdids: ["UDID-1"]));

        return new BuildPublishRequest(job, app, build, signedIpaPath, now);
    }

    private static string CreateTempRoot()
    {
        var root = Path.Combine(Path.GetTempPath(), "sign-manager-worker-r2-publisher-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        return root;
    }

    private static void Cleanup(string root)
    {
        if (Directory.Exists(root))
        {
            Directory.Delete(root, recursive: true);
        }
    }
}
