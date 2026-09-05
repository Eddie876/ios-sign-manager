using SignManager.Core.Constants;
using SignManager.Apple.Developer;

namespace SignManager.Apple.Auth;

public sealed class AppleAuthenticationService(
    IAppleGrandSlamClient grandSlamClient,
    IAppleDeveloperClient developerClient,
    IAppleSessionStore sessionStore)
{
    public async Task<(bool Success, string? ErrorCode)> LoginAsync(
        string appleId,
        string password,
        string? twoFactorCode,
        CancellationToken cancellationToken)
    {
        var result = await grandSlamClient.LoginAsync(
            new AppleLoginRequest(appleId, password, twoFactorCode),
            cancellationToken);

        if (result.RequiresTwoFactor)
        {
            if (string.IsNullOrWhiteSpace(twoFactorCode))
            {
                return (false, StableErrorCodes.AppleTwoFactorRequired);
            }

            result = await grandSlamClient.SubmitTwoFactorAsync(
                new AppleTwoFactorRequest(appleId, twoFactorCode),
                cancellationToken);
        }

        if (!result.Success || result.Session is null)
        {
            return (false, result.ErrorCode ?? StableErrorCodes.AppleLoginFailed);
        }

        var valid = await ValidateDeveloperSessionAsync(cancellationToken);
        if (!valid)
        {
            return (false, StableErrorCodes.AppleSessionRejected);
        }

        await sessionStore.SaveAsync(result.Session, cancellationToken);
        return (true, null);
    }

    public async Task<(bool Success, AppleSession? Session, string? ErrorCode)> RestoreSessionAsync(CancellationToken cancellationToken)
    {
        var session = await sessionStore.LoadAsync(cancellationToken);
        if (session is null)
        {
            return (false, null, StableErrorCodes.AuthRequired);
        }

        var valid = await ValidateDeveloperSessionAsync(cancellationToken);
        if (!valid)
        {
            await sessionStore.ClearAsync(cancellationToken);
            return (false, null, StableErrorCodes.AuthRequired);
        }

        return (true, session, null);
    }

    private async Task<bool> ValidateDeveloperSessionAsync(CancellationToken cancellationToken)
    {
        return await developerClient.ViewDeveloperAsync(cancellationToken);
    }
}