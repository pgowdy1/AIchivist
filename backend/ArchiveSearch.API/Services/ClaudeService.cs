using System.Text.Json;
using Anthropic;
using Anthropic.Models.Messages;
using ArchiveSearch.Core.Models;
using ArchiveSearch.Data.Entities;
using Microsoft.Extensions.Logging;

namespace ArchiveSearch.API.Services;

/// <summary>
/// Wraps the Anthropic SDK for archival search and chat functionality.
///
/// Expand (Haiku): Generates 6-8 alternative search phrases for improved FTS recall.
/// Rank (Haiku):   Reads full EAD details for FTS candidates + user query.
///                 Returns ranked top-10 results with relevance scores and explanations.
/// Chat (Sonnet):  Multi-turn conversation grounded in the top-10 search results.
/// </summary>
public class ClaudeService(AnthropicClient client, ILogger<ClaudeService> logger)
{
    private const string HaikuModel = "claude-haiku-4-5-20251001";
    private const string SonnetModel = "claude-sonnet-4-5-20250929";

    // ────────────────────────────────────────────────────────────────────────
    //  EXPAND: Generate alternative search phrases from user query
    // ────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Uses Claude Haiku to expand a user query into 6-8 alternative search phrases
    /// for improved full-text search recall, plus an optional date range if the query
    /// contains a temporal signal. Cost: ~$0.001 per call.
    /// </summary>
    public async Task<QueryExpansionResult> ExpandQueryAsync(string query)
    {
        var systemPrompt = """
            You are an expert research librarian at Washington State University's Manuscripts,
            Archives, and Special Collections (MASC). A researcher wants to search the archive.

            Your task: Generate 6-8 short search phrases that would help find ALL relevant
            archival collections. Think broadly:
            - Synonyms and alternate terms (e.g., "Native American" vs "Indian" vs "indigenous")
            - Related historical events, legislation, court decisions
            - Key people, organizations, and places associated with the topic
            - Broader and narrower subject terms
            - Related Library of Congress subject headings

            Each phrase should be 1-4 words. These will be used as PostgreSQL full-text search
            queries, so use natural search terms, not full sentences.

            Return ONLY a JSON object with these fields:
            - "phrases": array of 6-8 alternative search phrases (as described above)
            - "dateRange": optional object with "start" and "end" integer years, e.g. {"start": 1929, "end": 1939}
              Only include dateRange if the query has a clear temporal signal (decade reference, era name, or explicit years).
              Named eras: "Great Depression" = 1929-1939, "WWI" = 1914-1918, "WWII" = 1939-1945,
              "Cold War" = 1947-1991, "Progressive Era" = 1896-1920, "Prohibition" = 1920-1933,
              "Vietnam War" = 1955-1975, "Civil Rights Movement" = 1954-1968, "Dust Bowl" = 1930-1940.
              Decade references: "1920s" = 1920-1929, "turn of the century" = 1890-1910.
              If no temporal signal is present, omit the dateRange field entirely.

            No markdown fences. No explanation. Just the JSON.
            """;

        var parameters = new MessageCreateParams
        {
            Model = HaikuModel,
            MaxTokens = 256,
            System = systemPrompt,
            Messages =
            [
                new MessageParam
                {
                    Role = Role.User,
                    Content = $"Research query: {query}"
                }
            ]
        };

        var response = await client.Messages.Create(parameters);
        var text = ExtractText(response);
        return ParseExpansionResult(text);
    }

    // ────────────────────────────────────────────────────────────────────────
    //  RANK: Detailed analysis of FTS candidates → top 10
    // ────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Uses Claude Haiku to rank candidate collections and return the top 10
    /// with relevance scores and explanations.
    /// </summary>
    public async Task<List<RankedResult>> RankCandidatesAsync(
        IEnumerable<CollectionEntity> candidates, string query)
    {
        var candidateList = candidates.ToList();

        var systemPrompt = $"""
            You are an expert archivist at Washington State University. A researcher has asked a question
            and you have been given the full details of {candidateList.Count} archival collections that may be relevant.

            Your task: Select the 10 most relevant collections and rank them.

            When evaluating relevance, pay close attention to:
            - Biographical connections and relationships: who the person or organization was, their collaborators,
              associates, and the networks they operated within.
            - Organizational affiliations and corporate history: what institutions, companies, or agencies are
              linked to the collection and how they relate to the research query.
            - Historical narrative context from biographical notes: life events, career milestones, and historical
              circumstances described in the "History" field that connect to the query topic.
            These contextual details are often more revealing of relevance than subject headings alone.

            Return ONLY a valid JSON array with up to 10 objects (include fewer if fewer are relevant).
            Each object must have:
            - "collectionId": the unit ID string (e.g. "CAGE 47")
            - "rank": integer 1-10 (1 = most relevant)
            - "relevanceScore": integer 1-10 (10 = extremely relevant)
            - "explanation": 2-3 sentences explaining exactly why this collection is relevant

            No markdown fences. No wrapper text. Just the JSON array.
            """;

        var collectionsText = BuildDetailedContext(candidateList);

        var parameters = new MessageCreateParams
        {
            Model = HaikuModel,
            MaxTokens = 2560,
            System = systemPrompt,
            Messages =
            [
                new MessageParam
                {
                    Role = Role.User,
                    Content = $"Research question: {query}\n\nCANDIDATE COLLECTIONS:\n{collectionsText}"
                }
            ]
        };

        var response = await client.Messages.Create(parameters);
        var text = ExtractText(response);
        return ParseRankedResults(text);
    }

    // ────────────────────────────────────────────────────────────────────────
    //  CHAT: Multi-turn conversation grounded in top-10 results
    // ────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Conducts a multi-turn archival research conversation using Claude Sonnet,
    /// grounded in the 10 search results from the original query.
    /// </summary>
    public async Task<string> ChatAsync(
        IEnumerable<ChatMessage> messages,
        IEnumerable<CollectionResult> searchResults)
    {
        var resultsContext = BuildResultsContext(searchResults);

        var systemPrompt = $"""
            You are an expert archivist at Washington State University Libraries, Manuscripts, Archives
            and Special Collections (MASC). You are helping a researcher who has already searched the
            archive and received the following 10 relevant collections.

            Your role is to answer follow-up questions about these collections, help the researcher
            understand what materials they would find there, suggest which collections are most relevant
            to specific aspects of their research, and advise on how to request access.

            Be specific and factual. Reference the actual collection titles and content when answering.
            Do not make up information not present in the collection descriptions below.

            RETRIEVED COLLECTIONS FOR THIS RESEARCH SESSION:
            {resultsContext}
            """;

        var anthropicMessages = messages
            .Select(m => new MessageParam
            {
                Role = m.Role == "user" ? Role.User : Role.Assistant,
                Content = m.Content
            })
            .ToList();

        var parameters = new MessageCreateParams
        {
            Model = SonnetModel,
            MaxTokens = 1024,
            System = systemPrompt,
            Messages = anthropicMessages
        };

        var response = await client.Messages.Create(parameters);
        return ExtractText(response);
    }

    // ────────────────────────────────────────────────────────────────────────
    //  Helpers
    // ────────────────────────────────────────────────────────────────────────

    private static string ExtractText(Message response)
    {
        var sb = new System.Text.StringBuilder();
        foreach (var block in response.Content)
        {
            if (block.TryPickText(out var textBlock))
                sb.Append(textBlock.Text);
        }
        return sb.ToString().Trim();
    }

    private static List<RankedResult> ParseRankedResults(string json)
    {
        try
        {
            var cleaned = CleanJson(json);
            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            return JsonSerializer.Deserialize<List<RankedResult>>(cleaned, options) ?? [];
        }
        catch
        {
            return [];
        }
    }

    private static QueryExpansionResult ParseExpansionResult(string json)
    {
        try
        {
            var cleaned = CleanJson(json);
            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            var raw = JsonSerializer.Deserialize<ExpandedQueryResultRaw>(cleaned, options);
            var result = new QueryExpansionResult
            {
                Phrases = raw?.Phrases ?? []
            };

            if (raw?.DateRange is not null)
            {
                result.DateStart = raw.DateRange.Start;
                result.DateEnd = raw.DateRange.End;
            }

            return result;
        }
        catch
        {
            return new QueryExpansionResult();
        }
    }

    private static string CleanJson(string text)
    {
        var cleaned = text.Trim();
        if (cleaned.StartsWith("```"))
        {
            var firstNewline = cleaned.IndexOf('\n');
            cleaned = firstNewline >= 0 ? cleaned[(firstNewline + 1)..] : cleaned[3..];
        }
        if (cleaned.EndsWith("```"))
            cleaned = cleaned[..cleaned.LastIndexOf("```")];
        return cleaned.Trim();
    }

    private static string BuildDetailedContext(IEnumerable<CollectionEntity> collections)
    {
        var sb = new System.Text.StringBuilder();
        foreach (var c in collections)
        {
            sb.AppendLine($"[{c.CollectionUnitId}]");
            sb.AppendLine($"Title: {c.Title}");
            if (c.DateRange is not null) sb.AppendLine($"Dates: {c.DateRange}");
            if (c.Repository is not null) sb.AppendLine($"Repository: {c.Repository}");
            if (c.Extent is not null) sb.AppendLine($"Extent: {c.Extent}");
            if (c.Abstract is not null) sb.AppendLine($"Abstract: {c.Abstract}");
            if (c.ScopeContent is not null)
                sb.AppendLine($"Scope: {c.ScopeContent[..Math.Min(1500, c.ScopeContent.Length)]}");
            if (c.BiogHist is not null)
                sb.AppendLine($"History: {c.BiogHist[..Math.Min(1200, c.BiogHist.Length)]}");
            if (c.Subjects.Length > 0) sb.AppendLine($"Subjects: {string.Join("; ", c.Subjects)}");
            if (c.Persnames.Length > 0) sb.AppendLine($"People: {string.Join("; ", c.Persnames)}");
            if (c.Corpnames.Length > 0) sb.AppendLine($"Organizations: {string.Join("; ", c.Corpnames)}");
            if (c.Geognames.Length > 0) sb.AppendLine($"Places: {string.Join("; ", c.Geognames)}");
            if (c.Genres.Length > 0) sb.AppendLine($"Genres: {string.Join("; ", c.Genres)}");
            if (c.SeriesTitles.Length > 0)
                sb.AppendLine($"Series: {string.Join("; ", c.SeriesTitles.Take(5))}");
            sb.AppendLine();
        }
        return sb.ToString();
    }

    private static string BuildResultsContext(IEnumerable<CollectionResult> results)
    {
        var sb = new System.Text.StringBuilder();
        foreach (var r in results)
        {
            sb.AppendLine($"[{r.CollectionUnitId}] {r.Title} ({r.DateRange})");
            if (r.Abstract is not null) sb.AppendLine($"  {r.Abstract}");
            if (r.Subjects.Count > 0)
                sb.AppendLine($"  Subjects: {string.Join("; ", r.Subjects.Take(5))}");
            sb.AppendLine();
        }
        return sb.ToString();
    }
}

/// <summary>Intermediate model for deserializing Haiku's pass-2 JSON response.</summary>
public class RankedResult
{
    public string CollectionId { get; set; } = string.Empty;
    public int Rank { get; set; }
    public int RelevanceScore { get; set; }
    public string Explanation { get; set; } = string.Empty;
}

/// <summary>Intermediate model for deserializing Haiku's query expansion JSON response.</summary>
internal class ExpandedQueryResultRaw
{
    public List<string> Phrases { get; set; } = [];
    public DateRangeRaw? DateRange { get; set; }
}

/// <summary>Intermediate model for the optional dateRange object in expansion response.</summary>
internal class DateRangeRaw
{
    public int? Start { get; set; }
    public int? End { get; set; }
}
