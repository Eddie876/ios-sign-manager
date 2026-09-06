using System.Text;
using System.Text.Json;
using System.Security.Cryptography;
using SignManager.Core.Constants;
using SignManager.Infrastructure.Ota;

namespace SignManager.Infrastructure.R2;

public sealed class R2ReleasePublisher(
    IR2ObjectStore objectStore,
    R2ObjectKeyPlanner keyPlanner,
    OtaManifestGenerator manifestGenerator)
{
    private const string IpaMime = "application/octet-stream";
    private const string ManifestMime = "application/x-plist";
    private const string JsonMime = "application/json";

    public async Task<R2PublishResult> PublishAsync(R2PublishRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (!File.Exists(request.SignedIpaPath))
        {
            throw new R2PublishException(StableErrorCodes.R2UploadFailed, "Signed IPA does not exist.");
        }

        var ipaBytes = await File.ReadAllBytesAsync(request.SignedIpaPath, cancellationToken);
        ValidateSignedIpaArtifact(request, ipaBytes);

        var plan = keyPlanner.BuildPlan(request.RandomNamespace, request.AppId, request.BuildId);

        var versionedIpaUrl = CombineUrl(request.PublicBaseUrl, plan.VersionedIpaKey);
        var versionedManifestUrl = CombineUrl(request.PublicBaseUrl, plan.VersionedManifestKey);
        var latestManifestUrl = CombineUrl(request.PublicBaseUrl, plan.LatestManifestKey);
        var latestMetadataUrl = CombineUrl(request.PublicBaseUrl, plan.LatestJsonKey);
        var installUrl = ItmsServicesUrlBuilder.BuildInstallUrl(latestManifestUrl);

        string manifestXml;
        try
        {
            manifestXml = manifestGenerator.GenerateXml(new OtaManifestRequest(
                PackageUrl: versionedIpaUrl,
                BundleIdentifier: request.BundleIdentifier,
                BundleVersion: request.BundleVersion,
                Title: request.Title));
        }
        catch (Exception ex)
        {
            throw new R2PublishException(StableErrorCodes.ManifestGenerationFailed, "Failed to generate OTA manifest.", ex);
        }

        var buildJsonBytes = JsonSerializer.SerializeToUtf8Bytes(new
        {
            request.AppId,
            request.BuildId,
            request.Sha256,
            request.SizeBytes,
            CreatedAt = request.CreatedAt,
            IpaUrl = versionedIpaUrl,
            ManifestUrl = versionedManifestUrl,
        });

        var latestJsonBytes = JsonSerializer.SerializeToUtf8Bytes(new
        {
            request.AppId,
            request.BuildId,
            LatestManifestUrl = latestManifestUrl,
            InstallUrl = installUrl,
            UpdatedAt = request.CreatedAt,
        });

        var uploadedKeys = new List<string>();

        try
        {
            await UploadBytesAsync(plan.VersionedIpaKey, ipaBytes, IpaMime, uploadedKeys, cancellationToken);
            await UploadTextAsync(plan.VersionedManifestKey, manifestXml, ManifestMime, uploadedKeys, cancellationToken);
            await UploadBytesAsync(plan.VersionedBuildJsonKey, buildJsonBytes, JsonMime, uploadedKeys, cancellationToken);
            await VerifyVersionedArtifactsAsync(plan, cancellationToken);
            await UploadTextAsync(plan.LatestManifestKey, manifestXml, ManifestMime, uploadedKeys, cancellationToken);
            await UploadBytesAsync(plan.LatestJsonKey, latestJsonBytes, JsonMime, uploadedKeys, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            await CleanupPartialAsync(uploadedKeys, cancellationToken);

            if (ex is R2PublishException publishException)
            {
                throw publishException;
            }

            throw new R2PublishException(StableErrorCodes.R2UploadFailed, "Failed to publish build artifacts to R2.", ex);
        }

        return new R2PublishResult(
            Plan: plan,
            VersionedIpaUrl: versionedIpaUrl,
            VersionedManifestUrl: versionedManifestUrl,
            LatestManifestUrl: latestManifestUrl,
            LatestMetadataUrl: latestMetadataUrl,
            InstallUrl: installUrl);
    }

    private async Task UploadTextAsync(string key, string content, string contentType, List<string> uploadedKeys, CancellationToken cancellationToken)
    {
        var bytes = Encoding.UTF8.GetBytes(content);
        await UploadBytesAsync(key, bytes, contentType, uploadedKeys, cancellationToken);
    }

    private async Task UploadBytesAsync(string key, byte[] content, string contentType, List<string> uploadedKeys, CancellationToken cancellationToken)
    {
        await objectStore.PutObjectAsync(new R2PutObjectRequest(key, contentType, content), cancellationToken);
        uploadedKeys.Add(key);
    }

    private async Task CleanupPartialAsync(IReadOnlyList<string> uploadedKeys, CancellationToken cancellationToken)
    {
        for (var i = uploadedKeys.Count - 1; i >= 0; i--)
        {
            try
            {
                await objectStore.DeleteObjectIfExistsAsync(uploadedKeys[i], cancellationToken);
            }
            catch
            {
                // Best-effort cleanup.
            }
        }
    }

    private async Task VerifyVersionedArtifactsAsync(R2ObjectKeyPlan plan, CancellationToken cancellationToken)
    {
        var hasIpa = await objectStore.ObjectExistsAsync(plan.VersionedIpaKey, cancellationToken);
        var hasManifest = await objectStore.ObjectExistsAsync(plan.VersionedManifestKey, cancellationToken);
        var hasBuildJson = await objectStore.ObjectExistsAsync(plan.VersionedBuildJsonKey, cancellationToken);

        if (!hasIpa || !hasManifest || !hasBuildJson)
        {
            throw new R2PublishException(StableErrorCodes.R2UploadFailed, "Versioned artifact verification failed before latest pointer update.");
        }
    }

    private static void ValidateSignedIpaArtifact(R2PublishRequest request, byte[] ipaBytes)
    {
        if (request.SizeBytes <= 0)
        {
            throw new R2PublishException(StableErrorCodes.SignedIpaValidationFailed, "Signed IPA size metadata is invalid.");
        }

        if (ipaBytes.LongLength != request.SizeBytes)
        {
            throw new R2PublishException(StableErrorCodes.SignedIpaValidationFailed, "Signed IPA size does not match metadata.");
        }

        var hash = SHA256.HashData(ipaBytes);
        var actualSha256 = Convert.ToHexString(hash).ToLowerInvariant();
        if (!string.Equals(actualSha256, request.Sha256, StringComparison.OrdinalIgnoreCase))
        {
            throw new R2PublishException(StableErrorCodes.SignedIpaValidationFailed, "Signed IPA hash does not match metadata.");
        }
    }

    private static string CombineUrl(string baseUrl, string key)
    {
        if (string.IsNullOrWhiteSpace(baseUrl))
        {
            throw new R2PublishException(StableErrorCodes.R2UploadFailed, "Public base URL is required.");
        }

        return $"{baseUrl.TrimEnd('/')}/{key}";
    }
}
