using System.Collections.ObjectModel;
using KazdyDzienZJezusem.Models;

namespace KazdyDzienZJezusem.Services;

public sealed class TagRepository
{
    private readonly IReadOnlyDictionary<string, ContentTag> _tags;
    private readonly IReadOnlyDictionary<string, IReadOnlyList<string>> _tagsByFragment;

    public TagRepository(string rootPath)
    {
        var tags = Directory.Exists(rootPath)
            ? Directory.EnumerateFiles(rootPath, "*.md").Select(Parse).ToArray()
            : [];

        _tags = new ReadOnlyDictionary<string, ContentTag>(tags.ToDictionary(
            tag => tag.Name,
            StringComparer.OrdinalIgnoreCase));
        _tagsByFragment = new ReadOnlyDictionary<string, IReadOnlyList<string>>(tags
            .SelectMany(tag => tag.Fragments.Select(fragment => (fragment, tag.Name)))
            .GroupBy(item => item.fragment, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                group => group.Key,
                group => (IReadOnlyList<string>)group.Select(item => item.Name).Order().ToArray(),
                StringComparer.OrdinalIgnoreCase));
    }

    public IReadOnlyList<ContentTag> GetAll() => _tags.Values.OrderBy(tag => tag.Name).ToArray();

    public ContentTag? Find(string? name) => name is not null && _tags.TryGetValue(name, out var tag)
        ? tag
        : null;

    public IReadOnlyList<string> GetTags(string fragment) =>
        _tagsByFragment.TryGetValue(fragment, out var tags) ? tags : [];

    private static ContentTag Parse(string path)
    {
        var lines = File.ReadAllLines(path);
        if (lines.Length < 4 || lines[0] != "---")
        {
            throw new InvalidDataException($"Tag file must start with YAML front matter: {path}");
        }

        var closingDelimiter = Array.IndexOf(lines, "---", 1);
        if (closingDelimiter < 0)
        {
            throw new InvalidDataException($"Tag front matter is not closed: {path}");
        }

        var fragments = new List<string>();
        var inFragments = false;
        for (var index = 1; index < closingDelimiter; index++)
        {
            var line = lines[index];
            if (line == "fragments:")
            {
                inFragments = true;
                continue;
            }

            if (!inFragments || !line.StartsWith("  - ", StringComparison.Ordinal))
            {
                throw new InvalidDataException($"Invalid fragments list in tag file: {path}");
            }

            var value = line[4..].Trim();
            if (value.Length < 2 || value[0] != '"' || value[^1] != '"')
            {
                throw new InvalidDataException($"Tag fragments must be double-quoted in: {path}");
            }
            fragments.Add(System.Text.Json.JsonSerializer.Deserialize<string>(value)!);
        }

        return new ContentTag(
            Path.GetFileNameWithoutExtension(path),
            fragments,
            string.Join('\n', lines.Skip(closingDelimiter + 1)).Trim());
    }
}
