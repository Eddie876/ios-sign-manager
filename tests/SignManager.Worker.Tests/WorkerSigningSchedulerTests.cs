using SignManager.Core.Models;
using SignManager.Core.Services;
using SignManager.Infrastructure.Persistence;
using SignManager.Worker.Signing;

namespace SignManager.Worker.Tests;

public class WorkerSigningSchedulerTests
{
    [Fact]
    public async Task RunScanOnce_ShouldTriggerAutoSign_WhenDue()
    {
        var root = CreateTempRoot();

        try
        {
            var paths = CreatePaths(root);
            var now = new DateTimeOffset(2026, 9, 6, 10, 0, 0, TimeSpan.Zero);
            await SaveSingleAppConfigAsync(paths.ConfigPath, now);

            var processor = new FakeJobProcessor((request, _) => Task.FromResult(CreateSuccessResult(request, now)));
            var scheduler = CreateScheduler(processor);

            var result = await scheduler.RunScanOnceAsync(CreateOptions(paths), now, CancellationToken.None);

            Assert.Equal(1, result.ScannedApps);
            Assert.Equal(1, result.TriggeredJobs);
            Assert.Single(result.TriggeredAppIds);
            Assert.Equal("app1", result.TriggeredAppIds[0]);

            var state = await new AppStateStore().LoadAsync(paths.StatePath, CancellationToken.None);
            Assert.NotNull(state);
            Assert.True(state!.Apps.TryGetValue("app1", out var appState));
            Assert.Equal(RuntimeStatus.Ready, appState!.Status);
            Assert.Equal(now.AddHours(48), appState.NextSignDueAt);
            Assert.Single(processor.Calls);
        }
        finally
        {
            Cleanup(root);
        }
    }

    [Fact]
    public async Task RunScanOnce_ShouldSkipAutoSign_WhenAuthRequired()
    {
        var root = CreateTempRoot();

        try
        {
            var paths = CreatePaths(root);
            var now = new DateTimeOffset(2026, 9, 6, 10, 0, 0, TimeSpan.Zero);
            await SaveSingleAppConfigAsync(paths.ConfigPath, now);
            await SaveStateAsync(paths.StatePath, new AppRuntimeState(RuntimeStatus.AuthRequired, null, now.AddHours(-1), null, null, null, null, "AUTH_REQUIRED"));

            var processor = new FakeJobProcessor((request, _) => Task.FromResult(CreateSuccessResult(request, now)));
            var scheduler = CreateScheduler(processor);

            var result = await scheduler.RunScanOnceAsync(CreateOptions(paths), now, CancellationToken.None);

            Assert.Equal(0, result.TriggeredJobs);
            Assert.Empty(result.TriggeredAppIds);
            Assert.Empty(processor.Calls);
        }
        finally
        {
            Cleanup(root);
        }
    }

    [Fact]
    public async Task ManualSignNow_ShouldTriggerEvenWhenAuthRequired()
    {
        var root = CreateTempRoot();

        try
        {
            var paths = CreatePaths(root);
            var now = new DateTimeOffset(2026, 9, 6, 10, 0, 0, TimeSpan.Zero);
            await SaveSingleAppConfigAsync(paths.ConfigPath, now);
            await SaveStateAsync(paths.StatePath, new AppRuntimeState(RuntimeStatus.AuthRequired, null, null, null, null, null, null, "AUTH_REQUIRED"));

            var processor = new FakeJobProcessor((request, _) => Task.FromResult(CreateSuccessResult(request, now)));
            var scheduler = CreateScheduler(processor);
            Assert.True(scheduler.RequestManualSign("app1"));

            var result = await scheduler.RunScanOnceAsync(CreateOptions(paths), now, CancellationToken.None);

            Assert.Equal(1, result.TriggeredJobs);
            Assert.Single(processor.Calls);
            Assert.Equal(SigningJobType.Manual, processor.Calls[0].Job.Type);
        }
        finally
        {
            Cleanup(root);
        }
    }

    [Fact]
    public async Task RunScanOnce_ShouldPersistRetryDue_WhenProcessorRequestsRetry()
    {
        var root = CreateTempRoot();

        try
        {
            var paths = CreatePaths(root);
            var now = new DateTimeOffset(2026, 9, 6, 10, 0, 0, TimeSpan.Zero);
            await SaveSingleAppConfigAsync(paths.ConfigPath, now);

            var retryAt = now.AddMinutes(15);
            var processor = new FakeJobProcessor((request, _) => Task.FromResult(CreateRetryResult(request, retryAt)));
            var scheduler = CreateScheduler(processor);

            var result = await scheduler.RunScanOnceAsync(CreateOptions(paths), now, CancellationToken.None);

            Assert.Equal(1, result.TriggeredJobs);
            var state = await new AppStateStore().LoadAsync(paths.StatePath, CancellationToken.None);
            Assert.NotNull(state);
            Assert.Equal(RuntimeStatus.Failed, state!.Apps["app1"].Status);
            Assert.Equal(retryAt, state.Apps["app1"].NextSignDueAt);
        }
        finally
        {
            Cleanup(root);
        }
    }

    [Fact]
    public async Task RunScanOnce_ShouldSkip_WhenAlreadyPending()
    {
        var root = CreateTempRoot();

        try
        {
            var paths = CreatePaths(root);
            var now = new DateTimeOffset(2026, 9, 6, 10, 0, 0, TimeSpan.Zero);
            await SaveSingleAppConfigAsync(paths.ConfigPath, now);
            await SaveStateAsync(paths.StatePath, new AppRuntimeState(RuntimeStatus.Pending, null, now.AddHours(-1), null, null, null, null, null));

            var processor = new FakeJobProcessor((request, _) => Task.FromResult(CreateSuccessResult(request, now)));
            var scheduler = CreateScheduler(processor);

            var result = await scheduler.RunScanOnceAsync(CreateOptions(paths), now, CancellationToken.None);

            Assert.Equal(0, result.TriggeredJobs);
            Assert.Empty(processor.Calls);
        }
        finally
        {
            Cleanup(root);
        }
    }

    private static WorkerSigningScheduler CreateScheduler(ISigningJobProcessor processor)
        => new(
            new AppConfigStore(),
            new AppStateStore(),
            new RefreshPlanner(),
            processor,
            new ManualSignTriggerStore());

    private static SchedulerOptions CreateOptions((string ConfigPath, string StatePath, string WorkspaceRoot) paths)
        => new(
            AppConfigPath: paths.ConfigPath,
            AppStatePath: paths.StatePath,
            WorkspaceRoot: paths.WorkspaceRoot,
            ZsignExecutablePath: "zsign",
            ScanIntervalSeconds: 10,
            ZsignTimeoutSeconds: 1200,
            MaxProcessOutputBytes: 262144);

    private static SigningJobRunResult CreateSuccessResult(SigningJobRequest request, DateTimeOffset now)
    {
        var build = new BuildInfo(
            BuildId: $"{request.Job.JobId}-build",
            AppId: request.Job.AppId,
            Sha256: "abcd",
            SizeBytes: 1234,
            CreatedAt: now,
            Provisioning: new ProvisioningInfo(
                Uuid: "profile",
                CreationDate: now,
                ExpirationDate: now.AddDays(7),
                BundleId: request.App.Identity.EffectiveBundleId,
                TeamId: "TEAM",
                DeviceUdids: ["udid1"]));

        var state = new AppRuntimeState(
            RuntimeStatus.Ready,
            LastSuccessfulSignAt: now,
            NextSignDueAt: now.AddHours(48),
            LatestBuildId: build.BuildId,
            ProfileCreationDate: now,
            ProfileExpirationDate: now.AddDays(7),
            LastPromptAt: null,
            LastErrorCode: null);

        return new SigningJobRunResult(
            request.Job with { Status = SigningJobStatus.Ready },
            build,
            state,
            ShouldRetry: false,
            NextRetryAt: null,
            ErrorCode: null,
            Timeline: [SigningJobStatus.Preflight, SigningJobStatus.Provisioning, SigningJobStatus.Signing, SigningJobStatus.Validation, SigningJobStatus.Ready]);
    }

    private static SigningJobRunResult CreateRetryResult(SigningJobRequest request, DateTimeOffset retryAt)
    {
        var state = new AppRuntimeState(
            RuntimeStatus.Failed,
            LastSuccessfulSignAt: request.CurrentState?.LastSuccessfulSignAt,
            NextSignDueAt: retryAt,
            LatestBuildId: request.CurrentState?.LatestBuildId,
            ProfileCreationDate: request.CurrentState?.ProfileCreationDate,
            ProfileExpirationDate: request.CurrentState?.ProfileExpirationDate,
            LastPromptAt: request.CurrentState?.LastPromptAt,
            LastErrorCode: "ZSIGN_FAILED");

        return new SigningJobRunResult(
            request.Job with { Status = SigningJobStatus.Failed, Attempt = request.Job.Attempt + 1 },
            Build: null,
            RuntimeState: state,
            ShouldRetry: true,
            NextRetryAt: retryAt,
            ErrorCode: "ZSIGN_FAILED",
            Timeline: [SigningJobStatus.Preflight, SigningJobStatus.Failed]);
    }

    private static async Task SaveSingleAppConfigAsync(string configPath, DateTimeOffset now)
    {
        var config = new AppConfig(
            Version: AppConfigStore.CurrentVersion,
            Apps:
            [
                new ManagedAppConfig(
                    Id: "app1",
                    Name: "App One",
                    Enabled: true,
                    Source: new SourceArtifact(Path.Combine(Path.GetTempPath(), "source.ipa"), "sha", now.AddDays(-1)),
                    Identity: new BundleIdentity("com.source", "com.target"),
                    Signing: new AppSigningConfig(true),
                    Schedule: new AppScheduleConfig(true, 48),
                    Publish: new AppPublishConfig("app-one"))
            ]);

        var dir = Path.GetDirectoryName(configPath);
        if (!string.IsNullOrWhiteSpace(dir))
        {
            Directory.CreateDirectory(dir);
        }

        await new AppConfigStore().SaveAsync(configPath, config, CancellationToken.None);
    }

    private static async Task SaveStateAsync(string statePath, AppRuntimeState appState)
    {
        var dir = Path.GetDirectoryName(statePath);
        if (!string.IsNullOrWhiteSpace(dir))
        {
            Directory.CreateDirectory(dir);
        }

        var state = new AppState(
            Version: AppStateStore.CurrentVersion,
            Apps: new Dictionary<string, AppRuntimeState>
            {
                ["app1"] = appState,
            });

        await new AppStateStore().SaveAsync(statePath, state, CancellationToken.None);
    }

    private static (string ConfigPath, string StatePath, string WorkspaceRoot) CreatePaths(string root)
        => (
            ConfigPath: Path.Combine(root, "config", "apps.json"),
            StatePath: Path.Combine(root, "state", "state.json"),
            WorkspaceRoot: Path.Combine(root, "workspace"));

    private static string CreateTempRoot()
    {
        var root = Path.Combine(Path.GetTempPath(), "sign-manager-worker-scheduler-tests", Guid.NewGuid().ToString("N"));
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

    private sealed class FakeJobProcessor(Func<SigningJobRequest, CancellationToken, Task<SigningJobRunResult>> handler)
        : ISigningJobProcessor
    {
        public List<SigningJobRequest> Calls { get; } = [];

        public async Task<SigningJobRunResult> RunAsync(SigningJobRequest request, CancellationToken cancellationToken)
        {
            Calls.Add(request);
            return await handler(request, cancellationToken);
        }
    }
}
