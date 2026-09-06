using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using SignManager.Apple.Anisette;
using SignManager.Apple.Auth;
using SignManager.Apple.Developer;

var configuration = new ConfigurationBuilder()
    .AddJsonFile("appsettings.json", optional: true)
    .AddEnvironmentVariables()
    .Build();

var services = new ServiceCollection();
services.AddSingleton<IConfiguration>(configuration);
services.AddOptions<AppleCliOptions>()
    .Configure<IConfiguration>((options, cfg) =>
    {
        cfg.GetSection("AppleCli").Bind(options);

        var anisetteBaseUrl = Environment.GetEnvironmentVariable("SIGNMANAGER_ANISETTE_BASE_URL");
        var grandSlamBaseUrl = Environment.GetEnvironmentVariable("SIGNMANAGER_APPLE_GRANDSLAM_BASE_URL");
        var developerBaseUrl = Environment.GetEnvironmentVariable("SIGNMANAGER_APPLE_DEVELOPER_BASE_URL");
        var signingStatePath = Environment.GetEnvironmentVariable("SIGNMANAGER_SIGNING_STATE_PATH");
        var masterKeyPath = Environment.GetEnvironmentVariable("SIGNMANAGER_MASTER_KEY_PATH");

        if (!string.IsNullOrWhiteSpace(anisetteBaseUrl))
        {
            options.AnisetteBaseUrl = anisetteBaseUrl;
        }

        if (!string.IsNullOrWhiteSpace(grandSlamBaseUrl))
        {
            options.GrandSlamBaseUrl = grandSlamBaseUrl;
        }

        if (!string.IsNullOrWhiteSpace(developerBaseUrl))
        {
            options.DeveloperBaseUrl = developerBaseUrl;
        }

        if (!string.IsNullOrWhiteSpace(signingStatePath))
        {
            var normalized = signingStatePath.TrimEnd('/', '\\');
            options.SecretsFilePath = $"{normalized}/secrets.enc";
        }

        if (!string.IsNullOrWhiteSpace(masterKeyPath))
        {
            options.MasterKeyFilePath = masterKeyPath;
        }
    });
services.AddSingleton<IAppleSessionStore>(serviceProvider =>
{
    var options = serviceProvider.GetRequiredService<IOptions<AppleCliOptions>>().Value;
    EnsureParentDirectory(options.SecretsFilePath);
    EnsureParentDirectory(options.MasterKeyFilePath);
    EnsureMasterKeyFile(options.MasterKeyFilePath);

    return new EncryptedAppleSessionStore(options.SecretsFilePath, options.MasterKeyFilePath);
});

services.AddSingleton<IAnisetteProvider>(serviceProvider =>
{
    var options = serviceProvider.GetRequiredService<IOptions<AppleCliOptions>>().Value;
    var client = new HttpClient
    {
        BaseAddress = new Uri(options.AnisetteBaseUrl),
        Timeout = TimeSpan.FromSeconds(options.HttpTimeoutSeconds),
    };

    return new HttpAnisetteProvider(client, options.AnisetteHeadersPath);
});

services.AddSingleton<IAppleGrandSlamClient>(serviceProvider =>
{
    var options = serviceProvider.GetRequiredService<IOptions<AppleCliOptions>>().Value;
    var anisetteProvider = serviceProvider.GetRequiredService<IAnisetteProvider>();
    var client = new HttpClient
    {
        BaseAddress = new Uri(options.GrandSlamBaseUrl),
        Timeout = TimeSpan.FromSeconds(options.HttpTimeoutSeconds),
    };

    return new GrandSlamHttpClient(client, anisetteProvider);
});

services.AddSingleton<IAppleDeveloperClient>(serviceProvider =>
{
    var options = serviceProvider.GetRequiredService<IOptions<AppleCliOptions>>().Value;
    var client = new HttpClient
    {
        BaseAddress = new Uri(options.DeveloperBaseUrl),
        Timeout = TimeSpan.FromSeconds(options.HttpTimeoutSeconds),
    };

    return new HttpAppleDeveloperClient(client);
});

services.AddSingleton<AppleAuthenticationService>();

using var provider = services.BuildServiceProvider();
var auth = provider.GetRequiredService<AppleAuthenticationService>();

if (args.Length >= 2 && string.Equals(args[0], "apple", StringComparison.OrdinalIgnoreCase) && string.Equals(args[1], "login", StringComparison.OrdinalIgnoreCase))
{
    var appleId = ReadRequired("Apple ID: ");
    var password = ReadSecret("Password: ");
    var first = await auth.LoginAsync(appleId, password, twoFactorCode: null, CancellationToken.None);

    if (!first.Success && string.Equals(first.ErrorCode, "APPLE_2FA_REQUIRED", StringComparison.Ordinal))
    {
        var code = ReadRequired("2FA Code: ");
        var second = await auth.LoginAsync(appleId, password, code, CancellationToken.None);
        if (!second.Success)
        {
            Console.Error.WriteLine($"Login failed: {second.ErrorCode}");
            return 1;
        }

        Console.WriteLine("Login succeeded with 2FA. Session saved.");
        return 0;
    }

    if (!first.Success)
    {
        Console.Error.WriteLine($"Login failed: {first.ErrorCode}");
        return 1;
    }

    Console.WriteLine("Login succeeded. Session saved.");
    return 0;
}

if (args.Length >= 2 && string.Equals(args[0], "apple", StringComparison.OrdinalIgnoreCase) && string.Equals(args[1], "restore", StringComparison.OrdinalIgnoreCase))
{
    var restored = await auth.RestoreSessionAsync(CancellationToken.None);
    if (!restored.Success)
    {
        Console.Error.WriteLine($"Session restore failed: {restored.ErrorCode}");
        return 1;
    }

    Console.WriteLine("Session restore succeeded.");
    return 0;
}

Console.WriteLine("Usage:");
Console.WriteLine("  dotnet SignManager.Cli.dll apple login");
Console.WriteLine("  dotnet SignManager.Cli.dll apple restore");
return 1;

static string ReadRequired(string prompt)
{
    Console.Write(prompt);
    var value = Console.ReadLine();
    if (string.IsNullOrWhiteSpace(value))
    {
        throw new InvalidOperationException($"Input is required: {prompt}");
    }

    return value.Trim();
}

static string ReadSecret(string prompt)
{
    Console.Write(prompt);

    var chars = new List<char>();
    while (true)
    {
        var key = Console.ReadKey(intercept: true);
        if (key.Key == ConsoleKey.Enter)
        {
            Console.WriteLine();
            break;
        }

        if (key.Key == ConsoleKey.Backspace)
        {
            if (chars.Count > 0)
            {
                chars.RemoveAt(chars.Count - 1);
            }

            continue;
        }

        if (!char.IsControl(key.KeyChar))
        {
            chars.Add(key.KeyChar);
        }
    }

    if (chars.Count == 0)
    {
        throw new InvalidOperationException("Password is required.");
    }

    return new string(chars.ToArray());
}

static void EnsureParentDirectory(string filePath)
{
    var dir = Path.GetDirectoryName(filePath);
    if (!string.IsNullOrWhiteSpace(dir))
    {
        Directory.CreateDirectory(dir);
    }
}

static void EnsureMasterKeyFile(string masterKeyPath)
{
    if (File.Exists(masterKeyPath))
    {
        return;
    }

    var normalized = masterKeyPath.Replace('\\', '/');
    if (normalized.StartsWith("/run/secrets/", StringComparison.Ordinal))
    {
        throw new InvalidOperationException(
            $"Master key file not found at '{masterKeyPath}'. Mount secret '/run/secrets/signmanager_master_key' or set SIGNMANAGER_MASTER_KEY_PATH.");
    }

    var key = Convert.ToBase64String(System.Security.Cryptography.RandomNumberGenerator.GetBytes(32));
    File.WriteAllText(masterKeyPath, key);
}

public sealed class AppleCliOptions
{
    public string AnisetteBaseUrl { get; set; } = "http://anisette:6969/";

    public string AnisetteHeadersPath { get; set; } = "headers";

    public string GrandSlamBaseUrl { get; set; } = "http://apple-gateway.local/";

    public string DeveloperBaseUrl { get; set; } = "http://apple-gateway.local/";

    public string SecretsFilePath { get; set; } = "/signing-state/secrets.enc";

    public string MasterKeyFilePath { get; set; } = "/run/secrets/signmanager_master_key";

    public int HttpTimeoutSeconds { get; set; } = 30;
}
