using SignManager.Infrastructure.R2;

namespace SignManager.Worker.Operations;

public sealed class R2VersionedBuildCleanupService(IR2ObjectStore objectStore)
{
    public async Task<CleanupResult> CleanupAsync(
        int retentionDays,
        int keepLatestBuildsPerApp,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var keepLatest = Math.Max(1, keepLatestBuildsPerApp);
        var retention = TimeSpan.FromDays(Math.Max(0, retentionDays));
        var threshold = now - retention;

        var allObjects = await objectStore.ListObjectsAsync("apps/", cancellationToken);
        var versioned = allObjects
            .Select(TryParseBuildObject)
            .Where(x => x is not null)
            .Cast<BuildObject>()
            .ToArray();

        if (versioned.Length == 0)
        {
            return new CleanupResult(0, 0, 0);
        }

        var keepSet = versioned
            .GroupBy(x => x.AppPath, StringComparer.Ordinal)
            .SelectMany(group => group
                .GroupBy(x => x.BuildId, StringComparer.Ordinal)
                .Select(build => new
                {
                    BuildId = build.Key,
                    Latest = build.Max(x => x.LastModifiedUtc),
                })
                .OrderByDescending(x => x.Latest)
                .Take(keepLatest)
                .Select(x => $"{group.Key}|{x.BuildId}"))
            .ToHashSet(StringComparer.Ordinal);

        var scanned = versioned.Length;
        var deleted = 0;
        var failed = 0;

        foreach (var item in versioned)
        {
            var keepKey = $"{item.AppPath}|{item.BuildId}";
            if (keepSet.Contains(keepKey))
            {
                continue;
            }

            if (item.LastModifiedUtc >= threshold)
            {
                continue;
            }

            try
            {
                await objectStore.DeleteObjectIfExistsAsync(item.Key, cancellationToken);
                deleted++;
            }
            catch
            {
                failed++;
            }
        }

        return new CleanupResult(scanned, deleted, failed);
    }

    private static BuildObject? TryParseBuildObject(R2ObjectInfo info)
    {
        if (string.IsNullOrWhiteSpace(info.Key))
        {
            return null;
        }

        var segments = info.Key.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length < 6)
        {
            return null;
        }

        if (!string.Equals(segments[0], "apps", StringComparison.Ordinal)
            || !string.Equals(segments[3], "builds", StringComparison.Ordinal))
        {
            return null;
        }

        var appPath = string.Join('/', segments[0], segments[1], segments[2]);
        var buildId = segments[4];
        return new BuildObject(info.Key, appPath, buildId, info.LastModifiedUtc);
    }

    private sealed record BuildObject(string Key, string AppPath, string BuildId, DateTimeOffset LastModifiedUtc);
}