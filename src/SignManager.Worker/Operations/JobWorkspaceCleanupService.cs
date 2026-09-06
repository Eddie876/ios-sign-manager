namespace SignManager.Worker.Operations;

public sealed class JobWorkspaceCleanupService
{
    private readonly Func<string, bool> _tryDeleteDirectory;

    public JobWorkspaceCleanupService(Func<string, bool>? tryDeleteDirectory = null)
    {
        _tryDeleteDirectory = tryDeleteDirectory ?? TryDeleteDirectory;
    }

    public CleanupResult Cleanup(string workspaceRoot, TimeSpan maxAge, DateTimeOffset now)
    {
        if (string.IsNullOrWhiteSpace(workspaceRoot) || !Directory.Exists(workspaceRoot))
        {
            return new CleanupResult(0, 0, 0);
        }

        if (maxAge < TimeSpan.Zero)
        {
            maxAge = TimeSpan.Zero;
        }

        var scanned = 0;
        var deleted = 0;
        var failed = 0;

        foreach (var dir in Directory.GetDirectories(workspaceRoot))
        {
            scanned++;
            var lastWrite = Directory.GetLastWriteTimeUtc(dir);
            if (now - lastWrite <= maxAge)
            {
                continue;
            }

            if (_tryDeleteDirectory(dir))
            {
                deleted++;
            }
            else
            {
                failed++;
            }
        }

        return new CleanupResult(scanned, deleted, failed);
    }

    private static bool TryDeleteDirectory(string dir)
    {
        try
        {
            Directory.Delete(dir, recursive: true);
            return true;
        }
        catch
        {
            return false;
        }
    }
}

public sealed record CleanupResult(int ScannedDirectories, int DeletedDirectories, int FailedDirectories);
