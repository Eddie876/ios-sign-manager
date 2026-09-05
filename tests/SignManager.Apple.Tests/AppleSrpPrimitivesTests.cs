using System.Numerics;
using System.Text;
using SignManager.Apple.Auth;

namespace SignManager.Apple.Tests;

public class AppleSrpPrimitivesTests
{
    [Fact]
    public void Sha256_ShouldMatchKnownVector()
    {
        var hash = AppleSrpPrimitives.Sha256(Encoding.UTF8.GetBytes("abc"));
        var hex = Convert.ToHexString(hash).ToLowerInvariant();
        Assert.Equal("ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad", hex);
    }

    [Fact]
    public void ComputeX_ShouldBeDeterministic()
    {
        var salt = Convert.FromHexString("00112233445566778899AABBCCDDEEFF");
        var x1 = AppleSrpPrimitives.ComputeX(salt, "user@example.com", "password");
        var x2 = AppleSrpPrimitives.ComputeX(salt, "user@example.com", "password");
        Assert.Equal(x1, x2);
    }

    [Fact]
    public void ComputeU_ShouldProducePositiveBigInteger()
    {
        var u = AppleSrpPrimitives.ComputeU(new BigInteger(123), new BigInteger(456), padLength: 16);
        Assert.True(u > 0);
    }
}