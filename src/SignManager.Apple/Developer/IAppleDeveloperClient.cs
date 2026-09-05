namespace SignManager.Apple.Developer;

public interface IAppleDeveloperClient
{
    Task<bool> ViewDeveloperAsync(CancellationToken cancellationToken);

    Task<IReadOnlyList<DeveloperTeam>> GetTeamsAsync(CancellationToken cancellationToken);
}

public sealed record DeveloperTeam(
    string TeamId,
    string Name);