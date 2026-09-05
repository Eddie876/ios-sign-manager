using SignManager.Core.Models;
using SignManager.Core.Services;
using SignManager.Infrastructure.Persistence;

namespace SignManager.Worker.Signing;

public sealed class WorkerSigningScheduler(
    AppConfigStore appConfigStore,
    AppStateStore appStateStore,
    RefreshPlanner refreshPlanner,
    ISigningJobProcessor jobProcessor,
    ManualSignTriggerStore manualSignTriggerStore)
{
    public bool RequestManualSign(string appId)
        => manualSignTriggerStore.Request(appId);

    public async Task<SchedulerScanResult> RunScanOnceAsync(
        SchedulerOptions options,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(options);

        var config = await appConfigStore.LoadAsync(options.AppConfigPath, cancellationToken);
        if (config is null)
        {
            return new SchedulerScanResult(0, 0, []);
        }

        var state = await appStateStore.LoadAsync(options.AppStatePath, cancellationToken)
            ?? new AppState(AppStateStore.CurrentVersion, new Dictionary<string, AppRuntimeState>(StringComparer.Ordinal));

        var states = state.Apps.ToDictionary(x => x.Key, x => x.Value, StringComparer.Ordinal);
        var triggered = new List<string>();

        foreach (var app in config.Apps)
        {
            cancellationToken.ThrowIfCancellationRequested();

            states.TryGetValue(app.Id, out var runtimeState);
            var isManual = manualSignTriggerStore.HasPending(app.Id);

            if (!ShouldTrigger(app, runtimeState, now, isManual, refreshPlanner))
            {
                continue;
            }

            var attempt = GetCurrentAttempt(runtimeState, now);
            var pendingState = runtimeState is null
                ? new AppRuntimeState(RuntimeStatus.Pending, null, null, null, null, null, null, null)
                : runtimeState with { Status = RuntimeStatus.Pending };

            states[app.Id] = pendingState;
            await SaveStateAsync(options.AppStatePath, states, cancellationToken);

            var request = new SigningJobRequest(
                Job: new SigningJob(
                    JobId: BuildJobId(app.Id, now),
                    AppId: app.Id,
                    SourceSha256: app.Source.Sha256,
                    Type: isManual ? SigningJobType.Manual : SigningJobType.Auto,
                    CreatedAt: now,
                    Status: SigningJobStatus.Queued,
                    Attempt: attempt),
                App: app,
                CurrentState: runtimeState,
                Options: new SigningExecutionOptions(
                    WorkspaceRoot: options.WorkspaceRoot,
                    ZsignExecutablePath: options.ZsignExecutablePath,
                    ZsignTimeout: TimeSpan.FromSeconds(options.ZsignTimeoutSeconds),
                    MaxProcessOutputBytes: options.MaxProcessOutputBytes),
                NowUtc: now);

            var result = await jobProcessor.RunAsync(request, cancellationToken);
            states[app.Id] = ApplyPostRunState(result, now);
            await SaveStateAsync(options.AppStatePath, states, cancellationToken);

            if (isManual)
            {
                manualSignTriggerStore.Consume(app.Id);
            }

            triggered.Add(app.Id);
        }

        return new SchedulerScanResult(config.Apps.Count, triggered.Count, triggered);
    }

    private static bool ShouldTrigger(
        ManagedAppConfig app,
        AppRuntimeState? runtimeState,
        DateTimeOffset now,
        bool isManual,
        RefreshPlanner refreshPlanner)
    {
        if (!app.Enabled)
        {
            return false;
        }

        if (runtimeState?.Status == RuntimeStatus.Pending)
        {
            return false;
        }

        if (isManual)
        {
            return true;
        }

        if (runtimeState?.Status == RuntimeStatus.AuthRequired)
        {
            return false;
        }

        return refreshPlanner.IsSignDue(app, runtimeState, now);
    }

    private static int GetCurrentAttempt(AppRuntimeState? runtimeState, DateTimeOffset now)
    {
        if (runtimeState?.Status != RuntimeStatus.Failed || runtimeState.NextSignDueAt is null)
        {
            return 0;
        }

        return runtimeState.NextSignDueAt <= now
            ? 1
            : 0;
    }

    private static AppRuntimeState ApplyPostRunState(SigningJobRunResult result, DateTimeOffset now)
    {
        if (result.ShouldRetry && result.NextRetryAt is not null)
        {
            return result.RuntimeState with { NextSignDueAt = result.NextRetryAt };
        }

        if (result.Job.Status == SigningJobStatus.Failed)
        {
            return result.RuntimeState with { NextSignDueAt = null };
        }

        return result.RuntimeState;
    }

    private static string BuildJobId(string appId, DateTimeOffset now)
        => $"{now:yyyyMMddHHmmss}-{appId}-{Guid.NewGuid():N}";

    private async Task SaveStateAsync(
        string statePath,
        IReadOnlyDictionary<string, AppRuntimeState> states,
        CancellationToken cancellationToken)
    {
        var directory = Path.GetDirectoryName(statePath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        await appStateStore.SaveAsync(
            statePath,
            new AppState(AppStateStore.CurrentVersion, states),
            cancellationToken);
    }
}
