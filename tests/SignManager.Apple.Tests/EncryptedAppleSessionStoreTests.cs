using System.Security.Cryptography;
using SignManager.Apple.Auth;

namespace SignManager.Apple.Tests;

public class EncryptedAppleSessionStoreTests
{
    [Fact]
    public async Task SaveAndLoad_ShouldRoundtrip_WithoutPlaintextLeak()
    {
        var root = Path.Combine(Path.GetTempPath(), "sign-manager-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);

        try
        {
            var keyPath = Path.Combine(root, "master.key");
            var secretPath = Path.Combine(root, "secrets.enc");
            await File.WriteAllTextAsync(keyPath, Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)));

            var store = new EncryptedAppleSessionStore(secretPath, keyPath);
            var session = new AppleSession("adsid", "gs-token", DateTimeOffset.UtcNow, null);

            await store.SaveAsync(session, CancellationToken.None);
            var loaded = await store.LoadAsync(CancellationToken.None);
            var raw = await File.ReadAllTextAsync(secretPath);

            Assert.NotNull(loaded);
            Assert.Equal(session.AdsId, loaded!.AdsId);
            Assert.DoesNotContain("gs-token", raw, StringComparison.Ordinal);
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }
}