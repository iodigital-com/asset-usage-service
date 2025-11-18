using AssetUsageService.Business.Events;
using AssetUsageService.Business.Handlers;
using AssetUsageService.Domain.Models;
using AssetUsageService.Integration;
using Microsoft.Extensions.Logging;
using Moq;
using Newtonsoft.Json.Linq;
using Stylelabs.M.Framework.Essentials.LoadConfigurations;
using Stylelabs.M.Sdk.Contracts.Base;
using Stylelabs.M.Sdk.WebClient;

namespace AssetUsageServiceTests.Unit;

public class PushToDamHandlerTests
{
    private readonly Mock<IContentHubConnectionService> _mockConnectionService;
    private readonly Mock<ILogger<PushToDamHandler>> _mockLogger;
    private readonly Mock<IWebMClient> _mockClient;
    private readonly PushToDamHandler _handler;

    public PushToDamHandlerTests()
    {
        _mockConnectionService = new Mock<IContentHubConnectionService>();
        _mockLogger = new Mock<ILogger<PushToDamHandler>>();
        _mockClient = new Mock<IWebMClient>();

        _mockConnectionService
            .Setup(cs => cs.CreateClient())
            .Returns(_mockClient.Object);

        _handler = new PushToDamHandler(_mockConnectionService.Object, _mockLogger.Object);
    }

    [Fact]
    public async Task HandleAsync_WithNullEvent_ShouldThrowArgumentNullException()
    {
        // Arrange
        PushToDamEvent nullEvent = null!;

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            _handler.HandleAsync(nullEvent, CancellationToken.None));
    }

    [Theory]
    [InlineData(DamOperation.Add)]
    [InlineData(DamOperation.Remove)]
    public async Task HandleAsync_WithEmptyAssetList_ShouldNotCallDam(DamOperation operation)
    {
        // Arrange
        var publishedItem = PublishedItem.Create(
            itemId: Guid.NewGuid(),
            language: "en",
            itemName: "Test Item",
            version: 1
        );

        var @event = new PushToDamEvent
        {
            Item = publishedItem,
            AssetIds = new List<int>(),
            Operation = operation
        };

        // Act
        await _handler.HandleAsync(@event, CancellationToken.None);

        // Assert
        _mockConnectionService.Verify(cs => cs.CreateClient(), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_WithAddOperation_ShouldCallCreateClientAndSave()
    {
        // Arrange
        var itemId = Guid.NewGuid();
        var assetId = 123;
        var publishedItem = PublishedItem.Create(
            itemId: itemId,
            language: "en",
            itemName: "Test Item",
            version: 1,
            assetIds: new List<int> { assetId }
        );

        var addEvent = new PushToDamEvent
        {
            Item = publishedItem,
            AssetIds = new List<int> { assetId },
            Operation = DamOperation.Add
        };

        var mockEntity = SetupMockEntity(new JObject());
        _mockClient.Setup(c => c.Entities.GetAsync(assetId, It.IsAny<EntityLoadConfiguration>()))
            .ReturnsAsync(mockEntity.Object);
        _mockClient.Setup(c => c.Entities.SaveAsync(It.IsAny<IEntity>()))
             .Returns(Task.FromResult(1L));

        // Act
        await _handler.HandleAsync(addEvent, CancellationToken.None);

        // Assert
        _mockConnectionService.Verify(cs => cs.CreateClient(), Times.Once);
        mockEntity.Verify(e => e.SetPropertyValue("UsageTracking", It.IsAny<JObject>()), Times.Once);
    }

    [Theory]
    [MemberData(nameof(NullUsageTrackingTestData))]
    public async Task HandleAsync_WithAddOperation_AndNullUsageTracking_ShouldCreateNewJObject(
        JToken? usageTracking, string expectedLogMessage, LogLevel expectedLogLevel)
    {
        // Arrange
        var itemId = Guid.NewGuid();
        var assetId = 123;
        var publishedItem = PublishedItem.Create(
            itemId: itemId,
            language: "en",
            itemName: "Test Item",
            version: 1,
            assetIds: new List<int> { assetId }
        );

        var addEvent = new PushToDamEvent
        {
            Item = publishedItem,
            AssetIds = new List<int> { assetId },
            Operation = DamOperation.Add
        };

        var mockEntity = SetupMockEntity(usageTracking);
        _mockClient.Setup(c => c.Entities.GetAsync(assetId, It.IsAny<EntityLoadConfiguration>()))
            .ReturnsAsync(mockEntity.Object);
        _mockClient.Setup(c => c.Entities.SaveAsync(It.IsAny<IEntity>()))
             .Returns(Task.FromResult(1L));

        // Act
        await _handler.HandleAsync(addEvent, CancellationToken.None);

        // Assert
        _mockLogger.Verify(
            logger => logger.Log(
                expectedLogLevel,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains(expectedLogMessage)),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);

        mockEntity.Verify(e => e.SetPropertyValue("UsageTracking", It.IsAny<JObject>()), Times.Once);
    }

    public static IEnumerable<object?[]> NullUsageTrackingTestData()
    {
        yield return new object?[] { null, "UsageTracking property is null or invalid type", LogLevel.Information };
    }

    [Fact]
    public async Task HandleAsync_WithRemoveOperation_ShouldRemoveItemFromUsageTracking()
    {
        // Arrange
        var itemId = Guid.NewGuid();
        var assetId = 456;
        var publishedItem = PublishedItem.Create(
            itemId: itemId,
            language: "en",
            itemName: "Test Item",
            version: 1
        );

        var removeEvent = new PushToDamEvent
        {
            Item = publishedItem,
            AssetIds = new List<int> { assetId },
            Operation = DamOperation.Remove
        };

        var existingUsageTracking = new JObject
        {
            [itemId.ToString()] = publishedItem.GetUsageTrackingJson()
        };

        var mockEntity = SetupMockEntity(existingUsageTracking);
        _mockClient.Setup(c => c.Entities.GetAsync(assetId, It.IsAny<EntityLoadConfiguration>()))
            .ReturnsAsync(mockEntity.Object);
        _mockClient.Setup(c => c.Entities.SaveAsync(It.IsAny<IEntity>()))
            .Returns(Task.FromResult(1L));

        // Act
        await _handler.HandleAsync(removeEvent, CancellationToken.None);

        // Assert
        _mockClient.Verify(c => c.Entities.SaveAsync(mockEntity.Object), Times.Once);
        _mockLogger.Verify(
            logger => logger.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Successfully pushed remove to DAM")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Theory]
    [InlineData(null, true)] // Null UsageTracking
    [InlineData("other-guid", false)] // Item not in UsageTracking
    public async Task HandleAsync_WithRemoveOperation_AndInvalidState_ShouldNotSave(string? existingItemId, bool isNull)
    {
        // Arrange
        var itemId = Guid.NewGuid();
        var assetId = 789;
        var publishedItem = PublishedItem.Create(
            itemId: itemId,
            language: "en",
            itemName: "Test Item",
            version: 1
        );

        var removeEvent = new PushToDamEvent
        {
            Item = publishedItem,
            AssetIds = new List<int> { assetId },
            Operation = DamOperation.Remove
        };

        var mockEntity = new Mock<IEntity>();
        if (isNull)
        {
            mockEntity.Setup(e => e.GetPropertyValue<JToken>("UsageTracking"))
                .Returns((JToken?)null);
        }
        else
        {
            var existingUsageTracking = new JObject
            {
                [existingItemId!] = JObject.Parse("{\"ItemName\": \"Other Item\"}")
            };
            mockEntity.Setup(e => e.GetPropertyValue<JToken>("UsageTracking"))
                .Returns(existingUsageTracking);
        }

        _mockClient.Setup(c => c.Entities.GetAsync(assetId, It.IsAny<EntityLoadConfiguration>()))
            .ReturnsAsync(mockEntity.Object);

        // Act
        await _handler.HandleAsync(removeEvent, CancellationToken.None);

        // Assert
        _mockClient.Verify(c => c.Entities.SaveAsync(It.IsAny<IEntity>()), Times.Never);
        
        if (isNull)
        {
            _mockLogger.Verify(
                logger => logger.Log(
                    LogLevel.Warning,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("UsageTracking property is null")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }
    }

    [Theory]
    [InlineData(DamOperation.Add, "Failed to push add to DAM")]
    [InlineData(DamOperation.Remove, "Failed to push remove to DAM")]
    public async Task HandleAsync_WhenDamThrowsException_ShouldLogErrorAndRethrow(
        DamOperation operation, string expectedErrorMessage)
    {
        // Arrange
        var itemId = Guid.NewGuid();
        var assetId = 999;
        var publishedItem = PublishedItem.Create(
            itemId: itemId,
            language: "en",
            itemName: "Test Item",
            version: 1,
            assetIds: new List<int> { assetId }
        );

        var @event = new PushToDamEvent
        {
            Item = publishedItem,
            AssetIds = new List<int> { assetId },
            Operation = operation
        };

        _mockClient.Setup(c => c.Entities.GetAsync(assetId, It.IsAny<EntityLoadConfiguration>()))
            .ThrowsAsync(new Exception("DAM connection failed"));

        // Act & Assert
        await Assert.ThrowsAsync<Exception>(() =>
            _handler.HandleAsync(@event, CancellationToken.None));

        _mockLogger.Verify(
            logger => logger.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains(expectedErrorMessage)),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task HandleAsync_WhenSaveThrowsException_ShouldLogErrorAndRethrow()
    {
        // Arrange
        var itemId = Guid.NewGuid();
        var assetId = 123;
        var publishedItem = PublishedItem.Create(
            itemId: itemId,
            language: "en",
            itemName: "Test Item",
            version: 1,
            assetIds: new List<int> { assetId }
        );

        var addEvent = new PushToDamEvent
        {
            Item = publishedItem,
            AssetIds = new List<int> { assetId },
            Operation = DamOperation.Add
        };

        var mockEntity = SetupMockEntity(new JObject());
        _mockClient.Setup(c => c.Entities.GetAsync(assetId, It.IsAny<EntityLoadConfiguration>()))
            .ReturnsAsync(mockEntity.Object);
        _mockClient.Setup(c => c.Entities.SaveAsync(It.IsAny<IEntity>()))
            .ThrowsAsync(new Exception("Save failed"));

        // Act & Assert
        await Assert.ThrowsAsync<Exception>(() =>
            _handler.HandleAsync(addEvent, CancellationToken.None));

        _mockLogger.Verify(
            logger => logger.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Failed to push add to DAM")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task HandleAsync_WithMultipleAssets_ShouldProcessAll()
    {
        // Arrange
        var itemId = Guid.NewGuid();
        var assetIds = new List<int> { 100, 200, 300 };
        var publishedItem = PublishedItem.Create(
            itemId: itemId,
            language: "en",
            itemName: "Test Item",
            version: 1,
            assetIds: assetIds
        );

        var addEvent = new PushToDamEvent
        {
            Item = publishedItem,
            AssetIds = assetIds,
            Operation = DamOperation.Add
        };

        foreach (var assetId in assetIds)
        {
            var mockEntity = SetupMockEntity(new JObject());
            _mockClient.Setup(c => c.Entities.GetAsync(assetId, It.IsAny<EntityLoadConfiguration>()))
                .ReturnsAsync(mockEntity.Object);
            _mockClient.Setup(c => c.Entities.SaveAsync(mockEntity.Object))
                .Returns(Task.FromResult(1L));
        }

        // Act
        await _handler.HandleAsync(addEvent, CancellationToken.None);

        // Assert
        _mockClient.Verify(c => c.Entities.GetAsync(It.IsAny<long>(), It.IsAny<EntityLoadConfiguration>()),
            Times.Exactly(3));
        _mockClient.Verify(c => c.Entities.SaveAsync(It.IsAny<IEntity>()),
            Times.Exactly(3));
    }

    private Mock<IEntity> SetupMockEntity(JToken? usageTracking)
    {
        var mockEntity = new Mock<IEntity>();
        mockEntity.Setup(e => e.GetPropertyValue<JToken>("UsageTracking"))
            .Returns(usageTracking);
        mockEntity.Setup(e => e.SetPropertyValue("UsageTracking", It.IsAny<JObject>()));
        return mockEntity;
    }
}