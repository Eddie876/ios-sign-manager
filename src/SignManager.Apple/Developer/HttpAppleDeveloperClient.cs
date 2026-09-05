using System.Net.Http.Json;

namespace SignManager.Apple.Developer;

public sealed class HttpAppleDeveloperClient : IAppleDeveloperClient
{
    private readonly HttpClient _httpClient;

    public HttpAppleDeveloperClient(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<bool> ViewDeveloperAsync(CancellationToken cancellationToken)
    {
        using var response = await _httpClient.GetAsync("developer/view", cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            return false;
        }

        var payload = await response.Content.ReadFromJsonAsync<ViewDeveloperResponse>(cancellationToken);
        return payload?.Valid is true;
    }

    public async Task<IReadOnlyList<DeveloperTeam>> GetTeamsAsync(CancellationToken cancellationToken)
    {
        using var response = await _httpClient.GetAsync("developer/teams", cancellationToken);
        response.EnsureSuccessStatusCode();

        var payload = await response.Content.ReadFromJsonAsync<TeamsResponse>(cancellationToken)
            ?? throw new InvalidOperationException("Developer team response is empty.");

        return payload.Teams
            .Where(x => !string.IsNullOrWhiteSpace(x.TeamId) && !string.IsNullOrWhiteSpace(x.Name))
            .Select(x => new DeveloperTeam(x.TeamId!, x.Name!))
            .ToArray();
    }

    public async Task<RegisteredDevice> EnsureDeviceAsync(
        string teamId,
        string udid,
        string deviceName,
        CancellationToken cancellationToken)
    {
        using var response = await _httpClient.PostAsJsonAsync(
            "developer/devices/ensure",
            new EnsureDeviceRequest(teamId, udid, deviceName),
            cancellationToken);
        response.EnsureSuccessStatusCode();

        var payload = await response.Content.ReadFromJsonAsync<DeviceResponse>(cancellationToken)
            ?? throw new InvalidOperationException("Ensure device response is empty.");

        return new RegisteredDevice(payload.DeviceId, payload.Udid, payload.Name);
    }

    public async Task<DevelopmentCertificate> EnsureCertificateAsync(
        string teamId,
        string csrPem,
        CancellationToken cancellationToken)
    {
        using var response = await _httpClient.PostAsJsonAsync(
            "developer/certificates/ensure",
            new EnsureCertificateRequest(teamId, csrPem),
            cancellationToken);
        response.EnsureSuccessStatusCode();

        var payload = await response.Content.ReadFromJsonAsync<CertificateResponse>(cancellationToken)
            ?? throw new InvalidOperationException("Ensure certificate response is empty.");

        return new DevelopmentCertificate(
            payload.CertificateId,
            payload.SerialNumber,
            payload.Pem,
            payload.ExpiresAt);
    }

    public async Task<AppIdentifier> EnsureAppIdAsync(
        string teamId,
        string bundleId,
        CancellationToken cancellationToken)
    {
        using var response = await _httpClient.PostAsJsonAsync(
            "developer/appids/ensure",
            new EnsureAppIdRequest(teamId, bundleId),
            cancellationToken);
        response.EnsureSuccessStatusCode();

        var payload = await response.Content.ReadFromJsonAsync<AppIdResponse>(cancellationToken)
            ?? throw new InvalidOperationException("Ensure app id response is empty.");

        return new AppIdentifier(payload.AppIdId, payload.BundleId, payload.Name);
    }

    public async Task<ProvisioningProfile> CreateProvisioningProfileAsync(
        string teamId,
        string appIdId,
        string deviceId,
        string profileName,
        CancellationToken cancellationToken)
    {
        using var response = await _httpClient.PostAsJsonAsync(
            "developer/profiles/create",
            new CreateProvisioningProfileRequest(teamId, appIdId, deviceId, profileName),
            cancellationToken);
        response.EnsureSuccessStatusCode();

        var payload = await response.Content.ReadFromJsonAsync<ProfileResponse>(cancellationToken)
            ?? throw new InvalidOperationException("Create profile response is empty.");

        return new ProvisioningProfile(
            payload.ProfileId,
            payload.Uuid,
            payload.CreationDate,
            payload.ExpirationDate,
            payload.TeamId,
            payload.BundleId,
            payload.MobileProvisionBase64);
    }

    private sealed record ViewDeveloperResponse(bool Valid);
    private sealed record TeamsResponse(IReadOnlyList<TeamPayload> Teams);
    private sealed record TeamPayload(string? TeamId, string? Name);
    private sealed record EnsureDeviceRequest(string TeamId, string Udid, string DeviceName);
    private sealed record DeviceResponse(string DeviceId, string Udid, string Name);
    private sealed record EnsureCertificateRequest(string TeamId, string CsrPem);
    private sealed record CertificateResponse(string CertificateId, string SerialNumber, string Pem, DateTimeOffset ExpiresAt);
    private sealed record EnsureAppIdRequest(string TeamId, string BundleId);
    private sealed record AppIdResponse(string AppIdId, string BundleId, string Name);
    private sealed record CreateProvisioningProfileRequest(string TeamId, string AppIdId, string DeviceId, string ProfileName);
    private sealed record ProfileResponse(
        string ProfileId,
        string Uuid,
        DateTimeOffset CreationDate,
        DateTimeOffset ExpirationDate,
        string TeamId,
        string BundleId,
        string MobileProvisionBase64);
}
