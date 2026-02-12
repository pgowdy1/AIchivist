using ArchiveSearch.Core.Models;
using Microsoft.Extensions.Caching.Memory;

namespace ArchiveSearch.Core.Cache;

/// <summary>
/// TTL-based cache for search results.
/// </summary>
public class SearchCache(IMemoryCache memoryCache)
{
    private const string ResultsKeyPrefix = "search_result_";
    private static readonly TimeSpan ResultsTtl = TimeSpan.FromHours(1);

    /// <summary>Cache a search result by context ID (SHA256 of query).</summary>
    public void SetSearchResult(string contextId, SearchResponse response)
    {
        memoryCache.Set(ResultsKeyPrefix + contextId, response, ResultsTtl);
    }

    /// <summary>Retrieve a previously cached search result, or null if not found/expired.</summary>
    public SearchResponse? GetSearchResult(string contextId)
    {
        return memoryCache.Get<SearchResponse>(ResultsKeyPrefix + contextId);
    }

    /// <summary>Compute a stable context ID for a query (used as cache key and chat context reference).</summary>
    public static string ComputeContextId(string query)
    {
        var bytes = System.Security.Cryptography.SHA256.HashData(
            System.Text.Encoding.UTF8.GetBytes(query.Trim().ToLowerInvariant()));
        return Convert.ToHexString(bytes)[..16]; // 16 hex chars is plenty for a cache key
    }
}
