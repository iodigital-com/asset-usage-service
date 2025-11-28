using Azure.Messaging.ServiceBus;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using MongoDB.Driver.Core.Configuration;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AssetUsageService.Business.Services;

public class TestQueueService
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<TestQueueService> _logger;

    public TestQueueService(IConfiguration configuration, ILogger<TestQueueService> logger) 
    {
        _configuration = configuration;
        _logger = logger;
    }
    public async Task testQueue()
    {
        var connectionString = _configuration.GetValue<string>("ServiceBusQueue:ConnectionString");
        var queueName = _configuration.GetValue<string>("ServiceBusQueue:PublicLinkQueueName");
        await using var client = new ServiceBusClient(connectionString);
        ServiceBusProcessor processor = client.CreateProcessor(queueName, new ServiceBusProcessorOptions());

        // Test 1: Send message
        _logger.LogInformation("Sending test message...");
        await using var sender = client.CreateSender(queueName);
        await sender.SendMessageAsync(new ServiceBusMessage(), CancellationToken.None);
        _logger.LogInformation("Message sent!");

        // Test 2: Receive message
        //_logger.LogInformation("Receiving message...");
        //await using var receiver = client.CreateReceiver(queueName);
        //var message = await receiver.ReceiveMessageAsync(TimeSpan.FromSeconds(5));

        //if (message != null)
        //{
        //    _logger.LogInformation($"Message received: {message.Body}");
        //    await receiver.CompleteMessageAsync(message);
        //    _logger.LogInformation("Message completed!");
        //}
        //else
        //{
        //    _logger.LogInformation("No message received");
        //}

        //_logger.LogInformation("Test completed!");
    }
}

