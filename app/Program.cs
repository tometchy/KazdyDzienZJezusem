using System.Text;
using System.Text.Json;
using StackExchange.Redis;

var redis = await ConnectionMultiplexer.ConnectAsync("localhost");
var db = redis.GetDatabase();

if (args.Length == 0)
{
    Console.WriteLine("Usage: jhn1,1");
    return;
}

var input = args[0].ToLower();

var match = System.Text.RegularExpressions.Regex.Match(input, @"^([a-z0-9]+)(\d+),(\d+)$");

if (!match.Success)
{
    Console.WriteLine("Invalid format. Example: jhn3,16");
    return;
}

var bookCode = match.Groups[1].Value;
var chapter = int.Parse(match.Groups[2].Value);
var verse = int.Parse(match.Groups[3].Value);

var BOOK_MAP = new Dictionary<string, (string en, string pl, int num, string slug)>
{
    ["mat"] = ("Matthew", "Ewangelia Mateusza", 40, "Ewangelia-Mateusza"),
    ["mar"] = ("Mark", "Ewangelia Marka", 41, "Ewangelia-Marka"),
    ["luk"] = ("Luke", "Ewangelia Łukasza", 42, "Ewangelia-Lukasza"),
    ["jhn"] = ("John", "Ewangelia Jana", 43, "Ewangelia-Jana"),
    ["act"] = ("Acts", "Dzieje Apostolskie", 44, "Dzieje-Apostolskie"),
    ["rom"] = ("Romans", "List do Rzymian", 45, "List-do-Rzymian"),

    ["1co"] = ("1 Corinthians", "1 List do Koryntian", 46, "1-List-do-Koryntian"),
    ["2co"] = ("2 Corinthians", "2 List do Koryntian", 47, "2-List-do-Koryntian"),
    ["gal"] = ("Galatians", "List do Galacjan", 48, "List-do-Galacjan"),
    ["eph"] = ("Ephesians", "List do Efezjan", 49, "List-do-Efezjan"),
    ["php"] = ("Philippians", "List do Filipian", 50, "List-do-Filipian"),
    ["col"] = ("Colossians", "List do Kolosan", 51, "List-do-Kolosan"),

    ["1th"] = ("1 Thessalonians", "1 List do Tesaloniczan", 52, "1-List-do-Tesaloniczan"),
    ["2th"] = ("2 Thessalonians", "2 List do Tesaloniczan", 53, "2-List-do-Tesaloniczan"),

    ["1ti"] = ("1 Timothy", "1 List do Tymoteusza", 54, "1-List-do-Tymoteusza"),
    ["2ti"] = ("2 Timothy", "2 List do Tymoteusza", 55, "2-List-do-Tymoteusza"),

    ["tit"] = ("Titus", "List do Tytusa", 56, "List-do-Tytusa"),
    ["phm"] = ("Philemon", "List do Filemona", 57, "List-do-Filemona"),
    ["heb"] = ("Hebrews", "List do Hebrajczyków", 58, "List-do-Hebrajczykow"),
    ["jas"] = ("James", "List Jakuba", 59, "List-Jakuba"),

    ["1pe"] = ("1 Peter", "1 List Piotra", 60, "1-List-do-Piotra"),
    ["2pe"] = ("2 Peter", "2 List Piotra", 61, "2-List-do-Piotra"),

    ["1jn"] = ("1 John", "1 List Jana", 62, "1-List-do-Jana"),
    ["2jn"] = ("2 John", "2 List Jana", 63, "2-List-do-Jana"),
    ["3jn"] = ("3 John", "3 List Jana", 64, "3-List-do-Jana"),

    ["jud"] = ("Jude", "List Judy", 65, "List-Judy"),
    ["rev"] = ("Revelation", "Objawienie Jana", 66, "Objawienie-Jana"),
};

if (!BOOK_MAP.TryGetValue(bookCode, out var book))
{
    Console.WriteLine("Unknown book");
    return;
}

string keyTR = $"gnt:{book.en}:{chapter}:{verse}";
string keyTNP = $"tnp:{book.en}:{chapter}:{verse}";
string keyUBG = $"ubg:{book.en}:{chapter}:{verse}";
string keyKJV = $"kjv:{book.en}:{chapter}:{verse}";

var trJson = await db.StringGetAsync(keyTR);
var tnp = await db.StringGetAsync(keyTNP);
var ubg = await db.StringGetAsync(keyUBG);
var kjv = await db.StringGetAsync(keyKJV);

if (trJson.IsNull)
{
    Console.WriteLine("NOT FOUND");
    return;
}

var doc = JsonDocument.Parse(trJson.ToString());
var words = doc.RootElement.GetProperty("words");

// 🔥 KLUCZOWE
string Normalize(string s) => s.Normalize(NormalizationForm.FormC);

string Clean(string s)
{
    s = Normalize(s);
    return System.Text.RegularExpressions.Regex.Replace(s, @"[.,;·]", "");
}

var sb = new StringBuilder();

foreach (var w in words.EnumerateArray())
{
    var greek = Clean(w.GetProperty("greek").GetString()!);
    sb.Append($"[[{greek}]] ");
}

var baseDir = "/data-out/Index";
var bibleDir = Path.Combine(baseDir, "Biblia");
var greekDir = Path.Combine(baseDir, "Graeca");

Directory.CreateDirectory(bibleDir);
Directory.CreateDirectory(greekDir);

foreach (var w in words.EnumerateArray())
{
    var greek = Clean(w.GetProperty("greek").GetString()!);
    var lemma = Clean(w.GetProperty("dictionary_form").GetString()!);

    var path = Path.Combine(greekDir, $"{greek}.md");

    var content = $"""
| greek | {greek} |
|---|---|
| transliteration | {w.GetProperty("transliteration")} |
| strong | {w.GetProperty("strong")} |
| grammar | {w.GetProperty("grammar")} |
| grammar_human | {w.GetProperty("grammar_human")} |
| dictionary_form | [[{lemma}]] |
| definition | {w.GetProperty("definition")} |
""";

    File.WriteAllText(path, content, Encoding.UTF8);
}

string urlKJV = $"https://www.blueletterbible.org/kjv/{bookCode}/{chapter}/{verse}/";
string urlTNP = $"https://biblia-online.pl/Biblia/PrzekladTorunski/{book.slug}/{chapter}/{verse}";
string urlUBG = $"https://biblia-online.pl/Biblia/UwspolczesnionaBibliaGdanska/{book.slug}/{chapter}/{verse}";

var contentFinal = $"""
TR
> {sb.ToString().Trim()}

[KJV]({urlKJV})
> {kjv}

[TNP]({urlTNP})
> {tnp}

[UBG]({urlUBG})
> {ubg}
""";

var filename = $"{book.pl} {chapter},{verse}.md";
var filePath = Path.Combine(bibleDir, filename);

File.WriteAllText(filePath, contentFinal, Encoding.UTF8);

Console.WriteLine("Saved: " + filename);
