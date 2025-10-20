using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using Stylelabs.M.Sdk.WebClient;

namespace AssetUsageService.Integration;

public class APIGateway
{
    private readonly ContentHubConnectionService _contentHubConnection;

    public APIGateway(ContentHubConnectionService contentHubConnection)
    {
        _contentHubConnection = contentHubConnection;
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
    public async Task<IActionResult> SitecorePublishRequest(
       [HttpTrigger(AuthorizationLevel.Function, "post")] HttpRequest req,
       FunctionContext context)
    {
        var logger = context.GetLogger("SitecorePublishAPI");

        string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
        logger.LogInformation("Received payload: {Payload}", requestBody);

        return new AcceptedResult();
    }

}