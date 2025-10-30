using AssetUsageService.Business.Models;
using AssetUsageService.Infrastructure;
using Microsoft.Extensions.Logging;

namespace AssetUsageService.Business.Services;

public class DeltaCalculationService
{
    private readonly IAssetItemLinkRepository _assetItemLinkRepository;
    private readonly ILogger<DeltaCalculationService> _logger;

    public DeltaCalculationService(IAssetItemLinkRepository assetItemLinkRepository, ILogger<DeltaCalculationService> logger)
    {
        _assetItemLinkRepository = assetItemLinkRepository;
        _logger = logger;
    }

    public async Task<ItemAssetChanges> CalculateDeltaAsync(string itemIdString, List<string> assetIdStrings, CancellationToken cancellationToken = default)
    {
        var itemId = ValidateAndParseItemId(itemIdString);
        var newAssetIds = ParseAssetIds(assetIdStrings);

        var currentAssetIds = await GetCurrentAssetIdsAsync(itemId, cancellationToken);
        var itemExists = currentAssetIds.Count > 0;

        var (assetIdsToAdd, assetIdsToRemove) = CalculateDelta(currentAssetIds, newAssetIds);

        await ApplyChangesAsync(itemId, itemExists, newAssetIds, assetIdsToAdd, assetIdsToRemove, cancellationToken);

        return new ItemAssetChanges
        {
            ItemId = itemId,
            ToAddAssetIds = assetIdsToAdd,
            ToRemoveAssetIds = assetIdsToRemove
        };
    }

    private async Task<List<int>> GetCurrentAssetIdsAsync(Guid itemId, CancellationToken cancellationToken)
    {
        var existingAssetItemLink = await _assetItemLinkRepository.GetAssetItemLinkByItemIdAsync(itemId, cancellationToken);

        if (existingAssetItemLink is null)
        {
            return new List<int>();
        }

        return await _assetItemLinkRepository.GetAssetIdsFromItemIdAsync(itemId, cancellationToken);
    }

    private static (List<int> ToAdd, List<int> ToRemove) CalculateDelta(List<int> currentAssetIds, List<int> newAssetIds)
    {
        var toAdd = newAssetIds.Where(id => !currentAssetIds.Contains(id)).ToList();
        var toRemove = currentAssetIds.Where(id => !newAssetIds.Contains(id)).ToList();
        return (toAdd, toRemove);
    }

    private async Task ApplyChangesAsync(Guid itemId, bool itemExists, List<int> newAssetIds, List<int> assetIdsToAdd, List<int> assetIdsToRemove, CancellationToken cancellationToken)
    {
        if (!itemExists && newAssetIds.Count > 0)
        {
            await _assetItemLinkRepository.InsertAssetItemLinkAsync(itemId, newAssetIds, cancellationToken);
        }
        else if (itemExists && newAssetIds.Count > 0)
        {
            await _assetItemLinkRepository.AddAssetIdsToItemAsync(itemId, assetIdsToAdd, cancellationToken);
            await _assetItemLinkRepository.RemoveAssetIdsFromItemAsync(itemId, assetIdsToRemove, cancellationToken);
        }
        else if (itemExists && newAssetIds.Count == 0)
        {
            await _assetItemLinkRepository.RemoveItemAsync(itemId, cancellationToken);
        }
    }

    private static Guid ValidateAndParseItemId(string itemIdString)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(itemIdString);

        if (!Guid.TryParse(itemIdString, out var itemId))
        {
            throw new ArgumentException($"Invalid GUID format: {itemIdString}", nameof(itemIdString));
        }

        return itemId;
    }

    private static List<int> ParseAssetIds(List<string> assetIdStrings)
    {
        return assetIdStrings.Select(int.Parse).ToList();
    }
}