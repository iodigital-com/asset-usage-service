using AssetUsageService.Domain.Models;
using Microsoft.Extensions.Logging;

namespace AssetUsageService.Integration.Mappers;

public class PublishedItemMapper
{
    private readonly ILogger<PublishedItemMapper> _logger;

    public PublishedItemMapper(ILogger<PublishedItemMapper> logger)
    {
        _logger = logger;
    }

    public PublishedItem MapToDomain(PublishedItemDto dto)
    {
        ArgumentNullException.ThrowIfNull(dto);

        var itemId = ValidateAndParseItemId(dto.ItemId);
        var assetIds = ParseAssetIds(dto.AssetIds);

        return PublishedItem.Create(
            itemId: itemId,
            language: dto.Language,
            itemName: dto.ItemName,
            version: dto.Version,
            itemPath: dto.ItemPath,
            assetIds: assetIds,
            publicLinks: dto.PublicLink);
    }

    private Guid ValidateAndParseItemId(string itemIdString)
    {
        if (string.IsNullOrWhiteSpace(itemIdString))
        {
            _logger.LogError("ItemId cannot be null or empty");
            throw new ArgumentException("ItemId is required", nameof(itemIdString));
        }

        if (!Guid.TryParse(itemIdString, out var itemId))
        {
            _logger.LogError("Invalid ItemId format: {ItemId}", itemIdString);
            throw new FormatException($"ItemId '{itemIdString}' is not a valid GUID");
        }

        return itemId;
    }

    private List<int> ParseAssetIds(List<string>? assetIdStrings)
    {
        if (assetIdStrings == null || assetIdStrings.Count == 0)
        {
            return new List<int>();
        }

        var parsedAssetIds = new List<int>();
        var invalidAssetIds = new List<string>();

        foreach (var assetIdString in assetIdStrings)
        {
            if (int.TryParse(assetIdString, out var assetId))
            {
                parsedAssetIds.Add(assetId);
            }
            else
            {
                _logger.LogWarning("Failed to parse AssetId: {AssetId}", assetIdString);
                invalidAssetIds.Add(assetIdString);
            }
        }

        if (invalidAssetIds.Count > 0)
        {
            throw new FormatException($"Failed to parse {invalidAssetIds.Count} AssetId(s): {string.Join(", ", invalidAssetIds)}");
        }

        return parsedAssetIds;
    }
}