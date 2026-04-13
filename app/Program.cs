using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Globalization;
using StackExchange.Redis;

var redis = ConnectionMultiplexer.Connect("localhost").GetDatabase();

if (args.Length == 0)
{
    Console.WriteLine("Usage: jhn1,1");
    return;
}

var input = args[0].ToLower();
var match = Regex.Match(input, @"^([a-z0-9]+)(\d+),(\d+)$");

if (!match.Success)
{
    Console.WriteLine("Invalid format. Example: jhn3,16");
    return;
}

var bookCode = match.Groups[1].Value;
var chapter = int.Parse(match.Groups[2].Value);
var verse = int.Parse(match.Groups[3].Value);

// ✅ PEŁNY NT
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
    Console.WriteLine("Unknown book");
    return;
}

var (bookEng, bookPl) = BOOK_MAP[bookCode];

string keyTR = $"tr:{bookEng}:{chapter}:{verse}";
string keyTNP = $"tnp:{bookEng}:{chapter}:{verse}";
string keyUBG = $"ubg:{bookEng}:{chapter}:{verse}";
string keyKJV = $"kjv:{bookEng}:{chapter}:{verse}";

var trRaw = redis.StringGet(keyTR);
var tnp = redis.StringGet(keyTNP);
var ubg = redis.StringGet(keyUBG);
var kjv = redis.StringGet(keyKJV);

if (trRaw.IsNullOrEmpty)
{
    Console.WriteLine("NOT FOUND");
    return;
}

using var doc = JsonDocument.Parse(trRaw!);
var root = doc.RootElement;
var words = root.GetProperty("words");

// 🔥 NAJWAŻNIEJSZE — poprawiona normalizacja
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

// ✅ budowa TR (forma oryginalna, NIE lemma)
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

// 📁 ścieżki (host mount)
string basePath = "/data-out/Index";
string bibliaPath = Path.Combine(basePath, "Biblia");
string graecaPath = Path.Combine(basePath, "Graeca");

Directory.CreateDirectory(bibliaPath);
Directory.CreateDirectory(graecaPath);

// 📄 zapis wersetu
string filePath = Path.Combine(bibliaPath, $"{title}.md");
File.WriteAllText(filePath, sbOut.ToString(), Encoding.UTF8);

// 📄 pliki greckie
foreach (var w in words.EnumerateArray())
{
    var greek = NormalizeGreek(w.GetProperty("greek").GetString() ?? "");
    greek = Clean(greek);

    string gPath = Path.Combine(graecaPath, $"{greek}.md");

    var content = new StringBuilder();
    content.AppendLine($"# {greek}");
    content.AppendLine();

    content.AppendLine($"**transliteration:** {w.GetProperty("transliteration").GetString()}");
    content.AppendLine($"**grammar:** {w.GetProperty("grammar_human").GetString()}");
    content.AppendLine($"**strong:** {w.GetProperty("strong").GetInt32()}");
    content.AppendLine($"**definition:** {w.GetProperty("definition").GetString()}");

    File.WriteAllText(gPath, content.ToString(), Encoding.UTF8);
}

Console.WriteLine($"Saved: {title}.md");
