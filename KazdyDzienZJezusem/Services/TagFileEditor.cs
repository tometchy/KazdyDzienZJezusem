using System.Text.RegularExpressions;

namespace KazdyDzienZJezusem.Services;

public static partial class TagFileEditor
{
    public static string ValidateName(string? name)
    {
        name = name?.Trim().Normalize();
        if (string.IsNullOrEmpty(name) || name.Length > 80 || !SafeName().IsMatch(name))
            throw new TagEditException(400, "Use 1–80 letters, numbers, spaces, hyphens or underscores for the tag.");
        return name;
    }

    public static string AddFragment(string? content, string name, string fragment)
    {
        if (content is null) return $"---\nfragments:\n  - {fragment}\n---\n\n# {name}\n";
        var lines = content.Replace("\r\n", "\n").Split('\n').ToList();
        var end = lines.Count >= 2 ? lines.FindIndex(2, line => line == "---") : -1;
        if (lines.Count < 4 || lines[0] != "---" || lines[1] != "fragments:" || end < 0)
            throw new TagEditException(409, "The tag file has an unsupported format. Repair it in the repository first.");
        if (lines.Skip(2).Take(end - 2).Any(line => !line.StartsWith("  - ", StringComparison.Ordinal)))
            throw new TagEditException(409, "The tag file has an unsupported fragment list.");
        if (lines.Skip(2).Take(end - 2).SelectMany(line => TagRepository.ExpandFragment(line[4..].Trim()))
            .Contains(fragment, StringComparer.OrdinalIgnoreCase)) return content;
        lines.Insert(end, $"  - {fragment}");
        return string.Join('\n', lines);
    }

    [GeneratedRegex(@"^[\p{L}\p{N}][\p{L}\p{M}\p{N} _-]*$", RegexOptions.CultureInvariant)]
    private static partial Regex SafeName();
}
