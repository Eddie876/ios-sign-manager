using System.Text.Json;
using System.Text.RegularExpressions;
using SignManager.Core.Models;

namespace SignManager.Infrastructure.Persistence;

public sealed class AppConfigStore
{
    public const int CurrentVersion = 1;
    private static readonly Regex SafeSlugPattern = new("^[a-z0-9-]+$", RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private readonly JsonAtomicFileStore<AppConfigDocumentV1> _store = new();

    public async Task<AppConfig?> LoadAsync(string path, CancellationToken cancellationToken)
    {
        if (!File.Exists(path))
        {
            return null;
        }

        var version = await ReadVersionAsync(path, cancellationToken);
        if (version != CurrentVersion)
        {
            throw new UnsupportedSchemaVersionException("apps.json", version, CurrentVersion);
        }

        var document = await _store.ReadAsync(path, cancellationToken)
            ?? throw new InvalidOperationException("apps.json is empty.");

        ValidateDocument(document);

        return new AppConfig(
            document.Version,
            document.Apps.Select(MapToDomain).ToArray());
    }

    public Task SaveAsync(string path, AppConfig config, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(config);
        ValidateConfig(config);

        var document = new AppConfigDocumentV1(
            Version: CurrentVersion,
            Apps: config.Apps.Select(MapToDocument).ToArray());

        return _store.WriteAsync(path, document, cancellationToken);
    }

    private static ManagedAppConfig MapToDomain(ManagedAppConfigDocumentV1 app)
        => new(
            app.Id,
            app.Name,
            app.Enabled,
            new SourceArtifact(app.Source.Path, app.Source.Sha256, app.Source.UploadedAt),
            new BundleIdentity(app.Identity.SourceBundleId, app.Identity.EffectiveBundleId),
            new AppSigningConfig(app.Signing.RemoveExtensions),
            new AppScheduleConfig(app.Schedule.AutoSign, app.Schedule.IntervalHours),
            new AppPublishConfig(app.Publish.Slug));

    private static ManagedAppConfigDocumentV1 MapToDocument(ManagedAppConfig app)
        => new(
            app.Id,
            app.Name,
            app.Enabled,
            new SourceArtifactDocumentV1(app.Source.Path, app.Source.Sha256, app.Source.UploadedAt),
            new BundleIdentityDocumentV1(app.Identity.SourceBundleId, app.Identity.EffectiveBundleId),
            new AppSigningConfigDocumentV1(app.Signing.RemoveExtensions),
            new AppScheduleConfigDocumentV1(app.Schedule.AutoSign, app.Schedule.IntervalHours),
            new AppPublishConfigDocumentV1(app.Publish.Slug));

    private static async Task<int> ReadVersionAsync(string path, CancellationToken cancellationToken)
    {
        await using var stream = File.OpenRead(path);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);

        if (!document.RootElement.TryGetProperty("version", out var versionProperty))
        {
            throw new InvalidOperationException("apps.json is missing required 'version' field.");
        }

        return versionProperty.GetInt32();
    }

    private static void ValidateDocument(AppConfigDocumentV1 document)
    {
        if (document.Version != CurrentVersion)
        {
            throw new UnsupportedSchemaVersionException("apps.json", document.Version, CurrentVersion);
        }

        ValidateApps(document.Apps);
    }

    private static void ValidateConfig(AppConfig config)
    {
        ValidateApps(config.Apps);
    }

    private static void ValidateApps(IReadOnlyList<ManagedAppConfigDocumentV1> apps)
    {
        var seenAppIds = new HashSet<string>(StringComparer.Ordinal);

        foreach (var app in apps)
        {
            ValidateNonEmpty(app.Id, "apps.json app.id");
            ValidateNonEmpty(app.Name, $"apps.json app '{app.Id}' name");
            ValidateNonEmpty(app.Source.Path, $"apps.json app '{app.Id}' source.path");
            ValidateNonEmpty(app.Source.Sha256, $"apps.json app '{app.Id}' source.sha256");
            ValidateNonEmpty(app.Identity.SourceBundleId, $"apps.json app '{app.Id}' identity.sourceBundleId");
            ValidateNonEmpty(app.Identity.EffectiveBundleId, $"apps.json app '{app.Id}' identity.effectiveBundleId");
            ValidateNonEmpty(app.Publish.Slug, $"apps.json app '{app.Id}' publish.slug");

            if (!SafeSlugPattern.IsMatch(app.Publish.Slug))
            {
                throw new InvalidOperationException($"apps.json app '{app.Id}' publish.slug must match pattern [a-z0-9-]+.");
            }

            if (app.Schedule.IntervalHours <= 0)
            {
                throw new InvalidOperationException($"apps.json app '{app.Id}' schedule.intervalHours must be greater than 0.");
            }

            if (!seenAppIds.Add(app.Id))
            {
                throw new InvalidOperationException($"apps.json contains duplicate app id '{app.Id}'.");
            }
        }
    }

    private static void ValidateApps(IReadOnlyList<ManagedAppConfig> apps)
    {
        var seenAppIds = new HashSet<string>(StringComparer.Ordinal);

        foreach (var app in apps)
        {
            ValidateNonEmpty(app.Id, "app.id");
            ValidateNonEmpty(app.Name, $"app '{app.Id}' name");
            ValidateNonEmpty(app.Source.Path, $"app '{app.Id}' source.path");
            ValidateNonEmpty(app.Source.Sha256, $"app '{app.Id}' source.sha256");
            ValidateNonEmpty(app.Identity.SourceBundleId, $"app '{app.Id}' identity.sourceBundleId");
            ValidateNonEmpty(app.Identity.EffectiveBundleId, $"app '{app.Id}' identity.effectiveBundleId");
            ValidateNonEmpty(app.Publish.Slug, $"app '{app.Id}' publish.slug");

            if (!SafeSlugPattern.IsMatch(app.Publish.Slug))
            {
                throw new InvalidOperationException($"app '{app.Id}' publish.slug must match pattern [a-z0-9-]+.");
            }

            if (app.Schedule.IntervalHours <= 0)
            {
                throw new InvalidOperationException($"app '{app.Id}' schedule.intervalHours must be greater than 0.");
            }

            if (!seenAppIds.Add(app.Id))
            {
                throw new InvalidOperationException($"Duplicate app id '{app.Id}'.");
            }
        }
    }

    private static void ValidateNonEmpty(string value, string field)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidOperationException($"{field} is required.");
        }
    }

    private sealed record AppConfigDocumentV1(int Version, IReadOnlyList<ManagedAppConfigDocumentV1> Apps);

    private sealed record ManagedAppConfigDocumentV1(
        string Id,
        string Name,
        bool Enabled,
        SourceArtifactDocumentV1 Source,
        BundleIdentityDocumentV1 Identity,
        AppSigningConfigDocumentV1 Signing,
        AppScheduleConfigDocumentV1 Schedule,
        AppPublishConfigDocumentV1 Publish);

    private sealed record SourceArtifactDocumentV1(string Path, string Sha256, DateTimeOffset UploadedAt);

    private sealed record BundleIdentityDocumentV1(string SourceBundleId, string EffectiveBundleId);

    private sealed record AppSigningConfigDocumentV1(bool RemoveExtensions);

    private sealed record AppScheduleConfigDocumentV1(bool AutoSign, int IntervalHours);

    private sealed record AppPublishConfigDocumentV1(string Slug);
}
