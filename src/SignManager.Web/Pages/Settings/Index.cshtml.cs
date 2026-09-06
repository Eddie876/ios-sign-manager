using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.ComponentModel.DataAnnotations;
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
        if (!ModelState.IsValid)
        {
            return Page();
        }

        try
        {
            await appService.SaveSettingsAsync(
                new WebSettings(
                    PublicBaseUrl: Input.PublicBaseUrl,
                    AppleIdMasked: Input.AppleIdMasked,
                    TeamId: Input.TeamId,
                    SessionStatus: Input.SessionStatus,
                    CertificateStatus: Input.CertificateStatus),
                cancellationToken);
        }
        catch (InvalidOperationException ex)
        {
            ModelState.AddModelError(string.Empty, ex.Message);
            return Page();
        }

        Message = "Settings saved.";
        return RedirectToPage();
    }

    public sealed class InputModel
    {
        [Required]
        [MaxLength(2048)]
        public string PublicBaseUrl { get; set; } = "https://ios.example.com";

        public string? AppleIdMasked { get; set; }

        public string? TeamId { get; set; }

        [Required]
        [MaxLength(64)]
        public string SessionStatus { get; set; } = "Auth Required";

        [Required]
        [MaxLength(64)]
        public string CertificateStatus { get; set; } = "Unknown";
    }
}
