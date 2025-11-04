using DnsClient.Internal;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using AssetUsageService.Business.Controllers;
using AssetUsageService.Domain.Models;
using AssetUsageService.Integration.Mappers;

namespace AssetUsageService.Integration;

public class MessageHandler
{
    public ILogger<MessageHandler> _logger;
    private readonly AssetItemController _assetItemController;
    private readonly PublishedItemMapper _publishedItemMapper;
    public MessageHandler(ILogger<MessageHandler> logger, AssetItemController assetItemController, PublishedItemMapper publishedItemMapper)
	{
        _logger = logger;
        _assetItemController = assetItemController;
        _publishedItemMapper = publishedItemMapper;
    }
    public async Task HandleMessageAsync(string message, CancellationToken cancellationToken = default)
    {
        var publishedItemDto = JsonSerializer.Deserialize<PublishedItemDto>(message);

        if (publishedItemDto == null)
        {
            throw new InvalidOperationException("Failed to deserialize published items message");
        }

        publishedItemDto.AssetIds = GetAssetIdsFromPairs(publishedItemDto.AssetIds);

        _logger.LogInformation("Processing ItemId: {ItemId}, AssetIds: {AssetCount}", publishedItemDto.ItemId, publishedItemDto.AssetIds);
        var domainPublishedItem = _publishedItemMapper.MapToDomain(publishedItemDto);
        await _assetItemController.ProcessPublishedItemAsync(domainPublishedItem, cancellationToken);
    }
    private List<string> GetAssetIdsFromPairs(List<string> assetIds)
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
