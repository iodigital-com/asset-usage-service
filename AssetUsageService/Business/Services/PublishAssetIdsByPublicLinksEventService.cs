using AssetUsageService.Business.Events;
using AssetUsageService.Business.Events.interfaces;
using AssetUsageService.Domain.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace AssetUsageService.Business.Services;

public class PublishAssetIdsByPublicLinksEventService
{
    private const string ContentHubEndpointConfigKey = "ContentHub:Endpoint";

    private readonly IMediator _mediator;
    private readonly IConfiguration _configuration;
    private readonly ILogger<PublishAssetIdsByPublicLinksEventService> _logger;

    public PublishAssetIdsByPublicLinksEventService(IMediator mediator, IConfiguration configuration, ILogger<PublishAssetIdsByPublicLinksEventService> logger)
    {
        _mediator = mediator;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<List<int>> GetAssetIdsByPublicLinksAsync(PublishedItem publishedItem, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(publishedItem);

        if (publishedItem.PublicLinks is null || publishedItem.PublicLinks.Count == 0)
        {
            _logger.LogDebug("No public links found for item {ItemId}", publishedItem.ItemId);
            return new List<int>();
        }

        var contentHubLinks = FilterContentHubLinks(publishedItem.PublicLinks);

        if (contentHubLinks.Count == 0)
        {
            _logger.LogDebug("No ContentHub links found for item {ItemId}", publishedItem.ItemId);
            return new List<int>();
        }

        var @event = new AssetIdsByPublicLinksEvent
        {
            PublicLinks = contentHubLinks
        };

        await _mediator.PublishAsync(@event, cancellationToken);

        var assetIds = @event.AssetIds ?? new List<int>();

        _logger.LogInformation("Retrieved {AssetCount} asset IDs from {LinkCount} ContentHub links for item {ItemId}",
            assetIds.Count, contentHubLinks.Count, publishedItem.ItemId);

        return assetIds;
    }

    private List<string> FilterContentHubLinks(List<string> publicLinks)
    {
        var contentHubEndpoint = _configuration.GetValue<string>(ContentHubEndpointConfigKey);

        if (string.IsNullOrWhiteSpace(contentHubEndpoint))
        {
            _logger.LogWarning("{ConfigKey} configuration is missing or empty", ContentHubEndpointConfigKey);
            return new List<string>();
        }

        var filteredLinks = publicLinks
            .Where(link => !string.IsNullOrEmpty(link) && link.StartsWith(contentHubEndpoint, StringComparison.OrdinalIgnoreCase))
            .ToList();

        _logger.LogDebug("Filtered {FilteredCount} ContentHub links from {TotalCount} total public links",
            filteredLinks.Count, publicLinks.Count);

        return filteredLinks;
    }
}

