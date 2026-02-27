using ArchiveSearch.API.Models;
using ArchiveSearch.Core.Cache;
using ArchiveSearch.Core.Models;
using ArchiveSearch.Data.Entities;
using ArchiveSearch.Data.Repositories;
using Microsoft.Extensions.Logging;

namespace ArchiveSearch.API.Services;

/// <summary>
/// Orchestrates the 3-pass hybrid search:
/// 0. Claude Haiku expands query → 6-8 alternative search phrases
/// 1. SQLite FTS5 multi-query search → ~30-50 unique candidates
/// 2. Claude Haiku ranks candidates → top 10 with explanations
/// Fallback: if Claude fails at any stage, degrades gracefully.
/// </summary>
public class SearchService(
    ClaudeService claude,
    CollectionRepository repository,
    SearchCache cache,
    ILogger<SearchService> logger)
{
    public virtual async Task<SearchResponse> SearchAsync(string query, IProgress<SearchProgress>? progress = null)
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
        int? dateStart = null;
        int? dateEnd = null;
        try
        {
            progress?.Report(new SearchProgress("expanding_query", "active", "Generating search phrases..."));
            logger.LogInformation("Pass 0 (Expand): Generating search phrases for: {Query}", query);
            var expansion = await claude.ExpandQueryAsync(query);
            searchQueries = new List<string>(expansion.Phrases.Count + 1) { query };
            searchQueries.AddRange(expansion.Phrases);
            dateStart = expansion.DateStart;
            dateEnd = expansion.DateEnd;
            logger.LogInformation(
                "Pass 0 (Expand): Generated {Count} phrases: [{Phrases}]",
                expansion.Phrases.Count, string.Join(", ", expansion.Phrases));
            if (dateStart.HasValue && dateEnd.HasValue)
                logger.LogInformation(
                    "Pass 0 (Expand): Detected temporal range {Start}-{End}",
                    dateStart.Value, dateEnd.Value);
            progress?.Report(new SearchProgress("expanding_query", "completed", $"Generated {expansion.Phrases.Count} search phrases"));
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Pass 0 (Expand) failed, falling back to original query only");
            progress?.Report(new SearchProgress("expanding_query", "failed", "Using original query only"));
            searchQueries = [query];
        }

        // ── Pass 1: Multi-Query FTS (SQLite FTS5) ──────────────────────────
        progress?.Report(new SearchProgress("searching_database", "active", "Searching archival collections..."));
        logger.LogInformation("Pass 1 (FTS): Running {Count} search queries", searchQueries.Count);
        var candidates = await repository.MultiQuerySearchAsync(
            searchQueries, perQueryLimit: 15, totalLimit: 50,
            dateStart: dateStart, dateEnd: dateEnd);
        logger.LogInformation("Pass 1 (FTS) returned {Count} unique candidates", candidates.Count);
        progress?.Report(new SearchProgress("searching_database", "completed", $"Found {candidates.Count} matching collections"));

        if (candidates.Count == 0)
        {
            return new SearchResponse { Query = query, ContextId = contextId, Results = [], AdditionalResults = [] };
        }

        // ── Pass 2: Claude Ranking (Claude Haiku) ──────────────────────────
        List<CollectionResult> results;
        List<CollectionResult> additionalResults;
        try
        {
            progress?.Report(new SearchProgress("ranking_results", "active", "AI is ranking results..."));
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

                    return MapToResult(entity, r.Rank, r.RelevanceScore, r.Explanation, isAiRanked: true);
                })
                .Where(r => r is not null)
                .Select(r => r!)
                .ToList();

            var rankedIds = new HashSet<string>(results.Select(r => r.CollectionUnitId));
            additionalResults = candidates
                .Where(c => !rankedIds.Contains(c.CollectionUnitId))
                .Select((entity, i) => MapToResult(
                    entity, results.Count + i + 1, 0,
                    "Found by keyword search. Not individually ranked by AI.", isAiRanked: false))
                .ToList();

            logger.LogInformation(
                "Pass 2 returned {RankedCount} ranked + {AdditionalCount} additional results",
                results.Count, additionalResults.Count);
            progress?.Report(new SearchProgress("ranking_results", "completed", $"Ranked top {results.Count} results"));
        }
        catch (Exception ex)
        {
            // Fallback: Claude unavailable (rate limit, network error, etc.)
            logger.LogWarning(ex, "Pass 2 (Claude) failed, falling back to FTS-only results");
            progress?.Report(new SearchProgress("ranking_results", "failed", "Using keyword relevance order"));

            results = candidates
                .Take(10)
                .Select((entity, index) => MapToResult(
                    entity, index + 1, 10 - index,
                    "Ranked by keyword relevance (AI ranking unavailable).", isAiRanked: false))
                .ToList();

            additionalResults = candidates
                .Skip(10)
                .Select((entity, index) => MapToResult(
                    entity, 10 + index + 1, 0,
                    "Found by keyword search (AI ranking unavailable).", isAiRanked: false))
                .ToList();
        }

        var response = new SearchResponse
        {
            Query = query,
            ContextId = contextId,
            Results = results,
            AdditionalResults = additionalResults,
            Cached = false
        };

        cache.SetSearchResult(contextId, response);
        return response;
    }

    private static CollectionResult MapToResult(
        CollectionEntity entity, int rank, int relevanceScore, string explanation, bool isAiRanked)
    {
        return new CollectionResult
        {
            Rank = rank,
            RelevanceScore = relevanceScore,
            RelevanceExplanation = explanation,
            IsAiRanked = isAiRanked,
            CollectionUnitId = entity.CollectionUnitId,
            Title = entity.Title,
            Repository = entity.Repository,
            DateRange = entity.DateRange,
            Extent = entity.Extent,
            Abstract = entity.Abstract,
            ScopeContent = entity.ScopeContent,
            BiogHist = entity.BiogHist,
            Subjects = [.. entity.Subjects],
            Persnames = [.. entity.Persnames],
            Geognames = [.. entity.Geognames],
            Genres = [.. entity.Genres],
            SeriesTitles = [.. entity.SeriesTitles]
        };
    }
}
