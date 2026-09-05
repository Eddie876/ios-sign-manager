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
            var expectedExpiration = new DateTimeOffset(2026, 9, 13, 0, 0, 0, TimeSpan.Zero);

            var fakeRunner = new FakeProcessRunner(() =>
            {
                SignedIpaFixture.Create(root, "com.eddie.sideload.qrscanner", "profile-uuid", expectedExpiration);
                File.Move(Path.Combine(root, "signed.ipa"), outputIpa, overwrite: true);
            });

            var sut = new ZsignSigningService(fakeRunner, new SignedIpaValidator());
            var artifact = await sut.SignAsync(new ZsignSignRequest(
                ZsignExecutablePath: "zsign",
                WorkspaceDirectory: root,
                SourceIpaPath: Path.Combine(root, "source.ipa"),
                OutputIpaPath: outputIpa,
                PrivateKeyPath: "private-key.pem",
                CertificatePath: "certificate.pem",
                MobileProvisionPath: "profile.mobileprovision",
                EffectiveBundleId: "com.eddie.sideload.qrscanner",
                RemoveExtensions: true,
                ExpectedBundleId: "com.eddie.sideload.qrscanner",
                ExpectedProfileUuid: "profile-uuid",
                ExpectedProfileExpirationDate: expectedExpiration,
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

    private sealed class FakeProcessRunner(Action onRun) : IProcessRunner
    {
        public ProcessRunRequest? LastRequest { get; private set; }

        public Task<ProcessRunResult> RunAsync(ProcessRunRequest request, CancellationToken cancellationToken)
        {
            LastRequest = request;
            onRun();
            return Task.FromResult(new ProcessRunResult(
                ExitCode: 0,
                TimedOut: false,
                StandardOutput: "ok",
                StandardError: string.Empty,
                Duration: TimeSpan.FromSeconds(1)));
        }
    }
}
