using AssetUsageService.Business.Services;
using AssetUsageService.Domain.Models;

namespace AssetUsageService.Business.Controllers;

public class AssetItemController
{
    private readonly DeltaCalculationService _deltaCalculationService;
    private readonly PublishPushToDamEventsService _publishPushToDamEventsService;

    public AssetItemController(DeltaCalculationService deltaCalculationService, PublishPushToDamEventsService publishPushToDamEventsService)
    {
        _deltaCalculationService = deltaCalculationService;
        _publishPushToDamEventsService = publishPushToDamEventsService;
    }

    public async Task ProcessPublishedItemAsync(PublishedItem publishedItem, CancellationToken cancellationToken)
    {
        ItemAssetChanges itemAssetChanges = await _deltaCalculationService.CalculateDeltaAsync(publishedItem, publishedItem.AssetIds, cancellationToken);
        await _publishPushToDamEventsService.PublishPushToDamEventsAsync(itemAssetChanges, cancellationToken);
    }
}
