using SignManager.Infrastructure.Operations;
using System.IO.Compression;

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

            var signingStateRoot = Path.Combine(root, "signing-state");
            Directory.CreateDirectory(signingStateRoot);
            await File.WriteAllTextAsync(Path.Combine(signingStateRoot, "secrets.enc"), "encrypted");

            var backupPath = Path.Combine(root, "backups", "backup.zip");
            var restoreRoot = Path.Combine(root, "restore");
            var restoreSigningStateRoot = Path.Combine(root, "restore-signing-state");

            var service = new DataBackupService();
            await service.BackupAsync(dataRoot, backupPath, CancellationToken.None, signingStateRoot);
            Assert.True(File.Exists(backupPath));

            await service.RestoreAsync(backupPath, restoreRoot, CancellationToken.None, restoreSigningStateRoot);

            Assert.Equal(
                await File.ReadAllTextAsync(Path.Combine(dataRoot, "config", "apps.json")),
                await File.ReadAllTextAsync(Path.Combine(restoreRoot, "config", "apps.json")));
            Assert.Equal(
                await File.ReadAllTextAsync(Path.Combine(dataRoot, "state", "state.json")),
                await File.ReadAllTextAsync(Path.Combine(restoreRoot, "state", "state.json")));
            Assert.Equal(
                await File.ReadAllTextAsync(Path.Combine(signingStateRoot, "secrets.enc")),
                await File.ReadAllTextAsync(Path.Combine(restoreSigningStateRoot, "secrets.enc")));
        }
        finally
        {
            Cleanup(root);
        }
    }

    [Fact]
    public async Task Backup_ShouldRejectOutputPathInsideDataRoot()
    {
        var root = CreateTempRoot();

        try
        {
            var dataRoot = Path.Combine(root, "data");
            Directory.CreateDirectory(Path.Combine(dataRoot, "config"));
            await File.WriteAllTextAsync(Path.Combine(dataRoot, "config", "apps.json"), "{}");

            var service = new DataBackupService();
            var outputPath = Path.Combine(dataRoot, "backups", "backup.zip");

            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                service.BackupAsync(dataRoot, outputPath, CancellationToken.None));
        }
        finally
        {
            Cleanup(root);
        }
    }

    [Fact]
    public async Task Restore_ShouldRejectPathTraversalEntries()
    {
        var root = CreateTempRoot();

        try
        {
            var backupPath = Path.Combine(root, "backups", "unsafe.zip");
            var backupDir = Path.GetDirectoryName(backupPath);
            if (!string.IsNullOrWhiteSpace(backupDir))
            {
                Directory.CreateDirectory(backupDir);
            }

            using (var archive = ZipFile.Open(backupPath, ZipArchiveMode.Create))
            {
                var entry = archive.CreateEntry("../escape.txt");
                await using var writer = new StreamWriter(entry.Open());
                await writer.WriteAsync("unsafe");
            }

            var service = new DataBackupService();
            var restoreRoot = Path.Combine(root, "restore");

            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                service.RestoreAsync(backupPath, restoreRoot, CancellationToken.None));
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
