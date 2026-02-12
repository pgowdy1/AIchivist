namespace ArchiveSearch.Core.Models;

public class SearchResponse
{
    public string Query { get; set; } = string.Empty;
    public string ContextId { get; set; } = string.Empty;
    public List<CollectionResult> Results { get; set; } = [];
    public List<CollectionResult> AdditionalResults { get; set; } = [];
    public bool Cached { get; set; }
}

public class CollectionResult
{
    public int Rank { get; set; }
    public int RelevanceScore { get; set; }
    public string RelevanceExplanation { get; set; } = string.Empty;
    public bool IsAiRanked { get; set; }
    public string CollectionUnitId { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string? Repository { get; set; }
    public string? DateRange { get; set; }
    public string? Extent { get; set; }
    public string? Abstract { get; set; }
    public string? ScopeContent { get; set; }
    public string? BiogHist { get; set; }
    public List<string> Subjects { get; set; } = [];
    public List<string> Persnames { get; set; } = [];
    public List<string> Geognames { get; set; } = [];
    public List<string> Genres { get; set; } = [];
    public List<string> SeriesTitles { get; set; } = [];
}
