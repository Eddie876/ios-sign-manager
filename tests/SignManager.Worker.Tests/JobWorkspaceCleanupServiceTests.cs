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
            Assert.Equal(0, result.FailedDirectories);
            Assert.False(Directory.Exists(oldDir));
            Assert.True(Directory.Exists(newDir));
        }
        finally
        {
            Cleanup(root);
        }
    }

    [Fact]
    public void Cleanup_ShouldContinueWhenOneDirectoryDeleteFails()
    {
        var root = CreateTempRoot();

        try
        {
            var now = DateTimeOffset.UtcNow;
            var lockedDir = Path.Combine(root, "job-locked");
            var deletableDir = Path.Combine(root, "job-deletable");
            Directory.CreateDirectory(lockedDir);
            Directory.CreateDirectory(deletableDir);

            Directory.SetLastWriteTimeUtc(lockedDir, now.AddHours(-100).UtcDateTime);
            Directory.SetLastWriteTimeUtc(deletableDir, now.AddHours(-100).UtcDateTime);

            var service = new JobWorkspaceCleanupService(dir =>
            {
                if (string.Equals(dir, lockedDir, StringComparison.OrdinalIgnoreCase))
                {
                    return false;
                }

                Directory.Delete(dir, recursive: true);
                return true;
            });
            var result = service.Cleanup(root, TimeSpan.FromHours(72), now);

            Assert.Equal(2, result.ScannedDirectories);
            Assert.Equal(1, result.DeletedDirectories);
            Assert.Equal(1, result.FailedDirectories);
            Assert.True(Directory.Exists(lockedDir));
            Assert.False(Directory.Exists(deletableDir));
        }
        finally
        {
            Cleanup(root);
        }
    }

    [Fact]
    public void Cleanup_ShouldTreatNegativeMaxAgeAsZero()
    {
        var root = CreateTempRoot();

        try
        {
            var now = DateTimeOffset.UtcNow;
            var dir = Path.Combine(root, "job");
            Directory.CreateDirectory(dir);
            Directory.SetLastWriteTimeUtc(dir, now.AddMinutes(-1).UtcDateTime);

            var service = new JobWorkspaceCleanupService();
            var result = service.Cleanup(root, TimeSpan.FromHours(-1), now);

            Assert.Equal(1, result.ScannedDirectories);
            Assert.Equal(1, result.DeletedDirectories);
            Assert.Equal(0, result.FailedDirectories);
            Assert.False(Directory.Exists(dir));
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
