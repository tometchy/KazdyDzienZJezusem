using KazdyDzienZJezusem.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllersWithViews();
builder.Services.AddSingleton<IBibleFileSystem, PhysicalBibleFileSystem>();
builder.Services.AddSingleton(serviceProvider => new BibleRepository(
    Path.Combine(AppContext.BaseDirectory, "Bible"),
    serviceProvider.GetRequiredService<IBibleFileSystem>()));

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
