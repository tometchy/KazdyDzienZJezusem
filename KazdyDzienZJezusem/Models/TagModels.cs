namespace KazdyDzienZJezusem.Models;

public sealed record ContentTag(
    string Slug,
    string Name,
    IReadOnlyList<string> Fragments,
    string Description);

public sealed record TagDetailsViewModel(ContentTag Tag)
{
    public string DefaultTranslationCode { get; init; } = "TNP";

    public IReadOnlyList<TagTranslationViewModel> Translations { get; init; } = [];

    public IReadOnlyList<TagPassageViewModel> UntranslatedPassages { get; init; } = [];
}

public sealed record TagTranslationViewModel(
    BibleTranslation Translation,
    IReadOnlyList<TagPassageViewModel> Passages);

public sealed record TagPassageViewModel(
    string Reference,
    string? Url,
    BibleTranslation? Translation,
    IReadOnlyList<BibleVerse> Verses);
