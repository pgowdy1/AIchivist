using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using Anthropic;
using ArchiveSearch.API.Services;
using ArchiveSearch.Core.Cache;
using ArchiveSearch.Data;
using ArchiveSearch.Data.Repositories;
using Microsoft.EntityFrameworkCore;

// ── On-demand PostgreSQL (production only) ────────────────────────────────
// Start PostgreSQL before anything else so the database is ready for EF Core.
// Only attempt this when the bundled pg_ctl.exe exists (desktop/installer builds).
{
    var pgCtl = Path.Combine(AppContext.BaseDirectory, "pgsql", "bin", "pg_ctl.exe");
    if (File.Exists(pgCtl))
    {
        await Program.StartPostgreSqlAsync();
    }
}

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
if (!builder.Environment.IsDevelopment())
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

// Anthropic client — scoped so each request gets the current key from config
// (reloadOnChange picks up the key after first-run setup saves it to disk)
builder.Services.AddScoped(_ =>
{
    var key = builder.Configuration["ANTHROPIC_API_KEY"];
    return new AnthropicClient { ApiKey = string.IsNullOrWhiteSpace(key) ? "not-configured" : key };
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

// ── PostgreSQL shutdown handler ───────────────────────────────────────────
// Stop the bundled PostgreSQL when the application is stopping, so it does not
// linger as an orphaned process after AIchivist exits.
app.Lifetime.ApplicationStopping.Register(() =>
{
    Program.StopPostgreSql();
});

// ── Startup: apply migrations ─────────────────────────────────────────────

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<ArchiveContext>();
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();

    try
    {
        await db.Database.MigrateAsync();
        logger.LogInformation("Database migrations applied.");

        var collectionCount = await db.Collections.CountAsync();
        if (collectionCount == 0)
            logger.LogWarning("Database is empty — no collections found. Search will return no results. " +
                              "Run the indexing endpoint or check that the database dump was restored.");
        else
            logger.LogInformation("Database contains {Count} collections.", collectionCount);
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Failed to connect to database or apply migrations. " +
                            "Ensure PostgreSQL is running on the configured port. " +
                            "Connection string: {ConnectionString}",
                            connectionString.Contains("Password=")
                                ? connectionString[..connectionString.IndexOf("Password=")] + "Password=***"
                                : connectionString);
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
public partial class Program
{
    private static Process? _postgresProcess;

    /// <summary>
    /// Starts the bundled PostgreSQL instance using pg_ctl if it is not already running.
    /// PostgreSQL runs on port 5433 (desktop port) with the data directory at pgsql/data/.
    /// </summary>
    internal static async Task StartPostgreSqlAsync()
    {
        var pgCtl = Path.Combine(AppContext.BaseDirectory, "pgsql", "bin", "pg_ctl.exe");
        var dataDir = Path.Combine(AppContext.BaseDirectory, "pgsql", "data");

        if (!File.Exists(pgCtl))
        {
            Console.WriteLine("[PostgreSQL] pg_ctl.exe not found, skipping managed startup.");
            return;
        }

        if (!Directory.Exists(dataDir))
        {
            Console.Error.WriteLine($"[PostgreSQL] Data directory not found: {dataDir}");
            return;
        }

        try
        {
            // Check if PostgreSQL is already running
            var statusProcess = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = pgCtl,
                    Arguments = $"status -D \"{dataDir}\"",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };

            statusProcess.Start();
            await statusProcess.WaitForExitAsync();

            // Exit code 0 means PostgreSQL is already running
            if (statusProcess.ExitCode == 0)
            {
                Console.WriteLine("[PostgreSQL] Already running, skipping startup.");
                return;
            }

            // Start PostgreSQL
            Console.WriteLine("[PostgreSQL] Starting...");
            var startProcess = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = pgCtl,
                    Arguments = $"start -D \"{dataDir}\" -w -o \"-p 5433\"",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };

            startProcess.Start();
            _postgresProcess = startProcess;

            var stdout = await startProcess.StandardOutput.ReadToEndAsync();
            var stderr = await startProcess.StandardError.ReadToEndAsync();
            await startProcess.WaitForExitAsync();

            if (startProcess.ExitCode == 0)
            {
                Console.WriteLine("[PostgreSQL] Started successfully on port 5433.");
            }
            else
            {
                Console.Error.WriteLine($"[PostgreSQL] Failed to start (exit code {startProcess.ExitCode}).");
                if (!string.IsNullOrWhiteSpace(stdout))
                    Console.Error.WriteLine($"[PostgreSQL] stdout: {stdout.Trim()}");
                if (!string.IsNullOrWhiteSpace(stderr))
                    Console.Error.WriteLine($"[PostgreSQL] stderr: {stderr.Trim()}");
            }

            // Give PostgreSQL a moment to fully accept connections
            await Task.Delay(2000);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[PostgreSQL] Error during startup: {ex.Message}");
        }
    }

    /// <summary>
    /// Stops the bundled PostgreSQL instance using pg_ctl with "fast" shutdown mode.
    /// Waits up to 5 seconds for graceful shutdown.
    /// </summary>
    internal static void StopPostgreSql()
    {
        var pgCtl = Path.Combine(AppContext.BaseDirectory, "pgsql", "bin", "pg_ctl.exe");
        var dataDir = Path.Combine(AppContext.BaseDirectory, "pgsql", "data");

        if (!File.Exists(pgCtl))
            return;

        try
        {
            Console.WriteLine("[PostgreSQL] Stopping...");
            var stopProcess = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = pgCtl,
                    Arguments = $"stop -D \"{dataDir}\" -m fast",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };

            stopProcess.Start();

            // Wait up to 5 seconds for graceful shutdown
            if (stopProcess.WaitForExit(5000))
            {
                if (stopProcess.ExitCode == 0)
                    Console.WriteLine("[PostgreSQL] Stopped successfully.");
                else
                    Console.Error.WriteLine($"[PostgreSQL] Stop returned exit code {stopProcess.ExitCode}.");
            }
            else
            {
                Console.Error.WriteLine("[PostgreSQL] Stop timed out after 5 seconds.");
            }

            _postgresProcess = null;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[PostgreSQL] Error during shutdown: {ex.Message}");
        }
    }
}