using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using SignManager.Web.Services;

namespace SignManager.IntegrationTests;

public class OperationalReadinessHealthCheckTests
{
    [Fact]
    public async Task CheckHealthAsync_ShouldReturnHealthy_WhenDependenciesAreConfigured()
    {
        var root = CreateTempRoot();
        var appConfigPath = Path.Combine(root, "config", "apps.json");
        var appStatePath = Path.Combine(root, "state", "state.json");
        var settingsPath = Path.Combine(root, "config", "settings.json");
        var sourceRoot = Path.Combine(root, "sources");
        var uploadsRoot = Path.Combine(root, "uploads");
        var signingStateRoot = Path.Combine(root, "signing-state");
        var masterKeyPath = Path.Combine(root, "master.key");
        var zsignPath = Path.Combine(root, "zsign");

        Directory.CreateDirectory(Path.GetDirectoryName(appConfigPath)!);
        Directory.CreateDirectory(Path.GetDirectoryName(appStatePath)!);
        Directory.CreateDirectory(sourceRoot);
        Directory.CreateDirectory(uploadsRoot);
        Directory.CreateDirectory(signingStateRoot);

        await File.WriteAllTextAsync(appConfigPath, "{\"version\":1,\"apps\":[]}");
        await File.WriteAllTextAsync(appStatePath, "{\"version\":1,\"apps\":{}}");
        await File.WriteAllTextAsync(settingsPath, "{\"version\":1,\"publicBaseUrl\":\"https://ios.example.com\"}");
        await File.WriteAllTextAsync(masterKeyPath, Convert.ToBase64String(new byte[32]));
        await File.WriteAllTextAsync(zsignPath, "binary");

        var previous = CaptureEnvironment();
        try
        {
            Environment.SetEnvironmentVariable("SIGNMANAGER_MASTER_KEY_PATH", masterKeyPath);
            Environment.SetEnvironmentVariable("SIGNMANAGER_SIGNING_STATE_PATH", signingStateRoot);
            Environment.SetEnvironmentVariable("SIGNMANAGER_ZSIGN_PATH", zsignPath);
            Environment.SetEnvironmentVariable("SIGNMANAGER_ANISETTE_BASE_URL", "http://anisette:6969/");
            Environment.SetEnvironmentVariable("SIGNMANAGER_R2_ENDPOINT", "https://r2.example.com");
            Environment.SetEnvironmentVariable("SIGNMANAGER_R2_BUCKET", "bucket");
            Environment.SetEnvironmentVariable("SIGNMANAGER_R2_ACCESS_KEY_ID", "access");
            Environment.SetEnvironmentVariable("SIGNMANAGER_R2_SECRET_ACCESS_KEY", "secret");

            var options = new WebUiOptions(
                AppConfigPath: appConfigPath,
                AppStatePath: appStatePath,
                SettingsPath: settingsPath,
                SourceRootDirectory: sourceRoot,
                UploadTempDirectory: uploadsRoot);

            var sut = new OperationalReadinessHealthCheck(
                Options.Create(options),
                anisetteProbe: _ => Task.CompletedTask);

            var result = await sut.CheckHealthAsync(new HealthCheckContext(), CancellationToken.None);

            Assert.Equal(HealthStatus.Healthy, result.Status);
        }
        finally
        {
            RestoreEnvironment(previous);
            Cleanup(root);
        }
    }

    [Fact]
    public async Task CheckHealthAsync_ShouldReturnUnhealthy_WhenR2IsMissing()
    {
        var root = CreateTempRoot();
        var appConfigPath = Path.Combine(root, "config", "apps.json");
        var appStatePath = Path.Combine(root, "state", "state.json");
        var settingsPath = Path.Combine(root, "config", "settings.json");
        var sourceRoot = Path.Combine(root, "sources");
        var uploadsRoot = Path.Combine(root, "uploads");
        var masterKeyPath = Path.Combine(root, "master.key");
        var zsignPath = Path.Combine(root, "zsign");

        Directory.CreateDirectory(Path.GetDirectoryName(appConfigPath)!);
        Directory.CreateDirectory(Path.GetDirectoryName(appStatePath)!);
        Directory.CreateDirectory(sourceRoot);
        Directory.CreateDirectory(uploadsRoot);

        await File.WriteAllTextAsync(appConfigPath, "{\"version\":1,\"apps\":[]}");
        await File.WriteAllTextAsync(appStatePath, "{\"version\":1,\"apps\":{}}");
        await File.WriteAllTextAsync(settingsPath, "{\"version\":1,\"publicBaseUrl\":\"https://ios.example.com\"}");
        await File.WriteAllTextAsync(masterKeyPath, Convert.ToBase64String(new byte[32]));
        await File.WriteAllTextAsync(zsignPath, "binary");

        var previous = CaptureEnvironment();
        try
        {
            Environment.SetEnvironmentVariable("SIGNMANAGER_MASTER_KEY_PATH", masterKeyPath);
            Environment.SetEnvironmentVariable("SIGNMANAGER_ZSIGN_PATH", zsignPath);
            Environment.SetEnvironmentVariable("SIGNMANAGER_ANISETTE_BASE_URL", "http://anisette:6969/");
            Environment.SetEnvironmentVariable("SIGNMANAGER_R2_ENDPOINT", null);
            Environment.SetEnvironmentVariable("SIGNMANAGER_R2_BUCKET", null);
            Environment.SetEnvironmentVariable("SIGNMANAGER_R2_ACCESS_KEY_ID", null);
            Environment.SetEnvironmentVariable("SIGNMANAGER_R2_SECRET_ACCESS_KEY", null);

            var options = new WebUiOptions(
                AppConfigPath: appConfigPath,
                AppStatePath: appStatePath,
                SettingsPath: settingsPath,
                SourceRootDirectory: sourceRoot,
                UploadTempDirectory: uploadsRoot);

            var sut = new OperationalReadinessHealthCheck(
                Options.Create(options),
                anisetteProbe: _ => Task.CompletedTask);

            var result = await sut.CheckHealthAsync(new HealthCheckContext(), CancellationToken.None);

            Assert.Equal(HealthStatus.Unhealthy, result.Status);
            Assert.Contains("r2_config_failed", result.Description, StringComparison.Ordinal);
        }
        finally
        {
            RestoreEnvironment(previous);
            Cleanup(root);
        }
    }

    private static Dictionary<string, string?> CaptureEnvironment()
        => new(StringComparer.Ordinal)
        {
            ["SIGNMANAGER_MASTER_KEY_PATH"] = Environment.GetEnvironmentVariable("SIGNMANAGER_MASTER_KEY_PATH"),
            ["SIGNMANAGER_SIGNING_STATE_PATH"] = Environment.GetEnvironmentVariable("SIGNMANAGER_SIGNING_STATE_PATH"),
            ["SIGNMANAGER_ZSIGN_PATH"] = Environment.GetEnvironmentVariable("SIGNMANAGER_ZSIGN_PATH"),
            ["SIGNMANAGER_ANISETTE_BASE_URL"] = Environment.GetEnvironmentVariable("SIGNMANAGER_ANISETTE_BASE_URL"),
            ["SIGNMANAGER_R2_ENDPOINT"] = Environment.GetEnvironmentVariable("SIGNMANAGER_R2_ENDPOINT"),
            ["SIGNMANAGER_R2_BUCKET"] = Environment.GetEnvironmentVariable("SIGNMANAGER_R2_BUCKET"),
            ["SIGNMANAGER_R2_ACCESS_KEY_ID"] = Environment.GetEnvironmentVariable("SIGNMANAGER_R2_ACCESS_KEY_ID"),
            ["SIGNMANAGER_R2_SECRET_ACCESS_KEY"] = Environment.GetEnvironmentVariable("SIGNMANAGER_R2_SECRET_ACCESS_KEY"),
        };

    private static void RestoreEnvironment(IReadOnlyDictionary<string, string?> snapshot)
    {
        foreach (var entry in snapshot)
        {
            Environment.SetEnvironmentVariable(entry.Key, entry.Value);
        }
    }

    private static string CreateTempRoot()
    {
        var root = Path.Combine(Path.GetTempPath(), "sign-manager-ready-health-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        return root;
    }

    private static void Cleanup(string root)
    {
        if (Directory.Exists(root))
        {
            Directory.Delete(root, recursive: true);
        }
    }
}