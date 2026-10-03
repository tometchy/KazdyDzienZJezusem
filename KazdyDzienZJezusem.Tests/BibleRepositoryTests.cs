using KazdyDzienZJezusem.Services;

namespace KazdyDzienZJezusem.Tests;

public sealed class BibleRepositoryTests
{
    [Fact]
    public void GetChapter_loads_plain_verse_mapping()
    {
        var repository = CreateRepository("\"Rdz1,1\": \"In the beginning\"");
        var verse = Assert.Single(repository.GetChapter(
            repository.FindTranslation("UBG")!, repository.FindBook("Rdz")!, 1));

        Assert.Equal("In the beginning", verse.Text);
        Assert.Empty(verse.Tags);
    }

    [Fact]
    public void GetChapter_rejects_nested_old_schema()
    {
        var repository = CreateRepository("\"Rdz1,1\":\n  text: \"In the beginning\"");

        Assert.Throws<InvalidDataException>(() => repository.GetChapter(
            repository.FindTranslation("UBG")!, repository.FindBook("Rdz")!, 1));
    }

    private static BibleRepository CreateRepository(string chapter) =>
        new("/Bible", new SingleChapterBibleFileSystem(chapter));

    private sealed class SingleChapterBibleFileSystem(string chapter) : IBibleFileSystem
    {
        public bool DirectoryExists(string path) => true;
        public IEnumerable<string> EnumerateFiles(string path, string searchPattern) => [];
        public bool FileExists(string path) => path.Replace('\\', '/').EndsWith("UBG/Rdz/1.yml");
        public IEnumerable<string> ReadLines(string path) => chapter.Split('\n');
    }
}
