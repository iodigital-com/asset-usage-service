using AssetUsageService.Business.Events;
using AssetUsageService.Business.Events.interfaces;
using AssetUsageService.Business.Services;
using AssetUsageService.Domain.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;

namespace AssetUsageServiceTests.Unit;

public class PublishAssetIdsByPublicLinksEventServiceTests
{
    private readonly Mock<IMediator> _mockMediator;
    private readonly IConfiguration _configuration;
    private readonly Mock<ILogger<PublishAssetIdsByPublicLinksEventService>> _mockLogger;
    private readonly PublishAssetIdsByPublicLinksEventService _service;
    private readonly string _testEndpoint;

    public PublishAssetIdsByPublicLinksEventServiceTests()
    {
        _mockMediator = new Mock<IMediator>();
        _mockLogger = new Mock<ILogger<PublishAssetIdsByPublicLinksEventService>>();

        _configuration = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.Test.json", optional: false)
            .Build();

        _testEndpoint = _configuration["ContentHub:Endpoint"]
            ?? throw new InvalidOperationException("ContentHub:Endpoint not configured");

        _service = new PublishAssetIdsByPublicLinksEventService(
            _mockMediator.Object,
            _configuration,
            _mockLogger.Object);
    }

    [Fact]
    public async Task GetAssetIdsByPublicLinksAsync_NullPublishedItem_ThrowsArgumentNullException()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            _service.GetAssetIdsByPublicLinksAsync(null!, CancellationToken.None));
    }

    [Fact]
    public async Task GetAssetIdsByPublicLinksAsync_NullLinks_ReturnsEmptyList()
    {
        // Arrange
        var item = CreatePublishedItem(publicLinks: null);

        // Act
        var result = await _service.GetAssetIdsByPublicLinksAsync(item, CancellationToken.None);

        // Assert
        Assert.Empty(result);
        VerifyMediatorNeverCalled();
        VerifyLogContains(LogLevel.Debug, "No public links found");
    }

    [Fact]
    public async Task GetAssetIdsByPublicLinksAsync_EmptyLinks_ReturnsEmptyList()
    {
        // Arrange
        var item = CreatePublishedItem(publicLinks: new List<string>());

        // Act
        var result = await _service.GetAssetIdsByPublicLinksAsync(item, CancellationToken.None);

        // Assert
        Assert.Empty(result);
        VerifyMediatorNeverCalled();
    }

    [Fact]
    public async Task GetAssetIdsByPublicLinksAsync_NoContentHubLinks_ReturnsEmptyList()
    {
        // Arrange
        var item = CreatePublishedItem(publicLinks: new List<string>
        {
            "https://other-domain.com/asset1",
            "https://another-site.com/asset2"
        });

        // Act
        var result = await _service.GetAssetIdsByPublicLinksAsync(item, CancellationToken.None);

        // Assert
        Assert.Empty(result);
        VerifyMediatorNeverCalled();
        VerifyLogContains(LogLevel.Debug, "No ContentHub links found");
    }

    [Fact]
    public async Task GetAssetIdsByPublicLinksAsync_MissingConfiguration_ReturnsEmptyList()
    {
        // Arrange
        var mockConfig = new Mock<IConfiguration>();
        mockConfig.Setup(c => c.GetSection("ContentHub:Endpoint").Value).Returns((string?)null);

        var service = new PublishAssetIdsByPublicLinksEventService(
            _mockMediator.Object,
            mockConfig.Object,
            _mockLogger.Object);

        var item = CreatePublishedItem(publicLinks: new List<string> { $"{_testEndpoint}/asset1" });

        // Act
        var result = await service.GetAssetIdsByPublicLinksAsync(item, CancellationToken.None);

        // Assert
        Assert.Empty(result);
        VerifyLogContains(LogLevel.Warning, "configuration is missing or empty");
    }

    [Fact]
    public async Task GetAssetIdsByPublicLinksAsync_MixedLinks_FiltersOnlyContentHubLinks()
    {
        // Arrange
        var item = CreatePublishedItem(publicLinks: new List<string>
        {
            $"{_testEndpoint}/asset1",
            "https://other-domain.com/asset2",
            $"{_testEndpoint}/asset3",
            "https://another-site.com/asset4"
        });

        SetupMediatorResponse(new List<int> { 1, 2 });

        // Act
        var result = await _service.GetAssetIdsByPublicLinksAsync(item, CancellationToken.None);

        // Assert
        Assert.Equal(2, result.Count);
        VerifyMediatorCalled(expectedLinkCount: 2);
        VerifyLogContains(LogLevel.Debug, "Filtered 2 ContentHub links from 4 total public links");
    }

    [Fact]
    public async Task GetAssetIdsByPublicLinksAsync_NullOrEmptyLinksInList_FiltersThemOut()
    {
        // Arrange
        var item = CreatePublishedItem(publicLinks: new List<string>
        {
            $"{_testEndpoint}/asset1",
            null!,
            "",
            $"{_testEndpoint}/asset2"
        });

        SetupMediatorResponse(new List<int> { 1, 2 });

        // Act
        var result = await _service.GetAssetIdsByPublicLinksAsync(item, CancellationToken.None);

        // Assert
        Assert.Equal(2, result.Count);
        VerifyMediatorCalled(expectedLinkCount: 2);
    }

    [Fact]
    public async Task GetAssetIdsByPublicLinksAsync_ValidLinks_PublishesEventAndReturnsAssetIds()
    {
        // Arrange
        var itemId = Guid.NewGuid();
        var publicLinks = new List<string>
        {
            $"{_testEndpoint}/asset1",
            $"{_testEndpoint}/asset2"
        };
        var expectedAssetIds = new List<int> { 100, 200 };

        var item = CreatePublishedItem(itemId: itemId, publicLinks: publicLinks);
        SetupMediatorResponse(expectedAssetIds);

        // Act
        var result = await _service.GetAssetIdsByPublicLinksAsync(item, CancellationToken.None);

        // Assert
        Assert.Equal(expectedAssetIds, result);
        VerifyMediatorCalled(expectedLinkCount: 2, expectedLinks: publicLinks);
        VerifyLogContains(LogLevel.Information, $"Retrieved 2 asset IDs from 2 ContentHub links for item {itemId}");
    }

    [Fact]
    public async Task GetAssetIdsByPublicLinksAsync_EventReturnsNullAssetIds_ReturnsEmptyList()
    {
        // Arrange
        var item = CreatePublishedItem(publicLinks: new List<string> { $"{_testEndpoint}/asset1" });
        SetupMediatorResponse(assetIds: null);

        // Act
        var result = await _service.GetAssetIdsByPublicLinksAsync(item, CancellationToken.None);

        // Assert
        Assert.Empty(result);
        VerifyLogContains(LogLevel.Information, "Retrieved 0 asset IDs");
    }

    #region Helper Methods

    private PublishedItem CreatePublishedItem(
        Guid? itemId = null,
        string language = "en",
        string itemName = "Test Item",
        int version = 1,
        List<string>? publicLinks = null)
    {
        return PublishedItem.Create(
            itemId: itemId ?? Guid.NewGuid(),
            language: language,
            itemName: itemName,
            version: version,
            publicLinks: publicLinks);
    }

    private void SetupMediatorResponse(List<int>? assetIds)
    {
        _mockMediator
            .Setup(m => m.PublishAsync(It.IsAny<AssetIdsByPublicLinksEvent>(), It.IsAny<CancellationToken>()))
            .Callback<AssetIdsByPublicLinksEvent, CancellationToken>((e, ct) => e.AssetIds = assetIds)
            .Returns(Task.CompletedTask);
    }

    private void VerifyMediatorNeverCalled()
    {
        _mockMediator.Verify(
            m => m.PublishAsync(It.IsAny<AssetIdsByPublicLinksEvent>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    private void VerifyMediatorCalled(int expectedLinkCount, List<string>? expectedLinks = null)
    {
        _mockMediator.Verify(
            m => m.PublishAsync(
                It.Is<AssetIdsByPublicLinksEvent>(e =>
                    e.PublicLinks.Count == expectedLinkCount &&
                    (expectedLinks == null || e.PublicLinks.SequenceEqual(expectedLinks))),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    private void VerifyLogContains(LogLevel level, string message)
    {
        _mockLogger.Verify(
            logger => logger.Log(
                level,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains(message)),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    #endregion
}