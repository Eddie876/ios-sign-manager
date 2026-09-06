using System.Security.Cryptography;
using System.Text;
using SignManager.Infrastructure.Persistence;

namespace SignManager.Web.Services;

public sealed class ShortcutTokenStore
{
    private readonly JsonAtomicFileStore<ShortcutTokenDocument> _store = new();

    public async Task<bool> ValidateAsync(string path, string token, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return false;
        }

        var document = await _store.ReadAsync(path, cancellationToken);
        if (document is null)
        {
            return false;
        }

        var hash = ComputeSha256(token);
        return document.Tokens.Any(x => x.RevokedAt is null && string.Equals(x.Hash, hash, StringComparison.OrdinalIgnoreCase));
    }

    public async Task UpsertTokenAsync(string path, string name, string token, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(token);

        var document = await _store.ReadAsync(path, cancellationToken)
            ?? new ShortcutTokenDocument(1, []);

        var hash = ComputeSha256(token);
        var existing = document.Tokens.FirstOrDefault(x => string.Equals(x.Name, name, StringComparison.Ordinal));

        var updatedTokens = existing is null
            ? [.. document.Tokens, new ShortcutTokenEntry(name, hash, null)]
            : document.Tokens.Select(x => string.Equals(x.Name, name, StringComparison.Ordinal)
                ? new ShortcutTokenEntry(name, hash, null)
                : x).ToArray();

        await _store.WriteAsync(path, document with { Tokens = updatedTokens }, cancellationToken);
    }

    public async Task RevokeAsync(string path, string name, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        var document = await _store.ReadAsync(path, cancellationToken)
            ?? new ShortcutTokenDocument(1, []);

        var updatedTokens = document.Tokens.Select(x => string.Equals(x.Name, name, StringComparison.Ordinal)
            ? x with { RevokedAt = DateTimeOffset.UtcNow }
            : x).ToArray();

        await _store.WriteAsync(path, document with { Tokens = updatedTokens }, cancellationToken);
    }

    private static string ComputeSha256(string value)
    {
        var bytes = Encoding.UTF8.GetBytes(value);
        var hash = SHA256.HashData(bytes);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    private sealed record ShortcutTokenDocument(int Version, IReadOnlyList<ShortcutTokenEntry> Tokens);

    private sealed record ShortcutTokenEntry(string Name, string Hash, DateTimeOffset? RevokedAt);
}
