using AssetUsageService.Business.Events.interfaces;
using AssetUsageService.Business.Handlers.interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace AssetUsageService.Business.Events;

public sealed class InMemoryMediater : IMediater
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<InMemoryMediater> _logger;

    public InMemoryMediater(IServiceProvider serviceProvider, ILogger<InMemoryMediater> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    public async Task PublishAsync<TEvent>(TEvent @event, CancellationToken cancellationToken = default) where TEvent : class
    {
        ArgumentNullException.ThrowIfNull(@event);

        _logger.LogDebug("Publishing event {EventType}", typeof(TEvent).Name);

        var handlerType = typeof(IEventHandler<>).MakeGenericType(typeof(TEvent));
        var handlers = _serviceProvider.GetServices(handlerType);

        foreach (var handler in handlers)
        {
            var handleMethod = handlerType.GetMethod("HandleAsync");
            if (handleMethod != null)
            {
                var task = (Task?)handleMethod.Invoke(handler, new object[] { @event, cancellationToken });
                if (task != null)
                {
                    await task;
                }
            }
        }
    }
}