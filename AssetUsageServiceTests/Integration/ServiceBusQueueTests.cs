using AssetUsageService.Business.Services.ServiceBusQueueServices;
using AssetUsageService.Domain.Models;
using Azure.Messaging.ServiceBus;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit.Abstractions;

namespace AssetUsageServiceTests.Integration;

[Trait("Category", "Integration")]
[Collection("ServiceBus")]
public class ServiceBusQueueTests : IAsyncLifetime
{
    private readonly ITestOutputHelper _output;
    private readonly Mock<ILogger<ServiceBusQueueService>> _mockQueueLogger;
    private readonly Mock<ILogger<ServiceBusConfigService>> _mockConfigLogger;
    private readonly ServiceBusConfigService _configService;
    private readonly IConfiguration _configuration;
    private ServiceBusQueueService _queueService;
    private ServiceBusClient _testClient;
    private const string TestQueueName = "test-queue";

    public ServiceBusQueueTests(ITestOutputHelper output)
    {
        _output = output;
        _mockQueueLogger = new Mock<ILogger<ServiceBusQueueService>>();
        _mockConfigLogger = new Mock<ILogger<ServiceBusConfigService>>();

        _configuration = new ConfigurationBuilder()
            .AddJsonFile("appsettings.Test.json", optional: true, reloadOnChange: false)
            .AddEnvironmentVariables("SERVICEBUS_")
            .Build();

        _configService = new ServiceBusConfigService(_configuration, _mockConfigLogger.Object);
    }

    public async Task InitializeAsync()
    {
        if (string.IsNullOrEmpty(_configService.ConnectionString))
        {
            _output.WriteLine("ServiceBus connection string not configured. Skipping integration tests.");
            return;
        }

        _queueService = new ServiceBusQueueService(_configService, _mockQueueLogger.Object);
        _testClient = _configService.GetServiceBusClient();

        await CreateTestQueueIfNotExists();
    }

    public async Task DisposeAsync()
    {
        if (_queueService != null)
        {
            await _queueService.DisposeAsync();
        }

        if (_testClient != null)
        {
            await _testClient.DisposeAsync();
        }
    }

    #region Send Message Tests

    [Fact]
    public async Task SendMessageAsync_ValidMessage_ShouldSucceed()
    {
        // Arrange
        var publishedItem = CreateTestPublishedItem();

        // Act
        await _queueService.SendMessageAsync(TestQueueName, publishedItem, CancellationToken.None);

        // Assert
        _mockQueueLogger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Message sent to queue")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);

        // Verify message was sent
        var receiver = _testClient.CreateReceiver(TestQueueName);
        var receivedMessage = await receiver.ReceiveMessageAsync(TimeSpan.FromSeconds(5));
        Assert.NotNull(receivedMessage);
        await receiver.CompleteMessageAsync(receivedMessage);
        await receiver.DisposeAsync();
    }

    [Fact]
    public async Task SendMessageAsync_NullMessage_ShouldThrowArgumentNullException()
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(
            async () => await _queueService.SendMessageAsync<PublishedItem>(TestQueueName, null!, CancellationToken.None));
    }

    [Fact]
    public async Task SendMessageAsync_EmptyQueueName_ShouldThrowArgumentException()
    {
        // Arrange
        var publishedItem = CreateTestPublishedItem();

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(
            async () => await _queueService.SendMessageAsync(string.Empty, publishedItem, CancellationToken.None));
    }

    [Fact]
    public async Task SendMessageAsync_InvalidQueueName_ShouldThrowAndLog()
    {
        // Arrange
        var publishedItem = CreateTestPublishedItem();
        const string invalidQueueName = "non-existent-queue-xyz";

        // Act & Assert
        await Assert.ThrowsAsync<ServiceBusException>(
            async () => await _queueService.SendMessageAsync(invalidQueueName, publishedItem, CancellationToken.None));

        _mockQueueLogger.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Failed to send message to queue")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    #endregion

    #region Cancellation Tests

    [Fact]
    public async Task SendMessageAsync_WithCancellationToken_ShouldRespectCancellation()
    {
        // Arrange
        var publishedItem = CreateTestPublishedItem();
        var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act & Assert
        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            async () => await _queueService.SendMessageAsync(TestQueueName, publishedItem, cts.Token));
    }

    #endregion

    #region Helper Methods

    private async Task CreateTestQueueIfNotExists()
    {
        try
        {
            var sender = _testClient.CreateSender(TestQueueName);
            await sender.DisposeAsync();
        }
        catch (ServiceBusException)
        {
            _output.WriteLine($"Test queue {TestQueueName} does not exist. Please create it before running tests.");
            throw;
        }
    }

    private PublishedItem CreateTestPublishedItem()
    {
        return PublishedItem.Create(
            itemId: Guid.NewGuid(),
            language: "en",
            itemName: "Test Item",
            version: 1,
            assetIds: new List<int> { 1, 2, 3, 4, 5 },
            publicLinks: new List<string> { "https://example.com/link1", "https://example.com/link2" }
        );
    }

    #endregion
}