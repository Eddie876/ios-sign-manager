using SignManager.Signing.Tests.Fixtures;
using SignManager.Signing.Zsign;

namespace SignManager.Signing.Tests;

public class SignedIpaValidatorTests
{
    [Fact]
    public void Validate_ShouldPass_WhenMetadataMatches()
    {
        var root = Path.Combine(Path.GetTempPath(), "sign-manager-signing-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);

        try
        {
            var expectedExpiration = new DateTimeOffset(2026, 9, 13, 0, 0, 0, TimeSpan.Zero);
            var ipaPath = SignedIpaFixture.Create(root, "com.eddie.sideload.qrscanner", "profile-uuid", expectedExpiration);
            var validator = new SignedIpaValidator();

            var result = validator.Validate(new SignedIpaValidationRequest(
                ipaPath,
                "com.eddie.sideload.qrscanner",
                "profile-uuid",
                expectedExpiration));

            Assert.True(result.Success);
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    [Fact]
    public void Validate_ShouldFail_WhenBundleIdDiffers()
    {
        var root = Path.Combine(Path.GetTempPath(), "sign-manager-signing-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);

        try
        {
            var expectedExpiration = new DateTimeOffset(2026, 9, 13, 0, 0, 0, TimeSpan.Zero);
            var ipaPath = SignedIpaFixture.Create(root, "com.actual.app", "profile-uuid", expectedExpiration);
            var validator = new SignedIpaValidator();

            var result = validator.Validate(new SignedIpaValidationRequest(
                ipaPath,
                "com.expected.app",
                "profile-uuid",
                expectedExpiration));

            Assert.False(result.Success);
            Assert.Contains("Bundle ID mismatch", result.Error, StringComparison.Ordinal);
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
