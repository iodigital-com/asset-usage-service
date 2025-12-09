using AssetUsageService.Business.Controllers;
using AssetUsageService.Business.Services;
using AssetUsageService.Domain.Models;
using Azure.Messaging.ServiceBus;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace AssetUsageService.Functions.QueueFunctions;

public class PushToDAMQueueFunction
{
    private readonly ILogger<PushToDAMQueueFunction> _logger;
    private readonly AssetItemController _assetItemController;

    public PushToDAMQueueFunction(ILogger<PushToDAMQueueFunction> logger, AssetItemController assetItemController)
    {
        _logger = logger;
        _assetItemController = assetItemController;
    }

    [Function(nameof(PushToDAMQueueFunction))]
    public async Task Run(
        [ServiceBusTrigger("%ServiceBusQueue:PushToDAMQueueName%", Connection = "ServiceBusQueue:ConnectionString")]
        ServiceBusReceivedMessage message,
        ServiceBusMessageActions messageActions,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var messageBody = message.Body.ToString();
            var itemAssetChangesMessage = JsonSerializer.Deserialize<ItemAssetChanges>(messageBody);

            if (itemAssetChangesMessage == null)
            {
                _logger.LogError("Failed to deserialize message body to PublishedItem");
                await messageActions.DeadLetterMessageAsync(message);
                return;
            }

            await _assetItemController.PushToDamAsync(itemAssetChangesMessage, cancellationToken);

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