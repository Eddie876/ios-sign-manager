using System.Text.Json;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using SignManager.Apple.Auth;

namespace SignManager.Web.Services;

public sealed class OperationalReadinessHealthCheck(
    IOptions<WebUiOptions> options,
    Func<CancellationToken, Task>? anisetteProbe = null) : IHealthCheck
{
    private const string DefaultZsignPath = "/usr/local/bin/zsign";
    private const string DefaultMasterKeyPath = "/run/secrets/signmanager_master_key";
    private const string DefaultSecretsPath = "/signing-state/secrets.enc";

    private readonly Func<CancellationToken, Task> _anisetteProbe = anisetteProbe ?? ProbeAnisetteAsync;

    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        var failures = new List<string>();

        try
        {
            ValidateJsonReadable(options.Value);
        }
        catch (Exception ex)
        {
            failures.Add($"json_readable_failed: {ex.Message}");
        }

        try
        {
            ValidateDataPathWritable(options.Value);
        }
        catch (Exception ex)
        {
            failures.Add($"data_path_writable_failed: {ex.Message}");
        }

        try
        {
            await ValidateSecretStoreDecryptableAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            failures.Add($"secret_store_failed: {ex.Message}");
        }

        try
        {
            await _anisetteProbe(cancellationToken);
        }
        catch (Exception ex)
        {
            failures.Add($"anisette_failed: {ex.Message}");
        }

        try
        {
            ValidateZsignAvailable();
        }
        catch (Exception ex)
        {
            failures.Add($"zsign_failed: {ex.Message}");
        }

        try
        {
            ValidateR2Configured();
        }
        catch (Exception ex)
        {
            failures.Add($"r2_config_failed: {ex.Message}");
        }

        return failures.Count == 0
            ? HealthCheckResult.Healthy("Operational dependencies are ready.")
            : HealthCheckResult.Unhealthy(string.Join(" | ", failures));
    }

    private static void ValidateJsonReadable(WebUiOptions webUiOptions)
    {
        var paths = new[] { webUiOptions.AppConfigPath, webUiOptions.AppStatePath, webUiOptions.SettingsPath };
        foreach (var path in paths)
        {
            if (!File.Exists(path))
            {
                continue;
            }

            using var stream = File.OpenRead(path);
            using var _ = JsonDocument.Parse(stream);
        }
    }

    private static void ValidateDataPathWritable(WebUiOptions webUiOptions)
    {
        var directories = new[]
        {
            Path.GetDirectoryName(webUiOptions.AppConfigPath),
            Path.GetDirectoryName(webUiOptions.AppStatePath),
            Path.GetDirectoryName(webUiOptions.SettingsPath),
            webUiOptions.SourceRootDirectory,
            webUiOptions.UploadTempDirectory,
        }.Where(x => !string.IsNullOrWhiteSpace(x)).Distinct(StringComparer.OrdinalIgnoreCase);

        foreach (var directory in directories!)
        {
            Directory.CreateDirectory(directory!);
            var probeFile = Path.Combine(directory!, $".healthcheck-{Guid.NewGuid():N}.tmp");
            File.WriteAllText(probeFile, "ok");
            File.Delete(probeFile);
        }
    }

    private static async Task ValidateSecretStoreDecryptableAsync(CancellationToken cancellationToken)
    {
        var masterKeyPath = Environment.GetEnvironmentVariable("SIGNMANAGER_MASTER_KEY_PATH") ?? DefaultMasterKeyPath;
        var secretsPath = Environment.GetEnvironmentVariable("SIGNMANAGER_SIGNING_STATE_PATH") is { Length: > 0 } signingStatePath
            ? Path.Combine(signingStatePath.TrimEnd('/', '\\'), "secrets.enc")
            : DefaultSecretsPath;

        if (!File.Exists(masterKeyPath))
        {
            throw new InvalidOperationException($"Master key file not found: {masterKeyPath}");
        }

        var rawKey = (await File.ReadAllTextAsync(masterKeyPath, cancellationToken)).Trim();
        _ = DecodeMasterKey(rawKey);

        if (File.Exists(secretsPath))
        {
            var store = new EncryptedAppleSessionStore(secretsPath, masterKeyPath);
            await store.LoadAsync(cancellationToken);
        }
    }

    private static byte[] DecodeMasterKey(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            throw new InvalidOperationException("Master key file is empty.");
        }

        if (raw.Length % 2 == 0)
        {
            try
            {
                var hex = Convert.FromHexString(raw);
                if (hex.Length == 32)
                {
                    return hex;
                }
            }
            catch
            {
                // Try Base64 fallback.
            }
        }

        var base64 = Convert.FromBase64String(raw);
        if (base64.Length != 32)
        {
            throw new InvalidOperationException("Master key must decode to 32 bytes.");
        }

        return base64;
    }

    private static async Task ProbeAnisetteAsync(CancellationToken cancellationToken)
    {
        var baseUrl = Environment.GetEnvironmentVariable("SIGNMANAGER_ANISETTE_BASE_URL");
        if (string.IsNullOrWhiteSpace(baseUrl))
        {
            throw new InvalidOperationException("SIGNMANAGER_ANISETTE_BASE_URL is required.");
        }

        if (!Uri.TryCreate(baseUrl, UriKind.Absolute, out var uri))
        {
            throw new InvalidOperationException("SIGNMANAGER_ANISETTE_BASE_URL must be an absolute URL.");
        }

        using var httpClient = new HttpClient
        {
            BaseAddress = uri,
            Timeout = TimeSpan.FromSeconds(5),
        };

        using var response = await httpClient.GetAsync("headers", cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    private static void ValidateZsignAvailable()
    {
        var configuredPath = Environment.GetEnvironmentVariable("SIGNMANAGER_ZSIGN_PATH") ?? DefaultZsignPath;

        if (Path.IsPathRooted(configuredPath))
        {
            if (!File.Exists(configuredPath))
            {
                throw new InvalidOperationException($"zsign executable not found: {configuredPath}");
            }

            return;
        }

        var pathEnv = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
        var candidates = pathEnv.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries);
        var existsInPath = candidates.Any(dir => File.Exists(Path.Combine(dir, configuredPath)));
        if (!existsInPath)
        {
            throw new InvalidOperationException($"zsign executable '{configuredPath}' was not found in PATH.");
        }
    }

    private static void ValidateR2Configured()
    {
        var endpoint = Environment.GetEnvironmentVariable("SIGNMANAGER_R2_ENDPOINT");
        var bucket = Environment.GetEnvironmentVariable("SIGNMANAGER_R2_BUCKET");
        var accessKey = Environment.GetEnvironmentVariable("SIGNMANAGER_R2_ACCESS_KEY_ID");
        var secret = Environment.GetEnvironmentVariable("SIGNMANAGER_R2_SECRET_ACCESS_KEY");

        if (string.IsNullOrWhiteSpace(endpoint)
            || string.IsNullOrWhiteSpace(bucket)
            || string.IsNullOrWhiteSpace(accessKey)
            || string.IsNullOrWhiteSpace(secret))
        {
            throw new InvalidOperationException("R2 settings are incomplete.");
        }
    }
}