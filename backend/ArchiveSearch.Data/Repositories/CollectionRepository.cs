using ArchiveSearch.Core.Models;
using ArchiveSearch.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace ArchiveSearch.Data.Repositories;

public class CollectionRepository(ArchiveContext context)
{
    /// <summary>
    /// Full-text search using PostgreSQL weighted tsvector + GIN index.
    /// Returns up to <paramref name="limit"/> collections ordered by cover density rank.
    /// </summary>
    public async Task<List<CollectionEntity>> FullTextSearchAsync(string query, int limit = 25)
    {
        return await context.Collections
            .FromSqlInterpolated($"""
                SELECT * FROM collections
                WHERE search_vector @@ websearch_to_tsquery('english', {query})
                ORDER BY ts_rank_cd(search_vector, websearch_to_tsquery('english', {query})) DESC
                LIMIT {limit}
                """)
            .AsNoTracking()
            .ToListAsync();
    }

    /// <summary>
    /// Runs full-text search for each query phrase, merges results, and deduplicates.
    /// Each sub-query returns up to <paramref name="perQueryLimit"/> results.
    /// Total unique results capped at <paramref name="totalLimit"/>.
    /// </summary>
    public async Task<List<CollectionEntity>> MultiQuerySearchAsync(
        IEnumerable<string> queries, int perQueryLimit = 15, int totalLimit = 50)
    {
        var seen = new HashSet<string>();
        var results = new List<CollectionEntity>();

        foreach (var query in queries)
        {
            if (string.IsNullOrWhiteSpace(query)) continue;

            try
            {
                var hits = await context.Collections
                    .FromSqlInterpolated($"""
                        SELECT * FROM collections
                        WHERE search_vector @@ websearch_to_tsquery('english', {query})
                        ORDER BY ts_rank_cd(search_vector, websearch_to_tsquery('english', {query})) DESC
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
            catch
            {
                // Individual sub-query failure (e.g., malformed phrase) — skip silently
            }
        }

        return results.Count > totalLimit ? results.Take(totalLimit).ToList() : results;
    }

    /// <summary>
    /// Populates the search_vector column for all rows using weighted tsvector.
    /// Called after bulk insert/upsert during indexing.
    /// </summary>
    public async Task UpdateSearchVectorsAsync()
    {
        await context.Database.ExecuteSqlRawAsync("""
            UPDATE collections SET search_vector =
                setweight(to_tsvector('english', coalesce(title, '')), 'A') ||
                setweight(to_tsvector('english',
                    coalesce(abstract, '') || ' ' ||
                    coalesce(array_to_string(subjects, ' '), '') || ' ' ||
                    coalesce(array_to_string(persnames, ' '), '') || ' ' ||
                    coalesce(array_to_string(geognames, ' '), '')
                ), 'B') ||
                setweight(to_tsvector('english',
                    coalesce(scope_content, '') || ' ' ||
                    coalesce(biog_hist, '') || ' ' ||
                    coalesce(array_to_string(corpnames, ' '), '') || ' ' ||
                    coalesce(array_to_string(genres, ' '), '') || ' ' ||
                    coalesce(array_to_string(series_titles, ' '), '')
                ), 'C')
            """);
    }

    /// <summary>Fetch full records for a set of collection unit IDs.</summary>
    public async Task<List<CollectionEntity>> GetByUnitIdsAsync(IEnumerable<string> unitIds)
    {
        var ids = unitIds.ToList();
        return await context.Collections
            .Where(c => ids.Contains(c.CollectionUnitId))
            .AsNoTracking()
            .ToListAsync();
    }

    /// <summary>Upsert a batch of parsed documents. Insert new, update existing.</summary>
    public async Task UpsertBatchAsync(IEnumerable<CollectionDocument> documents)
    {
        foreach (var doc in documents)
        {
            var existing = await context.Collections
                .FirstOrDefaultAsync(c => c.CollectionUnitId == doc.CollectionUnitId);

            if (existing is null)
            {
                context.Collections.Add(MapToEntity(doc));
            }
            else
            {
                UpdateEntity(existing, doc);
            }
        }

        await context.SaveChangesAsync();
    }

    /// <summary>Total number of indexed collections.</summary>
    public Task<int> CountAsync() => context.Collections.CountAsync();

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
}
