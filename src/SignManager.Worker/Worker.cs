using Microsoft.Extensions.Options;
using SignManager.Infrastructure.Notifications;
using SignManager.Worker.Operations;
using SignManager.Worker.Signing;

namespace SignManager.Worker;

public sealed class Worker(
    ILogger<Worker> logger,
    WorkerSigningScheduler scheduler,
    JobWorkspaceCleanupService cleanupService,
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
                    "Workspace cleanup completed. scanned={ScannedDirectories}, deleted={DeletedDirectories}",
                    cleanup.ScannedDirectories,
                    cleanup.DeletedDirectories);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Scheduler scan failed.");

                await notifier.NotifyAsync(
                    new ExceptionAlert(
                        Title: "Scheduler scan failed",
                        ErrorCode: "TELEGRAM_FAILED",
                        Message: ex.Message,
                        Metadata: new Dictionary<string, string>
                        {
                            ["exception"] = ex.GetType().Name,
                        }),
                    stoppingToken);
            }

            var delaySeconds = Math.Max(1, options.ScanIntervalSeconds);
            await Task.Delay(TimeSpan.FromSeconds(delaySeconds), stoppingToken);
        }
    }
}
