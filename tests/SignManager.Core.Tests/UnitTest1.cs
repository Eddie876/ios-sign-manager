using SignManager.Core.Constants;
using SignManager.Core.Models;
using SignManager.Core.Policies;
using SignManager.Core.Services;

namespace SignManager.Core.Tests;

public class PolicyTests
{
    [Fact]
    public void ProfileFreshness_ShouldRequireIncreasingCreationAndExpiration()
    {
        var policy = new ProfileFreshnessPolicy(minimumFreshHours: 24);
        var now = new DateTimeOffset(2026, 9, 6, 0, 0, 0, TimeSpan.Zero);
        var previous = new ProvisioningInfo(
            Uuid: "old",
            CreationDate: now.AddDays(-2),
            ExpirationDate: now.AddDays(3),
            BundleId: "com.example.app",
            TeamId: "TEAM",
            DeviceUdids: ["udid-1"]);

        var current = previous with
        {
            Uuid = "new",
            CreationDate = now,
            ExpirationDate = now.AddDays(7),
        };

        Assert.True(policy.IsFresh(current, previous, now));
    }

    [Fact]
    public void RetryPolicy_ShouldNotRetryAuthRequired()
    {
        var policy = new RetryPolicy();
        Assert.False(policy.IsRetryable(StableErrorCodes.AuthRequired));
    }

    [Fact]
    public void RefreshPlanner_ShouldBeDue_WhenNoStateExists()
    {
        var planner = new RefreshPlanner();
        var app = new ManagedAppConfig(
            Id: "qr-scanner",
            Name: "QR Scanner",
            Enabled: true,
            Source: new SourceArtifact("/data/source.ipa", "abc", DateTimeOffset.UtcNow),
            Identity: new BundleIdentity("com.source", "com.target"),
            Signing: new AppSigningConfig(true),
            Schedule: new AppScheduleConfig(true, 48),
            Publish: new AppPublishConfig("qr-scanner"));

        Assert.True(planner.IsSignDue(app, runtimeState: null, DateTimeOffset.UtcNow));
    }
}
