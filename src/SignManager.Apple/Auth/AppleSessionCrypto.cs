using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace SignManager.Apple.Auth;

public static class AppleSessionCrypto
{
    private const int NonceSize = 12;
    private const int TagSize = 16;

    public static string EncryptToEnvelope(string plaintext, ReadOnlySpan<byte> key)
    {
        ValidateKey(key);

        var nonce = RandomNumberGenerator.GetBytes(NonceSize);
        var data = Encoding.UTF8.GetBytes(plaintext);
        var cipherText = new byte[data.Length];
        var tag = new byte[TagSize];

        using var aesGcm = new AesGcm(key, TagSize);
        aesGcm.Encrypt(nonce, data, cipherText, tag);

        var envelope = new EncryptedEnvelope(
            Version: 1,
            Nonce: Convert.ToBase64String(nonce),
            CipherText: Convert.ToBase64String(cipherText),
            Tag: Convert.ToBase64String(tag));

        return JsonSerializer.Serialize(envelope);
    }

    public static string DecryptFromEnvelope(string envelopeJson, ReadOnlySpan<byte> key)
    {
        ValidateKey(key);

        var envelope = JsonSerializer.Deserialize<EncryptedEnvelope>(envelopeJson)
            ?? throw new InvalidOperationException("Encrypted session envelope is invalid.");

        if (envelope.Version != 1)
        {
            throw new InvalidOperationException($"Unsupported session envelope version '{envelope.Version}'.");
        }

        var nonce = Convert.FromBase64String(envelope.Nonce);
        var cipherText = Convert.FromBase64String(envelope.CipherText);
        var tag = Convert.FromBase64String(envelope.Tag);
        var plainTextBytes = new byte[cipherText.Length];

        using var aesGcm = new AesGcm(key, TagSize);
        aesGcm.Decrypt(nonce, cipherText, tag, plainTextBytes);

        return Encoding.UTF8.GetString(plainTextBytes);
    }

    private static void ValidateKey(ReadOnlySpan<byte> key)
    {
        if (key.Length != 32)
        {
            throw new InvalidOperationException("Master key must be 32 bytes for AES-256-GCM.");
        }
    }

    private sealed record EncryptedEnvelope(
        int Version,
        string Nonce,
        string CipherText,
        string Tag);
}