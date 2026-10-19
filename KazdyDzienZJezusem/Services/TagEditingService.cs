using Microsoft.Extensions.Options;

namespace KazdyDzienZJezusem.Services;

public sealed class TagEditingService(
    ITagGitStore git, IRepositoryWriteAccess access, TagRepository tags,
    IOptions<TagEditingOptions> options, ILogger<TagEditingService> logger) : BackgroundService
{
    private readonly SemaphoreSlim _gate = new(1, 1);

    public async Task<bool> AddAsync(string name, string fragment, string token, CancellationToken cancellationToken)
    {
        if (!options.Value.Enabled) throw new TagEditException(503, "Tag editing is not configured on this server.");
        if (!await _gate.WaitAsync(0, cancellationToken))
            throw new TagEditException(409, "Another update is in progress. Please try again in a moment.");
        try
        {
            await access.CheckAsync(token, cancellationToken);
            // After authorization, finish/reconcile even if the browser disconnects.
            using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(3));
            var catalog = await git.AddAsync(name, fragment, token, timeout.Token);
            return tags.Publish(catalog.Tags, catalog.Revision);
        }
        finally { _gate.Release(); }
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!options.Value.Enabled) return;
        while (!stoppingToken.IsCancellationRequested)
        {
            await _gate.WaitAsync(stoppingToken);
            try
            {
                var catalog = await git.SynchronizeAsync(stoppingToken);
                if (!tags.Publish(catalog.Tags, catalog.Revision))
                    logger.LogWarning("The repository is synchronized; the Redis tag index will be retried.");
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { return; }
            catch (Exception)
            {
                // Deliberately omit exception details and process output: no credentials in logs.
                logger.LogWarning("Tag repository synchronization failed; retrying in 30 seconds.");
            }
            finally { _gate.Release(); }
            await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
        }
    }
}
