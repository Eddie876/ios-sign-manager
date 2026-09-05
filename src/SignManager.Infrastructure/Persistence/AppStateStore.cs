using System.Text.Json;
using SignManager.Core.Models;

namespace SignManager.Infrastructure.Persistence;

public sealed class AppStateStore
{
    public const int CurrentVersion = 1;

    private readonly JsonAtomicFileStore<AppStateDocumentV1> _store = new();

    public async Task<AppState?> LoadAsync(string path, CancellationToken cancellationToken)
    {
        if (!File.Exists(path))
        {
            return null;
        }

        var version = await ReadVersionAsync(path, cancellationToken);
        if (version != CurrentVersion)
        {
            throw new UnsupportedSchemaVersionException("state.json", version, CurrentVersion);
        }

        var document = await _store.ReadAsync(path, cancellationToken)
            ?? throw new InvalidOperationException("state.json is empty.");

        var apps = document.Apps.ToDictionary(
            x => x.Key,
            x => new AppRuntimeState(
                ParseStatus(x.Value.Status),
                x.Value.LastSuccessfulSignAt,
                x.Value.NextSignDueAt,
                x.Value.LatestBuildId,
                x.Value.ProfileCreationDate,
                x.Value.ProfileExpirationDate,
                x.Value.LastPromptAt,
                x.Value.LastErrorCode));

        return new AppState(document.Version, apps);
    }

    public Task SaveAsync(string path, AppState state, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(state);

        var apps = state.Apps.ToDictionary(
            x => x.Key,
            x => new AppRuntimeStateDocumentV1(
                x.Value.Status.ToString().ToLowerInvariant(),
                x.Value.LastSuccessfulSignAt,
                x.Value.NextSignDueAt,
                x.Value.LatestBuildId,
                x.Value.ProfileCreationDate,
                x.Value.ProfileExpirationDate,
                x.Value.LastPromptAt,
                x.Value.LastErrorCode));

        var document = new AppStateDocumentV1(CurrentVersion, apps);
        return _store.WriteAsync(path, document, cancellationToken);
    }

    private static RuntimeStatus ParseStatus(string status)
        => status.ToLowerInvariant() switch
        {
            "ready" => RuntimeStatus.Ready,
            "authrequired" => RuntimeStatus.AuthRequired,
            "pending" => RuntimeStatus.Pending,
            "failed" => RuntimeStatus.Failed,
            _ => throw new InvalidOperationException($"Unknown app runtime status '{status}'."),
        };

    private static async Task<int> ReadVersionAsync(string path, CancellationToken cancellationToken)
    {
        await using var stream = File.OpenRead(path);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);

        if (!document.RootElement.TryGetProperty("version", out var versionProperty))
        {
            throw new InvalidOperationException("state.json is missing required 'version' field.");
        }

        return versionProperty.GetInt32();
    }

    private sealed record AppStateDocumentV1(int Version, IReadOnlyDictionary<string, AppRuntimeStateDocumentV1> Apps);

    private sealed record AppRuntimeStateDocumentV1(
        string Status,
        DateTimeOffset? LastSuccessfulSignAt,
        DateTimeOffset? NextSignDueAt,
        string? LatestBuildId,
        DateTimeOffset? ProfileCreationDate,
        DateTimeOffset? ProfileExpirationDate,
        DateTimeOffset? LastPromptAt,
        string? LastErrorCode);
}
