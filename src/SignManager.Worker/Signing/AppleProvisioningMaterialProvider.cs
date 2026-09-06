using System.Security.Cryptography;
using System.Text.Json;
using SignManager.Apple.Auth;
using SignManager.Apple.Developer;
using SignManager.Core.Constants;
using SignManager.Core.Models;
using Microsoft.Extensions.Options;

namespace SignManager.Worker.Signing;

public sealed class AppleProvisioningMaterialProvider(
    AppleAuthenticationService authenticationService,
    IAppleDeveloperClient developerClient,
    AppleProvisioningService provisioningService,
    IOptions<SchedulerOptions> schedulerOptions) : IProvisioningMaterialProvider
{
    public async Task<ProvisioningMaterial> PrepareAsync(
        SigningJob job,
        ManagedAppConfig app,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(job);
        ArgumentNullException.ThrowIfNull(app);

        var options = schedulerOptions.Value;
        ValidateRequiredOptions(options);

        var restored = await authenticationService.RestoreSessionAsync(cancellationToken);
        if (!restored.Success)
        {
            throw new SigningWorkflowException(
                StableErrorCodes.AuthRequired,
                $"Apple session restore failed: {restored.ErrorCode ?? StableErrorCodes.AuthRequired}");
        }

        var teamId = await ResolveTeamIdAsync(options, cancellationToken);
        var now = DateTimeOffset.UtcNow;
        var previous = await LoadPreviousProvisioningAsync(options, app.Id, cancellationToken);

        ProvisioningWorkflowResult result;
        try
        {
            result = await provisioningService.EnsureProvisioningAsync(
                teamId: teamId,
                udid: options.AppleDeviceUdid,
                deviceName: options.AppleDeviceName,
                bundleId: app.Identity.EffectiveBundleId,
                profileName: BuildProfileName(options.AppleProfileNamePrefix, app.Id),
                privateKeyPassword: options.ApplePrivateKeyPassword,
                previous: previous,
                now: now,
                cancellationToken: cancellationToken);
        }
        catch (SigningWorkflowException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new SigningWorkflowException(StableErrorCodes.ProfileCreateFailed, "Provisioning workflow failed.", ex);
        }

        if (!result.IsFresh)
        {
            throw new SigningWorkflowException(StableErrorCodes.ProfileNotFresh, "Provisioning profile is not fresh enough.");
        }

        var workspaceDirectory = Path.Combine(options.WorkspaceRoot, job.JobId);
        Directory.CreateDirectory(workspaceDirectory);

        var profilePath = Path.Combine(workspaceDirectory, "profile.mobileprovision");
        var certificatePath = Path.Combine(workspaceDirectory, "certificate.pem");
        var privateKeyPath = Path.Combine(workspaceDirectory, "private-key.pem");

        await File.WriteAllBytesAsync(
            profilePath,
            Convert.FromBase64String(result.Profile.MobileProvisionBase64),
            cancellationToken);

        await File.WriteAllTextAsync(certificatePath, result.Certificate.Pem, cancellationToken);

        string privateKeyPem;
        try
        {
            privateKeyPem = DecryptPrivateKeyPem(result.EncryptedPrivateKeyPem, options.ApplePrivateKeyPassword);
        }
        catch (Exception ex)
        {
            throw new SigningWorkflowException(StableErrorCodes.CertificateFailed, "Failed to decrypt private key for signing.", ex);
        }

        await File.WriteAllTextAsync(privateKeyPath, privateKeyPem, cancellationToken);

        await PersistSigningStateArtifactsAsync(options, app.Id, result, cancellationToken);

        return new ProvisioningMaterial(
            PrivateKeyPath: privateKeyPath,
            CertificatePath: certificatePath,
            MobileProvisionPath: profilePath,
            Provisioning: result.ParsedProfile);
    }

    private static void ValidateRequiredOptions(SchedulerOptions options)
    {
        if (string.IsNullOrWhiteSpace(options.AppleDeviceUdid))
        {
            throw new SigningWorkflowException(StableErrorCodes.DeviceRegistrationFailed, "Scheduler option AppleDeviceUdid is required.");
        }

        if (string.IsNullOrWhiteSpace(options.AppleDeviceName))
        {
            throw new SigningWorkflowException(StableErrorCodes.DeviceRegistrationFailed, "Scheduler option AppleDeviceName is required.");
        }

        if (string.IsNullOrWhiteSpace(options.ApplePrivateKeyPassword))
        {
            throw new SigningWorkflowException(StableErrorCodes.CertificateFailed, "Scheduler option ApplePrivateKeyPassword is required.");
        }
    }

    private async Task<string> ResolveTeamIdAsync(SchedulerOptions options, CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(options.AppleTeamId))
        {
            return options.AppleTeamId;
        }

        IReadOnlyList<DeveloperTeam> teams;
        try
        {
            teams = await developerClient.GetTeamsAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            throw new SigningWorkflowException(StableErrorCodes.TeamNotFound, "Unable to query Apple teams.", ex);
        }

        var team = teams.FirstOrDefault();
        if (team is null || string.IsNullOrWhiteSpace(team.TeamId))
        {
            throw new SigningWorkflowException(StableErrorCodes.TeamNotFound, "No Apple developer team is available for provisioning.");
        }

        return team.TeamId;
    }

    private static string BuildProfileName(string prefix, string appId)
        => $"{(string.IsNullOrWhiteSpace(prefix) ? "signmanager" : prefix)}-{appId}";

    private static string GetMetadataPath(SchedulerOptions options, string appId)
    {
        var directory = Path.Combine(options.SigningStateRoot, "provisioning");
        Directory.CreateDirectory(directory);

        var safeAppId = appId.Replace('/', '-').Replace('\\', '-');
        return Path.Combine(directory, $"{safeAppId}.json");
    }

    private static async Task<ProvisioningInfo?> LoadPreviousProvisioningAsync(
        SchedulerOptions options,
        string appId,
        CancellationToken cancellationToken)
    {
        var path = GetMetadataPath(options, appId);
        if (!File.Exists(path))
        {
            return null;
        }

        await using var stream = File.OpenRead(path);
        var metadata = await JsonSerializer.DeserializeAsync<ProvisioningMetadata>(stream, cancellationToken: cancellationToken);
        return metadata?.Provisioning;
    }

    private static async Task PersistSigningStateArtifactsAsync(
        SchedulerOptions options,
        string appId,
        ProvisioningWorkflowResult result,
        CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(options.SigningStateRoot);

        var encryptedKeyPath = Path.Combine(options.SigningStateRoot, "private-key.pk8");
        var certPath = Path.Combine(options.SigningStateRoot, "certificate.pem");

        await File.WriteAllTextAsync(encryptedKeyPath, result.EncryptedPrivateKeyPem, cancellationToken);
        await File.WriteAllTextAsync(certPath, result.Certificate.Pem, cancellationToken);

        var metadataPath = GetMetadataPath(options, appId);
        var metadata = new ProvisioningMetadata(
            Provisioning: result.ParsedProfile,
            TeamId: result.Profile.TeamId,
            DeviceId: result.Device.DeviceId,
            CertificateId: result.Certificate.CertificateId,
            UpdatedAt: DateTimeOffset.UtcNow);

        await using var stream = File.Create(metadataPath);
        await JsonSerializer.SerializeAsync(stream, metadata, cancellationToken: cancellationToken);
        await stream.FlushAsync(cancellationToken);
    }

    private static string DecryptPrivateKeyPem(string encryptedPrivateKeyPem, string password)
    {
        using var rsa = RSA.Create();
        rsa.ImportFromEncryptedPem(encryptedPrivateKeyPem, password);
        var pkcs8 = rsa.ExportPkcs8PrivateKey();
        return new string(PemEncoding.Write("PRIVATE KEY", pkcs8));
    }

    private sealed record ProvisioningMetadata(
        ProvisioningInfo Provisioning,
        string TeamId,
        string DeviceId,
        string CertificateId,
        DateTimeOffset UpdatedAt);
}
