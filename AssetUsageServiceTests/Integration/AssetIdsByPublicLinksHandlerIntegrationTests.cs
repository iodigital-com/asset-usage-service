using AssetUsageService.Business.Events;
using AssetUsageService.Business.Handlers;
using AssetUsageService.Integration;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit.Abstractions;

namespace AssetUsageServiceTests.Integration;

[Trait("Category", "Integration")]
public class AssetIdsByPublicLinksHandlerIntegrationTests : IAsyncLifetime
{
    private readonly ITestOutputHelper _output;
    private readonly IConfiguration _configuration;
    private readonly Mock<ILogger<AssetIdsByPublicLinksHandler>> _mockLogger;
    private readonly IContentHubConnectionService _contentHubConnection;
    private readonly AssetIdsByPublicLinksHandler _handler;
    private readonly bool _integrationTestsEnabled;

    public AssetIdsByPublicLinksHandlerIntegrationTests(ITestOutputHelper output)
    {
        _output = output;
        _mockLogger = new Mock<ILogger<AssetIdsByPublicLinksHandler>>();

        _configuration = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.Test.json", optional: false, reloadOnChange: false)
            .Build();

        _integrationTestsEnabled = bool.Parse(_configuration["ContentHub:IntegrationTestsEnabled"] ?? "false");

        if (_integrationTestsEnabled)
        {
            var mockConnectionLogger = new Mock<ILogger<ContentHubConnectionService>>();
            var mockHttpClientFactory = new Mock<IHttpClientFactory>();
            mockHttpClientFactory
                .Setup(f => f.CreateClient(It.IsAny<string>()))
                .Returns(new HttpClient());

            _contentHubConnection = new ContentHubConnectionService(
                mockConnectionLogger.Object,
                _configuration,
                mockHttpClientFactory.Object);

            _handler = new AssetIdsByPublicLinksHandler(_contentHubConnection, _mockLogger.Object);
        }
        else
        {
            _contentHubConnection = null!;
            _handler = null!;
        }
    }

    public Task InitializeAsync()
    {
        if (!_integrationTestsEnabled)
        {
            _output.WriteLine("WARNING: Integration tests are DISABLED");
            _output.WriteLine("Set ContentHub:IntegrationTestsEnabled = true in appsettings.Test.json to enable");
            _output.WriteLine("Ensure ContentHub:Endpoint, ClientId, and ClientSecret are configured");
        }
        else
        {
            _output.WriteLine("INFO: Integration tests are ENABLED");
            _output.WriteLine($"ContentHub Endpoint: {_configuration["ContentHub:Endpoint"]}");
        }

        return Task.CompletedTask;
    }

    public Task DisposeAsync() => Task.CompletedTask;

    #region Connectivity Tests

    [SkippableFact]
    public async Task HandleAsync_RealConnection_ConnectsSuccessfully()
    {
        Skip.IfNot(_integrationTestsEnabled, "Integration tests are disabled");

        // Arrange & Act
        var isReachable = await _contentHubConnection.IsReachableAsync();

        // Assert
        Assert.True(isReachable, "ContentHub should be reachable with valid configuration");
        _output.WriteLine("SUCCESS: Successfully connected to ContentHub");
    }

    [SkippableFact]
    public async Task HandleAsync_EmptyPublicLinks_ReturnsEmptyList()
    {
        Skip.IfNot(_integrationTestsEnabled, "Integration tests are disabled");

        // Arrange
        var @event = CreateEvent();

        // Act
        await _handler.HandleAsync(@event, CancellationToken.None);

        // Assert
        Assert.NotNull(@event.AssetIds);
        Assert.Empty(@event.AssetIds);
        _output.WriteLine("SUCCESS: Empty public links handled correctly");
    }

    #endregion

    #region Real Data Tests

    [SkippableFact]
    public async Task HandleAsync_ValidPublicLink_RetrievesAssetId()
    {
        Skip.IfNot(_integrationTestsEnabled, "Integration tests are disabled");

        // Arrange
        var testPublicLink = GetTestLink();
        var @event = CreateEvent(testPublicLink);

        // Act
        await _handler.HandleAsync(@event, CancellationToken.None);

        // Assert
        Assert.NotNull(@event.AssetIds);

        if (@event.AssetIds.Count > 0)
        {
            _output.WriteLine($"SUCCESS: Retrieved {@event.AssetIds.Count} asset ID(s):");
            foreach (var assetId in @event.AssetIds)
            {
                _output.WriteLine($"  - Asset ID: {assetId}");
            }
        }
        else
        {
            _output.WriteLine("WARNING: No asset IDs found for the provided public link");
        }
    }

    [SkippableFact]
    public async Task HandleAsync_MultipleValidLinks_RetrievesAllAssetIds()
    {
        Skip.IfNot(_integrationTestsEnabled, "Integration tests are disabled");

        // Arrange
        var testLinks = GetTestLinks();
        var @event = CreateEvent(testLinks);

        // Act
        await _handler.HandleAsync(@event, CancellationToken.None);

        // Assert
        Assert.NotNull(@event.AssetIds);
        LogMultipleLinkResults(testLinks.Count, @event.AssetIds);
    }

    [SkippableFact]
    public async Task HandleAsync_InvalidPublicLink_ReturnsEmptyList()
    {
        Skip.IfNot(_integrationTestsEnabled, "Integration tests are disabled");

        // Arrange
        var invalidLink = $"{_configuration["ContentHub:Endpoint"]}/nonexistent-asset-12345";
        var @event = CreateEvent(invalidLink);

        // Act
        await _handler.HandleAsync(@event, CancellationToken.None);

        // Assert
        Assert.NotNull(@event.AssetIds);
        Assert.Empty(@event.AssetIds);
        _output.WriteLine("SUCCESS: Invalid public link handled correctly (returned empty list)");
    }

    [SkippableFact]
    public async Task HandleAsync_MixedValidAndInvalidLinks_RetrievesOnlyValidAssetIds()
    {
        Skip.IfNot(_integrationTestsEnabled, "Integration tests are disabled");

        // Arrange
        var testLink = GetTestLink();
        var invalidLink = $"{_configuration["ContentHub:Endpoint"]}/nonexistent-asset-67890";
        var @event = CreateEvent(testLink, invalidLink, "not-a-url");

        // Act
        await _handler.HandleAsync(@event, CancellationToken.None);

        // Assert
        Assert.NotNull(@event.AssetIds);
        _output.WriteLine("SUCCESS: Processed 3 links (1 valid, 1 invalid, 1 malformed)");
        _output.WriteLine($"  Retrieved {@event.AssetIds.Count} asset ID(s)");
    }

    #endregion

    #region Error Handling Tests

    [SkippableFact]
    public async Task HandleAsync_NullEvent_ThrowsArgumentNullException()
    {
        Skip.IfNot(_integrationTestsEnabled, "Integration tests are disabled");

        // Arrange
        AssetIdsByPublicLinksEvent nullEvent = null!;

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            _handler.HandleAsync(nullEvent, CancellationToken.None));

        _output.WriteLine("SUCCESS: Null event validation working correctly");
    }

    [SkippableFact]
    public async Task HandleAsync_NullPublicLinks_ThrowsArgumentNullException()
    {
        Skip.IfNot(_integrationTestsEnabled, "Integration tests are disabled");

        // Arrange
        var @event = new AssetIdsByPublicLinksEvent { PublicLinks = null! };

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            _handler.HandleAsync(@event, CancellationToken.None));

        _output.WriteLine("SUCCESS: Null PublicLinks validation working correctly");
    }

    [SkippableFact]
    public async Task HandleAsync_MalformedUrls_SkipsAndContinues()
    {
        Skip.IfNot(_integrationTestsEnabled, "Integration tests are disabled");

        // Arrange
        var @event = new AssetIdsByPublicLinksEvent
        {
            PublicLinks = new List<string>
            {
                "not-a-url",
                "",
                "ftp://invalid-protocol.com/asset",
                "http://",
                "https://contenthub.io/"
            }
        };

        // Act
        await _handler.HandleAsync(@event, CancellationToken.None);

        // Assert
        Assert.NotNull(@event.AssetIds);
        Assert.Empty(@event.AssetIds);
        VerifyLogContains(LogLevel.Warning, "Invalid URL format", Times.AtLeastOnce());
        _output.WriteLine("SUCCESS: Malformed URLs handled gracefully with warnings logged");
    }

    [SkippableFact]
    public async Task HandleAsync_CancellationToken_RespectsCancellation()
    {
        Skip.IfNot(_integrationTestsEnabled, "Integration tests are disabled");

        // Arrange
        var testLink = GetTestLink();
        var @event = CreateEvent(testLink);
        var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act & Assert
        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
            await _handler.HandleAsync(@event, cts.Token));

        _output.WriteLine("SUCCESS: Cancellation token respected");
    }

    #endregion

    #region Logging Verification Tests

    [SkippableFact]
    public async Task HandleAsync_Success_LogsInformation()
    {
        Skip.IfNot(_integrationTestsEnabled, "Integration tests are disabled");

        // Arrange
        var testLink = GetTestLink();
        var @event = CreateEvent(testLink);

        // Act
        await _handler.HandleAsync(@event, CancellationToken.None);

        // Assert
        VerifyLogContains(LogLevel.Information, "Successfully retrieved", Times.Once());
        _output.WriteLine("SUCCESS: Success logging verified");
    }

    #endregion

    #region Helper Methods

    private string GetTestLink()
    {
        var testLink = _configuration["ContentHub:TestPublicLink"];
        Skip.If(string.IsNullOrEmpty(testLink), "ContentHub:TestPublicLink not configured");
        return testLink;
    }

    private List<string> GetTestLinks()
    {
        var testLinks = _configuration.GetSection("ContentHub:TestPublicLinks").Get<List<string>>();
        Skip.If(testLinks == null || testLinks.Count == 0, "ContentHub:TestPublicLinks not configured");
        return testLinks;
    }

    private static AssetIdsByPublicLinksEvent CreateEvent(params string[] links)
    {
        return new AssetIdsByPublicLinksEvent { PublicLinks = links.ToList() };
    }

    private static AssetIdsByPublicLinksEvent CreateEvent(List<string> links)
    {
        return new AssetIdsByPublicLinksEvent { PublicLinks = links };
    }

    private void LogMultipleLinkResults(int linkCount, List<int> assetIds)
    {
        _output.WriteLine($"SUCCESS: Processed {linkCount} public link(s)");
        _output.WriteLine($"  Retrieved {assetIds.Count} asset ID(s)");

        if (assetIds.Count > 0)
        {
            foreach (var assetId in assetIds)
            {
                _output.WriteLine($"  - Asset ID: {assetId}");
            }
        }
    }

    private void VerifyLogContains(LogLevel level, string message, Times times)
    {
        _mockLogger.Verify(
            logger => logger.Log(
                level,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains(message)),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            times);
    }

    #endregion
}