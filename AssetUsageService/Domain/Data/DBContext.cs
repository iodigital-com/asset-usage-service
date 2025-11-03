using Microsoft.Extensions.Configuration;
using MongoDB.Driver;

namespace AssetUsageService.Domain.Data;

public class DBContext
{
    private readonly IMongoDatabase _database;

    public DBContext(IMongoClient mongoClient, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(mongoClient);
        ArgumentNullException.ThrowIfNull(configuration);

        var databaseName = configuration["MongoDB:DatabaseName"]
            ?? throw new InvalidOperationException("MongoDB:DatabaseName configuration is missing");

        _database = mongoClient.GetDatabase(databaseName);
    }

    public IMongoCollection<AssetItemLink> AssetItemLinks => _database.GetCollection<AssetItemLink>("AssetItemLinks");

    public async Task SeedDataAsync()
    {
        var existingCount = await AssetItemLinks.CountDocumentsAsync(FilterDefinition<AssetItemLink>.Empty);
        if (existingCount > 0)
        {
            return; 
        }

        var seedData = new List<AssetItemLink>
        {
            new AssetItemLink
            {
                ItemId = Guid.NewGuid(),
                AssetIds = new List<int>{34013, 13343},
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            },
            new AssetItemLink
            {
                ItemId = Guid.NewGuid(),
                AssetIds = new List<int>{23213, 34013},
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            },
            new AssetItemLink
            {
                ItemId = Guid.NewGuid(),
                AssetIds = new List<int>{34323, 34013, 32432},
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            }
        };

        await AssetItemLinks.InsertManyAsync(seedData);
    }
}
