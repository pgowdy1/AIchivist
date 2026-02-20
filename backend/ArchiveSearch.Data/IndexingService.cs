using ArchiveSearch.Core.Parsing;
using ArchiveSearch.Data.Repositories;
using Microsoft.Extensions.Logging;

namespace ArchiveSearch.Data;

public class IndexResult
{
    public int Indexed { get; set; }
    public int Skipped { get; set; }
    public int Errors { get; set; }
    public List<string> ErrorMessages { get; set; } = [];
}

/// <summary>
/// One-time (or on-demand) indexing service.
/// Parses all EAD XML files in a directory, upserts them into SQLite,
/// then rebuilds the FTS5 search index.
/// </summary>
public class IndexingService(
    CollectionRepository repository,
    ILogger<IndexingService> logger)
{
    private const int BatchSize = 50;

    public async Task<IndexResult> IndexDirectoryAsync(
        string collectionsPath,
        bool force = false,
        CancellationToken ct = default)
    {
        if (!Directory.Exists(collectionsPath))
            throw new DirectoryNotFoundException($"Collections directory not found: {collectionsPath}");

        var xmlFiles = Directory.GetFiles(collectionsPath, "*.xml", SearchOption.AllDirectories);
        logger.LogInformation("Found {Count} XML files in {Path}", xmlFiles.Length, collectionsPath);

        var result = new IndexResult();

        foreach (var batch in xmlFiles.Chunk(BatchSize))
        {
            if (ct.IsCancellationRequested) break;

            var documents = new List<Core.Models.CollectionDocument>();

            foreach (var file in batch)
            {
                try
                {
                    var doc = EadParser.Parse(file);
                    if (string.IsNullOrWhiteSpace(doc.CollectionUnitId))
                    {
                        logger.LogWarning("Skipping {File}: missing collection unit ID", Path.GetFileName(file));
                        result.Skipped++;
                        continue;
                    }
                    documents.Add(doc);
                }
                catch (Exception ex)
                {
                    logger.LogWarning("Error parsing {File}: {Message}", Path.GetFileName(file), ex.Message);
                    result.Errors++;
                    result.ErrorMessages.Add($"{Path.GetFileName(file)}: {ex.Message}");
                }
            }

            if (documents.Count > 0)
            {
                try
                {
                    await repository.UpsertBatchAsync(documents);
                    result.Indexed += documents.Count;
                    logger.LogDebug("Indexed batch of {Count} collections", documents.Count);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Failed to upsert batch");
                    result.Errors += documents.Count;
                }
            }
        }

        // Rebuild the FTS5 index from the collections table
        logger.LogInformation("Rebuilding FTS5 search index...");
        await repository.RebuildFtsIndexAsync();

        logger.LogInformation(
            "Indexing complete. Indexed: {Indexed}, Skipped: {Skipped}, Errors: {Errors}",
            result.Indexed, result.Skipped, result.Errors);

        return result;
    }

}
