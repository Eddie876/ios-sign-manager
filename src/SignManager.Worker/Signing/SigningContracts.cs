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

public sealed record SchedulerOptions(
    string AppConfigPath = "data/apps.json",
    string AppStatePath = "data/state.json",
    string WorkspaceRoot = "data/jobs",
    string ZsignExecutablePath = "zsign",
    int ScanIntervalSeconds = 30,
    int MaxProcessOutputBytes = 256 * 1024,
    int ZsignTimeoutSeconds = 1200);

public sealed record SchedulerScanResult(
    int ScannedApps,
    int TriggeredJobs,
    IReadOnlyList<string> TriggeredAppIds);
