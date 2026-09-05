namespace SignManager.Apple.Tests.Fixtures;

internal static class ProvisioningProfileFixture
{
    public const string PlistXml = """
<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0">
<dict>
  <key>UUID</key><string>11111111-2222-3333-4444-555555555555</string>
  <key>CreationDate</key><date>2026-09-06T00:00:00Z</date>
  <key>ExpirationDate</key><date>2026-09-13T00:00:00Z</date>
  <key>TeamIdentifier</key>
  <array><string>TEAM123</string></array>
  <key>ProvisionedDevices</key>
  <array><string>UDID-1</string><string>UDID-2</string></array>
  <key>Entitlements</key>
  <dict>
    <key>application-identifier</key><string>TEAM123.com.eddie.sideload.qrscanner</string>
  </dict>
</dict>
</plist>
""";

    public static byte[] AsMobileProvisionBytes()
        => System.Text.Encoding.UTF8.GetBytes("BINARYPREFIX" + PlistXml + "BINARYSUFFIX");
}
