using System.Diagnostics;
using System.Text;
using KazdyDzienZJezusem.Models;
using Microsoft.Extensions.Options;

namespace KazdyDzienZJezusem.Services;

public sealed record TagCatalog(ContentTag[] Tags, string Revision);

public interface ITagGitStore
{
    Task<TagCatalog> SynchronizeAsync(CancellationToken cancellationToken);
    Task<TagCatalog> AddAsync(string name, string fragment, string token, CancellationToken cancellationToken);
}

// All access is serialized by TagEditingService. This is a disposable, dedicated clone,
// never the application's source checkout. No credentials are written to its config.
public sealed class TagGitStore(IOptions<TagEditingOptions> options) : ITagGitStore
{
    private TagEditingOptions Settings => options.Value;
    private string Remote => $"https://github.com/{Settings.Repository}.git";
    private string TagsPath => Path.Combine(Settings.RepositoryPath, "tags");

    public async Task<TagCatalog> SynchronizeAsync(CancellationToken cancellationToken)
    {
        if (!Directory.Exists(Path.Combine(Settings.RepositoryPath, ".git")))
            throw new TagEditException(503, "The editing repository is not available.");
        var marker = await RunGitAsync(["config", "--local", "--get", "kdj.editorClone"], null, cancellationToken);
        if (marker.ExitCode != 0 || marker.Output.Trim() != "true")
            throw new TagEditException(503, "Editing requires a dedicated runtime clone.");
        var readToken = await ReadTokenAsync(cancellationToken);
        await GitAsync(["fetch", "--depth=1", Remote, $"refs/heads/{Settings.Branch}"], readToken, cancellationToken);
        await GitAsync(["reset", "--hard", "FETCH_HEAD"], null, cancellationToken);
        // Remove only abandoned tag files left by an interrupted, unpushed request.
        await GitAsync(["clean", "-fd", "--", "tags/"], null, cancellationToken);
        EnsureRegularDirectory(TagsPath);
        return await ReadCatalogAsync(cancellationToken);
    }

    public async Task<TagCatalog> AddAsync(string name, string fragment, string token, CancellationToken cancellationToken)
    {
        for (var attempt = 0; attempt < 3; attempt++)
        {
            var catalog = await SynchronizeAsync(cancellationToken);
            var existing = catalog.Tags.SingleOrDefault(tag => tag.Slug.Equals(name, StringComparison.OrdinalIgnoreCase));
            var fileName = existing?.Slug ?? name;
            var path = Path.Combine(TagsPath, fileName + ".md");
            if (File.Exists(path) && new FileInfo(path).LinkTarget is not null)
                throw new TagEditException(409, "Symbolic links cannot be edited.");
            var original = File.Exists(path) ? await File.ReadAllTextAsync(path, cancellationToken) : null;
            var updated = TagFileEditor.AddFragment(original, fileName, fragment);
            if (updated == original) return catalog; // Idempotent, including a previous ambiguous push.
            await File.WriteAllTextAsync(path, updated, new UTF8Encoding(false), cancellationToken);
            var relativePath = "tags/" + fileName + ".md";
            await GitAsync(["add", "--", relativePath], null, cancellationToken);
            await GitAsync(["-c", "user.name=Tag editor", "-c", "user.email=tag-editor@users.noreply.github.com",
                "commit", "-m", $"Add {fragment} to tag {fileName}", "--", relativePath], null, cancellationToken);
            var push = await RunGitAsync(["push", Remote, $"HEAD:refs/heads/{Settings.Branch}"], token, cancellationToken);
            if (push.ExitCode == 0) return await ReadCatalogAsync(cancellationToken);
            // A concurrent commit may have advanced the branch. Fetch and reapply the
            // semantic edit, never force-push or publish an unconfirmed local commit.
        }
        // This also resolves a push accepted by GitHub whose response was lost.
        var current = await SynchronizeAsync(cancellationToken);
        if (current.Tags.Any(tag => tag.Slug.Equals(name, StringComparison.OrdinalIgnoreCase)
            && tag.Fragments.SelectMany(TagRepository.ExpandFragment).Contains(fragment, StringComparer.OrdinalIgnoreCase)))
            return current;
        throw new TagEditException(409,
            "GitHub did not accept the change. Check Contents write access and branch rules, then try again. No unconfirmed change was published.");
    }

    private async Task<TagCatalog> ReadCatalogAsync(CancellationToken cancellationToken) =>
        new(TagRepository.ReadTags(TagsPath), (await GitAsync(["rev-parse", "HEAD"], null, cancellationToken)).Trim());

    private async Task<string?> ReadTokenAsync(CancellationToken cancellationToken)
    {
        // An empty setting allows public repositories and offline test remotes.
        // A configured but unavailable secret must never fall back to a user's write token.
        if (string.IsNullOrWhiteSpace(Settings.ReadTokenFile)) return null;
        try
        {
            var token = (await File.ReadAllTextAsync(Settings.ReadTokenFile, cancellationToken)).Trim();
            if (token.Length is > 0 and <= 1024 && token.All(c => char.IsAsciiLetterOrDigit(c) || c == '_'))
                return token;
        }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
        throw new TagEditException(503, "The repository read credential is unavailable or invalid.");
    }

    private static void EnsureRegularDirectory(string path)
    {
        if (!Directory.Exists(path) || new DirectoryInfo(path).LinkTarget is not null)
            throw new TagEditException(409, "The repository must contain a regular tags directory.");
    }

    private async Task<string> GitAsync(string[] arguments, string? token, CancellationToken cancellationToken)
    {
        var result = await RunGitAsync(arguments, token, cancellationToken);
        if (result.ExitCode != 0) throw new TagEditException(503, "Repository synchronization failed. Please try again later.");
        return result.Output;
    }

    private async Task<(int ExitCode, string Output)> RunGitAsync(
        string[] arguments, string? token, CancellationToken cancellationToken)
    {
        var start = new ProcessStartInfo("git")
        {
            WorkingDirectory = Settings.RepositoryPath,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        foreach (var value in new[] { "-c", "credential.helper=", "-c", "core.hooksPath=/dev/null",
            "-c", "core.fsmonitor=false", "-c", "commit.gpgsign=false" }.Concat(arguments))
            start.ArgumentList.Add(value);
        // Do not inherit tracing, askpass helpers, alternate worktrees or credential
        // settings that could expose this request's token or redirect repository writes.
        foreach (var key in start.Environment.Keys.Where(key => key.StartsWith("GIT_", StringComparison.Ordinal)).ToArray())
            start.Environment.Remove(key);
        start.Environment["GIT_TERMINAL_PROMPT"] = "0";
        start.Environment["GIT_CONFIG_NOSYSTEM"] = "1";
        start.Environment["GIT_CONFIG_GLOBAL"] = "/dev/null";
        if (token is not null)
        {
            start.Environment["GIT_CONFIG_COUNT"] = "1";
            start.Environment["GIT_CONFIG_KEY_0"] = "http.https://github.com/.extraheader";
            start.Environment["GIT_CONFIG_VALUE_0"] = "Authorization: Basic "
                + Convert.ToBase64String(Encoding.UTF8.GetBytes("x-access-token:" + token));
        }
        using var process = Process.Start(start) ?? throw new InvalidOperationException("Could not start Git.");
        // Never return or log Git stderr: remote errors may contain sensitive headers.
        var stdout = process.StandardOutput.ReadToEndAsync();
        var stderr = process.StandardError.ReadToEndAsync();
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(45));
        try
        {
            await process.WaitForExitAsync(timeout.Token);
            await stderr;
            return (process.ExitCode, await stdout);
        }
        catch (OperationCanceledException)
        {
            if (!process.HasExited) process.Kill(entireProcessTree: true);
            await process.WaitForExitAsync(CancellationToken.None);
            await Task.WhenAll(stdout, stderr);
            throw new TagEditException(503, "The repository operation timed out. Its result will be reconciled automatically; retrying is safe.");
        }
    }
}
