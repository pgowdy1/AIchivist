using System.ComponentModel.DataAnnotations;

namespace ArchiveSearch.Core.Models;

public class SearchRequest
{
    [Required]
    [MinLength(3)]
    [MaxLength(500)]
    public string Query { get; set; } = string.Empty;
}
