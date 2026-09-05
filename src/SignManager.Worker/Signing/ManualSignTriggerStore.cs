using System.Collections.Concurrent;

namespace SignManager.Worker.Signing;

public sealed class ManualSignTriggerStore
{
    private readonly ConcurrentDictionary<string, byte> _pending = new(StringComparer.Ordinal);

    public bool Request(string appId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(appId);
        return _pending.TryAdd(appId, 0);
    }

    public bool Consume(string appId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(appId);
        return _pending.TryRemove(appId, out _);
    }

    public bool HasPending(string appId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(appId);
        return _pending.ContainsKey(appId);
    }
}
