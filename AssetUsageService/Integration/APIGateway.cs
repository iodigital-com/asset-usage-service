using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using Stylelabs.M.Sdk.WebClient;

namespace AssetUsageService.Integration;

public class APIGateway
{
    private readonly IContentHubConnectionService _contentHubConnection;
    private readonly MessageHandler _messageHandler;
    private readonly ILogger<APIGateway> _logger;
    
    public APIGateway(IContentHubConnectionService contentHubConnection, MessageHandler messageHandler, ILogger<APIGateway> logger)
    {
        _contentHubConnection = contentHubConnection;
        _messageHandler = messageHandler;
        _logger = logger;
    }

    public Task<IWebMClient> GetContentHubClientAsync(CancellationToken cancellationToken = default)
    {
        var client = _contentHubConnection.CreateClient();
        return Task.FromResult(client);
    }

    public Task<bool> IsContentHubReachableAsync(CancellationToken cancellationToken = default)
    {
        return _contentHubConnection.IsReachableAsync(cancellationToken);
    }

    [Function("SitecorePublishAPI")]
    public async Task<IActionResult> SitecorePublishRequest([HttpTrigger(AuthorizationLevel.Function, "post")] HttpRequest req, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Received publish request");

            string requestBody = await new StreamReader(req.Body).ReadToEndAsync(cancellationToken);
            _logger.LogDebug("Payload: {Payload}", requestBody);

            await _messageHandler.HandleMessageAsync(requestBody, cancellationToken);

            return new AcceptedResult();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing request");
            return new BadRequestObjectResult(new { error = ex.Message });
        }
    }

}