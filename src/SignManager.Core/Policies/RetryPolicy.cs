using SignManager.Core.Constants;

namespace SignManager.Core.Policies;

public sealed class RetryPolicy
{
    private static readonly TimeSpan[] Backoff =
    [
        TimeSpan.FromMinutes(15),
        TimeSpan.FromHours(1),
        TimeSpan.FromHours(4),
    ];

    private static readonly HashSet<string> NonRetryableErrorCodes =
    [
        StableErrorCodes.AuthRequired,
        StableErrorCodes.AppIdQuotaExceeded,
        StableErrorCodes.UnsupportedEntitlement,
        StableErrorCodes.InvalidIpa,
        StableErrorCodes.ProfileNotFresh,
    ];

    public bool IsRetryable(string? errorCode)
    {
        if (string.IsNullOrWhiteSpace(errorCode))
        {
            return true;
        }

        return !NonRetryableErrorCodes.Contains(errorCode);
    }

    public TimeSpan? GetDelayForAttempt(int attempt)
    {
        if (attempt <= 0)
        {
            return Backoff[0];
        }

        return attempt > Backoff.Length ? null : Backoff[attempt - 1];
    }
}
