using System.Text;
using SignManager.Core.Models;
using SignManager.Infrastructure.Persistence;

namespace SignManager.IntegrationTests;

public class Milestone5PersistenceTests
{
    [Fact]
    public async Task AppConfigStore_ShouldRoundtripVersionedDocument()
    {
        var root = CreateTempRoot();

        try
        {
            var path = Path.Combine(root, "apps.json");
            var store = new AppConfigStore();

            var config = new AppConfig(
                Version: 1,
                Apps:
                [
                    new ManagedAppConfig(
                        Id: "qr-scanner",
                        Name: "QR Scanner",
                        Enabled: true,
                        Source: new SourceArtifact("/data/sources/qr/source.ipa", "abc", new DateTimeOffset(2026, 9, 6, 0, 0, 0, TimeSpan.Zero)),
                        Identity: new BundleIdentity("com.vendor.qr", "com.eddie.sideload.qrscanner"),
                        Signing: new AppSigningConfig(true),
                        Schedule: new AppScheduleConfig(true, 48),
                        Publish: new AppPublishConfig("qr-scanner")),
                ]);

            await store.SaveAsync(path, config, CancellationToken.None);
            var loaded = await store.LoadAsync(path, CancellationToken.None);
            var raw = await File.ReadAllTextAsync(path, Encoding.UTF8);

            Assert.NotNull(loaded);
            Assert.Equal(1, loaded!.Version);
            Assert.Single(loaded.Apps);
            Assert.Contains("\"version\": 1", raw, StringComparison.Ordinal);
        }
        finally
        {
            Cleanup(root);
        }
    }

    [Fact]
    public async Task AppStateStore_ShouldRoundtripVersionedDocument()
    {
        var root = CreateTempRoot();

        try
        {
            var path = Path.Combine(root, "state.json");
            var store = new AppStateStore();

            var now = new DateTimeOffset(2026, 9, 6, 0, 0, 0, TimeSpan.Zero);
            var state = new AppState(
                Version: 1,
                Apps: new Dictionary<string, AppRuntimeState>
                {
                    ["qr-scanner"] = new(
                        RuntimeStatus.Ready,
                        LastSuccessfulSignAt: now,
                        NextSignDueAt: now.AddHours(48),
                        LatestBuildId: "20260906T000000Z",
                        ProfileCreationDate: now,
                        ProfileExpirationDate: now.AddDays(7),
                        LastPromptAt: null,
                        LastErrorCode: null),
                });

            await store.SaveAsync(path, state, CancellationToken.None);
            var loaded = await store.LoadAsync(path, CancellationToken.None);

            Assert.NotNull(loaded);
            Assert.Equal(RuntimeStatus.Ready, loaded!.Apps["qr-scanner"].Status);
            Assert.Equal("20260906T000000Z", loaded.Apps["qr-scanner"].LatestBuildId);
        }
        finally
        {
            Cleanup(root);
        }
    }

    [Fact]
    public async Task AppConfigStore_ShouldRejectUnsupportedVersion()
    {
        var root = CreateTempRoot();

        try
        {
            var path = Path.Combine(root, "apps.json");
            await File.WriteAllTextAsync(path, "{\"version\":2,\"apps\":[]}", Encoding.UTF8);
            var store = new AppConfigStore();

            await Assert.ThrowsAsync<UnsupportedSchemaVersionException>(() => store.LoadAsync(path, CancellationToken.None));
        }
        finally
        {
            Cleanup(root);
        }
    }

        [Fact]
        public async Task AppConfigStore_ShouldRejectDuplicateAppIds()
        {
                var root = CreateTempRoot();

                try
                {
                        var path = Path.Combine(root, "apps.json");
                        var json = """
                        {
                            "version": 1,
                            "apps": [
                                {
                                    "id": "dup-app",
                                    "name": "One",
                                    "enabled": true,
                                    "source": {
                                        "path": "data/sources/dup/source.ipa",
                                        "sha256": "sha1",
                                        "uploadedAt": "2026-09-06T00:00:00Z"
                                    },
                                    "identity": {
                                        "sourceBundleId": "com.vendor.one",
                                        "effectiveBundleId": "com.vendor.one.effective"
                                    },
                                    "signing": {
                                        "removeExtensions": true
                                    },
                                    "schedule": {
                                        "autoSign": true,
                                        "intervalHours": 48
                                    },
                                    "publish": {
                                        "slug": "dup-app"
                                    }
                                },
                                {
                                    "id": "dup-app",
                                    "name": "Two",
                                    "enabled": true,
                                    "source": {
                                        "path": "data/sources/dup/source2.ipa",
                                        "sha256": "sha2",
                                        "uploadedAt": "2026-09-06T00:00:00Z"
                                    },
                                    "identity": {
                                        "sourceBundleId": "com.vendor.two",
                                        "effectiveBundleId": "com.vendor.two.effective"
                                    },
                                    "signing": {
                                        "removeExtensions": true
                                    },
                                    "schedule": {
                                        "autoSign": true,
                                        "intervalHours": 48
                                    },
                                    "publish": {
                                        "slug": "dup-app"
                                    }
                                }
                            ]
                        }
                        """;

                        await File.WriteAllTextAsync(path, json, Encoding.UTF8);
                        var store = new AppConfigStore();

                        await Assert.ThrowsAsync<InvalidOperationException>(() => store.LoadAsync(path, CancellationToken.None));
                }
                finally
                {
                        Cleanup(root);
                }
        }

        [Fact]
        public async Task AppStateStore_ShouldRejectInvalidProfileDateOrder()
        {
                var root = CreateTempRoot();

                try
                {
                        var path = Path.Combine(root, "state.json");
                        var now = new DateTimeOffset(2026, 9, 6, 0, 0, 0, TimeSpan.Zero);
                        var invalid = new AppState(
                                Version: AppStateStore.CurrentVersion,
                                Apps: new Dictionary<string, AppRuntimeState>
                                {
                                        ["qr-scanner"] = new(
                                                RuntimeStatus.Ready,
                                                LastSuccessfulSignAt: now,
                                                NextSignDueAt: now.AddHours(48),
                                                LatestBuildId: "build-1",
                                                ProfileCreationDate: now.AddDays(3),
                                                ProfileExpirationDate: now.AddDays(1),
                                                LastPromptAt: null,
                                                LastErrorCode: null),
                                });

                        var store = new AppStateStore();
                        await Assert.ThrowsAsync<InvalidOperationException>(() => store.SaveAsync(path, invalid, CancellationToken.None));
                }
                finally
                {
                        Cleanup(root);
                }
        }

    [Fact]
    public async Task JsonAtomicFileStore_ShouldReplaceExistingFile()
    {
        var root = CreateTempRoot();

        try
        {
            var path = Path.Combine(root, "atomic.json");
            var store = new JsonAtomicFileStore<AtomicDocument>();

            await store.WriteAsync(path, new AtomicDocument(1, "a"), CancellationToken.None);
            await store.WriteAsync(path, new AtomicDocument(2, "b"), CancellationToken.None);

            var loaded = await store.ReadAsync(path, CancellationToken.None);
            Assert.NotNull(loaded);
            Assert.Equal(2, loaded!.Version);
            Assert.Equal("b", loaded.Value);
        }
        finally
        {
            Cleanup(root);
        }
    }

    private static string CreateTempRoot()
    {
        var root = Path.Combine(Path.GetTempPath(), "sign-manager-tests", Guid.NewGuid().ToString("N"));
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

    private sealed record AtomicDocument(int Version, string Value);
}
