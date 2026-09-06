using System.Text;
using System.Text.Json;
using SignManager.Core.Constants;
using SignManager.Infrastructure.Ota;
using SignManager.Infrastructure.R2;

namespace SignManager.IntegrationTests;

public class Milestone9R2Tests
{
    [Fact]
    public async Task PublishAsync_ShouldUploadImmutableAndLatestArtifacts_WithExpectedMimeTypes()
    {
        var root = CreateTempRoot();

        try
        {
            var ipaPath = Path.Combine(root, "signed.ipa");
            await File.WriteAllBytesAsync(ipaPath, [1, 2, 3, 4]);

            var store = new InMemoryR2ObjectStore();
            var publisher = new R2ReleasePublisher(store, new R2ObjectKeyPlanner(), new OtaManifestGenerator());

            var request = new R2PublishRequest(
                RandomNamespace: "abcdefabcdefabcdefabcdefabcdefab",
                AppId: "qr-scanner",
                BuildId: "build-001",
                SignedIpaPath: ipaPath,
                BundleIdentifier: "com.vendor.qr",
                BundleVersion: "1.2.3",
                Title: "QR Scanner",
                Sha256: "cafebabe",
                SizeBytes: 4,
                CreatedAt: new DateTimeOffset(2026, 9, 6, 0, 0, 0, TimeSpan.Zero),
                PublicBaseUrl: "https://cdn.example.com");

            var result = await publisher.PublishAsync(request, CancellationToken.None);

            Assert.Equal("apps/abcdefabcdefabcdefabcdefabcdefab/qr-scanner/builds/build-001/app.ipa", result.Plan.VersionedIpaKey);
            Assert.Equal("https://cdn.example.com/apps/abcdefabcdefabcdefabcdefabcdefab/qr-scanner/latest/manifest.plist", result.LatestManifestUrl);
            Assert.Contains("itms-services://", result.InstallUrl, StringComparison.Ordinal);

            Assert.True(store.TryGet(result.Plan.VersionedIpaKey, out var ipaObject));
            Assert.Equal("application/octet-stream", ipaObject.ContentType);

            Assert.True(store.TryGet(result.Plan.VersionedManifestKey, out var manifestObject));
            Assert.Equal("application/x-plist", manifestObject.ContentType);
            var manifestText = Encoding.UTF8.GetString(manifestObject.Content);
            Assert.Contains("https://cdn.example.com/apps/abcdefabcdefabcdefabcdefabcdefab/qr-scanner/builds/build-001/app.ipa", manifestText, StringComparison.Ordinal);

            Assert.True(store.TryGet(result.Plan.VersionedBuildJsonKey, out var buildJsonObject));
            Assert.Equal("application/json", buildJsonObject.ContentType);

            Assert.True(store.TryGet(result.Plan.LatestJsonKey, out var latestJsonObject));
            var latestJson = JsonDocument.Parse(latestJsonObject.Content);
            Assert.Equal("build-001", latestJson.RootElement.GetProperty("BuildId").GetString());
            Assert.Equal("application/json", latestJsonObject.ContentType);
        }
        finally
        {
            Cleanup(root);
        }
    }

    [Fact]
    public async Task PublishAsync_ShouldRollbackUploadedObjects_WhenPartialFailureOccurs()
    {
        var root = CreateTempRoot();

        try
        {
            var ipaPath = Path.Combine(root, "signed.ipa");
            await File.WriteAllBytesAsync(ipaPath, [1, 2, 3, 4]);

            var failingStore = new FailingObjectStore("latest/manifest.plist");
            var publisher = new R2ReleasePublisher(failingStore, new R2ObjectKeyPlanner(), new OtaManifestGenerator());

            var request = new R2PublishRequest(
                RandomNamespace: "abcdefabcdefabcdefabcdefabcdefab",
                AppId: "qr-scanner",
                BuildId: "build-002",
                SignedIpaPath: ipaPath,
                BundleIdentifier: "com.vendor.qr",
                BundleVersion: "1.2.4",
                Title: "QR Scanner",
                Sha256: "deadbeef",
                SizeBytes: 4,
                CreatedAt: new DateTimeOffset(2026, 9, 6, 1, 0, 0, TimeSpan.Zero),
                PublicBaseUrl: "https://cdn.example.com");

            var ex = await Assert.ThrowsAsync<R2PublishException>(() => publisher.PublishAsync(request, CancellationToken.None));

            Assert.Equal(StableErrorCodes.R2UploadFailed, ex.ErrorCode);
            Assert.Empty(failingStore.Keys);
        }
        finally
        {
            Cleanup(root);
        }
    }

    private static string CreateTempRoot()
    {
        var root = Path.Combine(Path.GetTempPath(), "sign-manager-r2-tests", Guid.NewGuid().ToString("N"));
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

    private sealed class FailingObjectStore(string failOnSegment) : IR2ObjectStore
    {
        private readonly Dictionary<string, StoredR2Object> _objects = new(StringComparer.Ordinal);

        public IReadOnlyCollection<string> Keys => _objects.Keys;

        public Task PutObjectAsync(R2PutObjectRequest request, CancellationToken cancellationToken)
        {
            if (request.Key.Contains(failOnSegment, StringComparison.Ordinal))
            {
                throw new InvalidOperationException("simulated upload failure");
            }

            _objects[request.Key] = new StoredR2Object(request.ContentType, request.Content);
            return Task.CompletedTask;
        }

        public Task DeleteObjectIfExistsAsync(string key, CancellationToken cancellationToken)
        {
            _objects.Remove(key);
            return Task.CompletedTask;
        }
    }
}
