using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using KazdyDzienZJezusem.Models;
using KazdyDzienZJezusem.Services;
using StackExchange.Redis;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;

namespace KazdyDzienZJezusem.Tests;

public sealed class RedisTagRepositoryTests
{
    [Fact]
    public async Task ConfirmedChangesSurviveRedisOutageAndRepopulateAnEmptyRedis()
    {
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root is not null && !File.Exists(Path.Combine(root.FullName, "compose.yaml"))) root = root.Parent;
        var executable = Environment.GetEnvironmentVariable("KDJ_REDIS_SERVER")
            ?? Path.Combine(root!.FullName, "artifacts/visual-tools/redis-8.2.2/src/redis-server");
        Assert.SkipUnless(File.Exists(executable), "Run scripts/setup-visual-tests.sh to enable the isolated Redis recovery test.");
        var directory = Directory.CreateTempSubdirectory("kdj-redis-test-").FullName;
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        Process? process = null;
        var cancellationToken = TestContext.Current.CancellationToken;
        try
        {
            process = Start();
            var connectionString = $"127.0.0.1:{port},abortConnect=false,syncTimeout=300,connectTimeout=300,connectRetry=0";
            using var connection = await ConnectionMultiplexer.ConnectAsync(connectionString);
            await WaitUntil(() => Task.FromResult(connection.IsConnected));
            using var writer = new TagRepository(directory, connectionString, seedRedis: false);
            using var reader = new TagRepository(directory, connectionString, seedRedis: false);
            ContentTag[] first = [new("first", "first", ["Ob15,1"], "")];
            Assert.True(writer.Publish(first, "revision-1"));
            Assert.Equal("first", reader.GetTags("Ob15,1").Single().Slug);
            var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
                { ["Tags:RedisConnection"] = connectionString }).Build();
            var keyDirectory = Directory.CreateDirectory(Path.Combine(directory, "keys"));
            string cookie;
            using (var sessions = new EditorSessions(new EnvironmentStub(), DataProtectionProvider.Create(keyDirectory), configuration))
            {
                var login = new DefaultHttpContext();
                sessions.SignIn(login, "test-only-secret");
                cookie = login.Response.Headers.SetCookie.Last()!.Split(';')[0];
                Assert.Equal(64, cookie.Split('=')[1].Length);
            }
            using (var restartedSessions = new EditorSessions(new EnvironmentStub(), DataProtectionProvider.Create(keyDirectory), configuration))
            {
                var request = new DefaultHttpContext();
                request.Request.Headers.Cookie = cookie;
                Assert.Equal("test-only-secret", restartedSessions.GetToken(request));
                var encrypted = (string?)await connection.GetDatabase().StringGetAsync("editor:sessions:" + cookie.Split('=')[1]);
                Assert.NotNull(encrypted);
                Assert.DoesNotContain("test-only-secret", encrypted);
                restartedSessions.SignOut(request);
                Assert.Null(restartedSessions.GetToken(request));
            }
            process.Kill();
            await process.WaitForExitAsync(cancellationToken);
            process.Dispose();
            process = null;
            ContentTag[] second = [..first, new("second", "second", ["Ignatius/Ef1,1"], "")];
            Assert.False(writer.Publish(second, "revision-2"));
            Assert.Equal("second", writer.GetTags("Ignatius/Ef1,1").Single().Slug);
            process = Start();
            await WaitUntil(() => Task.FromResult(writer.Publish(second, "revision-2")));
            await WaitUntil(() => Task.FromResult(reader.GetTags("Ignatius/Ef1,1").Any(tag => tag.Slug == "second")));
            // A new process must not replace the recovered index with its older baked files.
            using var restarted = new TagRepository(directory, connectionString, seedRedis: false);
            Assert.Equal(2, restarted.GetAll().Count);
        }
        finally
        {
            if (process is not null)
            {
                if (!process.HasExited) process.Kill();
                await process.WaitForExitAsync(CancellationToken.None);
                process.Dispose();
            }
            Directory.Delete(directory, true);
        }

        Process Start()
        {
            var start = new ProcessStartInfo(executable) { RedirectStandardOutput = true, RedirectStandardError = true };
            foreach (var argument in new[] { "--bind", "127.0.0.1", "--port", port.ToString(), "--dir", directory,
                "--save", "", "--appendonly", "no" }) start.ArgumentList.Add(argument);
            var child = Process.Start(start)!;
            child.BeginOutputReadLine();
            child.BeginErrorReadLine();
            return child;
        }

        async Task WaitUntil(Func<Task<bool>> condition)
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(TimeSpan.FromSeconds(25));
            while (!await condition()) await Task.Delay(100, timeout.Token);
        }
    }

    private sealed class EnvironmentStub : IWebHostEnvironment
    {
        public string EnvironmentName { get; set; } = "Development";
        public string ApplicationName { get; set; } = "SessionTest";
        public string WebRootPath { get; set; } = "";
        public string ContentRootPath { get; set; } = "";
        public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
