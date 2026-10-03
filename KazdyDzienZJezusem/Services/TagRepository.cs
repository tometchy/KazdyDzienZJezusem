using System.Collections.Frozen;
using KazdyDzienZJezusem.Models;
using Markdig;
using StackExchange.Redis;

namespace KazdyDzienZJezusem.Services;

public sealed class TagRepository
{
    private readonly FrozenDictionary<string, ContentTag> _tags;
    private readonly FrozenDictionary<string, IReadOnlyList<ContentTag>> _tagsByFragment;

    public TagRepository(string rootPath)
    {
        if (!Directory.Exists(rootPath))
        {
            throw new DirectoryNotFoundException($"Tag directory was not found: {rootPath}");
        }

        var tags = Directory.EnumerateFiles(rootPath, "*.md")
            .Select(Parse)
            .ToFrozenDictionary(tag => tag.Name, StringComparer.OrdinalIgnoreCase);
        _tags = tags;
        _tagsByFragment = tags.Values
            .SelectMany(tag => tag.Fragments.Select(fragment => (Fragment: fragment, Tag: tag)))
            .GroupBy(item => item.Fragment, StringComparer.OrdinalIgnoreCase)
            .ToFrozenDictionary(
                group => group.Key,
                group => (IReadOnlyList<ContentTag>)group.Select(item => item.Tag).OrderBy(tag => tag.Name).ToArray(),
                StringComparer.OrdinalIgnoreCase);
    }

    public ContentTag? Find(string? name) => name is null ? null : _tags.GetValueOrDefault(name);

    public IReadOnlyList<ContentTag> ForFragment(string fragment) =>
        _tagsByFragment.GetValueOrDefault(fragment, []);

    public void LoadIntoRedis(IDatabase database)
    {
        foreach (var tag in _tags.Values)
        {
            var key = $"tag:{tag.Name}";
            database.HashSet(key,
            [
                new HashEntry("descriptionHtml", tag.DescriptionHtml),
                new HashEntry("fragments", System.Text.Json.JsonSerializer.Serialize(tag.Fragments))
            ]);
            foreach (var fragment in tag.Fragments)
            {
                database.SetAdd($"fragment-tags:{fragment}", tag.Name);
            }
        }
    }

    private static ContentTag Parse(string path)
    {
        var lines = File.ReadAllLines(path);
        if (lines.Length < 3 || lines[0] != "---")
        {
            throw new InvalidDataException($"Tag file must start with YAML front matter: {path}");
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

        var fragments = lines[2..end].Select((line, index) =>
        {
            var trimmed = line.Trim();
            if (!trimmed.StartsWith("- ", StringComparison.Ordinal) || trimmed.Length == 2)
            {
                throw new InvalidDataException($"Invalid fragment at {path}:{index + 3}");
            }
            return trimmed[2..].Trim();
        }).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();

        var markdown = string.Join('\n', lines[(end + 1)..]);
        var pipeline = new MarkdownPipelineBuilder().UseAdvancedExtensions().Build();
        return new ContentTag(Path.GetFileNameWithoutExtension(path), fragments, Markdown.ToHtml(markdown, pipeline));
    }
}
