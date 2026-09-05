namespace SignManager.Infrastructure.Time;

public interface IClock
{
    DateTimeOffset UtcNow { get; }
}
