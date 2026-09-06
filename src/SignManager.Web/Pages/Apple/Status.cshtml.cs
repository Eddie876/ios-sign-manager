using Microsoft.AspNetCore.Mvc.RazorPages;
using SignManager.Web.Services;

namespace SignManager.Web.Pages.Apple;

public sealed class StatusModel(WebAppService appService) : PageModel
{
    public AppleAccountStatusViewModel Status { get; private set; } = new(
        AppleIdMasked: null,
        TeamId: null,
        SessionStatus: "Unknown",
        CertificateStatus: "Unknown",
        LoginCommand: string.Empty);

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        Status = await appService.GetAppleStatusAsync(cancellationToken);
    }
}
