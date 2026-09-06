using SignManager.Signing.Tests.Fixtures;
using SignManager.Signing.Zsign;
using SignManager.Core.Constants;
using System.IO.Compression;
using System.Text;

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

    [Fact]
    public void Validate_ShouldFailWithUnsupportedEntitlement_WhenEntitlementIsNotAllowed()
    {
        var root = Path.Combine(Path.GetTempPath(), "sign-manager-signing-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);

        try
        {
            var expectedExpiration = new DateTimeOffset(2026, 9, 13, 0, 0, 0, TimeSpan.Zero);
            var ipaPath = SignedIpaFixture.Create(root, "com.eddie.sideload.qrscanner", "profile-uuid", expectedExpiration);

            using (var archive = ZipFile.Open(ipaPath, ZipArchiveMode.Update))
            {
                var entry = archive.CreateEntry("Payload/App.app/entitlements.plist");
                using var stream = entry.Open();
                using var writer = new StreamWriter(stream, Encoding.UTF8, leaveOpen: false);
                writer.Write("""
                <?xml version="1.0" encoding="UTF-8"?>
                <!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
                <plist version="1.0">
                <dict>
                  <key>com.apple.developer.associated-domains</key>
                  <array>
                    <string>applinks:example.com</string>
                  </array>
                </dict>
                </plist>
                """);
            }

            var validator = new SignedIpaValidator();
            var result = validator.Validate(new SignedIpaValidationRequest(
                ipaPath,
                "com.eddie.sideload.qrscanner",
                "profile-uuid",
                expectedExpiration,
                AllowedEntitlementKeys: new HashSet<string>(StringComparer.Ordinal)
                {
                    "application-identifier",
                    "com.apple.developer.team-identifier",
                    "keychain-access-groups",
                    "get-task-allow",
                }));

            Assert.False(result.Success);
            Assert.Equal(StableErrorCodes.UnsupportedEntitlement, result.ErrorCode);
            Assert.Contains("Unsupported entitlements", result.Error, StringComparison.Ordinal);
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
