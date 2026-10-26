using KazdyDzienZJezusem.Services;

namespace KazdyDzienZJezusem.Tests;

public sealed class GreekInterlinearTests
{
    private static readonly Lazy<GreekDictionaryCatalog> Corpus = new(() => GreekDictionaryCatalog.Read(
        Path.Combine(AppContext.BaseDirectory, "GreekPolish", "interlinear.jsonl.gz")));

    [Fact]
    public void Bundled_corpus_has_complete_aligned_arrays_and_declared_counts()
    {
        Assert.Equal(7940, Corpus.Value.Passages.Count);
        Assert.Equal(18433, Corpus.Value.Forms.Count);
        Assert.Equal(137323, Corpus.Value.Passages.Values.Sum(passage => passage.Greek.Length));
        Assert.All(Corpus.Value.Passages.Values, passage =>
        {
            Assert.Equal(passage.Greek.Length, passage.Polish.Length);
            Assert.Equal(passage.Greek.Length, passage.Grammar.Length);
        });
    }

    [Fact]
    public async Task Existing_Gnt_morphology_adds_attested_variants_with_the_same_inflection()
    {
        var service = new GreekInterlinearService(new MemoryReader(Corpus.Value));
        var result = await service.TranslateAsync("ᾄδουσι", "ᾄδουσι", TestContext.Current.CancellationToken);
        Assert.Equal("śpiewają", result.Words[0].Polish);
        Assert.Equal("form", result.Words[0].Match);
        var variant = Corpus.Value.Forms[GreekText.FormKey("ἔδωκε")];
        Assert.All(variant, form => Assert.Equal("V-AIA-3S", form.Grammar));
        Assert.Contains(variant, form => form.Polish == "dał");
        Assert.DoesNotContain(variant, form => form.Polish == "dali");
    }

    [Fact]
    public async Task Passage_lookup_preserves_inflected_forms_and_repeated_tokens()
    {
        var service = new GreekInterlinearService(new MemoryReader(Corpus.Value));
        const string text = "Ἐν ἀρχῇ ἦν ὁ λόγος, καὶ ὁ λόγος ἦν πρὸς τὸν Θεόν, καὶ Θεὸς ἦν ὁ λόγος.";
        var result = await service.TranslateAsync(text, text, TestContext.Current.CancellationToken, "J1,1");
        Assert.Equal("początku", result.Words[1].Polish);
        Assert.Equal("Boga", result.Words[11].Polish);
        Assert.Equal("Bóg", result.Words[13].Polish);
        Assert.Equal(3, result.Words.Count(word => word.Polish == "Słowo"));
        Assert.All(result.Words, word => Assert.Equal("passage", word.Match));
    }

    [Fact]
    public async Task Source_passage_disambiguates_accusative_without_a_lemma_fallback()
    {
        var reader = new FixedReader(new("Test1,1", ["ἀναγινώσκω", "βιβλίον"], ["czytam", "książkę"], ["V", "N-ANS"]),
            new() { [GreekText.FormKey("βιβλίον")] = [new("książka", "N-NNS"), new("książkę", "N-ANS")] });
        var service = new GreekInterlinearService(reader);
        const string text = "ἀναγινώσκω βιβλίον";
        var result = await service.TranslateAsync(text, text, TestContext.Current.CancellationToken, "Test1,1");
        Assert.Equal("książkę", result.Words[1].Polish);
    }

    [Fact]
    public async Task Unknown_and_ambiguous_forms_are_explicit_instead_of_guessed()
    {
        var reader = new FixedReader(null, new()
        {
            [GreekText.FormKey("βιβλίον")] = [new("książka", "N-NNS"), new("książkę", "N-ANS")]
        });
        var service = new GreekInterlinearService(reader);
        const string text = "βιβλίον ἀγνωστότατον";
        var result = await service.TranslateAsync(text, text, TestContext.Current.CancellationToken);
        Assert.Equal("ambiguous", result.Words[0].Match);
        Assert.Null(result.Words[0].Polish);
        Assert.Equal(new[] { "książka", "książkę" }, result.Words[0].Alternatives);
        Assert.Equal("missing", result.Words[1].Match);
        Assert.Null(result.Words[1].Polish);
    }

    [Fact]
    public async Task Unrelated_reference_does_not_transfer_passage_glosses()
    {
        var reader = new FixedReader(new("J1,1", ["Ἐν", "ἀρχῇ"], ["Na", "początku"], ["Prep", "N"]), new());
        const string text = "βιβλίον λόγος ἀρχῇ";
        var result = await new GreekInterlinearService(reader).TranslateAsync(text, text, TestContext.Current.CancellationToken, "J1,1");
        Assert.All(result.Words, word => Assert.Equal("missing", word.Match));
    }

    [Fact]
    public void Form_keys_handle_decomposition_and_grave_accents_but_preserve_distinctions()
    {
        Assert.Equal(GreekText.FormKey("ἀρχή"), GreekText.FormKey("α\u0313ρχη\u0300"));
        Assert.Equal(GreekText.FormKey("ΛΌΓΟΣ"), GreekText.FormKey("λόγος"));
        Assert.NotEqual(GreekText.FormKey("ἡ"), GreekText.FormKey("ἥ"));
        Assert.NotEqual(GreekText.FormKey("ᾗ"), GreekText.FormKey("ἡ"));
        Assert.Equal(new[] { "α\u0313ρχη\u0301", "βιβλίον" }, GreekText.Words("α\u0313ρχη\u0301· βιβλίον;"));
    }

    [Fact]
    public async Task Validates_request_and_skips_store_for_non_Greek_text()
    {
        var reader = new FixedReader(null, new());
        var service = new GreekInterlinearService(reader);
        Assert.Empty((await service.TranslateAsync("Polski tekst", "Polski tekst", TestContext.Current.CancellationToken)).Words);
        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.TranslateAsync("βιβλίον", "unrelated", TestContext.Current.CancellationToken));
        Assert.Equal(0, reader.Calls);
    }

    private sealed class MemoryReader(GreekDictionaryCatalog catalog) : IGreekDictionaryReader
    {
        public Task<GreekDictionaryLookup> LookupAsync(string? reference, string[] forms, CancellationToken cancellationToken) =>
            Task.FromResult(new GreekDictionaryLookup(reference is not null ? catalog.Passages.GetValueOrDefault(reference) : null,
                forms.Distinct().Where(catalog.Forms.ContainsKey).ToDictionary(key => key, key => catalog.Forms[key])));
    }

    private sealed class FixedReader(GreekDictionaryPassage? passage, Dictionary<string, GreekDictionaryForm[]> forms) : IGreekDictionaryReader
    {
        public int Calls { get; private set; }
        public Task<GreekDictionaryLookup> LookupAsync(string? reference, string[] keys, CancellationToken cancellationToken)
        {
            Calls++;
            return Task.FromResult(new GreekDictionaryLookup(passage, forms));
        }
    }
}
