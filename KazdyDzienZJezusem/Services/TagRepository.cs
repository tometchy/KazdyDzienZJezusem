using System.Collections.Frozen;
using System.Globalization;
using System.Text.Json;
using StackExchange.Redis;
using KazdyDzienZJezusem.Models;

namespace KazdyDzienZJezusem.Services;

public sealed class TagRepository
{
    private readonly FrozenDictionary<string, ContentTag> _tags;
    private readonly FrozenDictionary<string, IReadOnlyList<ContentTag>> _tagsByFragment;

    public TagRepository(string rootPath, string? redisConnection = null)
    {
        var tags = Directory.Exists(rootPath)
            ? Directory.EnumerateFiles(rootPath, "*.md").Select(Parse).ToArray()
            : [];
        _tags = tags.ToFrozenDictionary(tag => tag.Slug, StringComparer.OrdinalIgnoreCase);

        var index = new Dictionary<string, List<ContentTag>>(StringComparer.OrdinalIgnoreCase);
        foreach (var tag in tags)
        {
            foreach (var fragment in tag.Fragments.SelectMany(ExpandFragment))
            {
                if (!index.TryGetValue(fragment, out var fragmentTags))
                {
                    fragmentTags = [];
                    index.Add(fragment, fragmentTags);
                }
                fragmentTags.Add(tag);
            }
        }
        _tagsByFragment = index.ToFrozenDictionary(
            pair => pair.Key,
            pair => (IReadOnlyList<ContentTag>)pair.Value.OrderBy(tag => tag.Name).ToArray(),
            StringComparer.OrdinalIgnoreCase);

        if (!string.IsNullOrWhiteSpace(redisConnection))
        {
            LoadIntoRedis(redisConnection, tags, index);
        }
    }

    private static void LoadIntoRedis(
        string connectionString,
        IReadOnlyCollection<ContentTag> tags,
        IReadOnlyDictionary<string, List<ContentTag>> index)
    {
        using var connection = ConnectionMultiplexer.Connect(connectionString);
        var database = connection.GetDatabase();
        var transaction = database.CreateTransaction();
        _ = transaction.KeyDeleteAsync("tags:definitions");
        _ = transaction.KeyDeleteAsync("tags:fragments");
        foreach (var tag in tags)
        {
            _ = transaction.HashSetAsync("tags:definitions", tag.Slug, JsonSerializer.Serialize(tag));
        }
        foreach (var pair in index)
        {
            _ = transaction.HashSetAsync(
                "tags:fragments",
                pair.Key,
                JsonSerializer.Serialize(pair.Value.Select(tag => tag.Slug)));
        }
        if (!transaction.Execute())
        {
            throw new InvalidOperationException("Could not load the tag catalog into Redis.");
        }
    }

    public ContentTag? Find(string slug) => _tags.GetValueOrDefault(slug);

    public IReadOnlyList<ContentTag> GetAll() => _tags.Values
        .OrderBy(tag => tag.Name, StringComparer.Create(CultureInfo.GetCultureInfo("pl-PL"), ignoreCase: true))
        .ToArray();

    public IReadOnlyList<ContentTag> GetTags(string fragment) =>
        _tagsByFragment.GetValueOrDefault(fragment) ?? [];

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

    private static IEnumerable<string> ExpandFragment(string fragment)
    {
        yield return fragment;
        var dash = fragment.LastIndexOf('-');
        var comma = fragment.LastIndexOf(',', dash >= 0 ? dash : fragment.Length - 1);
        if (dash < 0 || comma < 0
            || !int.TryParse(fragment.AsSpan(comma + 1, dash - comma - 1), NumberStyles.None, CultureInfo.InvariantCulture, out var first)
            || !int.TryParse(fragment.AsSpan(dash + 1), NumberStyles.None, CultureInfo.InvariantCulture, out var last)
            || last < first)
        {
            yield break;
        }
        var prefix = fragment[..(comma + 1)];
        for (var number = first; number <= last; number++)
        {
            yield return $"{prefix}{number}";
        }
    }
}
