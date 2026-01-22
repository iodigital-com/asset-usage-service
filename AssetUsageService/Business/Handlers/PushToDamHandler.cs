using AssetUsageService.Business.Events;
using AssetUsageService.Business.Handlers.interfaces;
using AssetUsageService.Domain.Models;
using AssetUsageService.Integration;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;
using Stylelabs.M.Sdk.Contracts.Base;

namespace AssetUsageService.Business.Handlers;

public sealed class PushToDamHandler : IEventHandler<PushToDamEvent>
{
    private readonly IContentHubConnectionService _contentHubConnection;
    private readonly ILogger<PushToDamHandler> _logger;

    public PushToDamHandler(IContentHubConnectionService contentHubConnection, ILogger<PushToDamHandler> logger)
    {
        _contentHubConnection = contentHubConnection;
        _logger = logger;
    }

    public async Task HandleAsync(PushToDamEvent @event, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(@event);

        if (@event.Operation == DamOperation.Add)
        {
            await PushAddToDamAsync(@event.Item, @event.AssetIds, cancellationToken);
        }
        else
        {
            await PushRemoveToDamAsync(@event.Item, @event.AssetIds, cancellationToken);
        }
    }

    private async Task PushAddToDamAsync(PublishedItem item, List<int> assetIds, CancellationToken cancellationToken)
    {
        if (assetIds.Count == 0)
        {
            return;
        }
        var itemId = item.ItemId;
        var language = item.Language ?? "en";
        var contentHubClient = _contentHubConnection.CreateClient();

        foreach (var assetId in assetIds)
        {
            try
            {
                _logger.LogInformation("Pushing add to DAM: asset {AssetId} for item {ItemId}, language {Language}", assetId, itemId, language);

                var asset = await contentHubClient.Entities.GetAsync(assetId);
                if (asset == null)
                {
                    _logger.LogWarning("Asset {AssetId} not found in Content Hub, skipping", assetId);
                    continue;
                }
                var usageTrackingProperty = GetOrCreateUsageTrackingProperty(asset, assetId);
                
                var itemKey = itemId.ToString();
                if (!usageTrackingProperty.ContainsKey(itemKey))
                {
                    usageTrackingProperty[itemKey] = new JObject
                    {
                        ["itemName"] = item.ItemName,
                        ["itemPath"] = item.ItemPath,
                        ["languages"] = new JArray()
                    };
                }

                var itemObject = (JObject)usageTrackingProperty[itemKey];
                
                itemObject["itemName"] = item.ItemName;
                itemObject["itemPath"] = item.ItemPath;
                
                if (itemObject["languages"] == null || itemObject["languages"].Type != JTokenType.Array)
                {
                    itemObject["languages"] = new JArray();
                }
                
                var languagesArray = (JArray)itemObject["languages"];
                
                var existingLanguageEntry = languagesArray.FirstOrDefault(l => 
                    l["language"]?.ToString() == language) as JObject;
                
                if (existingLanguageEntry != null)
                {
                    existingLanguageEntry["version"] = item.Version;
                }
                else
                {
                    languagesArray.Add(new JObject
                    {
                        ["language"] = language,
                        ["version"] = item.Version
                    });
                }

                asset.SetPropertyValue("UsageTracking", usageTrackingProperty);
                await contentHubClient.Entities.SaveAsync(asset);

                _logger.LogInformation("Successfully pushed add to DAM for asset {AssetId}, language {Language}", assetId, language);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to push add to DAM for item {ItemId}, asset {AssetId}, language {Language}", itemId, assetId, language);
                throw;
            }
        }
    }

    private async Task PushRemoveToDamAsync(PublishedItem item, List<int> assetIds, CancellationToken cancellationToken)
    {
        if (assetIds.Count == 0)
        {
            return;
        }
        var itemId = item.ItemId;
        var language = item.Language ?? "en";
        var contentHubClient = _contentHubConnection.CreateClient();

        foreach (var assetId in assetIds)
        {
            try
            {
                _logger.LogInformation("Pushing remove to DAM: asset {AssetId} for item {ItemId}, language {Language}", assetId, itemId, language);

                var asset = await contentHubClient.Entities.GetAsync(assetId);
                if (asset == null)
                {
                    _logger.LogWarning("Asset {AssetId} not found in Content Hub, skipping", assetId);
                    continue;
                }
                var usageTrackingProperty = asset.GetPropertyValue<JToken>("UsageTracking");

                if (usageTrackingProperty == null)
                {
                    _logger.LogWarning("UsageTracking property is null for asset {AssetId}, nothing to remove", assetId);
                    continue;
                }

                var itemKey = itemId.ToString();
                var itemToken = usageTrackingProperty.SelectToken(itemKey);
                
                if (itemToken == null)
                {
                    _logger.LogWarning("Item {ItemId} not found in UsageTracking for asset {AssetId}, nothing to remove", itemId, assetId);
                    continue;
                }

                var itemObject = (JObject)itemToken;
                
                if (itemObject["languages"] != null && itemObject["languages"].Type == JTokenType.Array)
                {
                    var languagesArray = (JArray)itemObject["languages"];
                    
                    var languageToRemove = languagesArray.FirstOrDefault(l => 
                        l["language"]?.ToString() == language);
                    
                    if (languageToRemove != null)
                    {
                        languageToRemove.Remove();
                        _logger.LogInformation("Removed language {Language} from item {ItemId} in asset {AssetId}", language, itemId, assetId);
                    }
                    
                    if (!languagesArray.Any())
                    {
                        itemObject.Parent!.Remove();
                        _logger.LogInformation("Removed entire item {ItemId} from asset {AssetId} as no languages remain", itemId, assetId);
                    }
                }
                else
                {
                    itemObject.Parent!.Remove();
                    _logger.LogInformation("Removed entire item {ItemId} from asset {AssetId} (no languages array found)", itemId, assetId);
                }

                await contentHubClient.Entities.SaveAsync(asset);
                _logger.LogInformation("Successfully pushed remove to DAM for asset {AssetId}, language {Language}", assetId, language);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to push remove to DAM for item {ItemId}, asset {AssetId}, language {Language}", itemId, assetId, language);
                throw;
            }
        }
    }

    private JObject GetOrCreateUsageTrackingProperty(IEntity asset, int assetId)
    {
        var existingUsageTracking = asset.GetPropertyValue<JToken>("UsageTracking");

        if (existingUsageTracking == null)
        {
            _logger.LogInformation("UsageTracking property is null or invalid type for asset {AssetId}, creating new JObject", assetId);
            return new JObject();
        }

        return (JObject)existingUsageTracking;
    }
}
