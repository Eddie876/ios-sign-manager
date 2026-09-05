using System.Security.Cryptography;

namespace SignManager.Signing.Ipa;

public sealed class SourceIpaManager(IpaPreflightService preflightService)
{
    public async Task<SourceReplaceResult> ReplaceSourceAsync(
        string uploadedIpaPath,
        string immutableSourcePath,
        IpaPreflightLimits limits,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(uploadedIpaPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(immutableSourcePath);

        var preflight = await preflightService.ValidateAndExtractAsync(
            new IpaPreflightRequest(uploadedIpaPath, limits),
            cancellationToken);

        var targetDirectory = Path.GetDirectoryName(immutableSourcePath)
            ?? throw new InvalidOperationException("Immutable source path must include directory.");
        Directory.CreateDirectory(targetDirectory);

        var tempPath = $"{immutableSourcePath}.{Guid.NewGuid():N}.tmp";

        await using (var source = File.OpenRead(uploadedIpaPath))
        await using (var target = File.Create(tempPath))
        {
            await source.CopyToAsync(target, cancellationToken);
            await target.FlushAsync(cancellationToken);
        }

        if (File.Exists(immutableSourcePath))
        {
            File.Replace(tempPath, immutableSourcePath, destinationBackupFileName: null);
        }
        else
        {
            File.Move(tempPath, immutableSourcePath);
        }

        var finalSha = await ComputeSha256Async(immutableSourcePath, cancellationToken);
        return new SourceReplaceResult(
            immutableSourcePath,
            finalSha,
            DateTimeOffset.UtcNow,
            preflight.Metadata);
    }

    public Task CopyImmutableSourceToJobAsync(
        string immutableSourcePath,
        string jobSourcePath,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(immutableSourcePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(jobSourcePath);

        if (!File.Exists(immutableSourcePath))
        {
            throw new FileNotFoundException("Immutable source IPA does not exist.", immutableSourcePath);
        }

        var jobDirectory = Path.GetDirectoryName(jobSourcePath)
            ?? throw new InvalidOperationException("Job source path must include directory.");
        Directory.CreateDirectory(jobDirectory);

        return CopyFileAsync(immutableSourcePath, jobSourcePath, cancellationToken);
    }

    private static async Task CopyFileAsync(string sourcePath, string destinationPath, CancellationToken cancellationToken)
    {
        await using var source = File.OpenRead(sourcePath);
        await using var destination = File.Create(destinationPath);
        await source.CopyToAsync(destination, cancellationToken);
        await destination.FlushAsync(cancellationToken);
    }

    private static async Task<string> ComputeSha256Async(string path, CancellationToken cancellationToken)
    {
        await using var stream = File.OpenRead(path);
        using var sha256 = SHA256.Create();
        var hash = await sha256.ComputeHashAsync(stream, cancellationToken);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}

public sealed record SourceReplaceResult(
    string Path,
    string Sha256,
    DateTimeOffset UploadedAt,
    AppMetadata Metadata);
