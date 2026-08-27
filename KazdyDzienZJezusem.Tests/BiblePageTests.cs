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

    [Fact]
    public Task Ubg_chapter_page_matches_snapshot() =>
        VerifyPage("/Biblia/UBG/Ob/15");

    [Fact]
    public Task Tnp_chapter_page_matches_snapshot() =>
        VerifyPage("/Biblia/TNP/Ob/15");

    [Fact]
    public Task Tr_chapter_page_matches_snapshot() =>
        VerifyPage("/Biblia/TR/Ob/15");

    [Fact]
    public Task Kjv_chapter_page_matches_snapshot() =>
        VerifyPage("/Biblia/KJV/Ob/15");

    [Fact]
    public Task Verse_comparison_with_all_translations_matches_snapshot() =>
        VerifyPage("/Biblia/Werset/Ob/15/1?sourceTranslation=UBG");

    [Fact]
    public Task Verse_comparison_with_partial_translation_coverage_matches_snapshot() =>
        VerifyPage("/Biblia/Werset/Rdz/1/1?sourceTranslation=UBG");

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
