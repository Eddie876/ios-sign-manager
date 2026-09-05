using System.Numerics;
using System.Security.Cryptography;
using System.Text;

namespace SignManager.Apple.Auth;

public static class AppleSrpPrimitives
{
    public static byte[] Sha256(params byte[][] chunks)
    {
        using var sha256 = SHA256.Create();
        foreach (var chunk in chunks)
        {
            sha256.TransformBlock(chunk, 0, chunk.Length, null, 0);
        }

        sha256.TransformFinalBlock([], 0, 0);
        return sha256.Hash ?? throw new InvalidOperationException("SHA-256 hash was not produced.");
    }

    public static byte[] HmacSha256(byte[] key, params byte[][] chunks)
    {
        using var hmac = new HMACSHA256(key);
        foreach (var chunk in chunks)
        {
            hmac.TransformBlock(chunk, 0, chunk.Length, null, 0);
        }

        hmac.TransformFinalBlock([], 0, 0);
        return hmac.Hash ?? throw new InvalidOperationException("HMAC-SHA256 hash was not produced.");
    }

    public static BigInteger ComputeK(BigInteger modulusN, BigInteger generatorG)
    {
        var nBytes = ToUnsignedBigEndian(modulusN);
        var gBytes = PadLeft(ToUnsignedBigEndian(generatorG), nBytes.Length);
        return ToUnsignedBigInteger(Sha256(nBytes, gBytes));
    }

    public static BigInteger ComputeU(BigInteger clientPublicA, BigInteger serverPublicB, int padLength)
    {
        var aBytes = PadLeft(ToUnsignedBigEndian(clientPublicA), padLength);
        var bBytes = PadLeft(ToUnsignedBigEndian(serverPublicB), padLength);
        return ToUnsignedBigInteger(Sha256(aBytes, bBytes));
    }

    public static BigInteger ComputeX(byte[] salt, string appleId, string password)
    {
        var userPassword = Encoding.UTF8.GetBytes($"{appleId}:{password}");
        var userPasswordHash = Sha256(userPassword);
        return ToUnsignedBigInteger(Sha256(salt, userPasswordHash));
    }

    public static byte[] ComputeClientProof(byte[] a, byte[] b, byte[] sessionKey)
        => Sha256(a, b, sessionKey);

    public static byte[] ToUnsignedBigEndian(BigInteger value)
    {
        if (value.Sign < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(value), "Only unsigned values are supported.");
        }

        return value.ToByteArray(isUnsigned: true, isBigEndian: true);
    }

    public static BigInteger ToUnsignedBigInteger(byte[] bytes)
        => new(bytes, isUnsigned: true, isBigEndian: true);

    public static byte[] PadLeft(byte[] bytes, int targetLength)
    {
        if (bytes.Length > targetLength)
        {
            throw new ArgumentOutOfRangeException(nameof(targetLength), "Target length is smaller than source length.");
        }

        if (bytes.Length == targetLength)
        {
            return bytes;
        }

        var result = new byte[targetLength];
        Buffer.BlockCopy(bytes, 0, result, targetLength - bytes.Length, bytes.Length);
        return result;
    }
}