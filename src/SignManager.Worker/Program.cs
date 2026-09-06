using SignManager.Apple.Anisette;
using SignManager.Apple.Auth;
using SignManager.Apple.Developer;
using SignManager.Core.Policies;
using SignManager.Core.Services;
using SignManager.Infrastructure.Ota;
using SignManager.Infrastructure.Notifications;
using SignManager.Infrastructure.Operations;
using SignManager.Infrastructure.Persistence;
using SignManager.Infrastructure.R2;
using SignManager.Signing.Ipa;
using SignManager.Signing.Zsign;
using SignManager.Worker;
using SignManager.Worker.Operations;
using SignManager.Worker.Signing;
using Microsoft.Extensions.Options;

var builder = Host.CreateApplicationBuilder(args);

builder.Logging.AddJsonConsole(options =>
{
	options.IncludeScopes = true;
	options.TimestampFormat = "yyyy-MM-ddTHH:mm:ss.fffZ ";
});

builder.Services.Configure<SchedulerOptions>(builder.Configuration.GetSection("Scheduler"));
builder.Services.PostConfigure<SchedulerOptions>(options =>
{
	var dataPath = Environment.GetEnvironmentVariable("SIGNMANAGER_DATA_PATH");
	var signingStatePath = Environment.GetEnvironmentVariable("SIGNMANAGER_SIGNING_STATE_PATH");

	var anisetteBaseUrl = Environment.GetEnvironmentVariable("SIGNMANAGER_ANISETTE_BASE_URL");
	var grandSlamBaseUrl = Environment.GetEnvironmentVariable("SIGNMANAGER_APPLE_GRANDSLAM_BASE_URL");
	var developerBaseUrl = Environment.GetEnvironmentVariable("SIGNMANAGER_APPLE_DEVELOPER_BASE_URL");
	var masterKeyPath = Environment.GetEnvironmentVariable("SIGNMANAGER_MASTER_KEY_PATH");

	var deviceUdid = Environment.GetEnvironmentVariable("SIGNMANAGER_APPLE_DEVICE_UDID");
	var deviceName = Environment.GetEnvironmentVariable("SIGNMANAGER_APPLE_DEVICE_NAME");
	var teamId = Environment.GetEnvironmentVariable("SIGNMANAGER_APPLE_TEAM_ID");
	var privateKeyPassword = Environment.GetEnvironmentVariable("SIGNMANAGER_APPLE_PRIVATE_KEY_PASSWORD");

	if (!string.IsNullOrWhiteSpace(dataPath))
	{
		var normalized = dataPath.TrimEnd('/', '\\');
		options.AppConfigPath = $"{normalized}/config/apps.json";
		options.AppStatePath = $"{normalized}/state/state.json";
		options.WorkspaceRoot = $"{normalized}/jobs";
	}

	if (!string.IsNullOrWhiteSpace(signingStatePath))
	{
		var normalized = signingStatePath.TrimEnd('/', '\\');
		options.SigningStateRoot = normalized;
		options.AppleSessionSecretsPath = $"{normalized}/secrets.enc";
	}

	if (!string.IsNullOrWhiteSpace(anisetteBaseUrl))
	{
		options.AppleAnisetteBaseUrl = anisetteBaseUrl;
	}

	if (!string.IsNullOrWhiteSpace(grandSlamBaseUrl))
	{
		options.AppleGrandSlamBaseUrl = grandSlamBaseUrl;
	}

	if (!string.IsNullOrWhiteSpace(developerBaseUrl))
	{
		options.AppleDeveloperBaseUrl = developerBaseUrl;
	}

	if (!string.IsNullOrWhiteSpace(masterKeyPath))
	{
		options.AppleSessionMasterKeyPath = masterKeyPath;
	}

	if (!string.IsNullOrWhiteSpace(deviceUdid))
	{
		options.AppleDeviceUdid = deviceUdid;
	}

	if (!string.IsNullOrWhiteSpace(deviceName))
	{
		options.AppleDeviceName = deviceName;
	}

	if (!string.IsNullOrWhiteSpace(teamId))
	{
		options.AppleTeamId = teamId;
	}

	if (!string.IsNullOrWhiteSpace(privateKeyPassword))
	{
		options.ApplePrivateKeyPassword = privateKeyPassword;
	}
});

builder.Services.AddSingleton<AppConfigStore>();
builder.Services.AddSingleton<AppStateStore>();
builder.Services.AddSingleton<RefreshPlanner>();
builder.Services.AddSingleton<RetryPolicy>();
builder.Services.AddSingleton<R2ObjectKeyPlanner>();
builder.Services.AddSingleton<OtaManifestGenerator>();
builder.Services.AddSingleton<IR2ObjectStore, InMemoryR2ObjectStore>();
builder.Services.AddSingleton<R2ReleasePublisher>();
builder.Services.AddSingleton<DataBackupService>();

builder.Services.AddSingleton<IProcessRunner, LocalProcessRunner>();
builder.Services.AddSingleton<SignedIpaValidator>();
builder.Services.AddSingleton<ZsignSigningService>();
builder.Services.AddSingleton<IpaPreflightService>();
builder.Services.AddSingleton<SourceIpaManager>();
builder.Services.AddSingleton<ICsrGenerator, CsrGenerator>();
builder.Services.AddSingleton<ProvisioningProfileParser>();
builder.Services.AddSingleton<AppleProvisioningService>(serviceProvider =>
{
	var options = serviceProvider.GetRequiredService<IOptions<SchedulerOptions>>().Value;
	return new AppleProvisioningService(
		serviceProvider.GetRequiredService<IAppleDeveloperClient>(),
		serviceProvider.GetRequiredService<ICsrGenerator>(),
		serviceProvider.GetRequiredService<ProvisioningProfileParser>(),
		new ProfileFreshnessPolicy(options.ProfileMinimumFreshHours));
});

builder.Services.AddSingleton<IAppleSessionStore>(serviceProvider =>
{
	var options = serviceProvider.GetRequiredService<IOptions<SchedulerOptions>>().Value;
	return new EncryptedAppleSessionStore(options.AppleSessionSecretsPath, options.AppleSessionMasterKeyPath);
});

builder.Services.AddSingleton<IAnisetteProvider>(serviceProvider =>
{
	var options = serviceProvider.GetRequiredService<IOptions<SchedulerOptions>>().Value;
	var client = new HttpClient
	{
		BaseAddress = new Uri(options.AppleAnisetteBaseUrl),
		Timeout = TimeSpan.FromSeconds(Math.Max(5, options.AppleHttpTimeoutSeconds)),
	};

	return new HttpAnisetteProvider(client, options.AppleAnisetteHeadersPath);
});

builder.Services.AddSingleton<IAppleGrandSlamClient>(serviceProvider =>
{
	var options = serviceProvider.GetRequiredService<IOptions<SchedulerOptions>>().Value;
	var client = new HttpClient
	{
		BaseAddress = new Uri(options.AppleGrandSlamBaseUrl),
		Timeout = TimeSpan.FromSeconds(Math.Max(5, options.AppleHttpTimeoutSeconds)),
	};

	return new GrandSlamHttpClient(client, serviceProvider.GetRequiredService<IAnisetteProvider>());
});

builder.Services.AddSingleton<IAppleDeveloperClient>(serviceProvider =>
{
	var options = serviceProvider.GetRequiredService<IOptions<SchedulerOptions>>().Value;
	var client = new HttpClient
	{
		BaseAddress = new Uri(options.AppleDeveloperBaseUrl),
		Timeout = TimeSpan.FromSeconds(Math.Max(5, options.AppleHttpTimeoutSeconds)),
	};

	return new HttpAppleDeveloperClient(client);
});

builder.Services.AddSingleton<AppleAuthenticationService>();

builder.Services.AddSingleton<IGlobalSigningGate, GlobalSigningGate>();
builder.Services.AddSingleton<ISigningArtifactSigner, ZsignArtifactSigner>();
builder.Services.AddSingleton<IProvisioningMaterialProvider, AppleProvisioningMaterialProvider>();
builder.Services.AddSingleton<IBuildPublisher, R2BuildPublisher>();
builder.Services.AddSingleton<ISigningJobProcessor, SigningJobProcessor>();
builder.Services.AddSingleton<ManualSignTriggerStore>();
builder.Services.AddSingleton<JobWorkspaceCleanupService>();

builder.Services.AddSingleton<IExceptionNotifier>(serviceProvider =>
{
	var schedulerOptions = serviceProvider.GetRequiredService<IOptions<SchedulerOptions>>().Value;
	if (!schedulerOptions.TelegramAlertsEnabled)
	{
		return new NoOpExceptionNotifier();
	}

	return new TelegramExceptionNotifier(
		new HttpClient(),
		new TelegramOptions(
			Enabled: true,
			BotToken: schedulerOptions.TelegramBotToken,
			ChatId: schedulerOptions.TelegramChatId));
});

builder.Services.AddSingleton<WorkerSigningScheduler>();

builder.Services.AddHostedService<Worker>();

var host = builder.Build();
host.Run();
