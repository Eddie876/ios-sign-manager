using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using SignManager.Web.Services;

namespace SignManager.Web.Pages.Settings;

public sealed class IndexModel(WebAppService appService) : PageModel
{
    [BindProperty]
    public InputModel Input { get; set; } = new();

    [TempData]
    public string? Message { get; set; }

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        var settings = await appService.GetSettingsAsync(cancellationToken);
        Input = new InputModel
        {
            PublicBaseUrl = settings.PublicBaseUrl,
            AppleIdMasked = settings.AppleIdMasked,
            TeamId = settings.TeamId,
            SessionStatus = settings.SessionStatus,
            CertificateStatus = settings.CertificateStatus,
        };
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken)
    {
        await appService.SaveSettingsAsync(
            new WebSettings(
                PublicBaseUrl: Input.PublicBaseUrl,
                AppleIdMasked: Input.AppleIdMasked,
                TeamId: Input.TeamId,
                SessionStatus: Input.SessionStatus,
                CertificateStatus: Input.CertificateStatus),
            cancellationToken);

        Message = "Settings saved.";
        return RedirectToPage();
    }

    public sealed class InputModel
    {
        public string PublicBaseUrl { get; set; } = "https://ios.example.com";

        public string? AppleIdMasked { get; set; }

        public string? TeamId { get; set; }

        public string SessionStatus { get; set; } = "Auth Required";

        public string CertificateStatus { get; set; } = "Unknown";
    }
}
