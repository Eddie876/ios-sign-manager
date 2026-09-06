using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using SignManager.Web.Services;

namespace SignManager.Web.Pages.Apps;

public sealed class IndexModel(WebAppService appService) : PageModel
{
    public IReadOnlyList<AppListItemViewModel> Apps { get; private set; } = [];

    [TempData]
    public string? Message { get; set; }

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        Apps = await appService.GetAppsAsync(cancellationToken);
    }

    public async Task<IActionResult> OnPostSignNowAsync(string appId, CancellationToken cancellationToken)
    {
        await appService.RequestSignNowAsync(appId, cancellationToken);
        Message = $"Sign Now queued for {appId}.";
        return RedirectToPage();
    }
}
