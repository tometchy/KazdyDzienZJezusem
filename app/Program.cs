using StackExchange.Redis;
using System.Text.RegularExpressions;

// MAPA (Twoja + dodany OSIS potrzebny do Redis key)
var BOOK_MAP = new Dictionary<string, (string English, string Polish, int Num, string Slug, string File, string Osis)>
{
    ["mat"] = ("Matthew", "Ewangelia Mateusza", 40, "Ewangelia-Mateusza", "Matt.html", "Matt"),
    ["mar"] = ("Mark", "Ewangelia Marka", 41, "Ewangelia-Marka", "Mark.html", "Mark"),
    ["luk"] = ("Luke", "Ewangelia Łukasza", 42, "Ewangelia-Lukasza", "Luke.html", "Luke"),
    ["jhn"] = ("John", "Ewangelia Jana", 43, "Ewangelia-Jana", "John.html", "John"),
    ["act"] = ("Acts", "Dzieje Apostolskie", 44, "Dzieje-Apostolskie", "Acts.html", "Acts"),
    ["rom"] = ("Romans", "List do Rzymian", 45, "List-do-Rzymian", "Rom.html", "Rom"),

    ["1co"] = ("1 Corinthians", "1 List do Koryntian", 46, "1-List-do-Koryntian", "1Cor.html", "1Cor"),
    ["2co"] = ("2 Corinthians", "2 List do Koryntian", 47, "2-List-do-Koryntian", "2Cor.html", "2Cor"),
    ["gal"] = ("Galatians", "List do Galacjan", 48, "List-do-Galacjan", "Gal.html", "Gal"),
    ["eph"] = ("Ephesians", "List do Efezjan", 49, "List-do-Efezjan", "Eph.html", "Eph"),
    ["php"] = ("Philippians", "List do Filipian", 50, "List-do-Filipian", "Phil.html", "Phil"),
    ["col"] = ("Colossians", "List do Kolosan", 51, "List-do-Kolosan", "Col.html", "Col"),

    ["1th"] = ("1 Thessalonians", "1 List do Tesaloniczan", 52, "1-List-do-Tesaloniczan", "1Thess.html", "1Thess"),
    ["2th"] = ("2 Thessalonians", "2 List do Tesaloniczan", 53, "2-List-do-Tesaloniczan", "2Thess.html", "2Thess"),

    ["1ti"] = ("1 Timothy", "1 List do Tymoteusza", 54, "1-List-do-Tymoteusza", "1Tim.html", "1Tim"),
    ["2ti"] = ("2 Timothy", "2 List do Tymoteusza", 55, "2-List-do-Tymoteusza", "2Tim.html", "2Tim"),

    ["tit"] = ("Titus", "List do Tytusa", 56, "List-do-Tytusa", "Titus.html", "Titus"),
    ["phm"] = ("Philemon", "List do Filemona", 57, "List-do-Filemona", "Phlm.html", "Phlm"),
    ["heb"] = ("Hebrews", "List do Hebrajczyków", 58, "List-do-Hebrajczykow", "Heb.html", "Heb"),
    ["jas"] = ("James", "List Jakuba", 59, "List-Jakuba", "Jas.html", "Jas"),

    ["1pe"] = ("1 Peter", "1 List Piotra", 60, "1-List-Piotra", "1Pet.html", "1Pet"),
    ["2pe"] = ("2 Peter", "2 List Piotra", 61, "2-List-Piotra", "2Pet.html", "2Pet"),

    ["1jn"] = ("1 John", "1 List Jana", 62, "1-List-Jana", "1John.html", "1John"),
    ["2jn"] = ("2 John", "2 List Jana", 63, "2-List-Jana", "2John.html", "2John"),
    ["3jn"] = ("3 John", "3 List Jana", 64, "3-List-Jana", "3John.html", "3John"),

    ["jud"] = ("Jude", "List Judy", 65, "List-Judy", "Jude.html", "Jude"),
    ["rev"] = ("Revelation", "Objawienie Jana", 66, "Objawienie-Jana", "Rev.html", "Rev"),
};

if (args.Length == 0)
{
    Console.WriteLine("Usage: jhn3,16  OR  jud1,2");
    return;
}

var input = args[0].ToLower().Trim();

// regex: np. jud1,2 → (jud)(1)(2)
var match = Regex.Match(input, @"^([0-9]?[a-z]+)(\d+),(\d+)$");

if (!match.Success)
{
    Console.WriteLine("Invalid format. Example: jhn3,16");
    return;
}

var bookCode = match.Groups[1].Value;
var chapter = match.Groups[2].Value;
var verse = match.Groups[3].Value;

if (!BOOK_MAP.TryGetValue(bookCode, out var book))
{
    Console.WriteLine($"Unknown book: {bookCode}");
    return;
}

// klucz Redis
var key = $"gnt:{book.Osis}:{chapter}:{verse}";

try
{
    var redis = await ConnectionMultiplexer.ConnectAsync("localhost:6379");
    var db = redis.GetDatabase();

    var value = await db.StringGetAsync(key);

    if (value.IsNullOrEmpty)
    {
        Console.WriteLine("NOT FOUND");
    }
    else
    {
        Console.WriteLine(value);
    }
}
catch (Exception ex)
{
    Console.WriteLine("ERROR: " + ex.Message);
}
