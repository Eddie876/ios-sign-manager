namespace SignManager.Worker.Operations;

public sealed class LocalBuildRetentionCleanupService
{
    private readonly Func<string, bool> _tryDeleteDirectory;

    public LocalBuildRetentionCleanupService(Func<string, bool>? tryDeleteDirectory = null)
    {
        _tryDeleteDirectory = tryDeleteDirectory ?? TryDeleteDirectory;
    }

    public CleanupResult Cleanup(string localBuildsRoot, int keepLatestPerApp)
    {
        if (string.IsNullOrWhiteSpace(localBuildsRoot) || !Directory.Exists(localBuildsRoot))
        {
            return new CleanupResult(0, 0, 0);
        }

        var keep = Math.Max(1, keepLatestPerApp);
        var scanned = 0;
        var deleted = 0;
        var failed = 0;

        foreach (var appDirectory in Directory.GetDirectories(localBuildsRoot))
        {
            var buildDirectories = Directory.GetDirectories(appDirectory)
                .Select(path => new DirectoryInfo(path))
                .OrderByDescending(x => x.LastWriteTimeUtc)
                .ToArray();

            scanned += buildDirectories.Length;

            foreach (var oldBuild in buildDirectories.Skip(keep))
            {
                if (_tryDeleteDirectory(oldBuild.FullName))
                {
                    deleted++;
                }
                else
                {
                    failed++;
                }
            }
        }

        return new CleanupResult(scanned, deleted, failed);
    }

    private static bool TryDeleteDirectory(string directory)
    {
        try
        {
            Directory.Delete(directory, recursive: true);
            return true;
        }
        catch
        {
            return false;
        }
    }
}