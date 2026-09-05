using System.Net.Http.Json;

namespace SignManager.Apple.Anisette;

public sealed class HttpAnisetteProvider(HttpClient httpClient, string endpointPath = "headers") : IAnisetteProvider
{
    private static readonly string[] RequiredKeys =
    [
        "X-Apple-I-MD",
        "X-Apple-I-MD-M",
        "X-Apple-I-MD-LU",
        "X-Mme-Device-Id",
        "X-Mme-Client-Info",
    ];

    public async Task<AnisetteHeaders> GetHeadersAsync(CancellationToken cancellationToken)
    {
        for (var attempt = 1; attempt <= 3; attempt++)
        {
            try
            {
                using var response = await httpClient.GetAsync(endpointPath, cancellationToken);
                response.EnsureSuccessStatusCode();

                var payload = await response.Content
                    .ReadFromJsonAsync<Dictionary<string, string>>(cancellationToken)
                    ?? throw new InvalidOperationException("Anisette response body is empty.");

                ValidateRequiredHeaders(payload);
                return new AnisetteHeaders(
                    payload["X-Apple-I-MD"],
                    payload["X-Apple-I-MD-M"],
                    payload["X-Apple-I-MD-LU"],
                    payload["X-Mme-Device-Id"],
                    payload["X-Mme-Client-Info"]);
            }
            catch when (attempt < 3)
            {
                await Task.Delay(TimeSpan.FromMilliseconds(200 * attempt), cancellationToken);
            }
        }

        throw new InvalidOperationException("Failed to obtain anisette headers after retries.");
    }

    private static void ValidateRequiredHeaders(IReadOnlyDictionary<string, string> payload)
    {
        foreach (var key in RequiredKeys)
        {
            if (!payload.TryGetValue(key, out var value) || string.IsNullOrWhiteSpace(value))
            {
                throw new InvalidOperationException($"Required anisette header '{key}' is missing.");
            }
        }
    }
}
