using System.IO.Compression;

namespace SignManager.Infrastructure.Operations;

public sealed class DataBackupService
{
    public async Task BackupAsync(string dataRootPath, string outputZipPath, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(dataRootPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(outputZipPath);

        if (!Directory.Exists(dataRootPath))
        {
            throw new DirectoryNotFoundException($"Data root not found: {dataRootPath}");
        }

        var outDir = Path.GetDirectoryName(outputZipPath);
        if (!string.IsNullOrWhiteSpace(outDir))
        {
            Directory.CreateDirectory(outDir);
        }

        if (File.Exists(outputZipPath))
        {
            File.Delete(outputZipPath);
        }

        using var zip = ZipFile.Open(outputZipPath, ZipArchiveMode.Create);
        foreach (var file in Directory.GetFiles(dataRootPath, "*", SearchOption.AllDirectories))
        {
            cancellationToken.ThrowIfCancellationRequested();

            var relative = Path.GetRelativePath(dataRootPath, file).Replace('\\', '/');
            var entry = zip.CreateEntry(relative, CompressionLevel.Optimal);
            await using var source = File.OpenRead(file);
            await using var target = entry.Open();
            await source.CopyToAsync(target, cancellationToken);
            await target.FlushAsync(cancellationToken);
        }
    }

    public async Task RestoreAsync(string backupZipPath, string dataRootPath, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(backupZipPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(dataRootPath);

        if (!File.Exists(backupZipPath))
        {
            throw new FileNotFoundException("Backup archive not found.", backupZipPath);
        }

        Directory.CreateDirectory(dataRootPath);

        using var zip = ZipFile.OpenRead(backupZipPath);
        foreach (var entry in zip.Entries)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var outputPath = Path.GetFullPath(Path.Combine(dataRootPath, entry.FullName));
            var root = Path.GetFullPath(dataRootPath);

            if (!outputPath.StartsWith(root, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException($"Invalid entry path detected: {entry.FullName}");
            }

            var dir = Path.GetDirectoryName(outputPath);
            if (!string.IsNullOrWhiteSpace(dir))
            {
                Directory.CreateDirectory(dir);
            }

            await using var source = entry.Open();
            await using var target = File.Create(outputPath);
            await source.CopyToAsync(target, cancellationToken);
            await target.FlushAsync(cancellationToken);
        }
    }
}
