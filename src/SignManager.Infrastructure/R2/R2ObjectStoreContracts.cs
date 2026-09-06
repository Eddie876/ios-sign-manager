namespace SignManager.Infrastructure.R2;

public interface IR2ObjectStore
{
    Task PutObjectAsync(R2PutObjectRequest request, CancellationToken cancellationToken);

    Task DeleteObjectIfExistsAsync(string key, CancellationToken cancellationToken);

    Task<bool> ObjectExistsAsync(string key, CancellationToken cancellationToken);

    Task<IReadOnlyList<R2ObjectInfo>> ListObjectsAsync(string prefix, CancellationToken cancellationToken);
}

public sealed record R2PutObjectRequest(
    string Key,
    string ContentType,
    byte[] Content);

public sealed record R2ObjectInfo(
    string Key,
    DateTimeOffset LastModifiedUtc);

public sealed class R2PublishException(string errorCode, string message, Exception? innerException = null)
    : InvalidOperationException(message, innerException)
{
    public string ErrorCode { get; } = errorCode;
}

public sealed record R2PublishRequest(
    string RandomNamespace,
    string AppId,
    string BuildId,
    string SignedIpaPath,
    string BundleIdentifier,
    string BundleVersion,
    string Title,
    string Sha256,
    long SizeBytes,
    DateTimeOffset CreatedAt,
    string PublicBaseUrl);

public sealed record R2PublishResult(
    R2ObjectKeyPlan Plan,
    string VersionedIpaUrl,
    string VersionedManifestUrl,
    string LatestManifestUrl,
    string LatestMetadataUrl,
    string InstallUrl);
