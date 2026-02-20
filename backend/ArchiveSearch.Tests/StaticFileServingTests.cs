using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using ArchiveSearch.Data;
using Anthropic;

namespace ArchiveSearch.Tests;

public class StaticFileServingTests : IClassFixture<TestWebAppFactory>
{
    private readonly HttpClient _client;

    public StaticFileServingTests(TestWebAppFactory factory)
    {
        _client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });
    }

    [Fact]
    public async Task Root_ReturnsHtml_WithAppRoot()
    {
        var response = await _client.GetAsync("/");
        response.EnsureSuccessStatusCode();

        var content = await response.Content.ReadAsStringAsync();
        Assert.Contains("<app-root>", content);
    }

    [Fact]
    public async Task HealthEndpoint_Returns200()
    {
        var response = await _client.GetAsync("/api/health");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task UnknownRoute_FallsBackToIndexHtml()
    {
        var response = await _client.GetAsync("/some/unknown/angular/route");
        response.EnsureSuccessStatusCode();

        var content = await response.Content.ReadAsStringAsync();
        Assert.Contains("<app-root>", content);
    }

    [Fact]
    public async Task ApiSearchEndpoint_MapsToController_NotFallback()
    {
        // POST to /api/search without a body should return 400 (bad request),
        // NOT the index.html fallback — proving the route maps to the controller.
        var response = await _client.PostAsync("/api/search", null);
        Assert.NotEqual(HttpStatusCode.OK, response.StatusCode);

        var content = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain("<app-root>", content);
    }

    [Fact]
    public async Task StaticAsset_IsServed()
    {
        var response = await _client.GetAsync("/favicon.ico");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}

/// <summary>
/// Custom WebApplicationFactory that replaces SQLite with InMemory DB,
/// provides a dummy API key, and creates a minimal wwwroot for static file tests.
/// </summary>
public class TestWebAppFactory : WebApplicationFactory<Program>
{
    private string? _wwwrootPath;

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // Create a temporary wwwroot with a minimal index.html
        _wwwrootPath = Path.Combine(Path.GetTempPath(), "aichivist-test-wwwroot-" + Guid.NewGuid());
        Directory.CreateDirectory(_wwwrootPath);
        File.WriteAllText(Path.Combine(_wwwrootPath, "index.html"),
            "<!DOCTYPE html><html><body><app-root></app-root></body></html>");
        File.WriteAllBytes(Path.Combine(_wwwrootPath, "favicon.ico"), [0]);

        builder.UseSetting("ANTHROPIC_API_KEY", "sk-ant-test-dummy-key");
        builder.UseWebRoot(_wwwrootPath);

        builder.ConfigureServices(services =>
        {
            // Remove the real SQLite DbContext registration
            var descriptor = services.SingleOrDefault(
                d => d.ServiceType == typeof(DbContextOptions<ArchiveContext>));
            if (descriptor != null)
                services.Remove(descriptor);

            // Add InMemory DB instead
            services.AddDbContext<ArchiveContext>(options =>
                options.UseInMemoryDatabase("TestDb"));

            // Replace AnthropicClient with a dummy (won't make real API calls)
            var clientDescriptor = services.SingleOrDefault(
                d => d.ServiceType == typeof(AnthropicClient));
            if (clientDescriptor != null)
                services.Remove(clientDescriptor);
            services.AddSingleton(new AnthropicClient { ApiKey = "sk-ant-test-dummy-key" });
        });
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (_wwwrootPath != null && Directory.Exists(_wwwrootPath))
        {
            try { Directory.Delete(_wwwrootPath, true); } catch { }
        }
    }
}
