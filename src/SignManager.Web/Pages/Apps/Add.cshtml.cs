using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using SignManager.Web.Services;

namespace SignManager.Web.Pages.Apps;

public sealed class AddModel(WebAppService appService) : PageModel
{
    [BindProperty]
    public InputModel Input { get; set; } = new();

    public AddAppResult? Result { get; private set; }

    public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken)
    {
        if (Input.IpaFile is null)
        {
            ModelState.AddModelError(nameof(Input.IpaFile), "IPA file is required.");
        }

        if (!ModelState.IsValid)
        {
            return Page();
        }

        Result = await appService.AddAppAsync(
            new AddAppRequest(
                Name: Input.Name,
                EffectiveBundleId: Input.EffectiveBundleId,
                RemoveExtensions: Input.RemoveExtensions,
                AutoSign: Input.AutoSign,
                IntervalHours: Input.IntervalHours,
                PublishSlug: Input.PublishSlug,
                Upload: new UploadIpaRequest(
                    Input.IpaFile!.Length,
                    (target, ct) => Input.IpaFile.CopyToAsync(target, ct))),
            cancellationToken);

        return Page();
    }

    public sealed class InputModel
    {
        public string Name { get; set; } = string.Empty;

        public string EffectiveBundleId { get; set; } = string.Empty;

        public string? PublishSlug { get; set; }

        public bool RemoveExtensions { get; set; } = true;

        public bool AutoSign { get; set; } = true;

        public int IntervalHours { get; set; } = 48;

        public IFormFile? IpaFile { get; set; }
    }
}
