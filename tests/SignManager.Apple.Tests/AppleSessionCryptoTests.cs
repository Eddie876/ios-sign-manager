using System.Security.Cryptography;
using SignManager.Apple.Auth;

namespace SignManager.Apple.Tests;

public class AppleSessionCryptoTests
{
    [Fact]
    public void EncryptAndDecrypt_ShouldRoundtrip()
    {
        var key = RandomNumberGenerator.GetBytes(32);
        var plainText = "{\"adsid\":\"123\",\"gsToken\":\"secret\"}";

        var envelope = AppleSessionCrypto.EncryptToEnvelope(plainText, key);
        var decrypted = AppleSessionCrypto.DecryptFromEnvelope(envelope, key);

        Assert.Equal(plainText, decrypted);
    }

    [Fact]
    public void Decrypt_ShouldFailWithWrongKey()
    {
        var key = RandomNumberGenerator.GetBytes(32);
        var wrongKey = RandomNumberGenerator.GetBytes(32);
        var envelope = AppleSessionCrypto.EncryptToEnvelope("secret", key);

        Assert.ThrowsAny<CryptographicException>(() => AppleSessionCrypto.DecryptFromEnvelope(envelope, wrongKey));
    }
}