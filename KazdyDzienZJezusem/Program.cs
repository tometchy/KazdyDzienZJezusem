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
    Fail("Usage: jhn3,16 [jhn3:17 ...] OR \"jhn3,16 jhn3:17\"");
    return;
}

var inputs = args.SelectMany(arg => arg.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));

var redis = ConnectionMultiplexer.Connect("localhost").GetDatabase();

// Output paths
string basePath = "/data-out/Index";
string bibliaPath = Path.Combine(basePath, "Biblia");
string graecaPath = Path.Combine(basePath, "Graeca");
string strongPath = Path.Combine(basePath, "Strong");
string htmlPath = "/data-out/IndexHtml";

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

foreach (var rawInput in inputs)
{
    // ✅ obsługa , i :
    var input = rawInput.ToLower().Replace(":", ",");

    var match = Regex.Match(input, @"^([a-z0-9]+)(\d+),(\d+)$");

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

    if (!BOOK_MAP.ContainsKey(bookCode))
    {
        Fail($"Unknown book: {bookCode}");
        return;
    }

    var (bookEng, bookPl) = BOOK_MAP[bookCode];

    // 🔑 KLUCZE (FIX!)
    string keyTR = $"gnt:{bookEng}:{chapter}:{verse}";
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

    sbOut.AppendLine($"# {title}");
    sbOut.AppendLine();

    sbOut.AppendLine($"[TR]({urlTR})");
    sbOut.AppendLine($"> {greekLine}");
    sbOut.AppendLine();

    sbOut.AppendLine($"[KJV]({urlKJV})");
    sbOut.AppendLine($"> {kjv}");
    sbOut.AppendLine();

    sbOut.AppendLine($"[TNP]({urlTNP})");
    sbOut.AppendLine($"> {tnp}");
    sbOut.AppendLine();

    sbOut.AppendLine($"[UBG]({urlUBG})");
    sbOut.AppendLine($"> {ubg}");

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

var generatedLinks = new StringBuilder();
foreach (var title in generatedTitles)
{
    generatedLinks.AppendLine($"- [[Biblia/{title}|{title}]]");
}

File.WriteAllText(
    Path.Combine(basePath, "index.md"),
    $@"# Kazdy Dzien Z Jezusem

## Ostatnie Wersety

{generatedLinks}

## Indeksy

- [[Biblia]]
- [[Graeca]]
- [[Strong]]
",
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
