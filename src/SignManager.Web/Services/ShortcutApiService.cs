using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using SignManager.Core.Models;
using SignManager.Infrastructure.Ota;
using SignManager.Infrastructure.Persistence;

namespace SignManager.Web.Services;

public sealed class ShortcutApiService(
    AppConfigStore appConfigStore,
    AppStateStore appStateStore,
    WebSettingsStore settingsStore,
    ShortcutTokenStore tokenStore,
    IOptions<WebUiOptions> options)
{
    public async Task<bool> ValidateTokenAsync(string? bearerToken, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(bearerToken))
        {
            return false;
        }

        if (IsBootstrapTokenMatch(options.Value.ShortcutBootstrapToken, bearerToken))
        {
            return true;
        }

        return await tokenStore.ValidateAsync(options.Value.ShortcutTokenPath, bearerToken, cancellationToken);
    }

    public async Task<ShortcutRefreshPlanResponse> GetRefreshPlanAsync(DateTimeOffset now, CancellationToken cancellationToken)
    {
        var config = await appConfigStore.LoadAsync(options.Value.AppConfigPath, cancellationToken)
            ?? new AppConfig(AppConfigStore.CurrentVersion, []);
        var state = await appStateStore.LoadAsync(options.Value.AppStatePath, cancellationToken)
            ?? new AppState(AppStateStore.CurrentVersion, new Dictionary<string, AppRuntimeState>(StringComparer.Ordinal));
        var settings = await settingsStore.LoadAsync(options.Value.SettingsPath, cancellationToken);

        var cooldown = TimeSpan.FromHours(Math.Max(1, options.Value.ShortcutPromptCooldownHours));
        var opportunity = TimeSpan.FromHours(Math.Max(1, options.Value.ShortcutPromptOpportunityHours));

        var candidates = config.Apps
            .Where(x => x.Enabled)
            .Select(app =>
            {
                state.Apps.TryGetValue(app.Id, out var runtime);
                return (App: app, Runtime: runtime);
            })
            .Where(x => x.Runtime is not null)
            .Where(x => x.Runtime!.Status == RuntimeStatus.Ready)
            .Where(x => !string.IsNullOrWhiteSpace(x.Runtime!.LatestBuildId))
            .Where(x => IsPromptDue(x.Runtime!, now, cooldown, opportunity))
            .OrderBy(x => x.Runtime!.NextSignDueAt ?? DateTimeOffset.MaxValue)
            .ThenBy(x => x.Runtime!.LastPromptAt ?? DateTimeOffset.MinValue)
            .ThenBy(x => x.App.Id, StringComparer.Ordinal)
            .ToArray();

        var first = candidates.FirstOrDefault();
        if (first.App is null || first.Runtime is null)
        {
            return new ShortcutRefreshPlanResponse(false, null);
        }

        var normalizedPublicBaseUrl = ValidateAndNormalizePublicBaseUrl(settings.PublicBaseUrl);
        var manifestUrl = $"{normalizedPublicBaseUrl}/apps/{first.App.Publish.Slug}/latest/manifest.plist";
        var installUrl = ItmsServicesUrlBuilder.BuildInstallUrl(manifestUrl);

        return new ShortcutRefreshPlanResponse(
            Due: true,
            App: new ShortcutRefreshPlanApp(
                Id: first.App.Id,
                Name: first.App.Name,
                BuildId: first.Runtime.LatestBuildId!,
                ProfileExpiresAt: first.Runtime.ProfileExpirationDate,
                InstallUrl: installUrl));
    }

    public async Task<bool> MarkPromptedAsync(string appId, string buildId, DateTimeOffset now, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(appId);
        ArgumentException.ThrowIfNullOrWhiteSpace(buildId);

        var state = await appStateStore.LoadAsync(options.Value.AppStatePath, cancellationToken)
            ?? new AppState(AppStateStore.CurrentVersion, new Dictionary<string, AppRuntimeState>(StringComparer.Ordinal));

        if (!state.Apps.TryGetValue(appId, out var runtime) || runtime is null)
        {
            return false;
        }

        if (!string.Equals(runtime.LatestBuildId, buildId, StringComparison.Ordinal))
        {
            return false;
        }

        var updated = runtime with { LastPromptAt = now };
        var states = state.Apps.ToDictionary(x => x.Key, x => x.Value, StringComparer.Ordinal);
        states[appId] = updated;

        await appStateStore.SaveAsync(options.Value.AppStatePath, new AppState(AppStateStore.CurrentVersion, states), cancellationToken);
        return true;
    }

    private static bool IsPromptDue(AppRuntimeState state, DateTimeOffset now, TimeSpan cooldown, TimeSpan opportunity)
    {
        if (state.NextSignDueAt is null)
        {
            return false;
        }

        var dueAt = state.NextSignDueAt.Value;
        if (now < dueAt)
        {
            return false;
        }

        if (now - dueAt > opportunity)
        {
            return false;
        }

        if (state.LastPromptAt is null)
        {
            return true;
        }

        return now - state.LastPromptAt.Value >= cooldown;
    }

    private static bool IsBootstrapTokenMatch(string? configuredBootstrapToken, string bearerToken)
    {
        if (string.IsNullOrWhiteSpace(configuredBootstrapToken))
        {
            return false;
        }

        var configuredBytes = Encoding.UTF8.GetBytes(configuredBootstrapToken);
        var providedBytes = Encoding.UTF8.GetBytes(bearerToken);
        return CryptographicOperations.FixedTimeEquals(configuredBytes, providedBytes);
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

public sealed record ShortcutRefreshPlanResponse(bool Due, ShortcutRefreshPlanApp? App);

public sealed record ShortcutRefreshPlanApp(
    string Id,
    string Name,
    string BuildId,
    DateTimeOffset? ProfileExpiresAt,
    string InstallUrl);
