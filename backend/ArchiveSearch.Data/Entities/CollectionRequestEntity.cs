using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ArchiveSearch.Data.Entities;

[Table("collection_requests")]
public class CollectionRequestEntity
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Required]
    [Column("requester_name")]
    public string RequesterName { get; set; } = string.Empty;

    [Required]
    [Column("requester_email")]
    public string RequesterEmail { get; set; } = string.Empty;

    [Column("requester_phone")]
    public string? RequesterPhone { get; set; }

    [Column("affiliation")]
    public string? Affiliation { get; set; }

    [Column("notes")]
    public string? Notes { get; set; }

    [Required]
    [Column("collections_json")]
    public string CollectionsJson { get; set; } = string.Empty;

    [Required]
    [Column("submitted_at")]
    public DateTime SubmittedAt { get; set; }

    [Required]
    [Column("email_sent")]
    public bool EmailSent { get; set; }
}
