using KazdyDzienZJezusem.Services;

namespace KazdyDzienZJezusem.Tests;

public sealed class TagRepositoryTests
{
    [Fact]
    public void Loads_front_matter_fragments_and_description()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"tags-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        try
        {
            File.WriteAllText(Path.Combine(directory, "creation.md"), """
                ---
                fragments:
                  - "Rdz1,1"
                  - "Fathers:Ignatius:Ef1,1"
                ---
                # Creation

                A description.
                """);

            var repository = new TagRepository(directory);
            var tag = Assert.Single(repository.GetAll());

            Assert.Equal("creation", tag.Name);
            Assert.Equal(["Rdz1,1", "Fathers:Ignatius:Ef1,1"], tag.Fragments);
            Assert.Equal(["creation"], repository.GetTags("Rdz1,1"));
            Assert.Contains("A description.", tag.Description, StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }
}
