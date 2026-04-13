using System.Text;
using System.Text.Json;
using StackExchange.Redis;

var arg = args.FirstOrDefault();

if (string.IsNullOrEmpty(arg))
{
    Console.WriteLine("Usage: act1,6 or act1:6");
    return;
}

// normalize input
arg = arg.Replace(":", ",");

var bookCode = new string(arg.TakeWhile(char.IsLetter).ToArray()).ToLower();
var numbers = arg.Substring(bookCode.Length).Split(',');

if (numbers.Length != 2)
{
    Console.WriteLine("Invalid format. Example: act1,6");
    return;
}

var chapter = int.Parse(numbers[0]);
var verse = int.Parse(numbers[1]);

var redis = ConnectionMultiplexer.Connect("localhost");
var db = redis.GetDatabase();

string bookName = bookCode switch
{
    "jhn" => "Ewangelia Jana",
    "act" => "Dzieje Apostolskie",
    _ => "Unknown"
};

string redisBook = bookCode switch
{
    "jhn" => "John",
    "act" => "Acts",
    _ => ""
};

var trKey = $"gnt:{redisBook}:{chapter}:{verse}";
var tnpKey = $"tnp:{redisBook}:{chapter}:{verse}";
var ubgKey = $"ubg:{redisBook}:{chapter}:{verse}";
var kjvKey = $"kjv:{redisBook}:{chapter}:{verse}";

var tr = db.StringGet(trKey);
var tnp = db.StringGet(tnpKey);
var ubg = db.StringGet(ubgKey);
var kjv = db.StringGet(kjvKey);

if (tr.IsNull)
{
    Console.WriteLine("NOT FOUND");
    return;
}

// output dir (host mounted)
var baseDir = "/data-out/Index";
var bibliaDir = Path.Combine(baseDir, "Biblia");
var graecaDir = Path.Combine(baseDir, "Graeca");
var strongDir = Path.Combine(baseDir, "Strong");

Directory.CreateDirectory(bibliaDir);
Directory.CreateDirectory(graecaDir);
Directory.CreateDirectory(strongDir);

var fileName = $"{bookName} {chapter},{verse}.md";
var filePath = Path.Combine(bibliaDir, fileName);

var sb = new StringBuilder();

// =====================
// TR
// =====================
sb.AppendLine("TR");

using var doc = JsonDocument.Parse(tr.ToString());
var words = doc.RootElement.GetProperty("words");

var verseLine = new StringBuilder();

foreach (var w in words.EnumerateArray())
{
    var greek = w.GetProperty("greek").GetString();

    // LINK = dokładnie greek
    verseLine.Append($"[[{greek}]] ");

    var lemma = w.GetProperty("dictionary_form").GetString();
    var strong = w.GetProperty("strong").GetInt32();
    var grammar = w.GetProperty("grammar_human").GetString();
    var translit = w.GetProperty("transliteration").GetString();
    var definition = w.GetProperty("definition").GetString();

    var wordFile = Path.Combine(graecaDir, $"{greek}.md");

    var wordContent = $"""
# {greek}

lemma: {lemma}
strong: [[G{strong}]]
transliteration: {translit}
grammar: {grammar}
definition: {definition}
""";

    File.WriteAllText(wordFile, wordContent);

    var strongFile = Path.Combine(strongDir, $"G{strong}.md");

    if (!File.Exists(strongFile))
    {
        File.WriteAllText(strongFile, $"# G{strong}");
    }
}

sb.AppendLine($"> {verseLine.ToString().Trim()}");
sb.AppendLine();

// =====================
// KJV
// =====================
if (!kjv.IsNull)
{
    sb.AppendLine($"[KJV](https://www.blueletterbible.org/kjv/{bookCode}/{chapter}/{verse}/)");
    sb.AppendLine($"> {kjv}");
    sb.AppendLine();
}

// =====================
// TNP
// =====================
if (!tnp.IsNull)
{
    sb.AppendLine($"[TNP](https://biblia-online.pl/Biblia/PrzekladTorunski/{bookName.Replace(" ", "-")}/{chapter}/{verse})");
    sb.AppendLine($"> {tnp}");
    sb.AppendLine();
}

// =====================
// UBG
// =====================
if (!ubg.IsNull)
{
    sb.AppendLine($"[UBG](https://biblia-online.pl/Biblia/UwspolczesnionaBibliaGdanska/{bookName.Replace(" ", "-")}/{chapter}/{verse})");
    sb.AppendLine($"> {ubg}");
    sb.AppendLine();
}

// save
File.WriteAllText(filePath, sb.ToString());

Console.WriteLine($"Saved: {fileName}");
