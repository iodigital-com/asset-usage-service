using AssetUsageService.Business.Events;
using AssetUsageService.Business.Handlers.interfaces;
using AssetUsageService.Integration;
using Microsoft.Extensions.Logging;
using Stylelabs.M.Base.Querying;
using Stylelabs.M.Base.Querying.Filters;
using Stylelabs.M.Framework.Essentials.LoadConfigurations;
using Stylelabs.M.Framework.Essentials.LoadOptions;
using Stylelabs.M.Sdk.Contracts.Base;
using Stylelabs.M.Sdk.WebClient;

namespace AssetUsageService.Business.Handlers;

public sealed class AssetIdsByPublicLinksHandler : IEventHandler<AssetIdsByPublicLinksEvent>
{
    private const string RelativeUrlProperty = "RelativeUrl";
    private const string AssetToPublicLinkRelation = "AssetToPublicLink";
    private const int BatchSize = 50; 

    private readonly IContentHubConnectionService _contentHubConnection;
    private readonly ILogger<AssetIdsByPublicLinksHandler> _logger;

    public AssetIdsByPublicLinksHandler(IContentHubConnectionService contentHubConnection, ILogger<AssetIdsByPublicLinksHandler> logger)
    {
        _contentHubConnection = contentHubConnection;
        _logger = logger;
    }

    public async Task HandleAsync(AssetIdsByPublicLinksEvent @event, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(@event);
        ArgumentNullException.ThrowIfNull(@event.PublicLinks);

        try
        {
            var assetIds = await GetAssetIdsByPublicLinksAsync(@event.PublicLinks, cancellationToken);

            _logger.LogInformation("Successfully retrieved {Count} asset IDs from {LinkCount} public links",
                assetIds.Count, @event.PublicLinks.Count);
            @event.AssetIds = assetIds;
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Failed to retrieve asset IDs from public links");
            throw;
        }
    }

    private async Task<List<int>> GetAssetIdsByPublicLinksAsync(IReadOnlyList<string> publicLinks, CancellationToken cancellationToken)
    {
        if (publicLinks.Count == 0)
        {
            return new List<int>();
        }

        var isReachable = await _contentHubConnection.IsReachableAsync(cancellationToken);
        if (!isReachable)
        {
            _logger.LogError("ContentHub is not reachable. Cannot process public links.");
            throw new InvalidOperationException("ContentHub is not reachable. Please verify ContentHub:Endpoint configuration and network connectivity.");
        }

        var contentHubClient = _contentHubConnection.CreateClient();

        var urlMapping = publicLinks
            .Select(url => new { OriginalUrl = url, RelativeUrl = ExtractRelativeUrl(url) })
            .Where(x => x.RelativeUrl != null)
            .DistinctBy(x => x.RelativeUrl)
            .ToList();

        if (urlMapping.Count == 0)
        {
            return new List<int>();
        }

        var assetIds = new List<int>(urlMapping.Count);

        var batches = urlMapping
            .Select((x, i) => new { x.RelativeUrl, Index = i })
            .GroupBy(x => x.Index / BatchSize)
            .Select(g => g.Select(x => x.RelativeUrl!).ToList())
            .ToList();

        var batchTasks = batches.Select(batch => 
            ProcessBatchAsync(contentHubClient, batch, cancellationToken));

        var batchResults = await Task.WhenAll(batchTasks);

        foreach (var result in batchResults)
        {
            assetIds.AddRange(result);
        }

        return assetIds;
    }

    private async Task<List<int>> ProcessBatchAsync(IWebMClient contentHubClient, List<string> relativeUrls, CancellationToken cancellationToken)
    {
        try
        {
            var query = new Query
            {
                Filter = new PropertyQueryFilter
                {
                    Property = RelativeUrlProperty,
                    Values = relativeUrls,
                    DataType = FilterDataType.String
                }
            };

            var publicLinksResult = await contentHubClient.Querying.QueryAsync(query);

            if (!publicLinksResult.Items.Any())
            {
                _logger.LogWarning("No public links found for batch of {Count} URLs", relativeUrls.Count);
                return new List<int>();
            }

            var publicLinkIds = publicLinksResult.Items.Select(x => x.Id!.Value).ToList();

            var publicLinkEntities = await contentHubClient.Entities.GetManyAsync(
                publicLinkIds,
                new EntityLoadConfiguration(
                    CultureLoadOption.Default,
                    PropertyLoadOption.None, 
                    new RelationLoadOption(AssetToPublicLinkRelation)));

            var assetIds = new List<int>(publicLinkEntities.Count());

            foreach (var publicLink in publicLinkEntities)
            {
                var assetToPublicLinkRelation = publicLink.GetRelation<IChildToManyParentsRelation>(AssetToPublicLinkRelation);
                var assetId = assetToPublicLinkRelation?.GetIds().FirstOrDefault();

                if (assetId.HasValue && assetId.Value <= int.MaxValue)
                {
                    assetIds.Add((int)assetId.Value);
                }
            }

            return assetIds;
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Error processing batch of {Count} public links", relativeUrls.Count);
            return new List<int>();
        }
    }

    private string? ExtractRelativeUrl(string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
        {
            _logger.LogWarning("Invalid URL format: {Url}", url);
            return null;
        }

        if (uri.Segments.Length == 0)
        {
            _logger.LogWarning("URL has no path segments: {Url}", url);
            return null;
        }

        var segment = uri.Segments[^1];
        if (string.IsNullOrEmpty(segment))
        {
            _logger.LogWarning("URL segment is empty: {Url}", url);
            return null;
        }

        return segment.Split('?')[0];
    }
}