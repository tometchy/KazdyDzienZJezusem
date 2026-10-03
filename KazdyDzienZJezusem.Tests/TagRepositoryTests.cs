using KazdyDzienZJezusem.Services;

namespace KazdyDzienZJezusem.Tests;

public sealed class TagRepositoryTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());

    [Fact]
    public void IndexesSingleReferencesRangesAndFathersReferences()
    {
        Directory.CreateDirectory(_directory);
        File.WriteAllText(Path.Combine(_directory, "birth-of-jesus.md"), """
            ---
            fragments:
              - Mt1,18-20
              - Ignatius/Ef1,1
            ---

            # Birth of Jesus

            A description.
            """);

        var repository = new TagRepository(_directory);

        Assert.Equal("birth-of-jesus", repository.GetTags("Mt1,19").Single().Name);
        Assert.Equal("birth-of-jesus", repository.GetTags("Ignatius/Ef1,1").Single().Slug);
        Assert.Empty(repository.GetTags("Mt1,21"));
        Assert.Equal(2, repository.Find("BIRTH-OF-JESUS")!.Fragments.Count);
    }

    public void Dispose()
    {
        if (Directory.Exists(_directory)) Directory.Delete(_directory, true);
    }
}
