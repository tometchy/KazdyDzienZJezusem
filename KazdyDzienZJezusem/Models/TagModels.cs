namespace KazdyDzienZJezusem.Models;

public sealed record ContentTag(
    string Name,
    IReadOnlyList<string> Fragments,
    string DescriptionHtml);

public sealed record TagDetailsViewModel(ContentTag Tag);
