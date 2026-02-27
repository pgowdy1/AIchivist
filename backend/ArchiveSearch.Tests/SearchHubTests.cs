using Anthropic;
using ArchiveSearch.API.Hubs;
using ArchiveSearch.API.Models;
using ArchiveSearch.API.Services;
using ArchiveSearch.Core.Cache;
using ArchiveSearch.Core.Models;
using ArchiveSearch.Data;
using ArchiveSearch.Data.Repositories;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace ArchiveSearch.Tests;

public class SearchHubTests
{
    private readonly SearchService _searchService;
    private readonly ISingleClientProxy _callerProxy;
    private readonly SearchHub _hub;

    public SearchHubTests()
    {
        // Build real lightweight dependencies for SearchService's primary constructor.
        // Substitute.ForPartsOf requires the actual constructor args so NSubstitute
        // can create a subclass proxy with SearchAsync overridden.
        var anthropicClient = new AnthropicClient { ApiKey = "sk-ant-test-dummy" };
        var claudeService = new ClaudeService(anthropicClient, NullLogger<ClaudeService>.Instance);
        var dbOptions = new DbContextOptionsBuilder<ArchiveContext>()
            .UseInMemoryDatabase("SearchHubTests-" + Guid.NewGuid())
            .Options;
        var repository = new CollectionRepository(new ArchiveContext(dbOptions));
        var searchCache = new SearchCache(new MemoryCache(new MemoryCacheOptions()));

        _searchService = Substitute.ForPartsOf<SearchService>(
            claudeService, repository, searchCache, NullLogger<SearchService>.Instance);

        _callerProxy = Substitute.For<ISingleClientProxy>();

        var hubCallerClients = Substitute.For<IHubCallerClients>();
        hubCallerClients.Caller.Returns(_callerProxy);

        _hub = new SearchHub(_searchService, NullLogger<SearchHub>.Instance)
        {
            Clients = hubCallerClients
        };
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task StartSearch_WithEmptyOrNullQuery_SendsSearchFailed(string? query)
    {
        await _hub.StartSearch(query!);

        // SendAsync extension method delegates to SendCoreAsync on the interface
        await _callerProxy.Received(1).SendCoreAsync(
            "SearchFailed",
            Arg.Is<object?[]>(args => args.Length == 1 && args[0] != null),
            Arg.Any<CancellationToken>());

        await _searchService.DidNotReceive().SearchAsync(
            Arg.Any<string>(),
            Arg.Any<IProgress<SearchProgress>>());
    }

    [Fact]
    public async Task StartSearch_WithValidQuery_SendsSearchCompleted()
    {
        var expectedResponse = new SearchResponse
        {
            Query = "test query",
            ContextId = "abc123",
            Results =
            [
                new CollectionResult
                {
                    Rank = 1,
                    RelevanceScore = 10,
                    RelevanceExplanation = "Highly relevant",
                    IsAiRanked = true,
                    CollectionUnitId = "coll-001",
                    Title = "Test Collection"
                }
            ],
            AdditionalResults = []
        };

        _searchService.SearchAsync(
            Arg.Any<string>(),
            Arg.Any<IProgress<SearchProgress>>())
            .Returns(expectedResponse);

        await _hub.StartSearch("test query");

        await _callerProxy.Received(1).SendCoreAsync(
            "SearchCompleted",
            Arg.Is<object?[]>(args => args.Length == 1 && args[0] == expectedResponse),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task StartSearch_WithValidQuery_CallsSearchService()
    {
        _searchService.SearchAsync(
            Arg.Any<string>(),
            Arg.Any<IProgress<SearchProgress>>())
            .Returns(new SearchResponse { Query = "test", ContextId = "id", Results = [], AdditionalResults = [] });

        await _hub.StartSearch("archival photographs");

        await _searchService.Received(1).SearchAsync(
            "archival photographs",
            Arg.Any<IProgress<SearchProgress>>());
    }

    [Fact]
    public async Task StartSearch_WhenSearchServiceThrows_SendsSearchFailed()
    {
        var exception = new Exception("Service unavailable");
        exception.Data["step"] = "ranking_results";

        _searchService.SearchAsync(
            Arg.Any<string>(),
            Arg.Any<IProgress<SearchProgress>>())
            .ThrowsAsync(exception);

        await _hub.StartSearch("failing query");

        await _callerProxy.Received(1).SendCoreAsync(
            "SearchFailed",
            Arg.Is<object?[]>(args => args.Length == 1 && args[0] != null),
            Arg.Any<CancellationToken>());

        await _callerProxy.DidNotReceive().SendCoreAsync(
            "SearchCompleted",
            Arg.Any<object?[]>(),
            Arg.Any<CancellationToken>());
    }
}
