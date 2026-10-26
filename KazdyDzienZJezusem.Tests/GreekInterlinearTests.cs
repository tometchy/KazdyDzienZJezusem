using System.Net;
using System.Text.Json;
using KazdyDzienZJezusem.Services;
using Microsoft.Extensions.Options;

namespace KazdyDzienZJezusem.Tests;

public sealed class GreekInterlinearTests
{
    [Fact]
    public async Task Uses_context_and_keeps_inflected_glosses_and_repeated_words()
    {
        using var handler = new TranslationHandler("""
            {"words":[{"greek":"βιβλίον","polish":"książkę"},{"greek":"βιβλίον","polish":"książkę"}]}
            """);
        using var service = CreateService(handler);
        var result = await service.TranslateAsync("βιβλίον, βιβλίον·", "ἀναγινώσκω βιβλίον, βιβλίον·", TestContext.Current.CancellationToken);
        Assert.Equal(new[] { "książkę", "książkę" }, result.Words.Select(word => word.Polish));
        var messages = Assert.IsType<JsonElement>(handler.LastRequest).GetProperty("messages");
        var suppliedText = JsonSerializer.Deserialize<JsonElement>(messages[1].GetProperty("content").GetString()!);
        Assert.Equal("ἀναγινώσκω βιβλίον, βιβλίον·", suppliedText.GetProperty("context").GetString());
        Assert.Contains("inflected", messages[0].GetProperty("content").GetString());
        await service.TranslateAsync("βιβλίον, βιβλίον·", "ἀναγινώσκω βιβλίον, βιβλίον·", TestContext.Current.CancellationToken);
        Assert.Equal(1, handler.Calls);
        await service.TranslateAsync("βιβλίον, βιβλίον·", "ἔχω βιβλίον, βιβλίον·", TestContext.Current.CancellationToken);
        Assert.Equal(2, handler.Calls);
    }

    [Theory]
    [InlineData("{\"words\":[]}")]
    [InlineData("{\"words\":[{\"greek\":\"λόγος\",\"polish\":\"słowo\"}]}")]
    [InlineData("{\"words\":[{\"greek\":\"βιβλίον\",\"polish\":\"\"}]}")]
    [InlineData("{\"words\":[null]}")]
    public async Task Rejects_incomplete_or_misaligned_output(string output)
    {
        using var handler = new TranslationHandler(output);
        using var service = CreateService(handler);
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.TranslateAsync("βιβλίον", "βιβλίον", TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Preserves_combining_marks_and_excludes_Greek_punctuation()
    {
        const string text = "α\u0313ρχη\u0301· βιβλίον;";
        using var handler = new TranslationHandler("""
            {"words":[{"greek":"α\u0313ρχη\u0301","polish":"początku"},{"greek":"βιβλίον","polish":"książkę"}]}
            """);
        using var service = CreateService(handler);
        var result = await service.TranslateAsync(text, text, TestContext.Current.CancellationToken);
        Assert.Equal("α\u0313ρχη\u0301", result.Words[0].Greek);
        Assert.Equal(2, result.Words.Count);
    }

    [Fact]
    public async Task Rejects_missing_configuration_and_invalid_context_without_calling_model()
    {
        using var handler = new TranslationHandler("{}");
        using var service = CreateService(handler, "");
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.TranslateAsync("βιβλίον", "βιβλίον", TestContext.Current.CancellationToken));
        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.TranslateAsync("βιβλίον", "unrelated", TestContext.Current.CancellationToken));
        Assert.Equal(0, handler.Calls);
    }

    [Fact]
    public async Task Non_Greek_text_does_not_require_a_model()
    {
        using var handler = new TranslationHandler("{}");
        using var service = CreateService(handler, "");
        Assert.Empty((await service.TranslateAsync("Polski tekst", "Polski tekst", TestContext.Current.CancellationToken)).Words);
        Assert.Equal(0, handler.Calls);
    }

    private static GreekInterlinearService CreateService(TranslationHandler handler, string model = "test-model") =>
        new(new ClientFactory(handler), Options.Create(new GreekInterlinearOptions { Model = model }));

    private sealed class ClientFactory(HttpMessageHandler handler) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new(handler, disposeHandler: false);
    }

    private sealed class TranslationHandler(string content) : HttpMessageHandler
    {
        public int Calls { get; private set; }
        public JsonElement? LastRequest { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Calls++;
            LastRequest = JsonSerializer.Deserialize<JsonElement>(await request.Content!.ReadAsStringAsync(cancellationToken));
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(JsonSerializer.Serialize(new { message = new { content } }))
            };
        }
    }
}
