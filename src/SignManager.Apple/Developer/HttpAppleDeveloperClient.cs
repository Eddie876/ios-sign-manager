using System.Net.Http.Json;

namespace SignManager.Apple.Developer;

public sealed class HttpAppleDeveloperClient(HttpClient httpClient) : IAppleDeveloperClient
{
    public async Task<bool> ViewDeveloperAsync(CancellationToken cancellationToken)
    {
        using var response = await httpClient.GetAsync("developer/view", cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            return false;
        }

        var payload = await response.Content.ReadFromJsonAsync<ViewDeveloperResponse>(cancellationToken);
        return payload?.Valid is true;
    }

    public async Task<IReadOnlyList<DeveloperTeam>> GetTeamsAsync(CancellationToken cancellationToken)
    {
        using var response = await httpClient.GetAsync("developer/teams", cancellationToken);
        response.EnsureSuccessStatusCode();

        var payload = await response.Content.ReadFromJsonAsync<TeamsResponse>(cancellationToken)
            ?? throw new InvalidOperationException("Developer team response is empty.");

        return payload.Teams
            .Where(x => !string.IsNullOrWhiteSpace(x.TeamId) && !string.IsNullOrWhiteSpace(x.Name))
            .Select(x => new DeveloperTeam(x.TeamId!, x.Name!))
            .ToArray();
    }

    private sealed record ViewDeveloperResponse(bool Valid);

    private sealed record TeamsResponse(IReadOnlyList<TeamPayload> Teams);

    private sealed record TeamPayload(string? TeamId, string? Name);
}
