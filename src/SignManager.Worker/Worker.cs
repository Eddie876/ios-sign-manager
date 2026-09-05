using Microsoft.Extensions.Options;
using SignManager.Worker.Signing;

namespace SignManager.Worker;

public sealed class Worker(
    ILogger<Worker> logger,
    WorkerSigningScheduler scheduler,
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
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Scheduler scan failed.");
            }

            var delaySeconds = Math.Max(1, options.ScanIntervalSeconds);
            await Task.Delay(TimeSpan.FromSeconds(delaySeconds), stoppingToken);
        }
    }
}
