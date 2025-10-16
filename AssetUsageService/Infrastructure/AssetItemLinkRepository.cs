using AssetUsageService.Data;
using MongoDB.Driver;

namespace AssetUsageService.Infrastructure;

public class AssetItemLinkRepository : IAssetItemLinkRepository
{
    private readonly DBContext _dbContext;

    public AssetItemLinkRepository(DBContext dbContext)
    {
        ArgumentNullException.ThrowIfNull(dbContext);
        _dbContext = dbContext;
    }

    public async Task<AssetItemLink> InsertAssetItemLinkAsync(Guid itemId, List<int> assetIds, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(itemId);
     
            var newItem = new AssetItemLink
            {
                ItemId = itemId,
                AssetIds = assetIds.Distinct().OrderBy(id => id).ToList(),
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

        await _dbContext.AssetItemLinks.InsertOneAsync(newItem, cancellationToken: cancellationToken);
        return newItem;
    }
}