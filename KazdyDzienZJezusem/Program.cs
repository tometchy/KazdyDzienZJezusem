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
    Fail("Usage: jhn3,16 [jhn3:17 ...] [--tag tag-name] OR \"jhn3,16 jhn3:17\"");
    return;
}

var argTokens = args
    .SelectMany(arg => arg.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
    .ToList();

string? tagName = null;
var verseArgs = new List<string>();

for (var i = 0; i < argTokens.Count; i++)
{
    var arg = argTokens[i];

    if (arg == "--tag")
    {
        if (i + 1 >= argTokens.Count)
        {
            Fail("Missing tag name after --tag.");
            return;
        }

        tagName = argTokens[++i].Trim();
        continue;
    }

    if (arg.StartsWith("--tag=", StringComparison.Ordinal))
    {
        tagName = arg["--tag=".Length..].Trim();
        continue;
    }

    verseArgs.Add(arg);
}

if (string.IsNullOrWhiteSpace(tagName))
{
    tagName = null;
}

if (tagName is not null && (tagName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 || tagName.Contains(Path.DirectorySeparatorChar) || tagName.Contains(Path.AltDirectorySeparatorChar)))
{
    Fail($"Invalid tag name: {tagName}");
    return;
}

var inputs = verseArgs;

if (inputs.Count == 0)
{
    Fail("Usage: jhn3,16 [jhn3:17 ...] [--tag tag-name] OR \"jhn3,16 jhn3:17\"");
    return;
}

var redis = ConnectionMultiplexer.Connect("localhost").GetDatabase();

// Output paths
string basePath = "/data-out/Index";
string bibliaPath = Path.Combine(basePath, "Biblia");
string graecaPath = Path.Combine(basePath, "Graeca");
string strongPath = Path.Combine(basePath, "Strong");
string tagsPath = Path.Combine(basePath, "Tags");
string htmlPath = "/data-out/IndexHtml";

Directory.CreateDirectory(basePath);
Directory.CreateDirectory(bibliaPath);
Directory.CreateDirectory(graecaPath);
Directory.CreateDirectory(strongPath);

if (tagName is not null)
{
    Directory.CreateDirectory(tagsPath);
}

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
var tagContent = new StringBuilder();

if (tagName is not null)
{
    tagContent.AppendLine($"# {tagName}");
}

foreach (var rawInput in inputs)
{
    // ✅ obsługa , i :
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

    // 📚 NT
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

    if (!BOOK_MAP.ContainsKey(bookCode))
    {
        Fail($"Unknown book: {bookCode}");
        return;
    }

    var (bookEng, bookPl) = BOOK_MAP[bookCode];

    // 🔑 KLUCZE (FIX!)
    var bookOsis = GNT_BOOK_MAP[bookCode];
    string keyTR = $"gnt:{bookOsis}:{chapter}:{verse}";
    string keyTNP = $"tnp:{bookEng}:{chapter}:{verse}";
    string keyUBG = $"ubg:{bookEng}:{chapter}:{verse}";
    string keyKJV = $"kjv:{bookEng}:{chapter}:{verse}";

    var trRaw = redis.StringGet(keyTR);
    var tnp = redis.StringGet(keyTNP);
    var ubg = redis.StringGet(keyUBG);
    var kjv = redis.StringGet(keyKJV);

    if (trRaw.IsNullOrEmpty)
    {
        Fail($"NOT FOUND: {keyTR}");
        return;
    }

    // ✅ FIX build
    using var doc = JsonDocument.Parse(trRaw!.ToString());
    var root = doc.RootElement;
    var words = root.GetProperty("words");

    // 🧾 TR (FORMA)
    var greekWords = new List<string>();

    foreach (var w in words.EnumerateArray())
    {
        var greek = NormalizeGreek(w.GetProperty("greek").GetString() ?? "");
        greek = Clean(greek);
        greekWords.Add($"[[{greek}]]");
    }

    string greekLine = string.Join(" ", greekWords);

    // 🔗 linki
    string urlTR = $"https://www.blueletterbible.org/tr/{bookCode}/{chapter}/{verse}/";
    string urlKJV = $"https://www.blueletterbible.org/kjv/{bookCode}/{chapter}/{verse}/";
    string urlTNP = $"https://biblia-online.pl/Biblia/PrzekladTorunski/{bookPl.Replace(" ", "-")}/{chapter}/{verse}";
    string urlUBG = $"https://biblia-online.pl/Biblia/UwspolczesnionaBibliaGdanska/{bookPl.Replace(" ", "-")}/{chapter}/{verse}";

    string title = $"{bookPl} {chapter},{verse}";

    // 📄 OUTPUT
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

    if (tagName is not null)
    {
        tagContent.AppendLine();
        AppendVerseContent(tagContent, "##");
    }

    // 📄 zapis wersetu
    File.WriteAllText(
        Path.Combine(bibliaPath, $"{title}.md"),
        sbOut.ToString(),
        Encoding.UTF8
    );

    // 📄 słowa
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
}

if (tagName is not null)
{
    File.WriteAllText(
        Path.Combine(tagsPath, $"{tagName}.md"),
        tagContent.ToString(),
        Encoding.UTF8
    );

    Console.WriteLine($"Saved tag: {tagName}.md");
}

var generatedLinks = new StringBuilder();
foreach (var title in generatedTitles)
{
    generatedLinks.AppendLine($"- [[Biblia/{title}|{title}]]");
}

var tagIndexLink = tagName is null ? "" : "- [[Tags]]\n";

File.WriteAllText(
    Path.Combine(basePath, "index.md"),
    $@"# Kazdy Dzien Z Jezusem

## Ostatnie Wersety

{generatedLinks}

## Indeksy

- [[Biblia]]
- [[Graeca]]
- [[Strong]]
{tagIndexLink}",
    Encoding.UTF8
);

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
    return;
}

if (!File.Exists(Path.Combine(htmlPath, "index.html")))
{
    Fail($"Quartz build completed, but {Path.Combine(htmlPath, "index.html")} is missing.");
    if (!string.IsNullOrWhiteSpace(quartzStdOut))
        Console.WriteLine(quartzStdOut);
    if (!string.IsNullOrWhiteSpace(quartzStdErr))
        Console.WriteLine(quartzStdErr);
    return;
}

Console.WriteLine($"Quartz HTML generated in: {htmlPath}");
