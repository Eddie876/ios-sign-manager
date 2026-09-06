namespace SignManager.Apple.Tests.Fixtures;

internal static class AnisetteFixtures
{
    public static IReadOnlyDictionary<string, string> ValidHeaders { get; } =
        new Dictionary<string, string>
        {
            ["X-Apple-I-MD"] = "md",
            ["X-Apple-I-MD-M"] = "mdm",
            ["X-Apple-I-MD-LU"] = "mdlu",
            ["X-Mme-Device-Id"] = "device",
            ["X-Mme-Client-Info"] = "client",
        };

    public static IReadOnlyDictionary<string, string> MissingRequiredHeaders { get; } =
        new Dictionary<string, string>
        {
            ["X-Apple-I-MD"] = "md",
        };
}
