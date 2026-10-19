using System.Net;
using KazdyDzienZJezusem.Services;
using Microsoft.Extensions.Options;

namespace KazdyDzienZJezusem.Tests;

public sealed class GitHubWriteAccessTests
{
    [Theory]
    [InlineData(200, "{\"permissions\":{\"push\":true}}", true)]
    [InlineData(200, "{\"permissions\":{\"push\":false}}", false)]
    [InlineData(200, "{}", false)]
    [InlineData(401, "{}", false)]
    [InlineData(403, "{}", false)]
    [InlineData(404, "{}", false)]
    public async Task RequiresExplicitWritePermission(int status, string json, bool allowed)
    {
        using var handler = new Handler((HttpStatusCode)status, json);
        using var client = new HttpClient(handler);
        var access = new GitHubWriteAccess(client, Options.Create(new TagEditingOptions()));
        if (allowed) await access.CheckAsync("test-only-token", TestContext.Current.CancellationToken);
        else Assert.Equal(403, (await Assert.ThrowsAsync<TagEditException>(() =>
            access.CheckAsync("test-only-token", TestContext.Current.CancellationToken))).StatusCode);
    }

    private sealed class Handler(HttpStatusCode status, string json) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Assert.Equal("https://api.github.com/repos/tometchy/KazdyDzienZJezusem", request.RequestUri!.AbsoluteUri);
            Assert.Equal("Bearer", request.Headers.Authorization!.Scheme);
            Assert.Equal("test-only-token", request.Headers.Authorization.Parameter);
            return Task.FromResult(new HttpResponseMessage(status) { Content = new StringContent(json) });
        }
    }
}
