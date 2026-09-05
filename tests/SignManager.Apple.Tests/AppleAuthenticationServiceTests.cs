using SignManager.Apple.Auth;
using SignManager.Apple.Developer;
using SignManager.Core.Constants;

namespace SignManager.Apple.Tests;

public class AppleAuthenticationServiceTests
{
    [Fact]
    public async Task RestoreSession_ShouldReturnAuthRequired_WhenNoSession()
    {
        var store = new InMemorySessionStore();
        var service = new AppleAuthenticationService(
            new FakeGrandSlamClient(),
            new FakeDeveloperClient(withTeam: true),
            store);

        var result = await service.RestoreSessionAsync(CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal(StableErrorCodes.AuthRequired, result.ErrorCode);
    }

    [Fact]
    public async Task Login_ShouldSaveSession_WhenSuccessful()
    {
        var store = new InMemorySessionStore();
        var service = new AppleAuthenticationService(
            new FakeGrandSlamClient(),
            new FakeDeveloperClient(withTeam: true),
            store);

        var result = await service.LoginAsync("a@b.com", "pw", null, CancellationToken.None);

        Assert.True(result.Success);
        Assert.NotNull(store.Current);
    }

    [Fact]
    public async Task Login_ShouldRequireTwoFactor_WhenPromptedAndMissingCode()
    {
        var service = new AppleAuthenticationService(
            new FakeGrandSlamClient(require2Fa: true),
            new FakeDeveloperClient(withTeam: true),
            new InMemorySessionStore());

        var result = await service.LoginAsync("a@b.com", "pw", null, CancellationToken.None);
        Assert.False(result.Success);
        Assert.Equal(StableErrorCodes.AppleTwoFactorRequired, result.ErrorCode);
    }

    private sealed class InMemorySessionStore : IAppleSessionStore
    {
        public AppleSession? Current { get; private set; }

        public Task SaveAsync(AppleSession session, CancellationToken cancellationToken)
        {
            Current = session;
            return Task.CompletedTask;
        }

        public Task<AppleSession?> LoadAsync(CancellationToken cancellationToken)
            => Task.FromResult(Current);

        public Task ClearAsync(CancellationToken cancellationToken)
        {
            Current = null;
            return Task.CompletedTask;
        }
    }

    private sealed class FakeGrandSlamClient(bool require2Fa = false) : IAppleGrandSlamClient
    {
        public Task<AppleLoginResult> LoginAsync(AppleLoginRequest request, CancellationToken cancellationToken)
        {
            if (require2Fa)
            {
                return Task.FromResult(AppleLoginResult.TwoFactorRequired());
            }

            var session = new AppleSession("adsid", "token", DateTimeOffset.UtcNow, null);
            return Task.FromResult(AppleLoginResult.Succeeded(session));
        }

        public Task<AppleLoginResult> SubmitTwoFactorAsync(AppleTwoFactorRequest request, CancellationToken cancellationToken)
        {
            var session = new AppleSession("adsid", "token", DateTimeOffset.UtcNow, null);
            return Task.FromResult(AppleLoginResult.Succeeded(session));
        }
    }

    private sealed class FakeDeveloperClient(bool withTeam) : IAppleDeveloperClient
    {
        public Task<IReadOnlyList<DeveloperTeam>> GetTeamsAsync(CancellationToken cancellationToken)
        {
            IReadOnlyList<DeveloperTeam> teams = withTeam
                ? [new DeveloperTeam("TEAM", "Personal Team")]
                : [];

            return Task.FromResult(teams);
        }
    }
}