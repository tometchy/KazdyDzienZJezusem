namespace KazdyDzienZJezusem.Models;

public sealed record BibleTranslation(string Code, string Name, string Description);

public sealed record BibleBook(string Abbreviation, string Name, int Order);

public sealed record BibleVerse(
    int Number,
    string Text,
    IReadOnlyList<string> Tags);

public sealed record BibleTranslationListViewModel(
    IReadOnlyList<BibleTranslation> Translations);

public sealed record BibleBookListViewModel(
    BibleTranslation Translation,
    IReadOnlyList<BibleBook> Books);

public sealed record BibleChapterListViewModel(
    BibleTranslation Translation,
    BibleBook Book,
    IReadOnlyList<int> Chapters);

public sealed record BibleChapterViewModel(
    BibleTranslation Translation,
    BibleBook Book,
    int Chapter,
    IReadOnlyList<BibleVerse> Verses,
    int? PreviousChapter,
    int? NextChapter);

public sealed record BibleVerseComparison(
    BibleTranslation Translation,
    string? Text);

public sealed record BibleVerseComparisonViewModel(
    BibleBook Book,
    int Chapter,
    int Verse,
    BibleTranslation? SourceTranslation,
    IReadOnlyList<BibleVerseComparison> Translations);
