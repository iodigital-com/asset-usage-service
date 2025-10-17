using AssetUsageService.Data;

namespace AssetUsageService.Infrastructure;

public interface IAssetItemLinkRepository
{
    Task<AssetItemLink> InsertAssetItemLinkAsync(Guid itemId, List<int> assetIds, CancellationToken cancellationToken = default);
    Task<AssetItemLink?> GetAssetItemLinkByItemIdAsync(Guid itemId, CancellationToken cancellationToken = default);
    Task<List<int>> GetAssetIdsFromItemIdAsync(Guid itemId, CancellationToken cancellationToken = default);
    Task<List<AssetItemLink>> GetItemIdsByAssetIdAsync(int assetId, CancellationToken cancellationToken = default);
    Task UpdateAssetsForItemIdAsync(Guid itemId, List<int> assetIds, CancellationToken cancellationToken = default);
}