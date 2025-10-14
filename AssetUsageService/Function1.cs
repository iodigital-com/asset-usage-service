using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using Stylelabs.M.Sdk.WebClient;
using Stylelabs.M.Sdk.WebClient.Authentication;

namespace AssetUsageService;

public class Function1
{
    private readonly ILogger<Function1> _logger;

    public Function1(ILogger<Function1> logger)
    {
        _logger = logger;
    }

    [Function("Function1")]
    public IActionResult Run([HttpTrigger(AuthorizationLevel.Function, "get", "post")] HttpRequest req)
    {

        // Vervang deze waarden door jouw eigen gegevens
        var endpoint = new Uri("stage-pasha-darlon-2.sitecoresandbox.cloud");

        var oauth = new OAuthClientCredentialsGrant
        {
            ClientId = "AssetServiceClient",
            ClientSecret = "E3TdCedrmWoPeqMti3L7"
        };

        var client = MClientFactory.CreateMClient(endpoint, oauth);
        var e = client.Assets.ToString();

        _logger.LogInformation("C# HTTP trigger function processed a request.");
        return new OkObjectResult("Welcome to Azure Functions!");
    }
}