using ArchiveSearch.API.Services;
using ArchiveSearch.Data.Repositories;
using Microsoft.AspNetCore.Mvc;

namespace ArchiveSearch.API.Controllers;

[ApiController]
[Route("api/admin/[controller]")]
public class IndexController(
    IndexingService indexingService,
    CollectionRepository repository,
    ILogger<IndexController> logger) : ControllerBase
{
    [HttpPost]
    public async Task<IActionResult> Index([FromBody] IndexRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.CollectionsPath))
            return BadRequest(new { error = "collectionsPath is required." });

        try
        {
            logger.LogInformation("Starting indexing of: {Path}", request.CollectionsPath);
            var result = await indexingService.IndexDirectoryAsync(
                request.CollectionsPath, request.Force, ct);

            return Ok(new
            {
                indexed = result.Indexed,
                skipped = result.Skipped,
                errors = result.Errors,
                errorSamples = result.ErrorMessages.Take(10)
            });
        }
        catch (DirectoryNotFoundException)
        {
            return BadRequest(new { error = $"Directory not found: {request.CollectionsPath}" });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Indexing failed");
            return StatusCode(500, new { error = ex.Message });
        }
    }
}

public class IndexRequest
{
    public string CollectionsPath { get; set; } = string.Empty;
    public bool Force { get; set; } = false;
}
