using AssetUsageService.Business.Services.ServiceBusQueueServices;
using AssetUsageService.Domain.Models;
using Azure.Messaging.ServiceBus;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using System.Diagnostics;
using Xunit.Abstractions;

namespace AssetUsageServiceTests.Performance;

[Trait("Category", "Performance")]
[Collection("ServiceBus")]
public class ServiceBusPerformanceTests : IAsyncLifetime
{
    private const string TestQueueName = "test-queue";
    private const int MaxSingleMessageTimeMs = 100;
    private const int MessageCount = 1000;
    private const int MaxSequentialTimeMs = 500000;
    private const int LatencySampleSize = 100;
    private const int MemoryTestIterations = 500;
    private const double MinSuccessRate = 0.99;
    private const int MaxMemoryIncreaseMb = 50;
    
    private readonly ITestOutputHelper _output;
    private readonly Mock<ILogger<ServiceBusQueueService>> _mockLogger;
    private readonly IServiceBusConfigService _configService;
    private readonly IConfiguration _configuration;
    private IServiceBusQueueService _queueService;
    private ServiceBusClient _testClient;

    public ServiceBusPerformanceTests(ITestOutputHelper output)
    {
        _output = output;
        _mockLogger = new Mock<ILogger<ServiceBusQueueService>>();

        _configuration = new ConfigurationBuilder()
            .AddJsonFile("appsettings.Test.json", optional: true, reloadOnChange: false)
            .AddEnvironmentVariables("SERVICEBUS_")
            .Build();

        _configService = new ServiceBusConfigService(_configuration);
    }

    public async Task InitializeAsync()
    {
        if (string.IsNullOrEmpty(_configService.ConnectionString))
        {
            _output.WriteLine("ServiceBus connection string not configured. Skipping performance tests.");
            return;
        }

        _queueService = new ServiceBusQueueService(_configService, _mockLogger.Object);

        _testClient = _configService.GetServiceBusClient();

        await VerifyQueueExists();
    }

    public async Task DisposeAsync()
    {
        if (_queueService is IAsyncDisposable asyncDisposable)
        {
            await asyncDisposable.DisposeAsync();
        }

        if (_testClient != null)
        {
            await _testClient.DisposeAsync();
        }
    }

    #region Single Message Performance

    [Fact]
    public async Task SendMessageAsync_SingleLargeMessage_ShouldCompleteWithinThreshold()
    {
        if (string.IsNullOrEmpty(_configService.ConnectionString))
        {
            _output.WriteLine("Test skipped: No connection string configured");
            return;
        }

        // Arrange
        var publishedItem = CreateTestPublishedItem(assetCount: 1000);
        var stopwatch = Stopwatch.StartNew();

        // Act - Via IServiceBusQueueService
        await _queueService.SendMessageAsync(TestQueueName, publishedItem, CancellationToken.None);

        // Assert
        stopwatch.Stop();
        var elapsedMs = stopwatch.ElapsedMilliseconds;

        _output.WriteLine($"Single large message (1000 assets): {elapsedMs}ms (threshold: {MaxSingleMessageTimeMs}ms)");
        Assert.True(elapsedMs < MaxSingleMessageTimeMs,
            $"Send took {elapsedMs}ms, expected < {MaxSingleMessageTimeMs}ms");

        await CleanupQueue(1);
    }

    #endregion

    #region High Volume Performance Tests

    [Fact]
    public async Task SendMessageAsync_10000MessagesSequential_ShouldMeetPerformance()
    {
        if (string.IsNullOrEmpty(_configService.ConnectionString))
        {
            _output.WriteLine("Test skipped: No connection string configured");
            return;
        }

        // Arrange
        var stopwatch = Stopwatch.StartNew();
        var successCount = 0;
        var errors = new List<Exception>();

        // Act
        for (int i = 0; i < MessageCount; i++)
        {
            try
            {
                var publishedItem = CreateTestPublishedItem(assetCount: 10);
                await _queueService.SendMessageAsync(TestQueueName, publishedItem, CancellationToken.None);
                successCount++;
            }
            catch (Exception ex)
            {
                errors.Add(ex);
            }

            if ((i + 1) % 100 == 0)
            {
                _output.WriteLine($"Progress: {i + 1}/{MessageCount} messages sent");
            }
        }

        // Assert
        stopwatch.Stop();
        var elapsedMs = stopwatch.ElapsedMilliseconds;
        var throughput = successCount / (elapsedMs / 1000.0);
        var avgTimePerMessage = elapsedMs / (double)successCount;

        _output.WriteLine($"\n=== Sequential Performance Test Results ===");
        _output.WriteLine($"Total messages: {MessageCount}");
        _output.WriteLine($"Successful: {successCount}");
        _output.WriteLine($"Failed: {errors.Count}");
        _output.WriteLine($"Total time: {elapsedMs}ms ({elapsedMs / 1000.0:F1}s)");
        _output.WriteLine($"Average time per message: {avgTimePerMessage:F2}ms");
        _output.WriteLine($"Throughput: {throughput:F2} messages/sec");

        Assert.True(elapsedMs < MaxSequentialTimeMs,
            $"Sequential send took {elapsedMs}ms, expected < {MaxSequentialTimeMs}ms");
        Assert.True(successCount >= MessageCount * MinSuccessRate,
            $"Expected at least 99% success rate, got {successCount}/{MessageCount}");

        await CleanupQueue(successCount);
    }

    #endregion

    #region Latency Tests

    [Fact]
    public async Task SendMessageAsync_Latency_ShouldMeetThresholds()
    {
        if (string.IsNullOrEmpty(_configService.ConnectionString))
        {
            _output.WriteLine("Test skipped: No connection string configured");
            return;
        }

        // Arrange
        var latencies = new List<long>();

        // Act
        for (int i = 0; i < 5; i++)
        {
            var warmupItem = CreateTestPublishedItem(assetCount: 10);
            await _queueService.SendMessageAsync(TestQueueName, warmupItem, CancellationToken.None);
        }
        await CleanupQueue(5);

        for (int i = 0; i < LatencySampleSize; i++)
        {
            var publishedItem = CreateTestPublishedItem(assetCount: 10);
            var sw = Stopwatch.StartNew();

            await _queueService.SendMessageAsync(TestQueueName, publishedItem, CancellationToken.None);

            sw.Stop();
            latencies.Add(sw.ElapsedMilliseconds);
        }

        // Assert
        var sortedLatencies = latencies.OrderBy(l => l).ToList();
        var p50 = sortedLatencies[LatencySampleSize / 2];
        var p95 = sortedLatencies[(int)(LatencySampleSize * 0.95)];
        var p99 = sortedLatencies[(int)(LatencySampleSize * 0.99)];
        var avg = latencies.Average();

        _output.WriteLine($"Latency percentiles (n={LatencySampleSize}):");
        _output.WriteLine($"P50 = percent of messages < {p50}ms");
        _output.WriteLine($"  Average: {avg:F2}ms");
        _output.WriteLine($"  P50: {p50}ms");
        _output.WriteLine($"  P95: {p95}ms");
        _output.WriteLine($"  P99: {p99}ms");
        _output.WriteLine($"  Min: {sortedLatencies.First()}ms");
        _output.WriteLine($"  Max: {sortedLatencies.Last()}ms");

        Assert.True(p50 < 50, $"P50 latency {p50}ms exceeds 50ms");
        Assert.True(p95 < 100, $"P95 latency {p95}ms exceeds 100ms");
        Assert.True(p99 < 200, $"P99 latency {p99}ms exceeds 200ms");

        await CleanupQueue(LatencySampleSize);
    }

    #endregion

    #region Memory and Resource Tests

    [Fact]
    public async Task SendMessageAsync_RepeatedCalls_ShouldNotLeakMemory()
    {
        if (string.IsNullOrEmpty(_configService.ConnectionString))
        {
            _output.WriteLine("Test skipped: No connection string configured");
            return;
        }

        // Arrange
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        var initialMemory = GC.GetTotalMemory(true);

        // Act
        for (int i = 0; i < MemoryTestIterations; i++)
        {
            var publishedItem = CreateTestPublishedItem(assetCount: 20);
            await _queueService.SendMessageAsync(TestQueueName, publishedItem, CancellationToken.None);
        }

        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        var finalMemory = GC.GetTotalMemory(false);
        var memoryIncreaseMb = (finalMemory - initialMemory) / 1024.0 / 1024.0;

        // Assert
        _output.WriteLine($"Memory increase after {MemoryTestIterations} iterations: {memoryIncreaseMb:F2} MB");
        _output.WriteLine($"Initial memory: {initialMemory / 1024.0 / 1024.0:F2} MB");
        _output.WriteLine($"Final memory: {finalMemory / 1024.0 / 1024.0:F2} MB");

        Assert.True(memoryIncreaseMb < MaxMemoryIncreaseMb,
            $"Memory increased by {memoryIncreaseMb:F2} MB, potential memory leak");

        await CleanupQueue(MemoryTestIterations);
    }

    #endregion


    #region Helper Methods

    private async Task VerifyQueueExists()
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

    private async Task CleanupQueue(int expectedMessageCount, int maxMessages = 100)
    {
        try
        {
            var receiver = _testClient.CreateReceiver(TestQueueName);
            var totalReceived = 0;

            while (totalReceived < expectedMessageCount)
            {
                var batchSize = Math.Min(maxMessages, expectedMessageCount - totalReceived);
                var messages = await receiver.ReceiveMessagesAsync(
                    maxMessages: batchSize,
                    maxWaitTime: TimeSpan.FromSeconds(2));

                if (!messages.Any())
                    break;

                foreach (var message in messages)
                {
                    await receiver.CompleteMessageAsync(message);
                    totalReceived++;
                }
            }

            await receiver.DisposeAsync();
            _output.WriteLine($"Cleanup: Received and completed {totalReceived} messages");
        }
        catch (Exception ex)
        {
            _output.WriteLine($"Cleanup warning: {ex.Message}");
        }
    }

    private PublishedItem CreateTestPublishedItem(int assetCount = 10)
    {
        return PublishedItem.Create(
            itemId: Guid.NewGuid(),
            language: "en",
            itemName: $"Perf Test Item {Guid.NewGuid()}",
            version: 1,
            assetIds: Enumerable.Range(1, assetCount).ToList(),
            publicLinks: new List<string> { "https://example.com/test" }
        );
    }

    #endregion
}