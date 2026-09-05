namespace SignManager.Core.Models;

public sealed record BuildInfo(
    string BuildId,
    string AppId,
    string Sha256,
    long SizeBytes,
    DateTimeOffset CreatedAt,
    ProvisioningInfo Provisioning);
