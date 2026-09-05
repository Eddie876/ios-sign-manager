namespace SignManager.Apple.Tests.Fixtures;

internal static class DeveloperFixtures
{
    public const string Device = """
    {
      "deviceId": "device-1",
      "udid": "UDID-1",
      "name": "Eddie iPhone"
    }
    """;

    public const string Certificate = """
    {
      "certificateId": "cert-1",
      "serialNumber": "ABC123",
      "pem": "-----BEGIN CERTIFICATE-----\\nMIIB...\\n-----END CERTIFICATE-----",
      "expiresAt": "2026-09-13T00:00:00Z"
    }
    """;

    public const string AppId = """
    {
      "appIdId": "app-1",
      "bundleId": "com.eddie.sideload.qrscanner",
      "name": "QR Scanner"
    }
    """;

    public static string Profile(string mobileProvisionBase64) =>
        $$"""
        {
          "profileId": "profile-1",
          "uuid": "11111111-2222-3333-4444-555555555555",
          "creationDate": "2026-09-06T00:00:00Z",
          "expirationDate": "2026-09-13T00:00:00Z",
          "teamId": "TEAM123",
          "bundleId": "com.eddie.sideload.qrscanner",
          "mobileProvisionBase64": "{{mobileProvisionBase64}}"
        }
        """;
}
