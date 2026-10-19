using System.Collections.Frozen;
using System.Globalization;
using System.Text.Json;
using StackExchange.Redis;
using KazdyDzienZJezusem.Models;

namespace KazdyDzienZJezusem.Services;

public sealed class TagRepository : IDisposable
{
    private sealed record Snapshot(
        FrozenDictionary<string, ContentTag> Tags,
        FrozenDictionary<string, IReadOnlyList<ContentTag>> ByFragment);
    private readonly object _sync = new();
    private readonly ConnectionMultiplexer? _redis;
    private Snapshot _snapshot;
    private string? _redisValue;
    private bool _pendingRedis;

    public TagRepository(string rootPath, string? redisConnection = null, bool seedRedis = true)
    {
        var tags = ReadTags(rootPath);
        _snapshot = BuildSnapshot(tags);
        if (!string.IsNullOrWhiteSpace(redisConnection))
        {
            var configuration = ConfigurationOptions.Parse(redisConnection);
            configuration.AbortOnConnectFail = false;
            _redis = ConnectionMultiplexer.Connect(configuration);
            if (seedRedis) Publish(tags, "baked");
        }
    }

    public static ContentTag[] ReadTags(string rootPath) => Directory.Exists(rootPath)
        ? Directory.EnumerateFiles(rootPath, "*.md").Select(path =>
        {
            if (new FileInfo(path).LinkTarget is not null)
                throw new InvalidDataException("Tag files must not be symbolic links.");
            return Parse(path);
        }).ToArray()
        : [];

    private static Snapshot BuildSnapshot(IEnumerable<ContentTag> tags)
    {
        var definitions = tags.ToFrozenDictionary(tag => tag.Slug, StringComparer.OrdinalIgnoreCase);
        var index = new Dictionary<string, List<ContentTag>>(StringComparer.OrdinalIgnoreCase);
        foreach (var tag in definitions.Values)
        {
            foreach (var fragment in tag.Fragments.SelectMany(ExpandFragment).Distinct(StringComparer.OrdinalIgnoreCase))
            {
                if (!index.TryGetValue(fragment, out var fragmentTags))
                {
                    fragmentTags = [];
                    index.Add(fragment, fragmentTags);
                }
                fragmentTags.Add(tag);
            }
        }
        return new Snapshot(definitions, index.ToFrozenDictionary(
            pair => pair.Key,
            pair => (IReadOnlyList<ContentTag>)pair.Value.OrderBy(tag => tag.Name).ToArray(),
            StringComparer.OrdinalIgnoreCase));
    }

    // Publish only a catalog confirmed in the remote repository. All Redis keys change
    // atomically. Local readers still see the confirmed update while Redis recovers.
    public bool Publish(ContentTag[] tags, string revision)
    {
        var snapshot = BuildSnapshot(tags);
        lock (_sync)
        {
            _snapshot = snapshot;
            if (_redis is null) return true;
            _pendingRedis = true;
            try
            {
                var database = _redis.GetDatabase();
                var transaction = database.CreateTransaction();
                _ = transaction.KeyDeleteAsync("tags:definitions");
                _ = transaction.KeyDeleteAsync("tags:fragments");
                foreach (var tag in tags)
                {
                    _ = transaction.HashSetAsync("tags:definitions", tag.Slug, JsonSerializer.Serialize(tag));
                }
                foreach (var pair in snapshot.ByFragment)
                {
                    _ = transaction.HashSetAsync("tags:fragments", pair.Key,
                        JsonSerializer.Serialize(pair.Value.Select(tag => tag.Slug)));
                }
                var serialized = JsonSerializer.Serialize(new TagCatalog(tags, revision));
                _ = transaction.StringSetAsync("tags:catalog", serialized);
                if (!transaction.Execute()) return false;
                _redisValue = serialized;
                _pendingRedis = false;
                return true;
            }
            catch (RedisException) { return false; }
        }
    }

    private Snapshot Current()
    {
        lock (_sync)
        {
            if (_redis is null || _pendingRedis || !_redis.IsConnected) return _snapshot;
            try
            {
                var value = (string?)_redis.GetDatabase().StringGet("tags:catalog");
                if (value is not null && value != _redisValue)
                {
                    var catalog = JsonSerializer.Deserialize<TagCatalog>(value)!;
                    _snapshot = BuildSnapshot(catalog.Tags);
                    _redisValue = value;
                }
            }
            catch (RedisException) { /* Continue serving the last confirmed catalog. */ }
            return _snapshot;
        }
    }

    public ContentTag? Find(string slug) => Current().Tags.GetValueOrDefault(slug);

    public IReadOnlyList<ContentTag> GetAll() => Current().Tags.Values
        .OrderBy(tag => tag.Name, StringComparer.Create(CultureInfo.GetCultureInfo("pl-PL"), ignoreCase: true))
        .ToArray();

    public IReadOnlyList<ContentTag> GetTags(string fragment) =>
        Current().ByFragment.GetValueOrDefault(fragment) ?? [];

    public void Dispose() => _redis?.Dispose();

    private static ContentTag Parse(string path)
    {
        var lines = File.ReadAllLines(path);
        if (lines.Length < 4 || lines[0] != "---" || lines[1] != "fragments:")
        {
            throw new InvalidDataException($"Tag must start with a fragments YAML property in {path}");
        }

        var fragments = new List<string>();
        var index = 2;
        while (index < lines.Length && lines[index] != "---")
        {
            if (!lines[index].StartsWith("  - ", StringComparison.Ordinal) || lines[index].Length == 4)
            {
                throw new InvalidDataException($"Invalid fragment list item in {path}:{index + 1}");
            }
            fragments.Add(lines[index][4..].Trim());
            index++;
        }
        if (index == lines.Length || fragments.Count == 0)
        {
            throw new InvalidDataException($"Tag requires at least one fragment and closing front matter in {path}");
        }

        var slug = Path.GetFileNameWithoutExtension(path);
        var description = string.Join('\n', lines.Skip(index + 1)).Trim();
        return new ContentTag(slug, slug, fragments, description);
    }

    public static IEnumerable<string> ExpandFragment(string fragment)
    {
        yield return fragment;
        var dash = fragment.LastIndexOf('-');
        var comma = fragment.LastIndexOf(',', dash >= 0 ? dash : fragment.Length - 1);
        if (dash < 0 || comma < 0
            || !int.TryParse(fragment.AsSpan(comma + 1, dash - comma - 1), NumberStyles.None, CultureInfo.InvariantCulture, out var first)
            || !int.TryParse(fragment.AsSpan(dash + 1), NumberStyles.None, CultureInfo.InvariantCulture, out var last)
            || last < first || last - first > 10000)
        {
            yield break;
        }
        var prefix = fragment[..(comma + 1)];
        for (long number = first; number <= last; number++)
        {
            yield return $"{prefix}{number}";
        }
    }
}
