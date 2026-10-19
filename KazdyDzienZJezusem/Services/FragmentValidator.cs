using System.Text.RegularExpressions;

namespace KazdyDzienZJezusem.Services;

public sealed partial class FragmentValidator(BibleRepository bible, FathersRepository fathers)
{
    public string Validate(string? fragment)
    {
        var match = Reference().Match(fragment ?? "");
        if (!match.Success || !int.TryParse(match.Groups["chapter"].Value, out var chapter)
            || !int.TryParse(match.Groups["verse"].Value, out var verse) || verse < 1)
            throw new TagEditException(400, "Choose an existing verse or Church Fathers passage.");
        var bookCode = match.Groups["book"].Value;
        if (match.Groups["author"].Success)
        {
            var author = fathers.FindAuthor(match.Groups["author"].Value);
            var work = author is null ? null : fathers.FindWork(author, bookCode);
            if (author is not null && work is not null && fathers.HasChapter(author, work, chapter)
                && fathers.GetChapter(author, work, chapter).Any(section => section.Number == verse))
                return $"{author.Code}/{work.Abbreviation}{chapter},{verse}";
        }
        else
        {
            var book = bible.FindBook(bookCode);
            if (book is not null && chapter > 0 && bible.GetTranslations()
                .Any(translation => bible.GetVerse(translation, book, chapter, verse) is not null))
                return $"{book.Abbreviation}{chapter},{verse}";
        }
        throw new TagEditException(400, "This verse or passage does not exist.");
    }

    [GeneratedRegex(@"^(?:(?<author>[A-Za-z]+)/)?(?<book>[1-3]?[\p{L}]+)(?<chapter>[0-9]{1,3}),(?<verse>[0-9]{1,3})$", RegexOptions.CultureInvariant)]
    private static partial Regex Reference();
}
