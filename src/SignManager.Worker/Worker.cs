using Microsoft.Extensions.Options;
using SignManager.Infrastructure.Notifications;
using SignManager.Worker.Operations;
using SignManager.Worker.Signing;

namespace SignManager.Worker;

public sealed class Worker(
    ILogger<Worker> logger,
    WorkerSigningScheduler scheduler,
    JobWorkspaceCleanupService cleanupService,
    LocalBuildRetentionCleanupService localBuildCleanupService,
    R2VersionedBuildCleanupService r2BuildCleanupService,
    IExceptionNotifier notifier,
    IOptions<SchedulerOptions> schedulerOptions) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            var options = schedulerOptions.Value;

            try
            {
                var result = await scheduler.RunScanOnceAsync(options, DateTimeOffset.UtcNow, stoppingToken);

                if (logger.IsEnabled(LogLevel.Information))
                {
                    logger.LogInformation(
                        "Scheduler scan completed. scannedApps={ScannedApps}, triggeredJobs={TriggeredJobs}",
                        result.ScannedApps,
                        result.TriggeredJobs);
                }

                var cleanup = cleanupService.Cleanup(
                    options.WorkspaceRoot,
                    TimeSpan.FromHours(Math.Max(1, options.CleanupMaxAgeHours)),
                    DateTimeOffset.UtcNow);

                logger.LogInformation(
                    "Workspace cleanup completed. scanned={ScannedDirectories}, deleted={DeletedDirectories}, failed={FailedDirectories}",
                    cleanup.ScannedDirectories,
                    cleanup.DeletedDirectories,
                    cleanup.FailedDirectories);

                var localBuildCleanup = localBuildCleanupService.Cleanup(
                    options.LocalBuildsRoot,
                    options.LocalBuildsKeepLatestPerApp);

                logger.LogInformation(
                    "Local build retention cleanup completed. scanned={ScannedDirectories}, deleted={DeletedDirectories}, failed={FailedDirectories}",
                    localBuildCleanup.ScannedDirectories,
                    localBuildCleanup.DeletedDirectories,
                    localBuildCleanup.FailedDirectories);

                var r2Cleanup = await r2BuildCleanupService.CleanupAsync(
                    options.R2VersionedRetentionDays,
                    options.R2VersionedKeepLatestBuildsPerApp,
                    DateTimeOffset.UtcNow,
                    stoppingToken);

                logger.LogInformation(
                    "R2 versioned build cleanup completed. scanned={ScannedObjects}, deleted={DeletedObjects}, failed={FailedObjects}",
                    r2Cleanup.ScannedDirectories,
                    r2Cleanup.DeletedDirectories,
                    r2Cleanup.FailedDirectories);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Scheduler scan failed.");

                await notifier.NotifyAsync(
                    OperationalAlertFactory.CreateSchedulerScanFailureAlert(ex),
                    stoppingToken);
            }

            var delaySeconds = Math.Max(1, options.ScanIntervalSeconds);
            await Task.Delay(TimeSpan.FromSeconds(delaySeconds), stoppingToken);
        }
    }
}
