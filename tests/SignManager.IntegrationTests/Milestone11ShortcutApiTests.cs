using Microsoft.Extensions.Options;
using SignManager.Core.Models;
using SignManager.Infrastructure.Persistence;
using SignManager.Web.Services;

namespace SignManager.IntegrationTests;

public class Milestone11ShortcutApiTests
{
    [Fact]
    public async Task ValidateToken_ShouldAcceptBootstrapAndStoredHashedToken()
    {
        var root = CreateTempRoot();

        try
        {
            var webOptions = CreateOptions(root);
            var tokenStore = new ShortcutTokenStore();
            await tokenStore.UpsertTokenAsync(webOptions.ShortcutTokenPath, "iphone-main", "secret-token", CancellationToken.None);

            var service = CreateService(webOptions, tokenStore);

            Assert.True(await service.ValidateTokenAsync("dev-shortcut-token", CancellationToken.None));
            Assert.True(await service.ValidateTokenAsync("secret-token", CancellationToken.None));
            Assert.False(await service.ValidateTokenAsync("wrong", CancellationToken.None));

            await tokenStore.RevokeAsync(webOptions.ShortcutTokenPath, "iphone-main", CancellationToken.None);
            Assert.False(await service.ValidateTokenAsync("secret-token", CancellationToken.None));
        }
        finally
        {
            Cleanup(root);
        }
    }

    [Fact]
    public async Task RefreshPlan_ShouldReturnOneDueApp_UsingPriorityPolicy()
    {
        var root = CreateTempRoot();

        try
        {
            var webOptions = CreateOptions(root);
            await SeedAppsAndStateAsync(webOptions);

            var service = CreateService(webOptions);
            var now = new DateTimeOffset(2026, 9, 6, 12, 0, 0, TimeSpan.Zero);

            var response = await service.GetRefreshPlanAsync(now, CancellationToken.None);

            Assert.True(response.Due);
            Assert.NotNull(response.App);
            Assert.Equal("app-b", response.App!.Id);
            Assert.Equal("build-b", response.App.BuildId);
            Assert.Contains("itms-services://", response.App.InstallUrl, StringComparison.Ordinal);
        }
        finally
        {
            Cleanup(root);
        }
    }

    [Fact]
    public async Task Prompted_ShouldUpdateLastPromptAt_AndCooldownShouldSuppressDue()
    {
        var root = CreateTempRoot();

        try
        {
            var webOptions = CreateOptions(root);
            await SeedSingleDueAppAsync(webOptions, appId: "qr", buildId: "build-001");

            var service = CreateService(webOptions);
            var now = new DateTimeOffset(2026, 9, 6, 12, 0, 0, TimeSpan.Zero);

            var before = await service.GetRefreshPlanAsync(now, CancellationToken.None);
            Assert.True(before.Due);
            Assert.NotNull(before.App);

            var marked = await service.MarkPromptedAsync("qr", "build-001", now, CancellationToken.None);
            Assert.True(marked);

            var state = await new AppStateStore().LoadAsync(webOptions.AppStatePath, CancellationToken.None);
            Assert.NotNull(state);
            Assert.Equal(now, state!.Apps["qr"].LastPromptAt);

            var withinCooldown = await service.GetRefreshPlanAsync(now.AddHours(1), CancellationToken.None);
            Assert.False(withinCooldown.Due);
            Assert.Null(withinCooldown.App);

            var afterCooldown = await service.GetRefreshPlanAsync(now.AddHours(13), CancellationToken.None);
            Assert.True(afterCooldown.Due);
            Assert.NotNull(afterCooldown.App);
            Assert.Equal("qr", afterCooldown.App!.Id);
        }
        finally
        {
            Cleanup(root);
        }
    }

    private static ShortcutApiService CreateService(WebUiOptions webOptions, ShortcutTokenStore? store = null)
    {
        var options = Options.Create(webOptions);
        return new ShortcutApiService(
            new AppConfigStore(),
            new AppStateStore(),
            new WebSettingsStore(),
            store ?? new ShortcutTokenStore(),
            options);
    }

    private static async Task SeedAppsAndStateAsync(WebUiOptions options)
    {
        await new WebSettingsStore().SaveAsync(
            options.SettingsPath,
            new WebSettings("https://ios.example.com", "a***@icloud.com", "TEAM", "Valid", "Ready"),
            CancellationToken.None);

        var apps = new AppConfig(
            AppConfigStore.CurrentVersion,
            [
                new ManagedAppConfig(
                    Id: "app-a",
                    Name: "App A",
                    Enabled: true,
                    Source: new SourceArtifact("data/sources/app-a/source.ipa", "sha-a", DateTimeOffset.UtcNow),
                    Identity: new BundleIdentity("com.vendor.a", "com.vendor.a"),
                    Signing: new AppSigningConfig(true),
                    Schedule: new AppScheduleConfig(true, 48),
                    Publish: new AppPublishConfig("app-a")),
                new ManagedAppConfig(
                    Id: "app-b",
                    Name: "App B",
                    Enabled: true,
                    Source: new SourceArtifact("data/sources/app-b/source.ipa", "sha-b", DateTimeOffset.UtcNow),
                    Identity: new BundleIdentity("com.vendor.b", "com.vendor.b"),
                    Signing: new AppSigningConfig(true),
                    Schedule: new AppScheduleConfig(true, 48),
                    Publish: new AppPublishConfig("app-b")),
            ]);

        await new AppConfigStore().SaveAsync(options.AppConfigPath, apps, CancellationToken.None);

        var now = new DateTimeOffset(2026, 9, 6, 12, 0, 0, TimeSpan.Zero);
        var state = new AppState(
            AppStateStore.CurrentVersion,
            new Dictionary<string, AppRuntimeState>
            {
                ["app-a"] = new(
                    RuntimeStatus.Ready,
                    LastSuccessfulSignAt: now.AddHours(-2),
                    NextSignDueAt: now.AddHours(-1),
                    LatestBuildId: "build-a",
                    ProfileCreationDate: now.AddDays(-1),
                    ProfileExpirationDate: now.AddDays(6),
                    LastPromptAt: now.AddHours(-2),
                    LastErrorCode: null),
                ["app-b"] = new(
                    RuntimeStatus.Ready,
                    LastSuccessfulSignAt: now.AddHours(-3),
                    NextSignDueAt: now.AddHours(-2),
                    LatestBuildId: "build-b",
                    ProfileCreationDate: now.AddDays(-1),
                    ProfileExpirationDate: now.AddDays(6),
                    LastPromptAt: now.AddHours(-14),
                    LastErrorCode: null),
            });

        await new AppStateStore().SaveAsync(options.AppStatePath, state, CancellationToken.None);
    }

    private static async Task SeedSingleDueAppAsync(WebUiOptions options, string appId, string buildId)
    {
        await new WebSettingsStore().SaveAsync(
            options.SettingsPath,
            new WebSettings("https://ios.example.com", "a***@icloud.com", "TEAM", "Valid", "Ready"),
            CancellationToken.None);

        var config = new AppConfig(
            AppConfigStore.CurrentVersion,
            [
                new ManagedAppConfig(
                    Id: appId,
                    Name: "QR",
                    Enabled: true,
                    Source: new SourceArtifact($"data/sources/{appId}/source.ipa", "sha", DateTimeOffset.UtcNow),
                    Identity: new BundleIdentity("com.vendor.qr", "com.vendor.qr"),
                    Signing: new AppSigningConfig(true),
                    Schedule: new AppScheduleConfig(true, 48),
                    Publish: new AppPublishConfig(appId)),
            ]);

        await new AppConfigStore().SaveAsync(options.AppConfigPath, config, CancellationToken.None);

        var now = new DateTimeOffset(2026, 9, 6, 12, 0, 0, TimeSpan.Zero);
        var state = new AppState(
            AppStateStore.CurrentVersion,
            new Dictionary<string, AppRuntimeState>
            {
                [appId] = new(
                    RuntimeStatus.Ready,
                    LastSuccessfulSignAt: now.AddHours(-2),
                    NextSignDueAt: now.AddMinutes(-1),
                    LatestBuildId: buildId,
                    ProfileCreationDate: now.AddDays(-1),
                    ProfileExpirationDate: now.AddDays(6),
                    LastPromptAt: null,
                    LastErrorCode: null),
            });

        await new AppStateStore().SaveAsync(options.AppStatePath, state, CancellationToken.None);
    }

    private static WebUiOptions CreateOptions(string root)
        => new(
            AppConfigPath: Path.Combine(root, "config", "apps.json"),
            AppStatePath: Path.Combine(root, "state", "state.json"),
            SettingsPath: Path.Combine(root, "config", "settings.json"),
            UploadTempDirectory: Path.Combine(root, "uploads"),
            ShortcutTokenPath: Path.Combine(root, "config", "shortcut-tokens.json"),
            ShortcutPromptCooldownHours: 12,
            ShortcutPromptOpportunityHours: 72,
            ShortcutBootstrapToken: "dev-shortcut-token");

    private static string CreateTempRoot()
    {
        var root = Path.Combine(Path.GetTempPath(), "sign-manager-shortcut-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        return root;
    }

    private static void Cleanup(string root)
    {
        if (Directory.Exists(root))
        {
            Directory.Delete(root, recursive: true);
        }
    }
}
