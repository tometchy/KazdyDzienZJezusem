using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using KazdyDzienZJezusem.Models;
using KazdyDzienZJezusem.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

namespace KazdyDzienZJezusem.Tests;

public sealed class TagEditingTests
{
    [Theory]
    [InlineData("../escape")]
    [InlineData("a/b")]
    [InlineData("a\nb")]
    [InlineData("<script>")]
    [InlineData(".git")]
    public void RejectsUnsafeTagNames(string name) =>
        Assert.Throws<TagEditException>(() => TagFileEditor.ValidateName(name));

    [Fact]
    public void AddingExistingRangeDoesNotChangeDescriptionOrDuplicateFragment()
    {
        const string original = "---\r\nfragments:\r\n  - Mt1,18-25\r\n---\r\n\r\n# Title\r\nDescription\r\n";
        Assert.Equal(original, TagFileEditor.AddFragment(original, "tag", "Mt1,19"));
        var updated = TagFileEditor.AddFragment(original, "tag", "Ignatius/Ef0,1");
        Assert.Contains("  - Ignatius/Ef0,1\n---", updated);
        Assert.EndsWith("# Title\nDescription\n", updated);
        Assert.Equal(updated, TagFileEditor.AddFragment(updated, "tag", "Ignatius/Ef0,1"));
        Assert.Throws<TagEditException>(() => TagFileEditor.AddFragment("", "tag", "Mt1,1"));
    }

    [Fact]
    public async Task ProductionCookiesAreSecureBehindHttpTunnel()
    {
        await using var factory = new EditorFactory { EnvironmentName = "Production" };
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        using var response = await client.GetAsync("/login", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.All(response.Headers.GetValues("Set-Cookie"), cookie =>
            Assert.Contains("secure", cookie.ToLowerInvariant()));
    }

    [Fact]
    public async Task LoginIsOpaqueCsrfProtectedAndLogoutRevokesSession()
    {
        await using var factory = new EditorFactory();
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        using var rejected = await client.PostAsync("/login", new FormUrlEncodedContent(new Dictionary<string, string> { ["login"] = "secret" }), TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.BadRequest, rejected.StatusCode);
        var response = await Login(client);
        var cookies = string.Join(";", response.Headers.GetValues("Set-Cookie"));
        Assert.Contains("httponly", cookies.ToLowerInvariant());
        Assert.Contains("samesite=strict", cookies.ToLowerInvariant());
        Assert.DoesNotContain("test-secret", cookies);
        var state = await client.GetStringAsync("/api/editor/session", TestContext.Current.CancellationToken);
        Assert.DoesNotContain("test-secret", state);
        Assert.True(JsonDocument.Parse(state).RootElement.GetProperty("loggedIn").GetBoolean());
        Assert.Equal(0, factory.Access.Checks); // Presence is not authorization.
        var csrf = JsonDocument.Parse(state).RootElement.GetProperty("csrfToken").GetString()!;
        using var logout = await client.PostAsync("/logout", new FormUrlEncodedContent(new Dictionary<string, string>
            { ["__RequestVerificationToken"] = csrf }), TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Redirect, logout.StatusCode);
        var loggedOut = await client.GetFromJsonAsync<JsonElement>("/api/editor/session", TestContext.Current.CancellationToken);
        Assert.False(loggedOut.GetProperty("loggedIn").GetBoolean());
    }

    [Fact]
    public async Task WritesRequireSessionCsrfPermissionAndValidFragmentBeforeChangingRepository()
    {
        await using var factory = new EditorFactory();
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        Assert.Equal(HttpStatusCode.Unauthorized, (await Add(client, "tag", "Ob15,1")).StatusCode);
        await Login(client);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/tags/fragment", new { name = "tag", fragment = "Ob15,1" }, TestContext.Current.CancellationToken)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await Add(client, "../escape", "Ob15,1")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await Add(client, "tag", "Ob999,999")).StatusCode);
        factory.Access.Allowed = false;
        Assert.Equal(HttpStatusCode.Forbidden, (await Add(client, "tag", "Ob15,1")).StatusCode);
        Assert.Equal(0, factory.Git.Writes);
        Assert.Empty(factory.Tags.GetAll());
    }

    [Fact]
    public async Task SuccessfulWritesAreVisibleAcrossTranslationsAndForFathersWhileRejectedPushIsNotPublished()
    {
        await using var factory = new EditorFactory();
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        await Login(client);
        using var success = await Add(client, "new-tag", "Ob15,1");
        Assert.Equal(HttpStatusCode.OK, success.StatusCode);
        foreach (var translation in new[] { "UBG", "TNP", "TR", "KJV" })
            Assert.Contains("new-tag", await client.GetStringAsync($"/Biblia/{translation}/Ob/15", TestContext.Current.CancellationToken));
        Assert.Contains("new-tag", await client.GetStringAsync("/Biblia/Werset/Ob/15/1", TestContext.Current.CancellationToken));
        Assert.Equal(HttpStatusCode.OK, (await Add(client, "new-tag", "Ignatius/Ef1,1")).StatusCode);
        Assert.Contains("new-tag", await client.GetStringAsync("/Ojcowie/Ignatius/Ef/1", TestContext.Current.CancellationToken));
        factory.Git.Reject = true;
        Assert.Equal(HttpStatusCode.Conflict, (await Add(client, "rejected-tag", "Ob15,1")).StatusCode);
        Assert.Null(factory.Tags.Find("rejected-tag"));
    }

    private static async Task<HttpResponseMessage> Login(HttpClient client)
    {
        var html = await client.GetStringAsync("/login");
        var csrf = Regex.Match(html, "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"").Groups[1].Value;
        var result = await client.PostAsync("/login", new FormUrlEncodedContent(new Dictionary<string, string>
            { ["login"] = "test-secret", ["__RequestVerificationToken"] = WebUtility.HtmlDecode(csrf) }));
        Assert.Equal(HttpStatusCode.Redirect, result.StatusCode);
        return result;
    }

    private static async Task<HttpResponseMessage> Add(HttpClient client, string name, string fragment)
    {
        var session = await client.GetFromJsonAsync<JsonElement>("/api/editor/session");
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/tags/fragment")
            { Content = JsonContent.Create(new { name, fragment }) };
        request.Headers.Add("X-CSRF-TOKEN", session.GetProperty("csrfToken").GetString());
        return await client.SendAsync(request);
    }

    private sealed class EditorFactory : WebApplicationFactory<Program>
    {
        public string EnvironmentName = "Development";
        public readonly FakeAccess Access = new();
        public readonly FakeGit Git = new();
        public readonly TagRepository Tags = new(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString()));
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment(EnvironmentName);
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<IBibleFileSystem>();
                services.AddSingleton<IBibleFileSystem, StubBibleFileSystem>();
                services.RemoveAll<TagRepository>();
                services.AddSingleton(Tags);
                services.RemoveAll<IRepositoryWriteAccess>();
                services.AddSingleton<IRepositoryWriteAccess>(Access);
                services.RemoveAll<ITagGitStore>();
                services.AddSingleton<ITagGitStore>(Git);
                services.RemoveAll<IHostedService>();
                services.Configure<TagEditingOptions>(options => options.RepositoryPath = "/unused-test-clone");
            });
        }
    }

    private sealed class FakeAccess : IRepositoryWriteAccess
    {
        public bool Allowed = true;
        public int Checks;
        public Task CheckAsync(string token, CancellationToken cancellationToken)
        {
            Checks++;
            if (!Allowed) throw new TagEditException(403, "Access denied.");
            Assert.Equal("test-secret", token);
            return Task.CompletedTask;
        }
    }

    private sealed class FakeGit : ITagGitStore
    {
        private readonly Dictionary<string, ContentTag> _tags = [];
        public int Writes;
        public bool Reject;
        public Task<TagCatalog> SynchronizeAsync(CancellationToken cancellationToken) =>
            Task.FromResult(new TagCatalog(_tags.Values.ToArray(), "test-revision"));
        public Task<TagCatalog> AddAsync(string name, string fragment, string token, CancellationToken cancellationToken)
        {
            Assert.Equal("test-secret", token); // Retrieved from the opaque HttpOnly session.
            Writes++;
            if (Reject) throw new TagEditException(409, "Push rejected.");
            var fragments = _tags.GetValueOrDefault(name)?.Fragments ?? [];
            _tags[name] = new ContentTag(name, name, fragments.Append(fragment).Distinct().ToArray(), "");
            return SynchronizeAsync(cancellationToken);
        }
    }
}
