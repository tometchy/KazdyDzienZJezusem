using System.Collections.ObjectModel;
using System.Text.Json;
using KazdyDzienZJezusem.Models;
using StackExchange.Redis;

namespace KazdyDzienZJezusem.Services;

public sealed class TagRepository
{
    private readonly IReadOnlyDictionary<string, ContentTag> _tags;
    private readonly IReadOnlyDictionary<string, IReadOnlyList<string>> _tagsByFragment;

    public TagRepository(string rootPath, string? redisConnection = null)
    {
        if (!Directory.Exists(rootPath))
        {
            throw new DirectoryNotFoundException($"Tags data directory was not found: {rootPath}");
        }

        var tags = Directory.EnumerateFiles(rootPath, "*.md")
            .Select(LoadTag)
            .ToDictionary(tag => tag.Slug, StringComparer.OrdinalIgnoreCase);
        _tags = new ReadOnlyDictionary<string, ContentTag>(tags);
        _tagsByFragment = tags.Values
            .SelectMany(tag => tag.Fragments.Select(fragment => (fragment, tag.Slug)))
            .GroupBy(item => item.fragment, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                group => group.Key,
                group => (IReadOnlyList<string>)group.Select(item => item.Slug).Order().ToArray(),
                StringComparer.OrdinalIgnoreCase);

        if (!string.IsNullOrWhiteSpace(redisConnection))
        {
            LoadRedis(redisConnection, tags.Values);
        }
    }

    public ContentTag? Find(string slug) => _tags.GetValueOrDefault(slug);

    public IReadOnlyList<string> FindForFragment(string fragment) =>
        _tagsByFragment.GetValueOrDefault(fragment) ?? [];

    private static void LoadRedis(string connectionString, IEnumerable<ContentTag> tags)
    {
        using var connection = ConnectionMultiplexer.Connect(connectionString);
        var database = connection.GetDatabase();
        var oldFragments = database.SetMembers("tags:fragments");
        foreach (var fragment in oldFragments)
        {
            database.KeyDelete($"tags:fragment:{fragment}");
        }
        database.KeyDelete(["tags:definitions", "tags:fragments"]);
        foreach (var tag in tags)
        {
            database.HashSet("tags:definitions", tag.Slug, JsonSerializer.Serialize(tag));
            foreach (var fragment in tag.Fragments)
            {
                database.SetAdd($"tags:fragment:{fragment}", tag.Slug);
                database.SetAdd("tags:fragments", fragment);
            }
        }
    }

    private static ContentTag LoadTag(string path)
    {
        var lines = File.ReadAllLines(path);
        if (lines.Length < 4 || lines[0] != "---")
        {
            throw new InvalidDataException($"Tag must start with YAML front matter: {path}");
        }

        var end = Array.IndexOf(lines, "---", 1);
        if (end < 0)
        {
            throw new InvalidDataException($"Tag front matter is not closed: {path}");
        }
        if (end < 2 || lines[1].Trim() != "fragments:")
        {
            throw new InvalidDataException($"Tag front matter must contain a fragments array: {path}");
        }

        var fragments = lines[2..end]
            .Where(line => !string.IsNullOrWhiteSpace(line))
            .Select(line => line.Trim())
            .Select(line => line.StartsWith("- ", StringComparison.Ordinal)
                ? line[2..].Trim()
                : throw new InvalidDataException($"Each fragment must be a separate YAML list item: {path}"))
            .ToArray();
        if (fragments.Length == 0 || fragments.Any(string.IsNullOrWhiteSpace))
        {
            throw new InvalidDataException($"Tag must reference at least one fragment: {path}");
        }

        return new ContentTag(
            Path.GetFileNameWithoutExtension(path),
            string.Join('\n', lines[(end + 1)..]).Trim(),
            fragments);
    }
}
