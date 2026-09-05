using SignManager.Core.Constants;
using SignManager.Signing.Ipa;
using SignManager.Signing.Tests.Fixtures;

namespace SignManager.Signing.Tests;

public class IpaPreflightServiceTests
{
    [Fact]
    public async Task ValidateAndExtract_ShouldReturnMetadata_ForValidIpa()
    {
        var root = CreateTempRoot();

        try
        {
            var ipaPath = IpaFixtureBuilder.CreateValidIpa(root);
            var service = new IpaPreflightService();

            var result = await service.ValidateAndExtractAsync(
                new IpaPreflightRequest(ipaPath, new IpaPreflightLimits()),
                CancellationToken.None);

            Assert.Equal("Test App", result.Metadata.Name);
            Assert.Equal("com.vendor.test", result.Metadata.SourceBundleId);
            Assert.Equal("1.2.3", result.Metadata.Version);
            Assert.Equal("100", result.Metadata.Build);
            Assert.Contains("Share.appex", result.Metadata.Extensions);
            Assert.Contains("application-identifier", result.Metadata.Entitlements);
            Assert.True(result.Metadata.HasWatchApp);
            Assert.True(result.Metadata.HasAppClips);
            Assert.NotEmpty(result.Sha256);
        }
        finally
        {
            Cleanup(root);
        }
    }

    [Fact]
    public async Task ValidateAndExtract_ShouldRejectPathTraversal()
    {
        var root = CreateTempRoot();

        try
        {
            var ipaPath = IpaFixtureBuilder.CreatePathTraversalIpa(root);
            var service = new IpaPreflightService();

            var ex = await Assert.ThrowsAsync<IpaPreflightException>(() =>
                service.ValidateAndExtractAsync(new IpaPreflightRequest(ipaPath, new IpaPreflightLimits()), CancellationToken.None));

            Assert.Equal(StableErrorCodes.ZipPathTraversal, ex.ErrorCode);
        }
        finally
        {
            Cleanup(root);
        }
    }

    [Fact]
    public async Task ValidateAndExtract_ShouldRejectMultipleMainApps()
    {
        var root = CreateTempRoot();

        try
        {
            var ipaPath = IpaFixtureBuilder.CreateMultipleMainAppsIpa(root);
            var service = new IpaPreflightService();

            var ex = await Assert.ThrowsAsync<IpaPreflightException>(() =>
                service.ValidateAndExtractAsync(new IpaPreflightRequest(ipaPath, new IpaPreflightLimits()), CancellationToken.None));

            Assert.Equal(StableErrorCodes.InvalidIpa, ex.ErrorCode);
        }
        finally
        {
            Cleanup(root);
        }
    }

    private static string CreateTempRoot()
    {
        var root = Path.Combine(Path.GetTempPath(), "sign-manager-signing-tests", Guid.NewGuid().ToString("N"));
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
