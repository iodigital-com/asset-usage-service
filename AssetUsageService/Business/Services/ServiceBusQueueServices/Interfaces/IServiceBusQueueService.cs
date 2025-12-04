namespace AssetUsageService.Business.Services.ServiceBusQueueServices.Interfaces;

public interface IServiceBusQueueService
{
    Task SendMessageAsync<T>(string queueName, T message, CancellationToken cancellationToken = default) where T : class;
}