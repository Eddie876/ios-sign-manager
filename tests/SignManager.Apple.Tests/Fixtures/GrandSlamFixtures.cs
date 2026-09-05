namespace SignManager.Apple.Tests.Fixtures;

internal static class GrandSlamFixtures
{
    public const string Success = """
    {
      "status": "ok",
      "adsId": "123456789",
      "gsToken": "gs-token-value"
    }
    """;

    public const string TwoFactorRequired = """
    {
      "status": "2fa_required"
    }
    """;

    public const string Failure = """
    {
      "status": "error",
      "errorCode": "APPLE_LOGIN_FAILED"
    }
    """;
}
