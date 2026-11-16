using System.Text.Json;

namespace KazdyDzienZJezusem.Services;

// The Strong number and Ross morphology are read from the TR annotation source,
// not inferred from an unrelated translation or from the spelling alone.
public sealed record GreekStrongToken(string Greek, string Strong, string Grammar, string Lemma, string? Definition)
{
    public string Key => GreekText.FormKey(Greek);
}

public sealed class GreekStrongIndex
{
    private static readonly string[] Books =
        "Mt Mk Łk J Dz Rz 1Kor 2Kor Ga Ef Flp Kol 1Tes 2Tes 1Tm 2Tm Tt Flm Hbr Jk 1P 2P 1J 2J 3J Jud Ob".Split(' ');
    private static readonly string[] Osis =
        "Matt Mark Luke John Acts Rom 1Cor 2Cor Gal Eph Phil Col 1Thess 2Thess 1Tim 2Tim Titus Phlm Heb Jas 1Pet 2Pet 1John 2John 3John Jude Rev".Split(' ');
    private readonly Dictionary<string, GreekStrongToken[]> _verses = new(StringComparer.Ordinal);
    private readonly Dictionary<(string Strong, string Grammar), string[]> _forms = new();

    public static GreekStrongIndex Read(string path)
    {
        using var stream = File.OpenRead(path);
        using var document = JsonDocument.Parse(stream);
        return FromDocument(document);
    }

    public static GreekStrongIndex FromJson(string json)
    {
        using var document = JsonDocument.Parse(json);
        return FromDocument(document);
    }

    private static GreekStrongIndex FromDocument(JsonDocument document)
    {
        var index = new GreekStrongIndex();
        var bookCodes = Osis.Zip(Books).ToDictionary(pair => pair.First, pair => pair.Second, StringComparer.Ordinal);
        var identities = new Dictionary<(string Form, string Grammar), HashSet<string>>();
        var forms = new Dictionary<(string Strong, string Grammar), HashSet<string>>();
        foreach (var verse in document.RootElement.EnumerateArray())
        {
            if (!bookCodes.TryGetValue(verse.GetProperty("book_name_osis").GetString() ?? "", out var book)) continue;
            var reference = string.Concat(book, verse.GetProperty("chapter").ToString(), ",", verse.GetProperty("verse").ToString());
            var tokens = new List<GreekStrongToken>();
            foreach (var item in verse.GetProperty("words").EnumerateArray())
            {
                var greek = GreekText.Words(item.GetProperty("greek").GetString() ?? "").FirstOrDefault();
                if (greek is null) continue;
                var grammar = item.TryGetProperty("grammar", out var grammarValue) ? grammarValue.ToString() : "";
                var rawStrong = item.TryGetProperty("strong", out var strongValue) ? strongValue.ToString() : "";
                var strong = int.TryParse(rawStrong.TrimStart('G', 'g'), out var number) && number > 0
                    ? string.Concat("G", number) : "";
                var lemma = item.TryGetProperty("dictionary_form", out var lemmaValue)
                    ? lemmaValue.ToString() : "";
                var definition = item.TryGetProperty("definition", out var definitionValue)
                    ? definitionValue.ToString() : null;
                if (definition?.Length > 240) definition = definition[..240] + "…";
                var token = new GreekStrongToken(greek, strong, grammar, lemma, definition);
                tokens.Add(token);
                if (strong.Length == 0 || grammar.Length == 0) continue;
                var key = (token.Key, grammar);
                if (!identities.TryGetValue(key, out var ids)) identities.Add(key, ids = []);
                ids.Add(strong);
                var identity = (strong, grammar);
                if (!forms.TryGetValue(identity, out var keys)) forms.Add(identity, keys = []);
                keys.Add(token.Key);
            }
            index._verses.Add(reference, tokens.ToArray());
        }

        // A spelling which has more than one Strong identity at the SAME
        // grammatical code cannot supply a trustworthy dictionary crosswalk.
        foreach (var (identity, spellings) in forms)
            index._forms[identity] = spellings
                .Where(form => identities[(form, identity.Grammar)].Count == 1)
                .OrderBy(form => form, StringComparer.Ordinal)
                .ToArray();
        return index;
    }

    public GreekStrongToken[]? FindVerse(string? reference) =>
        reference is not null && _verses.TryGetValue(reference, out var tokens) ? tokens : null;

    public IReadOnlyList<string> CompatibleForms(GreekStrongToken token)
    {
        if (!_forms.TryGetValue((token.Strong, token.Grammar), out var forms)) return [];
        // Always try the actual text form first. Bound the Redis query for
        // unusually common identities; no guessed lemma or case substitutions.
        return forms.OrderBy(form => form == token.Key ? 0 : 1).ThenBy(form => form, StringComparer.Ordinal)
            .Take(32).ToArray();
    }
}

public sealed record GreekMorphologyWord(string Greek, string? Strong, string? Lemma, string? Grammar,
    string? Polish, IReadOnlyList<string> Alternatives, string Status, string? DictionaryDefinition);
public sealed record GreekMorphologyResult(IReadOnlyList<GreekMorphologyWord> Words);

public sealed class GreekMorphologyService(GreekStrongIndex index, IGreekDictionaryReader dictionary)
{
    public async Task<GreekMorphologyResult> AnalyzeAsync(string text, string? reference, CancellationToken cancellationToken)
    {
        var words = GreekText.Words(text);
        var source = index.FindVerse(reference);
        var matches = source is null ? new Dictionary<int, int>() : Align(words, source);
        var matching = words.Select((word, i) => matches.TryGetValue(i, out var position)
            && GreekText.FormKey(word) == source![position].Key ? source[position] : null).ToArray();
        var keys = matching.Where(token => token is not null && token.Strong.Length > 0 && token.Grammar.Length > 0)
            .SelectMany(token => index.CompatibleForms(token!))
            .Distinct(StringComparer.Ordinal).ToArray();
        var lookup = keys.Length > 0
            ? await dictionary.LookupAsync(null, keys, cancellationToken)
            : new GreekDictionaryLookup(null, new Dictionary<string, GreekDictionaryForm[]>());

        var result = words.Select((word, i) =>
        {
            var token = matching[i];
            if (token is null || token.Strong.Length == 0 || token.Grammar.Length == 0)
                return new GreekMorphologyWord(word, null, null, null, null, [], "unavailable", null);

            // Candidate glosses must have BOTH an attested Strong identity and
            // exactly the source morphology. Never turn a lemma definition into
            // an inflected Polish translation.
            var candidates = index.CompatibleForms(token)
                .SelectMany(key => lookup.Forms.GetValueOrDefault(key) ?? [])
                .Where(form => form.Code?.Split('/', StringSplitOptions.TrimEntries)
                    .Contains(token.Grammar, StringComparer.OrdinalIgnoreCase) == true)
                .Select(form => form.Polish?.Trim())
                .Where(value => !string.IsNullOrWhiteSpace(value) && value is not ("—" or "-"))
                .GroupBy(value => value!, StringComparer.OrdinalIgnoreCase)
                .OrderByDescending(group => group.Count()).ThenBy(group => group.Key, StringComparer.Ordinal)
                .Select(group => group.Key).ToArray();

            return new GreekMorphologyWord(word, token.Strong, token.Lemma, token.Grammar,
                candidates.FirstOrDefault(), candidates,
                candidates.Length == 0 ? "missing" : candidates.Length == 1 ? "single" : "variants",
                token.Definition);
        }).ToArray();
        return new GreekMorphologyResult(result);
    }

    private static Dictionary<int, int> Align(string[] words, GreekStrongToken[] source)
    {
        var a = words.Select(GreekText.AlignmentKey).ToArray();
        var b = source.Select(token => GreekText.AlignmentKey(token.Greek)).ToArray();
        var lengths = new int[a.Length + 1, b.Length + 1];
        for (var i = a.Length - 1; i >= 0; i--)
            for (var j = b.Length - 1; j >= 0; j--)
                lengths[i, j] = a[i] == b[j] ? lengths[i + 1, j + 1] + 1
                    : Math.Max(lengths[i + 1, j], lengths[i, j + 1]);
        var result = new Dictionary<int, int>();
        if (lengths[0, 0] < Math.Min(3, a.Length) || lengths[0, 0] < a.Length * 0.65)
            return result;
        for (int i = 0, j = 0; i < a.Length && j < b.Length;)
        {
            if (a[i] == b[j]) { result.Add(i, j); i++; j++; }
            else if (lengths[i + 1, j] > lengths[i, j + 1]) i++;
            else j++;
        }
        return result;
    }
}
