using AssetUsageService.Domain.Data;
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

    public async Task<AssetItemLink> UpsertAssetItemLinkAsync(Guid itemId, string language, List<int> assetIds, int? version, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(itemId);
        ArgumentException.ThrowIfNullOrWhiteSpace(language);
     
        var filter = Builders<AssetItemLink>.Filter.Eq(link => link.ItemId, itemId);
        var existingLink = await _dbContext.AssetItemLinks.Find(filter).FirstOrDefaultAsync(cancellationToken);

        if (existingLink == null)
        {
            var newItem = new AssetItemLink
            {
                ItemId = itemId,
                Languages = new Dictionary<string, LanguageAssetData>
                {
                    [language] = new LanguageAssetData
                    {
                        AssetIds = assetIds.Distinct().OrderBy(id => id).ToList(),
                        Version = version
                    }
                },
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            await _dbContext.AssetItemLinks.InsertOneAsync(newItem, cancellationToken: cancellationToken);
            return newItem;
        }

        existingLink.Languages[language] = new LanguageAssetData
        {
            AssetIds = assetIds.Distinct().OrderBy(id => id).ToList(),
            Version = version
        };
        existingLink.UpdatedAt = DateTime.UtcNow;

        var update = Builders<AssetItemLink>.Update
            .Set(link => link.Languages, existingLink.Languages)
            .Set(link => link.UpdatedAt, existingLink.UpdatedAt);

        await _dbContext.AssetItemLinks.UpdateOneAsync(filter, update, cancellationToken: cancellationToken);
        return existingLink;
    }

    public Task<AssetItemLink?> GetAssetItemLinkByItemIdAsync(Guid itemId, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(itemId);
        var filter = Builders<AssetItemLink>.Filter.Eq(link => link.ItemId, itemId);
        return _dbContext.AssetItemLinks.Find(filter).FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<List<int>> GetAssetIdsFromItemIdAsync(Guid itemId, string language, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(itemId);
        ArgumentException.ThrowIfNullOrWhiteSpace(language);
        
        var filter = Builders<AssetItemLink>.Filter.Eq(link => link.ItemId, itemId);
        var link = await _dbContext.AssetItemLinks.Find(filter).FirstOrDefaultAsync(cancellationToken);
        
        if (link == null || !link.Languages.ContainsKey(language))
        {
            return new List<int>();
        }

        return link.Languages[language].AssetIds;
    }

    public async Task<List<AssetItemLink>> GetItemIdsByAssetIdAsync(int assetId, CancellationToken cancellationToken = default)
    {
        var allLinks = await _dbContext.AssetItemLinks.Find(_ => true).ToListAsync(cancellationToken);
        
        return allLinks
            .Where(link => link.Languages.Values.Any(langData => langData.AssetIds.Contains(assetId)))
            .ToList();
    }

    public async Task RemoveAssetIdsFromItemAsync(Guid itemId, string language, List<int> assetIds, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(itemId);
        ArgumentException.ThrowIfNullOrWhiteSpace(language);
        
        var filter = Builders<AssetItemLink>.Filter.Eq(link => link.ItemId, itemId);
        var link = await _dbContext.AssetItemLinks.Find(filter).FirstOrDefaultAsync(cancellationToken);
        
        if (link == null || !link.Languages.ContainsKey(language))
        {
            return;
        }

        var currentAssetIds = link.Languages[language].AssetIds;
        var updatedAssetIds = currentAssetIds.Except(assetIds).ToList();
        
        link.Languages[language].AssetIds = updatedAssetIds;
        link.UpdatedAt = DateTime.UtcNow;

        var update = Builders<AssetItemLink>.Update
            .Set(link => link.Languages, link.Languages)
            .Set(link => link.UpdatedAt, link.UpdatedAt);
            
        await _dbContext.AssetItemLinks.UpdateOneAsync(filter, update, cancellationToken: cancellationToken);
    }
    public async Task AddAssetIdsToItemAsync(Guid itemId, string language, List<int> assetIds, int? version, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(itemId);
        ArgumentException.ThrowIfNullOrWhiteSpace(language);
        
        if (assetIds.Count == 0)
        {
            return;
        }

        var filter = Builders<AssetItemLink>.Filter.Eq(link => link.ItemId, itemId);
        var link = await _dbContext.AssetItemLinks.Find(filter).FirstOrDefaultAsync(cancellationToken);
        
        if (link == null)
        {
            return;
        }

        if (!link.Languages.ContainsKey(language))
        {
            link.Languages[language] = new LanguageAssetData 
            { 
                AssetIds = new List<int>(),
                Version = version
            };
        }

        var currentAssetIds = link.Languages[language].AssetIds;
        var updatedAssetIds = currentAssetIds.Union(assetIds).Distinct().OrderBy(id => id).ToList();
        
        link.Languages[language].AssetIds = updatedAssetIds;
        link.Languages[language].Version = version;
        link.UpdatedAt = DateTime.UtcNow;

        var update = Builders<AssetItemLink>.Update
            .Set(link => link.Languages, link.Languages)
            .Set(link => link.UpdatedAt, link.UpdatedAt);
            
        await _dbContext.AssetItemLinks.UpdateOneAsync(filter, update, cancellationToken: cancellationToken);
    }
    public async Task RemoveLanguageFromItemAsync(Guid itemId, string language, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(itemId);
        ArgumentException.ThrowIfNullOrWhiteSpace(language);
        
        var filter = Builders<AssetItemLink>.Filter.Eq(link => link.ItemId, itemId);
        var link = await _dbContext.AssetItemLinks.Find(filter).FirstOrDefaultAsync(cancellationToken);
        
        if (link == null || !link.Languages.ContainsKey(language))
        {
            return;
        }

        link.Languages.Remove(language);
        link.UpdatedAt = DateTime.UtcNow;

        if (link.Languages.Count == 0)
        {
            await _dbContext.AssetItemLinks.DeleteOneAsync(filter, cancellationToken);
            return;
        }

        var update = Builders<AssetItemLink>.Update
            .Set(link => link.Languages, link.Languages)
            .Set(link => link.UpdatedAt, link.UpdatedAt);
            
        await _dbContext.AssetItemLinks.UpdateOneAsync(filter, update, cancellationToken: cancellationToken);
    }
    
    public Task RemoveItemAsync(Guid itemId, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(itemId);
        var filter = Builders<AssetItemLink>.Filter.Eq(link => link.ItemId, itemId);
        return _dbContext.AssetItemLinks.DeleteOneAsync(filter, cancellationToken);
    }

}