using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace KazdyDzienZJezusem.Services;

public sealed record GreekWordTranslation(string Greek, string? Polish, string Match, IReadOnlyList<string> Alternatives,
    string? English, IReadOnlyList<string> EnglishAlternatives, string? Grammar, IReadOnlyList<string> GrammarAlternatives,
    string? Pronunciation, IReadOnlyList<string> PronunciationAlternatives);
public sealed record GreekInterlinearResult(IReadOnlyList<GreekWordTranslation> Words);

public static partial class GreekText
{
    public static string[] Words(string text) => Tokens().Matches(text).Select(match => match.Value).ToArray();

    // Grave accents replace acute accents in running Greek. Breathing marks
    // and iota subscripts stay significant: unrelated forms must not be merged.
    public static string FormKey(string text) => text.ToLowerInvariant().Normalize(NormalizationForm.FormD)
        .Replace('\u0300', '\u0301').Normalize(NormalizationForm.FormC).Replace('ς', 'σ');

    public static string AlignmentKey(string text) => new string(FormKey(text).Normalize(NormalizationForm.FormD)
        .Where(character => CharUnicodeInfo.GetUnicodeCategory(character) != UnicodeCategory.NonSpacingMark).ToArray());

    [GeneratedRegex(@"(?=[\u0370-\u03ff\u1f00-\u1fff])\p{L}(?:(?=[\u0370-\u03ff\u1f00-\u1fff])\p{L}|\p{M})*")]
    private static partial Regex Tokens();
}

public sealed class GreekInterlinearService(IGreekDictionaryReader dictionary)
{
    public async Task<GreekInterlinearResult> TranslateAsync(string text, string context,
        CancellationToken cancellationToken, string? reference = null)
    {
        if (string.IsNullOrWhiteSpace(text) || text.Length > 6000 || context.Length > 12000
            || !context.Contains(text, StringComparison.Ordinal) || reference?.Length > 80)
            throw new ArgumentException("Provide a Greek passage of at most 6000 characters and its surrounding context.");
        var words = GreekText.Words(text);
        if (words.Length == 0) return new GreekInterlinearResult([]);
        if (words.Length > 500) throw new ArgumentException("The passage contains too many Greek words.");
        var lookup = await dictionary.LookupAsync(reference, words.Select(GreekText.FormKey).ToArray(), cancellationToken);
        var aligned = lookup.Passage is { } passage ? Align(words, passage) : new Dictionary<int, int>();
        var result = words.Select((word, index) =>
        {
            if (aligned.TryGetValue(index, out var position))
            {
                var entry = lookup.Passage!;
                // Passage alignment ignores diacritics, but breathing and diaeresis
                // can change pronunciation. Never read a different spelling aloud.
                var pronunciation = entry.Pronunciation?.ElementAtOrDefault(position);
                if (GreekText.FormKey(word) != GreekText.FormKey(entry.Greek[position]))
                    pronunciation = lookup.Forms.GetValueOrDefault(GreekText.FormKey(word))?
                        .Select(form => form.Pronunciation).FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));
                return Describe(word, [new GreekDictionaryForm(entry.Polish[position], entry.Grammar[position],
                    entry.English?.ElementAtOrDefault(position), entry.Codes?.ElementAtOrDefault(position),
                    pronunciation)], "passage");
            }
            if (!lookup.Forms.TryGetValue(GreekText.FormKey(word), out var forms) || forms.Length == 0)
                return Describe(word, [], "missing");
            return Describe(word, forms, "form");
        }).ToArray();
        return new GreekInterlinearResult(result);
    }

    private static GreekWordTranslation Describe(string word, GreekDictionaryForm[] entries, string match)
    {
        static string[] Distinct(IEnumerable<string?> values) => values.Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value == "-" ? "—" : value!).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        static string? Unique(string[] values) => values.Length == 1 ? values[0] : null;
        var polish = Distinct(entries.Select(entry => entry.Polish));
        var english = Distinct(entries.Select(entry => entry.English));
        var grammar = Distinct(entries.Select(entry => entry.Code));
        var pronunciation = Distinct(entries.Select(entry => entry.Pronunciation));
        return new GreekWordTranslation(word, Unique(polish), match == "form" && polish.Length > 1 ? "ambiguous" : match,
            polish, Unique(english), english, Unique(grammar), grammar, Unique(pronunciation), pronunciation);
    }

    private static Dictionary<int, int> Align(string[] words, GreekDictionaryPassage passage)
    {
        var source = words.Select(GreekText.AlignmentKey).ToArray();
        var target = passage.Greek.Select(GreekText.AlignmentKey).ToArray();
        var lengths = new int[source.Length + 1, target.Length + 1];
        for (var i = source.Length - 1; i >= 0; i--)
            for (var j = target.Length - 1; j >= 0; j--)
                lengths[i, j] = source[i] == target[j]
                    ? lengths[i + 1, j + 1] + 1 : Math.Max(lengths[i + 1, j], lengths[i, j + 1]);

        // A reference must substantially match before passage glosses can be used.
        var result = new Dictionary<int, int>();
        if (lengths[0, 0] < Math.Min(3, source.Length) || lengths[0, 0] < source.Length * 0.65) return result;
        for (int i = 0, j = 0; i < source.Length && j < target.Length;)
        {
            if (source[i] == target[j]) { result.Add(i, j); i++; j++; }
            else if (lengths[i + 1, j] > lengths[i, j + 1]) i++;
            else j++;
        }
        return result;
    }
}
