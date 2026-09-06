using SignManager.Infrastructure.R2;
using Microsoft.Extensions.Options;

namespace SignManager.Worker.Signing;

public sealed class R2BuildPublisher(
    R2ReleasePublisher publisher,
    R2ObjectKeyPlanner keyPlanner,
    IOptions<SchedulerOptions> schedulerOptions)
    : IBuildPublisher
{
    public async Task<BuildPublishResult> PublishAsync(BuildPublishRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var publicBaseUrl = ValidateAndNormalizePublicBaseUrl(schedulerOptions.Value.OtaPublicBaseUrl);
        var randomNamespace = keyPlanner.CreateRandomNamespace();

        R2PublishResult publishResult;
        try
        {
            publishResult = await publisher.PublishAsync(
                new R2PublishRequest(
                    RandomNamespace: randomNamespace,
                    AppId: request.App.Id,
                    BuildId: request.Build.BuildId,
                    SignedIpaPath: request.SignedIpaPath,
                    BundleIdentifier: request.App.Identity.EffectiveBundleId,
                    BundleVersion: request.Build.CreatedAt.ToUnixTimeSeconds().ToString(),
                    Title: request.App.Name,
                    Sha256: request.Build.Sha256,
                    SizeBytes: request.Build.SizeBytes,
                    CreatedAt: request.NowUtc,
                    PublicBaseUrl: publicBaseUrl),
                cancellationToken);
        }
        catch (R2PublishException ex)
        {
            throw new SigningWorkflowException(ex.ErrorCode, ex.Message, ex);
        }

        return new BuildPublishResult(
            InstallUrl: publishResult.InstallUrl,
            LatestManifestUrl: publishResult.LatestManifestUrl,
            LatestMetadataUrl: publishResult.LatestMetadataUrl);
    }

    private static string ValidateAndNormalizePublicBaseUrl(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new SigningWorkflowException(
                SignManager.Core.Constants.StableErrorCodes.R2UploadFailed,
                "Scheduler option OtaPublicBaseUrl is required for OTA publishing.");
        }

        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri)
            || !string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase)
            || string.IsNullOrWhiteSpace(uri.Host))
        {
            throw new SigningWorkflowException(
                SignManager.Core.Constants.StableErrorCodes.R2UploadFailed,
                "Scheduler option OtaPublicBaseUrl must be an absolute HTTPS URL.");
        }

        return uri.ToString().TrimEnd('/');
    }
}
