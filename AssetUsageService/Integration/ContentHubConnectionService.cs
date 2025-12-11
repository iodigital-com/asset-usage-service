using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Stylelabs.M.Sdk.WebClient;
using Stylelabs.M.Sdk.WebClient.Authentication;


namespace AssetUsageService.Integration;

public class ContentHubConnectionService : IContentHubConnectionService
{
    private readonly ILogger<ContentHubConnectionService> _logger;
    private readonly HttpClient _httpClient;
    private readonly Uri _contentHubEndpoint;
    private readonly OAuthClientCredentialsGrant _contentHubAuth;

    public ContentHubConnectionService(ILogger<ContentHubConnectionService> logger, IConfiguration configuration, IHttpClientFactory httpClientFactory)
    {
        _logger = logger;
        _httpClient = httpClientFactory.CreateClient(nameof(ContentHubConnectionService));

        var endpointString = configuration["ContentHub:Endpoint"]
            ?? throw new InvalidOperationException("ContentHub:Endpoint configuration is missing");

        if (!Uri.TryCreate(endpointString, UriKind.Absolute, out _contentHubEndpoint))
        {
            throw new InvalidOperationException("ContentHub:Endpoint must be an absolute URI");
        }

        var clientId = configuration["ContentHub:ClientId"]
            ?? throw new InvalidOperationException("ContentHub:ClientId configuration is missing");

        var clientSecret = configuration["ContentHub:ClientSecret"]
            ?? throw new InvalidOperationException("ContentHub:ClientSecret configuration is missing");

        _contentHubAuth = new OAuthClientCredentialsGrant
        {
            ClientId = clientId,
            ClientSecret = clientSecret
        };

        _logger.LogInformation("ContentHub connection initialized for endpoint: {Endpoint}", _contentHubEndpoint);
    }

    public IWebMClient CreateClient()
    {
        try
        {
            var client = MClientFactory.CreateMClient(_contentHubEndpoint, _contentHubAuth);
            _logger.LogDebug("ContentHub client created successfully");
            return client;
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Failed to create ContentHub client");
            throw;
        }
    }

    public async Task<bool> IsReachableAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var healthCheckUri = new Uri(_contentHubEndpoint, "/");
            using var response = await _httpClient.GetAsync(healthCheckUri, cancellationToken);
            var isReachable = response.IsSuccessStatusCode;

            _logger.LogInformation("ContentHub reachability check: {IsReachable} (Status: {StatusCode})",
                isReachable, response.StatusCode);

            return isReachable;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            _logger.LogWarning(exception, "ContentHub endpoint is not reachable");
            return false;
        }
    }
}