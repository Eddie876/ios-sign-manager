using SignManager.Core.Constants;
using SignManager.Worker.Operations;

namespace SignManager.Worker.Tests;

public class OperationalAlertFactoryTests
{
    [Fact]
    public void CreateSchedulerScanFailureAlert_ShouldNotLeakRawExceptionMessage()
    {
        var ex = new InvalidOperationException("secret=apple-token-12345");

        var alert = OperationalAlertFactory.CreateSchedulerScanFailureAlert(ex);

        Assert.Equal("Scheduler scan failed", alert.Title);
        Assert.Equal(StableErrorCodes.TelegramFailed, alert.ErrorCode);
        Assert.DoesNotContain("apple-token", alert.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Check structured logs", alert.Message, StringComparison.Ordinal);
        Assert.NotNull(alert.Metadata);
        Assert.Equal("InvalidOperationException", alert.Metadata!["exceptionType"]);
    }
}