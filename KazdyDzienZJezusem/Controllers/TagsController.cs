using System.Globalization;
using System.Text.RegularExpressions;
using KazdyDzienZJezusem.Models;
using KazdyDzienZJezusem.Services;
using Markdig;
using Microsoft.AspNetCore.Mvc;

namespace KazdyDzienZJezusem.Controllers;

[Route("Tagi")]
public sealed partial class TagsController(
    TagRepository tags,
    BibleRepository bible,
    FathersRepository fathers) : Controller
{
    [HttpGet("{slug}")]
    public IActionResult Details(string slug)
    {
        var tag = tags.Find(slug);
        if (tag is null)
        {
            return NotFound();
        }

        var passages = tag.Fragments.Select(ResolvePassage).ToArray();
        return View(new TagDetailsViewModel(
            tag,
            Markdown.ToHtml(tag.DescriptionMarkdown),
            passages));
    }

    private TagPassage ResolvePassage(string reference)
    {
        var bibleMatch = BibleReference().Match(reference);
        if (bibleMatch.Success)
        {
            var book = bible.FindBook(bibleMatch.Groups[1].Value)
                ?? throw InvalidReference(reference);
            var chapter = ParseNumber(bibleMatch.Groups[2].Value);
            var verse = ParseNumber(bibleMatch.Groups[3].Value);
            var translation = bible.GetTranslations().FirstOrDefault(candidate =>
                bible.GetVerse(candidate, book, chapter, verse) is not null)
                ?? throw InvalidReference(reference);
            var text = bible.GetVerse(translation, book, chapter, verse)!;
            return new(reference["Bible:".Length..], text,
                Url.Action("Verse", "Bible", new { book = book.Abbreviation, chapter, verse, sourceTranslation = translation.Code })!);
        }

        var fatherMatch = FatherReference().Match(reference);
        if (fatherMatch.Success)
        {
            var author = fathers.FindAuthor(fatherMatch.Groups[1].Value)
                ?? throw InvalidReference(reference);
            var work = fathers.FindWork(author, fatherMatch.Groups[2].Value)
                ?? throw InvalidReference(reference);
            var chapter = ParseNumber(fatherMatch.Groups[3].Value);
            var section = ParseNumber(fatherMatch.Groups[4].Value);
            var text = fathers.GetChapter(author, work, chapter)
                .FirstOrDefault(item => item.Number == section)?.Text
                ?? throw InvalidReference(reference);
            return new(reference["Fathers:".Length..], text,
                Url.Action("Chapter", "Fathers", new { author = author.Code, work = work.Abbreviation, chapter })! + $"#{section}");
        }

        throw InvalidReference(reference);
    }

    private static int ParseNumber(string value) => int.Parse(value, CultureInfo.InvariantCulture);
    private static InvalidDataException InvalidReference(string reference) =>
        new($"Unknown tag fragment reference: {reference}");

    [GeneratedRegex(@"^Bible:(.+?)(\d+),(\d+)$")]
    private static partial Regex BibleReference();

    [GeneratedRegex(@"^Fathers:([^:]+):(.+?)(\d+),(\d+)$")]
    private static partial Regex FatherReference();
}
