using AssetUsageService.Business.Controllers;
using AssetUsageService.Domain.Models;
using AssetUsageService.Integration.Mappers;
using Azure.Messaging.ServiceBus;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System;
using System.Text.Json;
using System.Threading.Tasks;


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
            _logger.LogInformation("Processing message with MessageId: {MessageId}", message.MessageId);

            var messageBody = message.Body.ToString();
            var publishedItemMessage = JsonSerializer.Deserialize<PublishedItem>(messageBody);

            if (publishedItemMessage == null)
            {
                _logger.LogError("Failed to deserialize message body to PublishedItem");
                await messageActions.DeadLetterMessageAsync(message);
                return;
            }

            var allAssetIds = await _assetItemController.GetAssetIdsFromPublicLinkAsync(publishedItemMessage, cancellationToken);
            var publishedItem = await _assetItemController.AddPublicLinkAssetIdsToPublishedItem(publishedItemMessage, allAssetIds, cancellationToken);

            _logger.LogInformation("Successfully processed message with all asset IDs {ids}", publishedItem.AssetIds);
            await _assetItemController.EnqueueDeltaCalculationAsync(publishedItem, cancellationToken);

            await messageActions.CompleteMessageAsync(message, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("Operation was cancelled for message {MessageId}", message.MessageId);
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing message {MessageId}. DeliveryCount: {DeliveryCount}", message.MessageId, message.DeliveryCount);
            throw;
        }
    }
}