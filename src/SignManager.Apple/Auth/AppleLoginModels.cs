namespace SignManager.Apple.Auth;

public sealed record AppleLoginRequest(
    string AppleId,
    string Password,
    string? TwoFactorCode);

public sealed record AppleTwoFactorRequest(
    string AppleId,
    string Code);

public sealed record AppleLoginResult(
    bool Success,
    bool RequiresTwoFactor,
    AppleSession? Session,
    string? ErrorCode)
{
    public static AppleLoginResult Succeeded(AppleSession session) =>
        new(true, false, session, null);

    public static AppleLoginResult TwoFactorRequired() =>
        new(false, true, null, null);

    public static AppleLoginResult Failed(string errorCode) =>
        new(false, false, null, errorCode);
}