using System.IO.Compression;
using System.Text;
using Microsoft.Extensions.Options;
using SignManager.Infrastructure.Persistence;
using SignManager.Signing.Ipa;
using SignManager.Web.Services;

namespace SignManager.IntegrationTests;

public class Milestone10WebUiTests
{
    [Fact]
    public async Task AddApp_ShouldPersistConfigStateAndExposeMetadata()
    {
        var root = CreateTempRoot();

        try
        {
            var options = CreateOptions(root);
            var service = CreateService(options);
            var ipaBytes = CreateValidIpaBytes();

            var result = await service.AddAppAsync(
                new AddAppRequest(
                    Name: "QR Scanner",
                    EffectiveBundleId: "com.vendor.qr.effective",
                    RemoveExtensions: true,
                    AutoSign: true,
                    IntervalHours: 48,
                    PublishSlug: "qr-scanner",
                    Upload: new UploadIpaRequest(
                        ipaBytes.Length,
                        async (target, ct) =>
                        {
                            await using var source = new MemoryStream(ipaBytes, writable: false);
                            await source.CopyToAsync(target, ct);
                        })),
                CancellationToken.None);

            Assert.Equal("qr-scanner", result.AppId);
            Assert.Equal("com.vendor.qr", result.SourceBundleId);
            Assert.Equal("com.vendor.qr.effective", result.EffectiveBundleId);

            var config = await new AppConfigStore().LoadAsync(options.AppConfigPath, CancellationToken.None);
            Assert.NotNull(config);
            Assert.Single(config!.Apps);
            Assert.Equal("qr-scanner", config.Apps[0].Id);

            var state = await new AppStateStore().LoadAsync(options.AppStatePath, CancellationToken.None);
            Assert.NotNull(state);
            Assert.True(state!.Apps.ContainsKey("qr-scanner"));
        }
        finally
        {
            Cleanup(root);
        }
    }

    [Fact]
    public async Task SignNow_ShouldSetNextDueToNowOrEarlier()
    {
        var root = CreateTempRoot();

        try
        {
            var options = CreateOptions(root);
            var service = CreateService(options);
            var ipaBytes = CreateValidIpaBytes();

            await service.AddAppAsync(
                new AddAppRequest(
                    Name: "Reader",
                    EffectiveBundleId: "com.vendor.reader",
                    RemoveExtensions: true,
                    AutoSign: true,
                    IntervalHours: 48,
                    PublishSlug: "reader",
                    Upload: new UploadIpaRequest(
                        ipaBytes.Length,
                        async (target, ct) =>
                        {
                            await using var source = new MemoryStream(ipaBytes, writable: false);
                            await source.CopyToAsync(target, ct);
                        })),
                CancellationToken.None);

            await service.RequestSignNowAsync("reader", CancellationToken.None);

            var state = await new AppStateStore().LoadAsync(options.AppStatePath, CancellationToken.None);
            Assert.NotNull(state);
            Assert.True(state!.Apps.TryGetValue("reader", out var appState));
            Assert.NotNull(appState!.NextSignDueAt);
            Assert.True(appState.NextSignDueAt <= DateTimeOffset.UtcNow);
        }
        finally
        {
            Cleanup(root);
        }
    }

    [Fact]
    public async Task Dashboard_ShouldReturnAppleStatusAndAppCounts()
    {
        var root = CreateTempRoot();

        try
        {
            var options = CreateOptions(root);
            var service = CreateService(options);
            var ipaBytes = CreateValidIpaBytes();

            await service.SaveSettingsAsync(
                new WebSettings(
                    PublicBaseUrl: "https://ios.example.com",
                    AppleIdMasked: "a***@icloud.com",
                    TeamId: "TEAM123",
                    SessionStatus: "Valid",
                    CertificateStatus: "Ready"),
                CancellationToken.None);

            await service.AddAppAsync(
                new AddAppRequest(
                    Name: "Wallet",
                    EffectiveBundleId: "com.vendor.wallet",
                    RemoveExtensions: true,
                    AutoSign: true,
                    IntervalHours: 48,
                    PublishSlug: "wallet",
                    Upload: new UploadIpaRequest(
                        ipaBytes.Length,
                        async (target, ct) =>
                        {
                            await using var source = new MemoryStream(ipaBytes, writable: false);
                            await source.CopyToAsync(target, ct);
                        })),
                CancellationToken.None);

            var dashboard = await service.GetDashboardAsync(CancellationToken.None);

            Assert.Equal("Valid", dashboard.AppleSessionStatus);
            Assert.Equal(1, dashboard.AppCount);
            Assert.Single(dashboard.Apps);
            Assert.Contains("itms-services://", dashboard.Apps[0].InstallUrl, StringComparison.Ordinal);
        }
        finally
        {
            Cleanup(root);
        }
    }

    private static WebAppService CreateService(WebUiOptions webOptions)
    {
        var options = Options.Create(webOptions);
        return new WebAppService(
            new AppConfigStore(),
            new AppStateStore(),
            new WebSettingsStore(),
            new SourceIpaManager(new IpaPreflightService()),
            options);
    }

    private static WebUiOptions CreateOptions(string root)
        => new(
            AppConfigPath: Path.Combine(root, "config", "apps.json"),
            AppStatePath: Path.Combine(root, "state", "state.json"),
            SettingsPath: Path.Combine(root, "config", "settings.json"),
            UploadTempDirectory: Path.Combine(root, "uploads"));

    private static byte[] CreateValidIpaBytes()
    {
        using var memory = new MemoryStream();
        using (var archive = new ZipArchive(memory, ZipArchiveMode.Create, leaveOpen: true))
        {
            var entry = archive.CreateEntry("Payload/Test.app/Info.plist");
            using var stream = entry.Open();
            using var writer = new StreamWriter(stream, Encoding.UTF8, leaveOpen: false);
            writer.Write("<?xml version=\"1.0\" encoding=\"UTF-8\"?><plist version=\"1.0\"><dict><key>CFBundleDisplayName</key><string>Test App</string><key>CFBundleIdentifier</key><string>com.vendor.qr</string><key>CFBundleShortVersionString</key><string>1.0.0</string><key>CFBundleVersion</key><string>1</string></dict></plist>");
        }

        return memory.ToArray();
    }

    private static string CreateTempRoot()
    {
        var root = Path.Combine(Path.GetTempPath(), "sign-manager-web-tests", Guid.NewGuid().ToString("N"));
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
