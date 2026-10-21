using System.Globalization;
using System.Text.RegularExpressions;
using KazdyDzienZJezusem.Models;

namespace KazdyDzienZJezusem.Services;

public sealed partial class TagPassageReader(BibleRepository bible, FathersRepository fathers)
{
    public TagDetailsViewModel Read(ContentTag tag)
    {
        var bibleReferences = new List<(PassageReference Reference, BibleBook Book)>();
        var untranslatedPassages = new List<TagPassageViewModel>();
        foreach (var fragment in tag.Fragments)
        {
            var reference = ParseReference(fragment);
            if (reference is null)
            {
                untranslatedPassages.Add(Unavailable(fragment));
            }
            else if (reference.AuthorCode is not null)
            {
                untranslatedPassages.Add(ReadFatherPassage(reference));
            }
            else if (reference.Chapter > 0 && bible.FindBook(reference.BookCode) is { } book)
            {
                bibleReferences.Add((reference, book));
            }
            else
            {
                untranslatedPassages.Add(Unavailable(fragment));
            }
        }

        var fallbackTranslation = bible.FindTranslation("UBG")!;
        var fallbackPassages = bibleReferences
            .Select(item => ReadBiblePassage(item.Reference, item.Book, fallbackTranslation))
            .ToArray();
        var translations = new List<TagTranslationViewModel>();
        foreach (var translation in bible.GetTranslations())
        {
            var ownPassages = translation == fallbackTranslation
                ? fallbackPassages
                : bibleReferences
                    .Select(item => ReadBiblePassage(item.Reference, item.Book, translation))
                    .ToArray();
            if (!ownPassages.Any(passage => passage is not null))
            {
                continue;
            }

            var passages = ownPassages
                .Select((passage, index) => passage
                    ?? fallbackPassages[index]
                    ?? Unavailable(bibleReferences[index].Reference.Original))
                .ToArray();
            translations.Add(new TagTranslationViewModel(translation, passages));
        }

        if (translations.Count == 0)
        {
            untranslatedPassages.AddRange(bibleReferences.Select(item => Unavailable(item.Reference.Original)));
        }

        var defaultTranslation = translations.FirstOrDefault(item => item.Translation.Code == "TNP")
            ?? translations.FirstOrDefault(item => item.Translation.Code == "UBG")
            ?? translations.FirstOrDefault();
        return new TagDetailsViewModel(tag)
        {
            DefaultTranslationCode = defaultTranslation?.Translation.Code ?? "TNP",
            Translations = translations,
            UntranslatedPassages = untranslatedPassages
        };
    }

    private TagPassageViewModel? ReadBiblePassage(
        PassageReference reference,
        BibleBook book,
        BibleTranslation translation)
    {
        if (!bible.HasChapter(translation, book, reference.Chapter))
        {
            return null;
        }

        var verses = ReadRange(bible.GetChapter(translation, book, reference.Chapter), reference);
        if (verses.Count == 0)
        {
            return null;
        }

        var url = $"/Biblia/{Uri.EscapeDataString(translation.Code)}/{Uri.EscapeDataString(book.Abbreviation)}/{reference.Chapter.ToString(CultureInfo.InvariantCulture)}#{reference.FirstVerse.ToString(CultureInfo.InvariantCulture)}";
        return new TagPassageViewModel(reference.Original, url, translation, verses);
    }

    private TagPassageViewModel ReadFatherPassage(PassageReference reference)
    {
        var author = fathers.FindAuthor(reference.AuthorCode);
        var work = author is null ? null : fathers.FindWork(author, reference.BookCode);
        if (author is null || work is null || !fathers.HasChapter(author, work, reference.Chapter))
        {
            return Unavailable(reference.Original);
        }

        var verses = ReadRange(fathers.GetChapter(author, work, reference.Chapter), reference);
        if (verses.Count == 0)
        {
            return Unavailable(reference.Original);
        }

        var url = $"/Ojcowie/{Uri.EscapeDataString(author.Code)}/{Uri.EscapeDataString(work.Abbreviation)}/{reference.Chapter.ToString(CultureInfo.InvariantCulture)}#{reference.FirstVerse.ToString(CultureInfo.InvariantCulture)}";
        return new TagPassageViewModel(reference.Original, url, null, verses);
    }

    private static IReadOnlyList<BibleVerse> ReadRange(
        IReadOnlyList<BibleVerse> chapter,
        PassageReference reference)
    {
        var verses = chapter
            .Where(verse => verse.Number >= reference.FirstVerse && verse.Number <= reference.LastVerse)
            .ToArray();
        if (verses.Length != (long)reference.LastVerse - reference.FirstVerse + 1
            || verses.Where((verse, index) => verse.Number != (long)reference.FirstVerse + index).Any())
        {
            return [];
        }

        return verses;
    }

    private static PassageReference? ParseReference(string fragment)
    {
        var match = ReferencePattern().Match(fragment);
        if (!match.Success
            || !int.TryParse(match.Groups["chapter"].Value, NumberStyles.None, CultureInfo.InvariantCulture, out var chapter)
            || !int.TryParse(match.Groups["first"].Value, NumberStyles.None, CultureInfo.InvariantCulture, out var first)
            || first < 1)
        {
            return null;
        }

        var last = first;
        if (match.Groups["last"].Success
            && (!int.TryParse(match.Groups["last"].Value, NumberStyles.None, CultureInfo.InvariantCulture, out last)
                || last < first))
        {
            return null;
        }

        return new PassageReference(
            fragment,
            match.Groups["author"].Success ? match.Groups["author"].Value : null,
            match.Groups["book"].Value,
            chapter,
            first,
            last);
    }

    private static TagPassageViewModel Unavailable(string reference) => new(reference, null, null, []);

    [GeneratedRegex(@"\A(?:(?<author>[A-Za-z]+)/)?(?<book>[1-3]?[\p{L}]+)(?<chapter>[0-9]+),(?<first>[0-9]+)(?:-(?<last>[0-9]+))?\z", RegexOptions.CultureInvariant)]
    private static partial Regex ReferencePattern();

    private sealed record PassageReference(
        string Original,
        string? AuthorCode,
        string BookCode,
        int Chapter,
        int FirstVerse,
        int LastVerse);
}
