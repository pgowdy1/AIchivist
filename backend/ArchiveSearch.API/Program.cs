using Anthropic;
using ArchiveSearch.API.Services;
using ArchiveSearch.Core.Cache;
using ArchiveSearch.Data;
using ArchiveSearch.Data.Repositories;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// ── Configuration ──────────────────────────────────────────────────────────

// API key — User Secrets in Development, environment variable in Production.
// To set locally: dotnet user-secrets set "ANTHROPIC_API_KEY" "sk-ant-..." --project backend/ArchiveSearch.API
var anthropicApiKey = builder.Configuration["ANTHROPIC_API_KEY"];
if (string.IsNullOrWhiteSpace(anthropicApiKey))
    throw new InvalidOperationException(
        "ANTHROPIC_API_KEY is required. In development, run: " +
        "dotnet user-secrets set \"ANTHROPIC_API_KEY\" \"sk-ant-...\" --project backend/ArchiveSearch.API");

var connectionString = Environment.GetEnvironmentVariable("CONNECTION_STRING")
    ?? builder.Configuration.GetConnectionString("Default")
    ?? "Host=localhost;Port=5432;Database=archive_search;Username=archive;Password=archive";

// ── Services ───────────────────────────────────────────────────────────────

builder.Services.AddControllers();
builder.Services.AddMemoryCache();

// PostgreSQL via EF Core
builder.Services.AddDbContext<ArchiveContext>(options =>
    options.UseNpgsql(connectionString));

// Anthropic client — pass key directly (avoids stale env var issues)
builder.Services.AddSingleton(new AnthropicClient { ApiKey = anthropicApiKey });

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
app.UseAuthorization();
app.MapControllers();

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

app.Run();
