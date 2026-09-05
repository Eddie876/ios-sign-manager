using System.Security.Cryptography;
using SignManager.Apple.Developer;

namespace SignManager.Apple.Tests;

public class CsrGeneratorTests
{
    [Fact]
    public void Generate_ShouldReturnCsrAndEncryptedPrivateKey()
    {
        var generator = new CsrGenerator();

        var generated = generator.Generate("com.eddie.sideload.qrscanner", "test-password");

        Assert.Contains("BEGIN CERTIFICATE REQUEST", generated.CsrPem, StringComparison.Ordinal);
        Assert.Contains("BEGIN ENCRYPTED PRIVATE KEY", generated.EncryptedPrivateKeyPem, StringComparison.Ordinal);

        using var rsa = RSA.Create();
        rsa.ImportFromEncryptedPem(generated.EncryptedPrivateKeyPem, "test-password");

        Assert.Equal(2048, rsa.KeySize);
    }
}
