namespace SignManager.Apple.Developer;

public interface ICsrGenerator
{
    GeneratedCsr Generate(string commonName, string exportPassword);
}

public sealed record GeneratedCsr(
    string CsrPem,
    string EncryptedPrivateKeyPem);
