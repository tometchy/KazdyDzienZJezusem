using KazdyDzienZJezusem.Models;
using KazdyDzienZJezusem.Services;
using Microsoft.AspNetCore.Mvc;

namespace KazdyDzienZJezusem.Controllers;

[Route("Biblia")]
public sealed class BibleController(BibleRepository bibleRepository) : Controller
{
    [HttpGet("")]
    public IActionResult Index()
    {
        return View(new BibleTranslationListViewModel(
            bibleRepository.GetTranslations()));
    }

    [HttpGet("{translation}")]
    public IActionResult Translation(string translation)
    {
        var selectedTranslation = bibleRepository.FindTranslation(translation);
        if (selectedTranslation is null)
        {
            return NotFound();
        }

        return View(new BibleBookListViewModel(
            selectedTranslation,
            bibleRepository.GetBooks(selectedTranslation)));
    }

    [HttpGet("{translation}/{book}")]
    public IActionResult Book(string translation, string book)
    {
        var selectedTranslation = bibleRepository.FindTranslation(translation);
        var selectedBook = bibleRepository.FindBook(book);
        if (selectedTranslation is null
            || selectedBook is null
            || !bibleRepository.HasBook(selectedTranslation, selectedBook))
        {
            return NotFound();
        }

        var chapters = bibleRepository.GetChapters(selectedTranslation, selectedBook);
        if (chapters.Count == 0)
        {
            return NotFound();
        }

        return View(new BibleChapterListViewModel(
            selectedTranslation,
            selectedBook,
            chapters));
    }

    [HttpGet("{translation}/{book}/{chapter:int:min(1)}")]
    public IActionResult Chapter(string translation, string book, int chapter)
    {
        var selectedTranslation = bibleRepository.FindTranslation(translation);
        var selectedBook = bibleRepository.FindBook(book);
        if (selectedTranslation is null
            || selectedBook is null
            || !bibleRepository.HasChapter(selectedTranslation, selectedBook, chapter))
        {
            return NotFound();
        }

        var chapters = bibleRepository.GetChapters(selectedTranslation, selectedBook);
        var chapterIndex = chapters.ToList().IndexOf(chapter);
        if (chapterIndex < 0)
        {
            return NotFound();
        }

        return View(new BibleChapterViewModel(
            selectedTranslation,
            selectedBook,
            chapter,
            bibleRepository.GetChapter(selectedTranslation, selectedBook, chapter),
            chapterIndex > 0 ? chapters[chapterIndex - 1] : null,
            chapterIndex < chapters.Count - 1 ? chapters[chapterIndex + 1] : null));
    }

    [HttpGet("Werset/{book}/{chapter:int:min(1)}/{verse:int:min(1)}")]
    public IActionResult Verse(
        string book,
        int chapter,
        int verse,
        string? sourceTranslation = null)
    {
        var selectedBook = bibleRepository.FindBook(book);
        if (selectedBook is null)
        {
            return NotFound();
        }

        var source = bibleRepository.FindTranslation(sourceTranslation);
        if (source is not null && !bibleRepository.HasChapter(source, selectedBook, chapter))
        {
            source = null;
        }

        var translations = bibleRepository.GetTranslations()
            .Select(translation => new BibleVerseComparison(
                translation,
                bibleRepository.GetVerse(translation, selectedBook, chapter, verse)))
            .ToArray();

        if (translations.All(item => item.Text is null))
        {
            return NotFound();
        }

        return View(new BibleVerseComparisonViewModel(
            selectedBook,
            chapter,
            verse,
            source,
            translations));
    }
}
