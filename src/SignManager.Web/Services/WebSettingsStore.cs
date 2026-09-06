using SignManager.Infrastructure.Persistence;

namespace SignManager.Web.Services;

public sealed class WebSettingsStore
{
    private readonly JsonAtomicFileStore<WebSettings> _store = new();

    public async Task<WebSettings> LoadAsync(string path, CancellationToken cancellationToken)
    {
        var settings = await _store.ReadAsync(path, cancellationToken);
        return settings ?? WebSettings.Default;
    }

    public Task SaveAsync(string path, WebSettings settings, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(settings);
        return _store.WriteAsync(path, settings, cancellationToken);
    }
}
