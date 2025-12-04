using AssetUsageService.Business.Services;
using AssetUsageService.Business.Services.ServiceBusQueueServices.Interfaces;
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
    private readonly IServiceBusConfigService _serviceBusConfigService;

    public AssetItemController(PublishAssetIdsByPublicLinksEventService publishGetAssetIdsByPublicLinksEventService, DeltaCalculationService deltaCalculationService, PublishPushToDamEventsService publishPushToDamEventsService, ILogger<AssetItemController> logger, IServiceBusQueueService serviceBusQueueService, IServiceBusConfigService serviceBusConfigService)
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
            var queueName = _serviceBusConfigService.PublicLinkQueueName;
            if (!await QueueExistAsync(queueName))
            {
                _logger.LogWarning("Public link queue is not enabled. Skipping enqueue for item {ItemId}", publishedItem.ItemId);
                var assetIdsFromPublicLinks = await GetAssetIdsFromPublicLinkAsync(publishedItem, cancellationToken);
                var publishedItemWithIdsFromLinks = AddPublicLinkAssetIdsToPublishedItem(publishedItem, assetIdsFromPublicLinks, cancellationToken);
                await EnqueueDeltaCalculationAsync(publishedItemWithIdsFromLinks, cancellationToken);
            } else
            {
                await _serviceBusQueueService.SendMessageAsync(queueName!, publishedItem, cancellationToken);
            } 
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Failed to proccess public links for item {ItemId}", publishedItem.ItemId);
            throw;
        }
    }
    public async Task EnqueueDeltaCalculationAsync(PublishedItem publishedItem, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(publishedItem);

        try
        {
            var queueName = _serviceBusConfigService.DeltaCalculationQueueName;
            if (!await QueueExistAsync(queueName))
            {
                _logger.LogWarning("Delta calculation queue is not enabled. Skipping enqueue for item {ItemId}", publishedItem.ItemId);
                var itemAssetChanges = await _deltaCalculationService.CalculateDeltaAsync(publishedItem, publishedItem.AssetIds, cancellationToken);
                await _publishPushToDamEventsService.PublishPushToDamEventsAsync(itemAssetChanges, cancellationToken);
            }
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Failed process delta calculation for item {ItemId}", publishedItem.ItemId);
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
    public PublishedItem AddPublicLinkAssetIdsToPublishedItem(PublishedItem publishedItem, List<int> assetIdsFromPublicLinks, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(publishedItem);
        try
        {
            var allAssetIds = publishedItem.AssetIds.Concat(assetIdsFromPublicLinks).Distinct().ToList();
            publishedItem.AssetIds = allAssetIds;
            return publishedItem;
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Failed to retrieve total asset IDs for item {ItemId}", publishedItem.ItemId);
            throw;
        }
    }

    private async Task<bool> QueueExistAsync(string queueName)
    {
        return await _serviceBusConfigService.IsConnectionValidAsync()
            && await _serviceBusConfigService.DoesQueueExistAsync(queueName);
    }
}
