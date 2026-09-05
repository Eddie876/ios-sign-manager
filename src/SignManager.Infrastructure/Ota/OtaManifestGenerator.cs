using System.Xml.Linq;

namespace SignManager.Infrastructure.Ota;

public sealed class OtaManifestGenerator
{
    public string GenerateXml(OtaManifestRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var plist = new XDocument(
            new XDeclaration("1.0", "UTF-8", null),
            new XDocumentType("plist", "-//Apple//DTD PLIST 1.0//EN", "http://www.apple.com/DTDs/PropertyList-1.0.dtd", null),
            new XElement("plist",
                new XAttribute("version", "1.0"),
                new XElement("dict",
                    new XElement("key", "items"),
                    new XElement("array",
                        new XElement("dict",
                            new XElement("key", "assets"),
                            new XElement("array",
                                new XElement("dict",
                                    new XElement("key", "kind"),
                                    new XElement("string", "software-package"),
                                    new XElement("key", "url"),
                                    new XElement("string", request.PackageUrl)
                                )
                            ),
                            new XElement("key", "metadata"),
                            new XElement("dict",
                                new XElement("key", "bundle-identifier"),
                                new XElement("string", request.BundleIdentifier),
                                new XElement("key", "bundle-version"),
                                new XElement("string", request.BundleVersion),
                                new XElement("key", "kind"),
                                new XElement("string", "software"),
                                new XElement("key", "title"),
                                new XElement("string", request.Title)
                            )
                        )
                    )
                )
            )
        );

        return plist.ToString(SaveOptions.DisableFormatting);
    }
}

public sealed record OtaManifestRequest(
    string PackageUrl,
    string BundleIdentifier,
    string BundleVersion,
    string Title);
