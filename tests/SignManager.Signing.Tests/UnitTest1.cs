using SignManager.Signing.Zsign;

namespace SignManager.Signing.Tests;

public class ZsignArgumentBuilderTests
{
    [Fact]
    public void Build_ShouldAppendExtensionRemovalFlag_WhenEnabled()
    {
        var args = ZsignArgumentBuilder.Build(new ZsignRequest(
            SourceIpaPath: "source.ipa",
            OutputIpaPath: "output.ipa",
            PrivateKeyPath: "private-key.pem",
            CertificatePath: "certificate.pem",
            MobileProvisionPath: "profile.mobileprovision",
            EffectiveBundleId: "com.eddie.sideload.qrscanner",
            RemoveExtensions: true));

        Assert.Contains("-E", args);
        Assert.Equal("source.ipa", args[^1]);
    }

    [Fact]
    public void Build_ShouldNotAppendExtensionRemovalFlag_WhenDisabled()
    {
        var args = ZsignArgumentBuilder.Build(new ZsignRequest(
            SourceIpaPath: "source.ipa",
            OutputIpaPath: "output.ipa",
            PrivateKeyPath: "private-key.pem",
            CertificatePath: "certificate.pem",
            MobileProvisionPath: "profile.mobileprovision",
            EffectiveBundleId: "com.eddie.sideload.qrscanner",
            RemoveExtensions: false));

        Assert.DoesNotContain("-E", args);
    }
}
