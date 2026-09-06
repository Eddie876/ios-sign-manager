using Amazon.S3;
using Amazon.S3.Model;

namespace SignManager.Infrastructure.R2;

public sealed class R2S3ObjectStore : IR2ObjectStore
{
    private readonly IAmazonS3 _s3Client;
    private readonly string _bucket;

    public R2S3ObjectStore(string endpoint, string bucket, string accessKeyId, string secretAccessKey)
    {
        if (!IsConfigured(endpoint, bucket, accessKeyId, secretAccessKey))
        {
            throw new InvalidOperationException("R2 configuration is incomplete.");
        }

        var config = new AmazonS3Config
        {
            ServiceURL = endpoint,
            ForcePathStyle = true,
        };

        _s3Client = new AmazonS3Client(accessKeyId, secretAccessKey, config);
        _bucket = bucket;
    }

    public async Task PutObjectAsync(R2PutObjectRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        using var content = new MemoryStream(request.Content, writable: false);
        await _s3Client.PutObjectAsync(new PutObjectRequest
        {
            BucketName = _bucket,
            Key = request.Key,
            ContentType = request.ContentType,
            InputStream = content,
        }, cancellationToken);
    }

    public async Task DeleteObjectIfExistsAsync(string key, CancellationToken cancellationToken)
    {
        try
        {
            await _s3Client.DeleteObjectAsync(new DeleteObjectRequest
            {
                BucketName = _bucket,
                Key = key,
            }, cancellationToken);
        }
        catch (AmazonS3Exception ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            // Treat not-found as already deleted.
        }
    }

    public async Task<bool> ObjectExistsAsync(string key, CancellationToken cancellationToken)
    {
        try
        {
            await _s3Client.GetObjectMetadataAsync(new GetObjectMetadataRequest
            {
                BucketName = _bucket,
                Key = key,
            }, cancellationToken);

            return true;
        }
        catch (AmazonS3Exception ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return false;
        }
    }

    public async Task<IReadOnlyList<R2ObjectInfo>> ListObjectsAsync(string prefix, CancellationToken cancellationToken)
    {
        var result = new List<R2ObjectInfo>();
        string? continuationToken = null;

        do
        {
            var response = await _s3Client.ListObjectsV2Async(new ListObjectsV2Request
            {
                BucketName = _bucket,
                Prefix = prefix ?? string.Empty,
                ContinuationToken = continuationToken,
            }, cancellationToken);

            result.AddRange(response.S3Objects.Select(x => new R2ObjectInfo(x.Key, x.LastModified.ToUniversalTime())));
            continuationToken = response.IsTruncated ? response.NextContinuationToken : null;
        }
        while (!string.IsNullOrWhiteSpace(continuationToken));

        return result;
    }

    public static bool IsConfigured(string? endpoint, string? bucket, string? accessKeyId, string? secretAccessKey)
        => !string.IsNullOrWhiteSpace(endpoint)
            && !string.IsNullOrWhiteSpace(bucket)
            && !string.IsNullOrWhiteSpace(accessKeyId)
            && !string.IsNullOrWhiteSpace(secretAccessKey);
}