using System.IO.Compression;

namespace SignManager.Infrastructure.Operations;

public sealed class DataBackupService
{
    public async Task BackupAsync(
        string dataRootPath,
        string outputZipPath,
        CancellationToken cancellationToken,
        string? signingStateRootPath = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(dataRootPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(outputZipPath);

        var dataRootFullPath = Path.GetFullPath(dataRootPath);
        var outputZipFullPath = Path.GetFullPath(outputZipPath);

        if (!Directory.Exists(dataRootFullPath))
        {
            throw new DirectoryNotFoundException($"Data root not found: {dataRootFullPath}");
        }

        if (IsPathWithinRoot(outputZipFullPath, dataRootFullPath))
        {
            throw new InvalidOperationException("Backup output path cannot be inside data root.");
        }

        string? signingStateFullPath = null;
        if (!string.IsNullOrWhiteSpace(signingStateRootPath))
        {
            signingStateFullPath = Path.GetFullPath(signingStateRootPath);
            if (Directory.Exists(signingStateFullPath)
                && IsPathWithinRoot(outputZipFullPath, signingStateFullPath))
            {
                throw new InvalidOperationException("Backup output path cannot be inside signing-state root.");
            }
        }

        var outDir = Path.GetDirectoryName(outputZipFullPath);
        if (!string.IsNullOrWhiteSpace(outDir))
        {
            Directory.CreateDirectory(outDir);
        }

        if (File.Exists(outputZipFullPath))
        {
            File.Delete(outputZipFullPath);
        }

        using var zip = ZipFile.Open(outputZipFullPath, ZipArchiveMode.Create);
        await AddDirectoryToArchiveAsync(zip, dataRootFullPath, prefix: null, cancellationToken);

        if (!string.IsNullOrWhiteSpace(signingStateFullPath) && Directory.Exists(signingStateFullPath))
        {
            await AddDirectoryToArchiveAsync(zip, signingStateFullPath, prefix: "signing-state", cancellationToken);
        }
    }

    public async Task RestoreAsync(
        string backupZipPath,
        string dataRootPath,
        CancellationToken cancellationToken,
        string? signingStateRootPath = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(backupZipPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(dataRootPath);

        var backupZipFullPath = Path.GetFullPath(backupZipPath);
        var dataRootFullPath = Path.GetFullPath(dataRootPath);

        if (!File.Exists(backupZipFullPath))
        {
            throw new FileNotFoundException("Backup archive not found.", backupZipFullPath);
        }

        string? signingStateFullPath = null;
        if (!string.IsNullOrWhiteSpace(signingStateRootPath))
        {
            signingStateFullPath = Path.GetFullPath(signingStateRootPath);
            Directory.CreateDirectory(signingStateFullPath);
        }

        Directory.CreateDirectory(dataRootFullPath);

        using var zip = ZipFile.OpenRead(backupZipFullPath);
        foreach (var entry in zip.Entries)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (string.IsNullOrWhiteSpace(entry.FullName))
            {
                continue;
            }

            var normalizedEntry = entry.FullName.Replace('\\', '/');
            var entryTarget = ResolveEntryTarget(normalizedEntry, dataRootFullPath, signingStateFullPath);
            var outputPath = Path.GetFullPath(Path.Combine(entryTarget.RootPath, entryTarget.RelativePath));

            if (!IsPathWithinRoot(outputPath, entryTarget.RootPath))
            {
                throw new InvalidOperationException($"Invalid entry path detected: {entry.FullName}");
            }

            // Directory entries in ZIP archives are represented by trailing '/'.
            if (entry.FullName.EndsWith("/", StringComparison.Ordinal))
            {
                Directory.CreateDirectory(outputPath);
                continue;
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

    private static bool IsPathWithinRoot(string path, string root)
    {
        var normalizedRoot = root.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            + Path.DirectorySeparatorChar;

        return path.StartsWith(normalizedRoot, StringComparison.OrdinalIgnoreCase)
            || string.Equals(path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
                root.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
                StringComparison.OrdinalIgnoreCase);
    }

    private static async Task AddDirectoryToArchiveAsync(
        ZipArchive zip,
        string rootPath,
        string? prefix,
        CancellationToken cancellationToken)
    {
        foreach (var file in Directory.GetFiles(rootPath, "*", SearchOption.AllDirectories))
        {
            cancellationToken.ThrowIfCancellationRequested();

            var relative = Path.GetRelativePath(rootPath, file).Replace('\\', '/');
            var entryPath = string.IsNullOrWhiteSpace(prefix)
                ? relative
                : $"{prefix}/{relative}";

            var entry = zip.CreateEntry(entryPath, CompressionLevel.Optimal);
            await using var source = File.OpenRead(file);
            await using var target = entry.Open();
            await source.CopyToAsync(target, cancellationToken);
            await target.FlushAsync(cancellationToken);
        }
    }

    private static (string RootPath, string RelativePath) ResolveEntryTarget(
        string normalizedEntry,
        string dataRootFullPath,
        string? signingStateFullPath)
    {
        const string SigningStatePrefix = "signing-state/";
        if (!string.IsNullOrWhiteSpace(signingStateFullPath)
            && normalizedEntry.StartsWith(SigningStatePrefix, StringComparison.OrdinalIgnoreCase))
        {
            return (signingStateFullPath, normalizedEntry[SigningStatePrefix.Length..]);
        }

        return (dataRootFullPath, normalizedEntry);
    }
}
