using System.Text.Json;

namespace SignManager.Infrastructure.Persistence;

public sealed class JsonAtomicFileStore<T>
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
    };

    private readonly SemaphoreSlim _lock = new(1, 1);

    public async Task<T?> ReadAsync(string path, CancellationToken cancellationToken)
    {
        await _lock.WaitAsync(cancellationToken);
        try
        {
            if (!File.Exists(path))
            {
                return default;
            }

            await using var stream = new FileStream(
                path,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                bufferSize: 16 * 1024,
                useAsync: true);

            return await JsonSerializer.DeserializeAsync<T>(stream, SerializerOptions, cancellationToken);
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task WriteAsync(string path, T value, CancellationToken cancellationToken)
    {
        var directory = Path.GetDirectoryName(path)
            ?? throw new InvalidOperationException("Path must include a parent directory.");

        Directory.CreateDirectory(directory);

        var tempPath = $"{path}.{Guid.NewGuid():N}.tmp";

        await _lock.WaitAsync(cancellationToken);
        try
        {
            await using (var tempStream = new FileStream(
                tempPath,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                bufferSize: 16 * 1024,
                useAsync: true))
            {
                await JsonSerializer.SerializeAsync(tempStream, value, SerializerOptions, cancellationToken);
                await tempStream.FlushAsync(cancellationToken);
                tempStream.Flush(flushToDisk: true);
            }

            if (File.Exists(path))
            {
                File.Replace(tempPath, path, destinationBackupFileName: null);
            }
            else
            {
                File.Move(tempPath, path);
            }
        }
        finally
        {
            if (File.Exists(tempPath))
            {
                File.Delete(tempPath);
            }

            _lock.Release();
        }
    }
}
