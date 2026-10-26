using KazdyDzienZJezusem.Services;
using System.Text.RegularExpressions;
using System.Threading.RateLimiting;
using Microsoft.Extensions.Options;
using Microsoft.AspNetCore.DataProtection;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllersWithViews();
builder.Services.AddSingleton<EditorSessions>();
var protection = builder.Services.AddDataProtection().SetApplicationName("KazdyDzienZJezusem");
if (builder.Configuration["EditorSessions:KeyPath"] is { Length: > 0 } keyPath)
    protection.PersistKeysToFileSystem(new DirectoryInfo(keyPath));
builder.Services.AddAntiforgery(options =>
{
    options.HeaderName = "X-CSRF-TOKEN";
    options.Cookie.SameSite = SameSiteMode.Strict;
    // CookiePolicy applies Secure to the outgoing cookie in production, including
    // when Cloudflare terminates HTTPS and forwards HTTP to this container.
    options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
});
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy("greek-interlinear", context => RateLimitPartition.GetFixedWindowLimiter(
        context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions { PermitLimit = 180, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));
    options.AddPolicy("editor", context => RateLimitPartition.GetFixedWindowLimiter(
        context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions { PermitLimit = 20, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));
});
builder.Services.AddOptions<GreekInterlinearOptions>().BindConfiguration("GreekInterlinear")
    .Validate(options => Uri.TryCreate(options.Endpoint, UriKind.Absolute, out var endpoint)
        && (endpoint.Scheme == "http" || endpoint.Scheme == "https"), "Invalid Greek translation endpoint.")
    .ValidateOnStart();
builder.Services.AddHttpClient("GreekInterlinear", client => client.Timeout = TimeSpan.FromMinutes(2))
    .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler { AllowAutoRedirect = false });
builder.Services.AddSingleton<GreekInterlinearService>();
builder.Services.AddOptions<TagEditingOptions>().BindConfiguration("TagEditing")
    .Validate(options => Regex.IsMatch(options.Repository, @"^[A-Za-z0-9_.-]+/[A-Za-z0-9_.-]+$")
        && Regex.IsMatch(options.Branch, @"^[A-Za-z0-9][A-Za-z0-9/_-]*$")
        && (!options.Enabled || Path.IsPathFullyQualified(options.RepositoryPath)), "Invalid tag editing configuration.")
    .ValidateOnStart();
builder.Services.AddHttpClient<IRepositoryWriteAccess, GitHubWriteAccess>(client => client.Timeout = TimeSpan.FromSeconds(15))
    .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler { AllowAutoRedirect = false });
builder.Services.AddSingleton<ITagGitStore, TagGitStore>();
builder.Services.AddSingleton<FragmentValidator>();
builder.Services.AddSingleton<TagPassageReader>();
builder.Services.AddSingleton<TagEditingService>();
builder.Services.AddHostedService(provider => provider.GetRequiredService<TagEditingService>());
builder.Services.AddSingleton<IBibleFileSystem, PhysicalBibleFileSystem>();
builder.Services.AddSingleton(serviceProvider => new BibleRepository(
    Path.Combine(AppContext.BaseDirectory, "Bible"),
    serviceProvider.GetRequiredService<IBibleFileSystem>()));
builder.Services.AddSingleton(new FathersRepository(
    Path.Combine(AppContext.BaseDirectory, "Fathers")));
builder.Services.AddSingleton(provider => new TagRepository(
    Path.Combine(AppContext.BaseDirectory, "Tags"),
    builder.Configuration["Tags:RedisConnection"],
    seedRedis: !provider.GetRequiredService<IOptions<TagEditingOptions>>().Value.Enabled));

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseCookiePolicy(new CookiePolicyOptions
{
    Secure = app.Environment.IsDevelopment() ? CookieSecurePolicy.SameAsRequest : CookieSecurePolicy.Always
});
app.UseRouting();
app.UseRateLimiter();
app.Use(async (context, next) =>
{
    // Session-aware UI and fragments must not be reused from a shared proxy cache.
    context.Response.Headers.CacheControl = "no-store";
    context.Response.Headers["X-Content-Type-Options"] = "nosniff";
    context.Response.Headers["Referrer-Policy"] = "same-origin";
    context.Response.Headers["X-Frame-Options"] = "DENY";
    await next();
});

app.UseAuthorization();

app.MapStaticAssets();

app.MapControllers()
    .WithStaticAssets();

app.Run();

public partial class Program
{
}
