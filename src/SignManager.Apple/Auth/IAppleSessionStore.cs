namespace SignManager.Apple.Auth;

public interface IAppleSessionStore
{
    Task SaveAsync(AppleSession session, CancellationToken cancellationToken);

    Task<AppleSession?> LoadAsync(CancellationToken cancellationToken);

    Task ClearAsync(CancellationToken cancellationToken);
}