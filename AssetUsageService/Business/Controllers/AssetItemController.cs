using AssetUsageService.Business.Services;
using AssetUsageService.Business.Services.ServiceBusQueueServices;
using AssetUsageService.Domain.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace AssetUsageService.Business.Controllers;

public class AssetItemController
{
    private readonly PublishAssetIdsByPublicLinksEventService _publishGetAssetIdsByPublicLinksEventService;
    private readonly DeltaCalculationService _deltaCalculationService;
    private readonly PublishPushToDamEventsService _publishPushToDamEventsService;
    private readonly ILogger<AssetItemController> _logger;
    private readonly IServiceBusQueueService _serviceBusQueueService;
    private readonly ServiceBusConfigService _serviceBusConfigService;

    public AssetItemController(PublishAssetIdsByPublicLinksEventService publishGetAssetIdsByPublicLinksEventService, DeltaCalculationService deltaCalculationService, PublishPushToDamEventsService publishPushToDamEventsService, ILogger<AssetItemController> logger, IServiceBusQueueService serviceBusQueueService, IConfiguration configuration, ServiceBusConfigService serviceBusConfigService)
    {
        _publishGetAssetIdsByPublicLinksEventService = publishGetAssetIdsByPublicLinksEventService;
        _deltaCalculationService = deltaCalculationService;
        _publishPushToDamEventsService = publishPushToDamEventsService;
        _logger = logger;
        _serviceBusQueueService = serviceBusQueueService;
        _serviceBusConfigService = serviceBusConfigService;
    }

    public async Task ProcessPublishedItemAsync(PublishedItem publishedItem, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(publishedItem);

        if (publishedItem.PublicLinks?.Count == 0)
        {
            _logger.LogInformation("Item {ItemId} has no public links, skipping to delta calculation", publishedItem.ItemId);
            await EnqueueDeltaCalculationAsync(publishedItem, cancellationToken);
            return;
        }
        else
        {
            await EnqueuePublicLinkProcessingAsync(publishedItem, cancellationToken);
        }
    }
    public async Task EnqueuePublicLinkProcessingAsync(PublishedItem publishedItem, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(publishedItem);
        try
        {
            if (!_serviceBusConfigService.IsPublicLinkQueueEnabled)
            {
                var assetIdsFromPublicLinks = await GetAssetIdsFromPublicLinkAsync(publishedItem, cancellationToken);
                var publishedItemWithIdsFromLinks = await AddPublicLinkAssetIdsToPublishedItem(publishedItem, assetIdsFromPublicLinks, cancellationToken);
                await EnqueueDeltaCalculationAsync(publishedItemWithIdsFromLinks, cancellationToken);
                _logger.LogWarning("Public link queue is not enabled. Skipping enqueue for item {ItemId}", publishedItem.ItemId);
                return;
            } 
            var queueName = _serviceBusConfigService.PublicLinkQueueName;
            await _serviceBusQueueService.SendMessageAsync(queueName!, publishedItem, cancellationToken);
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Failed to enqueue public link processing for item {ItemId}", publishedItem.ItemId);
            throw;
        }
    }
    public async Task EnqueueDeltaCalculationAsync(PublishedItem publishedItem, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(publishedItem);

        try
        {
            if(!_serviceBusConfigService.IsDeltaCalculationQueueEnabled)
            {
                var itemAssetChanges = await _deltaCalculationService.CalculateDeltaAsync(publishedItem, publishedItem.AssetIds, cancellationToken);
                await _publishPushToDamEventsService.PublishPushToDamEventsAsync(itemAssetChanges, cancellationToken);
                _logger.LogWarning("Delta calculation queue is not enabled. Skipping enqueue for item {ItemId}", publishedItem.ItemId);
                return;
            }
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Failed to enqueue delta calculation for item {ItemId}", publishedItem.ItemId);
            throw;
        }
    }
    public async Task<List<int>> GetAssetIdsFromPublicLinkAsync(PublishedItem publishedItem, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(publishedItem);
        try
        {
            var allAssetIds = await _publishGetAssetIdsByPublicLinksEventService.GetAssetIdsByPublicLinksAsync(publishedItem, cancellationToken);
            return allAssetIds;
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Failed to retrieve asset IDs from public links for item {ItemId}", publishedItem.ItemId);
            throw;
        }
    }
    public async Task<PublishedItem> AddPublicLinkAssetIdsToPublishedItem(PublishedItem publishedItem, List<int> AssetIdsFromPublicLinks, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(publishedItem);
        try
        {
            var allAssetIds = publishedItem.AssetIds.Concat(AssetIdsFromPublicLinks).Distinct().ToList();
            publishedItem.AssetIds = allAssetIds;
            return publishedItem;
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Failed to retrieve total asset IDs for item {ItemId}", publishedItem.ItemId);
            throw;
        }
    }
}
