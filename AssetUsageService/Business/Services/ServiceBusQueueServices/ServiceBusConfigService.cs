using Azure.Messaging.ServiceBus;
using Azure.Messaging.ServiceBus.Administration;
using Microsoft.Extensions.Configuration;
using MongoDB.Driver.Core.Configuration;

namespace AssetUsageService.Business.Services.ServiceBusQueueServices;

public class ServiceBusConfigService : IServiceBusConfigService
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

    public async Task<bool> IsConnectionValidAsync()
    {
        try
        {
            await using var client = new ServiceBusClient(ConnectionString);
            return true;
        }
        catch (Exception ex)
        {
            return false;
        }
    }
  
    public async Task<bool> DoesQueueExistAsync(string queueName)
    {
        try
        {
            await using var client = new ServiceBusClient(ConnectionString);
            await using var sender = client.CreateSender(queueName);
            return true;
        }
        catch (ServiceBusException ex) when (ex.Reason == ServiceBusFailureReason.MessagingEntityNotFound)
        {
            return false;
        }
        catch (Exception ex)
        {
            return false;
        }
    }
    public ServiceBusClient GetServiceBusClient()
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

