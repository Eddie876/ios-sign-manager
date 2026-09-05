namespace SignManager.Core.Models;

public sealed record AppState(
    int Version,
    IReadOnlyDictionary<string, AppRuntimeState> Apps);

public sealed record AppRuntimeState(
    RuntimeStatus Status,
    DateTimeOffset? LastSuccessfulSignAt,
    DateTimeOffset? NextSignDueAt,
    string? LatestBuildId,
    DateTimeOffset? ProfileCreationDate,
    DateTimeOffset? ProfileExpirationDate,
    DateTimeOffset? LastPromptAt,
    string? LastErrorCode);

public enum RuntimeStatus
{
    Ready,
    AuthRequired,
    Pending,
    Failed,
}
