using System.Net.Http.Json;
using SignManager.Core.Constants;

namespace SignManager.Apple.Auth;

public sealed class GrandSlamHttpClient(HttpClient httpClient) : IAppleGrandSlamClient
{
    public async Task<AppleLoginResult> LoginAsync(AppleLoginRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        using var response = await httpClient.PostAsJsonAsync(
            "grandslam/login",
            new LoginPayload(request.AppleId, request.Password),
            cancellationToken);

        response.EnsureSuccessStatusCode();

        var payload = await response.Content.ReadFromJsonAsync<GrandSlamResponse>(cancellationToken)
            ?? throw new InvalidOperationException("GrandSlam login response is empty.");

        return MapResponse(payload);
    }

    public async Task<AppleLoginResult> SubmitTwoFactorAsync(AppleTwoFactorRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        using var response = await httpClient.PostAsJsonAsync(
            "grandslam/2fa",
            new TwoFactorPayload(request.AppleId, request.Code),
            cancellationToken);

        response.EnsureSuccessStatusCode();

        var payload = await response.Content.ReadFromJsonAsync<GrandSlamResponse>(cancellationToken)
            ?? throw new InvalidOperationException("GrandSlam 2FA response is empty.");

        return MapResponse(payload);
    }

    private static AppleLoginResult MapResponse(GrandSlamResponse payload)
    {
        if (string.Equals(payload.Status, "2fa_required", StringComparison.OrdinalIgnoreCase))
        {
            return AppleLoginResult.TwoFactorRequired();
        }

        if (string.Equals(payload.Status, "ok", StringComparison.OrdinalIgnoreCase))
        {
            if (string.IsNullOrWhiteSpace(payload.AdsId) || string.IsNullOrWhiteSpace(payload.GsToken))
            {
                return AppleLoginResult.Failed(StableErrorCodes.AppleSessionRejected);
            }

            return AppleLoginResult.Succeeded(new AppleSession(
                payload.AdsId,
                payload.GsToken,
                DateTimeOffset.UtcNow,
                LastValidatedAt: null));
        }

        return AppleLoginResult.Failed(payload.ErrorCode ?? StableErrorCodes.AppleLoginFailed);
    }

    private sealed record LoginPayload(string AppleId, string Password);

    private sealed record TwoFactorPayload(string AppleId, string Code);

    private sealed record GrandSlamResponse(
        string Status,
        string? AdsId,
        string? GsToken,
        string? ErrorCode);
}
