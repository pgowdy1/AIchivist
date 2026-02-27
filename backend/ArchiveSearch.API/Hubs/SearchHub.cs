using ArchiveSearch.API.Models;
using ArchiveSearch.API.Services;
using Microsoft.AspNetCore.SignalR;

namespace ArchiveSearch.API.Hubs;

public class SearchHub(SearchService searchService, ILogger<SearchHub> logger) : Hub
{
    public async Task StartSearch(string query)
    {
        var caller = Clients.Caller;

        var progress = new Progress<SearchProgress>(async p =>
        {
            await caller.SendAsync("SearchProgress", p);
        });

        try
        {
            var response = await searchService.SearchAsync(query, progress);
            await caller.SendAsync("SearchCompleted", response);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "SignalR search failed for query: {Query}", query);

            var failedStep = ex.Data.Contains("step")
                ? ex.Data["step"]?.ToString() ?? "unknown"
                : "unknown";

            await caller.SendAsync("SearchFailed", new { error = ex.Message, failedStep });
        }
    }
}
