using KazdyDzienZJezusem.Services;

namespace KazdyDzienZJezusem.Tests;

public sealed class TagRepositoryTests
{
    [Fact]
    public void Loads_front_matter_and_builds_reverse_fragment_index()
    {
        var directory = Directory.CreateTempSubdirectory();
        try
        {
            File.WriteAllText(Path.Combine(directory.FullName, "birth-of-jesus.md"),
                """
                ---
                fragments:
                  - Bible:Łk2,7
                  - Fathers:Ignatius:Ef19,1
                ---

                # Birth of Jesus
                """);

            var repository = new TagRepository(directory.FullName);

            var tag = Assert.IsType<KazdyDzienZJezusem.Models.ContentTag>(
                repository.Find("birth-of-jesus"));
            Assert.Equal(["Bible:Łk2,7", "Fathers:Ignatius:Ef19,1"], tag.Fragments);
            Assert.Equal(["birth-of-jesus"], repository.FindForFragment("Bible:Łk2,7"));
        }
        finally
        {
            directory.Delete(recursive: true);
        }
    }

    [Fact]
    public void Rejects_a_fragment_that_is_not_a_separate_list_item()
    {
        var directory = Directory.CreateTempSubdirectory();
        try
        {
            File.WriteAllText(Path.Combine(directory.FullName, "invalid.md"),
                "---\nfragments: [Bible:J3,16]\n---\nDescription");

            Assert.Throws<InvalidDataException>(() => new TagRepository(directory.FullName));
        }
        finally
        {
            directory.Delete(recursive: true);
        }
    }
}
