using System.Collections.Concurrent;
using System.Globalization;
using System.Text.Json;
using KazdyDzienZJezusem.Models;

namespace KazdyDzienZJezusem.Services;

public sealed class FathersRepository
{
    private static readonly FatherAuthor Ignatius = new(
        "Ignatius",
        "Ignatius of Antioch",
        "The seven letters of the middle recension in ancient Greek");

    private static readonly IReadOnlyList<FatherWork> IgnatiusWorks =
    [
        new("Ef", "To the Ephesians", "Πρὸς Ἐφεσίους", 1),
        new("Mag", "To the Magnesians", "Πρὸς Μαγνησιεῖς", 2),
        new("Tral", "To the Trallians", "Πρὸς Τραλλιανούς", 3),
        new("Rz", "To the Romans", "Πρὸς Ῥωμαίους", 4),
        new("Filad", "To the Philadelphians", "Πρὸς Φιλαδελφεῖς", 5),
        new("Smyr", "To the Smyrnaeans", "Πρὸς Σμυρναίους", 6),
        new("Pol", "To Polycarp", "Πρὸς Πολύκαρπον", 7)
    ];

    private readonly string _rootPath;
    private readonly ConcurrentDictionary<string, IReadOnlyList<BibleVerse>> _chapterCache = new();

    public FathersRepository(string rootPath)
    {
        _rootPath = Path.GetFullPath(rootPath);
        if (!Directory.Exists(GetAuthorPath(Ignatius)))
        {
            throw new DirectoryNotFoundException(
                $"Fathers data directory was not found: {_rootPath}");
        }
    }

    public IReadOnlyList<FatherAuthor> GetAuthors() => [Ignatius];

    public FatherAuthor? FindAuthor(string? code) =>
        string.Equals(Ignatius.Code, code, StringComparison.OrdinalIgnoreCase)
            ? Ignatius
            : null;

    public IReadOnlyList<FatherWork> GetWorks(FatherAuthor author) =>
        IsIgnatius(author) ? IgnatiusWorks : [];

    public FatherWork? FindWork(FatherAuthor author, string? abbreviation) =>
        GetWorks(author).FirstOrDefault(work => string.Equals(
            work.Abbreviation,
            abbreviation,
            StringComparison.OrdinalIgnoreCase));

    public IReadOnlyList<int> GetChapters(FatherAuthor author, FatherWork work)
    {
        var path = GetWorkPath(author, work);
        if (!Directory.Exists(path))
        {
            return [];
        }

        return Directory.EnumerateFiles(path, "*.yml")
            .Select(Path.GetFileNameWithoutExtension)
            .Select(name => int.TryParse(name, NumberStyles.None, CultureInfo.InvariantCulture, out var chapter)
                ? chapter
                : (int?)null)
            .Where(chapter => chapter.HasValue)
            .Select(chapter => chapter!.Value)
            .Order()
            .ToArray();
    }

    public bool HasChapter(FatherAuthor author, FatherWork work, int chapter) =>
        File.Exists(GetChapterPath(author, work, chapter));

    public IReadOnlyList<BibleVerse> GetChapter(
        FatherAuthor author,
        FatherWork work,
        int chapter)
    {
        var path = GetChapterPath(author, work, chapter);
        return _chapterCache.GetOrAdd(path, LoadChapter);
    }

    private static IReadOnlyList<BibleVerse> LoadChapter(string path)
    {
        var sections = new List<BibleVerse>();
        foreach (var line in File.ReadLines(path))
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            var field = line.Split(':', 2);
            var key = JsonSerializer.Deserialize<string>(field[0])
                ?? throw new InvalidDataException($"Invalid section key in {path}");
            var text = JsonSerializer.Deserialize<string>(field[1].Trim())
                ?? throw new InvalidDataException($"Invalid section text in {path}");
            var number = int.Parse(key[(key.LastIndexOf(',') + 1)..], CultureInfo.InvariantCulture);
            sections.Add(new BibleVerse(number, text, []));
        }
        return sections.OrderBy(section => section.Number).ToArray();
    }

    private static bool IsIgnatius(FatherAuthor author) =>
        string.Equals(author.Code, Ignatius.Code, StringComparison.OrdinalIgnoreCase);

    private string GetAuthorPath(FatherAuthor author) =>
        Path.Combine(_rootPath, author.Code);

    private string GetWorkPath(FatherAuthor author, FatherWork work) =>
        Path.Combine(GetAuthorPath(author), work.Abbreviation);

    private string GetChapterPath(FatherAuthor author, FatherWork work, int chapter) =>
        Path.Combine(GetWorkPath(author, work), $"{chapter}.yml");
}
