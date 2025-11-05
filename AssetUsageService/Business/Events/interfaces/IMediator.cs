namespace AssetUsageService.Business.Events.interfaces;

public interface IMediator
{
    Task PublishAsync<TEvent>(TEvent @event, CancellationToken cancellationToken = default) where TEvent : class;
}
