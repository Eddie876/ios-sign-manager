using System.Text;
using System.Text.Json;

namespace SignManager.Apple.Auth;

public sealed class EncryptedAppleSessionStore(string secretsFilePath, string masterKeyFilePath) : IAppleSessionStore
{
    private readonly SemaphoreSlim _lock = new(1, 1);

    public async Task SaveAsync(AppleSession session, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(session);

        var key = await LoadMasterKeyAsync(cancellationToken);
        var content = JsonSerializer.Serialize(session);
        var encrypted = AppleSessionCrypto.EncryptToEnvelope(content, key);

        var directory = Path.GetDirectoryName(secretsFilePath)
            ?? throw new InvalidOperationException("Secrets file path must include a parent directory.");

        Directory.CreateDirectory(directory);
        var tempFilePath = $"{secretsFilePath}.{Guid.NewGuid():N}.tmp";

        await _lock.WaitAsync(cancellationToken);
        try
        {
            await File.WriteAllTextAsync(tempFilePath, encrypted, Encoding.UTF8, cancellationToken);

            if (File.Exists(secretsFilePath))
            {
                File.Replace(tempFilePath, secretsFilePath, destinationBackupFileName: null);
            }
            else
            {
                File.Move(tempFilePath, secretsFilePath);
            }
        }
        finally
        {
            if (File.Exists(tempFilePath))
            {
                File.Delete(tempFilePath);
            }

            _lock.Release();
        }
    }

    public async Task<AppleSession?> LoadAsync(CancellationToken cancellationToken)
    {
        if (!File.Exists(secretsFilePath))
        {
            return null;
        }

        var key = await LoadMasterKeyAsync(cancellationToken);

        await _lock.WaitAsync(cancellationToken);
        try
        {
            var encrypted = await File.ReadAllTextAsync(secretsFilePath, cancellationToken);
            var json = AppleSessionCrypto.DecryptFromEnvelope(encrypted, key);
            return JsonSerializer.Deserialize<AppleSession>(json);
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task ClearAsync(CancellationToken cancellationToken)
    {
        await _lock.WaitAsync(cancellationToken);
        try
        {
            if (File.Exists(secretsFilePath))
            {
                File.Delete(secretsFilePath);
            }
        }
        finally
        {
            _lock.Release();
        }
    }

    private async Task<byte[]> LoadMasterKeyAsync(CancellationToken cancellationToken)
    {
        if (!File.Exists(masterKeyFilePath))
        {
            throw new InvalidOperationException("Master key file does not exist.");
        }

        var raw = (await File.ReadAllTextAsync(masterKeyFilePath, cancellationToken)).Trim();
        if (string.IsNullOrWhiteSpace(raw))
        {
            throw new InvalidOperationException("Master key file is empty.");
        }

        // Accept either Base64 or hex for operational flexibility.
        if (TryDecodeHex(raw, out var hexBytes))
        {
            return hexBytes;
        }

        return Convert.FromBase64String(raw);
    }

    private static bool TryDecodeHex(string value, out byte[] bytes)
    {
        bytes = [];

        if (value.Length % 2 != 0)
        {
            return false;
        }

        try
        {
            bytes = Convert.FromHexString(value);
            return true;
        }
        catch (FormatException)
        {
            return false;
        }
    }
}