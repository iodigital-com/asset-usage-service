using AssetUsageService.Business.Events;
using AssetUsageService.Business.Events.interfaces;
using AssetUsageService.Business.Handlers;
using AssetUsageService.Business.Services;
using AssetUsageService.Domain.Models;
using AssetUsageService.Integration;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
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
