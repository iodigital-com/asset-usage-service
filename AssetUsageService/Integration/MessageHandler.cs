using AssetUsageService.Integration.ViewModel;
using DnsClient.Internal;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using AssetUsageService.Business.Controllers;

namespace AssetUsageService.Integration;

public class MessageHandler
{
    public ILogger<MessageHandler> _logger;
    private readonly AssetItemController _assetItemController;
    public MessageHandler(ILogger<MessageHandler> logger, AssetItemController assetItemController)
	{
        _logger = logger;
        _assetItemController = assetItemController;
    }
    public async Task HandleMessageAsync(string message, CancellationToken cancellationToken = default)
    {
        var publishedItem = JsonSerializer.Deserialize<PublishedItemViewModel>(message);

        if (publishedItem == null)
        {
            throw new InvalidOperationException("Failed to deserialize published items message");
        }

        publishedItem.AssetIds = FilterAssets(publishedItem.AssetIds);

        _logger.LogInformation("Processing ItemId: {ItemId}, AssetIds: {AssetCount}", publishedItem.ItemId, publishedItem.AssetIds);

        await _assetItemController.ProcessPublishedItemAsync(publishedItem, cancellationToken);
    }
    private List<string> FilterAssets(List<string> assetIds)
    {
        var filteredAssets = new List<string>();

        for (var index = 0; index < assetIds.Count; index++)
        {
            if (index % 2 != 0)
            {
                filteredAssets.Add(assetIds[index]);
            }
        }

        return filteredAssets;
    }
}
