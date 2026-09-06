using SignManager.Signing.Tests.Fixtures;
using SignManager.Signing.Zsign;

namespace SignManager.Signing.Tests;

public class ZsignSigningServiceTests
{
    [Fact]
    public async Task SignAsync_ShouldRunProcessAndReturnArtifact_WhenValid()
    {
        var root = Path.Combine(Path.GetTempPath(), "sign-manager-signing-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);

        try
        {
            var outputIpa = Path.Combine(root, "output.ipa");
            var sourceIpa = Path.Combine(root, "source.ipa");
            var expectedExpiration = new DateTimeOffset(2026, 9, 13, 0, 0, 0, TimeSpan.Zero);
            await File.WriteAllBytesAsync(sourceIpa, [1, 2, 3]);
            var (privateKeyPath, certificatePath, mobileProvisionPath) = CreateSigningMaterialFiles(root);

            var fakeRunner = new FakeProcessRunner(() =>
            {
                SignedIpaFixture.Create(root, "com.eddie.sideload.qrscanner", "profile-uuid", expectedExpiration);
                File.Move(Path.Combine(root, "signed.ipa"), outputIpa, overwrite: true);
            });

            var sut = new ZsignSigningService(fakeRunner, new SignedIpaValidator());
            var artifact = await sut.SignAsync(new ZsignSignRequest(
                ZsignExecutablePath: "zsign",
                WorkspaceDirectory: root,
                SourceIpaPath: sourceIpa,
                OutputIpaPath: outputIpa,
                PrivateKeyPath: privateKeyPath,
                CertificatePath: certificatePath,
                MobileProvisionPath: mobileProvisionPath,
                EffectiveBundleId: "com.eddie.sideload.qrscanner",
                RemoveExtensions: true,
                ExpectedBundleId: "com.eddie.sideload.qrscanner",
                ExpectedProfileUuid: "profile-uuid",
                ExpectedProfileExpirationDate: expectedExpiration,
                AllowedEntitlementKeys: null,
                Timeout: TimeSpan.FromSeconds(30)),
                CancellationToken.None);

            Assert.True(File.Exists(artifact.Path));
            Assert.True(artifact.SizeBytes > 0);
            Assert.NotEmpty(artifact.Sha256);
            Assert.Contains("-E", fakeRunner.LastRequest?.Arguments ?? []);
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    [Fact]
    public async Task SignAsync_ShouldThrow_WhenSourceIpaMissing()
    {
        var root = Path.Combine(Path.GetTempPath(), "sign-manager-signing-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);

        try
        {
            var (privateKey, cert, profile) = CreateSigningMaterialFiles(root);
            var sut = new ZsignSigningService(new FakeProcessRunner(() => { }), new SignedIpaValidator());

            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                sut.SignAsync(new ZsignSignRequest(
                    ZsignExecutablePath: "zsign",
                    WorkspaceDirectory: root,
                    SourceIpaPath: Path.Combine(root, "missing.ipa"),
                    OutputIpaPath: Path.Combine(root, "out.ipa"),
                    PrivateKeyPath: privateKey,
                    CertificatePath: cert,
                    MobileProvisionPath: profile,
                    EffectiveBundleId: "com.eddie.sideload.qrscanner",
                    RemoveExtensions: true,
                    ExpectedBundleId: "com.eddie.sideload.qrscanner",
                    ExpectedProfileUuid: "profile-uuid",
                    ExpectedProfileExpirationDate: DateTimeOffset.UtcNow.AddDays(1),
                    AllowedEntitlementKeys: null,
                    Timeout: TimeSpan.FromSeconds(30)),
                CancellationToken.None));

            Assert.Contains("Source IPA does not exist", ex.Message, StringComparison.Ordinal);
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    [Fact]
    public async Task SignAsync_ShouldThrow_WhenProcessTimesOut()
    {
        var root = Path.Combine(Path.GetTempPath(), "sign-manager-signing-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);

        try
        {
            var source = Path.Combine(root, "source.ipa");
            await File.WriteAllBytesAsync(source, [1, 2, 3]);
            var (privateKey, cert, profile) = CreateSigningMaterialFiles(root);

            var runner = new FakeProcessRunner(() => { }, new ProcessRunResult(
                ExitCode: -1,
                TimedOut: true,
                StandardOutput: string.Empty,
                StandardError: string.Empty,
                Duration: TimeSpan.FromSeconds(30)));

            var sut = new ZsignSigningService(runner, new SignedIpaValidator());

            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                sut.SignAsync(new ZsignSignRequest(
                    ZsignExecutablePath: "zsign",
                    WorkspaceDirectory: root,
                    SourceIpaPath: source,
                    OutputIpaPath: Path.Combine(root, "output.ipa"),
                    PrivateKeyPath: privateKey,
                    CertificatePath: cert,
                    MobileProvisionPath: profile,
                    EffectiveBundleId: "com.eddie.sideload.qrscanner",
                    RemoveExtensions: false,
                    ExpectedBundleId: "com.eddie.sideload.qrscanner",
                    ExpectedProfileUuid: "profile-uuid",
                    ExpectedProfileExpirationDate: DateTimeOffset.UtcNow.AddDays(1),
                    AllowedEntitlementKeys: null,
                    Timeout: TimeSpan.FromSeconds(1)),
                CancellationToken.None));

            Assert.Contains("timed out", ex.Message, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    [Fact]
    public async Task SignAsync_ShouldThrow_WhenOutputMissingAfterSuccessExit()
    {
        var root = Path.Combine(Path.GetTempPath(), "sign-manager-signing-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);

        try
        {
            var source = Path.Combine(root, "source.ipa");
            await File.WriteAllBytesAsync(source, [1, 2, 3]);
            var (privateKey, cert, profile) = CreateSigningMaterialFiles(root);

            var sut = new ZsignSigningService(new FakeProcessRunner(() => { }), new SignedIpaValidator());

            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                sut.SignAsync(new ZsignSignRequest(
                    ZsignExecutablePath: "zsign",
                    WorkspaceDirectory: root,
                    SourceIpaPath: source,
                    OutputIpaPath: Path.Combine(root, "output.ipa"),
                    PrivateKeyPath: privateKey,
                    CertificatePath: cert,
                    MobileProvisionPath: profile,
                    EffectiveBundleId: "com.eddie.sideload.qrscanner",
                    RemoveExtensions: false,
                    ExpectedBundleId: "com.eddie.sideload.qrscanner",
                    ExpectedProfileUuid: "profile-uuid",
                    ExpectedProfileExpirationDate: DateTimeOffset.UtcNow.AddDays(1),
                    AllowedEntitlementKeys: null,
                    Timeout: TimeSpan.FromSeconds(10)),
                CancellationToken.None));

            Assert.Contains("not produced", ex.Message, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    private static (string PrivateKeyPath, string CertificatePath, string MobileProvisionPath) CreateSigningMaterialFiles(string root)
    {
        var privateKey = Path.Combine(root, "private-key.pem");
        var cert = Path.Combine(root, "certificate.pem");
        var profile = Path.Combine(root, "profile.mobileprovision");

        File.WriteAllText(privateKey, "pk");
        File.WriteAllText(cert, "cert");
        File.WriteAllText(profile, "profile");

        return (privateKey, cert, profile);
    }

    private sealed class FakeProcessRunner(Action onRun, ProcessRunResult? result = null) : IProcessRunner
    {
        public ProcessRunRequest? LastRequest { get; private set; }

        public Task<ProcessRunResult> RunAsync(ProcessRunRequest request, CancellationToken cancellationToken)
        {
            LastRequest = request;
            onRun();
            return Task.FromResult(result ?? new ProcessRunResult(
                ExitCode: 0,
                TimedOut: false,
                StandardOutput: "ok",
                StandardError: string.Empty,
                Duration: TimeSpan.FromSeconds(1)));
        }
    }
}
