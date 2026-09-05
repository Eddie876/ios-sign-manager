using System.Security.Cryptography;

namespace SignManager.Signing.Zsign;

public sealed class ZsignSigningService(
    IProcessRunner processRunner,
    SignedIpaValidator validator)
{
    public async Task<SignedIpaArtifact> SignAsync(
        ZsignSignRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        Directory.CreateDirectory(request.WorkspaceDirectory);

        var zsignRequest = new ZsignRequest(
            SourceIpaPath: request.SourceIpaPath,
            OutputIpaPath: request.OutputIpaPath,
            PrivateKeyPath: request.PrivateKeyPath,
            CertificatePath: request.CertificatePath,
            MobileProvisionPath: request.MobileProvisionPath,
            EffectiveBundleId: request.EffectiveBundleId,
            RemoveExtensions: request.RemoveExtensions);

        var args = ZsignArgumentBuilder.Build(zsignRequest);
        var processResult = await processRunner.RunAsync(
            new ProcessRunRequest(
                FileName: request.ZsignExecutablePath,
                Arguments: args,
                WorkingDirectory: request.WorkspaceDirectory,
                Timeout: request.Timeout,
                MaxOutputBytes: request.MaxProcessOutputBytes),
            cancellationToken);

        if (processResult.TimedOut)
        {
            throw new InvalidOperationException("zsign process timed out.");
        }

        if (processResult.ExitCode != 0)
        {
            throw new InvalidOperationException($"zsign failed with exit code {processResult.ExitCode}: {processResult.StandardError}");
        }

        if (!File.Exists(request.OutputIpaPath))
        {
            throw new InvalidOperationException("Signed IPA was not produced.");
        }

        var validation = validator.Validate(new SignedIpaValidationRequest(
            request.OutputIpaPath,
            request.ExpectedBundleId,
            request.ExpectedProfileUuid,
            request.ExpectedProfileExpirationDate));

        if (!validation.Success)
        {
            throw new InvalidOperationException($"Signed IPA validation failed: {validation.Error}");
        }

        var outputInfo = new FileInfo(request.OutputIpaPath);
        var sha256 = await ComputeSha256Async(request.OutputIpaPath, cancellationToken);

        return new SignedIpaArtifact(
            request.OutputIpaPath,
            outputInfo.Length,
            sha256,
            processResult.Duration);
    }

    private static async Task<string> ComputeSha256Async(string path, CancellationToken cancellationToken)
    {
        await using var stream = File.OpenRead(path);
        using var sha256 = SHA256.Create();
        var hash = await sha256.ComputeHashAsync(stream, cancellationToken);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}

public sealed record ZsignSignRequest(
    string ZsignExecutablePath,
    string WorkspaceDirectory,
    string SourceIpaPath,
    string OutputIpaPath,
    string PrivateKeyPath,
    string CertificatePath,
    string MobileProvisionPath,
    string EffectiveBundleId,
    bool RemoveExtensions,
    string ExpectedBundleId,
    string ExpectedProfileUuid,
    DateTimeOffset ExpectedProfileExpirationDate,
    TimeSpan Timeout,
    int MaxProcessOutputBytes = 256 * 1024);

public sealed record SignedIpaArtifact(
    string Path,
    long SizeBytes,
    string Sha256,
    TimeSpan SigningDuration);
