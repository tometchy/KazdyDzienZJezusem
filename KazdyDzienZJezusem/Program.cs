using KazdyDzienZJezusem.Services;
using StackExchange.Redis;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllersWithViews();
builder.Services.AddSingleton<IBibleFileSystem, PhysicalBibleFileSystem>();
var tagRepository = new TagRepository(Path.Combine(AppContext.BaseDirectory, "Tags"));
builder.Services.AddSingleton(tagRepository);
var redisConnectionString = Environment.GetEnvironmentVariable("REDIS_CONNECTION_STRING");
if (!string.IsNullOrWhiteSpace(redisConnectionString))
{
    var redis = ConnectionMultiplexer.Connect(redisConnectionString);
    builder.Services.AddSingleton<IConnectionMultiplexer>(redis);
    tagRepository.LoadIntoRedis(redis.GetDatabase());
}
builder.Services.AddSingleton(serviceProvider => new BibleRepository(
    Path.Combine(AppContext.BaseDirectory, "Bible"),
    serviceProvider.GetRequiredService<IBibleFileSystem>(),
    serviceProvider.GetRequiredService<TagRepository>()));
builder.Services.AddSingleton(serviceProvider => new FathersRepository(
    Path.Combine(AppContext.BaseDirectory, "Fathers"),
    serviceProvider.GetRequiredService<TagRepository>()));

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseRouting();

app.UseAuthorization();

app.MapStaticAssets();

app.MapControllers()
    .WithStaticAssets();

app.Run();

public partial class Program
{
}
