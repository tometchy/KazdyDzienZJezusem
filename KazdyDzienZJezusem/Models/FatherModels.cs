namespace KazdyDzienZJezusem.Models;

public sealed record FatherAuthor(string Code, string Name, string Description);

public sealed record FatherWork(
    string Abbreviation,
    string Name,
    string GreekName,
    int Order);

public sealed record FatherIndexViewModel(IReadOnlyList<FatherAuthor> Authors);

public sealed record FatherWorkListViewModel(
    FatherAuthor Author,
    IReadOnlyList<FatherWork> Works);

public sealed record FatherChapterListViewModel(
    FatherAuthor Author,
    FatherWork Work,
    IReadOnlyList<int> Chapters);

public sealed record FatherChapterViewModel(
    FatherAuthor Author,
    FatherWork Work,
    int Chapter,
    IReadOnlyList<BibleVerse> Sections,
    int? PreviousChapter,
    int? NextChapter);
