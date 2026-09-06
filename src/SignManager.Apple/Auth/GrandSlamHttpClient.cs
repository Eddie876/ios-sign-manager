using System.Net.Http.Json;
using SignManager.Apple.Anisette;
using SignManager.Core.Constants;

namespace SignManager.Apple.Auth;

public sealed class GrandSlamHttpClient(
    HttpClient httpClient,
    IAnisetteProvider anisetteProvider) : IAppleGrandSlamClient
{
    public async Task<AppleLoginResult> LoginAsync(AppleLoginRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        try
        {
            var payload = await SendGrandSlamRequestAsync(
                path: "grandslam/login",
                body: new LoginPayload(request.AppleId, request.Password),
                cancellationToken);

            return MapResponse(payload, StableErrorCodes.AppleLoginFailed);
        }
        catch (AnisetteUnavailableException)
        {
            return AppleLoginResult.Failed(StableErrorCodes.AnisetteUnavailable);
        }
        catch
        {
            return AppleLoginResult.Failed(StableErrorCodes.AppleLoginFailed);
        }
    }

    public async Task<AppleLoginResult> SubmitTwoFactorAsync(AppleTwoFactorRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        try
        {
            var payload = await SendGrandSlamRequestAsync(
                path: "grandslam/2fa",
                body: new TwoFactorPayload(request.AppleId, request.Code),
                cancellationToken);

            return MapResponse(payload, StableErrorCodes.AppleTwoFactorFailed);
        }
        catch (AnisetteUnavailableException)
        {
            return AppleLoginResult.Failed(StableErrorCodes.AnisetteUnavailable);
        }
        catch
        {
            return AppleLoginResult.Failed(StableErrorCodes.AppleTwoFactorFailed);
        }
    }

    private async Task<GrandSlamResponse> SendGrandSlamRequestAsync(
        string path,
        object body,
        CancellationToken cancellationToken)
    {
        var anisetteHeaders = await GetAnisetteHeadersAsync(cancellationToken);

        using var message = new HttpRequestMessage(HttpMethod.Post, path)
        {
            Content = JsonContent.Create(body),
        };

        message.Headers.TryAddWithoutValidation("X-Apple-I-MD", anisetteHeaders.XAppleIMd);
        message.Headers.TryAddWithoutValidation("X-Apple-I-MD-M", anisetteHeaders.XAppleIMdM);
        message.Headers.TryAddWithoutValidation("X-Apple-I-MD-LU", anisetteHeaders.XAppleIMdLu);
        message.Headers.TryAddWithoutValidation("X-Mme-Device-Id", anisetteHeaders.XMmeDeviceId);
        message.Headers.TryAddWithoutValidation("X-Mme-Client-Info", anisetteHeaders.XMmeClientInfo);

        using var response = await httpClient.SendAsync(message, cancellationToken);
        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<GrandSlamResponse>(cancellationToken)
            ?? throw new InvalidOperationException("GrandSlam response is empty.");
    }

    private async Task<AnisetteHeaders> GetAnisetteHeadersAsync(CancellationToken cancellationToken)
    {
        try
        {
            return await anisetteProvider.GetHeadersAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            throw new AnisetteUnavailableException("Failed to obtain anisette headers.", ex);
        }
    }

    private static AppleLoginResult MapResponse(GrandSlamResponse payload, string fallbackErrorCode)
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

        return AppleLoginResult.Failed(payload.ErrorCode ?? fallbackErrorCode);
    }

    private sealed class AnisetteUnavailableException(string message, Exception innerException)
        : InvalidOperationException(message, innerException);

    private sealed record LoginPayload(string AppleId, string Password);

    private sealed record TwoFactorPayload(string AppleId, string Code);

    private sealed record GrandSlamResponse(
        string Status,
        string? AdsId,
        string? GsToken,
        string? ErrorCode);
}
