namespace ArchiveSearch.Core.Models;

/// <summary>
/// Result from Claude query expansion (Pass 0), carrying both expanded search
/// phrases and an optional date range extracted from temporal signals in the query.
/// </summary>
public class QueryExpansionResult
{
    public List<string> Phrases { get; set; } = [];
    public int? DateStart { get; set; }  // null if no temporal signal detected
    public int? DateEnd { get; set; }
}
