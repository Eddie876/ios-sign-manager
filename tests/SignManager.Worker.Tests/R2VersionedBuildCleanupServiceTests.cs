using SignManager.Infrastructure.R2;
using SignManager.Worker.Operations;

namespace SignManager.Worker.Tests;

public class R2VersionedBuildCleanupServiceTests
{
    [Fact]
    public async Task CleanupAsync_ShouldDeleteOnlyExpiredObjectsOutsideKeepLatest()
    {
        var now = new DateTimeOffset(2026, 9, 7, 0, 0, 0, TimeSpan.Zero);
        var store = new FakeStore(
        [
            new R2ObjectInfo("apps/ns/app1/builds/b1/app.ipa", now.AddDays(-40)),
            new R2ObjectInfo("apps/ns/app1/builds/b1/manifest.plist", now.AddDays(-40)),
            new R2ObjectInfo("apps/ns/app1/builds/b2/app.ipa", now.AddDays(-20)),
            new R2ObjectInfo("apps/ns/app1/builds/b3/app.ipa", now.AddDays(-2)),
            new R2ObjectInfo("apps/ns/app1/latest/latest.json", now.AddDays(-1)),
        ]);

        var service = new R2VersionedBuildCleanupService(store);
        var result = await service.CleanupAsync(
            retentionDays: 30,
            keepLatestBuildsPerApp: 2,
            now,
            CancellationToken.None);

        Assert.Equal(4, result.ScannedDirectories);
        Assert.Equal(2, result.DeletedDirectories);
        Assert.Equal(0, result.FailedDirectories);
        Assert.Contains("apps/ns/app1/builds/b1/app.ipa", store.DeletedKeys);
        Assert.Contains("apps/ns/app1/builds/b1/manifest.plist", store.DeletedKeys);
        Assert.DoesNotContain("apps/ns/app1/builds/b2/app.ipa", store.DeletedKeys);
        Assert.DoesNotContain("apps/ns/app1/builds/b3/app.ipa", store.DeletedKeys);
    }

    private sealed class FakeStore(IReadOnlyList<R2ObjectInfo> objects) : IR2ObjectStore
    {
        private readonly List<R2ObjectInfo> _objects = [.. objects];

        public List<string> DeletedKeys { get; } = [];

        public Task PutObjectAsync(R2PutObjectRequest request, CancellationToken cancellationToken)
            => Task.CompletedTask;

        public Task DeleteObjectIfExistsAsync(string key, CancellationToken cancellationToken)
        {
            DeletedKeys.Add(key);
            _objects.RemoveAll(x => string.Equals(x.Key, key, StringComparison.Ordinal));
            return Task.CompletedTask;
        }

        public Task<bool> ObjectExistsAsync(string key, CancellationToken cancellationToken)
            => Task.FromResult(_objects.Any(x => string.Equals(x.Key, key, StringComparison.Ordinal)));

        public Task<IReadOnlyList<R2ObjectInfo>> ListObjectsAsync(string prefix, CancellationToken cancellationToken)
            => Task.FromResult<IReadOnlyList<R2ObjectInfo>>(
                _objects.Where(x => x.Key.StartsWith(prefix ?? string.Empty, StringComparison.Ordinal)).ToArray());
    }
}