using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text.Json.Nodes;
using Anthropic;
using ArchiveSearch.API.Services;
using ArchiveSearch.Core.Cache;
using ArchiveSearch.Data;
using ArchiveSearch.Data.Repositories;
using Microsoft.EntityFrameworkCore;

// ── On-demand PostgreSQL (desktop mode only) ──────────────────────────────
// Self-heal config, then start PostgreSQL before anything else so the database
// is ready for EF Core. Only when bundled pg_ctl.exe exists (installer builds).
{
    if (Program.IsDesktopMode)
    {
        Program.EnsureDesktopConfig();
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

// Safety net: in desktop mode, ensure connection string targets port 5433 regardless
// of config file state. Catches deleted/stale config or wrong env var.
if (Program.IsDesktopMode && !connectionString.Contains($"Port={Program.DesktopPgPort}"))
{
    Console.WriteLine("[PostgreSQL] Desktop mode: overriding connection string to port " + Program.DesktopPgPort);
    if (connectionString.Contains("Port="))
        connectionString = System.Text.RegularExpressions.Regex.Replace(
            connectionString, @"Port=\d+", $"Port={Program.DesktopPgPort}");
    else
        connectionString = connectionString.Replace("Host=localhost;",
            $"Host=localhost;Port={Program.DesktopPgPort};");
}

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
        var sanitizedConn = connectionString.Contains("Password=")
            ? connectionString[..connectionString.IndexOf("Password=")] + "Password=***"
            : connectionString;
        logger.LogError(ex, "Failed to connect to database or apply migrations. " +
                            "Connection string: {ConnectionString}", sanitizedConn);

        if (Program.IsDesktopMode)
        {
            Program.FatalDesktopError(
                "Could not connect to the database.\n\n" +
                $"Connection: {sanitizedConn}\n\n" +
                "PostgreSQL may have started but is not responding, " +
                "or the database may need to be reinitialized.");
        }
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
    // ── Desktop PostgreSQL constants ──────────────────────────────────────────
    internal const int DesktopPgPort = 5433;
    private const string DesktopConnectionString =
        "Host=localhost;Port=5433;Database=archive_search;Username=archive;Password=archive";
    private const int PgStartMaxAttempts = 3;
    private static readonly int[] PgRetryDelaysMs = [2000, 4000];

    /// <summary>True when the bundled pg_ctl.exe exists (desktop/installer mode).</summary>
    internal static bool IsDesktopMode =>
        File.Exists(Path.Combine(AppContext.BaseDirectory, "pgsql", "bin", "pg_ctl.exe"));

    // ── Fatal error dialog (P/Invoke) ────────────────────────────────────────

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern int MessageBox(IntPtr hWnd, string text, string caption, uint type);

    private const uint MB_OK = 0x0;
    private const uint MB_ICONERROR = 0x10;

    /// <summary>
    /// Shows a native Windows error dialog and exits the process.
    /// Used only in desktop mode when a fatal startup error occurs.
    /// </summary>
    internal static void FatalDesktopError(string message)
    {
        var fullMessage = message + "\n\n" +
            "Troubleshooting:\n" +
            "  1. Check if another instance of AIchivist is running\n" +
            "  2. Check if port 5433 is in use by another program\n" +
            "  3. Try restarting your computer\n" +
            "  4. Reinstall AIchivist if the problem persists";

        Console.Error.WriteLine($"[FATAL] {message}");

        try { MessageBox(IntPtr.Zero, fullMessage, "AIchivist \u2014 Startup Error", MB_OK | MB_ICONERROR); }
        catch { /* P/Invoke failure should not prevent exit */ }

        Environment.Exit(1);
    }

    // ── Config self-healing ──────────────────────────────────────────────────

    /// <summary>
    /// Ensures appsettings.local.json exists with the desktop connection string (port 5433).
    /// Regenerates if missing; updates if present but wrong port. Preserves other keys (API key).
    /// Called before WebApplication.CreateBuilder so the file is present for AddJsonFile.
    /// </summary>
    internal static void EnsureDesktopConfig()
    {
        var configDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "AIchivist", "config");
        var configPath = Path.Combine(configDir, "appsettings.local.json");

        try
        {
            if (File.Exists(configPath))
            {
                var content = File.ReadAllText(configPath);
                if (content.Contains($"Port={DesktopPgPort}"))
                    return; // Config looks good
            }

            // File is missing or has wrong port — fix it
            Console.WriteLine($"[PostgreSQL] Config self-heal: ensuring desktop connection string in {configPath}");
            Directory.CreateDirectory(configDir);

            if (File.Exists(configPath))
            {
                // Preserve existing keys (e.g. API key) while fixing connection string
                var node = JsonNode.Parse(File.ReadAllText(configPath))?.AsObject()
                           ?? new JsonObject();
                var connStrings = node["ConnectionStrings"]?.AsObject()
                                  ?? new JsonObject();
                connStrings["Default"] = DesktopConnectionString;
                node["ConnectionStrings"] = connStrings;
                File.WriteAllText(configPath, node.ToJsonString(
                    new JsonSerializerOptions { WriteIndented = true }));
            }
            else
            {
                File.WriteAllText(configPath, """
                    {
                      "ConnectionStrings": {
                        "Default": "Host=localhost;Port=5433;Database=archive_search;Username=archive;Password=archive"
                      }
                    }
                    """);
            }

            Console.WriteLine("[PostgreSQL] Config self-heal complete.");
        }
        catch (Exception ex)
        {
            // Non-fatal: the in-memory connection string override will handle this
            Console.Error.WriteLine($"[PostgreSQL] Config self-heal failed: {ex.Message}");
        }
    }

    // ── PostgreSQL lifecycle ─────────────────────────────────────────────────

    /// <summary>
    /// Starts the bundled PostgreSQL instance with retry logic (up to 3 attempts).
    /// Validates connectivity with pg_isready after each attempt.
    /// Calls FatalDesktopError if all attempts fail.
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
            FatalDesktopError(
                "PostgreSQL data directory not found.\n\n" +
                $"Expected: {dataDir}\n\n" +
                "This usually means the installer did not complete successfully.");
            return;
        }

        // Check if already running and connectable
        if (await IsPgRunning(pgCtl, dataDir))
        {
            Console.WriteLine("[PostgreSQL] Already running.");
            if (await WaitForPgReady(pgCtl, timeoutSeconds: 5))
            {
                Console.WriteLine("[PostgreSQL] Verified accepting connections.");
                return;
            }
            // Running but not accepting connections — stop and restart
            Console.WriteLine("[PostgreSQL] Running but not accepting connections. Restarting...");
            StopPostgreSql();
        }

        // Attempt startup with retries
        for (int attempt = 1; attempt <= PgStartMaxAttempts; attempt++)
        {
            Console.WriteLine($"[PostgreSQL] Starting... (attempt {attempt}/{PgStartMaxAttempts})");

            try
            {
                using var startProcess = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = pgCtl,
                        Arguments = $"start -D \"{dataDir}\" -w -o \"-p {DesktopPgPort}\"",
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        UseShellExecute = false,
                        CreateNoWindow = true
                    }
                };

                startProcess.Start();

                var stdoutTask = startProcess.StandardOutput.ReadToEndAsync();
                var stderrTask = startProcess.StandardError.ReadToEndAsync();
                await startProcess.WaitForExitAsync();
                var stdout = await stdoutTask;
                var stderr = await stderrTask;

                if (startProcess.ExitCode == 0)
                {
                    // pg_ctl reported success — validate with pg_isready
                    if (await WaitForPgReady(pgCtl, timeoutSeconds: 10))
                    {
                        Console.WriteLine($"[PostgreSQL] Started successfully on port {DesktopPgPort}.");
                        return;
                    }
                    Console.Error.WriteLine("[PostgreSQL] pg_ctl succeeded but not accepting connections.");
                }
                else
                {
                    Console.Error.WriteLine($"[PostgreSQL] pg_ctl start failed (exit code {startProcess.ExitCode}).");
                    if (!string.IsNullOrWhiteSpace(stdout))
                        Console.Error.WriteLine($"[PostgreSQL] stdout: {stdout.Trim()}");
                    if (!string.IsNullOrWhiteSpace(stderr))
                        Console.Error.WriteLine($"[PostgreSQL] stderr: {stderr.Trim()}");
                }
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"[PostgreSQL] Startup attempt {attempt} error: {ex.Message}");
            }

            // Wait before retrying (unless last attempt)
            if (attempt < PgStartMaxAttempts)
            {
                var delayMs = PgRetryDelaysMs[attempt - 1];
                Console.WriteLine($"[PostgreSQL] Waiting {delayMs / 1000}s before retry...");
                await Task.Delay(delayMs);
            }
        }

        // All attempts exhausted
        FatalDesktopError(
            $"PostgreSQL failed to start after {PgStartMaxAttempts} attempts.\n\n" +
            "Possible causes:\n" +
            "  \u2022 Another program is using port 5433\n" +
            "  \u2022 Corrupted database files\n" +
            "  \u2022 Antivirus software blocking PostgreSQL");
    }

    /// <summary>Checks if pg_ctl reports PostgreSQL as running.</summary>
    private static async Task<bool> IsPgRunning(string pgCtl, string dataDir)
    {
        try
        {
            using var process = new Process
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
            process.Start();
            await process.WaitForExitAsync();
            return process.ExitCode == 0;
        }
        catch { return false; }
    }

    /// <summary>
    /// Polls pg_isready to confirm PostgreSQL is accepting connections.
    /// Falls back to TCP connect if pg_isready.exe is not available.
    /// </summary>
    private static async Task<bool> WaitForPgReady(string pgCtl, int timeoutSeconds)
    {
        var pgIsReady = Path.Combine(Path.GetDirectoryName(pgCtl)!, "pg_isready.exe");
        if (!File.Exists(pgIsReady))
            return await WaitForTcpPort(DesktopPgPort, timeoutSeconds);

        for (int i = 0; i < timeoutSeconds; i++)
        {
            try
            {
                using var process = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = pgIsReady,
                        Arguments = $"-p {DesktopPgPort} -t 1",
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        UseShellExecute = false,
                        CreateNoWindow = true
                    }
                };
                process.Start();
                await process.WaitForExitAsync();
                if (process.ExitCode == 0) return true;
            }
            catch { /* retry */ }
            await Task.Delay(1000);
        }
        return false;
    }

    /// <summary>Fallback connectivity check via TCP socket.</summary>
    private static async Task<bool> WaitForTcpPort(int port, int timeoutSeconds)
    {
        for (int i = 0; i < timeoutSeconds; i++)
        {
            try
            {
                using var client = new TcpClient();
                await client.ConnectAsync("localhost", port);
                return true;
            }
            catch { /* retry */ }
            await Task.Delay(1000);
        }
        return false;
    }

    /// <summary>
    /// Stops the bundled PostgreSQL instance using pg_ctl with "fast" shutdown mode.
    /// Waits up to 30 seconds for graceful shutdown. If that fails, forces immediate shutdown.
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
            using var stopProcess = new Process
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

            // Wait up to 30 seconds for graceful shutdown
            if (stopProcess.WaitForExit(30000))
            {
                if (stopProcess.ExitCode == 0)
                    Console.WriteLine("[PostgreSQL] Stopped successfully.");
                else
                    Console.Error.WriteLine($"[PostgreSQL] Stop returned exit code {stopProcess.ExitCode}.");
            }
            else
            {
                // Timeout - force immediate shutdown to prevent orphaned process
                Console.Error.WriteLine("[PostgreSQL] Graceful stop timed out after 30 seconds. Forcing immediate shutdown...");
                stopProcess.Kill();

                using var forceStopProcess = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = pgCtl,
                        Arguments = $"stop -D \"{dataDir}\" -m immediate",
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        UseShellExecute = false,
                        CreateNoWindow = true
                    }
                };

                forceStopProcess.Start();
                if (forceStopProcess.WaitForExit(10000))
                {
                    if (forceStopProcess.ExitCode == 0)
                        Console.WriteLine("[PostgreSQL] Forced stop succeeded.");
                    else
                        Console.Error.WriteLine($"[PostgreSQL] Forced stop returned exit code {forceStopProcess.ExitCode}.");
                }
                else
                {
                    Console.Error.WriteLine("[PostgreSQL] Forced stop also timed out. PostgreSQL may remain running.");
                    forceStopProcess.Kill();
                }
            }
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[PostgreSQL] Error during shutdown: {ex.Message}");
        }
    }
}
