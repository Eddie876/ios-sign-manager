using System.Net.Http.Json;

namespace SignManager.Infrastructure.Notifications;

public sealed class TelegramExceptionNotifier(
    HttpClient httpClient,
    TelegramOptions options) : IExceptionNotifier
{
    public async Task NotifyAsync(ExceptionAlert alert, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(alert);

        if (!options.Enabled || string.IsNullOrWhiteSpace(options.BotToken) || string.IsNullOrWhiteSpace(options.ChatId))
        {
            return;
        }

        var metadataLines = alert.Metadata is null
            ? string.Empty
            : string.Join("\n", alert.Metadata.Select(x => $"- {x.Key}: {x.Value}"));

        var text = $"[SignManager] {alert.Title}\nErrorCode: {alert.ErrorCode}\nMessage: {alert.Message}";
        if (!string.IsNullOrWhiteSpace(metadataLines))
        {
            text += $"\n{metadataLines}";
        }

        var response = await httpClient.PostAsJsonAsync(
            $"https://api.telegram.org/bot{options.BotToken}/sendMessage",
            new
            {
                chat_id = options.ChatId,
                text,
                disable_web_page_preview = true,
            },
            cancellationToken);

        response.EnsureSuccessStatusCode();
    }
}

public sealed record TelegramOptions(
    bool Enabled = false,
    string? BotToken = null,
    string? ChatId = null);
