using SignManager.Signing.Ipa;
using SignManager.Signing.Tests.Fixtures;

namespace SignManager.Signing.Tests;

public class SourceIpaManagerTests
{
    [Fact]
    public async Task ReplaceSourceAsync_ShouldWriteImmutableSourceAndMetadata()
    {
        var root = CreateTempRoot();

        try
        {
            var uploadedIpaPath = IpaFixtureBuilder.CreateValidIpa(root);
            var immutablePath = Path.Combine(root, "sources", "app1", "source.ipa");
            var manager = new SourceIpaManager(new IpaPreflightService());

            var result = await manager.ReplaceSourceAsync(
                uploadedIpaPath,
                immutablePath,
                new IpaPreflightLimits(),
                CancellationToken.None);

            Assert.True(File.Exists(result.Path));
            Assert.Equal("com.vendor.test", result.Metadata.SourceBundleId);
            Assert.NotEmpty(result.Sha256);
        }
        finally
        {
            Cleanup(root);
        }
    }

    [Fact]
    public async Task CopyImmutableSourceToJobAsync_ShouldCopyWithoutMutatingOriginal()
    {
        var root = CreateTempRoot();

        try
        {
            var uploadedIpaPath = IpaFixtureBuilder.CreateValidIpa(root);
            var immutablePath = Path.Combine(root, "sources", "app1", "source.ipa");
            var jobPath = Path.Combine(root, "jobs", "job1", "source.ipa");
            var manager = new SourceIpaManager(new IpaPreflightService());

            await manager.ReplaceSourceAsync(uploadedIpaPath, immutablePath, new IpaPreflightLimits(), CancellationToken.None);

            var beforeSize = new FileInfo(immutablePath).Length;
            await manager.CopyImmutableSourceToJobAsync(immutablePath, jobPath, CancellationToken.None);
            var afterSize = new FileInfo(immutablePath).Length;

            Assert.True(File.Exists(jobPath));
            Assert.Equal(beforeSize, afterSize);
            Assert.Equal(new FileInfo(immutablePath).Length, new FileInfo(jobPath).Length);
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
