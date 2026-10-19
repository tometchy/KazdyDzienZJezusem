using System.Diagnostics;
using KazdyDzienZJezusem.Services;
using Microsoft.Extensions.Options;

namespace KazdyDzienZJezusem.Tests;

// All commits and pushes in these tests are confined to newly created temporary
// repositories. No network, GitHub credentials or application checkout is used.
public sealed class TagGitStoreTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "kdj-git-test-" + Guid.NewGuid());
    private string Remote => Path.Combine(_root, "remote.git");

    [Fact]
    public async Task SavesNewAndExistingTagsPreservesConcurrentChangesAndIsIdempotent()
    {
        var first = await CreateStore("first");
        var second = await CreateStore("second");
        var initial = await first.SynchronizeAsync(TestContext.Current.CancellationToken);
        var saved = await first.AddAsync("new-tag", "Ob15,1", "fake-token", TestContext.Current.CancellationToken);
        Assert.NotEqual(initial.Revision, saved.Revision);
        var repeated = await first.AddAsync("new-tag", "Ob15,1", "fake-token", TestContext.Current.CancellationToken);
        Assert.Equal(saved.Revision, repeated.Revision);
        var newer = await second.AddAsync("new-tag", "Ignatius/Ef1,1", "fake-token", TestContext.Current.CancellationToken);
        Assert.Equal(2, newer.Tags.Single(tag => tag.Slug == "new-tag").Fragments.Count);
        var original = await first.AddAsync("existing", "Ob15,2", "fake-token", TestContext.Current.CancellationToken);
        Assert.Contains("Original description.", original.Tags.Single(tag => tag.Slug == "existing").Description);
        Assert.Equal(2, original.Tags.Single(tag => tag.Slug == "new-tag").Fragments.Count);
        Assert.DoesNotContain("fake-token", await File.ReadAllTextAsync(Path.Combine(_root, "first/.git/config"), TestContext.Current.CancellationToken));
        var remoteFile = await Git(Remote, "show", "master:tags/new-tag.md");
        Assert.Contains("Ignatius/Ef1,1", remoteFile);
    }

    [Fact]
    public async Task RejectedPushIsDiscardedAndUnpushedCrashResidueIsNeverPublished()
    {
        var store = await CreateStore("first");
        var before = await store.SynchronizeAsync(TestContext.Current.CancellationToken);
        await Git(Remote, "config", "receive.denyNonFastForwards", "true");
        var hook = Path.Combine(Remote, "hooks/pre-receive");
        await File.WriteAllTextAsync(hook, "#!/bin/sh\nexit 1\n", TestContext.Current.CancellationToken);
        if (!OperatingSystem.IsWindows())
            File.SetUnixFileMode(hook, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        var exception = await Assert.ThrowsAsync<TagEditException>(() =>
            store.AddAsync("rejected", "Ob15,1", "fake-token", TestContext.Current.CancellationToken));
        Assert.Equal(409, exception.StatusCode);
        Assert.Equal(before.Revision, (await store.SynchronizeAsync(TestContext.Current.CancellationToken)).Revision);
        var abandoned = Path.Combine(_root, "first/tags/abandoned.md");
        await File.WriteAllTextAsync(abandoned, TagFileEditor.AddFragment(null, "abandoned", "Ob15,1"), TestContext.Current.CancellationToken);
        await Git(Path.Combine(_root, "first"), "add", "tags/abandoned.md");
        await Git(Path.Combine(_root, "first"), "-c", "user.name=Test", "-c", "user.email=test@example.invalid", "commit", "-m", "Simulate interrupted write");
        var recovered = await store.SynchronizeAsync(TestContext.Current.CancellationToken);
        Assert.DoesNotContain(recovered.Tags, tag => tag.Slug == "abandoned");
        Assert.False(File.Exists(abandoned));
    }

    private async Task<TagGitStore> CreateStore(string name)
    {
        Directory.CreateDirectory(_root);
        if (!Directory.Exists(Remote))
        {
            await Git(_root, "init", "--bare", "--initial-branch=master", Remote);
            var seed = Path.Combine(_root, "seed");
            await Git(_root, "clone", Remote, seed);
            Directory.CreateDirectory(Path.Combine(seed, "tags"));
            await File.WriteAllTextAsync(Path.Combine(seed, "tags/existing.md"),
                "---\nfragments:\n  - Ob15,1\n---\n\n# Existing\n\nOriginal description.\n");
            await Git(seed, "add", "tags/existing.md");
            await Git(seed, "-c", "user.name=Test", "-c", "user.email=test@example.invalid", "commit", "-m", "Seed");
            await Git(seed, "push", "origin", "master");
        }
        var clone = Path.Combine(_root, name);
        await Git(_root, "clone", Remote, clone);
        await Git(clone, "config", "kdj.editorClone", "true");
        // Rewrite the production URL only within this test clone.
        await Git(clone, "config", $"url.file://{Remote}.insteadOf", "https://github.com/test/repository.git");
        return new TagGitStore(Options.Create(new TagEditingOptions
            { RepositoryPath = clone, Repository = "test/repository", Branch = "master" }));
    }

    private static async Task<string> Git(string directory, params string[] arguments)
    {
        var start = new ProcessStartInfo("git")
        {
            WorkingDirectory = directory, RedirectStandardOutput = true,
            RedirectStandardError = true, UseShellExecute = false
        };
        start.Environment["GIT_CONFIG_NOSYSTEM"] = "1";
        start.Environment["GIT_CONFIG_GLOBAL"] = "/dev/null";
        foreach (var argument in arguments) start.ArgumentList.Add(argument);
        using var process = Process.Start(start)!;
        var output = process.StandardOutput.ReadToEndAsync();
        var error = process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync(TestContext.Current.CancellationToken);
        Assert.True(process.ExitCode == 0, await error);
        return await output;
    }

    public void Dispose() { if (Directory.Exists(_root)) Directory.Delete(_root, true); }
}
