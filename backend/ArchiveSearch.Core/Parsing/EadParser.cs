using System.Text;
using System.Xml.Linq;
using ArchiveSearch.Core.Models;

namespace ArchiveSearch.Core.Parsing;

/// <summary>
/// Parses EAD (Encoded Archival Description) XML finding aids into CollectionDocument objects.
/// One CollectionDocument is produced per EAD file (collection-level granularity, not chunk-level).
/// </summary>
public static class EadParser
{
    private static readonly XNamespace Ead = "urn:isbn:1-931666-22-9";

    public static CollectionDocument Parse(string filePath)
    {
        XDocument xdoc;
        try
        {
            xdoc = XDocument.Load(filePath);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to load XML: {filePath}", ex);
        }

        var root = xdoc.Root ?? throw new InvalidOperationException($"Empty XML: {filePath}");

        // Support both namespaced and non-namespaced EAD
        XNamespace ns = root.Name.Namespace;

        var archdesc = root.Descendants(ns + "archdesc").FirstOrDefault()
                    ?? root.Descendants("archdesc").FirstOrDefault();
        var did = archdesc?.Element(ns + "did") ?? archdesc?.Element("did");

        var doc = new CollectionDocument
        {
            SourceFile = Path.GetFileName(filePath)
        };

        // Collection unit ID
        doc.CollectionUnitId = GetText(did?.Elements().FirstOrDefault(e =>
            e.Name.LocalName == "unitid"))?.Trim() ?? Path.GetFileNameWithoutExtension(filePath);

        // Title
        doc.Title = GetText(did?.Elements().FirstOrDefault(e =>
            e.Name.LocalName == "unittitle"))?.Trim() ?? string.Empty;

        // Dates
        ParseDates(did, doc);

        // Repository
        doc.Repository = GetText(did?.Descendants().FirstOrDefault(e =>
            e.Name.LocalName == "corpname"))?.Trim();

        // Physical description / extent
        var extents = did?.Descendants()
            .Where(e => e.Name.LocalName == "extent")
            .Select(e => GetText(e)?.Trim())
            .Where(t => !string.IsNullOrWhiteSpace(t))
            .ToList() ?? [];
        doc.Extent = extents.Count > 0 ? string.Join("; ", extents) : null;

        // Abstract (prefer <abstract> element, fall back to first scopecontent paragraph)
        doc.Abstract = GetText(did?.Descendants().FirstOrDefault(e =>
            e.Name.LocalName == "abstract"))?.Trim();

        // Scope and content
        var scopecontent = archdesc?.Descendants().FirstOrDefault(e =>
            e.Name.LocalName == "scopecontent");
        doc.ScopeContent = JoinParagraphs(scopecontent)?.Trim();

        if (doc.Abstract is null && doc.ScopeContent is not null)
        {
            // Use the first sentence of scopecontent as the abstract
            var firstSentenceEnd = doc.ScopeContent.IndexOfAny(['.', '!', '?']);
            doc.Abstract = firstSentenceEnd > 0
                ? doc.ScopeContent[..(firstSentenceEnd + 1)]
                : doc.ScopeContent[..Math.Min(200, doc.ScopeContent.Length)];
        }

        // Biographical/historical note
        var bioghist = archdesc?.Descendants().FirstOrDefault(e =>
            e.Name.LocalName == "bioghist");
        doc.BiogHist = JoinParagraphs(bioghist)?.Trim();

        // controlaccess elements
        var controlaccess = archdesc?.Descendants().FirstOrDefault(e =>
            e.Name.LocalName == "controlaccess");

        if (controlaccess is not null)
        {
            doc.Subjects = GetControlAccessTerms(controlaccess, "subject");
            doc.Persnames = GetControlAccessTerms(controlaccess, "persname");
            doc.Geognames = GetControlAccessTerms(controlaccess, "geogname");
            doc.Genres = GetControlAccessTerms(controlaccess, "genreform");
            doc.Corpnames = GetControlAccessTerms(controlaccess, "corpname");
        }

        // Top-level series titles (c01 only)
        var dsc = archdesc?.Descendants().FirstOrDefault(e => e.Name.LocalName == "dsc");
        if (dsc is not null)
        {
            doc.SeriesTitles = dsc.Elements()
                .Where(e => e.Name.LocalName == "c01")
                .Select(c01 =>
                {
                    var c01did = c01.Elements().FirstOrDefault(e => e.Name.LocalName == "did");
                    return GetText(c01did?.Elements().FirstOrDefault(e =>
                        e.Name.LocalName == "unittitle"))?.Trim();
                })
                .Where(t => !string.IsNullOrWhiteSpace(t))
                .Select(t => t!)
                .ToList();
        }

        doc.CompactLine = BuildCompactLine(doc);
        return doc;
    }

    private static void ParseDates(XElement? did, CollectionDocument doc)
    {
        var unitdate = did?.Descendants().FirstOrDefault(e => e.Name.LocalName == "unitdate");
        if (unitdate is null) return;

        var normalAttr = unitdate.Attribute("normal")?.Value;
        var displayText = GetText(unitdate)?.Trim();

        doc.DateRange = displayText ?? normalAttr;

        if (normalAttr is not null)
        {
            // normal attribute format: "1899/1909" or "1899" or "1899-01-01/1909-12-31"
            var parts = normalAttr.Split('/');
            if (parts.Length == 2)
            {
                doc.DateStart = ParseYear(parts[0]);
                doc.DateEnd = ParseYear(parts[1]);
            }
            else if (parts.Length == 1)
            {
                doc.DateStart = ParseYear(parts[0]);
                doc.DateEnd = doc.DateStart;
            }
        }
    }

    private static int? ParseYear(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        // May be "1899-01-01" or "1899"
        var yearStr = value.Length >= 4 ? value[..4] : value;
        return int.TryParse(yearStr, out var year) ? year : null;
    }

    private static string? JoinParagraphs(XElement? parent)
    {
        if (parent is null) return null;
        var paragraphs = parent.Descendants()
            .Where(e => e.Name.LocalName == "p")
            .Select(e => GetText(e)?.Trim())
            .Where(t => !string.IsNullOrWhiteSpace(t));
        var joined = string.Join(" ", paragraphs);
        return string.IsNullOrWhiteSpace(joined) ? null : joined;
    }

    private static List<string> GetControlAccessTerms(XElement controlaccess, string localName)
    {
        return controlaccess.Descendants()
            .Where(e => e.Name.LocalName == localName)
            .Select(e => GetText(e)?.Trim())
            .Where(t => !string.IsNullOrWhiteSpace(t))
            .Select(t => t!)
            .Distinct()
            .ToList();
    }

    /// <summary>Gets all inner text content of an element, stripping child element tags.</summary>
    private static string? GetText(XElement? element)
    {
        if (element is null) return null;
        var sb = new StringBuilder();
        foreach (var node in element.DescendantNodes())
        {
            if (node is XText text)
                sb.Append(text.Value);
        }
        var result = sb.ToString().Trim();
        return string.IsNullOrWhiteSpace(result) ? null : result;
    }

    /// <summary>
    /// Builds the ultra-compact catalog line for Haiku pass-1.
    /// Target: 11-12 tokens average.
    /// Format: [UNITID] TruncatedTitle DATES | Subject1; Subject2; Subject3
    /// </summary>
    private static string BuildCompactLine(CollectionDocument doc)
    {
        // Truncate title to 5 words maximum
        var titleWords = doc.Title.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var shortTitle = string.Join(' ', titleWords.Take(5));

        // Date range
        var dates = doc.DateRange is not null ? $" {doc.DateRange}" : string.Empty;

        // Top 3 subjects (prefer subjects, fall back to genres/persnames)
        var terms = doc.Subjects.Take(3).ToList();
        if (terms.Count < 2)
            terms.AddRange(doc.Geognames.Take(2 - terms.Count));
        if (terms.Count < 2)
            terms.AddRange(doc.Genres.Take(2 - terms.Count));

        // Strip long subject qualifiers (e.g. "Subject -- Washington (State) -- History" → "Subject -- Washington")
        var compactTerms = terms
            .Select(t =>
            {
                var parts = t.Split("--", StringSplitOptions.TrimEntries);
                return parts.Length > 2 ? string.Join(" -- ", parts.Take(2)) : t;
            })
            .Take(3);

        var subjectPart = terms.Count > 0 ? " | " + string.Join("; ", compactTerms) : string.Empty;

        return $"[{doc.CollectionUnitId}] {shortTitle}{dates}{subjectPart}";
    }
}
