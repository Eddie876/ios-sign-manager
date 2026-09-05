namespace SignManager.Core.Constants;

public static class StableErrorCodes
{
    public const string AuthRequired = "AUTH_REQUIRED";
    public const string AnisetteUnavailable = "ANISSETTE_UNAVAILABLE";
    public const string AppleLoginFailed = "APPLE_LOGIN_FAILED";
    public const string AppleTwoFactorRequired = "APPLE_2FA_REQUIRED";
    public const string AppleTwoFactorFailed = "APPLE_2FA_FAILED";
    public const string AppleSessionRejected = "APPLE_SESSION_REJECTED";

    public const string TeamNotFound = "TEAM_NOT_FOUND";
    public const string DeviceRegistrationFailed = "DEVICE_REGISTRATION_FAILED";
    public const string CertificateFailed = "CERTIFICATE_FAILED";
    public const string AppIdQuotaExceeded = "APP_ID_QUOTA_EXCEEDED";
    public const string AppIdCreateFailed = "APP_ID_CREATE_FAILED";
    public const string ProfileCreateFailed = "PROFILE_CREATE_FAILED";
    public const string ProfileNotFresh = "PROFILE_NOT_FRESH";

    public const string InvalidIpa = "INVALID_IPA";
    public const string UnsupportedEntitlement = "UNSUPPORTED_ENTITLEMENT";
    public const string ZipLimitExceeded = "ZIP_LIMIT_EXCEEDED";
    public const string ZipPathTraversal = "ZIP_PATH_TRAVERSAL";
    public const string ZsignFailed = "ZSIGN_FAILED";
    public const string SignedIpaValidationFailed = "SIGNED_IPA_VALIDATION_FAILED";

    public const string R2UploadFailed = "R2_UPLOAD_FAILED";
    public const string ManifestGenerationFailed = "MANIFEST_GENERATION_FAILED";
    public const string TelegramFailed = "TELEGRAM_FAILED";
}
