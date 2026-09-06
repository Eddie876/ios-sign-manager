using SignManager.Core.Models;

namespace SignManager.Worker.Signing;

public interface IProvisioningMaterialProvider
{
    Task<ProvisioningMaterial> PrepareAsync(
        SigningJob job,
        ManagedAppConfig app,
        CancellationToken cancellationToken);
}

public interface ISigningArtifactSigner
{
    Task<SignedBuildArtifact> SignAsync(
        SignArtifactRequest request,
        CancellationToken cancellationToken);
}

public interface IGlobalSigningGate
{
    Task<T> RunExclusiveAsync<T>(Func<CancellationToken, Task<T>> operation, CancellationToken cancellationToken);
}

public interface ISigningJobProcessor
{
    Task<SigningJobRunResult> RunAsync(SigningJobRequest request, CancellationToken cancellationToken);
}

public interface IBuildPublisher
{
    Task<BuildPublishResult> PublishAsync(BuildPublishRequest request, CancellationToken cancellationToken);
}

public sealed record ProvisioningMaterial(
    string PrivateKeyPath,
    string CertificatePath,
    string MobileProvisionPath,
    ProvisioningInfo Provisioning);

public sealed record SignArtifactRequest(
    string JobId,
    string SourceIpaPath,
    string OutputIpaPath,
    string EffectiveBundleId,
    bool RemoveExtensions,
    ProvisioningMaterial Provisioning,
    string ZsignExecutablePath,
    TimeSpan Timeout,
    int MaxProcessOutputBytes);

public sealed record SignedBuildArtifact(
    string Path,
    long SizeBytes,
    string Sha256,
    TimeSpan SigningDuration);

public sealed record BuildPublishRequest(
    SigningJob Job,
    ManagedAppConfig App,
    BuildInfo Build,
    string SignedIpaPath,
    DateTimeOffset NowUtc);

public sealed record BuildPublishResult(
    string InstallUrl,
    string LatestManifestUrl,
    string LatestMetadataUrl);

public sealed record SigningExecutionOptions(
    string WorkspaceRoot,
    string ZsignExecutablePath,
    TimeSpan ZsignTimeout,
    int MaxProcessOutputBytes = 256 * 1024);

public sealed record SigningJobRequest(
    SigningJob Job,
    ManagedAppConfig App,
    AppRuntimeState? CurrentState,
    SigningExecutionOptions Options,
    DateTimeOffset NowUtc);

public sealed record SigningJobRunResult(
    SigningJob Job,
    BuildInfo? Build,
    AppRuntimeState RuntimeState,
    bool ShouldRetry,
    DateTimeOffset? NextRetryAt,
    string? ErrorCode,
    IReadOnlyList<SigningJobStatus> Timeline);

public sealed class SigningWorkflowException(string errorCode, string message, Exception? innerException = null)
    : InvalidOperationException(message, innerException)
{
    public string ErrorCode { get; } = errorCode;
}

public sealed class SchedulerOptions
{
    public SchedulerOptions(
        string AppConfigPath = "data/config/apps.json",
        string AppStatePath = "data/state/state.json",
        string WorkspaceRoot = "data/jobs",
        string ZsignExecutablePath = "zsign",
        int ScanIntervalSeconds = 30,
        int MaxProcessOutputBytes = 256 * 1024,
        int ZsignTimeoutSeconds = 1200,
        int CleanupMaxAgeHours = 24,
        string LocalBuildsRoot = "data/builds",
        int LocalBuildsKeepLatestPerApp = 3,
        int R2VersionedRetentionDays = 30,
        int R2VersionedKeepLatestBuildsPerApp = 3,
        int ProfileMinimumFreshHours = 144,
        string AppleDeviceUdid = "",
        string AppleDeviceName = "",
        string? AppleTeamId = null,
        string AppleProfileNamePrefix = "signmanager",
        string ApplePrivateKeyPassword = "",
        string SigningStateRoot = "/signing-state",
        string AppleSessionSecretsPath = "/signing-state/secrets.enc",
        string AppleSessionMasterKeyPath = "/run/secrets/signmanager_master_key",
        string AppleAnisetteBaseUrl = "http://anisette:6969/",
        string AppleAnisetteHeadersPath = "headers",
        string AppleGrandSlamBaseUrl = "http://apple-gateway.local/",
        string AppleDeveloperBaseUrl = "http://apple-gateway.local/",
        int AppleHttpTimeoutSeconds = 30,
        string OtaPublicBaseUrl = "https://ios.example.com",
        string R2Endpoint = "",
        string R2Bucket = "",
        string R2AccessKeyId = "",
        string R2SecretAccessKey = "",
        bool TelegramAlertsEnabled = false,
        string? TelegramBotToken = null,
        string? TelegramChatId = null)
    {
        this.AppConfigPath = AppConfigPath;
        this.AppStatePath = AppStatePath;
        this.WorkspaceRoot = WorkspaceRoot;
        this.ZsignExecutablePath = ZsignExecutablePath;
        this.ScanIntervalSeconds = ScanIntervalSeconds;
        this.MaxProcessOutputBytes = MaxProcessOutputBytes;
        this.ZsignTimeoutSeconds = ZsignTimeoutSeconds;
        this.CleanupMaxAgeHours = CleanupMaxAgeHours;
        this.LocalBuildsRoot = LocalBuildsRoot;
        this.LocalBuildsKeepLatestPerApp = LocalBuildsKeepLatestPerApp;
        this.R2VersionedRetentionDays = R2VersionedRetentionDays;
        this.R2VersionedKeepLatestBuildsPerApp = R2VersionedKeepLatestBuildsPerApp;
        this.ProfileMinimumFreshHours = ProfileMinimumFreshHours;
        this.AppleDeviceUdid = AppleDeviceUdid;
        this.AppleDeviceName = AppleDeviceName;
        this.AppleTeamId = AppleTeamId;
        this.AppleProfileNamePrefix = AppleProfileNamePrefix;
        this.ApplePrivateKeyPassword = ApplePrivateKeyPassword;
        this.SigningStateRoot = SigningStateRoot;
        this.AppleSessionSecretsPath = AppleSessionSecretsPath;
        this.AppleSessionMasterKeyPath = AppleSessionMasterKeyPath;
        this.AppleAnisetteBaseUrl = AppleAnisetteBaseUrl;
        this.AppleAnisetteHeadersPath = AppleAnisetteHeadersPath;
        this.AppleGrandSlamBaseUrl = AppleGrandSlamBaseUrl;
        this.AppleDeveloperBaseUrl = AppleDeveloperBaseUrl;
        this.AppleHttpTimeoutSeconds = AppleHttpTimeoutSeconds;
        this.OtaPublicBaseUrl = OtaPublicBaseUrl;
        this.R2Endpoint = R2Endpoint;
        this.R2Bucket = R2Bucket;
        this.R2AccessKeyId = R2AccessKeyId;
        this.R2SecretAccessKey = R2SecretAccessKey;
        this.TelegramAlertsEnabled = TelegramAlertsEnabled;
        this.TelegramBotToken = TelegramBotToken;
        this.TelegramChatId = TelegramChatId;
    }

    public string AppConfigPath { get; set; }

    public string AppStatePath { get; set; }

    public string WorkspaceRoot { get; set; }

    public string ZsignExecutablePath { get; set; }

    public int ScanIntervalSeconds { get; set; }

    public int MaxProcessOutputBytes { get; set; }

    public int ZsignTimeoutSeconds { get; set; }

    public int CleanupMaxAgeHours { get; set; }

    public string LocalBuildsRoot { get; set; }

    public int LocalBuildsKeepLatestPerApp { get; set; }

    public int R2VersionedRetentionDays { get; set; }

    public int R2VersionedKeepLatestBuildsPerApp { get; set; }

    public int ProfileMinimumFreshHours { get; set; }

    public string AppleDeviceUdid { get; set; }

    public string AppleDeviceName { get; set; }

    public string? AppleTeamId { get; set; }

    public string AppleProfileNamePrefix { get; set; }

    public string ApplePrivateKeyPassword { get; set; }

    public string SigningStateRoot { get; set; }

    public string AppleSessionSecretsPath { get; set; }

    public string AppleSessionMasterKeyPath { get; set; }

    public string AppleAnisetteBaseUrl { get; set; }

    public string AppleAnisetteHeadersPath { get; set; }

    public string AppleGrandSlamBaseUrl { get; set; }

    public string AppleDeveloperBaseUrl { get; set; }

    public int AppleHttpTimeoutSeconds { get; set; }

    public string OtaPublicBaseUrl { get; set; }

    public string R2Endpoint { get; set; }

    public string R2Bucket { get; set; }

    public string R2AccessKeyId { get; set; }

    public string R2SecretAccessKey { get; set; }

    public bool TelegramAlertsEnabled { get; set; }

    public string? TelegramBotToken { get; set; }

    public string? TelegramChatId { get; set; }
}

public sealed record SchedulerScanResult(
    int ScannedApps,
    int TriggeredJobs,
    IReadOnlyList<string> TriggeredAppIds);
