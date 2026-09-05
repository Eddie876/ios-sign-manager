namespace SignManager.Core.Models;

public sealed record SigningJob(
    string JobId,
    string AppId,
    string SourceSha256,
    SigningJobType Type,
    DateTimeOffset CreatedAt,
    SigningJobStatus Status,
    int Attempt);

public enum SigningJobType
{
    Auto,
    Manual,
}

public enum SigningJobStatus
{
    Queued,
    Preflight,
    Provisioning,
    Signing,
    Validation,
    Publishing,
    Ready,
    Failed,
}
