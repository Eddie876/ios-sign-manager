using System.Security.Cryptography;

namespace SignManager.Infrastructure.R2;

public sealed class R2ObjectKeyPlanner
{
    public string CreateRandomNamespace()
    {
        var bytes = RandomNumberGenerator.GetBytes(16);
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    public R2ObjectKeyPlan BuildPlan(string randomNamespace, string appId, string buildId)
    {
        ValidateSegment(randomNamespace, nameof(randomNamespace));
        ValidateSegment(appId, nameof(appId));
        ValidateSegment(buildId, nameof(buildId));

        var basePath = $"apps/{randomNamespace}/{appId}";
        return new R2ObjectKeyPlan(
            VersionedIpaKey: $"{basePath}/builds/{buildId}/app.ipa",
            VersionedManifestKey: $"{basePath}/builds/{buildId}/manifest.plist",
            VersionedBuildJsonKey: $"{basePath}/builds/{buildId}/build.json",
            LatestManifestKey: $"{basePath}/latest/manifest.plist",
            LatestJsonKey: $"{basePath}/latest/latest.json");
    }

    private static void ValidateSegment(string value, string name)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Value is required.", name);
        }

        if (value.Contains('/'))
        {
            throw new ArgumentException("Value must be a single path segment.", name);
        }
    }
}

public sealed record R2ObjectKeyPlan(
    string VersionedIpaKey,
    string VersionedManifestKey,
    string VersionedBuildJsonKey,
    string LatestManifestKey,
    string LatestJsonKey)
{
    public IReadOnlyList<string> PublishOrderKeys =>
    [
        VersionedIpaKey,
        VersionedManifestKey,
        VersionedBuildJsonKey,
        LatestManifestKey,
        LatestJsonKey,
    ];
}
