using AssetUsageService.Business.Events;
using Microsoft.Extensions.Logging;
using AssetUsageService.Business.Events.interfaces;
using AssetUsageService.Domain.Models;

namespace AssetUsageService.Business.Services;

public class PublishPushToDamEventsService
{
    private readonly ILogger<PublishPushToDamEventsService> _logger;
    private readonly IMediator _mediator;

    public PublishPushToDamEventsService(ILogger<PublishPushToDamEventsService> logger, IMediator mediator)
    {
        _logger = logger;
        _mediator = mediator;
    }
    public async Task PublishPushToDamEventsAsync(ItemAssetChanges itemAssetChanges, CancellationToken cancellationToken)
    {
        if (itemAssetChanges.ToAddAssetIds?.Count > 0)
        {
            var addEvent = new PushToDamEvent
            {
                Item = itemAssetChanges.Item,
                AssetIds = itemAssetChanges.ToAddAssetIds,
                Operation = DamOperation.Add
            };

            _logger.LogInformation("Publishing PushToDamEvent (Add) for item {ItemId} with {Count} assets",
                itemAssetChanges.Item.ItemId, itemAssetChanges.ToAddAssetIds.Count);

            await _mediator.PublishAsync(addEvent, cancellationToken);
        }

        if (itemAssetChanges.ToRemoveAssetIds?.Count > 0)
        {
            var removeEvent = new PushToDamEvent
            {
                Item = itemAssetChanges.Item,
                AssetIds = itemAssetChanges.ToRemoveAssetIds,
                Operation = DamOperation.Remove
            };

            _logger.LogInformation("Publishing PushToDamEvent (Remove) for item {ItemId} with {Count} assets",
                itemAssetChanges.Item.ItemId, itemAssetChanges.ToRemoveAssetIds.Count);

            await _mediator.PublishAsync(removeEvent, cancellationToken);
        }
    }
}