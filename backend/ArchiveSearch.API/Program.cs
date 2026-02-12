using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using Anthropic;
using ArchiveSearch.API.Services;
using ArchiveSearch.Core.Cache;
using ArchiveSearch.Data;
using ArchiveSearch.Data.Repositories;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// ── Production: port check + URL binding ─────────────────────────────────
if (!builder.Environment.IsDevelopment())
{
    // Check if port 5265 is available before trying to bind
    try
    {
        using var listener = new TcpListener(IPAddress.Loopback, 5265);
        listener.Start();
        listener.Stop();
    }
    catch (SocketException)
    {
        Console.Error.WriteLine("ERROR: Port 5265 is already in use.");
        Console.Error.WriteLine("Is another instance of AIchivist running?");
        Console.Error.WriteLine("Close it and try again, or check Task Manager for AIchivist.exe.");
        Environment.Exit(1);
    }

    builder.WebHost.UseUrls("http://localhost:5265");
}

// ── File logging (production) ────────────────────────────────────────────
if (!builder.Environment.IsDevelopment())
{
    var logDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "AIchivist", "logs");
    Directory.CreateDirectory(logDir);

    builder.Logging.AddSimpleConsole(options =>
    {
        options.TimestampFormat = "[yyyy-MM-dd HH:mm:ss] ";
    });
}

// ── Configuration ──────────────────────────────────────────────────────────

// Load optional local config (desktop installs store API key + connection string here)
var appDataDir = Path.Combine(
    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
    "AIchivist", "config");
Directory.CreateDirectory(appDataDir);
var localSettingsPath = Path.Combine(appDataDir, "appsettings.local.json");
builder.Configuration.AddJsonFile(localSettingsPath, optional: true, reloadOnChange: true);

// API key — check User Secrets, environment variable, and local config
var anthropicApiKey = builder.Configuration["ANTHROPIC_API_KEY"];
var isSetupMode = string.IsNullOrWhiteSpace(anthropicApiKey);

var connectionString = Environment.GetEnvironmentVariable("CONNECTION_STRING")
    ?? builder.Configuration.GetConnectionString("Default")
    ?? "Host=localhost;Port=5432;Database=archive_search;Username=archive;Password=archive";

// ── Services ───────────────────────────────────────────────────────────────

builder.Services.AddControllers();
builder.Services.AddMemoryCache();

// PostgreSQL via EF Core
builder.Services.AddDbContext<ArchiveContext>(options =>
    options.UseNpgsql(connectionString));

// Anthropic client — use real key or dummy for setup mode
builder.Services.AddSingleton(new AnthropicClient
{
    ApiKey = isSetupMode ? "not-configured" : anthropicApiKey!
});

// Make setup state and config path available to controllers
builder.Services.AddSingleton(new SetupState(isSetupMode, localSettingsPath));

// Application services
builder.Services.AddScoped<CollectionRepository>();
builder.Services.AddSingleton<SearchCache>();
builder.Services.AddScoped<IndexingService>();
builder.Services.AddScoped<ClaudeService>();
builder.Services.AddScoped<SearchService>();

// CORS — allow Angular dev server and any configured frontend origin
var frontendOrigin = builder.Configuration["FrontendOrigin"] ?? "http://localhost:4200";
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
        policy.WithOrigins(frontendOrigin)
              .AllowAnyHeader()
              .AllowAnyMethod());
});

var app = builder.Build();

// ── Middleware ─────────────────────────────────────────────────────────────

app.UseCors();

// Setup mode: block API calls (except /api/setup and /api/health) when no API key configured
app.Use(async (context, next) =>
{
    var setupState = context.RequestServices.GetRequiredService<SetupState>();
    var path = context.Request.Path.Value ?? "";

    if (setupState.IsSetupMode
        && path.StartsWith("/api/", StringComparison.OrdinalIgnoreCase)
        && !path.StartsWith("/api/setup", StringComparison.OrdinalIgnoreCase)
        && !path.StartsWith("/api/health", StringComparison.OrdinalIgnoreCase))
    {
        context.Response.StatusCode = 503;
        context.Response.ContentType = "application/json";
        await context.Response.WriteAsync("""{"error":"Setup required","setupUrl":"/setup"}""");
        return;
    }

    await next();
});

app.UseDefaultFiles();
app.UseStaticFiles();
app.UseAuthorization();
app.MapControllers();
app.MapFallbackToFile("index.html");

// ── Startup: apply migrations ─────────────────────────────────────────────

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<ArchiveContext>();
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();

    try
    {
        await db.Database.MigrateAsync();
        logger.LogInformation("Database migrations applied.");
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Failed to apply database migrations. Ensure PostgreSQL is running.");
    }
}

// ── Open browser when server is ready (production only) ──────────────────

if (!app.Environment.IsDevelopment())
{
    app.Lifetime.ApplicationStarted.Register(() =>
    {
        var url = "http://localhost:5265";
        Console.WriteLine($"AIchivist is running at {url}");
        Console.WriteLine("Press Ctrl+C to stop.");
        try { Process.Start(new ProcessStartInfo(url) { UseShellExecute = true }); }
        catch { /* Ignore if browser launch fails */ }
    });
}

app.Run();

// ── Supporting types ─────────────────────────────────────────────────────

/// <summary>Tracks whether the app is in first-run setup mode (no API key configured).</summary>
public class SetupState(bool isSetupMode, string localSettingsPath)
{
    public bool IsSetupMode { get; set; } = isSetupMode;
    public string LocalSettingsPath { get; } = localSettingsPath;
}

// Enables WebApplicationFactory<Program> discovery for integration tests
public partial class Program { }
