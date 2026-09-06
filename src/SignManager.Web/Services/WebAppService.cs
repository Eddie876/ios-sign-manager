using System.Text.RegularExpressions;
using Microsoft.Extensions.Options;
using SignManager.Core.Models;
using SignManager.Infrastructure.Ota;
using SignManager.Infrastructure.Persistence;
using SignManager.Signing.Ipa;

namespace SignManager.Web.Services;

public sealed class WebAppService(
    AppConfigStore appConfigStore,
    AppStateStore appStateStore,
    WebSettingsStore settingsStore,
    SourceIpaManager sourceIpaManager,
    IOptions<WebUiOptions> options)
{
    private static readonly HashSet<string> SupportedEntitlementKeys =
    [
        "application-identifier",
        "com.apple.developer.team-identifier",
        "keychain-access-groups",
        "get-task-allow",
    ];

    public async Task<DashboardViewModel> GetDashboardAsync(CancellationToken cancellationToken)
    {
        var (config, states) = await LoadConfigAndStateAsync(cancellationToken);
        var settings = await settingsStore.LoadAsync(options.Value.SettingsPath, cancellationToken);

        var appCards = config.Apps.Select(app =>
        {
            states.TryGetValue(app.Id, out var state);

            return new DashboardAppCard(
                AppId: app.Id,
                Name: app.Name,
                SourceVersion: state?.LatestBuildId ?? "n/a",
                EffectiveBundleId: app.Identity.EffectiveBundleId,
                LastSignAt: state?.LastSuccessfulSignAt,
                ProfileExpiresAt: state?.ProfileExpirationDate,
                NextSignAt: state?.NextSignDueAt,
                Status: state?.Status.ToString() ?? RuntimeStatus.Ready.ToString(),
                InstallUrl: BuildInstallUrl(settings.PublicBaseUrl, app.Publish.Slug));
        }).ToArray();

        var statusCounts = states.Values
            .GroupBy(x => x.Status)
            .ToDictionary(x => x.Key, x => x.Count());

        return new DashboardViewModel(
            AppleSessionStatus: settings.SessionStatus,
            AppCount: config.Apps.Count,
            ReadyCount: statusCounts.GetValueOrDefault(RuntimeStatus.Ready),
            FailedCount: statusCounts.GetValueOrDefault(RuntimeStatus.Failed),
            AuthRequiredCount: statusCounts.GetValueOrDefault(RuntimeStatus.AuthRequired),
            Apps: appCards);
    }

    public async Task<IReadOnlyList<AppListItemViewModel>> GetAppsAsync(CancellationToken cancellationToken)
    {
        var (config, states) = await LoadConfigAndStateAsync(cancellationToken);
        var settings = await settingsStore.LoadAsync(options.Value.SettingsPath, cancellationToken);

        return config.Apps.Select(app =>
        {
            states.TryGetValue(app.Id, out var state);
            return new AppListItemViewModel(
                AppId: app.Id,
                Name: app.Name,
                EffectiveBundleId: app.Identity.EffectiveBundleId,
                SourceBundleId: app.Identity.SourceBundleId,
                LastSignAt: state?.LastSuccessfulSignAt,
                NextSignAt: state?.NextSignDueAt,
                Status: state?.Status.ToString() ?? RuntimeStatus.Ready.ToString(),
                InstallUrl: BuildInstallUrl(settings.PublicBaseUrl, app.Publish.Slug));
        }).ToArray();
    }

    public async Task<AddAppResult> AddAppAsync(AddAppRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var tempIpaPath = await SaveUploadToTempAsync(request.Upload, cancellationToken);

        try
        {
            var appId = CreateAppId(request.PublishSlug, request.Name);
            var immutableSourcePath = Path.Combine(options.Value.SourceRootDirectory, appId, "source.ipa");

            var replace = await sourceIpaManager.ReplaceSourceAsync(
                uploadedIpaPath: tempIpaPath,
                immutableSourcePath: immutableSourcePath,
                limits: CreatePreflightLimits(),
                cancellationToken: cancellationToken);

            var (config, states) = await LoadConfigAndStateAsync(cancellationToken);
            if (config.Apps.Any(x => string.Equals(x.Id, appId, StringComparison.Ordinal)))
            {
                throw new InvalidOperationException($"App id '{appId}' already exists.");
            }

            var app = new ManagedAppConfig(
                Id: appId,
                Name: request.Name,
                Enabled: true,
                Source: new SourceArtifact(replace.Path, replace.Sha256, replace.UploadedAt),
                Identity: new BundleIdentity(replace.Metadata.SourceBundleId, request.EffectiveBundleId),
                Signing: new AppSigningConfig(request.RemoveExtensions),
                Schedule: new AppScheduleConfig(request.AutoSign, request.IntervalHours),
                Publish: new AppPublishConfig(appId));

            await appConfigStore.SaveAsync(
                options.Value.AppConfigPath,
                new AppConfig(AppConfigStore.CurrentVersion, [.. config.Apps, app]),
                cancellationToken);

            states[appId] = new AppRuntimeState(RuntimeStatus.Ready, null, DateTimeOffset.UtcNow, null, null, null, null, null);
            await appStateStore.SaveAsync(options.Value.AppStatePath, new AppState(AppStateStore.CurrentVersion, states), cancellationToken);

            var unsupportedEntitlements = replace.Metadata.Entitlements
                .Where(x => !SupportedEntitlementKeys.Contains(x))
                .ToArray();

            return new AddAppResult(
                AppId: appId,
                Name: request.Name,
                Version: replace.Metadata.Version,
                SourceBundleId: replace.Metadata.SourceBundleId,
                EffectiveBundleId: request.EffectiveBundleId,
                Extensions: replace.Metadata.Extensions,
                EstimatedAppIdsUsed: config.Apps.Count + 1,
                UnsupportedEntitlements: unsupportedEntitlements,
                Warnings: replace.Metadata.Warnings);
        }
        finally
        {
            SafeDelete(tempIpaPath);
        }
    }

    public async Task<ReplaceAppResult> ReplaceAppSourceAsync(string appId, UploadIpaRequest upload, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(appId);
        ArgumentNullException.ThrowIfNull(upload);

        var tempIpaPath = await SaveUploadToTempAsync(upload, cancellationToken);

        try
        {
            var (config, states) = await LoadConfigAndStateAsync(cancellationToken);
            var app = config.Apps.FirstOrDefault(x => string.Equals(x.Id, appId, StringComparison.Ordinal))
                ?? throw new InvalidOperationException($"App '{appId}' not found.");

            var replace = await sourceIpaManager.ReplaceSourceAsync(
                uploadedIpaPath: tempIpaPath,
                immutableSourcePath: app.Source.Path,
                limits: CreatePreflightLimits(),
                cancellationToken: cancellationToken);

            var updatedApp = app with
            {
                Source = new SourceArtifact(replace.Path, replace.Sha256, replace.UploadedAt),
                Identity = app.Identity with { SourceBundleId = replace.Metadata.SourceBundleId },
            };

            var updatedApps = config.Apps.Select(x => x.Id == appId ? updatedApp : x).ToArray();
            await appConfigStore.SaveAsync(options.Value.AppConfigPath, new AppConfig(AppConfigStore.CurrentVersion, updatedApps), cancellationToken);

            return new ReplaceAppResult(
                AppId: appId,
                NewSourceBundleId: replace.Metadata.SourceBundleId,
                NewSha256: replace.Sha256,
                Version: replace.Metadata.Version,
                Warnings: replace.Metadata.Warnings);
        }
        finally
        {
            SafeDelete(tempIpaPath);
        }
    }

    public async Task RequestSignNowAsync(string appId, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(appId);

        var config = await appConfigStore.LoadAsync(options.Value.AppConfigPath, cancellationToken)
            ?? new AppConfig(AppConfigStore.CurrentVersion, []);

        if (!config.Apps.Any(x => string.Equals(x.Id, appId, StringComparison.Ordinal)))
        {
            throw new InvalidOperationException($"App '{appId}' not found.");
        }

        var (_, states) = await LoadConfigAndStateAsync(cancellationToken);
        states.TryGetValue(appId, out var current);

        var now = DateTimeOffset.UtcNow;
        states[appId] = current is null
            ? new AppRuntimeState(RuntimeStatus.Ready, null, now, null, null, null, now, null)
            : current with
            {
                Status = RuntimeStatus.Ready,
                NextSignDueAt = now,
                LastPromptAt = now,
                LastErrorCode = null,
            };

        await appStateStore.SaveAsync(options.Value.AppStatePath, new AppState(AppStateStore.CurrentVersion, states), cancellationToken);
    }

    public async Task<BuildHistoryViewModel> GetBuildHistoryAsync(CancellationToken cancellationToken)
    {
        var (config, states) = await LoadConfigAndStateAsync(cancellationToken);

        var items = config.Apps.Select(app =>
        {
            states.TryGetValue(app.Id, out var state);
            return new BuildHistoryItem(
                AppId: app.Id,
                AppName: app.Name,
                LatestBuildId: state?.LatestBuildId,
                LastSuccessfulSignAt: state?.LastSuccessfulSignAt,
                ProfileExpirationDate: state?.ProfileExpirationDate,
                LastErrorCode: state?.LastErrorCode,
                Status: state?.Status.ToString() ?? RuntimeStatus.Ready.ToString());
        }).OrderByDescending(x => x.LastSuccessfulSignAt).ToArray();

        return new BuildHistoryViewModel(items);
    }

    public Task<WebSettings> GetSettingsAsync(CancellationToken cancellationToken)
        => settingsStore.LoadAsync(options.Value.SettingsPath, cancellationToken);

    public Task SaveSettingsAsync(WebSettings settings, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(settings);

        var normalized = settings with
        {
            PublicBaseUrl = ValidateAndNormalizePublicBaseUrl(settings.PublicBaseUrl),
        };

        return settingsStore.SaveAsync(options.Value.SettingsPath, normalized, cancellationToken);
    }

    public async Task<AppleAccountStatusViewModel> GetAppleStatusAsync(CancellationToken cancellationToken)
    {
        var settings = await settingsStore.LoadAsync(options.Value.SettingsPath, cancellationToken);
        return new AppleAccountStatusViewModel(
            AppleIdMasked: settings.AppleIdMasked,
            TeamId: settings.TeamId,
            SessionStatus: settings.SessionStatus,
            CertificateStatus: settings.CertificateStatus,
            LoginCommand: "docker compose exec sign-manager dotnet /app/cli/SignManager.Cli.dll apple login");
    }

    public async Task<AppReplaceSummaryViewModel?> GetAppReplaceSummaryAsync(string appId, CancellationToken cancellationToken)
    {
        var (config, _) = await LoadConfigAndStateAsync(cancellationToken);
        var app = config.Apps.FirstOrDefault(x => string.Equals(x.Id, appId, StringComparison.Ordinal));
        if (app is null)
        {
            return null;
        }

        return new AppReplaceSummaryViewModel(app.Id, app.Name, app.Identity.SourceBundleId, app.Identity.EffectiveBundleId, app.Source.Sha256);
    }

    private async Task<(AppConfig Config, Dictionary<string, AppRuntimeState> States)> LoadConfigAndStateAsync(CancellationToken cancellationToken)
    {
        var config = await appConfigStore.LoadAsync(options.Value.AppConfigPath, cancellationToken)
            ?? new AppConfig(AppConfigStore.CurrentVersion, []);

        var state = await appStateStore.LoadAsync(options.Value.AppStatePath, cancellationToken)
            ?? new AppState(AppStateStore.CurrentVersion, new Dictionary<string, AppRuntimeState>(StringComparer.Ordinal));

        return (config, state.Apps.ToDictionary(x => x.Key, x => x.Value, StringComparer.Ordinal));
    }

    private static string BuildInstallUrl(string publicBaseUrl, string slug)
    {
        var normalizedBaseUrl = ValidateAndNormalizePublicBaseUrl(publicBaseUrl);
        var manifestUrl = $"{normalizedBaseUrl}/apps/{slug}/latest/manifest.plist";
        return ItmsServicesUrlBuilder.BuildInstallUrl(manifestUrl);
    }

    private async Task<string> SaveUploadToTempAsync(UploadIpaRequest upload, CancellationToken cancellationToken)
    {
        if (upload.Length <= 0)
        {
            throw new InvalidOperationException("Uploaded IPA is empty.");
        }

        if (upload.Length > options.Value.UploadMaxBytes)
        {
            throw new InvalidOperationException($"Uploaded IPA exceeds max size limit of {options.Value.UploadMaxBytes} bytes.");
        }

        Directory.CreateDirectory(options.Value.UploadTempDirectory);
        var tempPath = Path.Combine(options.Value.UploadTempDirectory, $"{Guid.NewGuid():N}.ipa");

        await using var file = File.Create(tempPath);
        await upload.CopyToAsync(file, cancellationToken);
        await file.FlushAsync(cancellationToken);

        return tempPath;
    }

    private static void SafeDelete(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch
        {
            // Best-effort cleanup for temp uploads.
        }
    }

    private IpaPreflightLimits CreatePreflightLimits()
        => new(
            MaxUploadBytes: options.Value.UploadMaxBytes,
            MaxEntries: options.Value.UploadMaxEntries,
            MaxTotalExpandedBytes: options.Value.UploadMaxTotalExpandedBytes,
            MaxSingleEntryBytes: options.Value.UploadMaxSingleEntryBytes,
            MaxCompressionRatio: options.Value.UploadMaxCompressionRatio);

    private static string CreateAppId(string? publishSlug, string name)
    {
        var raw = string.IsNullOrWhiteSpace(publishSlug) ? name : publishSlug;
        var slug = Regex.Replace(raw.Trim().ToLowerInvariant(), "[^a-z0-9-]+", "-");
        slug = Regex.Replace(slug, "-+", "-").Trim('-');

        if (string.IsNullOrWhiteSpace(slug))
        {
            throw new InvalidOperationException("Unable to create app id from input.");
        }

        return slug;
    }

    private static string ValidateAndNormalizePublicBaseUrl(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidOperationException("Public base URL is required.");
        }

        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri)
            || !string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase)
            || string.IsNullOrWhiteSpace(uri.Host))
        {
            throw new InvalidOperationException("Public base URL must be an absolute HTTPS URL.");
        }

        return uri.ToString().TrimEnd('/');
    }
}

public sealed record UploadIpaRequest(long Length, Func<Stream, CancellationToken, Task> CopyToAsync);

public sealed record AddAppRequest(
    string Name,
    string EffectiveBundleId,
    bool RemoveExtensions,
    bool AutoSign,
    int IntervalHours,
    string? PublishSlug,
    UploadIpaRequest Upload);

public sealed record AddAppResult(
    string AppId,
    string Name,
    string Version,
    string SourceBundleId,
    string EffectiveBundleId,
    IReadOnlyList<string> Extensions,
    int EstimatedAppIdsUsed,
    IReadOnlyList<string> UnsupportedEntitlements,
    IReadOnlyList<string> Warnings);

public sealed record ReplaceAppResult(
    string AppId,
    string NewSourceBundleId,
    string NewSha256,
    string Version,
    IReadOnlyList<string> Warnings);

public sealed record DashboardViewModel(
    string AppleSessionStatus,
    int AppCount,
    int ReadyCount,
    int FailedCount,
    int AuthRequiredCount,
    IReadOnlyList<DashboardAppCard> Apps);

public sealed record DashboardAppCard(
    string AppId,
    string Name,
    string SourceVersion,
    string EffectiveBundleId,
    DateTimeOffset? LastSignAt,
    DateTimeOffset? ProfileExpiresAt,
    DateTimeOffset? NextSignAt,
    string Status,
    string InstallUrl);

public sealed record AppListItemViewModel(
    string AppId,
    string Name,
    string EffectiveBundleId,
    string SourceBundleId,
    DateTimeOffset? LastSignAt,
    DateTimeOffset? NextSignAt,
    string Status,
    string InstallUrl);

public sealed record BuildHistoryViewModel(IReadOnlyList<BuildHistoryItem> Items);

public sealed record BuildHistoryItem(
    string AppId,
    string AppName,
    string? LatestBuildId,
    DateTimeOffset? LastSuccessfulSignAt,
    DateTimeOffset? ProfileExpirationDate,
    string? LastErrorCode,
    string Status);

public sealed record AppleAccountStatusViewModel(
    string? AppleIdMasked,
    string? TeamId,
    string SessionStatus,
    string CertificateStatus,
    string LoginCommand);

public sealed record AppReplaceSummaryViewModel(
    string AppId,
    string Name,
    string SourceBundleId,
    string EffectiveBundleId,
    string SourceSha256);
