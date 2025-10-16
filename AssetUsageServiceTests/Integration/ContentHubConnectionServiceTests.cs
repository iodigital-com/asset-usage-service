using AssetUsageService.Integration;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using Moq.Protected;
using System.Net;

namespace AssetUsageServiceTests.Integration;

public class ContentHubConnectionServiceTests
{
    private readonly Mock<ILogger<ContentHubConnectionService>> _mockLogger;
    private readonly Mock<IConfiguration> _mockConfiguration;
    private readonly Mock<IHttpClientFactory> _mockHttpClientFactory;
    private readonly Mock<HttpMessageHandler> _mockHttpMessageHandler;

    public ContentHubConnectionServiceTests()
    {
        _mockLogger = new Mock<ILogger<ContentHubConnectionService>>();
        _mockConfiguration = new Mock<IConfiguration>();
        _mockHttpClientFactory = new Mock<IHttpClientFactory>();
        _mockHttpMessageHandler = new Mock<HttpMessageHandler>();
    }

    private void SetupValidConfiguration()
    {
        _mockConfiguration.Setup(config => config["ContentHub:Endpoint"]).Returns("https://contenthub.example.com");
        _mockConfiguration.Setup(config => config["ContentHub:ClientId"]).Returns("test-client-id");
        _mockConfiguration.Setup(config => config["ContentHub:ClientSecret"]).Returns("test-client-secret");
    }

    private HttpClient CreateMockHttpClient()
    {
        var httpClient = new HttpClient(_mockHttpMessageHandler.Object)
        {
            BaseAddress = new Uri("https://contenthub.example.com")
        };
        _mockHttpClientFactory.Setup(clientFactory => clientFactory.CreateClient(nameof(ContentHubConnectionService)))
            .Returns(httpClient);
        return httpClient;
    }

    #region Constructor Tests

    [Fact]
    public void Constructor_WithValidConfiguration_ShouldInitializeSuccessfully()
    {
        // Arrange
        SetupValidConfiguration();
        CreateMockHttpClient();

        // Act
        var service = new ContentHubConnectionService(_mockLogger.Object, _mockConfiguration.Object, _mockHttpClientFactory.Object);

        // Assert
        Assert.NotNull(service);
    }

    [Theory]
    [InlineData(null, "test-client-id", "test-client-secret", "ContentHub:Endpoint configuration is missing")]
    [InlineData("https://contenthub.example.com", null, "test-client-secret", "ContentHub:ClientId configuration is missing")]
    [InlineData("https://contenthub.example.com", "test-client-id", null, "ContentHub:ClientSecret configuration is missing")]
    public void Constructor_WithMissingConfiguration_ShouldThrowInvalidOperationException(string? endpoint, string? clientId, string? clientSecret, string expectedMessage)
    {
        // Arrange
        _mockConfiguration.Setup(config => config["ContentHub:Endpoint"]).Returns(endpoint);
        _mockConfiguration.Setup(config => config["ContentHub:ClientId"]).Returns(clientId);
        _mockConfiguration.Setup(config => config["ContentHub:ClientSecret"]).Returns(clientSecret);
        CreateMockHttpClient();

        // Act & Assert
        var exception = Assert.Throws<InvalidOperationException>(() =>
            new ContentHubConnectionService(_mockLogger.Object, _mockConfiguration.Object, _mockHttpClientFactory.Object));

        Assert.Equal(expectedMessage, exception.Message);
    }

    [Fact]
    public void Constructor_WithInvalidEndpointUri_ShouldThrowInvalidOperationException()
    {
        // Arrange
        _mockConfiguration.Setup(config => config["ContentHub:Endpoint"]).Returns("not-a-valid-uri");
        _mockConfiguration.Setup(config => config["ContentHub:ClientId"]).Returns("test-client-id");
        _mockConfiguration.Setup(config => config["ContentHub:ClientSecret"]).Returns("test-client-secret");
        CreateMockHttpClient();

        // Act & Assert
        var exception = Assert.Throws<InvalidOperationException>(() =>
            new ContentHubConnectionService(_mockLogger.Object, _mockConfiguration.Object, _mockHttpClientFactory.Object));

        Assert.Equal("ContentHub:Endpoint must be an absolute URI", exception.Message);
    }

    #endregion

    #region CreateClient Tests

    [Fact]
    public void CreateClient_WithValidConfiguration_ShouldReturnClient()
    {
        // Arrange
        SetupValidConfiguration();
        CreateMockHttpClient();
        var service = new ContentHubConnectionService(_mockLogger.Object, _mockConfiguration.Object, _mockHttpClientFactory.Object);

        // Act
        var client = service.CreateClient();

        // Assert
        Assert.NotNull(client);
    }

    #endregion

    #region IsReachableAsync Tests

    [Theory]
    [InlineData(HttpStatusCode.OK, true)]
    [InlineData(HttpStatusCode.NoContent, true)]
    [InlineData(HttpStatusCode.NotFound, false)]
    [InlineData(HttpStatusCode.InternalServerError, false)]
    [InlineData(HttpStatusCode.Unauthorized, false)]
    public async Task IsReachableAsync_WithDifferentStatusCodes_ShouldReturnExpectedResult(HttpStatusCode statusCode, bool expectedResult)
    {
        // Arrange
        SetupValidConfiguration();
        CreateMockHttpClient();

        _mockHttpMessageHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = statusCode
            });

        var service = new ContentHubConnectionService(_mockLogger.Object, _mockConfiguration.Object, _mockHttpClientFactory.Object);

        // Act
        var result = await service.IsReachableAsync();

        // Assert
        Assert.Equal(expectedResult, result);
    }

    [Fact]
    public async Task IsReachableAsync_WhenHttpRequestThrowsException_ShouldReturnFalse()
    {
        // Arrange
        SetupValidConfiguration();
        CreateMockHttpClient();

        _mockHttpMessageHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ThrowsAsync(new HttpRequestException("Network error"));

        var service = new ContentHubConnectionService(_mockLogger.Object, _mockConfiguration.Object, _mockHttpClientFactory.Object);

        // Act
        var result = await service.IsReachableAsync();

        // Assert
        Assert.False(result);
        _mockLogger.Verify(
            logger => logger.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("ContentHub endpoint is not reachable")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task IsReachableAsync_WithCancellationToken_ShouldHandleCancellation()
    {
        // Arrange
        SetupValidConfiguration();
        CreateMockHttpClient();
        var cts = new CancellationTokenSource();
        cts.Cancel();

        _mockHttpMessageHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ThrowsAsync(new TaskCanceledException());

        var service = new ContentHubConnectionService(_mockLogger.Object, _mockConfiguration.Object, _mockHttpClientFactory.Object);

        // Act
        var result = await service.IsReachableAsync(cts.Token);

        // Assert
        Assert.False(result);
    }

    #endregion
}