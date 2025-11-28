using AssetUsageService.Business.Controllers;
using AssetUsageService.Domain.Models;
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
    private readonly IConfiguration _configuration;
    private readonly AssetItemController _assetItemController;

    public PublicLinkQueueFunction(ILogger<PublicLinkQueueFunction> logger, AssetItemController assetItemController, IConfiguration configuration)
    {
        _logger = logger;
        _assetItemController = assetItemController;
        _configuration = configuration;
    }

    [Function(nameof(PublicLinkQueueFunction))]
    public async Task Run(
        [ServiceBusTrigger("%ServiceBusQueue:PublicLinkQueueName%", Connection = "ServiceBusQueue:ConnectionString")]
        ServiceBusReceivedMessage message,
        ServiceBusMessageActions messageActions,
        CancellationToken cancellationToken = default)
    {
        //messageActions.DeadLetterMessageAsync(message).GetAwaiter().GetResult();
        //_logger.LogInformation("Message ID: {id}", message.MessageId);
        //var test = message;
        //var rer = messageActions;
        //_logger.LogInformation("Message ID: {id}", test);
        var messageBody = message.Body.ToString();
        _logger.LogInformation("Processing message ID: {id} with body: {body}", message.MessageId, messageBody);
        var publishedItem = JsonSerializer.Deserialize<PublishedItem>(
               message.Body.ToString(),
               new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        var allAssetIds = await _assetItemController.GetAssetIdsFromPublicLinkAsync(publishedItem, cancellationToken);
        _assetItemController.AddPublicLinkAssetIdsToPublishedItem(publishedItem, allAssetIds, cancellationToken).GetAwaiter().GetResult();

        // Complete the message
        await messageActions.CompleteMessageAsync(message);
        _logger.LogInformation("Message completed successfully.");
    }
}