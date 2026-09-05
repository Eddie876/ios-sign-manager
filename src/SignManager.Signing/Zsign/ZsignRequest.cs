namespace SignManager.Signing.Zsign;

public sealed record ZsignRequest(
    string SourceIpaPath,
    string OutputIpaPath,
    string PrivateKeyPath,
    string CertificatePath,
    string MobileProvisionPath,
    string EffectiveBundleId,
    bool RemoveExtensions);
