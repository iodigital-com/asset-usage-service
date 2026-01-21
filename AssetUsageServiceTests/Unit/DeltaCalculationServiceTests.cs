using AssetUsageService.Business.Services;
using AssetUsageService.Domain.Data;
using AssetUsageService.Domain.Models;
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
        var itemId = Guid.NewGuid();
        var assetIds = new List<int> { 1, 2, 3 };
        var publishedItem = PublishedItem.Create(
            itemId: itemId,
            language: "en",
            itemName: "Test Item",
            version: 1,
            assetIds: assetIds
        );

        _mockRepository
            .Setup(r => r.GetAssetItemLinkByItemIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((AssetItemLink?)null);

        // Act
        var result = await _service.CalculateDeltaAsync(publishedItem, assetIds);

        // Assert
        _mockRepository.Verify(r => r.UpsertAssetItemLinkAsync(
            itemId,
            "en",
            It.Is<List<int>>(ids => ids.SequenceEqual(assetIds)),
            1,
            It.IsAny<CancellationToken>()), Times.Once);
        
        Assert.NotNull(result);
        Assert.Equal(publishedItem.ItemId, result.Item.ItemId);
        Assert.Equal(assetIds, result.ToAddAssetIds);
        Assert.Empty(result.ToRemoveAssetIds);
    }

    [Fact]
    public async Task CalculateDeltaAsync_WhenItemDoesNotExist_AndNoAssetIds_ShouldNotCallRepository()
    {
        // Arrange
        var itemId = Guid.NewGuid();
        var assetIds = new List<int>();
        var publishedItem = PublishedItem.Create(
            itemId: itemId,
            language: "en",
            itemName: "Test Item",
            version: 1,
            assetIds: assetIds
        );

        _mockRepository
            .Setup(r => r.GetAssetItemLinkByItemIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((AssetItemLink?)null);

        // Act
        var result = await _service.CalculateDeltaAsync(publishedItem, assetIds);

        // Assert
        _mockRepository.Verify(r => r.UpsertAssetItemLinkAsync(
            It.IsAny<Guid>(),
            It.IsAny<string>(),
            It.IsAny<List<int>>(),
            It.IsAny<int?>(),
            It.IsAny<CancellationToken>()), Times.Never);
        
        Assert.NotNull(result);
        Assert.Empty(result.ToAddAssetIds);
        Assert.Empty(result.ToRemoveAssetIds);
    }

    #endregion

    #region Update Item Tests

    [Fact]
    public async Task CalculateDeltaAsync_WhenItemExists_AndNewAssetIdsAdded_ShouldAddOnlyNewAssets()
    {
        // Arrange
        var itemId = Guid.NewGuid();
        var currentAssetIds = new List<int> { 1, 2, 3 };
        var newAssetIds = new List<int> { 1, 2, 3, 4, 5 };
        var expectedToAdd = new List<int> { 4, 5 };
        
        var publishedItem = PublishedItem.Create(
            itemId: itemId,
            language: "en",
            itemName: "Test Item",
            version: 2,
            assetIds: newAssetIds
        );

        _mockRepository
            .Setup(r => r.GetAssetItemLinkByItemIdAsync(itemId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AssetItemLink 
            { 
                ItemId = itemId, 
                Languages = new Dictionary<string, LanguageAssetData> 
                { 
                    ["en"] = new LanguageAssetData { AssetIds = currentAssetIds, Version = 1 } 
                } 
            });

        _mockRepository
            .Setup(r => r.GetAssetIdsFromItemIdAsync(itemId, "en", It.IsAny<CancellationToken>()))
            .ReturnsAsync(currentAssetIds);

        // Act
            var result = await _service.CalculateDeltaAsync(publishedItem, newAssetIds);

        // Assert
        _mockRepository.Verify(r => r.AddAssetIdsToItemAsync(
            itemId,
            "en",
            It.Is<List<int>>(ids => ids.SequenceEqual(expectedToAdd)),
            2,
            It.IsAny<CancellationToken>()), Times.Once);

        _mockRepository.Verify(r => r.RemoveAssetIdsFromItemAsync(
            itemId,
            "en",
            It.Is<List<int>>(ids => ids.Count == 0),
            It.IsAny<CancellationToken>()), Times.Once);
        
        Assert.NotNull(result);
        Assert.Equal(expectedToAdd, result.ToAddAssetIds);
        Assert.Empty(result.ToRemoveAssetIds);
    }

    [Fact]
    public async Task CalculateDeltaAsync_WhenItemExists_AndAssetIdsRemoved_ShouldRemoveObsoleteAssets()
    {
        // Arrange
        var itemId = Guid.NewGuid();
        var currentAssetIds = new List<int> { 1, 2, 3, 4, 5 };
        var newAssetIds = new List<int> { 1, 2 };
        var expectedToRemove = new List<int> { 3, 4, 5 };
        
        var publishedItem = PublishedItem.Create(
            itemId: itemId,
            language: "en",
            itemName: "Test Item",
            version: 2,
            assetIds: newAssetIds
        );

        _mockRepository
            .Setup(r => r.GetAssetItemLinkByItemIdAsync(itemId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AssetItemLink 
            { 
                ItemId = itemId, 
                Languages = new Dictionary<string, LanguageAssetData> 
                { 
                    ["en"] = new LanguageAssetData { AssetIds = currentAssetIds, Version = 1 } 
                } 
            });

        _mockRepository
            .Setup(r => r.GetAssetIdsFromItemIdAsync(itemId, "en", It.IsAny<CancellationToken>()))
            .ReturnsAsync(currentAssetIds);

        // Act
        var result = await _service.CalculateDeltaAsync(publishedItem, newAssetIds);

        // Assert
        _mockRepository.Verify(r => r.RemoveAssetIdsFromItemAsync(
            itemId,
            "en",
            It.Is<List<int>>(ids => ids.SequenceEqual(expectedToRemove)),
            It.IsAny<CancellationToken>()), Times.Once);

        _mockRepository.Verify(r => r.AddAssetIdsToItemAsync(
            itemId,
            "en",
            It.Is<List<int>>(ids => ids.Count == 0),
            2,
            It.IsAny<CancellationToken>()), Times.Once);
        
        Assert.NotNull(result);
        Assert.Empty(result.ToAddAssetIds);
        Assert.Equal(expectedToRemove, result.ToRemoveAssetIds);
    }

    [Fact]
    public async Task CalculateDeltaAsync_WhenItemExists_AndMixedChanges_ShouldAddAndRemoveCorrectly()
    {
        // Arrange
        var itemId = Guid.NewGuid();
        var currentAssetIds = new List<int> { 1, 2, 3, 4 };
        var newAssetIds = new List<int> { 2, 3, 5, 6 };
        var expectedToAdd = new List<int> { 5, 6 };
        var expectedToRemove = new List<int> { 1, 4 };
        
        var publishedItem = PublishedItem.Create(
            itemId: itemId,
            language: "en",
            itemName: "Test Item",
            version: 2,
            assetIds: newAssetIds
        );

        _mockRepository
            .Setup(r => r.GetAssetItemLinkByItemIdAsync(itemId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AssetItemLink 
            { 
                ItemId = itemId, 
                Languages = new Dictionary<string, LanguageAssetData> 
                { 
                    ["en"] = new LanguageAssetData { AssetIds = currentAssetIds, Version = 1 } 
                } 
            });

        _mockRepository
            .Setup(r => r.GetAssetIdsFromItemIdAsync(itemId, "en", It.IsAny<CancellationToken>()))
            .ReturnsAsync(currentAssetIds);

        // Act
        var result = await _service.CalculateDeltaAsync(publishedItem, newAssetIds);

        // Assert
        _mockRepository.Verify(r => r.AddAssetIdsToItemAsync(
            itemId,
            "en",
            It.Is<List<int>>(ids => ids.SequenceEqual(expectedToAdd)),
            2,
            It.IsAny<CancellationToken>()), Times.Once);

        _mockRepository.Verify(r => r.RemoveAssetIdsFromItemAsync(
            itemId,
            "en",
            It.Is<List<int>>(ids => ids.SequenceEqual(expectedToRemove)),
            It.IsAny<CancellationToken>()), Times.Once);
        
        Assert.NotNull(result);
        Assert.Equal(expectedToAdd, result.ToAddAssetIds);
        Assert.Equal(expectedToRemove, result.ToRemoveAssetIds);
    }

    [Fact]
    public async Task CalculateDeltaAsync_WhenItemExists_AndNoChanges_ShouldCallAddAndRemoveWithEmptyLists()
    {
        // Arrange
        var itemId = Guid.NewGuid();
        var currentAssetIds = new List<int> { 1, 2, 3 };
        var newAssetIds = new List<int> { 1, 2, 3 };
        
        var publishedItem = PublishedItem.Create(
            itemId: itemId,
            language: "en",
            itemName: "Test Item",
            version: 2,
            assetIds: newAssetIds
        );

        _mockRepository
            .Setup(r => r.GetAssetItemLinkByItemIdAsync(itemId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AssetItemLink 
            { 
                ItemId = itemId, 
                Languages = new Dictionary<string, LanguageAssetData> 
                { 
                    ["en"] = new LanguageAssetData { AssetIds = currentAssetIds, Version = 1 } 
                } 
            });

        _mockRepository
            .Setup(r => r.GetAssetIdsFromItemIdAsync(itemId, "en", It.IsAny<CancellationToken>()))
            .ReturnsAsync(currentAssetIds);

        // Act
        var result = await _service.CalculateDeltaAsync(publishedItem, newAssetIds);

        // Assert
        _mockRepository.Verify(r => r.AddAssetIdsToItemAsync(
            itemId,
            "en",
            It.Is<List<int>>(ids => ids.Count == 0),
            2,
            It.IsAny<CancellationToken>()), Times.Once);

        _mockRepository.Verify(r => r.RemoveAssetIdsFromItemAsync(
            itemId,
            "en",
            It.Is<List<int>>(ids => ids.Count == 0),
            It.IsAny<CancellationToken>()), Times.Once);
        
        Assert.NotNull(result);
        Assert.Empty(result.ToAddAssetIds);
        Assert.Empty(result.ToRemoveAssetIds);
    }

    #endregion

    #region Delete Item Tests

    [Fact]
    public async Task CalculateDeltaAsync_WhenItemExists_AndNoAssetIds_ShouldRemoveLanguage()
    {
        // Arrange
        var itemId = Guid.NewGuid();
        var assetIds = new List<int>();
        var currentAssetIds = new List<int> { 1, 2, 3 };
        
        var publishedItem = PublishedItem.Create(
            itemId: itemId,
            language: "en",
            itemName: "Test Item",
            version: 2,
            assetIds: assetIds
        );

        _mockRepository
            .Setup(r => r.GetAssetItemLinkByItemIdAsync(itemId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AssetItemLink 
            { 
                ItemId = itemId, 
                Languages = new Dictionary<string, LanguageAssetData> 
                { 
                    ["en"] = new LanguageAssetData { AssetIds = currentAssetIds, Version = 1 } 
                } 
            });

        _mockRepository
            .Setup(r => r.GetAssetIdsFromItemIdAsync(itemId, "en", It.IsAny<CancellationToken>()))
            .ReturnsAsync(currentAssetIds);

        // Act
        var result = await _service.CalculateDeltaAsync(publishedItem, assetIds);

        // Assert
        _mockRepository.Verify(r => r.RemoveLanguageFromItemAsync(
            itemId,
            "en",
            It.IsAny<CancellationToken>()), Times.Once);
        
        Assert.NotNull(result);
        Assert.Empty(result.ToAddAssetIds);
        Assert.Equal(currentAssetIds, result.ToRemoveAssetIds);
    }

    #endregion

    #region Edge Cases

    [Fact]
    public async Task CalculateDeltaAsync_WithDuplicateAssetIds_ShouldHandleCorrectly()
    {
        // Arrange
        var itemId = Guid.NewGuid();
        var assetIds = new List<int> { 1, 2, 2, 3, 3, 3 };
        
        var publishedItem = PublishedItem.Create(
            itemId: itemId,
            language: "en",
            itemName: "Test Item",
            version: 1,
            assetIds: assetIds
        );

        _mockRepository
            .Setup(r => r.GetAssetItemLinkByItemIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((AssetItemLink?)null);

        // Act
        var result = await _service.CalculateDeltaAsync(publishedItem, assetIds);

        // Assert
        _mockRepository.Verify(r => r.UpsertAssetItemLinkAsync(
            It.IsAny<Guid>(),
            "en",
            It.Is<List<int>>(ids => ids.SequenceEqual(assetIds)),
            1,
            It.IsAny<CancellationToken>()), Times.Once);
        
        Assert.NotNull(result);
        Assert.Equal(assetIds, result.ToAddAssetIds);
    }

    [Fact]
    public async Task CalculateDeltaAsync_WithLargeAssetList_ShouldProcessAll()
    {
        // Arrange
        var itemId = Guid.NewGuid();
        var assetIds = Enumerable.Range(1, 1000).ToList();
        
        var publishedItem = PublishedItem.Create(
            itemId: itemId,
            language: "en",
            itemName: "Test Item",
            version: 1,
            assetIds: assetIds
        );

        _mockRepository
            .Setup(r => r.GetAssetItemLinkByItemIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((AssetItemLink?)null);

        // Act
        var result = await _service.CalculateDeltaAsync(publishedItem, assetIds);

        // Assert
        _mockRepository.Verify(r => r.UpsertAssetItemLinkAsync(
            It.IsAny<Guid>(),
            "en",
            It.Is<List<int>>(ids => ids.Count == 1000),
            1,
            It.IsAny<CancellationToken>()), Times.Once);
        
        Assert.NotNull(result);
        Assert.Equal(1000, result.ToAddAssetIds.Count);
    }

    #endregion
}
