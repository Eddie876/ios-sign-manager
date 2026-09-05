using SignManager.Core.Models;
using SignManager.Core.Policies;

namespace SignManager.Apple.Developer;

public sealed class AppleProvisioningService(
    IAppleDeveloperClient developerClient,
    ICsrGenerator csrGenerator,
    ProvisioningProfileParser profileParser,
    ProfileFreshnessPolicy freshnessPolicy)
{
    public async Task<ProvisioningWorkflowResult> EnsureProvisioningAsync(
        string teamId,
        string udid,
        string deviceName,
        string bundleId,
        string profileName,
        string privateKeyPassword,
        ProvisioningInfo? previous,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var device = await developerClient.EnsureDeviceAsync(teamId, udid, deviceName, cancellationToken);

        var csr = csrGenerator.Generate(bundleId, privateKeyPassword);

        var certificate = await developerClient.EnsureCertificateAsync(teamId, csr.CsrPem, cancellationToken);

        var appId = await developerClient.EnsureAppIdAsync(teamId, bundleId, cancellationToken);

        var profile = await developerClient.CreateProvisioningProfileAsync(
            teamId,
            appId.AppIdId,
            device.DeviceId,
            profileName,
            cancellationToken);

        var parsed = profileParser.Parse(Convert.FromBase64String(profile.MobileProvisionBase64));

        var current = new ProvisioningInfo(
            parsed.Uuid,
            parsed.CreationDate,
            parsed.ExpirationDate,
            parsed.BundleId,
            parsed.TeamId,
            parsed.DeviceUdids);

        var isFresh = freshnessPolicy.IsFresh(current, previous, now);

        return new ProvisioningWorkflowResult(
            Device: device,
            Certificate: certificate,
            AppId: appId,
            Profile: profile,
            ParsedProfile: current,
            EncryptedPrivateKeyPem: csr.EncryptedPrivateKeyPem,
            IsFresh: isFresh);
    }
}

public sealed record ProvisioningWorkflowResult(
    RegisteredDevice Device,
    DevelopmentCertificate Certificate,
    AppIdentifier AppId,
    ProvisioningProfile Profile,
    ProvisioningInfo ParsedProfile,
    string EncryptedPrivateKeyPem,
    bool IsFresh);
