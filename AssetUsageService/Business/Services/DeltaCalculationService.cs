using AssetUsageService.Domain.Models;
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

    public async Task<ItemAssetChanges> CalculateDeltaAsync(PublishedItem item, List<int> newAssetIds, CancellationToken cancellationToken = default)
    {
        var itemId = item.ItemId;
        var language = item.Language ?? "en";
        
        var currentAssetIds = await GetCurrentAssetIdsAsync(itemId, language, cancellationToken);
        var itemExists = currentAssetIds.Count > 0;

        var (assetIdsToAdd, assetIdsToRemove) = CalculateDelta(currentAssetIds, newAssetIds);

        await ApplyChangesAsync(itemId, language, item.Version, itemExists, newAssetIds, assetIdsToAdd, assetIdsToRemove, cancellationToken);

        return new ItemAssetChanges
        {
            Item = item,
            ToAddAssetIds = assetIdsToAdd,
            ToRemoveAssetIds = assetIdsToRemove
        };
    }

    private async Task<List<int>> GetCurrentAssetIdsAsync(Guid itemId, string language, CancellationToken cancellationToken)
    {
        var existingAssetItemLink = await _assetItemLinkRepository.GetAssetItemLinkByItemIdAsync(itemId, cancellationToken);

        if (existingAssetItemLink is null)
        {
            return new List<int>();
        }

        return await _assetItemLinkRepository.GetAssetIdsFromItemIdAsync(itemId, language, cancellationToken);
    }

    private static (List<int> ToAdd, List<int> ToRemove) CalculateDelta(List<int> currentAssetIds, List<int> newAssetIds)
    {
        var toAdd = newAssetIds.Where(id => !currentAssetIds.Contains(id)).ToList();
        var toRemove = currentAssetIds.Where(id => !newAssetIds.Contains(id)).ToList();
        return (toAdd, toRemove);
    }

    private async Task ApplyChangesAsync(Guid itemId, string language, int? version, bool itemExists, List<int> newAssetIds, List<int> assetIdsToAdd, List<int> assetIdsToRemove, CancellationToken cancellationToken)
    {
        if (newAssetIds.Count > 0)
        {
            if (!itemExists)
            {
                await _assetItemLinkRepository.UpsertAssetItemLinkAsync(itemId, language, newAssetIds, version, cancellationToken);
            }
            else
            {
                await _assetItemLinkRepository.AddAssetIdsToItemAsync(itemId, language, assetIdsToAdd, version, cancellationToken);
                await _assetItemLinkRepository.RemoveAssetIdsFromItemAsync(itemId, language, assetIdsToRemove, cancellationToken);
            }
        }
        else if (itemExists)
        {
            await _assetItemLinkRepository.RemoveLanguageFromItemAsync(itemId, language, cancellationToken);
        }
    }
}