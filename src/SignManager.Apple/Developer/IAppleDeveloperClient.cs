namespace SignManager.Apple.Developer;

public interface IAppleDeveloperClient
{
    Task<IReadOnlyList<DeveloperTeam>> GetTeamsAsync(CancellationToken cancellationToken);
}

public sealed record DeveloperTeam(
    string TeamId,
    string Name);