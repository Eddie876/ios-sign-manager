using System.Security.Cryptography;
using SignManager.Core.Constants;
using SignManager.Core.Models;
using SignManager.Core.Policies;
using SignManager.Core.Services;
using SignManager.Signing.Ipa;
using SignManager.Signing.Zsign;
using SignManager.Worker.Signing;

namespace SignManager.Worker.Tests;

public class SigningJobProcessorTests
{
    [Fact]
    public async Task RunAsync_ShouldProduceBuildAndReadyState_WhenDependenciesSucceed()
    {
        var root = CreateTempRoot();

        try
        {
            var sourcePath = Path.Combine(root, "sources", "app1", "source.ipa");
            Directory.CreateDirectory(Path.GetDirectoryName(sourcePath)!);
            await File.WriteAllBytesAsync(sourcePath, [1, 2, 3, 4]);
            var sourceSha256 = await ComputeSha256Async(sourcePath);

            var request = CreateRequest(root, sourcePath, sourceSha256, DateTimeOffset.UtcNow);
            var provider = new FakeProvisioningProvider(CreateProvisioningMaterial(root, request.NowUtc));
            var signer = new FakeSigner(async (signRequest, _) =>
            {
                File.WriteAllBytes(signRequest.OutputIpaPath, [9, 9, 9, 9]);
                var hash = await ComputeSha256Async(signRequest.OutputIpaPath);
                return new SignedBuildArtifact(signRequest.OutputIpaPath, 4, hash, TimeSpan.FromSeconds(1));
            });
            var publisher = new FakeBuildPublisher((_, _) => Task.FromResult(new BuildPublishResult("itms-services://ok", "https://example/latest/manifest.plist", "https://example/latest/latest.json")));

            var processor = CreateProcessor(provider, signer, publisher);
            var result = await processor.RunAsync(request, CancellationToken.None);

            Assert.Equal(SigningJobStatus.Ready, result.Job.Status);
            Assert.Equal(RuntimeStatus.Ready, result.RuntimeState.Status);
            Assert.False(result.ShouldRetry);
            Assert.Null(result.ErrorCode);
            Assert.NotNull(result.Build);
            Assert.Equal(request.App.Id, result.Build!.AppId);
            Assert.Contains(SigningJobStatus.Preflight, result.Timeline);
            Assert.Contains(SigningJobStatus.Provisioning, result.Timeline);
            Assert.Contains(SigningJobStatus.Signing, result.Timeline);
            Assert.Contains(SigningJobStatus.Validation, result.Timeline);
            Assert.Contains(SigningJobStatus.Publishing, result.Timeline);
            Assert.Contains(SigningJobStatus.Ready, result.Timeline);
            var timeline = result.Timeline.ToArray();
            Assert.True(Array.IndexOf(timeline, SigningJobStatus.Publishing) < Array.IndexOf(timeline, SigningJobStatus.Ready));
            Assert.True(File.Exists(Path.Combine(root, "workspace", request.Job.JobId, "signed.ipa")));
        }
        finally
        {
            Cleanup(root);
        }
    }

    [Fact]
    public async Task RunAsync_ShouldFailWithInvalidIpa_AndNotRetry_WhenSourceShaMismatch()
    {
        var root = CreateTempRoot();

        try
        {
            var sourcePath = Path.Combine(root, "sources", "app1", "source.ipa");
            Directory.CreateDirectory(Path.GetDirectoryName(sourcePath)!);
            await File.WriteAllBytesAsync(sourcePath, [1, 2, 3, 4]);

            var request = CreateRequest(root, sourcePath, "mismatch", DateTimeOffset.UtcNow);
            var provider = new FakeProvisioningProvider(CreateProvisioningMaterial(root, request.NowUtc));
            var signer = new FakeSigner((_, _) => throw new InvalidOperationException("should not sign"));
            var publisher = new FakeBuildPublisher((_, _) => throw new InvalidOperationException("should not publish"));

            var processor = CreateProcessor(provider, signer, publisher);
            var result = await processor.RunAsync(request, CancellationToken.None);

            Assert.Equal(SigningJobStatus.Failed, result.Job.Status);
            Assert.Equal(StableErrorCodes.InvalidIpa, result.ErrorCode);
            Assert.False(result.ShouldRetry);
            Assert.Null(result.NextRetryAt);
        }
        finally
        {
            Cleanup(root);
        }
    }

    [Fact]
    public async Task RunAsync_ShouldScheduleRetry_WhenSignerFailsWithRetryableError()
    {
        var root = CreateTempRoot();

        try
        {
            var sourcePath = Path.Combine(root, "sources", "app1", "source.ipa");
            Directory.CreateDirectory(Path.GetDirectoryName(sourcePath)!);
            await File.WriteAllBytesAsync(sourcePath, [1, 2, 3, 4]);
            var sourceSha256 = await ComputeSha256Async(sourcePath);
            var now = new DateTimeOffset(2026, 9, 6, 3, 0, 0, TimeSpan.Zero);

            var request = CreateRequest(root, sourcePath, sourceSha256, now);
            var provider = new FakeProvisioningProvider(CreateProvisioningMaterial(root, request.NowUtc));
            var signer = new FakeSigner((_, _) => throw new InvalidOperationException("zsign failed with exit code 1"));
            var publisher = new FakeBuildPublisher((_, _) => throw new InvalidOperationException("should not publish"));

            var processor = CreateProcessor(provider, signer, publisher);
            var result = await processor.RunAsync(request, CancellationToken.None);

            Assert.Equal(SigningJobStatus.Failed, result.Job.Status);
            Assert.Equal(StableErrorCodes.ZsignFailed, result.ErrorCode);
            Assert.True(result.ShouldRetry);
            Assert.Equal(now.AddMinutes(15), result.NextRetryAt);
            Assert.Equal(1, result.Job.Attempt);
        }
        finally
        {
            Cleanup(root);
        }
    }

    [Fact]
    public async Task RunAsync_ShouldMarkAuthRequiredAndNotRetry_WhenProvisioningRequiresAuth()
    {
        var root = CreateTempRoot();

        try
        {
            var sourcePath = Path.Combine(root, "sources", "app1", "source.ipa");
            Directory.CreateDirectory(Path.GetDirectoryName(sourcePath)!);
            await File.WriteAllBytesAsync(sourcePath, [1, 2, 3, 4]);
            var sourceSha256 = await ComputeSha256Async(sourcePath);

            var request = CreateRequest(root, sourcePath, sourceSha256, DateTimeOffset.UtcNow);
            var provider = new FakeProvisioningProvider(new SigningWorkflowException(StableErrorCodes.AuthRequired, "session expired"));
            var signer = new FakeSigner((_, _) => throw new InvalidOperationException("should not sign"));
            var publisher = new FakeBuildPublisher((_, _) => throw new InvalidOperationException("should not publish"));

            var processor = CreateProcessor(provider, signer, publisher);
            var result = await processor.RunAsync(request, CancellationToken.None);

            Assert.Equal(SigningJobStatus.Failed, result.Job.Status);
            Assert.Equal(StableErrorCodes.AuthRequired, result.ErrorCode);
            Assert.Equal(RuntimeStatus.AuthRequired, result.RuntimeState.Status);
            Assert.False(result.ShouldRetry);
        }
        finally
        {
            Cleanup(root);
        }
    }

    [Fact]
    public async Task RunAsync_ShouldNotMapAuthFromExceptionMessage_WhenErrorIsUntyped()
    {
        var root = CreateTempRoot();

        try
        {
            var sourcePath = Path.Combine(root, "sources", "app1", "source.ipa");
            Directory.CreateDirectory(Path.GetDirectoryName(sourcePath)!);
            await File.WriteAllBytesAsync(sourcePath, [1, 2, 3, 4]);
            var sourceSha256 = await ComputeSha256Async(sourcePath);

            var request = CreateRequest(root, sourcePath, sourceSha256, DateTimeOffset.UtcNow);
            var provider = new FakeProvisioningProvider(CreateProvisioningMaterial(root, request.NowUtc));
            var signer = new FakeSigner((_, _) => throw new InvalidOperationException("auth token expired in signer"));
            var publisher = new FakeBuildPublisher((_, _) => throw new InvalidOperationException("should not publish"));

            var processor = CreateProcessor(provider, signer, publisher);
            var result = await processor.RunAsync(request, CancellationToken.None);

            Assert.Equal(SigningJobStatus.Failed, result.Job.Status);
            Assert.Equal(StableErrorCodes.ZsignFailed, result.ErrorCode);
            Assert.Equal(RuntimeStatus.Failed, result.RuntimeState.Status);
        }
        finally
        {
            Cleanup(root);
        }
    }

    [Fact]
    public async Task RunAsync_ShouldMapPublishFailureToR2UploadFailed_AndRetry()
    {
        var root = CreateTempRoot();

        try
        {
            var sourcePath = Path.Combine(root, "sources", "app1", "source.ipa");
            Directory.CreateDirectory(Path.GetDirectoryName(sourcePath)!);
            await File.WriteAllBytesAsync(sourcePath, [1, 2, 3, 4]);
            var sourceSha256 = await ComputeSha256Async(sourcePath);
            var now = new DateTimeOffset(2026, 9, 6, 3, 0, 0, TimeSpan.Zero);

            var request = CreateRequest(root, sourcePath, sourceSha256, now);
            var provider = new FakeProvisioningProvider(CreateProvisioningMaterial(root, request.NowUtc));
            var signer = new FakeSigner(async (signRequest, _) =>
            {
                File.WriteAllBytes(signRequest.OutputIpaPath, [9, 9, 9, 9]);
                var hash = await ComputeSha256Async(signRequest.OutputIpaPath);
                return new SignedBuildArtifact(signRequest.OutputIpaPath, 4, hash, TimeSpan.FromSeconds(1));
            });
            var publisher = new FakeBuildPublisher((_, _) => throw new InvalidOperationException("upload failed"));

            var processor = CreateProcessor(provider, signer, publisher);
            var result = await processor.RunAsync(request, CancellationToken.None);

            Assert.Equal(SigningJobStatus.Failed, result.Job.Status);
            Assert.Equal(StableErrorCodes.R2UploadFailed, result.ErrorCode);
            Assert.True(result.ShouldRetry);
            Assert.Equal(now.AddMinutes(15), result.NextRetryAt);
        }
        finally
        {
            Cleanup(root);
        }
    }

    [Fact]
    public async Task RunAsync_ShouldMapUnsupportedEntitlement_FromTypedValidationException()
    {
        var root = CreateTempRoot();

        try
        {
            var sourcePath = Path.Combine(root, "sources", "app1", "source.ipa");
            Directory.CreateDirectory(Path.GetDirectoryName(sourcePath)!);
            await File.WriteAllBytesAsync(sourcePath, [1, 2, 3, 4]);
            var sourceSha256 = await ComputeSha256Async(sourcePath);

            var request = CreateRequest(root, sourcePath, sourceSha256, DateTimeOffset.UtcNow);
            var provider = new FakeProvisioningProvider(CreateProvisioningMaterial(root, request.NowUtc));
            var signer = new FakeSigner((_, _) => throw new SignedIpaValidationException(StableErrorCodes.UnsupportedEntitlement, "unsupported entitlement"));
            var publisher = new FakeBuildPublisher((_, _) => throw new InvalidOperationException("should not publish"));

            var processor = CreateProcessor(provider, signer, publisher);
            var result = await processor.RunAsync(request, CancellationToken.None);

            Assert.Equal(SigningJobStatus.Failed, result.Job.Status);
            Assert.Equal(StableErrorCodes.UnsupportedEntitlement, result.ErrorCode);
            Assert.False(result.ShouldRetry);
        }
        finally
        {
            Cleanup(root);
        }
    }

    [Fact]
    public async Task RunAsync_ShouldFailSignedValidation_WhenArtifactHashDoesNotMatchOutput()
    {
        var root = CreateTempRoot();

        try
        {
            var sourcePath = Path.Combine(root, "sources", "app1", "source.ipa");
            Directory.CreateDirectory(Path.GetDirectoryName(sourcePath)!);
            await File.WriteAllBytesAsync(sourcePath, [1, 2, 3, 4]);
            var sourceSha256 = await ComputeSha256Async(sourcePath);

            var request = CreateRequest(root, sourcePath, sourceSha256, DateTimeOffset.UtcNow);
            var provider = new FakeProvisioningProvider(CreateProvisioningMaterial(root, request.NowUtc));
            var signer = new FakeSigner((signRequest, _) =>
            {
                File.WriteAllBytes(signRequest.OutputIpaPath, [9, 9, 9, 9]);
                return Task.FromResult(new SignedBuildArtifact(signRequest.OutputIpaPath, 4, "deadbeef", TimeSpan.FromSeconds(1)));
            });
            var publisher = new FakeBuildPublisher((_, _) => throw new InvalidOperationException("should not publish"));

            var processor = CreateProcessor(provider, signer, publisher);
            var result = await processor.RunAsync(request, CancellationToken.None);

            Assert.Equal(SigningJobStatus.Failed, result.Job.Status);
            Assert.Equal(StableErrorCodes.SignedIpaValidationFailed, result.ErrorCode);
            Assert.False(result.ShouldRetry);
        }
        finally
        {
            Cleanup(root);
        }
    }

    [Fact]
    public async Task RunAsync_ShouldPropagateCancellation()
    {
        var root = CreateTempRoot();

        try
        {
            var sourcePath = Path.Combine(root, "sources", "app1", "source.ipa");
            Directory.CreateDirectory(Path.GetDirectoryName(sourcePath)!);
            await File.WriteAllBytesAsync(sourcePath, [1, 2, 3, 4]);
            var sourceSha256 = await ComputeSha256Async(sourcePath);

            var request = CreateRequest(root, sourcePath, sourceSha256, DateTimeOffset.UtcNow);
            var provider = new FakeProvisioningProvider(CreateProvisioningMaterial(root, request.NowUtc));
            var signer = new FakeSigner((_, ct) => Task.FromCanceled<SignedBuildArtifact>(ct));
            var publisher = new FakeBuildPublisher((_, _) => throw new InvalidOperationException("should not publish"));

            var processor = CreateProcessor(provider, signer, publisher);
            using var cts = new CancellationTokenSource();
            cts.Cancel();

            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => processor.RunAsync(request, cts.Token));
        }
        finally
        {
            Cleanup(root);
        }
    }

    [Fact]
    public async Task GlobalSigningGate_ShouldAllowOnlyOneConcurrentExecution()
    {
        var gate = new GlobalSigningGate();
        var inFlight = 0;
        var maxInFlight = 0;

        async Task RunOnceAsync()
        {
            await gate.RunExclusiveAsync(async _ =>
            {
                var current = Interlocked.Increment(ref inFlight);
                maxInFlight = Math.Max(maxInFlight, current);
                await Task.Delay(80);
                Interlocked.Decrement(ref inFlight);
                return 0;
            }, CancellationToken.None);
        }

        await Task.WhenAll(RunOnceAsync(), RunOnceAsync(), RunOnceAsync());

        Assert.Equal(1, maxInFlight);
    }

    private static SigningJobProcessor CreateProcessor(
        IProvisioningMaterialProvider provider,
        ISigningArtifactSigner signer,
        IBuildPublisher buildPublisher)
        => new(
            new SourceIpaManager(new IpaPreflightService()),
            provider,
            signer,
            buildPublisher,
            new GlobalSigningGate(),
            new RetryPolicy(),
            new RefreshPlanner());

    private static SigningJobRequest CreateRequest(string root, string sourcePath, string sourceSha256, DateTimeOffset now)
    {
        var app = new ManagedAppConfig(
            Id: "app1",
            Name: "App One",
            Enabled: true,
            Source: new SourceArtifact(sourcePath, sourceSha256, now.AddDays(-1)),
            Identity: new BundleIdentity("com.vendor.source", "com.vendor.target"),
            Signing: new AppSigningConfig(true),
            Schedule: new AppScheduleConfig(true, 48),
            Publish: new AppPublishConfig("app-one"));

        var job = new SigningJob(
            JobId: "job-001",
            AppId: "app1",
            SourceSha256: sourceSha256,
            Type: SigningJobType.Auto,
            CreatedAt: now,
            Status: SigningJobStatus.Queued,
            Attempt: 0);

        var options = new SigningExecutionOptions(
            WorkspaceRoot: Path.Combine(root, "workspace"),
            ZsignExecutablePath: "zsign",
            ZsignTimeout: TimeSpan.FromSeconds(30));

        return new SigningJobRequest(job, app, CurrentState: null, options, now);
    }

    private static ProvisioningMaterial CreateProvisioningMaterial(string root, DateTimeOffset now)
    {
        var materialRoot = Path.Combine(root, "material");
        Directory.CreateDirectory(materialRoot);

        var privateKeyPath = Path.Combine(materialRoot, "private.pem");
        var certPath = Path.Combine(materialRoot, "cert.pem");
        var profilePath = Path.Combine(materialRoot, "profile.mobileprovision");

        File.WriteAllText(privateKeyPath, "key");
        File.WriteAllText(certPath, "cert");
        File.WriteAllText(profilePath, "profile");

        return new ProvisioningMaterial(
            PrivateKeyPath: privateKeyPath,
            CertificatePath: certPath,
            MobileProvisionPath: profilePath,
            Provisioning: new ProvisioningInfo(
                Uuid: "profile-uuid",
                CreationDate: now,
                ExpirationDate: now.AddDays(7),
                BundleId: "com.vendor.target",
                TeamId: "TEAM1",
                DeviceUdids: ["udid1"]));
    }

    private static async Task<string> ComputeSha256Async(string path)
    {
        await using var stream = File.OpenRead(path);
        using var sha256 = SHA256.Create();
        var hash = await sha256.ComputeHashAsync(stream);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    private static string CreateTempRoot()
    {
        var root = Path.Combine(Path.GetTempPath(), "sign-manager-worker-tests", Guid.NewGuid().ToString("N"));
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

    private sealed class FakeProvisioningProvider : IProvisioningMaterialProvider
    {
        private readonly ProvisioningMaterial? _material;
        private readonly Exception? _exception;

        public FakeProvisioningProvider(ProvisioningMaterial material)
        {
            _material = material;
        }

        public FakeProvisioningProvider(Exception exception)
        {
            _exception = exception;
        }

        public Task<ProvisioningMaterial> PrepareAsync(SigningJob job, ManagedAppConfig app, CancellationToken cancellationToken)
        {
            if (_exception is not null)
            {
                throw _exception;
            }

            return Task.FromResult(_material!);
        }
    }

    private sealed class FakeSigner(Func<SignArtifactRequest, CancellationToken, Task<SignedBuildArtifact>> handler)
        : ISigningArtifactSigner
    {
        public Task<SignedBuildArtifact> SignAsync(SignArtifactRequest request, CancellationToken cancellationToken)
            => handler(request, cancellationToken);
    }

    private sealed class FakeBuildPublisher(Func<BuildPublishRequest, CancellationToken, Task<BuildPublishResult>> handler)
        : IBuildPublisher
    {
        public Task<BuildPublishResult> PublishAsync(BuildPublishRequest request, CancellationToken cancellationToken)
            => handler(request, cancellationToken);
    }
}
