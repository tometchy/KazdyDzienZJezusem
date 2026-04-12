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
    ["jud"] = ("Jude", "List Judy"),
    ["rev"] = ("Rev", "Objawienie Jana")
};

if (!map.TryGetValue(bookCode, out var book))
{
    Console.WriteLine("Unknown book");
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

var trJson = JsonDocument.Parse(tr!);
var words = trJson.RootElement.GetProperty("words");

var root = Path.Combine(Directory.GetCurrentDirectory(), "Index");
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

    // słowo
    File.WriteAllText(
        Path.Combine(greekDir, $"{greek}.md"),
        BuildWordTable(w, true)
    );

    // lemma
    File.WriteAllText(
        Path.Combine(greekDir, $"{lemma}.md"),
        BuildWordTable(w, false)
    );
}

var content = $"""
[TR]
> {sb.ToString().Trim()}

[TNP]
> {tnp}

[UBG]
> {ubg}

[KJV]
> {kjv}
""";

var fileName = $"{book.pl} {chapter},{verse}.md";

File.WriteAllText(Path.Combine(bibleDir, fileName), content);

Console.WriteLine("Saved:", fileName);

// --- helpers ---

static string Clean(string? text)
{
    if (text == null) return "";
    return Regex.Replace(text, "[.,;·]", "");
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
