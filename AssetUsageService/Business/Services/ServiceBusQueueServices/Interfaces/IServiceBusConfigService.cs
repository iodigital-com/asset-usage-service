using Azure.Messaging.ServiceBus;

namespace AssetUsageService.Business.Services.ServiceBusQueueServices.Interfaces;

public interface IServiceBusConfigService
{
    bool IsPublicLinkQueueEnabled { get; }
    bool IsDeltaCalculationQueueEnabled { get; }
    string? ConnectionString { get; }
    string? DeltaCalculationQueueName { get; }
    string? PublicLinkQueueName { get; }
  
    Task<bool> IsConnectionValidAsync();
    Task<bool> DoesQueueExistAsync(string queueName);
    ServiceBusClient GetServiceBusClient();
}
