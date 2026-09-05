using SignManager.Infrastructure.Ota;
using SignManager.Infrastructure.R2;

namespace SignManager.IntegrationTests;

public class Milestone4OtaTests
{
    [Fact]
    public void OtaManifest_ShouldContainRequiredMetadata()
    {
        var generator = new OtaManifestGenerator();

        var xml = generator.GenerateXml(new OtaManifestRequest(
            PackageUrl: "https://ios.example.com/apps/ns/qr/builds/1/app.ipa",
            BundleIdentifier: "com.eddie.sideload.qrscanner",
            BundleVersion: "100",
            Title: "QR Scanner"));

        Assert.Contains("bundle-identifier", xml, StringComparison.Ordinal);
        Assert.Contains("com.eddie.sideload.qrscanner", xml, StringComparison.Ordinal);
        Assert.Contains("software-package", xml, StringComparison.Ordinal);
    }

    [Fact]
    public void InstallUrl_ShouldUseItmsServicesScheme()
    {
        var url = ItmsServicesUrlBuilder.BuildInstallUrl("https://ios.example.com/latest/manifest.plist");

        Assert.StartsWith("itms-services://?action=download-manifest&url=", url, StringComparison.Ordinal);
        Assert.Contains("https%3A%2F%2Fios.example.com%2Flatest%2Fmanifest.plist", url, StringComparison.Ordinal);
    }

    [Fact]
    public void R2Plan_ShouldFollowImmutableThenLatestOrder()
    {
        var planner = new R2ObjectKeyPlanner();
        var plan = planner.BuildPlan("abcdefabcdefabcdefabcdefabcdefab", "qr-scanner", "20260906T000000Z");

        Assert.Equal("apps/abcdefabcdefabcdefabcdefabcdefab/qr-scanner/builds/20260906T000000Z/app.ipa", plan.VersionedIpaKey);
        Assert.Equal("apps/abcdefabcdefabcdefabcdefabcdefab/qr-scanner/latest/latest.json", plan.LatestJsonKey);
        Assert.Equal(plan.VersionedIpaKey, plan.PublishOrderKeys[0]);
        Assert.Equal(plan.LatestJsonKey, plan.PublishOrderKeys[^1]);
    }

    [Fact]
    public void RandomNamespace_ShouldBe128BitHex()
    {
        var planner = new R2ObjectKeyPlanner();
        var nsValue = planner.CreateRandomNamespace();

        Assert.Equal(32, nsValue.Length);
        Assert.Matches("^[0-9a-f]{32}$", nsValue);
    }
}
