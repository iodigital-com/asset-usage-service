namespace AssetUsageService.Business.Events.interfaces;

public interface IMediater
{
    Task PublishAsync<TEvent>(TEvent @event, CancellationToken cancellationToken = default) where TEvent : class;
}
