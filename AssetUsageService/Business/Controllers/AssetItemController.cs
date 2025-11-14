using AssetUsageService.Business.Services;
using AssetUsageService.Domain.Models;
using Microsoft.Extensions.Logging;

namespace AssetUsageService.Business.Controllers;

public class AssetItemController
{
    private readonly PublishAssetIdsByPublicLinksEventService _publishGetAssetIdsByPublicLinksEventService;
    private readonly DeltaCalculationService _deltaCalculationService;
    private readonly PublishPushToDamEventsService _publishPushToDamEventsService;
    private readonly ILogger<AssetItemController> _logger;

    public AssetItemController(PublishAssetIdsByPublicLinksEventService publishGetAssetIdsByPublicLinksEventService, DeltaCalculationService deltaCalculationService, PublishPushToDamEventsService publishPushToDamEventsService, ILogger<AssetItemController> logger)
    {
        _publishGetAssetIdsByPublicLinksEventService = publishGetAssetIdsByPublicLinksEventService;
        _deltaCalculationService = deltaCalculationService;
        _publishPushToDamEventsService = publishPushToDamEventsService;
        _logger = logger;
    }

    public async Task ProcessPublishedItemAsync(PublishedItem publishedItem, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(publishedItem);

        try
        {
            var AssetIdsFromPublicLinks = await _publishGetAssetIdsByPublicLinksEventService.GetAssetIdsByPublicLinksAsync(publishedItem, cancellationToken);

            var allAssetIds = publishedItem.AssetIds.Concat(AssetIdsFromPublicLinks).Distinct().ToList();

            var itemAssetChanges = await _deltaCalculationService.CalculateDeltaAsync(publishedItem, allAssetIds, cancellationToken);

            await _publishPushToDamEventsService.PublishPushToDamEventsAsync(itemAssetChanges, cancellationToken);

            _logger.LogInformation("Successfully processed item {ItemId} with {AssetCount} assets", 
                publishedItem.ItemId, allAssetIds.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to process published item {ItemId}", publishedItem.ItemId);
            throw;
        }
    }
}
