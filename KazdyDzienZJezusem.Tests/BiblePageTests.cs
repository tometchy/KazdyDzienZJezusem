using System.Net;
using System.Text.RegularExpressions;
using KazdyDzienZJezusem.Services;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
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

    [Theory]
    [InlineData("/Biblia/UBG", "/Biblia/KJV")]
    [InlineData("/Biblia/UBG/Ob", "/Biblia/KJV/Ob")]
    [InlineData("/Biblia/UBG/Ob/15", "/Biblia/KJV/Ob/15")]
    [InlineData("/Biblia/Werset/Ob/15/1?sourceTranslation=UBG",
        "/Biblia/Werset/Ob/15/1?sourceTranslation=KJV")]
    public async Task Translation_breadcrumb_keeps_the_current_navigation_level(
        string currentPath,
        string expectedDestination)
    {
        var html = await GetPageHtml(host.Client, currentPath);
        var breadcrumb = GetBreadcrumb(html);

        Assert.Contains("aria-label=\"Bible translation\"", breadcrumb);
        Assert.Contains($"href=\"{expectedDestination}\"", breadcrumb);
        Assert.Matches("aria-label=\"Bible translation\"[^>]*>UBG</button>", breadcrumb);

        // A rendered option must lead to a real page, including the same book/chapter.
        await GetPageHtml(host.Client, expectedDestination);
    }

    [Theory]
    [InlineData("/Biblia/UBG/Rdz")]
    [InlineData("/Biblia/UBG/Rdz/1")]
    [InlineData("/Biblia/Werset/Rdz/1/1?sourceTranslation=UBG")]
    public async Task Translations_without_the_current_book_are_disabled(string path)
    {
        var breadcrumb = GetBreadcrumb(await GetPageHtml(host.Client, path));

        AssertUnavailableTranslation(breadcrumb, "TNP");
        AssertUnavailableTranslation(breadcrumb, "TR");
    }

    [Fact]
    public async Task Missing_chapter_is_disabled_even_when_the_translation_has_the_book()
    {
        await using var factory = new StubbedWebApplicationFactory().WithWebHostBuilder(builder =>
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<IBibleFileSystem>();
                services.AddSingleton<IBibleFileSystem, MissingChapterBibleFileSystem>();
            }));
        using var client = factory.CreateClient();

        var bookBreadcrumb = GetBreadcrumb(await GetPageHtml(client, "/Biblia/UBG/Ob"));
        Assert.Contains("href=\"/Biblia/KJV/Ob\"", bookBreadcrumb);

        var chapterBreadcrumb = GetBreadcrumb(await GetPageHtml(client, "/Biblia/UBG/Ob/15"));
        AssertUnavailableTranslation(chapterBreadcrumb, "KJV");
        Assert.Contains("href=\"/Biblia/TNP/Ob/15\"", chapterBreadcrumb);

        var verseBreadcrumb = GetBreadcrumb(await GetPageHtml(
            client, "/Biblia/Werset/Ob/15/1?sourceTranslation=UBG"));
        AssertUnavailableTranslation(verseBreadcrumb, "KJV");
    }

    [Fact]
    public async Task Comparison_breadcrumb_uses_the_selected_translation_for_return_navigation()
    {
        var html = await GetPageHtml(host.Client, "/Biblia/Werset/Ob/15/1?sourceTranslation=KJV");
        var breadcrumb = GetBreadcrumb(html);

        Assert.Matches("aria-label=\"Bible translation\"[^>]*>KJV</button>", breadcrumb);
        Assert.Contains("href=\"/Biblia/KJV/Ob\"", breadcrumb);
        Assert.Contains("href=\"/Biblia/KJV/Ob/15\"", breadcrumb);
        Assert.Contains("href=\"/Biblia/KJV/Ob/15#1\"", html);
        Assert.Contains("href=\"/Biblia/Werset/Ob/15/1?sourceTranslation=TNP\"", breadcrumb);
    }

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

    private static async Task<string> GetPageHtml(HttpClient client, string path)
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        using var response = await client.GetAsync(path, cancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync(cancellationToken));
    }

    private static string GetBreadcrumb(string html)
    {
        var match = Regex.Match(html, "<nav aria-label=\"breadcrumb\">.*?</nav>", RegexOptions.Singleline);
        Assert.True(match.Success, "The page must have Bible breadcrumb navigation.");
        return match.Value;
    }

    private static void AssertUnavailableTranslation(string breadcrumb, string code)
    {
        Assert.Matches(
            $"<span[^>]*class=\"dropdown-item disabled\"[^>]*aria-disabled=\"true\"[^>]*>\\s*{code} —",
            breadcrumb);
        Assert.DoesNotMatch($"<a[^>]*>\\s*{code} —", breadcrumb);
    }

    private sealed class MissingChapterBibleFileSystem : IBibleFileSystem
    {
        private readonly StubBibleFileSystem _inner = new();

        public bool DirectoryExists(string path) => _inner.DirectoryExists(path);

        public IEnumerable<string> EnumerateFiles(string path, string searchPattern) =>
            _inner.EnumerateFiles(path, searchPattern).Where(file => !IsMissingChapter(file));

        public bool FileExists(string path) => !IsMissingChapter(path) && _inner.FileExists(path);

        public IEnumerable<string> ReadLines(string path) => _inner.ReadLines(path);

        private static bool IsMissingChapter(string path) =>
            path.Replace('\\', '/').EndsWith("/KJV/Ob/15.yml", StringComparison.Ordinal);
    }
}
