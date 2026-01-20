using Azure.Messaging.ServiceBus;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace AssetUsageService.Functions.QueueFunctions;

public class DeadLetterQueueMonitor
{
    private readonly ILogger<DeadLetterQueueMonitor> _logger;

    public DeadLetterQueueMonitor(ILogger<DeadLetterQueueMonitor> logger)
    {
        _logger = logger;
    }

    [Function(nameof(MonitorPublicLinkDeadLetterQueue))]
    public void MonitorPublicLinkDeadLetterQueue(
        [ServiceBusTrigger("%ServiceBusQueue:PublicLinkQueueName%/$deadletterqueue", Connection = "ServiceBusQueue:ConnectionString")]
        ServiceBusReceivedMessage message)
    {
        ProcessDeadLetterAsync("PublicLink", message);
    }

    [Function(nameof(MonitorPushToDAMDeadLetterQueue))]
    public void MonitorPushToDAMDeadLetterQueue(
        [ServiceBusTrigger("%ServiceBusQueue:PushToDAMQueueName%/$deadletterqueue", Connection = "ServiceBusQueue:ConnectionString")]
        ServiceBusReceivedMessage message)
    {
        ProcessDeadLetterAsync("PushToDAM", message);
    }

    private void ProcessDeadLetterAsync(
        string queueType,
        ServiceBusReceivedMessage message)
    {
        try
        {
            var messageType = message.ApplicationProperties.TryGetValue("MessageType", out var type)
                ? type?.ToString()
                : "Unknown";

            _logger.LogCritical(
                "DEAD LETTER: {QueueType} Queue | " +
                "MessageId: {MessageId} | " +
                "Type: {MessageType} | " +
                "Reason: {Reason} | " +
                "Error: {Error} | " +
                "DeliveryCount: {DeliveryCount} | " +
                "Body: {Body}",
                queueType,
                message.MessageId,
                messageType,
                message.DeadLetterReason ?? "N/A",
                message.DeadLetterErrorDescription ?? "N/A",
                message.DeliveryCount,
                message.Body.ToString());
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Failed to process dead letter message {MessageId}", message.MessageId);
        }
    }
}