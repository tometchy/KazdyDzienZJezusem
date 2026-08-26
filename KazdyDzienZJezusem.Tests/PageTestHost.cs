using Microsoft.AspNetCore.Mvc.Testing;

namespace KazdyDzienZJezusem.Tests;

public sealed class PageTestHost : IDisposable
{
    public const string BaseUrlEnvironmentVariable = "KDJ_E2E_BASE_URL";

    private readonly WebApplicationFactory<Program>? _factory;

    public PageTestHost()
    {
        var configuredBaseUrl = Environment.GetEnvironmentVariable(BaseUrlEnvironmentVariable);
        if (string.IsNullOrWhiteSpace(configuredBaseUrl))
        {
            _factory = new StubbedWebApplicationFactory();
            Client = _factory.CreateClient(new WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false
            });
            return;
        }

        var baseAddress = ParseBaseAddress(configuredBaseUrl);
        Client = new HttpClient(new HttpClientHandler
        {
            AllowAutoRedirect = false
        })
        {
            BaseAddress = baseAddress,
            Timeout = TimeSpan.FromSeconds(30)
        };
    }

    public HttpClient Client { get; }

    public void Dispose()
    {
        Client.Dispose();
        _factory?.Dispose();
    }

    private static Uri ParseBaseAddress(string configuredBaseUrl)
    {
        if (!Uri.TryCreate(configuredBaseUrl, UriKind.Absolute, out var baseAddress)
            || (baseAddress.Scheme != Uri.UriSchemeHttp
                && baseAddress.Scheme != Uri.UriSchemeHttps))
        {
            throw new InvalidOperationException(
                $"{BaseUrlEnvironmentVariable} must be an absolute HTTP or HTTPS URL.");
        }

        return new Uri($"{baseAddress.AbsoluteUri.TrimEnd('/')}/", UriKind.Absolute);
    }
}
