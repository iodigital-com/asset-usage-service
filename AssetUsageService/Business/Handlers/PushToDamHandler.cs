using AssetUsageService.Integration;
using Microsoft.Extensions.Logging;

namespace AssetUsageService.Business.Handlers;

public class PushToDamHandler
{

    public readonly ILogger<PushToDamHandler> _logger;

    public PushToDamHandler( ILogger<PushToDamHandler> logger)
    {

        _logger = logger;
    }
    public async Task<bool> PushToDamAsync(int assetId, APIGateway apIGateway, CancellationToken cancellationToken = default)
    {
        var client = await apIGateway.GetContentHubClientAsync(cancellationToken);
        // Implement the logic to push data to DAM using the client and payload
        // This is a placeholder for the actual implementation
        
        var asset = await client.Entities.GetAsync(assetId);
        _logger.LogInformation("{asset}", asset);
        asset.GetProperty("usageTracking");

        return true;
    }
}
