using Azure.Messaging.ServiceBus;
using Microsoft.Extensions.Configuration;

namespace AssetUsageService.Business.Services.ServiceBusQueueServices;

public class ServiceBusConfigService
{
    private readonly IConfiguration _configuration;

    public ServiceBusConfigService(IConfiguration configuration)
    {
        _configuration = configuration;
    }
    public bool IsPublicLinkQueueEnabled => !string.IsNullOrEmpty(_configuration["ServiceBusQueue:PublicLinkQueueName"]);
    public bool IsDeltaCalculationQueueEnabled => !string.IsNullOrEmpty(_configuration["ServiceBusQueue:DeltaCalculationQueueName"]);
    public string? ConnectionString => _configuration["ServiceBusQueue:ConnectionString"] ;
    
    public string? DeltaCalculationQueueName => _configuration["ServiceBusQueue:DeltaCalculationQueueName"];
    
    public string? PublicLinkQueueName => _configuration["ServiceBusQueue:PublicLinkQueueName"];
    
    public  ServiceBusClient GetServiceBusClient()
    {
        try
        {
            var clientOptions =  SetClientOptions();
            return new ServiceBusClient(ConnectionString, clientOptions);
        }
        catch (Exception ex)
        {
            throw;
        }
    }
    private  ServiceBusClientOptions SetClientOptions()
    {
        return new ServiceBusClientOptions
        {
            RetryOptions = new ServiceBusRetryOptions
            {
                Mode = ServiceBusRetryMode.Exponential,
                Delay = TimeSpan.FromSeconds(2),
                MaxDelay = TimeSpan.FromSeconds(30),
                MaxRetries = 5,
                TryTimeout = TimeSpan.FromSeconds(60)
            }
        };
    }
}

