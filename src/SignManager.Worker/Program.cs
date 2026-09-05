using SignManager.Apple.Developer;
using SignManager.Core.Policies;
using SignManager.Core.Services;
using SignManager.Infrastructure.Persistence;
using SignManager.Signing.Ipa;
using SignManager.Signing.Zsign;
using SignManager.Worker;
using SignManager.Worker.Signing;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.Configure<SchedulerOptions>(builder.Configuration.GetSection("Scheduler"));

builder.Services.AddSingleton<AppConfigStore>();
builder.Services.AddSingleton<AppStateStore>();
builder.Services.AddSingleton<RefreshPlanner>();
builder.Services.AddSingleton<RetryPolicy>();

builder.Services.AddSingleton<IProcessRunner, LocalProcessRunner>();
builder.Services.AddSingleton<SignedIpaValidator>();
builder.Services.AddSingleton<ZsignSigningService>();
builder.Services.AddSingleton<IpaPreflightService>();
builder.Services.AddSingleton<SourceIpaManager>();

builder.Services.AddSingleton<IGlobalSigningGate, GlobalSigningGate>();
builder.Services.AddSingleton<ISigningArtifactSigner, ZsignArtifactSigner>();
builder.Services.AddSingleton<IProvisioningMaterialProvider, UnavailableProvisioningMaterialProvider>();
builder.Services.AddSingleton<ISigningJobProcessor, SigningJobProcessor>();
builder.Services.AddSingleton<ManualSignTriggerStore>();
builder.Services.AddSingleton<WorkerSigningScheduler>();

builder.Services.AddHostedService<Worker>();

var host = builder.Build();
host.Run();
