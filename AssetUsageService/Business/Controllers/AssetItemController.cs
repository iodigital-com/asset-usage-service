using AssetUsageService.Business.Handlers;
using AssetUsageService.Business.Models;
using AssetUsageService.Business.Services;
using AssetUsageService.Integration;
using AssetUsageService.Integration.Models;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
namespace AssetUsageService.Business.Controllers;

public class AssetItemController
{
    private readonly DeltaCalculationService _deltaCalculationService;
    private readonly ILogger<AssetItemController> _logger;
    private readonly ContentHubConnectionService _contentHubConnection;

    public AssetItemController(DeltaCalculationService deltaCalculationService, ContentHubConnectionService contentHubConnection, ILogger<AssetItemController> logger)
    {
        _deltaCalculationService = deltaCalculationService;
        _contentHubConnection = contentHubConnection;
        _logger = logger;
    }
    public async Task ProcessPublishedItemAsync(PublishedItem publishedItemViewModel, CancellationToken cancellationToken)
    {
       ItemAssetChanges itemAssetChanges = await _deltaCalculationService.CalculateDeltaAsync(publishedItemViewModel.ItemId, publishedItemViewModel.AssetIds, cancellationToken);
        //await pushToDamHandler.PushToDamAsync(35442, _APIGateway, cancellationToken);
        var client = _contentHubConnection.CreateClient();
        foreach(var assetId in itemAssetChanges.ToAddAssetIds)
        {
            try
            {
                _logger.LogInformation("Processing asset {AssetId} for addition to item {ItemId}", 
                    assetId, itemAssetChanges.ItemId);
                
                var asset = await client.Entities.GetAsync(assetId);
                _logger.LogDebug("Retrieved asset {AssetId} from Content Hub", assetId);
                
                var itemsJson = asset.GetPropertyValue<JToken>("UsageTracking");
                JObject usageTracking;
                
                if (itemsJson == null || itemsJson.Type != JTokenType.Object)
                {
                    _logger.LogInformation("UsageTracking property is null or invalid type for asset {AssetId}, creating new JObject", assetId);
                    usageTracking = new JObject();
                }
                else
                {
                    usageTracking = (JObject)itemsJson;
                    _logger.LogDebug("Existing UsageTracking found for asset {AssetId}: {UsageTracking}", 
                        assetId, usageTracking.ToString());
                }
                
                usageTracking[itemAssetChanges.ItemId.ToString()] = "test";
                
                _logger.LogInformation("Adding item {ItemId} to UsageTracking for asset {AssetId}", 
                    itemAssetChanges.ItemId, assetId);
                asset.SetPropertyValue("UsageTracking", usageTracking);
                
                await client.Entities.SaveAsync(asset);
                _logger.LogInformation("Successfully saved asset {AssetId} with updated UsageTracking", assetId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to add item {ItemId} to asset {AssetId} UsageTracking", 
                    itemAssetChanges.ItemId, assetId);
                throw;
            }
        }

        foreach(var assetId in itemAssetChanges.ToRemoveAssetIds)
        {
            try
            {
                _logger.LogInformation("Processing asset {AssetId} for removal of item {ItemId}", assetId, itemAssetChanges.ItemId);
                
                var asset = await client.Entities.GetAsync(assetId);
                _logger.LogDebug("Retrieved asset {AssetId} from Content Hub", assetId);
                
                var itemsJson = asset.GetPropertyValue<JToken>("UsageTracking");
                
                if (itemsJson == null)
                {
                    _logger.LogWarning("UsageTracking property is null for asset {AssetId}, nothing to remove", assetId);
                    continue;
                }
                
                var itemToken = itemsJson.SelectToken(itemAssetChanges.ItemId.ToString());
                if (itemToken != null)
                {
                    _logger.LogInformation("Removing item {ItemId} from UsageTracking for asset {AssetId}", itemAssetChanges.ItemId, assetId);
                    itemToken.Parent.Remove();
                    await client.Entities.SaveAsync(asset);
                    _logger.LogInformation("Successfully saved asset {AssetId} after removing item {ItemId}", assetId, itemAssetChanges.ItemId);
                }
                else
                {
                    _logger.LogWarning("Item {ItemId} not found in UsageTracking for asset {AssetId}, nothing to remove", itemAssetChanges.ItemId, assetId);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to remove item {ItemId} from asset {AssetId} UsageTracking", itemAssetChanges.ItemId, assetId);
                throw;
            }
        }
    }
}
