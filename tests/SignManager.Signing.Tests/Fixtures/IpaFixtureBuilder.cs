using System.IO.Compression;
using System.Text;

namespace SignManager.Signing.Tests.Fixtures;

internal static class IpaFixtureBuilder
{
    public static string CreateValidIpa(string rootDirectory)
    {
        Directory.CreateDirectory(rootDirectory);
        var path = Path.Combine(rootDirectory, "upload.ipa");

        using var archive = ZipFile.Open(path, ZipArchiveMode.Create);

        WriteTextEntry(archive, "Payload/Test.app/Info.plist", "<?xml version=\"1.0\" encoding=\"UTF-8\"?><plist version=\"1.0\"><dict><key>CFBundleDisplayName</key><string>Test App</string><key>CFBundleIdentifier</key><string>com.vendor.test</string><key>CFBundleShortVersionString</key><string>1.2.3</string><key>CFBundleVersion</key><string>100</string><key>MinimumOSVersion</key><string>15.0</string></dict></plist>");
        WriteTextEntry(archive, "Payload/Test.app/PlugIns/Share.appex/Info.plist", "<?xml version=\"1.0\" encoding=\"UTF-8\"?><plist version=\"1.0\"><dict/></plist>");
        WriteTextEntry(archive, "Payload/Test.app/Watch/WatchApp.app/dummy.txt", "watch");
        WriteTextEntry(archive, "Payload/Test.app/AppClips/Clip.app/dummy.txt", "clip");
        WriteTextEntry(archive, "Payload/Test.app/test.entitlements", "<?xml version=\"1.0\" encoding=\"UTF-8\"?><plist version=\"1.0\"><dict><key>application-identifier</key><string>TEAM.com.vendor.test</string></dict></plist>");

        return path;
    }

    public static string CreatePathTraversalIpa(string rootDirectory)
    {
        Directory.CreateDirectory(rootDirectory);
        var path = Path.Combine(rootDirectory, "traversal.ipa");

        using var archive = ZipFile.Open(path, ZipArchiveMode.Create);
        WriteTextEntry(archive, "../evil.txt", "evil");

        return path;
    }

    public static string CreateMultipleMainAppsIpa(string rootDirectory)
    {
        Directory.CreateDirectory(rootDirectory);
        var path = Path.Combine(rootDirectory, "multi.ipa");

        using var archive = ZipFile.Open(path, ZipArchiveMode.Create);
        WriteTextEntry(archive, "Payload/App1.app/Info.plist", "<?xml version=\"1.0\" encoding=\"UTF-8\"?><plist version=\"1.0\"><dict><key>CFBundleIdentifier</key><string>com.vendor.app1</string></dict></plist>");
        WriteTextEntry(archive, "Payload/App2.app/Info.plist", "<?xml version=\"1.0\" encoding=\"UTF-8\"?><plist version=\"1.0\"><dict><key>CFBundleIdentifier</key><string>com.vendor.app2</string></dict></plist>");

        return path;
    }

    public static string CreateAbsolutePathIpa(string rootDirectory)
    {
        Directory.CreateDirectory(rootDirectory);
        var path = Path.Combine(rootDirectory, "absolute.ipa");

        using var archive = ZipFile.Open(path, ZipArchiveMode.Create);
        WriteTextEntry(archive, "/absolute/evil.txt", "evil");

        return path;
    }

    private static void WriteTextEntry(ZipArchive archive, string path, string content)
    {
        var entry = archive.CreateEntry(path);
        using var stream = entry.Open();
        using var writer = new StreamWriter(stream, Encoding.UTF8, leaveOpen: false);
        writer.Write(content);
    }
}
