using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.ComponentModel.DataAnnotations;
using SignManager.Signing.Ipa;
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

        try
        {
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

        return Page();
    }

    public sealed class InputModel
    {
        [Required]
        [MaxLength(100)]
        public string Name { get; set; } = string.Empty;

        [Required]
        [RegularExpression("^[A-Za-z0-9.-]+$", ErrorMessage = "Effective Bundle ID can only contain letters, numbers, dot, and hyphen.")]
        [MaxLength(200)]
        public string EffectiveBundleId { get; set; } = string.Empty;

        [RegularExpression("^[a-z0-9-]*$", ErrorMessage = "Publish Slug can only contain lowercase letters, numbers, and hyphen.")]
        [MaxLength(100)]
        public string? PublishSlug { get; set; }

        public bool RemoveExtensions { get; set; } = true;

        public bool AutoSign { get; set; } = true;

        [Range(1, 24 * 30)]
        public int IntervalHours { get; set; } = 48;

        public IFormFile? IpaFile { get; set; }
    }
}
