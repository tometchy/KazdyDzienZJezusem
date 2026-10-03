using System.Text.Json;
using StackExchange.Redis;

namespace KazdyDzienZJezusem.Services;

public sealed class RedisTagPublisher(
    IConfiguration configuration,
    TagRepository tagRepository,
    ILogger<RedisTagPublisher> logger) : IHostedService
{
    private IConnectionMultiplexer? _connection;

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        var connectionString = configuration.GetConnectionString("Redis");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return;
        }

        _connection = await ConnectionMultiplexer.ConnectAsync(connectionString);
        var database = _connection.GetDatabase();
        var transaction = database.CreateTransaction();
        _ = transaction.KeyDeleteAsync("tags:names");

        foreach (var tag in tagRepository.GetAll())
        {
            _ = transaction.SetAddAsync("tags:names", tag.Name);
            _ = transaction.StringSetAsync($"tag:{tag.Name}", JsonSerializer.Serialize(tag));
            foreach (var fragment in tag.Fragments)
            {
                _ = transaction.SetAddAsync($"fragment-tags:{fragment}", tag.Name);
            }
        }

        await transaction.ExecuteAsync();
        logger.LogInformation("Published {TagCount} repository tags to Redis", tagRepository.GetAll().Count);
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        if (_connection is not null)
        {
            await _connection.CloseAsync();
            _connection.Dispose();
        }
    }
}
