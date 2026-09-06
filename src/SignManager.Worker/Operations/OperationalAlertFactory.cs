using SignManager.Core.Constants;
using SignManager.Infrastructure.Notifications;

namespace SignManager.Worker.Operations;

public static class OperationalAlertFactory
{
    public static ExceptionAlert CreateSchedulerScanFailureAlert(Exception ex)
    {
        ArgumentNullException.ThrowIfNull(ex);

        return new ExceptionAlert(
            Title: "Scheduler scan failed",
            ErrorCode: StableErrorCodes.TelegramFailed,
            Message: "Scheduler scan failed. Check structured logs for diagnostic details.",
            Metadata: new Dictionary<string, string>
            {
                ["exceptionType"] = ex.GetType().Name,
            });
    }
}