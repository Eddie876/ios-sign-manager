using System.Security.Cryptography;
using System.Text;
using SignManager.Apple.Auth;

namespace SignManager.Apple.Tests;

public class AppleSpdDecryptorTests
{
    [Fact]
    public void DecryptToString_ShouldRoundtripKnownPayload()
    {
        var key = Encoding.UTF8.GetBytes("0123456789ABCDEF0123456789ABCDEF");
        var iv = Encoding.UTF8.GetBytes("ABCDEF0123456789");
        var plaintext = "{\"adsid\":\"123\",\"token\":\"abc\"}";

        var encrypted = EncryptForFixture(Encoding.UTF8.GetBytes(plaintext), key, iv);
        var result = AppleSpdDecryptor.DecryptToString(encrypted, key, iv);

        Assert.Equal(plaintext, result);
    }

    [Fact]
    public void Decrypt_ShouldThrow_OnInvalidIvLength()
    {
        var encrypted = new byte[16];
        var key = RandomNumberGenerator.GetBytes(32);
        var iv = RandomNumberGenerator.GetBytes(8);

        Assert.Throws<InvalidOperationException>(() => AppleSpdDecryptor.Decrypt(encrypted, key, iv));
    }

    private static byte[] EncryptForFixture(byte[] plaintext, byte[] key, byte[] iv)
    {
        using var aes = Aes.Create();
        aes.Mode = CipherMode.CBC;
        aes.Padding = PaddingMode.PKCS7;
        aes.Key = key;
        aes.IV = iv;

        using var encryptor = aes.CreateEncryptor();
        return encryptor.TransformFinalBlock(plaintext, 0, plaintext.Length);
    }
}
