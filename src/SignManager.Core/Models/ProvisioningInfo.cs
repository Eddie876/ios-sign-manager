namespace SignManager.Core.Models;

public sealed record ProvisioningInfo(
    string Uuid,
    DateTimeOffset CreationDate,
    DateTimeOffset ExpirationDate,
    string BundleId,
    string TeamId,
    IReadOnlyList<string> DeviceUdids);
