using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.Extensions.Options;

namespace KazdyDzienZJezusem.Services;

public interface IRepositoryWriteAccess
{
    Task CheckAsync(string token, CancellationToken cancellationToken);
}

public sealed class GitHubWriteAccess(HttpClient client, IOptions<TagEditingOptions> options) : IRepositoryWriteAccess
{
    public async Task CheckAsync(string token, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get,
            $"https://api.github.com/repos/{options.Value.Repository}");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        request.Headers.UserAgent.ParseAdd("KazdyDzienZJezusem-TagEditor/1.0");
        request.Headers.Accept.ParseAdd("application/vnd.github+json");
        request.Headers.Add("X-GitHub-Api-Version", "2022-11-28");
        using var response = await client.SendAsync(request, cancellationToken);
        if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden or HttpStatusCode.NotFound)
            throw new TagEditException(403, "This login cannot edit the repository. Check its access and try again.");
        if (!response.IsSuccessStatusCode)
            throw new TagEditException(503, "GitHub is unavailable. Please try again later.");
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
        if (!json.RootElement.TryGetProperty("permissions", out var permissions)
            || !permissions.TryGetProperty("push", out var push) || push.ValueKind != JsonValueKind.True)
            throw new TagEditException(403, "This login cannot edit the repository.");
        // This is only a preflight. Token scopes and branch rules are enforced by the actual push.
    }
}
