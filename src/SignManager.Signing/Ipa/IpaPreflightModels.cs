namespace SignManager.Signing.Ipa;

public sealed record IpaPreflightLimits(
    long MaxUploadBytes = 2L * 1024 * 1024 * 1024,
    int MaxEntries = 50_000,
    long MaxTotalExpandedBytes = 4L * 1024 * 1024 * 1024,
    long MaxSingleEntryBytes = 1L * 1024 * 1024 * 1024,
    double MaxCompressionRatio = 200);

public sealed record IpaPreflightRequest(
    string IpaPath,
    IpaPreflightLimits Limits);

public sealed record IpaPreflightResult(
    AppMetadata Metadata,
    long IpaSizeBytes,
    string Sha256);

public sealed record AppMetadata(
    string Name,
    string SourceBundleId,
    string Version,
    string Build,
    string? MinimumOsVersion,
    IReadOnlyList<string> Extensions,
    IReadOnlyList<string> Entitlements,
    bool HasWatchApp,
    bool HasAppClips,
    IReadOnlyList<string> Warnings);

public sealed class IpaPreflightException(string errorCode, string message) : InvalidOperationException(message)
{
    public string ErrorCode { get; } = errorCode;
}
