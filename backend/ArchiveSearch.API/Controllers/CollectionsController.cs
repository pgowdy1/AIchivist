using ArchiveSearch.Core.Models;
using ArchiveSearch.Data.Repositories;
using Microsoft.AspNetCore.Mvc;

namespace ArchiveSearch.API.Controllers;

[ApiController]
[Route("api/collections")]
public class CollectionsController(CollectionRepository repository, ILogger<CollectionsController> logger) : ControllerBase
{
    /// <summary>
    /// Returns collections related to the given collection by entity co-occurrence
    /// (shared persons, organizations, subjects, places, and date overlap).
    /// </summary>
    [HttpGet("{unitId}/related")]
    public async Task<ActionResult<List<RelatedCollection>>> GetRelated(string unitId)
    {
        if (string.IsNullOrWhiteSpace(unitId))
            return BadRequest(new { error = "Collection unit ID is required." });

        try
        {
            var related = await repository.FindRelatedAsync(unitId);
            return Ok(related);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to find related collections for {UnitId}", unitId);
            return StatusCode(500, new { error = "Failed to find related collections. Please try again." });
        }
    }
}
