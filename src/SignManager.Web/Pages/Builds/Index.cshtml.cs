using Microsoft.AspNetCore.Mvc.RazorPages;
using SignManager.Web.Services;

namespace SignManager.Web.Pages.Builds;

public sealed class IndexModel(WebAppService appService) : PageModel
{
    public BuildHistoryViewModel History { get; private set; } = new([]);

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        History = await appService.GetBuildHistoryAsync(cancellationToken);
    }
}
