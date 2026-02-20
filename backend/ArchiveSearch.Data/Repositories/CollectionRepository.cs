using System.Globalization;
using ArchiveSearch.Core.Models;
using ArchiveSearch.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace ArchiveSearch.Data.Repositories;

public class CollectionRepository(ArchiveContext context)
{
    /// <summary>
    /// Full-text search using SQLite FTS5 with weighted bm25 ranking.
    /// Returns up to <paramref name="limit"/> collections ordered by relevance.
    /// </summary>
    public async Task<List<CollectionEntity>> FullTextSearchAsync(string query, int limit = 25)
    {
        var ftsQuery = SanitizeFts5Query(query);
        return await context.Collections
            .FromSqlInterpolated($"""
                SELECT c.* FROM collections c
                INNER JOIN collections_fts f ON f.collection_unitid = c.collection_unitid
                WHERE collections_fts MATCH {ftsQuery}
                ORDER BY bm25(collections_fts, 0.0, 10.0, 5.0, 1.0)
                LIMIT {limit}
                """)
            .AsNoTracking()
            .ToListAsync();
    }

    /// <summary>
    /// Runs FTS5 search for each query phrase, merges results, and deduplicates.
    /// Each sub-query returns up to <paramref name="perQueryLimit"/> results.
    /// Total unique results capped at <paramref name="totalLimit"/>.
    /// When <paramref name="dateStart"/> and <paramref name="dateEnd"/> are provided,
    /// collections whose date range overlaps the temporal window receive a rank boost.
    /// </summary>
    public async Task<List<CollectionEntity>> MultiQuerySearchAsync(
        IEnumerable<string> queries, int perQueryLimit = 15, int totalLimit = 50,
        int? dateStart = null, int? dateEnd = null)
    {
        var seen = new HashSet<string>();
        var results = new List<CollectionEntity>();
        var hasDateInt = (dateStart.HasValue && dateEnd.HasValue) ? 1 : 0;
        // Provide concrete values for SQL parameters even when unused; the CASE guard prevents them from affecting results.
        var sqlDateStart = dateStart ?? 0;
        var sqlDateEnd = dateEnd ?? 0;

        foreach (var query in queries)
        {
            if (string.IsNullOrWhiteSpace(query)) continue;

            try
            {
                var ftsQuery = SanitizeFts5Query(query);
                var hits = await context.Collections
                    .FromSqlInterpolated($"""
                        SELECT c.* FROM collections c
                        INNER JOIN collections_fts f ON f.collection_unitid = c.collection_unitid
                        WHERE collections_fts MATCH {ftsQuery}
                        ORDER BY
                            bm25(collections_fts, 0.0, 10.0, 5.0, 1.0)
                            + CASE
                                WHEN {hasDateInt} = 1
                                     AND c.date_start IS NOT NULL AND c.date_end IS NOT NULL
                                     AND c.date_start <= {sqlDateEnd} AND c.date_end >= {sqlDateStart}
                                THEN -0.5
                                ELSE 0.0
                              END
                        LIMIT {perQueryLimit}
                        """)
                    .AsNoTracking()
                    .ToListAsync();

                foreach (var hit in hits)
                {
                    if (seen.Add(hit.CollectionUnitId))
                        results.Add(hit);
                }
            }
            catch (Exception ex) when (ex is Microsoft.Data.Sqlite.SqliteException or InvalidOperationException)
            {
                // Infrastructure error (database unreachable, connection issue, etc.) — rethrow
                throw;
            }
            catch
            {
                // Individual sub-query failure (e.g., malformed FTS5 query from Claude phrase) — skip silently
            }
        }

        return results.Count > totalLimit ? results.Take(totalLimit).ToList() : results;
    }

    /// <summary>
    /// Rebuilds the FTS5 virtual table from the collections table.
    /// Called after bulk insert/upsert during indexing.
    /// Wrapped in a transaction so a crash never leaves the FTS table empty.
    /// </summary>
    public async Task RebuildFtsIndexAsync()
    {
        await using var transaction = await context.Database.BeginTransactionAsync();
        await context.Database.ExecuteSqlRawAsync("DELETE FROM collections_fts");
        await context.Database.ExecuteSqlRawAsync("""
            INSERT INTO collections_fts(collection_unitid, title, abstract_subjects, scope_biog_detail)
            SELECT
                collection_unitid,
                coalesce(title, ''),
                coalesce(abstract, '') || ' ' || coalesce(subjects, '[]') || ' ' || coalesce(persnames, '[]') || ' ' || coalesce(geognames, '[]'),
                coalesce(scope_content, '') || ' ' || coalesce(biog_hist, '') || ' ' || coalesce(corpnames, '[]') || ' ' || coalesce(genres, '[]') || ' ' || coalesce(series_titles, '[]')
            FROM collections
            """);
        await transaction.CommitAsync();
    }

    /// <summary>Fetch full records for a set of collection unit IDs.</summary>
    public async Task<List<CollectionEntity>> GetByUnitIdsAsync(IEnumerable<string> unitIds)
    {
        var ids = unitIds.ToList();
        if (ids.Count == 0) return [];
        return await context.Collections
            .Where(c => ids.Contains(c.CollectionUnitId))
            .AsNoTracking()
            .ToListAsync();
    }

    /// <summary>Upsert a batch of parsed documents. Insert new, update existing.</summary>
    public async Task UpsertBatchAsync(IEnumerable<CollectionDocument> documents)
    {
        var docList = documents.ToList();
        var unitIds = docList.Select(d => d.CollectionUnitId).ToList();
        var existingMap = await context.Collections
            .Where(c => unitIds.Contains(c.CollectionUnitId))
            .ToDictionaryAsync(c => c.CollectionUnitId);

        foreach (var doc in docList)
        {
            if (existingMap.TryGetValue(doc.CollectionUnitId, out var existing))
                UpdateEntity(existing, doc);
            else
                context.Collections.Add(MapToEntity(doc));
        }

        await context.SaveChangesAsync();
    }

    /// <summary>Total number of indexed collections.</summary>
    public Task<int> CountAsync() => context.Collections.CountAsync();

    /// <summary>
    /// Finds collections that share entities (persons, organizations, subjects, places)
    /// with the given collection. Scores by weighted overlap and returns lightweight DTOs.
    /// </summary>
    public async Task<List<RelatedCollection>> FindRelatedAsync(string unitId, int limit = 8)
    {
        // 1. Load the source collection
        var source = await context.Collections
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.CollectionUnitId == unitId);

        if (source is null) return [];

        // 2. Build search terms from the source's key entities for candidate retrieval
        var sourceTerms = source.Subjects.Take(5)
            .Concat(source.Persnames.Take(3))
            .Concat(source.Corpnames.Take(3))
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .ToList();

        if (sourceTerms.Count == 0) return [];

        // Build an FTS5-compatible query string from the terms (OR-joined quoted phrases)
        var ftsQuery = string.Join(" OR ",
            sourceTerms.Select(t => $"\"{t.Replace("\"", "", StringComparison.Ordinal)}\""));

        // 3. Get up to 40 candidates via FTS5 (excluding the source itself)
        List<CollectionEntity> candidates;
        try
        {
            candidates = await context.Collections
                .FromSqlInterpolated($"""
                    SELECT c.* FROM collections c
                    INNER JOIN collections_fts f ON f.collection_unitid = c.collection_unitid
                    WHERE collections_fts MATCH {ftsQuery}
                      AND c.collection_unitid != {unitId}
                    ORDER BY bm25(collections_fts, 0.0, 10.0, 5.0, 1.0)
                    LIMIT 40
                    """)
                .AsNoTracking()
                .ToListAsync();
        }
        catch (Exception ex) when (ex is Microsoft.Data.Sqlite.SqliteException or InvalidOperationException)
        {
            throw;
        }
        catch
        {
            // FTS5 query may fail if terms produce invalid syntax — return empty gracefully
            return [];
        }

        // 4. Score each candidate by entity overlap (application-side, arrays are small)
        var scored = candidates.Select(c =>
        {
            var sharedSubjects = source.Subjects.Intersect(c.Subjects, StringComparer.OrdinalIgnoreCase).ToList();
            var sharedPersons = source.Persnames.Intersect(c.Persnames, StringComparer.OrdinalIgnoreCase).ToList();
            var sharedOrgs = source.Corpnames.Intersect(c.Corpnames, StringComparer.OrdinalIgnoreCase).ToList();
            var sharedPlaces = source.Geognames.Intersect(c.Geognames, StringComparer.OrdinalIgnoreCase).ToList();

            double score = (sharedPersons.Count * 4.0) + (sharedOrgs.Count * 4.0)
                         + (sharedSubjects.Count * 3.0) + (sharedPlaces.Count * 2.0);

            // Date overlap bonus
            if (source.DateStart.HasValue && source.DateEnd.HasValue
                && c.DateStart.HasValue && c.DateEnd.HasValue
                && c.DateStart <= source.DateEnd && c.DateEnd >= source.DateStart)
            {
                score += 2.0;
            }

            return (Entity: c, Score: score, SharedSubjects: sharedSubjects,
                    SharedPersons: sharedPersons, SharedOrgs: sharedOrgs, SharedPlaces: sharedPlaces);
        })
        .Where(x => x.Score > 0)
        .OrderByDescending(x => x.Score)
        .Take(limit)
        .ToList();

        // 5. Map to lightweight DTOs
        return scored.Select(x =>
        {
            var parts = new List<string>();
            if (x.SharedSubjects.Count > 0)
                parts.Add(string.Create(CultureInfo.InvariantCulture,
                    $"{x.SharedSubjects.Count} subject{(x.SharedSubjects.Count > 1 ? "s" : "")}"));
            if (x.SharedPersons.Count > 0)
                parts.Add(string.Create(CultureInfo.InvariantCulture,
                    $"{x.SharedPersons.Count} person{(x.SharedPersons.Count > 1 ? "s" : "")}"));
            if (x.SharedOrgs.Count > 0)
                parts.Add(string.Create(CultureInfo.InvariantCulture,
                    $"{x.SharedOrgs.Count} org{(x.SharedOrgs.Count > 1 ? "s" : "")}"));
            if (x.SharedPlaces.Count > 0)
                parts.Add(string.Create(CultureInfo.InvariantCulture,
                    $"{x.SharedPlaces.Count} place{(x.SharedPlaces.Count > 1 ? "s" : "")}"));

            return new RelatedCollection
            {
                CollectionUnitId = x.Entity.CollectionUnitId,
                Title = x.Entity.Title,
                Repository = x.Entity.Repository,
                DateRange = x.Entity.DateRange,
                Abstract = x.Entity.Abstract,
                OverlapScore = x.Score,
                SharedSubjects = x.SharedSubjects.Take(3).ToList(),
                SharedPersons = x.SharedPersons.Take(3).ToList(),
                SharedOrganizations = x.SharedOrgs.Take(3).ToList(),
                SharedPlaces = x.SharedPlaces.Take(3).ToList(),
                OverlapSummary = parts.Count > 0
                    ? $"Shares: {string.Join(", ", parts)}"
                    : "Related by context"
            };
        }).ToList();
    }

    private static CollectionEntity MapToEntity(CollectionDocument doc) => new()
    {
        CollectionUnitId = doc.CollectionUnitId,
        Title = doc.Title,
        Repository = doc.Repository,
        DateRange = doc.DateRange,
        DateStart = doc.DateStart,
        DateEnd = doc.DateEnd,
        Extent = doc.Extent,
        Abstract = doc.Abstract,
        ScopeContent = doc.ScopeContent,
        BiogHist = doc.BiogHist,
        Subjects = [.. doc.Subjects],
        Persnames = [.. doc.Persnames],
        Geognames = [.. doc.Geognames],
        Genres = [.. doc.Genres],
        Corpnames = [.. doc.Corpnames],
        SeriesTitles = [.. doc.SeriesTitles],
        CompactLine = doc.CompactLine,
        SourceFile = doc.SourceFile
    };

    private static void UpdateEntity(CollectionEntity entity, CollectionDocument doc)
    {
        entity.Title = doc.Title;
        entity.Repository = doc.Repository;
        entity.DateRange = doc.DateRange;
        entity.DateStart = doc.DateStart;
        entity.DateEnd = doc.DateEnd;
        entity.Extent = doc.Extent;
        entity.Abstract = doc.Abstract;
        entity.ScopeContent = doc.ScopeContent;
        entity.BiogHist = doc.BiogHist;
        entity.Subjects = [.. doc.Subjects];
        entity.Persnames = [.. doc.Persnames];
        entity.Geognames = [.. doc.Geognames];
        entity.Genres = [.. doc.Genres];
        entity.Corpnames = [.. doc.Corpnames];
        entity.SeriesTitles = [.. doc.SeriesTitles];
        entity.CompactLine = doc.CompactLine;
        entity.SourceFile = doc.SourceFile;
    }

    private static readonly HashSet<string> Fts5Operators = new(StringComparer.OrdinalIgnoreCase)
        { "AND", "OR", "NOT", "NEAR" };

    /// <summary>
    /// Sanitizes a user query string for safe use in FTS5 MATCH expressions.
    /// Strips special FTS5 syntax characters and quotes each word individually
    /// to suppress operator interpretation (NOT, AND, OR, NEAR, column filters).
    /// </summary>
    private static string SanitizeFts5Query(string query)
    {
        if (string.IsNullOrWhiteSpace(query)) return "\"\"";
        // Remove FTS5 special characters: quotes, wildcards, parens, column filter, initial-token, braces
        var cleaned = query
            .Replace("\"", " ", StringComparison.Ordinal)
            .Replace("*", " ", StringComparison.Ordinal)
            .Replace("(", " ", StringComparison.Ordinal)
            .Replace(")", " ", StringComparison.Ordinal)
            .Replace(":", " ", StringComparison.Ordinal)
            .Replace("^", " ", StringComparison.Ordinal)
            .Replace("{", " ", StringComparison.Ordinal)
            .Replace("}", " ", StringComparison.Ordinal)
            .Trim();
        if (string.IsNullOrWhiteSpace(cleaned)) return "\"\"";
        // Split into words, filter bare FTS5 operators, quote each word to prevent operator interpretation
        var words = cleaned.Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Where(w => !Fts5Operators.Contains(w));
        var quoted = words.Select(w => $"\"{w}\"");
        var result = string.Join(" ", quoted);
        return string.IsNullOrEmpty(result) ? "\"\"" : result;
    }
}
