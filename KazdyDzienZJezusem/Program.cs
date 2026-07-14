using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Globalization;
using System.Diagnostics;
using StackExchange.Redis;

Console.WriteLine("Starting...");

void Fail(string message)
{
    Console.WriteLine(message);
    Environment.ExitCode = 1;
}

if (args.Length == 0)
{
    Console.WriteLine("No verse arguments supplied; serving existing generated content only.");
}

var argTokens = args
    .SelectMany(arg => arg.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
    .ToList();

var verseArgs = new List<string>();
var generateAll = false;

foreach (var arg in argTokens)
{
    if (arg == "--all")
    {
        generateAll = true;
        continue;
    }

    if (arg.StartsWith("--topic", StringComparison.Ordinal))
    {
        Fail("Topic generation via command-line arguments is no longer supported. Use files in Topics/ instead.");
        return;
    }

    verseArgs.Add(arg);
}

if (generateAll && verseArgs.Count > 0)
{
    Fail("Use --all, or jhn3,16 [jhn3:17 ...] OR \"jhn3,16 jhn3:17\"");

    return;
}

var inputs = verseArgs;
var generateContent = generateAll || inputs.Count > 0;
ConnectionMultiplexer? redisConnection = generateContent ? ConnectionMultiplexer.Connect("localhost,allowAdmin=true") : null;
IDatabase? redis = redisConnection?.GetDatabase();

// Output paths
string basePath = "/data-out/Index";
string bibliaPath = Path.Combine(basePath, "Biblia");
string graecaPath = Path.Combine(basePath, "Graeca");
string strongPath = Path.Combine(basePath, "Strong");
string topicsPath = Path.Combine(basePath, "Topics");
string htmlPath = "/data-out/IndexHtml";
string sourceTopicsPath = Directory.Exists("/data-out/Topics")
    ? "/data-out/Topics"
    : Path.Combine(Directory.GetCurrentDirectory(), "Topics");

Directory.CreateDirectory(basePath);
Directory.CreateDirectory(bibliaPath);
Directory.CreateDirectory(graecaPath);
Directory.CreateDirectory(strongPath);

var obsidianPath = Path.Combine(basePath, ".obsidian");
if (Directory.Exists(obsidianPath))
{
    Directory.Delete(obsidianPath, recursive: true);
}

string NormalizeGreek(string s)
{
    if (string.IsNullOrEmpty(s)) return s;

    var formD = s.Normalize(NormalizationForm.FormD);
    var sb = new StringBuilder();

    foreach (var ch in formD)
    {
        var uc = Char.GetUnicodeCategory(ch);
        if (uc != UnicodeCategory.Control)
            sb.Append(ch);
    }

    return sb.ToString().Normalize(NormalizationForm.FormC);
}

string Clean(string s)
{
    return Regex.Replace(s, @"[.,;·]", "");
}

var BOOK_MAP = new Dictionary<string, (string eng, string pl)>
{
    ["mat"] = ("Matthew", "Ewangelia Mateusza"),
    ["mar"] = ("Mark", "Ewangelia Marka"),
    ["luk"] = ("Luke", "Ewangelia Łukasza"),
    ["jhn"] = ("John", "Ewangelia Jana"),
    ["act"] = ("Acts", "Dzieje Apostolskie"),
    ["rom"] = ("Romans", "List do Rzymian"),

    ["1co"] = ("1 Corinthians", "1 List do Koryntian"),
    ["2co"] = ("2 Corinthians", "2 List do Koryntian"),
    ["gal"] = ("Galatians", "List do Galacjan"),
    ["eph"] = ("Ephesians", "List do Efezjan"),
    ["php"] = ("Philippians", "List do Filipian"),
    ["col"] = ("Colossians", "List do Kolosan"),

    ["1th"] = ("1 Thessalonians", "1 List do Tesaloniczan"),
    ["2th"] = ("2 Thessalonians", "2 List do Tesaloniczan"),

    ["1ti"] = ("1 Timothy", "1 List do Tymoteusza"),
    ["2ti"] = ("2 Timothy", "2 List do Tymoteusza"),

    ["tit"] = ("Titus", "List do Tytusa"),
    ["phm"] = ("Philemon", "List do Filemona"),
    ["heb"] = ("Hebrews", "List do Hebrajczyków"),
    ["jas"] = ("James", "List Jakuba"),

    ["1pe"] = ("1 Peter", "1 List Piotra"),
    ["2pe"] = ("2 Peter", "2 List Piotra"),

    ["1jn"] = ("1 John", "1 List Jana"),
    ["2jn"] = ("2 John", "2 List Jana"),
    ["3jn"] = ("3 John", "3 List Jana"),

    ["jud"] = ("Jude", "List Judy"),
    ["rev"] = ("Revelation", "Objawienie Jana"),
};

var GNT_BOOK_MAP = new Dictionary<string, string>
{
    ["mat"] = "Matt",
    ["mar"] = "Mark",
    ["luk"] = "Luke",
    ["jhn"] = "John",
    ["act"] = "Acts",
    ["rom"] = "Rom",
    ["1co"] = "1Cor",
    ["2co"] = "2Cor",
    ["gal"] = "Gal",
    ["eph"] = "Eph",
    ["php"] = "Phil",
    ["col"] = "Col",
    ["1th"] = "1Thess",
    ["2th"] = "2Thess",
    ["1ti"] = "1Tim",
    ["2ti"] = "2Tim",
    ["tit"] = "Titus",
    ["phm"] = "Phlm",
    ["heb"] = "Heb",
    ["jas"] = "Jas",
    ["1pe"] = "1Pet",
    ["2pe"] = "2Pet",
    ["1jn"] = "1John",
    ["2jn"] = "2John",
    ["3jn"] = "3John",
    ["jud"] = "Jude",
    ["rev"] = "Rev",
};

var NT_BOOK_SEQUENCE = new[]
{
    "mat", "mar", "luk", "jhn", "act", "rom",
    "1co", "2co", "gal", "eph", "php", "col",
    "1th", "2th", "1ti", "2ti", "tit", "phm", "heb", "jas",
    "1pe", "2pe", "1jn", "2jn", "3jn", "jud", "rev",
};

var NT_BOOK_ORDER = NT_BOOK_SEQUENCE
    .Select((bookCode, index) => new { bookCode, index })
    .ToDictionary(entry => entry.bookCode, entry => entry.index, StringComparer.Ordinal);

var BOOK_MAP_BY_PL = BOOK_MAP.ToDictionary(entry => entry.Value.pl, entry => entry.Value.eng, StringComparer.Ordinal);

bool TryGetUbgQuote(string reference, out string quote)
{
    quote = "";

    var match = Regex.Match(reference.Trim(), @"^(.+?)\s+(\d+),(\d+)$");
    if (!match.Success)
    {
        return false;
    }

    var bookPl = match.Groups[1].Value.Trim();
    if (!BOOK_MAP_BY_PL.TryGetValue(bookPl, out var bookEng))
    {
        return false;
    }

    var chapter = int.Parse(match.Groups[2].Value, CultureInfo.InvariantCulture);
    var verse = int.Parse(match.Groups[3].Value, CultureInfo.InvariantCulture);
    var keyUBG = $"ubg:{bookEng}:{chapter}:{verse}";
    var ubg = redis.StringGet(keyUBG);

    if (ubg.IsNullOrEmpty)
    {
        return false;
    }

    quote = ubg.ToString();
    return true;
}

string ProcessTopicContent(string content)
{
    var normalized = content.Replace("\r\n", "\n").Replace('\r', '\n');
    var lines = normalized.Split('\n');
    var sb = new StringBuilder();

    for (var i = 0; i < lines.Length; i++)
    {
        var line = lines[i];
        sb.AppendLine(line);

        var linkMatch = Regex.Match(line.Trim(), @"^\[\[(.+)\]\]$");
        if (linkMatch.Success && TryGetUbgQuote(linkMatch.Groups[1].Value, out var quote))
        {
            sb.AppendLine($"> {quote}");
        }
    }

    return sb.ToString();
}

void CopyDirectory(string sourceDir, string destinationDir)
{
    Directory.CreateDirectory(destinationDir);

    foreach (var directory in Directory.GetDirectories(sourceDir, "*", SearchOption.AllDirectories))
    {
        var relativePath = Path.GetRelativePath(sourceDir, directory);
        Directory.CreateDirectory(Path.Combine(destinationDir, relativePath));
    }

    foreach (var file in Directory.GetFiles(sourceDir, "*", SearchOption.AllDirectories))
    {
        var relativePath = Path.GetRelativePath(sourceDir, file);
        File.Copy(file, Path.Combine(destinationDir, relativePath), overwrite: true);
    }
}

var generatedTitles = new List<string>();
var generatedBookIndexLinks = new List<string>();

bool TryGenerateVerse(string bookCode, int chapter, int verse)
{
    if (!BOOK_MAP.TryGetValue(bookCode, out var bookInfo))
    {
        Fail($"Unknown book: {bookCode}");
        return false;
    }

    var (bookEng, bookPl) = bookInfo;

    if (!GNT_BOOK_MAP.TryGetValue(bookCode, out var bookOsis))
    {
        Fail($"Missing GNT mapping for book: {bookCode}");
        return false;
    }

    string keyTR = $"gnt:{bookOsis}:{chapter}:{verse}";
    string keyTNP = $"tnp:{bookEng}:{chapter}:{verse}";
    string keyUBG = $"ubg:{bookEng}:{chapter}:{verse}";
    string keyKJV = $"kjv:{bookEng}:{chapter}:{verse}";

    var trRaw = redis!.StringGet(keyTR);
    var tnp = redis.StringGet(keyTNP);
    var ubg = redis.StringGet(keyUBG);
    var kjv = redis.StringGet(keyKJV);

    if (trRaw.IsNullOrEmpty)
    {
        Fail($"NOT FOUND: {keyTR}");
        return false;
    }

    using var doc = JsonDocument.Parse(trRaw!.ToString());
    var root = doc.RootElement;
    var words = root.GetProperty("words");

    var greekWords = new List<string>();

    foreach (var w in words.EnumerateArray())
    {
        var greek = NormalizeGreek(w.GetProperty("greek").GetString() ?? "");
        greek = Clean(greek);
        greekWords.Add($"[[{greek}]]");
    }

    string greekLine = string.Join(" ", greekWords);

    string urlTR = $"https://www.blueletterbible.org/tr/{bookCode}/{chapter}/{verse}/";
    string urlKJV = $"https://www.blueletterbible.org/kjv/{bookCode}/{chapter}/{verse}/";
    string urlTNP = $"https://biblia-online.pl/Biblia/PrzekladTorunski/{bookPl.Replace(" ", "-")}/{chapter}/{verse}";
    string urlUBG = $"https://biblia-online.pl/Biblia/UwspolczesnionaBibliaGdanska/{bookPl.Replace(" ", "-")}/{chapter}/{verse}";

    string title = $"{bookPl} {chapter},{verse}";

    var sbOut = new StringBuilder();

    void AppendVerseContent(StringBuilder sb, string headingPrefix)
    {
        sb.AppendLine($"{headingPrefix} {title}");
        sb.AppendLine();

        sb.AppendLine($"[TR]({urlTR})");
        sb.AppendLine($"> {greekLine}");
        sb.AppendLine();

        sb.AppendLine($"[KJV]({urlKJV})");
        sb.AppendLine($"> {kjv}");
        sb.AppendLine();

        sb.AppendLine($"[TNP]({urlTNP})");
        sb.AppendLine($"> {tnp}");
        sb.AppendLine();

        sb.AppendLine($"[UBG]({urlUBG})");
        sb.AppendLine($"> {ubg}");
    }

    AppendVerseContent(sbOut, "#");

    File.WriteAllText(
        Path.Combine(bibliaPath, $"{title}.md"),
        sbOut.ToString(),
        Encoding.UTF8
    );

    foreach (var w in words.EnumerateArray())
    {
        var greek = NormalizeGreek(w.GetProperty("greek").GetString() ?? "");
        greek = Clean(greek);

        var lemma = NormalizeGreek(w.GetProperty("dictionary_form").GetString() ?? "");
        var strong = $"G{w.GetProperty("strong").GetInt32()}";

        File.WriteAllText(
            Path.Combine(graecaPath, $"{greek}.md"),
            $@"# {greek}

lemma: [[{lemma}]]
strong: [[{strong}]]

transliteration: {w.GetProperty("transliteration").GetString()}
grammar: {w.GetProperty("grammar_human").GetString()}
definition: {w.GetProperty("definition").GetString()}
",
            Encoding.UTF8
        );

        var strongFile = Path.Combine(strongPath, $"{strong}.md");

        if (!File.Exists(strongFile))
        {
            File.WriteAllText(
                strongFile,
                $@"# {strong}

lemma: [[{lemma}]]
definition: {w.GetProperty("definition").GetString()}
",
                Encoding.UTF8
            );
        }
    }

    Console.WriteLine($"Saved: {title}.md");
    generatedTitles.Add(title);
    return true;
}

var verseReferences = new List<(string BookCode, int Chapter, int Verse)>();

if (generateAll)
{
    if (redisConnection is null)
    {
        Fail("Redis connection is required for --all.");
        return;
    }

    var endPoint = redisConnection.GetEndPoints().FirstOrDefault();
    if (endPoint is null)
    {
        Fail("No Redis endpoint is available.");
        return;
    }

    var server = redisConnection.GetServer(endPoint);
    var versePattern = new Regex(@"^gnt:([^:]+):(\d+):(\d+)$", RegexOptions.Compiled);

    foreach (var key in server.Keys(pattern: "gnt:*"))
    {
        var match = versePattern.Match(key.ToString());
        if (!match.Success)
        {
            continue;
        }

        var osis = match.Groups[1].Value;
        var bookCode = GNT_BOOK_MAP.FirstOrDefault(entry => entry.Value == osis).Key;
        if (string.IsNullOrEmpty(bookCode))
        {
            Fail($"Unknown GNT book mapping: {osis}");
            return;
        }

        var chapter = int.Parse(match.Groups[2].Value, CultureInfo.InvariantCulture);
        var verse = int.Parse(match.Groups[3].Value, CultureInfo.InvariantCulture);
        verseReferences.Add((bookCode, chapter, verse));
    }

    verseReferences = verseReferences
        .OrderBy(reference => NT_BOOK_ORDER[reference.BookCode])
        .ThenBy(reference => reference.Chapter)
        .ThenBy(reference => reference.Verse)
        .ToList();
}
else
{
    foreach (var rawInput in inputs)
    {
        var input = rawInput.ToLower().Replace(":", ",");
        var match = Regex.Match(input, @"^([1-3]?[a-z]+)(\d+),(\d+)$");

        if (!match.Success)
        {
            Fail($"Invalid format: {rawInput}. Example: jhn3,16");
            return;
        }

        var bookCode = match.Groups[1].Value;
        var chapter = int.Parse(match.Groups[2].Value);
        var verse = int.Parse(match.Groups[3].Value);

        verseReferences.Add((bookCode, chapter, verse));
    }
}

for (var i = 0; i < verseReferences.Count; i++)
{
    var (bookCode, chapter, verse) = verseReferences[i];

    if (generateAll)
    {
        var index = i + 1;
        if (index == 1)
        {
            Console.WriteLine($"Generating full NT: {verseReferences.Count} verses");
        }

        if (index == 1 || index == verseReferences.Count || index % 100 == 0)
        {
            var bookLabel = BOOK_MAP[bookCode].eng;
            Console.WriteLine($"Progress: {index}/{verseReferences.Count} - {bookLabel} {chapter}:{verse}");
        }
    }

    if (!TryGenerateVerse(bookCode, chapter, verse))
    {
        return;
    }
}

if (generateAll)
{
    foreach (var group in verseReferences.GroupBy(reference => reference.BookCode))
    {
        var bookCode = group.Key;
        var (bookEng, _) = BOOK_MAP[bookCode];
        var bookPath = Path.Combine(bibliaPath, $"{bookEng}.md");
        var bookIndex = new StringBuilder();

        bookIndex.AppendLine($"# {bookEng}");
        bookIndex.AppendLine();

        foreach (var chapterGroup in group.GroupBy(reference => reference.Chapter))
        {
            bookIndex.AppendLine($"## Chapter {chapterGroup.Key}");
            bookIndex.AppendLine();

            foreach (var reference in chapterGroup)
            {
                var title = $"{BOOK_MAP[bookCode].pl} {reference.Chapter},{reference.Verse}";
                bookIndex.AppendLine($"- [[Biblia/{title}|{reference.Chapter}:{reference.Verse}]]");
            }

            bookIndex.AppendLine();
        }

        File.WriteAllText(bookPath, bookIndex.ToString(), Encoding.UTF8);
        generatedBookIndexLinks.Add($"- [[Biblia/{bookEng}|{bookEng}]]");
    }
}

var topicFiles = Directory.Exists(sourceTopicsPath)
    ? Directory.GetFiles(sourceTopicsPath, "*.md", SearchOption.AllDirectories)
        .OrderBy(path => Path.GetRelativePath(sourceTopicsPath, path), StringComparer.Ordinal)
        .ToList()
    : new List<string>();

var generatedTopicLinks = new List<string>();

if (topicFiles.Count > 0)
{
    if (Directory.Exists(topicsPath))
    {
        Directory.Delete(topicsPath, recursive: true);
    }

    Directory.CreateDirectory(topicsPath);

    foreach (var sourceFile in topicFiles)
    {
        var relativePath = Path.GetRelativePath(sourceTopicsPath, sourceFile);
        var destinationFile = Path.Combine(topicsPath, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(destinationFile)!);

        var content = File.ReadAllText(sourceFile, Encoding.UTF8);
        var processedContent = ProcessTopicContent(content);
        File.WriteAllText(destinationFile, processedContent, Encoding.UTF8);

        Console.WriteLine($"Saved topic: {relativePath}");
        generatedTopicLinks.Add(relativePath);
    }

    var topicIndex = new StringBuilder();
    topicIndex.AppendLine("# Topics");
    topicIndex.AppendLine();

    foreach (var topicLink in generatedTopicLinks)
    {
        var topicTitle = Path.GetFileNameWithoutExtension(topicLink);
        var topicTarget = Path.ChangeExtension(topicLink, null)!.Replace(Path.DirectorySeparatorChar, '/');
        topicIndex.AppendLine($"- [[Topics/{topicTarget}|{topicTitle}]]");
    }

    File.WriteAllText(Path.Combine(topicsPath, "index.md"), topicIndex.ToString(), Encoding.UTF8);
}

var generatedLinks = new StringBuilder();
if (generateContent)
{
    if (generateAll)
    {
        generatedLinks.AppendLine("Browse by book below.");
    }
    else
    {
        foreach (var title in generatedTitles)
        {
            generatedLinks.AppendLine($"- [[Biblia/{title}|{title}]]");
        }
    }
}

var topicIndexLink = generateContent && topicFiles.Count > 0 ? "- [[Topics]]\n" : "";

if (generateContent)
{
    File.WriteAllText(
        Path.Combine(basePath, "index.md"),
        $@"# Kazdy Dzien Z Jezusem

## {(generateAll ? "New Testament" : "Ostatnie Wersety")}

{generatedLinks}

{(generateAll ? "### Books\n\n" + string.Join(Environment.NewLine, generatedBookIndexLinks) + Environment.NewLine : "")}

## Indeksy

- [[Biblia]]
- [[Graeca]]
- [[Strong]]
{topicIndexLink}",
        Encoding.UTF8
    );
}

if (Directory.Exists(htmlPath))
{
    Directory.Delete(htmlPath, recursive: true);
}

Directory.CreateDirectory(htmlPath);

string quartzSitePath = "/opt/quartz-site";
string quartzContentPath = Path.Combine(quartzSitePath, "content");

if (Directory.Exists(quartzContentPath))
{
    Directory.Delete(quartzContentPath, recursive: true);
}

CopyDirectory(basePath, quartzContentPath);

var quartzProcess = new ProcessStartInfo
{
    FileName = "npx",
    WorkingDirectory = quartzSitePath,
    RedirectStandardOutput = true,
    RedirectStandardError = true,
    UseShellExecute = false,
};

quartzProcess.ArgumentList.Add("quartz");
quartzProcess.ArgumentList.Add("build");
quartzProcess.ArgumentList.Add("--output");
quartzProcess.ArgumentList.Add(htmlPath);

Console.WriteLine($"Building Quartz site in: {quartzSitePath}");
using var quartz = Process.Start(quartzProcess);

if (quartz is null)
{
    Fail("Quartz build failed to start.");
    return;
}

var quartzTimeout = TimeSpan.FromHours(12);
Console.WriteLine($"Waiting for Quartz build to finish (timeout: {quartzTimeout.TotalHours:0} hours)...");

if (!quartz.WaitForExit((int)quartzTimeout.TotalMilliseconds))
{
    try
    {
        quartz.Kill(entireProcessTree: true);
    }
    catch
    {
        // Ignore kill errors; we still want to surface the timeout as the primary failure.
    }

    try
    {
        quartz.WaitForExit();
    }
    catch
    {
        // Ignore wait errors after a forced kill.
    }

    var quartzTimedOutStdOut = quartz.StandardOutput.ReadToEnd();
    var quartzTimedOutStdErr = quartz.StandardError.ReadToEnd();

    Console.WriteLine($"Quartz build timed out after {quartzTimeout.TotalHours:0} hours.");
    if (!string.IsNullOrWhiteSpace(quartzTimedOutStdOut))
        Console.WriteLine(quartzTimedOutStdOut);
    if (!string.IsNullOrWhiteSpace(quartzTimedOutStdErr))
        Console.WriteLine(quartzTimedOutStdErr);

    Environment.Exit(1);
    return;
}

var quartzStdOut = quartz.StandardOutput.ReadToEnd();
var quartzStdErr = quartz.StandardError.ReadToEnd();
quartz.WaitForExit();

if (quartz.ExitCode != 0)
{
    Fail("Quartz build failed.");
    if (!string.IsNullOrWhiteSpace(quartzStdOut))
        Console.WriteLine(quartzStdOut);
    if (!string.IsNullOrWhiteSpace(quartzStdErr))
        Console.WriteLine(quartzStdErr);
    Environment.Exit(1);
    return;
}

if (!File.Exists(Path.Combine(htmlPath, "index.html")))
{
    Fail($"Quartz build completed, but {Path.Combine(htmlPath, "index.html")} is missing.");
    if (!string.IsNullOrWhiteSpace(quartzStdOut))
        Console.WriteLine(quartzStdOut);
    if (!string.IsNullOrWhiteSpace(quartzStdErr))
        Console.WriteLine(quartzStdErr);
    Environment.Exit(1);
    return;
}

Console.WriteLine($"Quartz HTML generated in: {htmlPath}");
