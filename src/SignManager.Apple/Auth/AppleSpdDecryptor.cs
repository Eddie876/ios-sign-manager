using System.Security.Cryptography;
using System.Text;

namespace SignManager.Apple.Auth;

public static class AppleSpdDecryptor
{
    public static string DecryptToString(byte[] encryptedSpd, byte[] key, byte[] iv)
    {
        ArgumentNullException.ThrowIfNull(encryptedSpd);
        ArgumentNullException.ThrowIfNull(key);
        ArgumentNullException.ThrowIfNull(iv);

        var plaintext = Decrypt(encryptedSpd, key, iv);
        return Encoding.UTF8.GetString(plaintext);
    }

    public static byte[] Decrypt(byte[] encryptedSpd, byte[] key, byte[] iv)
    {
        if (key.Length is not (16 or 24 or 32))
        {
            throw new InvalidOperationException("SPD AES key must be 16, 24, or 32 bytes.");
        }

        if (iv.Length != 16)
        {
            throw new InvalidOperationException("SPD AES-CBC IV must be 16 bytes.");
        }

        using var aes = Aes.Create();
        aes.Mode = CipherMode.CBC;
        aes.Padding = PaddingMode.PKCS7;
        aes.Key = key;
        aes.IV = iv;

        using var decryptor = aes.CreateDecryptor();
        return decryptor.TransformFinalBlock(encryptedSpd, 0, encryptedSpd.Length);
    }
}
