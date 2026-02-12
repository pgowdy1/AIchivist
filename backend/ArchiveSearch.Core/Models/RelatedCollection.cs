namespace ArchiveSearch.Core.Models;

/// <summary>
/// Lightweight DTO for a collection related by entity overlap (shared persons,
/// organizations, subjects, places, overlapping dates) to a source collection.
/// </summary>
public class RelatedCollection
{
    public string CollectionUnitId { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string? Repository { get; set; }
    public string? DateRange { get; set; }
    public string? Abstract { get; set; }
    public double OverlapScore { get; set; }
    public List<string> SharedSubjects { get; set; } = [];
    public List<string> SharedPersons { get; set; } = [];
    public List<string> SharedOrganizations { get; set; } = [];
    public List<string> SharedPlaces { get; set; } = [];

    /// <summary>Human-readable summary, e.g. "Shares: 3 subjects, 1 person".</summary>
    public string OverlapSummary { get; set; } = string.Empty;
}
