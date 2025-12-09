using Azure.Messaging.ServiceBus;

namespace AssetUsageService.Business.Services.ServiceBusQueueServices.Interfaces;

public interface IServiceBusConfigService
{
    string? ConnectionString { get; }
    string? DeltaCalculationQueueName { get; }
    string? PublicLinkQueueName { get; }
    string? PushToDamQueueName { get; }

    Task<bool> IsConnectionValidAsync();
    Task<bool> DoesQueueExistAsync(string queueName);
    ServiceBusClient GetServiceBusClient();
}
