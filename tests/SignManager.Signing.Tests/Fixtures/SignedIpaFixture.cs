using System.IO.Compression;
using System.Text;

namespace SignManager.Signing.Tests.Fixtures;

internal static class SignedIpaFixture
{
    public static string Create(
        string directory,
        string bundleId,
        string profileUuid,
        DateTimeOffset profileExpirationDate)
    {
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, "signed.ipa");

        using var archive = ZipFile.Open(path, ZipArchiveMode.Create);

        var infoPlist = archive.CreateEntry("Payload/App.app/Info.plist");
        using (var stream = infoPlist.Open())
        using (var writer = new StreamWriter(stream, Encoding.UTF8, leaveOpen: false))
        {
                        writer.Write(
                                "<?xml version=\"1.0\" encoding=\"UTF-8\"?>\n" +
                                "<!DOCTYPE plist PUBLIC \"-//Apple//DTD PLIST 1.0//EN\" \"http://www.apple.com/DTDs/PropertyList-1.0.dtd\">\n" +
                                "<plist version=\"1.0\">\n" +
                                "<dict>\n" +
                                "  <key>CFBundleIdentifier</key>\n" +
                                $"  <string>{bundleId}</string>\n" +
                                "</dict>\n" +
                                "</plist>\n");
        }

        var provision = archive.CreateEntry("Payload/App.app/embedded.mobileprovision");
        using (var stream = provision.Open())
        using (var writer = new StreamWriter(stream, Encoding.UTF8, leaveOpen: false))
        {
                        writer.Write(
                                "BINARYPREFIX\n" +
                                "<?xml version=\"1.0\" encoding=\"UTF-8\"?>\n" +
                                "<!DOCTYPE plist PUBLIC \"-//Apple//DTD PLIST 1.0//EN\" \"http://www.apple.com/DTDs/PropertyList-1.0.dtd\">\n" +
                                "<plist version=\"1.0\">\n" +
                                "<dict>\n" +
                                $"  <key>UUID</key><string>{profileUuid}</string>\n" +
                                $"  <key>ExpirationDate</key><date>{profileExpirationDate:yyyy-MM-ddTHH:mm:ssZ}</date>\n" +
                                "</dict>\n" +
                                "</plist>\n" +
                                "BINARYSUFFIX\n");
        }

        return path;
        }
    }
