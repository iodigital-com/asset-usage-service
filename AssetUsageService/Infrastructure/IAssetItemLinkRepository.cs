using AssetUsageService.Data;

namespace AssetUsageService.Infrastructure;

public interface IAssetItemLinkRepository
{
    Task<AssetItemLink> InsertAssetItemLinkAsync(Guid itemId, List<int> assetIds, CancellationToken cancellationToken = default);
}