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
        var itemId = Guid.Parse(itemIdString);
        var itemExists = await _assetItemLinkRepository.GetAssetItemLinkByItemIdAsync(itemId) != null ? true : false;
        var assetIds = assetIdStrings.Select(id => int.Parse(id)).ToList();

        List<int> toAddAssetIds = new List<int>();
        List<int> toRemoveAssetIds = new List<int>();

        if (!itemExists && assetIds.Count > 0)
        {
            toAddAssetIds = assetIds;
            await _assetItemLinkRepository.InsertAssetItemLinkAsync(itemId, assetIds, cancellationToken);
        } else if (itemExists && assetIds.Count > 0)
        {
            var currentAssetIds = await _assetItemLinkRepository.GetAssetIdsFromItemIdAsync(itemId, cancellationToken);
            var newAssetIds = assetIds;

            toAddAssetIds = newAssetIds.Where(id => !currentAssetIds.Contains(id)).ToList();
            await _assetItemLinkRepository.AddAssetIdsToItemAsync(itemId, toAddAssetIds, cancellationToken);

            toRemoveAssetIds = currentAssetIds.Where(id => !newAssetIds.Contains(id)).ToList();
            await _assetItemLinkRepository.RemoveAssetIdsFromItemAsync(itemId, toRemoveAssetIds, cancellationToken);

        } else if (itemExists && assetIds.Count == 0)
        {
            toRemoveAssetIds = await _assetItemLinkRepository.GetAssetIdsFromItemIdAsync(itemId, cancellationToken);
            await _assetItemLinkRepository.RemoveItemAsync(itemId, cancellationToken);
        }
        return new ItemAssetChanges { ItemId = itemId, ToAddAssetIds = toAddAssetIds, ToRemoveAssetIds = toRemoveAssetIds};
    }
}
