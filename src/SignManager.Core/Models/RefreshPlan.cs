namespace SignManager.Core.Models;

public sealed record RefreshPlan(
    bool Due,
    string? AppId,
    string? BuildId,
    DateTimeOffset? ProfileExpiresAt,
    string? InstallUrl);
