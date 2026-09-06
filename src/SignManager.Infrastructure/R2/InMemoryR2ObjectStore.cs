using System.Collections.Concurrent;

namespace SignManager.Infrastructure.R2;

public sealed class InMemoryR2ObjectStore : IR2ObjectStore
{
    private readonly ConcurrentDictionary<string, StoredR2Object> _objects = new(StringComparer.Ordinal);

    public Task PutObjectAsync(R2PutObjectRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        _objects[request.Key] = new StoredR2Object(request.ContentType, request.Content);
        return Task.CompletedTask;
    }

    public Task DeleteObjectIfExistsAsync(string key, CancellationToken cancellationToken)
    {
        _objects.TryRemove(key, out _);
        return Task.CompletedTask;
    }

    public bool TryGet(string key, out StoredR2Object value)
        => _objects.TryGetValue(key, out value!);
}

public sealed record StoredR2Object(string ContentType, byte[] Content);
