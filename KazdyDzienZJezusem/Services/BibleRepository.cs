using System.Collections.Concurrent;
using System.Globalization;
using System.Text.Json;
using KazdyDzienZJezusem.Models;

namespace KazdyDzienZJezusem.Services;

public sealed class BibleRepository
{
    private static readonly IReadOnlyList<BibleTranslation> Translations =
    [
        new("UBG", "UBG", "Uwspółcześniona Biblia Gdańska"),
        new("TNP", "TNP", "Toruński Przekład Nowego Przymierza"),
        new("TR", "TR", "Textus Receptus — tekst grecki"),
        new("KJV", "KJV", "King James Version")
    ];

    private static readonly IReadOnlyList<BibleBook> Books =
    [
        new("Rdz", "Księga Rodzaju", 1),
        new("Wj", "Księga Wyjścia", 2),
        new("Kpł", "Księga Kapłańska", 3),
        new("Lb", "Księga Liczb", 4),
        new("Pwt", "Księga Powtórzonego Prawa", 5),
        new("Joz", "Księga Jozuego", 6),
        new("Sdz", "Księga Sędziów", 7),
        new("Rt", "Księga Rut", 8),
        new("1Sm", "I Księga Samuela", 9),
        new("2Sm", "II Księga Samuela", 10),
        new("1Krl", "I Księga Królewska", 11),
        new("2Krl", "II Księga Królewska", 12),
        new("1Krn", "I Księga Kronik", 13),
        new("2Krn", "II Księga Kronik", 14),
        new("Ezd", "Księga Ezdrasza", 15),
        new("Ne", "Księga Nehemiasza", 16),
        new("Est", "Księga Estery", 17),
        new("Hi", "Księga Hioba", 18),
        new("Ps", "Księga Psalmów", 19),
        new("Prz", "Księga Przypowieści Salomona", 20),
        new("Kaz", "Księga Kaznodziei", 21),
        new("Pnp", "Pieśń nad Pieśniami", 22),
        new("Iz", "Księga Izajasza", 23),
        new("Jr", "Księga Jeremiasza", 24),
        new("Lm", "Lamentacje", 25),
        new("Ez", "Księga Ezechiela", 26),
        new("Dn", "Księga Daniela", 27),
        new("Oz", "Księga Ozeasza", 28),
        new("Jl", "Księga Joela", 29),
        new("Am", "Księga Amosa", 30),
        new("Ab", "Księga Abdiasza", 31),
        new("Jon", "Księga Jonasza", 32),
        new("Mi", "Księga Micheasza", 33),
        new("Na", "Księga Nahuma", 34),
        new("Ha", "Księga Habakuka", 35),
        new("So", "Księga Sofoniasza", 36),
        new("Ag", "Księga Aggeusza", 37),
        new("Za", "Księga Zachariasza", 38),
        new("Ml", "Księga Malachiasza", 39),
        new("Mt", "Ewangelia Mateusza", 40),
        new("Mk", "Ewangelia Marka", 41),
        new("Łk", "Ewangelia Łukasza", 42),
        new("J", "Ewangelia Jana", 43),
        new("Dz", "Dzieje Apostolskie", 44),
        new("Rz", "List do Rzymian", 45),
        new("1Kor", "I List do Koryntian", 46),
        new("2Kor", "II List do Koryntian", 47),
        new("Ga", "List do Galacjan", 48),
        new("Ef", "List do Efezjan", 49),
        new("Flp", "List do Filipian", 50),
        new("Kol", "List do Kolosan", 51),
        new("1Tes", "I List do Tesaloniczan", 52),
        new("2Tes", "II List do Tesaloniczan", 53),
        new("1Tm", "I List do Tymoteusza", 54),
        new("2Tm", "II List do Tymoteusza", 55),
        new("Tt", "List do Tytusa", 56),
        new("Flm", "List do Filemona", 57),
        new("Hbr", "List do Hebrajczyków", 58),
        new("Jk", "List Jakuba", 59),
        new("1P", "I List Piotra", 60),
        new("2P", "II List Piotra", 61),
        new("1J", "I List Jana", 62),
        new("2J", "II List Jana", 63),
        new("3J", "III List Jana", 64),
        new("Jud", "List Judy", 65),
        new("Ob", "Księga Objawienia", 66)
    ];

    private readonly string _rootPath;
    private readonly IBibleFileSystem _fileSystem;
    private readonly ConcurrentDictionary<ChapterKey, IReadOnlyList<BibleVerse>> _chapterCache = new();

    public BibleRepository(string rootPath, IBibleFileSystem fileSystem)
    {
        _rootPath = Path.GetFullPath(rootPath);
        _fileSystem = fileSystem;

        if (!_fileSystem.DirectoryExists(_rootPath))
        {
            throw new DirectoryNotFoundException(
                $"Bible data directory was not found: {_rootPath}");
        }

        var missingTranslations = Translations
            .Where(translation => !_fileSystem.DirectoryExists(GetTranslationPath(translation)))
            .Select(translation => translation.Code)
            .ToArray();

        if (missingTranslations.Length > 0)
        {
            throw new DirectoryNotFoundException(
                $"Bible data is missing translations: {string.Join(", ", missingTranslations)}");
        }
    }

    public IReadOnlyList<BibleTranslation> GetTranslations() => Translations;

    public BibleTranslation? FindTranslation(string? code) => Translations.FirstOrDefault(
        translation => string.Equals(translation.Code, code, StringComparison.OrdinalIgnoreCase));

    public BibleBook? FindBook(string? abbreviation) => Books.FirstOrDefault(
        book => string.Equals(book.Abbreviation, abbreviation, StringComparison.OrdinalIgnoreCase));

    public IReadOnlyList<BibleBook> GetBooks(BibleTranslation translation) => Books
        .Where(book => _fileSystem.DirectoryExists(GetBookPath(translation, book)))
        .ToArray();

    public bool HasBook(BibleTranslation translation, BibleBook book) =>
        _fileSystem.DirectoryExists(GetBookPath(translation, book));

    public IReadOnlyList<int> GetChapters(BibleTranslation translation, BibleBook book)
    {
        var bookPath = GetBookPath(translation, book);
        if (!_fileSystem.DirectoryExists(bookPath))
        {
            return [];
        }

        return _fileSystem
            .EnumerateFiles(bookPath, "*.yml")
            .Select(Path.GetFileNameWithoutExtension)
            .Select(fileName => int.TryParse(fileName, NumberStyles.None, CultureInfo.InvariantCulture, out var chapter)
                ? chapter
                : (int?)null)
            .Where(chapter => chapter.HasValue)
            .Select(chapter => chapter!.Value)
            .Order()
            .ToArray();
    }

    public bool HasChapter(BibleTranslation translation, BibleBook book, int chapter) =>
        _fileSystem.FileExists(GetChapterPath(translation, book, chapter));

    public IReadOnlyList<BibleVerse> GetChapter(
        BibleTranslation translation,
        BibleBook book,
        int chapter)
    {
        var key = new ChapterKey(translation.Code, book.Abbreviation, chapter);
        return _chapterCache.GetOrAdd(key, _ => LoadChapter(translation, book, chapter));
    }

    public string? GetVerse(
        BibleTranslation translation,
        BibleBook book,
        int chapter,
        int verse)
    {
        if (!HasChapter(translation, book, chapter))
        {
            return null;
        }

        return GetChapter(translation, book, chapter)
            .FirstOrDefault(item => item.Number == verse)
            ?.Text;
    }

    private IReadOnlyList<BibleVerse> LoadChapter(
        BibleTranslation translation,
        BibleBook book,
        int chapter)
    {
        var path = GetChapterPath(translation, book, chapter);
        if (!_fileSystem.FileExists(path))
        {
            return [];
        }

        var keyPrefix = $"{book.Abbreviation}{chapter},";
        var seenVerses = new HashSet<int>();
        var verses = new List<BibleVerse>();
        string? currentKey = null;
        int? currentVerseNumber = null;
        string? currentText = null;
        IReadOnlyList<string>? currentTags = null;
        var hasText = false;
        var hasTags = false;

        void AddCurrentVerse()
        {
            if (currentKey is null || currentVerseNumber is null)
            {
                return;
            }

            if (!hasText || string.IsNullOrWhiteSpace(currentText))
            {
                throw new InvalidDataException(
                    $"Field 'text' in verse '{currentKey}' must be a non-empty string in {path}");
            }

            if (!hasTags || currentTags is null)
            {
                throw new InvalidDataException(
                    $"Field 'tags' is required in verse '{currentKey}' in {path}");
            }

            if (!seenVerses.Add(currentVerseNumber.Value))
            {
                throw new InvalidDataException(
                    $"Duplicate verse '{currentKey}' in {path}");
            }

            verses.Add(new BibleVerse(currentVerseNumber.Value, currentText, currentTags));
        }

        foreach (var line in _fileSystem.ReadLines(path))
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            if (!char.IsWhiteSpace(line[0]))
            {
                AddCurrentVerse();

                currentKey = ParseVerseKey(line, path);
                currentVerseNumber = ParseVerseNumber(currentKey, keyPrefix, path);
                currentText = null;
                currentTags = null;
                hasText = false;
                hasTags = false;
                continue;
            }

            if (currentKey is null)
            {
                throw new InvalidDataException(
                    $"Verse field found before a verse key in {path}");
            }

            var trimmedLine = line.Trim();
            var separatorIndex = trimmedLine.IndexOf(':');
            if (separatorIndex < 1)
            {
                throw new InvalidDataException(
                    $"Invalid field in verse '{currentKey}' in {path}");
            }

            var fieldName = trimmedLine[..separatorIndex];
            var serializedValue = trimmedLine[(separatorIndex + 1)..].Trim();
            switch (fieldName)
            {
                case "text" when !hasText:
                    currentText = ParseText(serializedValue, currentKey, path);
                    hasText = true;
                    break;
                case "tags" when !hasTags:
                    currentTags = ParseTags(serializedValue, currentKey, path);
                    hasTags = true;
                    break;
                default:
                    throw new InvalidDataException(
                        $"Unknown or duplicate field '{fieldName}' in verse '{currentKey}' in {path}");
            }
        }

        AddCurrentVerse();
        return verses.OrderBy(verse => verse.Number).ToArray();
    }

    private static string ParseVerseKey(string line, string path)
    {
        var trimmedLine = line.TrimEnd();
        if (!trimmedLine.EndsWith(':'))
        {
            throw new InvalidDataException($"Invalid verse key in {path}: {line}");
        }

        try
        {
            using var document = JsonDocument.Parse($"{{{trimmedLine}null}}");
            var properties = document.RootElement.EnumerateObject().ToArray();
            if (properties.Length != 1 || properties[0].Value.ValueKind != JsonValueKind.Null)
            {
                throw new InvalidDataException($"Invalid verse key in {path}: {line}");
            }

            return properties[0].Name;
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException($"Invalid verse key in {path}: {line}", exception);
        }
    }

    private static int ParseVerseNumber(string key, string keyPrefix, string path)
    {
        if (!key.StartsWith(keyPrefix, StringComparison.Ordinal)
            || !int.TryParse(
                key.AsSpan(keyPrefix.Length),
                NumberStyles.None,
                CultureInfo.InvariantCulture,
                out var verseNumber)
            || verseNumber < 1)
        {
            throw new InvalidDataException($"Invalid verse key '{key}' in {path}");
        }

        return verseNumber;
    }

    private static string ParseText(string serializedValue, string key, string path)
    {
        using var document = ParseJsonValue(serializedValue, "text", key, path);
        if (document.RootElement.ValueKind != JsonValueKind.String)
        {
            throw new InvalidDataException(
                $"Field 'text' in verse '{key}' must be a string in {path}");
        }

        return document.RootElement.GetString()!;
    }

    private static IReadOnlyList<string> ParseTags(
        string serializedValue,
        string key,
        string path)
    {
        using var document = ParseJsonValue(serializedValue, "tags", key, path);
        if (document.RootElement.ValueKind != JsonValueKind.Array)
        {
            throw new InvalidDataException(
                $"Field 'tags' in verse '{key}' must be an array in {path}");
        }

        var tags = new List<string>();
        foreach (var element in document.RootElement.EnumerateArray())
        {
            if (element.ValueKind != JsonValueKind.String)
            {
                throw new InvalidDataException(
                    $"Every tag in verse '{key}' must be a string in {path}");
            }

            tags.Add(element.GetString()!);
        }

        return tags;
    }

    private static JsonDocument ParseJsonValue(
        string serializedValue,
        string fieldName,
        string key,
        string path)
    {
        try
        {
            return JsonDocument.Parse(serializedValue);
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException(
                $"Field '{fieldName}' in verse '{key}' has an invalid value in {path}",
                exception);
        }
    }

    private string GetTranslationPath(BibleTranslation translation) =>
        Path.Combine(_rootPath, translation.Code);

    private string GetBookPath(BibleTranslation translation, BibleBook book) =>
        Path.Combine(GetTranslationPath(translation), book.Abbreviation);

    private string GetChapterPath(BibleTranslation translation, BibleBook book, int chapter) =>
        Path.Combine(
            GetBookPath(translation, book),
            $"{chapter.ToString(CultureInfo.InvariantCulture)}.yml");

    private readonly record struct ChapterKey(string Translation, string Book, int Chapter);
}
