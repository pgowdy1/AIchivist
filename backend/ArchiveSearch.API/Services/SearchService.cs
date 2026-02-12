using ArchiveSearch.Core.Cache;
using ArchiveSearch.Core.Models;
using ArchiveSearch.Data.Repositories;
using Microsoft.Extensions.Logging;

namespace ArchiveSearch.API.Services;

/// <summary>
/// Orchestrates the 3-pass hybrid search:
/// 0. Claude Haiku expands query → 6-8 alternative search phrases
/// 1. PostgreSQL multi-query FTS → ~30-50 unique candidates
/// 2. Claude Haiku ranks candidates → top 10 with explanations
/// Fallback: if Claude fails at any stage, degrades gracefully.
/// </summary>
public class SearchService(
    ClaudeService claude,
    CollectionRepository repository,
    SearchCache cache,
    ILogger<SearchService> logger)
{
    public async Task<SearchResponse> SearchAsync(string query)
    {
        var contextId = SearchCache.ComputeContextId(query);

        // Check search result cache first
        var cached = cache.GetSearchResult(contextId);
        if (cached is not null)
        {
            logger.LogInformation("Cache hit for query: {Query}", query);
            cached.Cached = true;
            return cached;
        }

        // ── Pass 0: Query Expansion (Claude Haiku) ─────────────────────────
        List<string> searchQueries;
        try
        {
            logger.LogInformation("Pass 0 (Expand): Generating search phrases for: {Query}", query);
            var expansions = await claude.ExpandQueryAsync(query);
            searchQueries = new List<string>(expansions.Count + 1) { query };
            searchQueries.AddRange(expansions);
            logger.LogInformation(
                "Pass 0 (Expand): Generated {Count} phrases: [{Phrases}]",
                expansions.Count, string.Join(", ", expansions));
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Pass 0 (Expand) failed, falling back to original query only");
            searchQueries = [query];
        }

        // ── Pass 1: Multi-Query FTS (PostgreSQL) ───────────────────────────
        logger.LogInformation("Pass 1 (FTS): Running {Count} search queries", searchQueries.Count);
        var candidates = await repository.MultiQuerySearchAsync(searchQueries, perQueryLimit: 15, totalLimit: 50);
        logger.LogInformation("Pass 1 (FTS) returned {Count} unique candidates", candidates.Count);

        if (candidates.Count == 0)
        {
            return new SearchResponse { Query = query, ContextId = contextId, Results = [] };
        }

        // ── Pass 2: Claude Ranking (Claude Haiku) ──────────────────────────
        List<CollectionResult> results;
        try
        {
            logger.LogInformation("Pass 2 (Claude): Ranking {Count} candidates", candidates.Count);
            var ranked = await claude.RankCandidatesAsync(candidates, query);

            var entityMap = candidates.ToDictionary(e => e.CollectionUnitId);
            results = ranked
                .OrderBy(r => r.Rank)
                .Take(10)
                .Select(r =>
                {
                    if (!entityMap.TryGetValue(r.CollectionId, out var entity))
                        return null;

                    return new CollectionResult
                    {
                        Rank = r.Rank,
                        RelevanceScore = r.RelevanceScore,
                        RelevanceExplanation = r.Explanation,
                        CollectionUnitId = entity.CollectionUnitId,
                        Title = entity.Title,
                        Repository = entity.Repository,
                        DateRange = entity.DateRange,
                        Extent = entity.Extent,
                        Abstract = entity.Abstract,
                        ScopeContent = entity.ScopeContent,
                        Subjects = [.. entity.Subjects],
                        Persnames = [.. entity.Persnames],
                        Geognames = [.. entity.Geognames],
                        Genres = [.. entity.Genres],
                        SeriesTitles = [.. entity.SeriesTitles]
                    };
                })
                .Where(r => r is not null)
                .Select(r => r!)
                .ToList();

            logger.LogInformation("Pass 2 returned {Count} ranked results", results.Count);
        }
        catch (Exception ex)
        {
            // Fallback: Claude unavailable (rate limit, network error, etc.)
            logger.LogWarning(ex, "Pass 2 (Claude) failed, falling back to FTS-only results");

            results = candidates
                .Take(10)
                .Select((entity, index) => new CollectionResult
                {
                    Rank = index + 1,
                    RelevanceScore = 10 - index,
                    RelevanceExplanation = "Ranked by keyword relevance (AI ranking unavailable).",
                    CollectionUnitId = entity.CollectionUnitId,
                    Title = entity.Title,
                    Repository = entity.Repository,
                    DateRange = entity.DateRange,
                    Extent = entity.Extent,
                    Abstract = entity.Abstract,
                    ScopeContent = entity.ScopeContent,
                    Subjects = [.. entity.Subjects],
                    Persnames = [.. entity.Persnames],
                    Geognames = [.. entity.Geognames],
                    Genres = [.. entity.Genres],
                    SeriesTitles = [.. entity.SeriesTitles]
                })
                .ToList();
        }

        var response = new SearchResponse
        {
            Query = query,
            ContextId = contextId,
            Results = results,
            Cached = false
        };

        cache.SetSearchResult(contextId, response);
        return response;
    }
}
