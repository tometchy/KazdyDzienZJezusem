using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text.Json;
using KazdyDzienZJezusem.Services;
using Microsoft.Extensions.Logging.Abstractions;
using StackExchange.Redis;

namespace KazdyDzienZJezusem.Tests;

public sealed class GreekDictionaryRedisTests
{
    [Fact]
    public async Task Imports_file_reads_Redis_and_repairs_missing_hash_without_touching_other_data()
    {
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root is not null && !File.Exists(Path.Combine(root.FullName, "compose.yaml"))) root = root.Parent;
        var executable = Environment.GetEnvironmentVariable("KDJ_REDIS_SERVER")
            ?? Path.Combine(root!.FullName, "artifacts/visual-tools/redis-8.2.2/src/redis-server");
        Assert.SkipUnless(File.Exists(executable), "Run scripts/setup-visual-tests.sh for the isolated Redis test.");
        var directory = Directory.CreateTempSubdirectory("kdj-greek-redis-").FullName;
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        var start = new ProcessStartInfo(executable) { RedirectStandardOutput = true, RedirectStandardError = true };
        foreach (var argument in new[] { "--bind", "127.0.0.1", "--port", port.ToString(), "--dir", directory, "--save", "", "--appendonly", "no" })
            start.ArgumentList.Add(argument);
        using var process = Process.Start(start)!;
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();
        var cancellationToken = TestContext.Current.CancellationToken;
        var connectionString = $"127.0.0.1:{port},abortConnect=false,connectTimeout=3000";
        var source = Path.Combine(AppContext.BaseDirectory, "GreekPolish", "interlinear.jsonl.gz");
        var copy = Path.Combine(directory, "dictionary.jsonl.gz");
        File.Copy(source, copy);
        try
        {
            using var redis = await ConnectionMultiplexer.ConnectAsync(connectionString);
            var database = redis.GetDatabase();
            await database.StringSetAsync("tags:sentinel", "preserve");
            string prefix;
            using (var repository = new GreekDictionaryRepository(copy, connectionString, NullLogger<GreekDictionaryRepository>.Instance))
            {
                await repository.StartAsync(cancellationToken);
                var lookup = await repository.LookupAsync("J1,1", [GreekText.FormKey("θεός")], cancellationToken);
                Assert.Equal("początku", lookup.Passage!.Polish[1]);
                prefix = (string)(await database.StringGetAsync("greek-polish:active"))!;
                Assert.Equal(7940, await database.HashLengthAsync(prefix + ":passages"));
                Assert.Equal(18433, await database.HashLengthAsync(prefix + ":forms"));
                File.Delete(copy); // A reader must not fall back to its input file.
                await database.HashSetAsync(prefix + ":forms", GreekText.FormKey("θεός"),
                    JsonSerializer.Serialize(new[] { new GreekDictionaryForm("Redis-only test value", "N") }));
                var fromRedis = await repository.LookupAsync(null, [GreekText.FormKey("θεός")], cancellationToken);
                Assert.Equal("Redis-only test value", fromRedis.Forms[GreekText.FormKey("θεός")][0].Polish);
                await repository.StopAsync(cancellationToken);
            }
            File.Copy(source, copy);
            await database.KeyDeleteAsync(prefix + ":forms");
            using (var restarted = new GreekDictionaryRepository(copy, connectionString, NullLogger<GreekDictionaryRepository>.Instance))
            {
                await restarted.StartAsync(cancellationToken);
                var restored = await restarted.LookupAsync(null, [GreekText.FormKey("θεός")], cancellationToken);
                Assert.Contains(restored.Forms[GreekText.FormKey("θεός")], form => form.Polish == "Bóg");
                Assert.Equal(18433, await database.HashLengthAsync(prefix + ":forms"));
                Assert.Equal("preserve", (string?)await database.StringGetAsync("tags:sentinel"));
                await restarted.StopAsync(cancellationToken);
            }
        }
        finally
        {
            if (!process.HasExited) process.Kill();
            await process.WaitForExitAsync(CancellationToken.None);
            Directory.Delete(directory, recursive: true);
        }
    }
}
