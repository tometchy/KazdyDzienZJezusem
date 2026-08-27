using KazdyDzienZJezusem.Services;

namespace KazdyDzienZJezusem.Tests;

public sealed class BibleRepositoryTests
{
    [Fact]
    public void GetChapter_loads_non_empty_tags()
    {
        var repository = CreateRepository(
            """
            "Rdz1,1":
              text: "In the beginning"
              tags: ["creation", "beginning"]
            """);
        var translation = repository.FindTranslation("UBG")!;
        var book = repository.FindBook("Rdz")!;

        var verse = Assert.Single(repository.GetChapter(translation, book, 1));

        Assert.Equal("In the beginning", verse.Text);
        Assert.Equal(["creation", "beginning"], verse.Tags);
    }

    [Fact]
    public void GetChapter_rejects_a_non_array_tags_value()
    {
        var repository = CreateRepository(
            """
            "Rdz1,1":
              text: "In the beginning"
              tags: "creation"
            """);
        var translation = repository.FindTranslation("UBG")!;
        var book = repository.FindBook("Rdz")!;

        var exception = Assert.Throws<InvalidDataException>(
            () => repository.GetChapter(translation, book, 1));

        Assert.Contains("Field 'tags'", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Stub_chapter_uses_the_current_schema()
    {
        var repository = new BibleRepository("/Bible", new StubBibleFileSystem());
        var translation = repository.FindTranslation("TNP")!;
        var book = repository.FindBook("1Kor")!;

        var verses = repository.GetChapter(translation, book, 1);

        Assert.Equal(31, verses.Count);
        Assert.Equal(["Paweł"], verses[0].Tags);
        Assert.All(verses.Skip(1), verse => Assert.Empty(verse.Tags));
    }

    private static BibleRepository CreateRepository(string chapter) =>
        new("/Bible", new SingleChapterBibleFileSystem(chapter));

    private sealed class SingleChapterBibleFileSystem(string chapter) : IBibleFileSystem
    {
        private static readonly HashSet<string> Directories =
        [
            "",
            "KJV",
            "TNP",
            "TR",
            "UBG",
            "UBG/Rdz"
        ];

        public bool DirectoryExists(string path) =>
            Directories.Contains(GetBibleRelativePath(path));

        public IEnumerable<string> EnumerateFiles(string path, string searchPattern) => [];

        public bool FileExists(string path) =>
            GetBibleRelativePath(path) == "UBG/Rdz/1.yml";

        public IEnumerable<string> ReadLines(string path)
        {
            if (!FileExists(path))
            {
                throw new FileNotFoundException("The requested chapter is not available.", path);
            }

            return chapter.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        }

        private static string GetBibleRelativePath(string path)
        {
            var normalizedPath = path.Replace('\\', '/').TrimEnd('/');
            var bibleDirectoryIndex = normalizedPath.LastIndexOf("/Bible", StringComparison.Ordinal);

            if (bibleDirectoryIndex < 0)
            {
                return normalizedPath;
            }

            return normalizedPath[(bibleDirectoryIndex + "/Bible".Length)..]
                .TrimStart('/');
        }
    }
}
