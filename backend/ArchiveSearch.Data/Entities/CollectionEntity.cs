using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ArchiveSearch.Data.Entities;

[Table("collections")]
public class CollectionEntity
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("collection_unitid")]
    [MaxLength(200)]
    public string CollectionUnitId { get; set; } = string.Empty;

    [Column("title")]
    public string Title { get; set; } = string.Empty;

    [Column("repository")]
    public string? Repository { get; set; }

    [Column("date_range")]
    public string? DateRange { get; set; }

    [Column("date_start")]
    public int? DateStart { get; set; }

    [Column("date_end")]
    public int? DateEnd { get; set; }

    [Column("extent")]
    public string? Extent { get; set; }

    [Column("abstract")]
    public string? Abstract { get; set; }

    [Column("scope_content")]
    public string? ScopeContent { get; set; }

    [Column("biog_hist")]
    public string? BiogHist { get; set; }

    [Column("subjects")]
    public string[] Subjects { get; set; } = [];

    [Column("persnames")]
    public string[] Persnames { get; set; } = [];

    [Column("geognames")]
    public string[] Geognames { get; set; } = [];

    [Column("genres")]
    public string[] Genres { get; set; } = [];

    [Column("corpnames")]
    public string[] Corpnames { get; set; } = [];

    [Column("series_titles")]
    public string[] SeriesTitles { get; set; } = [];

    [Column("compact_line")]
    public string CompactLine { get; set; } = string.Empty;

    [Column("source_file")]
    public string? SourceFile { get; set; }
}
