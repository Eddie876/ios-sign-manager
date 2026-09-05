using System.IO.Compression;
using System.Text;
using System.Xml.Linq;

namespace SignManager.Signing.Zsign;

public sealed class SignedIpaValidator
{
    public SignedIpaValidationResult Validate(SignedIpaValidationRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (!File.Exists(request.IpaPath))
        {
            return SignedIpaValidationResult.CreateFailure("Signed IPA output does not exist.");
        }

        using var archive = ZipFile.OpenRead(request.IpaPath);
        var infoPlistEntry = archive.Entries.FirstOrDefault(e => e.FullName.StartsWith("Payload/", StringComparison.Ordinal) && e.FullName.EndsWith(".app/Info.plist", StringComparison.Ordinal));
        if (infoPlistEntry is null)
        {
            return SignedIpaValidationResult.CreateFailure("Main app Info.plist not found.");
        }

        var mobileProvisionEntry = archive.Entries.FirstOrDefault(e => e.FullName.StartsWith("Payload/", StringComparison.Ordinal) && e.FullName.EndsWith(".app/embedded.mobileprovision", StringComparison.Ordinal));
        if (mobileProvisionEntry is null)
        {
            return SignedIpaValidationResult.CreateFailure("embedded.mobileprovision not found.");
        }

        var actualBundleId = ReadBundleId(infoPlistEntry);
        if (!string.Equals(actualBundleId, request.ExpectedBundleId, StringComparison.Ordinal))
        {
            return SignedIpaValidationResult.CreateFailure($"Bundle ID mismatch. Expected '{request.ExpectedBundleId}', got '{actualBundleId}'.");
        }

        var profile = ReadProfileMetadata(mobileProvisionEntry);
        if (!string.Equals(profile.Uuid, request.ExpectedProfileUuid, StringComparison.OrdinalIgnoreCase))
        {
            return SignedIpaValidationResult.CreateFailure($"Profile UUID mismatch. Expected '{request.ExpectedProfileUuid}', got '{profile.Uuid}'.");
        }

        if (profile.ExpirationDate != request.ExpectedProfileExpirationDate)
        {
            return SignedIpaValidationResult.CreateFailure("Profile expiration mismatch.");
        }

        return SignedIpaValidationResult.CreateSuccess(actualBundleId, profile.Uuid, profile.ExpirationDate);
    }

    private static string ReadBundleId(ZipArchiveEntry infoPlistEntry)
    {
        using var stream = infoPlistEntry.Open();
        using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
        var plist = XDocument.Parse(reader.ReadToEnd());

        var bundleId = ReadStringKey(plist, "CFBundleIdentifier");
        if (string.IsNullOrWhiteSpace(bundleId))
        {
            throw new InvalidOperationException("CFBundleIdentifier is missing from Info.plist.");
        }

        return bundleId;
    }

    private static ProfileMetadata ReadProfileMetadata(ZipArchiveEntry mobileProvisionEntry)
    {
        using var stream = mobileProvisionEntry.Open();
        using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
        var content = reader.ReadToEnd();

        var start = content.IndexOf("<?xml", StringComparison.Ordinal);
        var end = content.IndexOf("</plist>", StringComparison.Ordinal);
        if (start < 0 || end < 0)
        {
            throw new InvalidOperationException("embedded.mobileprovision plist payload not found.");
        }

        var xml = content[start..(end + "</plist>".Length)];
        var plist = XDocument.Parse(xml);

        var uuid = ReadStringKey(plist, "UUID");
        var expiration = ReadDateKey(plist, "ExpirationDate");
        return new ProfileMetadata(uuid, expiration);
    }

    private static string ReadStringKey(XDocument plist, string key)
    {
        var keyNode = plist.Descendants("key").FirstOrDefault(x => x.Value == key)
            ?? throw new InvalidOperationException($"Key '{key}' not found.");

        var valueNode = keyNode.ElementsAfterSelf().OfType<XElement>().FirstOrDefault()
            ?? throw new InvalidOperationException($"Key '{key}' has no value node.");

        if (valueNode.Name.LocalName != "string")
        {
            throw new InvalidOperationException($"Key '{key}' is not a string.");
        }

        return valueNode.Value;
    }

    private static DateTimeOffset ReadDateKey(XDocument plist, string key)
    {
        var keyNode = plist.Descendants("key").FirstOrDefault(x => x.Value == key)
            ?? throw new InvalidOperationException($"Key '{key}' not found.");

        var valueNode = keyNode.ElementsAfterSelf().OfType<XElement>().FirstOrDefault()
            ?? throw new InvalidOperationException($"Key '{key}' has no value node.");

        if (valueNode.Name.LocalName != "date")
        {
            throw new InvalidOperationException($"Key '{key}' is not a date.");
        }

        return DateTimeOffset.Parse(valueNode.Value, System.Globalization.CultureInfo.InvariantCulture);
    }

    private sealed record ProfileMetadata(string Uuid, DateTimeOffset ExpirationDate);
}

public sealed record SignedIpaValidationRequest(
    string IpaPath,
    string ExpectedBundleId,
    string ExpectedProfileUuid,
    DateTimeOffset ExpectedProfileExpirationDate);

public sealed record SignedIpaValidationResult(
    bool Success,
    string? Error,
    string? BundleId,
    string? ProfileUuid,
    DateTimeOffset? ProfileExpirationDate)
{
    public static SignedIpaValidationResult CreateFailure(string error) => new(false, error, null, null, null);

    public static SignedIpaValidationResult CreateSuccess(string bundleId, string profileUuid, DateTimeOffset profileExpirationDate)
        => new(true, null, bundleId, profileUuid, profileExpirationDate);
}
