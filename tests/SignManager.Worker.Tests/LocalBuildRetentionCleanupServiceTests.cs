using SignManager.Worker.Operations;

namespace SignManager.Worker.Tests;

public class LocalBuildRetentionCleanupServiceTests
{
    [Fact]
    public void Cleanup_ShouldKeepLatestNPerApp_AndDeleteOlderBuilds()
    {
        var root = CreateTempRoot();

        try
        {
            var appRoot = Path.Combine(root, "app1");
            Directory.CreateDirectory(appRoot);

            var oldBuild = CreateBuildDirectory(appRoot, "build-1", DateTimeOffset.UtcNow.AddHours(-10));
            var midBuild = CreateBuildDirectory(appRoot, "build-2", DateTimeOffset.UtcNow.AddHours(-5));
            var newBuild = CreateBuildDirectory(appRoot, "build-3", DateTimeOffset.UtcNow.AddHours(-1));

            var service = new LocalBuildRetentionCleanupService();
            var result = service.Cleanup(root, keepLatestPerApp: 2);

            Assert.Equal(3, result.ScannedDirectories);
            Assert.Equal(1, result.DeletedDirectories);
            Assert.Equal(0, result.FailedDirectories);
            Assert.False(Directory.Exists(oldBuild));
            Assert.True(Directory.Exists(midBuild));
            Assert.True(Directory.Exists(newBuild));
        }
        finally
        {
            Cleanup(root);
        }
    }

    private static string CreateBuildDirectory(string appRoot, string buildId, DateTimeOffset lastWriteAt)
    {
        var path = Path.Combine(appRoot, buildId);
        Directory.CreateDirectory(path);
        File.WriteAllText(Path.Combine(path, "build.json"), "{}");
        Directory.SetLastWriteTimeUtc(path, lastWriteAt.UtcDateTime);
        return path;
    }

    private static string CreateTempRoot()
    {
        var root = Path.Combine(Path.GetTempPath(), "sign-manager-local-build-cleanup-tests", Guid.NewGuid().ToString("N"));
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