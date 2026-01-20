using AssetUsageService.Business.Controllers;
using AssetUsageService.Domain.Models;
using Azure.Messaging.ServiceBus;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace AssetUsageService.Functions.QueueFunctions;

public class PublicLinkQueueFunction
{
    private readonly ILogger<PublicLinkQueueFunction> _logger;
    private readonly AssetItemController _assetItemController;

    public PublicLinkQueueFunction(ILogger<PublicLinkQueueFunction> logger, AssetItemController assetItemController)
    {
        _logger = logger;
        _assetItemController = assetItemController;
    }

    [Function(nameof(PublicLinkQueueFunction))]
    public async Task Run(
        [ServiceBusTrigger("%ServiceBusQueue:PublicLinkQueueName%", Connection = "ServiceBusQueue:ConnectionString")]
        ServiceBusReceivedMessage message,
        ServiceBusMessageActions messageActions,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var messageBody = message.Body.ToString();
            var publishedItemMessage = JsonSerializer.Deserialize<PublishedItem>(messageBody);

            if (publishedItemMessage == null)
            {
                _logger.LogError("Failed to deserialize message body to PublishedItem");
                await messageActions.DeadLetterMessageAsync(message);
                return;
            }

            var allAssetIds = await _assetItemController.GetAssetIdsFromPublicLinkAsync(publishedItemMessage, cancellationToken);
            var publishedItem = _assetItemController.AddPublicLinkAssetIdsToPublishedItem(publishedItemMessage, allAssetIds, cancellationToken);

            await _assetItemController.EnqueueDeltaCalculationAsync(publishedItem, cancellationToken);

            await messageActions.CompleteMessageAsync(message, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("Operation was cancelled for message {MessageId}", message.MessageId);
            throw;
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Error processing message {MessageId}. DeliveryCount: {DeliveryCount}", message.MessageId, message.DeliveryCount);
            throw;
        }
    }
}