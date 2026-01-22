using AssetUsageService.Domain.Data;

namespace AssetUsageService.Infrastructure;

public interface IAssetItemLinkRepository
{
    Task<AssetItemLink> UpsertAssetItemLinkAsync(Guid itemId, string language, List<int> assetIds, int? version, CancellationToken cancellationToken = default);
    Task<AssetItemLink?> GetAssetItemLinkByItemIdAsync(Guid itemId, CancellationToken cancellationToken = default);
    Task<List<int>> GetAssetIdsFromItemIdAsync(Guid itemId, string language, CancellationToken cancellationToken = default);
    Task<List<AssetItemLink>> GetItemIdsByAssetIdAsync(int assetId, CancellationToken cancellationToken = default);
    Task RemoveAssetIdsFromItemAsync(Guid itemId, string language, List<int> assetIds, CancellationToken cancellationToken = default);
    Task AddAssetIdsToItemAsync(Guid itemId, string language, List<int> assetIds, int? version, CancellationToken cancellationToken = default);
    Task RemoveLanguageFromItemAsync(Guid itemId, string language, CancellationToken cancellationToken = default);
    Task RemoveItemAsync(Guid itemId, CancellationToken cancellationToken = default);
}