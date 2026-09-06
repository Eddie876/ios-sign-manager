using SignManager.Infrastructure.R2;

namespace SignManager.Worker.Signing;

public sealed class R2BuildPublisher(
    R2ReleasePublisher publisher,
    R2ObjectKeyPlanner keyPlanner)
    : IBuildPublisher
{
    public async Task<BuildPublishResult> PublishAsync(BuildPublishRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

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
                    PublicBaseUrl: "https://example.invalid"),
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
}
