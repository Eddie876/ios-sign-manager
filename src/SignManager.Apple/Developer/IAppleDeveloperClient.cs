namespace SignManager.Apple.Developer;

public interface IAppleDeveloperClient
{
    Task<bool> ViewDeveloperAsync(CancellationToken cancellationToken);

    Task<IReadOnlyList<DeveloperTeam>> GetTeamsAsync(CancellationToken cancellationToken);

    Task<RegisteredDevice> EnsureDeviceAsync(
        string teamId,
        string udid,
        string deviceName,
        CancellationToken cancellationToken);

    Task<DevelopmentCertificate> EnsureCertificateAsync(
        string teamId,
        string csrPem,
        CancellationToken cancellationToken);

    Task<AppIdentifier> EnsureAppIdAsync(
        string teamId,
        string bundleId,
        CancellationToken cancellationToken);

    Task<ProvisioningProfile> CreateProvisioningProfileAsync(
        string teamId,
        string appIdId,
        string deviceId,
        string profileName,
        CancellationToken cancellationToken);
}

public sealed record DeveloperTeam(
    string TeamId,
    string Name);

public sealed record RegisteredDevice(
    string DeviceId,
    string Udid,
    string Name);

public sealed record DevelopmentCertificate(
    string CertificateId,
    string SerialNumber,
    string Pem,
    DateTimeOffset ExpiresAt);

public sealed record AppIdentifier(
    string AppIdId,
    string BundleId,
    string Name);

public sealed record ProvisioningProfile(
    string ProfileId,
    string Uuid,
    DateTimeOffset CreationDate,
    DateTimeOffset ExpirationDate,
    string TeamId,
    string BundleId,
    string MobileProvisionBase64);