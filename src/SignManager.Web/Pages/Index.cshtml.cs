using Microsoft.AspNetCore.Mvc.RazorPages;
using SignManager.Web.Services;

namespace SignManager.Web.Pages;

public sealed class IndexModel(WebAppService appService) : PageModel
{
    public DashboardViewModel Dashboard { get; private set; } = new(
        AppleSessionStatus: "Unknown",
        AppCount: 0,
        ReadyCount: 0,
        FailedCount: 0,
        AuthRequiredCount: 0,
        Apps: []);

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        Dashboard = await appService.GetDashboardAsync(cancellationToken);
    }
}
