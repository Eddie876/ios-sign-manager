using SignManager.Core.Models;

namespace SignManager.Core.Services;

public sealed class RefreshPlanner
{
    public bool IsSignDue(ManagedAppConfig app, AppRuntimeState? runtimeState, DateTimeOffset now)
    {
        if (!app.Enabled || !app.Schedule.AutoSign)
        {
            return false;
        }

        if (runtimeState?.NextSignDueAt is null)
        {
            return true;
        }

        return now >= runtimeState.NextSignDueAt.Value;
    }

    public DateTimeOffset ComputeNextSignDue(DateTimeOffset lastSuccessfulSignAt, int intervalHours)
    {
        if (intervalHours <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(intervalHours));
        }

        return lastSuccessfulSignAt.AddHours(intervalHours);
    }
}
