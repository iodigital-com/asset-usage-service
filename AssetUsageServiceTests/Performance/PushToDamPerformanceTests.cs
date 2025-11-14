using AssetUsageService.Business.Events;
using AssetUsageService.Business.Events.interfaces;
using AssetUsageService.Business.Services;
using AssetUsageService.Domain.Models;
using Microsoft.Extensions.Logging;
using Moq;
using System.Diagnostics;
using Xunit.Abstractions;

namespace AssetUsageServiceTests.Performance;

[Trait("Category", "Performance")]
public class PushToDamPerformanceTests
{
    private readonly ITestOutputHelper _output;
    private readonly Mock<ILogger<PublishPushToDamEventsService>> _mockLogger;
    private readonly Mock<IMediator> _mockMediator;
    private readonly PublishPushToDamEventsService _service;

    private const int MaxPublishTimeMs = 50;
    private const int MaxBulkPublishTimeMs = 500;

    public PushToDamPerformanceTests(ITestOutputHelper output)
    {
        _output = output;
        _mockLogger = new Mock<ILogger<PublishPushToDamEventsService>>();
        _mockMediator = new Mock<IMediator>();
        
        _mockMediator
            .Setup(m => m.PublishAsync(It.IsAny<PushToDamEvent>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        
        _service = new PublishPushToDamEventsService(_mockLogger.Object, _mockMediator.Object);
    }

    #region Single Event Performance Tests

    [Fact]
    public async Task PublishPushToDamEventsAsync_SingleAddEvent_ShouldCompleteQuickly()
    {
        // Arrange
        var itemAssetChanges = CreateItemAssetChanges(toAdd: 10, toRemove: 0);
        var stopwatch = Stopwatch.StartNew();

        // Act
        await _service.PublishPushToDamEventsAsync(itemAssetChanges, CancellationToken.None);

        // Assert
        stopwatch.Stop();
        var elapsedMs = stopwatch.ElapsedMilliseconds;
        
        _output.WriteLine($"Single add event (10 assets): {elapsedMs}ms (threshold: {MaxPublishTimeMs}ms)");
        Assert.True(elapsedMs < MaxPublishTimeMs,
            $"Publish took {elapsedMs}ms, expected < {MaxPublishTimeMs}ms");
    }

    [Fact]
    public async Task PublishPushToDamEventsAsync_SingleRemoveEvent_ShouldCompleteQuickly()
    {
        // Arrange
        var itemAssetChanges = CreateItemAssetChanges(toAdd: 0, toRemove: 10);
        var stopwatch = Stopwatch.StartNew();

        // Act
        await _service.PublishPushToDamEventsAsync(itemAssetChanges, CancellationToken.None);

        // Assert
        stopwatch.Stop();
        var elapsedMs = stopwatch.ElapsedMilliseconds;
        
        _output.WriteLine($"Single remove event (10 assets): {elapsedMs}ms (threshold: {MaxPublishTimeMs}ms)");
        Assert.True(elapsedMs < MaxPublishTimeMs,
            $"Publish took {elapsedMs}ms, expected < {MaxPublishTimeMs}ms");
    }

    [Fact]
    public async Task PublishPushToDamEventsAsync_MixedEvents_ShouldCompleteQuickly()
    {
        // Arrange
        var itemAssetChanges = CreateItemAssetChanges(toAdd: 10, toRemove: 10);
        var stopwatch = Stopwatch.StartNew();

        // Act
        await _service.PublishPushToDamEventsAsync(itemAssetChanges, CancellationToken.None);

        // Assert
        stopwatch.Stop();
        var elapsedMs = stopwatch.ElapsedMilliseconds;
        
        _output.WriteLine($"Mixed events (10 add + 10 remove): {elapsedMs}ms (threshold: {MaxPublishTimeMs}ms)");
        Assert.True(elapsedMs < MaxPublishTimeMs,
            $"Publish took {elapsedMs}ms, expected < {MaxPublishTimeMs}ms");
    }

    #endregion

    #region Bulk Event Performance Tests

    [Theory]
    [InlineData(10)]
    [InlineData(50)]
    [InlineData(100)]
    public async Task PublishPushToDamEventsAsync_WithMultipleAssets_ShouldScaleLinearly(int assetCount)
    {
        // Arrange
        var itemAssetChanges = CreateItemAssetChanges(toAdd: assetCount, toRemove: 0);
        var stopwatch = Stopwatch.StartNew();

        // Act
        await _service.PublishPushToDamEventsAsync(itemAssetChanges, CancellationToken.None);

        // Assert
        stopwatch.Stop();
        var elapsedMs = stopwatch.ElapsedMilliseconds;
        
        _output.WriteLine($"Publish with {assetCount} assets: {elapsedMs}ms");
        Assert.True(elapsedMs < MaxPublishTimeMs,
            $"Publish with {assetCount} assets took {elapsedMs}ms, expected < {MaxPublishTimeMs}ms");
    }

    [Fact]
    public async Task PublishPushToDamEventsAsync_WithLargeAssetList_ShouldHandleEfficiently()
    {
        // Arrange
        var itemAssetChanges = CreateItemAssetChanges(toAdd: 1000, toRemove: 500);
        var stopwatch = Stopwatch.StartNew();

        // Act
        await _service.PublishPushToDamEventsAsync(itemAssetChanges, CancellationToken.None);

        // Assert
        stopwatch.Stop();
        var elapsedMs = stopwatch.ElapsedMilliseconds;
        
        _output.WriteLine($"Large asset list (1000 add + 500 remove): {elapsedMs}ms (threshold: {MaxBulkPublishTimeMs}ms)");
        Assert.True(elapsedMs < MaxBulkPublishTimeMs,
            $"Bulk publish took {elapsedMs}ms, expected < {MaxBulkPublishTimeMs}ms");
        
        _mockMediator.Verify(m => m.PublishAsync(
            It.Is<PushToDamEvent>(e => e.Operation == DamOperation.Add),
            It.IsAny<CancellationToken>()), Times.Once);
        
        _mockMediator.Verify(m => m.PublishAsync(
            It.Is<PushToDamEvent>(e => e.Operation == DamOperation.Remove),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    #endregion

    #region Concurrent Publishing Tests

    [Theory]
    [InlineData(10)]
    [InlineData(50)]
    [InlineData(100)]
    public async Task PublishPushToDamEventsAsync_ConcurrentPublishing_ShouldHandleParallelLoad(int concurrentCount)
    {
        // Arrange
        var stopwatch = Stopwatch.StartNew();
        var tasks = new List<Task>();

        for (int i = 0; i < concurrentCount; i++)
        {
            var itemAssetChanges = CreateItemAssetChanges(toAdd: 5, toRemove: 5);
            tasks.Add(_service.PublishPushToDamEventsAsync(itemAssetChanges, CancellationToken.None));
        }

        // Act
        await Task.WhenAll(tasks);

        // Assert
        stopwatch.Stop();
        var elapsedMs = stopwatch.ElapsedMilliseconds;
        var throughput = concurrentCount / (elapsedMs / 1000.0);
        
        _output.WriteLine($"Concurrent publishing ({concurrentCount} items): {elapsedMs}ms, {throughput:F0} ops/sec");
        
        _mockMediator.Verify(m => m.PublishAsync(
            It.IsAny<PushToDamEvent>(),
            It.IsAny<CancellationToken>()), Times.Exactly(concurrentCount * 2));
    }

    #endregion

    #region Memory and Resource Tests

    [Fact]
    public async Task PublishPushToDamEventsAsync_RepeatedCalls_ShouldNotLeakMemory()
    {
        // Arrange
        const int iterations = 1000;
        var initialMemory = GC.GetTotalMemory(true);

        // Act
        for (int i = 0; i < iterations; i++)
        {
            var itemAssetChanges = CreateItemAssetChanges(toAdd: 10, toRemove: 10);
            await _service.PublishPushToDamEventsAsync(itemAssetChanges, CancellationToken.None);
        }

        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        var finalMemory = GC.GetTotalMemory(false);
        var memoryIncreaseMb = (finalMemory - initialMemory) / 1024.0 / 1024.0;

        // Assert
        _output.WriteLine($"Memory increase after {iterations} iterations: {memoryIncreaseMb:F2} MB");
        Assert.True(memoryIncreaseMb < 10,
            $"Memory increased by {memoryIncreaseMb:F2} MB, potential memory leak");
    }

    #endregion

    #region Helper Methods

    private ItemAssetChanges CreateItemAssetChanges(int toAdd, int toRemove)
    {
        var itemId = Guid.NewGuid();
        var toAddAssetIds = toAdd > 0 ? Enumerable.Range(1, toAdd).ToList() : new List<int>();
        var toRemoveAssetIds = toRemove > 0 ? Enumerable.Range(1000, toRemove).ToList() : new List<int>();

        var publishedItem = PublishedItem.Create(
            itemId: itemId,
            language: "en",
            itemName: $"Test Item {itemId}",
            version: 1,
            assetIds: toAddAssetIds
        );

        return new ItemAssetChanges
        {
            Item = publishedItem,
            ToAddAssetIds = toAddAssetIds,
            ToRemoveAssetIds = toRemoveAssetIds
        };
    }

    #endregion
}