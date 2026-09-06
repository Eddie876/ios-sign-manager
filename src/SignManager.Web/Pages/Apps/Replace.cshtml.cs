using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using SignManager.Signing.Ipa;
using SignManager.Web.Services;

namespace SignManager.Web.Pages.Apps;

public sealed class ReplaceModel(WebAppService appService) : PageModel
{
    [BindProperty(SupportsGet = true)]
    public string AppId { get; set; } = string.Empty;

    [BindProperty]
    public IFormFile? IpaFile { get; set; }

    public AppReplaceSummaryViewModel? App { get; private set; }

    public ReplaceAppResult? Result { get; private set; }

    public async Task<IActionResult> OnGetAsync(CancellationToken cancellationToken)
    {
        App = await appService.GetAppReplaceSummaryAsync(AppId, cancellationToken);
        if (App is null)
        {
            return NotFound();
        }

        return Page();
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken)
    {
        App = await appService.GetAppReplaceSummaryAsync(AppId, cancellationToken);
        if (App is null)
        {
            return NotFound();
        }

        if (IpaFile is null)
        {
            ModelState.AddModelError(nameof(IpaFile), "IPA file is required.");
            return Page();
        }

        try
        {
            Result = await appService.ReplaceAppSourceAsync(
                AppId,
                new UploadIpaRequest(IpaFile.Length, (target, ct) => IpaFile.CopyToAsync(target, ct)),
                cancellationToken);
        }
        catch (IpaPreflightException ex)
        {
            ModelState.AddModelError(string.Empty, $"IPA validation failed ({ex.ErrorCode}). {ex.Message}");
            return Page();
        }
        catch (InvalidOperationException ex)
        {
            ModelState.AddModelError(string.Empty, ex.Message);
            return Page();
        }

        App = await appService.GetAppReplaceSummaryAsync(AppId, cancellationToken);
        return Page();
    }
}
