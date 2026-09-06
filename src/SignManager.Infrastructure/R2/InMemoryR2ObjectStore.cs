using System.Collections.Concurrent;

namespace SignManager.Infrastructure.R2;

public sealed class InMemoryR2ObjectStore : IR2ObjectStore
{
    private readonly ConcurrentDictionary<string, StoredR2ObjectInternal> _objects = new(StringComparer.Ordinal);

    public Task PutObjectAsync(R2PutObjectRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        _objects[request.Key] = new StoredR2ObjectInternal(
            request.ContentType,
            request.Content,
            DateTimeOffset.UtcNow);
        return Task.CompletedTask;
    }

    public Task DeleteObjectIfExistsAsync(string key, CancellationToken cancellationToken)
    {
        _objects.TryRemove(key, out _);
        return Task.CompletedTask;
    }

    public Task<bool> ObjectExistsAsync(string key, CancellationToken cancellationToken)
        => Task.FromResult(_objects.ContainsKey(key));

    public Task<IReadOnlyList<R2ObjectInfo>> ListObjectsAsync(string prefix, CancellationToken cancellationToken)
    {
        var normalizedPrefix = prefix ?? string.Empty;
        var items = _objects
            .Where(x => x.Key.StartsWith(normalizedPrefix, StringComparison.Ordinal))
            .Select(x => new R2ObjectInfo(x.Key, x.Value.LastModifiedUtc))
            .OrderBy(x => x.Key, StringComparer.Ordinal)
            .ToArray();

        return Task.FromResult<IReadOnlyList<R2ObjectInfo>>(items);
    }

    public bool TryGet(string key, out StoredR2Object value)
    {
        if (_objects.TryGetValue(key, out var stored))
        {
            value = new StoredR2Object(stored.ContentType, stored.Content);
            return true;
        }

        value = null!;
        return false;
    }
}

public sealed record StoredR2Object(string ContentType, byte[] Content);

internal sealed record StoredR2ObjectInternal(string ContentType, byte[] Content, DateTimeOffset LastModifiedUtc);
