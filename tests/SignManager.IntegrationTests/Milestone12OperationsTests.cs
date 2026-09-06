using SignManager.Infrastructure.Operations;

namespace SignManager.IntegrationTests;

public class Milestone12OperationsTests
{
    [Fact]
    public async Task BackupAndRestore_ShouldRoundtripDataFiles()
    {
        var root = CreateTempRoot();

        try
        {
            var dataRoot = Path.Combine(root, "data");
            Directory.CreateDirectory(Path.Combine(dataRoot, "config"));
            Directory.CreateDirectory(Path.Combine(dataRoot, "state"));
            await File.WriteAllTextAsync(Path.Combine(dataRoot, "config", "apps.json"), "{\"version\":1}");
            await File.WriteAllTextAsync(Path.Combine(dataRoot, "state", "state.json"), "{\"version\":1}");

            var backupPath = Path.Combine(root, "backups", "backup.zip");
            var restoreRoot = Path.Combine(root, "restore");

            var service = new DataBackupService();
            await service.BackupAsync(dataRoot, backupPath, CancellationToken.None);
            Assert.True(File.Exists(backupPath));

            await service.RestoreAsync(backupPath, restoreRoot, CancellationToken.None);

            Assert.Equal(
                await File.ReadAllTextAsync(Path.Combine(dataRoot, "config", "apps.json")),
                await File.ReadAllTextAsync(Path.Combine(restoreRoot, "config", "apps.json")));
            Assert.Equal(
                await File.ReadAllTextAsync(Path.Combine(dataRoot, "state", "state.json")),
                await File.ReadAllTextAsync(Path.Combine(restoreRoot, "state", "state.json")));
        }
        finally
        {
            Cleanup(root);
        }
    }

    private static string CreateTempRoot()
    {
        var root = Path.Combine(Path.GetTempPath(), "sign-manager-ops-tests", Guid.NewGuid().ToString("N"));
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
