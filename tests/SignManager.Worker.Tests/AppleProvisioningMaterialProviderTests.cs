using Microsoft.Extensions.Options;
using SignManager.Apple.Auth;
using SignManager.Apple.Developer;
using SignManager.Core.Constants;
using SignManager.Core.Models;
using SignManager.Worker.Signing;

namespace SignManager.Worker.Tests;

public class AppleProvisioningMaterialProviderTests
{
    [Fact]
    public async Task PrepareAsync_ShouldReturnMaterial_AndPersistStateArtifacts()
    {
        var root = CreateTempRoot();

        try
        {
            var schedulerOptions = CreateOptions(root);
            var sessionStore = new InMemorySessionStore(new AppleSession("adsid", "token", DateTimeOffset.UtcNow, null));
            var developerClient = new FakeDeveloperClient(withTeam: true);
            var auth = new AppleAuthenticationService(new NoopGrandSlamClient(), developerClient, sessionStore);
            var provisioning = new AppleProvisioningService(
                developerClient,
                new CsrGenerator(),
                new ProvisioningProfileParser(),
                new SignManager.Core.Policies.ProfileFreshnessPolicy(24));

            var provider = new AppleProvisioningMaterialProvider(
                auth,
                developerClient,
                provisioning,
                Options.Create(schedulerOptions));

            var now = new DateTimeOffset(2026, 9, 6, 0, 0, 0, TimeSpan.Zero);
            var job = new SigningJob(Guid.NewGuid().ToString("N"), "app1", "sha", SigningJobType.Auto, now, SigningJobStatus.Queued, 0);
            var app = new ManagedAppConfig(
                Id: "app1",
                Name: "App One",
                Enabled: true,
                Source: new SourceArtifact("source.ipa", "sha", now),
                Identity: new BundleIdentity("com.vendor.app", "com.eddie.sideload.app1"),
                Signing: new AppSigningConfig(true),
                Schedule: new AppScheduleConfig(true, 48),
                Publish: new AppPublishConfig("app1"));

            var material = await provider.PrepareAsync(job, app, CancellationToken.None);

            Assert.True(File.Exists(material.PrivateKeyPath));
            Assert.True(File.Exists(material.CertificatePath));
            Assert.True(File.Exists(material.MobileProvisionPath));

            var keyPem = await File.ReadAllTextAsync(material.PrivateKeyPath);
            Assert.Contains("BEGIN PRIVATE KEY", keyPem, StringComparison.Ordinal);

            Assert.True(File.Exists(Path.Combine(schedulerOptions.SigningStateRoot, "private-key.pk8")));
            Assert.True(File.Exists(Path.Combine(schedulerOptions.SigningStateRoot, "certificate.pem")));
            Assert.True(File.Exists(Path.Combine(schedulerOptions.SigningStateRoot, "provisioning", "app1.json")));

            Assert.Equal("com.eddie.sideload.app1", material.Provisioning.BundleId);
        }
        finally
        {
            Cleanup(root);
        }
    }

    [Fact]
    public async Task PrepareAsync_ShouldThrowAuthRequired_WhenSessionRestoreFails()
    {
        var root = CreateTempRoot();

        try
        {
            var schedulerOptions = CreateOptions(root);
            var sessionStore = new InMemorySessionStore(session: null);
            var developerClient = new FakeDeveloperClient(withTeam: true);
            var auth = new AppleAuthenticationService(new NoopGrandSlamClient(), developerClient, sessionStore);
            var provisioning = new AppleProvisioningService(
                developerClient,
                new CsrGenerator(),
                new ProvisioningProfileParser(),
                new SignManager.Core.Policies.ProfileFreshnessPolicy(24));

            var provider = new AppleProvisioningMaterialProvider(
                auth,
                developerClient,
                provisioning,
                Options.Create(schedulerOptions));

            var now = new DateTimeOffset(2026, 9, 6, 0, 0, 0, TimeSpan.Zero);
            var job = new SigningJob(Guid.NewGuid().ToString("N"), "app1", "sha", SigningJobType.Auto, now, SigningJobStatus.Queued, 0);
            var app = new ManagedAppConfig(
                Id: "app1",
                Name: "App One",
                Enabled: true,
                Source: new SourceArtifact("source.ipa", "sha", now),
                Identity: new BundleIdentity("com.vendor.app", "com.eddie.sideload.app1"),
                Signing: new AppSigningConfig(true),
                Schedule: new AppScheduleConfig(true, 48),
                Publish: new AppPublishConfig("app1"));

            var ex = await Assert.ThrowsAsync<SigningWorkflowException>(() => provider.PrepareAsync(job, app, CancellationToken.None));
            Assert.Equal(StableErrorCodes.AuthRequired, ex.ErrorCode);
        }
        finally
        {
            Cleanup(root);
        }
    }

    private static SchedulerOptions CreateOptions(string root)
        => new(
            AppConfigPath: Path.Combine(root, "config", "apps.json"),
            AppStatePath: Path.Combine(root, "state", "state.json"),
            WorkspaceRoot: Path.Combine(root, "jobs"),
            ZsignExecutablePath: "zsign",
            ScanIntervalSeconds: 30,
            MaxProcessOutputBytes: 262144,
            ZsignTimeoutSeconds: 1200,
            CleanupMaxAgeHours: 72,
            ProfileMinimumFreshHours: 24,
            AppleDeviceUdid: "UDID-1",
            AppleDeviceName: "Eddie iPhone",
            AppleTeamId: "TEAM123",
            AppleProfileNamePrefix: "signmanager",
            ApplePrivateKeyPassword: "test-password",
            SigningStateRoot: Path.Combine(root, "signing-state"),
            AppleSessionSecretsPath: Path.Combine(root, "signing-state", "secrets.enc"),
            AppleSessionMasterKeyPath: Path.Combine(root, "signing-state", "master.key"),
            AppleAnisetteBaseUrl: "http://anisette:6969/",
            AppleAnisetteHeadersPath: "headers",
            AppleGrandSlamBaseUrl: "http://apple.local/",
            AppleDeveloperBaseUrl: "http://apple.local/",
            AppleHttpTimeoutSeconds: 30,
            TelegramAlertsEnabled: false,
            TelegramBotToken: null,
            TelegramChatId: null);

    private static byte[] CreateProvisioningProfileBytes(string teamId, string bundleId, DateTimeOffset creationDate, DateTimeOffset expirationDate)
    {
        var content = $$"""
        <?xml version="1.0" encoding="UTF-8"?>
        <!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
        <plist version="1.0">
          <dict>
            <key>UUID</key>
            <string>11111111-2222-3333-4444-555555555555</string>
            <key>CreationDate</key>
            <date>{{creationDate:yyyy-MM-ddTHH:mm:ssZ}}</date>
            <key>ExpirationDate</key>
            <date>{{expirationDate:yyyy-MM-ddTHH:mm:ssZ}}</date>
            <key>TeamIdentifier</key>
            <array>
              <string>{{teamId}}</string>
            </array>
            <key>Entitlements</key>
            <dict>
              <key>application-identifier</key>
              <string>{{teamId}}.{{bundleId}}</string>
            </dict>
            <key>ProvisionedDevices</key>
            <array>
              <string>UDID-1</string>
            </array>
          </dict>
        </plist>
        """;

        return System.Text.Encoding.UTF8.GetBytes(content);
    }

    private static string CreateTempRoot()
    {
        var root = Path.Combine(Path.GetTempPath(), "sign-manager-worker-provisioning-tests", Guid.NewGuid().ToString("N"));
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

    private sealed class InMemorySessionStore(AppleSession? session) : IAppleSessionStore
    {
        private AppleSession? _session = session;

        public Task SaveAsync(AppleSession session, CancellationToken cancellationToken)
        {
            _session = session;
            return Task.CompletedTask;
        }

        public Task<AppleSession?> LoadAsync(CancellationToken cancellationToken)
            => Task.FromResult(_session);

        public Task ClearAsync(CancellationToken cancellationToken)
        {
            _session = null;
            return Task.CompletedTask;
        }
    }

    private sealed class NoopGrandSlamClient : IAppleGrandSlamClient
    {
        public Task<AppleLoginResult> LoginAsync(AppleLoginRequest request, CancellationToken cancellationToken)
            => Task.FromResult(AppleLoginResult.Failed(StableErrorCodes.AppleLoginFailed));

        public Task<AppleLoginResult> SubmitTwoFactorAsync(AppleTwoFactorRequest request, CancellationToken cancellationToken)
            => Task.FromResult(AppleLoginResult.Failed(StableErrorCodes.AppleTwoFactorFailed));
    }

    private sealed class FakeDeveloperClient(bool withTeam) : IAppleDeveloperClient
    {
        public Task<bool> ViewDeveloperAsync(CancellationToken cancellationToken)
            => Task.FromResult(withTeam);

        public Task<IReadOnlyList<DeveloperTeam>> GetTeamsAsync(CancellationToken cancellationToken)
            => Task.FromResult<IReadOnlyList<DeveloperTeam>>(withTeam ? [new DeveloperTeam("TEAM123", "Personal Team")] : []);

        public Task<RegisteredDevice> EnsureDeviceAsync(string teamId, string udid, string deviceName, CancellationToken cancellationToken)
            => Task.FromResult(new RegisteredDevice("device-1", udid, deviceName));

        public Task<DevelopmentCertificate> EnsureCertificateAsync(string teamId, string csrPem, CancellationToken cancellationToken)
            => Task.FromResult(new DevelopmentCertificate("cert-1", "SERIAL", "-----BEGIN CERTIFICATE-----\nTEST\n-----END CERTIFICATE-----", DateTimeOffset.UtcNow.AddDays(7)));

        public Task<AppIdentifier> EnsureAppIdAsync(string teamId, string bundleId, CancellationToken cancellationToken)
            => Task.FromResult(new AppIdentifier("app-1", bundleId, "App One"));

        public Task<ProvisioningProfile> CreateProvisioningProfileAsync(string teamId, string appIdId, string deviceId, string profileName, CancellationToken cancellationToken)
        {
            var creationDate = new DateTimeOffset(2026, 9, 6, 0, 0, 0, TimeSpan.Zero);
            var expirationDate = creationDate.AddDays(7);
            var profileBytes = CreateProvisioningProfileBytes(teamId, "com.eddie.sideload.app1", creationDate, expirationDate);

            return Task.FromResult(new ProvisioningProfile(
                ProfileId: "profile-1",
                Uuid: "11111111-2222-3333-4444-555555555555",
                CreationDate: creationDate,
                ExpirationDate: expirationDate,
                TeamId: teamId,
                BundleId: "com.eddie.sideload.app1",
                MobileProvisionBase64: Convert.ToBase64String(profileBytes)));
        }
    }
}
