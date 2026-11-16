using KazdyDzienZJezusem.Services;

namespace KazdyDzienZJezusem.Tests;

public sealed class GreekMorphologyTests
{
    private const string Passage = """
        [
          {"book_name_osis":"1Thess","chapter":4,"verse":16,"words":[
            {"greek":"ἐν","strong":1722,"grammar":"PREP","dictionary_form":"ἐν"},
            {"greek":"σάλπιγγι","strong":4536,"grammar":"N-DSF","dictionary_form":"σάλπιγξ"},
            {"greek":"θεοῦ","strong":2316,"grammar":"N-GSM","dictionary_form":"θεός"}
          ]}
        ]
        """;

    [Fact]
    public async Task Matches_Strong_and_exact_morphology_without_reusing_other_cases()
    {
        var dictionary = new StubReader(new()
        {
            [GreekText.FormKey("ἐν")] =
                [new("w", "PREP", "in", "PREP"), new("na", "PREP", "with", "PREP")],
            [GreekText.FormKey("σάλπιγγι")] =
                [new("trąbą", "N-DSF", "trumpet", "N-DSF"),
                 new("trąbie", "N-DSF", "trumpet", "N-DSF"),
                 new("trąby", "N-GSF", "trumpet", "N-GSF")],
            [GreekText.FormKey("θεοῦ")] = [new("Boga", "N-GSM", "God", "N-GSM")]
        });
        var service = new GreekMorphologyService(GreekStrongIndex.FromJson(Passage), dictionary);
        var result = await service.AnalyzeAsync("ἐν σάλπιγγι θεοῦ", "1Tes4,16", TestContext.Current.CancellationToken);

        Assert.Equal(3, result.Words.Count);
        Assert.Equal("G1722", result.Words[0].Strong);
        Assert.Equal("PREP", result.Words[0].Grammar);
        Assert.Contains("w", result.Words[0].Alternatives);
        Assert.Contains("na", result.Words[0].Alternatives);
        Assert.Equal("G4536", result.Words[1].Strong);
        Assert.Equal("σάλπιγξ", result.Words[1].Lemma);
        Assert.Equal("N-DSF", result.Words[1].Grammar);
        Assert.Contains("trąbą", result.Words[1].Alternatives);
        Assert.Contains("trąbie", result.Words[1].Alternatives);
        Assert.DoesNotContain("trąby", result.Words[1].Alternatives);
        Assert.Equal("variants", result.Words[1].Status);
        Assert.Equal("Boga", result.Words[2].Polish);
        Assert.Equal("single", result.Words[2].Status);
    }

    [Fact]
    public async Task Refuses_to_infer_Strong_from_a_word_in_an_unrelated_passage()
    {
        var service = new GreekMorphologyService(GreekStrongIndex.FromJson(Passage), new StubReader(new()));
        var result = await service.AnalyzeAsync("ἐν σάλπιγγι", "J1,1", TestContext.Current.CancellationToken);
        Assert.All(result.Words, word =>
        {
            Assert.Null(word.Strong);
            Assert.Equal("unavailable", word.Status);
            Assert.Null(word.Polish);
        });
    }

    [Fact]
    public async Task Excludes_homographic_forms_with_two_Strong_identities()
    {
        var corpus = Passage.TrimEnd().TrimEnd(']') + """
            ,
            {"book_name_osis":"John","chapter":1,"verse":1,"words":[
              {"greek":"ἐν","strong":9999,"grammar":"PREP","dictionary_form":"ἐν"}
            ]}
            ]
            """;
        var dictionary = new StubReader(new()
        {
            [GreekText.FormKey("ἐν")] = [new("w", "PREP", "in", "PREP")]
        });
        var service = new GreekMorphologyService(GreekStrongIndex.FromJson(corpus), dictionary);
        var result = await service.AnalyzeAsync("ἐν", "1Tes4,16", TestContext.Current.CancellationToken);
        Assert.Equal("G1722", result.Words[0].Strong);
        Assert.Equal("missing", result.Words[0].Status);
        Assert.Null(result.Words[0].Polish);
    }

    private sealed class StubReader(Dictionary<string, GreekDictionaryForm[]> forms) : IGreekDictionaryReader
    {
        public Task<GreekDictionaryLookup> LookupAsync(string? reference, string[] keys, CancellationToken cancellationToken)
            => Task.FromResult(new GreekDictionaryLookup(null,
                keys.Where(forms.ContainsKey).ToDictionary(key => key, key => forms[key])));
    }
}
