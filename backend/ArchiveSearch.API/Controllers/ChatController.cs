using ArchiveSearch.API.Services;
using ArchiveSearch.Core.Cache;
using ArchiveSearch.Core.Models;
using Microsoft.AspNetCore.Mvc;

namespace ArchiveSearch.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ChatController(
    ClaudeService claude,
    SearchCache cache,
    ILogger<ChatController> logger) : ControllerBase
{
    [HttpPost]
    public async Task<ActionResult<ChatResponse>> Chat([FromBody] ChatRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        // Retrieve the original search results to use as grounding context
        var searchResult = cache.GetSearchResult(request.ContextId);
        if (searchResult is null)
            return BadRequest(new { error = "Search context not found or expired. Please search again." });

        try
        {
            var response = await claude.ChatAsync(request.Messages, searchResult.Results);
            return Ok(new ChatResponse { Response = response });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Chat failed for context: {ContextId}", request.ContextId);
            return StatusCode(500, new { error = "Chat request failed. Please try again." });
        }
    }
}
