using System.Security.Cryptography;
using SignManager.Core.Constants;
using SignManager.Core.Models;
using SignManager.Core.Policies;
using SignManager.Core.Services;
using SignManager.Signing.Ipa;

namespace SignManager.Worker.Signing;

public sealed class SigningJobProcessor(
    SourceIpaManager sourceIpaManager,
    IProvisioningMaterialProvider provisioningProvider,
    ISigningArtifactSigner artifactSigner,
    IBuildPublisher buildPublisher,
    IGlobalSigningGate signingGate,
    RetryPolicy retryPolicy,
    RefreshPlanner refreshPlanner) : ISigningJobProcessor
{
    public Task<SigningJobRunResult> RunAsync(SigningJobRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        return signingGate.RunExclusiveAsync(
            gateToken => RunCoreAsync(request, gateToken),
            cancellationToken);
    }

    private async Task<SigningJobRunResult> RunCoreAsync(SigningJobRequest request, CancellationToken cancellationToken)
    {
        var now = request.NowUtc;
        var timeline = new List<SigningJobStatus>();

        try
        {
            ValidateInputs(request);
            await ValidateSourceSha256Async(request.App.Source.Path, request.App.Source.Sha256, request.Job.SourceSha256, cancellationToken);

            var workspaceDirectory = Path.Combine(request.Options.WorkspaceRoot, request.Job.JobId);
            Directory.CreateDirectory(workspaceDirectory);

            var jobSourceIpaPath = Path.Combine(workspaceDirectory, "source.ipa");
            var signedIpaPath = Path.Combine(workspaceDirectory, "signed.ipa");

            timeline.Add(SigningJobStatus.Preflight);
            await sourceIpaManager.CopyImmutableSourceToJobAsync(request.App.Source.Path, jobSourceIpaPath, cancellationToken);

            timeline.Add(SigningJobStatus.Provisioning);
            var provisioning = await provisioningProvider.PrepareAsync(request.Job, request.App, cancellationToken);

            timeline.Add(SigningJobStatus.Signing);
            var artifact = await artifactSigner.SignAsync(
                new SignArtifactRequest(
                    JobId: request.Job.JobId,
                    SourceIpaPath: jobSourceIpaPath,
                    OutputIpaPath: signedIpaPath,
                    EffectiveBundleId: request.App.Identity.EffectiveBundleId,
                    RemoveExtensions: request.App.Signing.RemoveExtensions,
                    Provisioning: provisioning,
                    ZsignExecutablePath: request.Options.ZsignExecutablePath,
                    Timeout: request.Options.ZsignTimeout,
                    MaxProcessOutputBytes: request.Options.MaxProcessOutputBytes),
                cancellationToken);

            timeline.Add(SigningJobStatus.Validation);
            ValidateSignedBuild(artifact);

            timeline.Add(SigningJobStatus.Ready);
            var completedJob = request.Job with { Status = SigningJobStatus.Ready };

            var build = new BuildInfo(
                BuildId: $"{request.Job.JobId}-build",
                AppId: request.Job.AppId,
                Sha256: artifact.Sha256,
                SizeBytes: artifact.SizeBytes,
                CreatedAt: now,
                Provisioning: provisioning.Provisioning);

            timeline.Add(SigningJobStatus.Publishing);
            await buildPublisher.PublishAsync(
                new BuildPublishRequest(
                    Job: completedJob,
                    App: request.App,
                    Build: build,
                    SignedIpaPath: artifact.Path,
                    NowUtc: now),
                cancellationToken);

            var runtimeState = new AppRuntimeState(
                Status: RuntimeStatus.Ready,
                LastSuccessfulSignAt: now,
                NextSignDueAt: refreshPlanner.ComputeNextSignDue(now, request.App.Schedule.IntervalHours),
                LatestBuildId: build.BuildId,
                ProfileCreationDate: provisioning.Provisioning.CreationDate,
                ProfileExpirationDate: provisioning.Provisioning.ExpirationDate,
                LastPromptAt: request.CurrentState?.LastPromptAt,
                LastErrorCode: null);

            return new SigningJobRunResult(
                Job: completedJob,
                Build: build,
                RuntimeState: runtimeState,
                ShouldRetry: false,
                NextRetryAt: null,
                ErrorCode: null,
                Timeline: timeline);
        }
        catch (Exception ex)
        {
            var errorCode = MapErrorCode(ex);
            var nextAttempt = request.Job.Attempt + 1;
            var retryable = retryPolicy.IsRetryable(errorCode);
            var delay = retryable ? retryPolicy.GetDelayForAttempt(nextAttempt) : null;
            var shouldRetry = retryable && delay is not null;

            timeline.Add(SigningJobStatus.Failed);

            var failedJob = request.Job with
            {
                Status = SigningJobStatus.Failed,
                Attempt = nextAttempt,
            };

            var runtimeStatus = errorCode == StableErrorCodes.AuthRequired
                ? RuntimeStatus.AuthRequired
                : RuntimeStatus.Failed;

            var runtimeState = request.CurrentState is null
                ? new AppRuntimeState(runtimeStatus, null, null, null, null, null, null, errorCode)
                : request.CurrentState with { Status = runtimeStatus, LastErrorCode = errorCode };

            return new SigningJobRunResult(
                Job: failedJob,
                Build: null,
                RuntimeState: runtimeState,
                ShouldRetry: shouldRetry,
                NextRetryAt: shouldRetry ? now + delay : null,
                ErrorCode: errorCode,
                Timeline: timeline);
        }
    }

    private static void ValidateInputs(SigningJobRequest request)
    {
        if (!string.Equals(request.Job.AppId, request.App.Id, StringComparison.Ordinal))
        {
            throw new SigningWorkflowException(StableErrorCodes.InvalidIpa, "Signing job app id does not match app config.");
        }

        if (request.App.Schedule.IntervalHours <= 0)
        {
            throw new SigningWorkflowException(StableErrorCodes.SignedIpaValidationFailed, "Schedule interval must be positive.");
        }
    }

    private static async Task ValidateSourceSha256Async(
        string sourcePath,
        string expectedConfigSha256,
        string expectedJobSha256,
        CancellationToken cancellationToken)
    {
        if (!File.Exists(sourcePath))
        {
            throw new SigningWorkflowException(StableErrorCodes.InvalidIpa, "Source IPA does not exist.");
        }

        var actualSha256 = await ComputeSha256Async(sourcePath, cancellationToken);

        if (!string.Equals(actualSha256, expectedConfigSha256, StringComparison.OrdinalIgnoreCase)
            || !string.Equals(actualSha256, expectedJobSha256, StringComparison.OrdinalIgnoreCase))
        {
            throw new SigningWorkflowException(StableErrorCodes.InvalidIpa, "Source IPA SHA256 mismatch.");
        }
    }

    private static void ValidateSignedBuild(SignedBuildArtifact artifact)
    {
        if (!File.Exists(artifact.Path) || artifact.SizeBytes <= 0 || string.IsNullOrWhiteSpace(artifact.Sha256))
        {
            throw new SigningWorkflowException(StableErrorCodes.SignedIpaValidationFailed, "Signed build validation failed.");
        }
    }

    private static string MapErrorCode(Exception ex)
        => ex switch
        {
            SigningWorkflowException workflow => workflow.ErrorCode,
            IpaPreflightException preflight => preflight.ErrorCode,
            FileNotFoundException => StableErrorCodes.InvalidIpa,
            _ when IsSignedValidationFailure(ex) => StableErrorCodes.SignedIpaValidationFailed,
            _ when IsAuthFailure(ex) => StableErrorCodes.AuthRequired,
            _ => StableErrorCodes.ZsignFailed,
        };

    private static bool IsSignedValidationFailure(Exception ex)
        => ex is InvalidOperationException invalidOperation
            && invalidOperation.Message.Contains("validation failed", StringComparison.OrdinalIgnoreCase);

    private static bool IsAuthFailure(Exception ex)
        => ex is InvalidOperationException invalidOperation
            && invalidOperation.Message.Contains("auth", StringComparison.OrdinalIgnoreCase);

    private static async Task<string> ComputeSha256Async(string path, CancellationToken cancellationToken)
    {
        await using var stream = File.OpenRead(path);
        using var sha256 = SHA256.Create();
        var hash = await sha256.ComputeHashAsync(stream, cancellationToken);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}
