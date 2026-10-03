namespace KazdyDzienZJezusem.Models;

public sealed record ContentTag(
    string Slug,
    string Name,
    IReadOnlyList<string> Fragments,
    string Description);

public sealed record TagDetailsViewModel(ContentTag Tag);
