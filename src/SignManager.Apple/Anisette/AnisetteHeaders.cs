namespace SignManager.Apple.Anisette;

public sealed record AnisetteHeaders(
    string XAppleIMd,
    string XAppleIMdM,
    string XAppleIMdLu,
    string XMmeDeviceId,
    string XMmeClientInfo);
