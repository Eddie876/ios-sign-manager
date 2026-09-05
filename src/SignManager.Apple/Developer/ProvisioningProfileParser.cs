using System.Xml.Linq;

namespace SignManager.Apple.Developer;

public sealed class ProvisioningProfileParser
{
    public ParsedProvisioningProfile Parse(byte[] mobileProvisionBytes)
    {
        ArgumentNullException.ThrowIfNull(mobileProvisionBytes);

        var xml = ExtractPlistXml(mobileProvisionBytes);
        var doc = XDocument.Parse(xml);

        var uuid = GetStringValue(doc, "UUID");
        var creationDate = GetDateValue(doc, "CreationDate");
        var expirationDate = GetDateValue(doc, "ExpirationDate");
        var teamId = GetFirstArrayString(doc, "TeamIdentifier");
        var applicationIdentifier = GetNestedString(doc, "Entitlements", "application-identifier");
        var bundleId = applicationIdentifier.Contains('.')
            ? applicationIdentifier[(applicationIdentifier.IndexOf('.') + 1)..]
            : applicationIdentifier;
        var devices = GetArrayValues(doc, "ProvisionedDevices");

        return new ParsedProvisioningProfile(
            uuid,
            creationDate,
            expirationDate,
            teamId,
            bundleId,
            devices);
    }

    private static string ExtractPlistXml(byte[] bytes)
    {
        var content = System.Text.Encoding.UTF8.GetString(bytes);
        var start = content.IndexOf("<?xml", StringComparison.Ordinal);
        var end = content.IndexOf("</plist>", StringComparison.Ordinal);

        if (start < 0 || end < 0)
        {
            throw new InvalidOperationException("Provisioning profile plist payload not found.");
        }

        return content[start..(end + "</plist>".Length)];
    }

    private static string GetStringValue(XDocument doc, string key)
    {
        var element = GetValueElementAfterKey(doc, key);
        if (element.Name.LocalName != "string")
        {
            throw new InvalidOperationException($"Key '{key}' must be a string.");
        }

        return element.Value;
    }

    private static DateTimeOffset GetDateValue(XDocument doc, string key)
    {
        var element = GetValueElementAfterKey(doc, key);
        if (element.Name.LocalName != "date")
        {
            throw new InvalidOperationException($"Key '{key}' must be a date.");
        }

        return DateTimeOffset.Parse(element.Value, System.Globalization.CultureInfo.InvariantCulture);
    }

    private static string GetFirstArrayString(XDocument doc, string key)
    {
        var arrayElement = GetValueElementAfterKey(doc, key);
        if (arrayElement.Name.LocalName != "array")
        {
            throw new InvalidOperationException($"Key '{key}' must be an array.");
        }

        return arrayElement.Elements("string").FirstOrDefault()?.Value
            ?? throw new InvalidOperationException($"Key '{key}' array is empty.");
    }

    private static IReadOnlyList<string> GetArrayValues(XDocument doc, string key)
    {
        var arrayElement = GetValueElementAfterKey(doc, key);
        if (arrayElement.Name.LocalName != "array")
        {
            throw new InvalidOperationException($"Key '{key}' must be an array.");
        }

        return arrayElement.Elements("string").Select(x => x.Value).ToArray();
    }

    private static string GetNestedString(XDocument doc, string dictKey, string nestedKey)
    {
        var dictElement = GetValueElementAfterKey(doc, dictKey);
        if (dictElement.Name.LocalName != "dict")
        {
            throw new InvalidOperationException($"Key '{dictKey}' must be a dict.");
        }

        var keyNode = dictElement
            .Elements("key")
            .FirstOrDefault(x => string.Equals(x.Value, nestedKey, StringComparison.Ordinal))
            ?? throw new InvalidOperationException($"Nested key '{nestedKey}' not found in '{dictKey}'.");

        var valueElement = keyNode.ElementsAfterSelf().OfType<XElement>().FirstOrDefault()
            ?? throw new InvalidOperationException($"Nested key '{nestedKey}' has no value.");

        if (valueElement.Name.LocalName != "string")
        {
            throw new InvalidOperationException($"Nested key '{nestedKey}' must be a string.");
        }

        return valueElement.Value;
    }

    private static XElement GetValueElementAfterKey(XDocument doc, string key)
    {
        var keyNode = doc
            .Descendants("key")
            .FirstOrDefault(x => string.Equals(x.Value, key, StringComparison.Ordinal))
            ?? throw new InvalidOperationException($"Key '{key}' not found.");

        return keyNode.ElementsAfterSelf().OfType<XElement>().FirstOrDefault()
            ?? throw new InvalidOperationException($"Key '{key}' has no value.");
    }
}

public sealed record ParsedProvisioningProfile(
    string Uuid,
    DateTimeOffset CreationDate,
    DateTimeOffset ExpirationDate,
    string TeamId,
    string BundleId,
    IReadOnlyList<string> DeviceUdids);
