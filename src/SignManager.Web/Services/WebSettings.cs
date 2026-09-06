namespace SignManager.Web.Services;

public sealed record WebSettings(
    string PublicBaseUrl,
    string? AppleIdMasked,
    string? TeamId,
    string SessionStatus,
    string CertificateStatus)
{
    public static WebSettings Default { get; } = new(
        PublicBaseUrl: "https://ios.example.com",
        AppleIdMasked: null,
        TeamId: null,
        SessionStatus: "Auth Required",
        CertificateStatus: "Unknown");
}
