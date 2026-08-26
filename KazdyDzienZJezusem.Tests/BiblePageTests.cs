using System.Net;
using VerifyTests;

namespace KazdyDzienZJezusem.Tests;

public sealed class BiblePageTests(PageTestHost host) : IClassFixture<PageTestHost>
{
    [Fact]
    public Task Bible_index_matches_snapshot() =>
        VerifyPage("/Biblia");

    [Fact]
    public Task Chapter_page_matches_snapshot() =>
        VerifyPage("/Biblia/TNP/1Kor/1");

    private async Task VerifyPage(string path)
    {
        using var response = await host.Client.GetAsync(
            path,
            HttpCompletionOption.ResponseHeadersRead);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("text/html", response.Content.Headers.ContentType?.MediaType);

        var html = await response.Content.ReadAsStringAsync();

        await Verifier
            .Verify(target: html, extension: "html")
            .UseDirectory("Snapshots");
    }
}
