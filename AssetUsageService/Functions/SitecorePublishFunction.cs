using AssetUsageService.Integration;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace AssetUsageService.Functions;

public class SitecorePublishFunction
{
    private readonly MessageHandler _messageHandler;
    private readonly ILogger<SitecorePublishFunction> _logger;

    public SitecorePublishFunction(MessageHandler messageHandler, ILogger<SitecorePublishFunction> logger)
    {
        _messageHandler = messageHandler;
        _logger = logger;
    }

    [Function("SitecorePublishAPI")]
    public async Task<IActionResult> RunAsync([HttpTrigger(AuthorizationLevel.Function, "post")] HttpRequest req, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Received publish request");

            string requestBody = await new StreamReader(req.Body).ReadToEndAsync(cancellationToken);
            _logger.LogDebug("Payload: {Payload}", requestBody);

            await _messageHandler.HandleMessageAsync(requestBody, cancellationToken);

            return new AcceptedResult();
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Error processing request");
            return new BadRequestObjectResult(new { error = exception.Message });
        }
    }
}