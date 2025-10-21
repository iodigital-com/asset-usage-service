using AssetUsageService.Infrastructure;
using Microsoft.Extensions.Logging;

namespace AssetUsageService.Business.Services;

public class DeltaCalculationService
{
    private IAssetItemLinkRepository _assetItemLinkRepository;
    private ILogger<DeltaCalculationService> _logger;
    public DeltaCalculationService(IAssetItemLinkRepository assetItemLinkRepository, ILogger<DeltaCalculationService> logger)
    {
        _assetItemLinkRepository = assetItemLinkRepository;
        _logger = logger;
    }

    public async Task CalculateDeltaAsync(string itemIdString, List<string> assetIdStrings, CancellationToken cancellationToken = default)
    {
        var itemId = Guid.Parse(itemIdString);
        var itemExists = await _assetItemLinkRepository.GetAssetItemLinkByItemIdAsync(itemId) != null ? true : false;
        var assetIds = assetIdStrings.Select(id => int.Parse(id)).ToList();
        if (!itemExists && assetIds.Any())
        {
            await _assetItemLinkRepository.InsertAssetItemLinkAsync(itemId, assetIds, cancellationToken);
        } else if (itemExists && assetIds.Any())
        {
            var currentAssetIds = await _assetItemLinkRepository.GetAssetIdsFromItemIdAsync(itemId, cancellationToken);
            var newAssetIds = assetIds;

            List<int> toAddAssetIds = newAssetIds.Where(id => !currentAssetIds.Contains(id)).ToList();
            await _assetItemLinkRepository.AddAssetIdsToItemAsync(itemId, toAddAssetIds, cancellationToken);

            List<int> toRemoveAssetIds = currentAssetIds.Where(id => !newAssetIds.Contains(id)).ToList();
            await _assetItemLinkRepository.RemoveAssetIdsFromItemAsync(itemId, toRemoveAssetIds, cancellationToken);

        } else if (itemExists && !assetIds.Any())
        {
            await _assetItemLinkRepository.RemoveItemAsync(itemId, cancellationToken);
        }
    }
}
