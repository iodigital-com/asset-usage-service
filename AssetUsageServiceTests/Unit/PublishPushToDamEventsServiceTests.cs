using AssetUsageService.Business.Events;
using AssetUsageService.Business.Events.interfaces;
using AssetUsageService.Business.Services;
using AssetUsageService.Domain.Models;
using Microsoft.Extensions.Logging;
using Moq;

namespace AssetUsageServiceTests.Unit;

public class PublishPushToDamEventsServiceTests
{
    private readonly Mock<ILogger<PublishPushToDamEventsService>> _mockLogger;
    private readonly Mock<IMediater> _mockMediater;
    private readonly PublishPushToDamEventsService _service;

    public PublishPushToDamEventsServiceTests()
    {
        _mockLogger = new Mock<ILogger<PublishPushToDamEventsService>>();
        _mockMediater = new Mock<IMediater>();
        _service = new PublishPushToDamEventsService(_mockLogger.Object, _mockMediater.Object);
    }

    [Theory]
    [InlineData(new[] { 1, 2, 3 }, new int[] { }, 1, 0)] // Only Add
    [InlineData(new int[] { }, new[] { 4, 5, 6 }, 0, 1)] // Only Remove
    [InlineData(new[] { 1, 2 }, new[] { 3, 4 }, 1, 1)]   // Both Add and Remove
    public async Task PublishPushToDamEventsAsync_WithVariousOperations_ShouldPublishCorrectEvents(int[] toAdd, int[] toRemove, int expectedAddCalls, int expectedRemoveCalls)
    {
        // Arrange
        var itemId = Guid.NewGuid();
        var publishedItem = PublishedItem.Create(
            itemId: itemId,
            language: "en",
            itemName: "Test Item",
            version: 1,
            assetIds: toAdd.ToList()
        );

        var itemAssetChanges = new ItemAssetChanges
        {
            Item = publishedItem,
            ToAddAssetIds = toAdd.ToList(),
            ToRemoveAssetIds = toRemove.ToList()
        };

        // Act
        await _service.PublishPushToDamEventsAsync(itemAssetChanges, CancellationToken.None);

        // Assert
        _mockMediater.Verify(m => m.PublishAsync(
            It.Is<PushToDamEvent>(e => e.Operation == DamOperation.Add),
            It.IsAny<CancellationToken>()), Times.Exactly(expectedAddCalls));

        _mockMediater.Verify(m => m.PublishAsync(
            It.Is<PushToDamEvent>(e => e.Operation == DamOperation.Remove),
            It.IsAny<CancellationToken>()), Times.Exactly(expectedRemoveCalls));
    }

    [Theory]
    [MemberData(nameof(EmptyListTestData))]
    public async Task PublishPushToDamEventsAsync_WithEmptyOrNullLists_ShouldNotPublishAnyEvents(
        List<int>? toAdd, List<int>? toRemove)
    {
        // Arrange
        var publishedItem = PublishedItem.Create(
            itemId: Guid.NewGuid(),
            language: "en",
            itemName: "Test Item",
            version: 1
        );

        var itemAssetChanges = new ItemAssetChanges
        {
            Item = publishedItem,
            ToAddAssetIds = toAdd,
            ToRemoveAssetIds = toRemove
        };

        // Act
        await _service.PublishPushToDamEventsAsync(itemAssetChanges, CancellationToken.None);

        // Assert
        _mockMediater.Verify(m => m.PublishAsync(
            It.IsAny<PushToDamEvent>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    public static IEnumerable<object[]> EmptyListTestData()
    {
        yield return new object[] { new List<int>(), new List<int>() };
        yield return new object[] { null, null };
        yield return new object[] { new List<int>(), null };
        yield return new object[] { null, new List<int>() };
    }

    [Fact]
    public async Task PublishPushToDamEventsAsync_WithLargeAssetList_ShouldPublishSuccessfully()
    {
        // Arrange
        var itemId = Guid.NewGuid();
        var largeAssetList = Enumerable.Range(1, 1000).ToList();
        var publishedItem = PublishedItem.Create(
            itemId: itemId,
            language: "en",
            itemName: "Test Item",
            version: 1,
            assetIds: largeAssetList
        );

        var itemAssetChanges = new ItemAssetChanges
        {
            Item = publishedItem,
            ToAddAssetIds = largeAssetList,
            ToRemoveAssetIds = new List<int>()
        };

        // Act
        await _service.PublishPushToDamEventsAsync(itemAssetChanges, CancellationToken.None);

        // Assert
        _mockMediater.Verify(m => m.PublishAsync(
            It.Is<PushToDamEvent>(e =>
                e.AssetIds.Count == 1000 &&
                e.Operation == DamOperation.Add),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task PublishPushToDamEventsAsync_WithCancellationToken_ShouldPassTokenToMediater()
    {
        // Arrange
        var cts = new CancellationTokenSource();
        var itemId = Guid.NewGuid();
        var assetIds = new List<int> { 1, 2, 3 };
        var publishedItem = PublishedItem.Create(
            itemId: itemId,
            language: "en",
            itemName: "Test Item",
            version: 1,
            assetIds: assetIds
        );

        var itemAssetChanges = new ItemAssetChanges
        {
            Item = publishedItem,
            ToAddAssetIds = assetIds,
            ToRemoveAssetIds = new List<int>()
        };

        // Act
        await _service.PublishPushToDamEventsAsync(itemAssetChanges, cts.Token);

        // Assert
        _mockMediater.Verify(m => m.PublishAsync(
            It.IsAny<PushToDamEvent>(),
            cts.Token), Times.Once);
    }
}