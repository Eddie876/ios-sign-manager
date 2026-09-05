namespace SignManager.Infrastructure.Ota;

public static class ItmsServicesUrlBuilder
{
    public static string BuildInstallUrl(string manifestUrl)
    {
        if (string.IsNullOrWhiteSpace(manifestUrl))
        {
            throw new ArgumentException("Manifest URL is required.", nameof(manifestUrl));
        }

        var encodedManifestUrl = Uri.EscapeDataString(manifestUrl);
        return $"itms-services://?action=download-manifest&url={encodedManifestUrl}";
    }
}
