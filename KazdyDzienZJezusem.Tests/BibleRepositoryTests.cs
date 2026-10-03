using KazdyDzienZJezusem.Services;

namespace KazdyDzienZJezusem.Tests;

public sealed class BibleRepositoryTests
{
    [Fact]
    public void GetChapter_loads_flat_verse_text()
    {
        var repository = CreateRepository("\"Rdz1,1\": \"In the beginning\"");
        var translation = repository.FindTranslation("UBG")!;
        var book = repository.FindBook("Rdz")!;

        var verse = Assert.Single(repository.GetChapter(translation, book, 1));

        Assert.Equal("In the beginning", verse.Text);
        Assert.Empty(verse.Tags);
    }

    [Fact]
    public void GetChapter_rejects_nested_verse_data()
    {
        var repository = CreateRepository("\"Rdz1,1\":\n  text: \"In the beginning\"");
        var translation = repository.FindTranslation("UBG")!;
        var book = repository.FindBook("Rdz")!;

        Assert.Throws<InvalidDataException>(() => repository.GetChapter(translation, book, 1));
    }

    [Fact]
    public void Stub_chapter_uses_the_flat_schema()
    {
        var repository = new BibleRepository("/Bible", new StubBibleFileSystem());
        var translation = repository.FindTranslation("TNP")!;
        var book = repository.FindBook("1Kor")!;

        var verses = repository.GetChapter(translation, book, 1);

        Assert.Equal(31, verses.Count);
        Assert.All(verses, verse => Assert.Empty(verse.Tags));
    }

    private static BibleRepository CreateRepository(string chapter) =>
        new("/Bible", new SingleChapterBibleFileSystem(chapter));

    private sealed class SingleChapterBibleFileSystem(string chapter) : IBibleFileSystem
    {
        private static readonly HashSet<string> Directories = ["", "KJV", "TNP", "TR", "UBG", "UBG/Rdz"];
        public bool DirectoryExists(string path) => Directories.Contains(GetBibleRelativePath(path));
        public IEnumerable<string> EnumerateFiles(string path, string searchPattern) => [];
        public bool FileExists(string path) => GetBibleRelativePath(path) == "UBG/Rdz/1.yml";
        public IEnumerable<string> ReadLines(string path) => chapter.Split('\n', StringSplitOptions.RemoveEmptyEntries);

        private static string GetBibleRelativePath(string path)
        {
            var normalizedPath = path.Replace('\\', '/').TrimEnd('/');
            var index = normalizedPath.LastIndexOf("/Bible", StringComparison.Ordinal);
            return index < 0 ? normalizedPath : normalizedPath[(index + 6)..].TrimStart('/');
        }
    }
}
