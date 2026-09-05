namespace SignManager.Apple.Auth;

public sealed record AppleSession(
    string AdsId,
    string GrandSlamToken,
    DateTimeOffset IssuedAt,
    DateTimeOffset? LastValidatedAt);