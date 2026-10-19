using System.Security.Cryptography;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Caching.Memory;
using StackExchange.Redis;

namespace KazdyDzienZJezusem.Services;

// Only an opaque, revocable identifier leaves the server. Redis stores encrypted
// tokens with a fixed expiry; plaintext tokens exist only in server memory.
public sealed class EditorSessions : IDisposable
{
    private readonly MemoryCache _sessions = new(new MemoryCacheOptions { SizeLimit = 1000 });
    private readonly IWebHostEnvironment _environment;
    private readonly IDataProtector _protector;
    private readonly ConnectionMultiplexer? _redis;
    private string CookieName => _environment.IsDevelopment() ? "kdj-editor" : "__Host-kdj-editor";

    public EditorSessions(IWebHostEnvironment environment, IDataProtectionProvider protection, IConfiguration configuration)
    {
        _environment = environment;
        _protector = protection.CreateProtector("KazdyDzienZJezusem.EditorSessions.v1");
        if (configuration["Tags:RedisConnection"] is { Length: > 0 } connectionString)
        {
            var options = ConfigurationOptions.Parse(connectionString);
            options.AbortOnConnectFail = false;
            _redis = ConnectionMultiplexer.Connect(options);
        }
    }

    public string? GetToken(HttpContext context)
    {
        if (!context.Request.Cookies.TryGetValue(CookieName, out var id) || id.Length != 64) return null;
        try
        {
            var encrypted = _redis is null ? _sessions.Get<string>(id)
                : (string?)_redis.GetDatabase().StringGet("editor:sessions:" + id);
            return encrypted is null ? null : _protector.Unprotect(encrypted);
        }
        catch (CryptographicException) { return null; }
        catch (RedisException) { return null; } // Fail closed if session storage is unavailable.
    }

    public void SignIn(HttpContext context, string token)
    {
        SignOut(context);
        var id = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        var encrypted = _protector.Protect(token);
        if (_redis is not null)
        {
            try
            {
                _redis.GetDatabase().StringSet("editor:sessions:" + id, encrypted, TimeSpan.FromHours(8));
            }
            catch (RedisException) { throw new TagEditException(503, "Login is temporarily unavailable. Please try again later."); }
        }
        else _sessions.Set(id, encrypted, new MemoryCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(8),
            Size = 1
        });
        context.Response.Cookies.Append(CookieName, id, CookieOptions());
    }

    public void SignOut(HttpContext context)
    {
        if (context.Request.Cookies.TryGetValue(CookieName, out var id))
        {
            _sessions.Remove(id);
            if (_redis is not null)
            {
                try { _redis.GetDatabase().KeyDelete("editor:sessions:" + id); }
                catch (RedisException) { throw new TagEditException(503, "Could not end the session. Please try again when the connection recovers."); }
            }
        }
        context.Response.Cookies.Delete(CookieName, CookieOptions());
    }

    private CookieOptions CookieOptions() => new()
    {
        HttpOnly = true,
        Secure = !_environment.IsDevelopment(),
        SameSite = SameSiteMode.Strict,
        Path = "/",
        MaxAge = TimeSpan.FromHours(8),
        IsEssential = true
    };

    public void Dispose()
    {
        _sessions.Dispose();
        _redis?.Dispose();
    }
}
