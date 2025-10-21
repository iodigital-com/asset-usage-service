using AssetUsageService.Business.Services;
using AssetUsageService.Data;
using AssetUsageService.Infrastructure;
using Microsoft.Extensions.Logging;
using Moq;

namespace AssetUsageServiceTests.Unit;

public class DeltaCalculationServiceTests
{
    private readonly Mock<IAssetItemLinkRepository> _mockRepository;
    private readonly Mock<ILogger<DeltaCalculationService>> _mockLogger;
    private readonly DeltaCalculationService _service;

    public DeltaCalculationServiceTests()
    {
        _mockRepository = new Mock<IAssetItemLinkRepository>();
        _mockLogger = new Mock<ILogger<DeltaCalculationService>>();
        _service = new DeltaCalculationService(_mockRepository.Object, _mockLogger.Object);
    }

    #region New Item Tests

    [Fact]
    public async Task CalculateDeltaAsync_WhenItemDoesNotExist_AndAssetIdsProvided_ShouldInsertNewItem()
    {
        // Arrange
        var itemIdString = Guid.NewGuid().ToString();
        var assetIdStrings = new List<string> { "1", "2", "3" };
        var expectedAssetIds = new List<int> { 1, 2, 3 };

        _mockRepository
            .Setup(r => r.GetAssetItemLinkByItemIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((AssetItemLink?)null);

        // Act
        await _service.CalculateDeltaAsync(itemIdString, assetIdStrings);

        // Assert
        _mockRepository.Verify(r => r.InsertAssetItemLinkAsync(
            Guid.Parse(itemIdString),
            It.Is<List<int>>(ids => ids.SequenceEqual(expectedAssetIds)),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CalculateDeltaAsync_WhenItemDoesNotExist_AndNoAssetIds_ShouldNotCallRepository()
    {
        // Arrange
        var itemIdString = Guid.NewGuid().ToString();
        var assetIdStrings = new List<string>();

        _mockRepository
            .Setup(r => r.GetAssetItemLinkByItemIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((AssetItemLink?)null);

        // Act
        await _service.CalculateDeltaAsync(itemIdString, assetIdStrings);

        // Assert
        _mockRepository.Verify(r => r.InsertAssetItemLinkAsync(
            It.IsAny<Guid>(),
            It.IsAny<List<int>>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    #endregion

    #region Update Item Tests

    [Fact]
    public async Task CalculateDeltaAsync_WhenItemExists_AndNewAssetIdsAdded_ShouldAddOnlyNewAssets()
    {
        // Arrange
        var itemId = Guid.NewGuid();
        var itemIdString = itemId.ToString();
        var currentAssetIds = new List<int> { 1, 2, 3 };
        var newAssetIdStrings = new List<string> { "1", "2", "3", "4", "5" };
        var expectedToAdd = new List<int> { 4, 5 };

        _mockRepository
            .Setup(r => r.GetAssetItemLinkByItemIdAsync(itemId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AssetItemLink { ItemId = itemId, AssetIds = currentAssetIds });

        _mockRepository
            .Setup(r => r.GetAssetIdsFromItemIdAsync(itemId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(currentAssetIds);

        // Act
        await _service.CalculateDeltaAsync(itemIdString, newAssetIdStrings);

        // Assert
        _mockRepository.Verify(r => r.AddAssetIdsToItemAsync(
            itemId,
            It.Is<List<int>>(ids => ids.SequenceEqual(expectedToAdd)),
            It.IsAny<CancellationToken>()), Times.Once);

        _mockRepository.Verify(r => r.RemoveAssetIdsFromItemAsync(
            itemId,
            It.Is<List<int>>(ids => ids.Count == 0),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CalculateDeltaAsync_WhenItemExists_AndAssetIdsRemoved_ShouldRemoveObsoleteAssets()
    {
        // Arrange
        var itemId = Guid.NewGuid();
        var itemIdString = itemId.ToString();
        var currentAssetIds = new List<int> { 1, 2, 3, 4, 5 };
        var newAssetIdStrings = new List<string> { "1", "2" };
        var expectedToRemove = new List<int> { 3, 4, 5 };

        _mockRepository
            .Setup(r => r.GetAssetItemLinkByItemIdAsync(itemId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AssetItemLink { ItemId = itemId, AssetIds = currentAssetIds });

        _mockRepository
            .Setup(r => r.GetAssetIdsFromItemIdAsync(itemId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(currentAssetIds);

        // Act
        await _service.CalculateDeltaAsync(itemIdString, newAssetIdStrings);

        // Assert
        _mockRepository.Verify(r => r.RemoveAssetIdsFromItemAsync(
            itemId,
            It.Is<List<int>>(ids => ids.SequenceEqual(expectedToRemove)),
            It.IsAny<CancellationToken>()), Times.Once);

        _mockRepository.Verify(r => r.AddAssetIdsToItemAsync(
            itemId,
            It.Is<List<int>>(ids => ids.Count == 0),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CalculateDeltaAsync_WhenItemExists_AndMixedChanges_ShouldAddAndRemoveCorrectly()
    {
        // Arrange
        var itemId = Guid.NewGuid();
        var itemIdString = itemId.ToString();
        var currentAssetIds = new List<int> { 1, 2, 3, 4 };
        var newAssetIdStrings = new List<string> { "2", "3", "5", "6" };
        var expectedToAdd = new List<int> { 5, 6 };
        var expectedToRemove = new List<int> { 1, 4 };

        _mockRepository
            .Setup(r => r.GetAssetItemLinkByItemIdAsync(itemId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AssetItemLink { ItemId = itemId, AssetIds = currentAssetIds });

        _mockRepository
            .Setup(r => r.GetAssetIdsFromItemIdAsync(itemId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(currentAssetIds);

        // Act
        await _service.CalculateDeltaAsync(itemIdString, newAssetIdStrings);

        // Assert
        _mockRepository.Verify(r => r.AddAssetIdsToItemAsync(
            itemId,
            It.Is<List<int>>(ids => ids.SequenceEqual(expectedToAdd)),
            It.IsAny<CancellationToken>()), Times.Once);

        _mockRepository.Verify(r => r.RemoveAssetIdsFromItemAsync(
            itemId,
            It.Is<List<int>>(ids => ids.SequenceEqual(expectedToRemove)),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CalculateDeltaAsync_WhenItemExists_AndNoChanges_ShouldCallAddAndRemoveWithEmptyLists()
    {
        // Arrange
        var itemId = Guid.NewGuid();
        var itemIdString = itemId.ToString();
        var currentAssetIds = new List<int> { 1, 2, 3 };
        var newAssetIdStrings = new List<string> { "1", "2", "3" };

        _mockRepository
            .Setup(r => r.GetAssetItemLinkByItemIdAsync(itemId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AssetItemLink { ItemId = itemId, AssetIds = currentAssetIds });

        _mockRepository
            .Setup(r => r.GetAssetIdsFromItemIdAsync(itemId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(currentAssetIds);

        // Act
        await _service.CalculateDeltaAsync(itemIdString, newAssetIdStrings);

        // Assert
        _mockRepository.Verify(r => r.AddAssetIdsToItemAsync(
            itemId,
            It.Is<List<int>>(ids => ids.Count == 0),
            It.IsAny<CancellationToken>()), Times.Once);

        _mockRepository.Verify(r => r.RemoveAssetIdsFromItemAsync(
            itemId,
            It.Is<List<int>>(ids => ids.Count == 0),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    #endregion

    #region Delete Item Tests

    [Fact]
    public async Task CalculateDeltaAsync_WhenItemExists_AndNoAssetIds_ShouldRemoveItem()
    {
        // Arrange
        var itemId = Guid.NewGuid();
        var itemIdString = itemId.ToString();
        var assetIdStrings = new List<string>();

        _mockRepository
            .Setup(r => r.GetAssetItemLinkByItemIdAsync(itemId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AssetItemLink { ItemId = itemId, AssetIds = new List<int> { 1, 2, 3 } });

        // Act
        await _service.CalculateDeltaAsync(itemIdString, assetIdStrings);

        // Assert
        _mockRepository.Verify(r => r.RemoveItemAsync(
            itemId,
            It.IsAny<CancellationToken>()), Times.Once);
    }

    #endregion

    #region Edge Cases

    [Fact]
    public async Task CalculateDeltaAsync_WithDuplicateAssetIds_ShouldHandleCorrectly()
    {
        // Arrange
        var itemIdString = Guid.NewGuid().ToString();
        var assetIdStrings = new List<string> { "1", "2", "2", "3", "3", "3" };
        var expectedAssetIds = new List<int> { 1, 2, 2, 3, 3, 3 };

        _mockRepository
            .Setup(r => r.GetAssetItemLinkByItemIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((AssetItemLink?)null);

        // Act
        await _service.CalculateDeltaAsync(itemIdString, assetIdStrings);

        // Assert
        _mockRepository.Verify(r => r.InsertAssetItemLinkAsync(
            It.IsAny<Guid>(),
            It.Is<List<int>>(ids => ids.SequenceEqual(expectedAssetIds)),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CalculateDeltaAsync_WithLargeAssetList_ShouldProcessAll()
    {
        // Arrange
        var itemIdString = Guid.NewGuid().ToString();
        var assetIdStrings = Enumerable.Range(1, 1000).Select(i => i.ToString()).ToList();

        _mockRepository
            .Setup(r => r.GetAssetItemLinkByItemIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((AssetItemLink?)null);

        // Act
        await _service.CalculateDeltaAsync(itemIdString, assetIdStrings);

        // Assert
        _mockRepository.Verify(r => r.InsertAssetItemLinkAsync(
            It.IsAny<Guid>(),
            It.Is<List<int>>(ids => ids.Count == 1000),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Theory]
    [InlineData("invalid-guid")]
    [InlineData("")]
    public async Task CalculateDeltaAsync_WithInvalidGuid_ShouldThrowException(string invalidGuid)
    {
        // Arrange
        var assetIdStrings = new List<string> { "1", "2", "3" };

        // Act & Assert
        await Assert.ThrowsAsync<FormatException>(() =>
            _service.CalculateDeltaAsync(invalidGuid, assetIdStrings));
    }

    [Fact]
    public async Task CalculateDeltaAsync_WithInvalidAssetId_ShouldThrowException()
    {
        // Arrange
        var itemIdString = Guid.NewGuid().ToString();
        var assetIdStrings = new List<string> { "1", "invalid", "3" };

        // Act & Assert
        await Assert.ThrowsAsync<FormatException>(() =>
            _service.CalculateDeltaAsync(itemIdString, assetIdStrings));
    }

    #endregion
}