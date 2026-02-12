using ArchiveSearch.Data.Repositories;
using Microsoft.AspNetCore.Mvc;

namespace ArchiveSearch.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class HealthController(CollectionRepository repository) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Health()
    {
        bool dbConnected;
        int collectionsIndexed = 0;

        try
        {
            collectionsIndexed = await repository.CountAsync();
            dbConnected = true;
        }
        catch
        {
            dbConnected = false;
        }

        return Ok(new
        {
            status = dbConnected ? "ok" : "degraded",
            dbConnected,
            collectionsIndexed
        });
    }
}
