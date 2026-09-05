namespace SignManager.Apple.Anisette;

public interface IAnisetteProvider
{
    Task<AnisetteHeaders> GetHeadersAsync(CancellationToken cancellationToken);
}
