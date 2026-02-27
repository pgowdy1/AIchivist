using ArchiveSearch.API.Models;
using ArchiveSearch.API.Services;
using Microsoft.AspNetCore.SignalR;

namespace ArchiveSearch.API.Hubs;

public class SearchHub(SearchService searchService, ILogger<SearchHub> logger) : Hub
{
    public async Task StartSearch(string query)
    {
        var caller = Clients.Caller;

        if (string.IsNullOrWhiteSpace(query))
        {
            await caller.SendAsync("SearchFailed", new { error = "Query cannot be empty", failedStep = "expanding_query" });
            return;
        }

        var progress = new Progress<SearchProgress>(async p =>
        {
            try { await caller.SendAsync("SearchProgress", p); }
            catch (Exception ex) { logger.LogDebug(ex, "Failed to send progress (client may have disconnected)"); }
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
