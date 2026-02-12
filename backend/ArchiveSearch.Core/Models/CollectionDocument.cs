namespace ArchiveSearch.Core.Models;

/// <summary>
/// Parsed representation of an EAD XML finding aid. One per EAD file.
/// </summary>
public class CollectionDocument
{
    public string CollectionUnitId { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string? Repository { get; set; }
    public string? DateRange { get; set; }
    public int? DateStart { get; set; }
    public int? DateEnd { get; set; }
    public string? Extent { get; set; }
    public string? Abstract { get; set; }
    public string? ScopeContent { get; set; }
    public string? BiogHist { get; set; }
    public List<string> Subjects { get; set; } = [];
    public List<string> Persnames { get; set; } = [];
    public List<string> Geognames { get; set; } = [];
    public List<string> Genres { get; set; } = [];
    public List<string> Corpnames { get; set; } = [];
    public List<string> SeriesTitles { get; set; } = [];
    public string SourceFile { get; set; } = string.Empty;

    /// <summary>
    /// Ultra-compact one-liner for the Haiku pass-1 catalog.
    /// Format: [UNITID] Title Words DateRange | Subject1; Subject2; Subject3
    /// Target: 11-12 tokens average.
    /// </summary>
    public string CompactLine { get; set; } = string.Empty;
}
