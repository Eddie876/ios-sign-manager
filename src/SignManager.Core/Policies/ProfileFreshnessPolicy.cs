using SignManager.Core.Models;

namespace SignManager.Core.Policies;

public sealed class ProfileFreshnessPolicy
{
    public ProfileFreshnessPolicy(int minimumFreshHours = 144)
    {
        if (minimumFreshHours <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(minimumFreshHours));
        }

        MinimumFreshHours = minimumFreshHours;
    }

    public int MinimumFreshHours { get; }

    public bool IsFresh(
        ProvisioningInfo current,
        ProvisioningInfo? previous,
        DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(current);

        if (previous is not null)
        {
            if (current.CreationDate <= previous.CreationDate)
            {
                return false;
            }

            if (current.ExpirationDate <= previous.ExpirationDate)
            {
                return false;
            }
        }

        return current.ExpirationDate >= now.AddHours(MinimumFreshHours);
    }
}
