using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace SignManager.Apple.Developer;

public sealed class CsrGenerator : ICsrGenerator
{
    public GeneratedCsr Generate(string commonName, string exportPassword)
    {
        if (string.IsNullOrWhiteSpace(commonName))
        {
            throw new ArgumentException("Common name is required.", nameof(commonName));
        }

        if (string.IsNullOrWhiteSpace(exportPassword))
        {
            throw new ArgumentException("Export password is required.", nameof(exportPassword));
        }

        using var rsa = RSA.Create(2048);
        var request = new CertificateRequest(
            $"CN={commonName}",
            rsa,
            HashAlgorithmName.SHA256,
            RSASignaturePadding.Pkcs1);

        var csrDer = request.CreateSigningRequest();
        var csrPem = new string(PemEncoding.Write("CERTIFICATE REQUEST", csrDer));

        var encryptedPkcs8 = rsa.ExportEncryptedPkcs8PrivateKey(
            exportPassword,
            new PbeParameters(PbeEncryptionAlgorithm.Aes256Cbc, HashAlgorithmName.SHA256, 100_000));
        var encryptedPrivateKeyPem = new string(PemEncoding.Write("ENCRYPTED PRIVATE KEY", encryptedPkcs8));

        return new GeneratedCsr(csrPem, encryptedPrivateKeyPem);
    }
}
