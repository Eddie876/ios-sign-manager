namespace SignManager.Core.Models;

public sealed record AppConfig(
    int Version,
    IReadOnlyList<ManagedAppConfig> Apps);

public sealed record ManagedAppConfig(
    string Id,
    string Name,
    bool Enabled,
    SourceArtifact Source,
    BundleIdentity Identity,
    AppSigningConfig Signing,
    AppScheduleConfig Schedule,
    AppPublishConfig Publish);

public sealed record SourceArtifact(
    string Path,
    string Sha256,
    DateTimeOffset UploadedAt);

public sealed record AppSigningConfig(
    bool RemoveExtensions = true);

public sealed record AppScheduleConfig(
    bool AutoSign,
    int IntervalHours = 48);

public sealed record AppPublishConfig(
    string Slug);
