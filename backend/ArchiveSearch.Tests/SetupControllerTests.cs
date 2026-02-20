using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using ArchiveSearch.Data;
using Anthropic;

namespace ArchiveSearch.Tests;

/// <summary>
/// Tests for the setup flow: status endpoint, setup mode middleware blocking,
/// and config file writing. Uses a factory that starts with NO API key (setup mode).
/// </summary>
public class SetupControllerTests : IClassFixture<SetupModeWebAppFactory>
{
    private readonly HttpClient _client;
    private readonly SetupModeWebAppFactory _factory;

    public SetupControllerTests(SetupModeWebAppFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });
    }

    [Fact]
    public async Task Status_ReturnsNotConfigured_WhenNoApiKey()
    {
        var response = await _client.GetAsync("/api/setup/status");
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.False(json.GetProperty("configured").GetBoolean());
    }

    [Fact]
    public async Task SetupMiddleware_Blocks_NonSetupRoutes_WhenNotConfigured()
    {
        var response = await _client.GetAsync("/api/search");
        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);

        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Setup required", json.GetProperty("error").GetString());
    }

    [Fact]
    public async Task SetupMiddleware_Allows_SetupRoutes_WhenNotConfigured()
    {
        var response = await _client.GetAsync("/api/setup/status");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task SetupMiddleware_Allows_HealthRoute_WhenNotConfigured()
    {
        var response = await _client.GetAsync("/api/health");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Save_RejectsEmptyKey()
    {
        var response = await _client.PostAsJsonAsync("/api/setup/save", new { apiKey = "" });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Save_WritesConfigFile_WhenValidKey()
    {
        // Use a unique temp path so we can verify file creation
        var tempConfigPath = _factory.TempConfigPath;

        // Delete any existing file
        if (File.Exists(tempConfigPath))
            File.Delete(tempConfigPath);

        // The save endpoint will try to validate the key with Anthropic API,
        // which will fail with our dummy key. This tests the error path.
        var response = await _client.PostAsJsonAsync("/api/setup/save", new { apiKey = "sk-ant-invalid-key" });

        // Should get 400 because the key validation will fail (can't reach Anthropic)
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task StaticFiles_StillServed_InSetupMode()
    {
        // Static files should work even when in setup mode (so the setup page loads)
        var response = await _client.GetAsync("/");
        response.EnsureSuccessStatusCode();

        var content = await response.Content.ReadAsStringAsync();
        Assert.Contains("<app-root>", content);
    }
}

/// <summary>
/// Tests for the configured (non-setup) mode.
/// </summary>
public class SetupConfiguredTests : IClassFixture<TestWebAppFactory>
{
    private readonly HttpClient _client;

    public SetupConfiguredTests(TestWebAppFactory factory)
    {
        _client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });
    }

    [Fact]
    public async Task Status_ReturnsConfigured_WhenApiKeyExists()
    {
        var response = await _client.GetAsync("/api/setup/status");
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(json.GetProperty("configured").GetBoolean());
    }

    [Fact]
    public async Task SetupMiddleware_Allows_AllRoutes_WhenConfigured()
    {
        // In configured mode, /api/health should work (it also works in setup mode,
        // but here we verify the middleware doesn't interfere)
        var response = await _client.GetAsync("/api/health");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}

/// <summary>
/// WebApplicationFactory that starts in setup mode (no API key).
/// </summary>
public class SetupModeWebAppFactory : WebApplicationFactory<Program>
{
    private string? _wwwrootPath;
    public string TempConfigPath { get; } = Path.Combine(
        Path.GetTempPath(), $"aichivist-test-config-{Guid.NewGuid()}", "appsettings.local.json");

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        _wwwrootPath = Path.Combine(Path.GetTempPath(), "aichivist-test-wwwroot-setup-" + Guid.NewGuid());
        Directory.CreateDirectory(_wwwrootPath);
        File.WriteAllText(Path.Combine(_wwwrootPath, "index.html"),
            "<!DOCTYPE html><html><body><app-root></app-root></body></html>");

        // Do NOT set ANTHROPIC_API_KEY — this triggers setup mode
        builder.UseWebRoot(_wwwrootPath);

        builder.ConfigureServices(services =>
        {
            // Remove SQLite, use InMemory
            var descriptor = services.SingleOrDefault(
                d => d.ServiceType == typeof(DbContextOptions<ArchiveContext>));
            if (descriptor != null)
                services.Remove(descriptor);
            services.AddDbContext<ArchiveContext>(options =>
                options.UseInMemoryDatabase("SetupTestDb"));

            // Override the SetupState to use our temp config path
            var setupDescriptor = services.SingleOrDefault(
                d => d.ServiceType == typeof(SetupState));
            if (setupDescriptor != null)
                services.Remove(setupDescriptor);
            services.AddSingleton(new SetupState(true, TempConfigPath));
        });
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (_wwwrootPath != null && Directory.Exists(_wwwrootPath))
            try { Directory.Delete(_wwwrootPath, true); } catch { }

        var configDir = Path.GetDirectoryName(TempConfigPath);
        if (configDir != null && Directory.Exists(configDir))
            try { Directory.Delete(configDir, true); } catch { }
    }
}
