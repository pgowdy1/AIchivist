using ArchiveSearch.Data;
using ArchiveSearch.Data.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

// ── Argument parsing ───────────────────────────────────────────────────────

string? inputDir = null;
string? outputPath = null;
bool force = false;

for (int i = 0; i < args.Length; i++)
{
    switch (args[i])
    {
        case "--input" when i + 1 < args.Length:
            inputDir = args[++i];
            break;
        case "--output" when i + 1 < args.Length:
            outputPath = args[++i];
            break;
        case "--force":
            force = true;
            break;
        default:
            Console.Error.WriteLine($"Unknown argument: {args[i]}");
            PrintUsage();
            return 1;
    }
}

if (string.IsNullOrWhiteSpace(inputDir) || string.IsNullOrWhiteSpace(outputPath))
{
    Console.Error.WriteLine("Error: --input and --output are required.");
    PrintUsage();
    return 1;
}

if (!Directory.Exists(inputDir))
{
    Console.Error.WriteLine($"Error: Input directory does not exist: {inputDir}");
    return 1;
}

// ── Ensure output directory exists ───────────────────────────────────────

var outputDir = Path.GetDirectoryName(Path.GetFullPath(outputPath));
if (!string.IsNullOrEmpty(outputDir))
    Directory.CreateDirectory(outputDir);

var connectionString = $"Data Source={Path.GetFullPath(outputPath)}";

// ── DI container ─────────────────────────────────────────────────────────

var services = new ServiceCollection();

services.AddLogging(builder =>
    builder.AddSimpleConsole(options =>
    {
        options.TimestampFormat = "[HH:mm:ss] ";
        options.SingleLine = true;
    })
    .SetMinimumLevel(LogLevel.Information));

services.AddDbContext<ArchiveContext>(options =>
    options.UseSqlite(connectionString));

services.AddScoped<CollectionRepository>();
services.AddScoped<IndexingService>();

await using var provider = services.BuildServiceProvider();

// ── Apply EF migrations ───────────────────────────────────────────────────

using (var scope = provider.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<ArchiveContext>();
    await db.Database.MigrateAsync();
    await db.Database.ExecuteSqlRawAsync("PRAGMA journal_mode=WAL;");
}

// ── Run indexing ──────────────────────────────────────────────────────────

using (var scope = provider.CreateScope())
{
    var indexer = scope.ServiceProvider.GetRequiredService<IndexingService>();
    var result = await indexer.IndexDirectoryAsync(inputDir, force);

    // Checkpoint WAL so the database is a single file for the installer
    var db = scope.ServiceProvider.GetRequiredService<ArchiveContext>();
    await db.Database.ExecuteSqlRawAsync("PRAGMA wal_checkpoint(TRUNCATE);");
    await db.Database.ExecuteSqlRawAsync("PRAGMA journal_mode=DELETE;");

    Console.WriteLine();
    Console.WriteLine($"Indexing complete.");
    Console.WriteLine($"  Indexed: {result.Indexed}");
    Console.WriteLine($"  Skipped: {result.Skipped}");
    Console.WriteLine($"  Errors:  {result.Errors}");

    if (result.ErrorMessages.Count > 0)
    {
        Console.WriteLine("  Error samples:");
        foreach (var msg in result.ErrorMessages.Take(10))
            Console.WriteLine($"    - {msg}");
    }

    return result.Errors > result.Indexed ? 1 : 0;
}

static void PrintUsage()
{
    Console.WriteLine("Usage: AIchivist.Indexer --input <collections-dir> --output <archive.db> [--force]");
    Console.WriteLine();
    Console.WriteLine("  --input   Path to directory containing EAD XML files");
    Console.WriteLine("  --output  Path to output SQLite database file");
    Console.WriteLine("  --force   Re-index all files regardless of existing data");
}
