using KazdyDzienZJezusem.Models;
using KazdyDzienZJezusem.Services;

namespace KazdyDzienZJezusem.Tests;

public sealed class TagPassageReaderTests : IDisposable
{
    private readonly string _fathersDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
    private readonly TagPassageReader _reader;

    public TagPassageReaderTests()
    {
        var workDirectory = Path.Combine(_fathersDirectory, "Ignatius", "Ef");
        Directory.CreateDirectory(workDirectory);
        File.WriteAllText(Path.Combine(workDirectory, "1.yml"), """
            "Ignatius/Ef1,1": "First section."
            "Ignatius/Ef1,2": "Second section."
            "Ignatius/Ef1,3": "Third section."
            """);
        File.WriteAllText(Path.Combine(workDirectory, "0.yml"), """
            "Ignatius/Ef0,1": "Preface."
            """);
        _reader = new TagPassageReader(
            new BibleRepository("/Bible", new StubBibleFileSystem()),
            new FathersRepository(_fathersDirectory));
    }

    [Fact]
    public void Bible_ranges_show_exact_verses_in_the_selected_translation()
    {
        var result = _reader.Read(Tag("Ob15,2-3"));

        Assert.Equal("TNP", result.DefaultTranslationCode);
        Assert.Equal(4, result.Translations.Count);
        var torun = result.Translations.Single(item => item.Translation.Code == "TNP");
        var passage = Assert.Single(torun.Passages);
        Assert.Equal("Ob15,2-3", passage.Reference);
        Assert.Equal("TNP", passage.Translation?.Code);
        Assert.Equal([2, 3], passage.Verses.Select(verse => verse.Number));
        Assert.StartsWith("I widziałem jakby szklane morze", passage.Verses[0].Text);
        Assert.Equal("/Biblia/TNP/Ob/15#2", passage.Url);
        Assert.Empty(result.UntranslatedPassages);
    }

    [Fact]
    public void Old_Testament_tags_default_to_Ubg_and_offer_only_available_translations()
    {
        var result = _reader.Read(Tag("Rdz1,1"));

        Assert.Equal("UBG", result.DefaultTranslationCode);
        Assert.Equal(["KJV", "UBG"], result.Translations.Select(item => item.Translation.Code).Order());
        var passage = Assert.Single(result.Translations.Single(item => item.Translation.Code == "UBG").Passages);
        Assert.Equal("Na początku Bóg stworzył niebo i ziemię.", Assert.Single(passage.Verses).Text);
        Assert.Equal("UBG", passage.Translation?.Code);
    }

    [Fact]
    public void Mixed_tags_keep_Torun_as_default_and_label_Ubg_fallback_passages()
    {
        var result = _reader.Read(Tag("Ob15,1-2", "Rdz1,1"));

        Assert.Equal("TNP", result.DefaultTranslationCode);
        var torun = result.Translations.Single(item => item.Translation.Code == "TNP");
        Assert.Equal(["Ob15,1-2", "Rdz1,1"], torun.Passages.Select(passage => passage.Reference));
        Assert.Equal("TNP", torun.Passages[0].Translation?.Code);
        Assert.Equal("UBG", torun.Passages[1].Translation?.Code);
        Assert.Equal("/Biblia/UBG/Rdz/1#1", torun.Passages[1].Url);
        Assert.Equal("Na początku Bóg stworzył niebo i ziemię.", Assert.Single(torun.Passages[1].Verses).Text);

        var kingJames = result.Translations.Single(item => item.Translation.Code == "KJV");
        Assert.All(kingJames.Passages, passage => Assert.Equal("KJV", passage.Translation?.Code));
        Assert.StartsWith("In the beginning", Assert.Single(kingJames.Passages[1].Verses).Text);
    }

    [Fact]
    public void Fathers_ranges_and_prefaces_are_rendered_outside_Bible_translation_choices()
    {
        var result = _reader.Read(Tag("Ignatius/Ef1,1-2", "Ignatius/Ef0,1"));

        Assert.Empty(result.Translations);
        Assert.Equal(2, result.UntranslatedPassages.Count);
        var range = result.UntranslatedPassages[0];
        Assert.Null(range.Translation);
        Assert.Equal([1, 2], range.Verses.Select(verse => verse.Number));
        Assert.Equal(["First section.", "Second section."], range.Verses.Select(verse => verse.Text));
        Assert.Equal("/Ojcowie/Ignatius/Ef/1#1", range.Url);
        Assert.Equal("Preface.", Assert.Single(result.UntranslatedPassages[1].Verses).Text);
        Assert.Equal("/Ojcowie/Ignatius/Ef/0#1", result.UntranslatedPassages[1].Url);
    }

    [Fact]
    public void Malformed_and_missing_Fathers_references_remain_visible_without_throwing()
    {
        string[] references = ["not a reference", "Unknown1,1", "Ignatius/Ef99,1", "Ignatius/Missing1,1"];

        var result = _reader.Read(Tag(references));

        Assert.Empty(result.Translations);
        Assert.Equal(references, result.UntranslatedPassages.Select(passage => passage.Reference));
        Assert.All(result.UntranslatedPassages, passage => Assert.Empty(passage.Verses));
    }

    [Fact]
    public void Incomplete_Bible_ranges_are_not_silently_truncated()
    {
        var result = _reader.Read(Tag("Ob15,1", "Ob15,7-9"));

        Assert.Equal(4, result.Translations.Count);
        Assert.All(result.Translations, translation =>
        {
            Assert.Equal(2, translation.Passages.Count);
            Assert.Single(translation.Passages[0].Verses);
            Assert.Equal("Ob15,7-9", translation.Passages[1].Reference);
            Assert.Empty(translation.Passages[1].Verses);
        });
    }

    private static ContentTag Tag(params string[] references) =>
        new("test-tag", "test-tag", references, "A test tag.");

    public void Dispose()
    {
        if (Directory.Exists(_fathersDirectory)) Directory.Delete(_fathersDirectory, true);
    }
}
