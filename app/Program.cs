using StackExchange.Redis;
using System.Text.RegularExpressions;
using System.Text;
using System.Text.Json;

if (args.Length != 1)
{
    Console.WriteLine("Usage: jhn1,1");
    return;
}

var input = args[0].ToLower().Trim();

var match = Regex.Match(input, @"^([0-9]?[a-z]+)(\d+),(\d+)$");

if (!match.Success)
{
    Console.WriteLine("Invalid format. Example: jhn3,16");
    return;
}

var bookCode = match.Groups[1].Value;
var chapter = int.Parse(match.Groups[2].Value);
var verse = int.Parse(match.Groups[3].Value);

var map = new Dictionary<string, (string osis, string pl)>
{
    ["mat"] = ("Matt", "Ewangelia Mateusza"),
    ["mar"] = ("Mark", "Ewangelia Marka"),
    ["luk"] = ("Luke", "Ewangelia Łukasza"),
    ["jhn"] = ("John", "Ewangelia Jana"),
    ["act"] = ("Acts", "Dzieje Apostolskie"),
    ["rom"] = ("Rom", "List do Rzymian"),
    ["1co"] = ("1Cor", "1 List do Koryntian"),
    ["2co"] = ("2Cor", "2 List do Koryntian"),
    ["gal"] = ("Gal", "List do Galacjan"),
    ["eph"] = ("Eph", "List do Efezjan"),
    ["php"] = ("Phil", "List do Filipian"),
    ["col"] = ("Col", "List do Kolosan"),
    ["1th"] = ("1Thess", "1 List do Tesaloniczan"),
    ["2th"] = ("2Thess", "2 List do Tesaloniczan"),
    ["1ti"] = ("1Tim", "1 List do Tymoteusza"),
    ["2ti"] = ("2Tim", "2 List do Tymoteusza"),
    ["tit"] = ("Titus", "List do Tytusa"),
    ["phm"] = ("Phlm", "List do Filemona"),
    ["heb"] = ("Heb", "List do Hebrajczyków"),
    ["jas"] = ("Jas", "List Jakuba"),
    ["1pe"] = ("1Pet", "1 List Piotra"),
    ["2pe"] = ("2Pet", "2 List Piotra"),
    ["1jn"] = ("1John", "1 List Jana"),
    ["2jn"] = ("2John", "2 List Jana"),
    ["3jn"] = ("3John", "3 List Jana"),
    ["jud"] = ("Jude", "List Judy"),
    ["rev"] = ("Rev", "Objawienie Jana")
};

if (!map.TryGetValue(bookCode, out var book))
{
    Console.WriteLine($"Unknown book: {bookCode}");
    return;
}

var redis = await ConnectionMultiplexer.ConnectAsync("localhost:6379");
var db = redis.GetDatabase();

string Key(string prefix) => $"{prefix}:{book.osis}:{chapter}:{verse}";

var tr = await db.StringGetAsync(Key("gnt"));
var tnp = await db.StringGetAsync(Key("tnp"));
var ubg = await db.StringGetAsync(Key("ubg"));
var kjv = await db.StringGetAsync(Key("kjv"));

if (tr.IsNull)
{
    Console.WriteLine("TR NOT FOUND");
    return;
}

var trJson = JsonDocument.Parse(tr!.ToString());
var words = trJson.RootElement.GetProperty("words");

// 🔥 DEBUG: pokaż gdzie zapisujemy
var root = "/data-out/Index";
Console.WriteLine("OUTPUT DIR:", root);

var bibleDir = Path.Combine(root, "Biblia");
var greekDir = Path.Combine(root, "Graeca");

Directory.CreateDirectory(bibleDir);
Directory.CreateDirectory(greekDir);

var sb = new StringBuilder();

foreach (var w in words.EnumerateArray())
{
    var greek = Clean(w.GetProperty("greek").GetString());
    var lemma = Clean(w.GetProperty("dictionary_form").GetString());

    sb.Append($"[[{greek}]] ");

    File.WriteAllText(
        Path.Combine(greekDir, $"{greek}.md"),
        BuildWordTable(w, true)
    );

    File.WriteAllText(
        Path.Combine(greekDir, $"{lemma}.md"),
        BuildWordTable(w, false)
    );
}

var content = $"""
[TR]
> {sb.ToString().Trim()}

[TNP]
> {tnp.ToString()}

[UBG]
> {ubg.ToString()}

[KJV]
> {kjv.ToString()}
""";

var fileName = $"{book.pl} {chapter},{verse}.md";

Console.WriteLine("FILE:", fileName);

File.WriteAllText(Path.Combine(bibleDir, fileName), content);

Console.WriteLine("DONE");


// ================= HELPERS =================

static string Clean(string? text)
{
    if (text == null) return "";
    return Regex.Replace(text.Normalize(NormalizationForm.FormC), "[.,;·]", "");
}

static string BuildWordTable(JsonElement w, bool linkLemma)
{
    var greek = Clean(w.GetProperty("greek").GetString());
    var lemma = Clean(w.GetProperty("dictionary_form").GetString());

    var lemmaVal = linkLemma ? $"[[{lemma}]]" : lemma;

    return $"""
| greek | {greek} |
|---|---|
| transliteration | {w.GetProperty("transliteration")} |
| strong | {w.GetProperty("strong")} |
| grammar | {w.GetProperty("grammar")} |
| grammar_human | {w.GetProperty("grammar_human")} |
| dictionary_form | {lemmaVal} |
| definition | {w.GetProperty("definition")} |
""";
}
