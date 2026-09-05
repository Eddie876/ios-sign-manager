namespace SignManager.Apple.Auth;

public interface IAppleGrandSlamClient
{
    Task<AppleLoginResult> LoginAsync(AppleLoginRequest request, CancellationToken cancellationToken);

    Task<AppleLoginResult> SubmitTwoFactorAsync(AppleTwoFactorRequest request, CancellationToken cancellationToken);
}