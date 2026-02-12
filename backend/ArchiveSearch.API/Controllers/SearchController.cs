using ArchiveSearch.API.Services;
using ArchiveSearch.Core.Models;
using Microsoft.AspNetCore.Mvc;

namespace ArchiveSearch.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class SearchController(SearchService searchService, ILogger<SearchController> logger) : ControllerBase
{
    [HttpPost]
    public async Task<ActionResult<SearchResponse>> Search([FromBody] SearchRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        try
        {
            var response = await searchService.SearchAsync(request.Query);
            return Ok(response);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Search failed for query: {Query}", request.Query);
            return StatusCode(500, new { error = "Search failed. Please try again." });
        }
    }
}
