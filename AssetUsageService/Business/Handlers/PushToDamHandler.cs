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
        var contentHubClient = _contentHubConnection.CreateClient();

        foreach (var assetId in assetIds)
        {
            try
            {
                _logger.LogInformation("Pushing add to DAM: asset {AssetId} for item {ItemId}", assetId, itemId);

                var asset = await contentHubClient.Entities.GetAsync(assetId);
                if (asset == null)
                {
                    _logger.LogWarning("Asset {AssetId} not found in Content Hub, skipping", assetId);
                    continue;
                }
                var usageTrackingProperty = GetOrCreateUsageTrackingProperty(asset, assetId);
                usageTrackingProperty[itemId.ToString()] = item.GetUsageTrackingJson();

                asset.SetPropertyValue("UsageTracking", usageTrackingProperty);
                await contentHubClient.Entities.SaveAsync(asset);

                _logger.LogInformation("Successfully pushed add to DAM for asset {AssetId}", assetId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to push add to DAM for item {ItemId}, asset {AssetId}", itemId, assetId);
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
        var contentHubClient = _contentHubConnection.CreateClient();

        foreach (var assetId in assetIds)
        {
            try
            {
                _logger.LogInformation("Pushing remove to DAM: asset {AssetId} for item {ItemId}", assetId, itemId);

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

                var itemTokenToRemove = usageTrackingProperty.SelectToken(itemId.ToString());
                if (itemTokenToRemove != null)
                {
                    itemTokenToRemove.Parent!.Remove();
                    await contentHubClient.Entities.SaveAsync(asset);
                    _logger.LogInformation("Successfully pushed remove to DAM for asset {AssetId}", assetId);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to push remove to DAM for item {ItemId}, asset {AssetId}", itemId, assetId);
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
