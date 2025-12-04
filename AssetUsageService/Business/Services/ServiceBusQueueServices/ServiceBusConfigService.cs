using AssetUsageService.Business.Services.ServiceBusQueueServices.Interfaces;
using Azure.Messaging.ServiceBus;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System;

namespace AssetUsageService.Business.Services.ServiceBusQueueServices;

public class ServiceBusConfigService : IServiceBusConfigService
{
    private const string ServiceBusConnectionStringKey = "ServiceBusQueue:ConnectionString";
    private const string PublicLinkQueueNameKey = "ServiceBusQueue:PublicLinkQueueName";
    private const string DeltaCalculationQueueNameKey = "ServiceBusQueue:DeltaCalculationQueueName";

    private static readonly TimeSpan DefaultRetryDelay = TimeSpan.FromSeconds(2);
    private static readonly TimeSpan MaxRetryDelay = TimeSpan.FromSeconds(30);
    private const int MaxRetryAttempts = 5;
    private static readonly TimeSpan OperationTimeout = TimeSpan.FromSeconds(60);

    private readonly IConfiguration _configuration;
    private readonly ILogger<ServiceBusConfigService> _logger;

    public ServiceBusConfigService(IConfiguration configuration, ILogger<ServiceBusConfigService> logger)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(logger);

        _configuration = configuration;
        _logger = logger;
    }

    public bool IsPublicLinkQueueEnabled => !string.IsNullOrEmpty(_configuration[PublicLinkQueueNameKey]);

    public bool IsDeltaCalculationQueueEnabled => !string.IsNullOrEmpty(_configuration[DeltaCalculationQueueNameKey]);

    public string? ConnectionString => _configuration[ServiceBusConnectionStringKey];

    public string? DeltaCalculationQueueName => _configuration[DeltaCalculationQueueNameKey];

    public string? PublicLinkQueueName => _configuration[PublicLinkQueueNameKey];

    public async Task<bool> IsConnectionValidAsync()
    {
        if (string.IsNullOrEmpty(ConnectionString))
        {
            _logger.LogWarning("Service Bus connection string is not configured");
            return false;
        }

        try
        {
            await using var client = GetServiceBusClient();
            return true;
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Failed to validate Service Bus connection");
            return false;
        }
    }

    public async Task<bool> DoesQueueExistAsync(string queueName)
    {
        if (string.IsNullOrEmpty(queueName))
        {
            _logger.LogWarning("Queue name is null or empty");
            return false;
        }

        try
        {
            await using var client = new ServiceBusClient(ConnectionString);
            await using var receiver = client.CreateReceiver(queueName);
            await receiver.PeekMessageAsync();

            return true;
        }
        catch (ServiceBusException exception) when (exception.Reason == ServiceBusFailureReason.MessagingEntityNotFound)
        {
            _logger.LogWarning("Queue {QueueName} does not exist", queueName);
            return false;
        }
        catch (Exception exception)
        {
            _logger.LogWarning(exception, "Failed to check if queue {QueueName} exists", queueName);
            return false;
        }
    }

    public ServiceBusClient GetServiceBusClient()
    {
        try
        {
            var clientOptions = CreateClientOptions();
            return new ServiceBusClient(ConnectionString, clientOptions);
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Cannot create Service Bus client");
            throw;
        }
    }

    private static ServiceBusClientOptions CreateClientOptions()
    {
        return new ServiceBusClientOptions
        {
            RetryOptions = new ServiceBusRetryOptions
            {
                Mode = ServiceBusRetryMode.Exponential,
                Delay = DefaultRetryDelay,
                MaxDelay = MaxRetryDelay,
                MaxRetries = MaxRetryAttempts,
                TryTimeout = OperationTimeout
            }
        };
    }
}