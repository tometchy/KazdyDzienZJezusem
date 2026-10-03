namespace KazdyDzienZJezusem.Models;

public sealed record ContentTag(string Slug, string DescriptionMarkdown, IReadOnlyList<string> Fragments);

public sealed record TagPassage(string Reference, string Text, string Url);

public sealed record TagDetailsViewModel(ContentTag Tag, string DescriptionHtml, IReadOnlyList<TagPassage> Passages);
