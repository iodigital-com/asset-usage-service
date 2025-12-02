using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AssetUsageService.Business.Services.ServiceBusQueueServices;

public interface IServiceBusQueueService
{
    Task SendMessageAsync<T>(string queueName, T message, CancellationToken cancellationToken = default) where T : class;
}