using StackExchange.Redis;
using System.Text.RegularExpressions;

if (args.Length == 0)
{
    Console.WriteLine("Usage: jhn3,16 [tr|tnp|ubg|kjv|all]");
    return;
}

var input = args[0].ToLower().Trim();
var mode = args.Length > 1 ? args[1].ToLower().Trim() : "tr";

var match = Regex.Match(input, @"^([0-9]?[a-z]+)(\d+),(\d+)$");

if (!match.Success)
{
    Console.WriteLine("Invalid format. Example: jhn3,16");
    return;
}

var bookCode = match.Groups[1].Value;
var chapter = match.Groups[2].Value;
var verse = match.Groups[3].Value;

// MAPA → OSIS
var map = new Dictionary<string, string>
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
    ["rev"] = "Rev"
};

if (!map.TryGetValue(bookCode, out var osis))
{
    Console.WriteLine($"Unknown book: {bookCode}");
    return;
}

var redis = await ConnectionMultiplexer.ConnectAsync("localhost:6379");
var db = redis.GetDatabase();

async Task Print(string label, string key)
{
    var val = await db.StringGetAsync(key);

    Console.WriteLine($"\n[{label}]");

    if (val.IsNullOrEmpty)
        Console.WriteLine("NOT FOUND");
    else
        Console.WriteLine(val.ToString());
}

if (mode == "all")
{
    await Print("TR",  $"gnt:{osis}:{chapter}:{verse}");
    await Print("TNP", $"tnp:{osis}:{chapter}:{verse}");
    await Print("UBG", $"ubg:{osis}:{chapter}:{verse}");
    await Print("KJV", $"kjv:{osis}:{chapter}:{verse}");
}
else
{
    var key = $"{mode}:{osis}:{chapter}:{verse}";
    var value = await db.StringGetAsync(key);

    if (value.IsNullOrEmpty)
        Console.WriteLine("NOT FOUND");
    else
        Console.WriteLine(value.ToString());
}
