using AssetUsageService.Data;
using MongoDB.Bson;
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

    public Task<AssetItemLink?> GetAssetItemLinkByItemIdAsync(Guid itemId, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(itemId);
        var filter = Builders<AssetItemLink>.Filter.Eq(link => link.ItemId, itemId);
        return _dbContext.AssetItemLinks.Find(filter).FirstOrDefaultAsync(cancellationToken);
    }

    public Task<List<int>> GetAssetIdsFromItemIdAsync(Guid itemId, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(itemId);
        var filter = Builders<AssetItemLink>.Filter.Eq(link => link.ItemId, itemId);
        var link = _dbContext.AssetItemLinks.Find(filter).FirstAsync(cancellationToken);
        var assets = link.Result?.AssetIds;
        return Task.FromResult(assets);
    }

    public Task<List<AssetItemLink>> GetItemIdsByAssetIdAsync(int assetId, CancellationToken cancellationToken = default)
    {
        var filter = Builders<AssetItemLink>.Filter.AnyEq(link => link.AssetIds, assetId);
        return _dbContext.AssetItemLinks.Find(filter).ToListAsync(cancellationToken);
    }
}