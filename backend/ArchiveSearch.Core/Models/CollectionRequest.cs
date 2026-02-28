using System.ComponentModel.DataAnnotations;

namespace ArchiveSearch.Core.Models;

public class CollectionRequestSubmission
{
    [Required]
    public string RequesterName { get; set; } = string.Empty;

    [Required]
    [EmailAddress]
    public string RequesterEmail { get; set; } = string.Empty;

    public string? RequesterPhone { get; set; }

    public string? Affiliation { get; set; }

    public string? Notes { get; set; }

    [Required]
    [MinLength(1)]
    public List<RequestedCollection> Collections { get; set; } = [];
}

public class RequestedCollection
{
    [Required]
    public string CollectionUnitId { get; set; } = string.Empty;

    [Required]
    public string Title { get; set; } = string.Empty;

    public string? Repository { get; set; }

    public string? DateRange { get; set; }
}

public class CollectionRequestResponse
{
    public bool Success { get; set; }
    public bool EmailSent { get; set; }
    public string? Message { get; set; }
}
