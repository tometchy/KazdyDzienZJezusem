namespace KazdyDzienZJezusem.Models;

public sealed record ContentTag(
    string Name,
    IReadOnlyList<string> Fragments,
    string Description);

public sealed record TagIndexViewModel(IReadOnlyList<ContentTag> Tags);

public sealed record TagDetailsViewModel(ContentTag Tag);
