using SignManager.Signing.Zsign;

namespace SignManager.Worker.Signing;

public sealed class ZsignArtifactSigner(ZsignSigningService signingService) : ISigningArtifactSigner
{
    public async Task<SignedBuildArtifact> SignAsync(SignArtifactRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var artifact = await signingService.SignAsync(
            new ZsignSignRequest(
                ZsignExecutablePath: request.ZsignExecutablePath,
                WorkspaceDirectory: Path.GetDirectoryName(request.OutputIpaPath)
                    ?? throw new InvalidOperationException("Output IPA path must include directory."),
                SourceIpaPath: request.SourceIpaPath,
                OutputIpaPath: request.OutputIpaPath,
                PrivateKeyPath: request.Provisioning.PrivateKeyPath,
                CertificatePath: request.Provisioning.CertificatePath,
                MobileProvisionPath: request.Provisioning.MobileProvisionPath,
                EffectiveBundleId: request.EffectiveBundleId,
                RemoveExtensions: request.RemoveExtensions,
                ExpectedBundleId: request.EffectiveBundleId,
                ExpectedProfileUuid: request.Provisioning.Provisioning.Uuid,
                ExpectedProfileExpirationDate: request.Provisioning.Provisioning.ExpirationDate,
                Timeout: request.Timeout,
                MaxProcessOutputBytes: request.MaxProcessOutputBytes),
            cancellationToken);

        return new SignedBuildArtifact(
            artifact.Path,
            artifact.SizeBytes,
            artifact.Sha256,
            artifact.SigningDuration);
    }
}
