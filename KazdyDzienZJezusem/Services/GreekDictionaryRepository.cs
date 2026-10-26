using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;
using StackExchange.Redis;

namespace KazdyDzienZJezusem.Services;

public sealed record GreekDictionaryPassage(string Reference, string[] Greek, string[] Polish, string[] Grammar,
    string[]? English = null, string[]? Codes = null, string[]? Pronunciation = null);
public sealed record GreekDictionaryForm(string? Polish, string Grammar, string? English = null,
    string? Code = null, string? Pronunciation = null, string EnglishKind = "gloss",
    string? Lemma = null, string? Source = null);
public sealed record GreekDictionaryLookup(GreekDictionaryPassage? Passage, Dictionary<string, GreekDictionaryForm[]> Forms);
public interface IGreekDictionaryReader
{
    Task<GreekDictionaryLookup> LookupAsync(string? reference, string[] forms, CancellationToken cancellationToken);
}

public sealed class GreekDictionaryCatalog
{
    public Dictionary<string, GreekDictionaryPassage> Passages { get; } = new(StringComparer.Ordinal);
    public Dictionary<string, GreekDictionaryForm[]> Forms { get; } = new(StringComparer.Ordinal);
    internal static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    public static string SupplementPath(string path) => Path.Combine(Path.GetDirectoryName(path)!, "supplement.jsonl.gz");

    public static GreekDictionaryCatalog Read(string path)
    {
        using var stream = File.OpenRead(path);
        using var gzip = new GZipStream(stream, CompressionMode.Decompress);
        using var reader = new StreamReader(gzip);
        using var metadata = JsonDocument.Parse(reader.ReadLine() ?? throw new InvalidDataException("Missing dictionary metadata."));
        if (metadata.RootElement.GetProperty("schema").GetInt32() != 2)
            throw new InvalidDataException("Unsupported Greek dictionary schema.");
        var catalog = new GreekDictionaryCatalog();
        var forms = new Dictionary<string, HashSet<GreekDictionaryForm>>(StringComparer.Ordinal);
        while (reader.ReadLine() is { } line)
        {
            using var record = JsonDocument.Parse(line);
            if (record.RootElement.TryGetProperty("form", out var alias))
            {
                var entries = record.RootElement.GetProperty("glosses")
                    .Deserialize<GreekDictionaryForm[]>(JsonOptions) ?? throw new InvalidDataException("Invalid form alias.");
                if (entries.Length == 0 || entries.Any(entry => string.IsNullOrWhiteSpace(entry.Polish)
                    || string.IsNullOrWhiteSpace(entry.English) || string.IsNullOrWhiteSpace(entry.Code)
                    || string.IsNullOrWhiteSpace(entry.Pronunciation)))
                    throw new InvalidDataException("Incomplete Greek form alias.");
                forms.Add(alias.GetString()!, entries.Select(entry => entry.Polish == "-" ? entry with { Polish = "—" } : entry).ToHashSet());
                continue;
            }
            var passage = JsonSerializer.Deserialize<GreekDictionaryPassage>(line, JsonOptions)
                ?? throw new InvalidDataException("Invalid Greek dictionary passage.");
            if (passage.Greek.Length == 0 || passage.Greek.Length > 500
                || passage.Greek.Length != passage.Polish.Length || passage.Greek.Length != passage.Grammar.Length
                || passage.Greek.Length != passage.English?.Length || passage.Greek.Length != passage.Codes?.Length
                || passage.Greek.Length != passage.Pronunciation?.Length
                || passage.Greek.Any(string.IsNullOrWhiteSpace) || passage.Polish.Any(string.IsNullOrWhiteSpace)
                || passage.English!.Any(string.IsNullOrWhiteSpace) || passage.Codes!.Any(string.IsNullOrWhiteSpace)
                || passage.Pronunciation!.Any(string.IsNullOrWhiteSpace))
                throw new InvalidDataException($"Unaligned Greek dictionary passage: {passage.Reference}");
            for (var index = 0; index < passage.Greek.Length; index++)
            {
                if (passage.Polish[index] == "-") passage.Polish[index] = "—";
                var key = GreekText.FormKey(passage.Greek[index]);
                if (!forms.TryGetValue(key, out var entries)) forms.Add(key, entries = []);
                entries.Add(new GreekDictionaryForm(passage.Polish[index], passage.Grammar[index],
                    passage.English![index], passage.Codes![index], passage.Pronunciation![index]));
            }
            catalog.Passages.Add(passage.Reference, passage);
        }
        if (catalog.Passages.Count != metadata.RootElement.GetProperty("passages").GetInt32()
            || forms.Count != metadata.RootElement.GetProperty("forms").GetInt32())
            throw new InvalidDataException("The Greek dictionary does not match its declared counts.");
        foreach (var entry in forms)
            catalog.Forms.Add(entry.Key, entry.Value.OrderBy(form => form.Polish, StringComparer.Ordinal)
                .ThenBy(form => form.Grammar, StringComparer.Ordinal).ThenBy(form => form.English, StringComparer.Ordinal).ToArray());
        using var supplementStream = File.OpenRead(SupplementPath(path));
        using var supplementGzip = new GZipStream(supplementStream, CompressionMode.Decompress);
        using var supplement = new StreamReader(supplementGzip);
        using var supplementMetadata = JsonDocument.Parse(supplement.ReadLine() ?? throw new InvalidDataException("Missing supplement metadata."));
        if (supplementMetadata.RootElement.GetProperty("schema").GetInt32() != 1)
            throw new InvalidDataException("Unsupported dictionary supplement schema.");
        var added = 0;
        while (supplement.ReadLine() is { } line)
        {
            using var record = JsonDocument.Parse(line);
            var key = record.RootElement.GetProperty("form").GetString()!;
            var entries = record.RootElement.GetProperty("glosses").Deserialize<GreekDictionaryForm[]>(JsonOptions)!;
            if (entries.Length == 0 || entries.Any(entry => string.IsNullOrWhiteSpace(entry.English)
                || string.IsNullOrWhiteSpace(entry.Pronunciation) || string.IsNullOrWhiteSpace(entry.Lemma)
                || entry.EnglishKind is not ("gloss" or "dictionary") || entry.Source is not ("MACULA" or "Wiktionary" or "Bundled GNT")
                || entry.Polish is not null && entry.Code is not ("ADV" or "PREP" or "CONJ" or "PRT" or "INJ")))
                throw new InvalidDataException("Invalid dictionary supplement: do not substitute Polish lemma meanings for inflected translations.");
            // Supplement only missing forms; never override the aligned corpus.
            catalog.Forms.Add(key, entries);
            added++;
        }
        if (added != supplementMetadata.RootElement.GetProperty("forms").GetInt32())
            throw new InvalidDataException("The dictionary supplement does not match its declared count.");
        return catalog;
    }
}

// Files populate Redis at startup. Reader requests use Redis HMGET exclusively.
public sealed class GreekDictionaryRepository(string path, string? redisConnection,
    ILogger<GreekDictionaryRepository> logger) : BackgroundService, IGreekDictionaryReader
{
    private ConnectionMultiplexer? _redis;
    private string? _prefix;
    private readonly TaskCompletionSource _ready = new(TaskCreationOptions.RunContinuationsAsynchronously);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (string.IsNullOrWhiteSpace(redisConnection)) return;
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                if (_redis is null)
                {
                    var configuration = ConfigurationOptions.Parse(redisConnection);
                    configuration.AbortOnConnectFail = false;
                    configuration.ConnectTimeout = 3000;
                    configuration.AsyncTimeout = 5000;
                    _redis = await ConnectionMultiplexer.ConnectAsync(configuration);
                }
                await SeedAsync(stoppingToken);
                _ready.TrySetResult();
            }
            catch (Exception exception) when (!stoppingToken.IsCancellationRequested)
            {
                logger.LogWarning("The static Greek dictionary is unavailable ({ErrorType}); retrying in 30 seconds.", exception.GetType().Name);
            }
            await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
        }
    }

    private async Task SeedAsync(CancellationToken cancellationToken)
    {
        if (_prefix is null)
        {
            using var file = File.OpenRead(path);
            using var supplement = File.OpenRead(GreekDictionaryCatalog.SupplementPath(path));
            var primaryHash = await SHA256.HashDataAsync(file, cancellationToken);
            var supplementHash = await SHA256.HashDataAsync(supplement, cancellationToken);
            _prefix = "greek-polish:v3:" + Convert.ToHexString(SHA256.HashData([.. primaryHash, .. supplementHash])).ToLowerInvariant();
        }
        var database = _redis!.GetDatabase();
        var completion = (string?)await database.StringGetAsync(_prefix + ":ready");
        if (completion is not null)
        {
            var counts = completion.Split(':');
            if (counts.Length == 2 && long.TryParse(counts[0], out var passages) && long.TryParse(counts[1], out var forms)
                && passages > 0 && forms > 0
                && await database.HashLengthAsync(_prefix + ":passages") == passages
                && await database.HashLengthAsync(_prefix + ":forms") == forms) return;
        }
        var catalog = GreekDictionaryCatalog.Read(path);
        var temporary = _prefix + ":import:" + Guid.NewGuid().ToString("N");
        try
        {
            await WriteHash(temporary + ":passages", catalog.Passages.Select(pair =>
                new HashEntry(pair.Key, JsonSerializer.Serialize(pair.Value, GreekDictionaryCatalog.JsonOptions))));
            await WriteHash(temporary + ":forms", catalog.Forms.Select(pair =>
                new HashEntry(pair.Key, JsonSerializer.Serialize(pair.Value, GreekDictionaryCatalog.JsonOptions))));
            cancellationToken.ThrowIfCancellationRequested();
            var transaction = database.CreateTransaction();
            _ = transaction.KeyRenameAsync(temporary + ":passages", _prefix + ":passages");
            _ = transaction.KeyRenameAsync(temporary + ":forms", _prefix + ":forms");
            _ = transaction.KeyPersistAsync(_prefix + ":passages");
            _ = transaction.KeyPersistAsync(_prefix + ":forms");
            _ = transaction.StringSetAsync(_prefix + ":ready", $"{catalog.Passages.Count}:{catalog.Forms.Count}");
            _ = transaction.StringSetAsync("greek-polish:active", _prefix);
            if (!await transaction.ExecuteAsync()) throw new InvalidOperationException("Dictionary publication failed.");
            logger.LogInformation("Loaded {Passages} Greek-Polish passages and {Forms} inflected Greek forms into Redis.", catalog.Passages.Count, catalog.Forms.Count);
        }
        finally { await database.KeyDeleteAsync([temporary + ":passages", temporary + ":forms"]); }

        async Task WriteHash(string key, IEnumerable<HashEntry> entries)
        {
            foreach (var batch in entries.Chunk(250))
            {
                cancellationToken.ThrowIfCancellationRequested();
                await database.HashSetAsync(key, batch);
                await database.KeyExpireAsync(key, TimeSpan.FromHours(1));
            }
        }
    }

    public async Task<GreekDictionaryLookup> LookupAsync(string? reference, string[] forms, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(redisConnection))
            throw new InvalidOperationException("The static Greek dictionary requires a Redis connection.");
        try
        {
            await _ready.Task.WaitAsync(TimeSpan.FromSeconds(5), cancellationToken);
            var database = _redis!.GetDatabase();
            if (!await database.KeyExistsAsync(_prefix + ":ready"))
                throw new InvalidOperationException("The static Greek dictionary is being restored. Please retry shortly.");
            var keys = forms.Distinct(StringComparer.Ordinal).ToArray();
            var values = await database.HashGetAsync(_prefix + ":forms", keys.Select(key => (RedisValue)key).ToArray()).WaitAsync(cancellationToken);
            var matches = new Dictionary<string, GreekDictionaryForm[]>(StringComparer.Ordinal);
            for (var index = 0; index < keys.Length; index++)
                if (values[index].HasValue)
                    matches.Add(keys[index], JsonSerializer.Deserialize<GreekDictionaryForm[]>((string)values[index]!, GreekDictionaryCatalog.JsonOptions)!);
            GreekDictionaryPassage? passage = null;
            if (!string.IsNullOrEmpty(reference))
            {
                var value = await database.HashGetAsync(_prefix + ":passages", reference).WaitAsync(cancellationToken);
                if (value.HasValue) passage = JsonSerializer.Deserialize<GreekDictionaryPassage>((string)value!, GreekDictionaryCatalog.JsonOptions);
            }
            return new GreekDictionaryLookup(passage, matches);
        }
        catch (Exception exception) when (exception is RedisException or TimeoutException)
        {
            throw new InvalidOperationException("The static Greek dictionary is temporarily unavailable. Please retry shortly.");
        }
    }

    public override void Dispose()
    {
        _redis?.Dispose();
        base.Dispose();
    }
}
