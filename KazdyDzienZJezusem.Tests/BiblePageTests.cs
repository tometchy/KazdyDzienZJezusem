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

    [Fact]
    public async Task Copyright_notices_are_displayed_for_Ubg_and_Tnp()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        using var ubgResponse = await host.Client.GetAsync(
            "/Biblia/UBG/Ob/15",
            cancellationToken);
        using var tnpResponse = await host.Client.GetAsync(
            "/Biblia/TNP/Ob/15",
            cancellationToken);

        var ubgHtml = WebUtility.HtmlDecode(
            await ubgResponse.Content.ReadAsStringAsync(cancellationToken));
        var tnpHtml = WebUtility.HtmlDecode(
            await tnpResponse.Content.ReadAsStringAsync(cancellationToken));

        Assert.Contains("© 2017 Fundacja Wrota Nadziei", ubgHtml);
        Assert.Contains("© 2017–2024 Logos Media", ubgHtml);
        Assert.Contains("za zgodą wydawnictwa Świadome Chrześcijaństwo", tnpHtml);
    }

    [Fact]
    public async Task Fathers_reader_navigates_to_the_downloaded_Greek_text()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        using var indexResponse = await host.Client.GetAsync("/Ojcowie", cancellationToken);
        using var authorResponse = await host.Client.GetAsync("/Ojcowie/Ignatius", cancellationToken);
        using var chapterResponse = await host.Client.GetAsync(
            "/Ojcowie/Ignatius/Ef/1",
            cancellationToken);

        Assert.Equal(HttpStatusCode.OK, indexResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, authorResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, chapterResponse.StatusCode);

        var indexHtml = WebUtility.HtmlDecode(
            await indexResponse.Content.ReadAsStringAsync(cancellationToken));
        var authorHtml = WebUtility.HtmlDecode(
            await authorResponse.Content.ReadAsStringAsync(cancellationToken));
        var chapterHtml = WebUtility.HtmlDecode(
            await chapterResponse.Content.ReadAsStringAsync(cancellationToken));

        Assert.Contains("CC BY-SA 4.0", indexHtml);
        Assert.Equal(7, CountOccurrences(authorHtml, "class=\"book-tile\""));
        Assert.Contains("Πρὸς Ἐφεσίους", authorHtml);
        Assert.Contains("Ἀποδεξάμενος", chapterHtml);
    }

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

    private static int CountOccurrences(string text, string value) =>
        text.Split(value, StringSplitOptions.None).Length - 1;
}
