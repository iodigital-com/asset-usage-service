using Azure.Messaging.ServiceBus;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace AssetUsageService.Business.Services.ServiceBusQueueServices;

public class ServiceBusQueueService : IServiceBusQueueService, IAsyncDisposable
{
    private readonly ServiceBusClient _client;
    private readonly ILogger<ServiceBusQueueService> _logger;

    public ServiceBusQueueService(ServiceBusConfigService configuration, ILogger<ServiceBusQueueService> logger)
    {            
        _client = configuration.GetServiceBusClient();
        _logger = logger;
    }
    public async Task<ServiceBusSender> CreateSender(string queueName)
    {
        await using var sender = _client.CreateSender(queueName);
        return sender;
    }
    public async Task SendMessageAsync<T>(string queueName, T message, CancellationToken cancellationToken = default) where T : class
    {
        ArgumentNullException.ThrowIfNull(message);
        ArgumentException.ThrowIfNullOrEmpty(queueName);

        try
        {
            await using var sender = _client.CreateSender(queueName);

            var messageBody = JsonSerializer.Serialize(message);
            var serviceBusMessage = new ServiceBusMessage(messageBody)
            {
                ContentType = "application/json",
                MessageId = Guid.NewGuid().ToString()
            };

            await sender.SendMessageAsync(serviceBusMessage, cancellationToken);

            _logger.LogInformation("Message sent to queue {QueueName} with MessageId {MessageId}",
                queueName, serviceBusMessage.MessageId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send message to queue {QueueName}", queueName);
            throw;
        }
    }

    public async ValueTask DisposeAsync()
    {
        await _client.DisposeAsync();
    }
}