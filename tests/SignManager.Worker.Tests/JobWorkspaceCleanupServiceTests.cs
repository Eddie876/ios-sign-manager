using SignManager.Worker.Operations;

namespace SignManager.Worker.Tests;

public class JobWorkspaceCleanupServiceTests
{
    [Fact]
    public void Cleanup_ShouldDeleteOnlyDirectoriesOlderThanMaxAge()
    {
        var root = CreateTempRoot();

        try
        {
            var oldDir = Path.Combine(root, "job-old");
            var newDir = Path.Combine(root, "job-new");
            Directory.CreateDirectory(oldDir);
            Directory.CreateDirectory(newDir);

            var now = DateTimeOffset.UtcNow;
            Directory.SetLastWriteTimeUtc(oldDir, now.AddHours(-100).UtcDateTime);
            Directory.SetLastWriteTimeUtc(newDir, now.AddHours(-1).UtcDateTime);

            var service = new JobWorkspaceCleanupService();
            var result = service.Cleanup(root, TimeSpan.FromHours(72), now);

            Assert.Equal(2, result.ScannedDirectories);
            Assert.Equal(1, result.DeletedDirectories);
            Assert.False(Directory.Exists(oldDir));
            Assert.True(Directory.Exists(newDir));
        }
        finally
        {
            Cleanup(root);
        }
    }

    private static string CreateTempRoot()
    {
        var root = Path.Combine(Path.GetTempPath(), "sign-manager-worker-cleanup-tests", Guid.NewGuid().ToString("N"));
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
