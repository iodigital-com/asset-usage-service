using AssetUsageService.Business.Services;
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
    private readonly ServiceBusQueueService _serviceBusQueueService;
    private readonly IConfiguration _configuration;


    public AssetItemController(PublishAssetIdsByPublicLinksEventService publishGetAssetIdsByPublicLinksEventService, DeltaCalculationService deltaCalculationService, PublishPushToDamEventsService publishPushToDamEventsService, ILogger<AssetItemController> logger, ServiceBusQueueService serviceBusQueueService, IConfiguration configuration)
    {
        _publishGetAssetIdsByPublicLinksEventService = publishGetAssetIdsByPublicLinksEventService;
        _deltaCalculationService = deltaCalculationService;
        _publishPushToDamEventsService = publishPushToDamEventsService;
        _logger = logger;
        _serviceBusQueueService = serviceBusQueueService;
        _configuration = configuration;

    }

    public async Task ProcessPublishedItemAsync(PublishedItem publishedItem, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(publishedItem);

        if (publishedItem.PublicLinks?.Count() == 0 || _configuration["ServiceBusQueue:PublicLinkQueueName"] is null)
        {
            _logger.LogInformation("Item {ItemId} has no public links, skipping to delta calculation", publishedItem.ItemId);
            // Geen public links? Direct naar delta berekening
            //await EnqueueDeltaCalculationAsync(publishedItem, new List<int>(), cancellationToken);
            return;
        } else 
        {
            var queueName = _configuration["ServiceBusQueue:PublicLinkQueueName"];
            await _serviceBusQueueService.SendMessageAsync(queueName!, publishedItem, cancellationToken);
        }
        //var AssetIdsFromPublicLinks = await GetAssetIdsFromPublicLinkAsync(publishedItem, cancellationToken);
        //var allAssetIds = await GetAllAssetIds(publishedItem, AssetIdsFromPublicLinks, cancellationToken);
        //try
        //{
        //    //var AssetIdsFromPublicLinks = await _publishGetAssetIdsByPublicLinksEventService.GetAssetIdsByPublicLinksAsync(publishedItem, cancellationToken);

        //    //var allAssetIds = publishedItem.AssetIds.Concat(AssetIdsFromPublicLinks).Distinct().ToList();

        //    var itemAssetChanges = await _deltaCalculationService.CalculateDeltaAsync(publishedItem, allAssetIds, cancellationToken);

        //    await _publishPushToDamEventsService.PublishPushToDamEventsAsync(itemAssetChanges, cancellationToken);

        //    _logger.LogInformation("Successfully processed item {ItemId} with {AssetCount} assets", 
        //        publishedItem.ItemId, allAssetIds.Count);
        //}
        //catch (Exception ex)
        //{
        //    _logger.LogError(ex, "Failed to process published item {ItemId}", publishedItem.ItemId);
        //    throw;
        //}
    }
    public async Task EnqueuePublicLinkProcessingAsync(PublishedItem publishedItem, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(publishedItem);
        try
        {
            var queueName = _configuration["ServiceBusQueue:PublicLinkQueueName"];
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
            var queueName = _configuration["ServiceBusQueue:DeltaCalculationQueueName"];
            await _serviceBusQueueService.SendMessageAsync(queueName!, publishedItem, cancellationToken);
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Failed to enqueue delta calculation for item {ItemId}", publishedItem.ItemId);
            throw;
        }
    }
    public async Task PublishPushToDamEventsAsync(ItemAssetChanges itemAssetChanges, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(itemAssetChanges);
        try
        {
            await _publishPushToDamEventsService.PublishPushToDamEventsAsync(itemAssetChanges, cancellationToken);
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Failed to publish push to DAM events for item {ItemId}", itemAssetChanges.Item.ItemId);
            throw;
        }
    }
    //public async Task ProcessDeltaCalculationAsync(PublishedItem publishedItem, List<int> AssetIdsFromPublicLinks, CancellationToken cancellationToken)
    //{
    //    ArgumentNullException.ThrowIfNull(publishedItem);
    //    try
    //    {
    //        var itemAssetChanges = await _deltaCalculationService.CalculateDeltaAsync(publishedItem, allAssetIds, cancellationToken);
    //        _logger.LogInformation("Successfully processed item {ItemId} with {AssetCount} assets",
    //            publishedItem.ItemId, allAssetIds.Count);
    //    }
    //    catch (Exception exception)
    //    {
    //        _logger.LogError(exception, "Failed to process published item {ItemId}", publishedItem.ItemId);
    //        throw;
    //    }
    //}

    public async Task<List<int>> GetAssetIdsFromPublicLinkAsync(PublishedItem publishedItem, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(publishedItem);
        try
        {
            return await _publishGetAssetIdsByPublicLinksEventService.GetAssetIdsByPublicLinksAsync(publishedItem, cancellationToken);
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
