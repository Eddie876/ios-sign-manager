namespace SignManager.Infrastructure.Notifications;

public interface IExceptionNotifier
{
    Task NotifyAsync(ExceptionAlert alert, CancellationToken cancellationToken);
}

public sealed record ExceptionAlert(
    string Title,
    string ErrorCode,
    string Message,
    IReadOnlyDictionary<string, string>? Metadata = null);

public sealed class NoOpExceptionNotifier : IExceptionNotifier
{
    public Task NotifyAsync(ExceptionAlert alert, CancellationToken cancellationToken)
        => Task.CompletedTask;
}
