using SignManager.Apple.Developer;
using SignManager.Apple.Tests.Fixtures;

namespace SignManager.Apple.Tests;

public class ProvisioningProfileParserTests
{
    [Fact]
    public void Parse_ShouldExtractCoreFields()
    {
        var parser = new ProvisioningProfileParser();

        var profile = parser.Parse(ProvisioningProfileFixture.AsMobileProvisionBytes());

        Assert.Equal("11111111-2222-3333-4444-555555555555", profile.Uuid);
        Assert.Equal("TEAM123", profile.TeamId);
        Assert.Equal("com.eddie.sideload.qrscanner", profile.BundleId);
        Assert.Equal(2, profile.DeviceUdids.Count);
    }
}
