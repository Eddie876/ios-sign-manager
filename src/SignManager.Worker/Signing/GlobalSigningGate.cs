namespace SignManager.Worker.Signing;

public sealed class GlobalSigningGate : IGlobalSigningGate
{
    private static readonly SemaphoreSlim Semaphore = new(initialCount: 1, maxCount: 1);

    public async Task<T> RunExclusiveAsync<T>(Func<CancellationToken, Task<T>> operation, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(operation);

        await Semaphore.WaitAsync(cancellationToken);
        try
        {
            return await operation(cancellationToken);
        }
        finally
        {
            Semaphore.Release();
        }
    }
}
