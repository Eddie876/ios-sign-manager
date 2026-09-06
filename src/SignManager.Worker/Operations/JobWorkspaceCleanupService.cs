namespace SignManager.Worker.Operations;

public sealed class JobWorkspaceCleanupService
{
    public CleanupResult Cleanup(string workspaceRoot, TimeSpan maxAge, DateTimeOffset now)
    {
        if (string.IsNullOrWhiteSpace(workspaceRoot) || !Directory.Exists(workspaceRoot))
        {
            return new CleanupResult(0, 0);
        }

        var scanned = 0;
        var deleted = 0;

        foreach (var dir in Directory.GetDirectories(workspaceRoot))
        {
            scanned++;
            var lastWrite = Directory.GetLastWriteTimeUtc(dir);
            if (now - lastWrite <= maxAge)
            {
                continue;
            }

            Directory.Delete(dir, recursive: true);
            deleted++;
        }

        return new CleanupResult(scanned, deleted);
    }
}

public sealed record CleanupResult(int ScannedDirectories, int DeletedDirectories);
