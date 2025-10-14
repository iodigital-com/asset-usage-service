using Microsoft.Extensions.Configuration;
using MongoDB.Driver;

namespace AssetUsageService.Data;

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
                AssetIds = new List<Guid>
                {
                    Guid.NewGuid(),
                    Guid.Parse("3FFF377C-986E-45A1-A854-2131EC2647C5")
                },
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            },
            new AssetItemLink
            {
                ItemId = Guid.NewGuid(),
                AssetIds = new List<Guid>
                {
                    Guid.NewGuid(),
                    Guid.NewGuid()
                },
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            },
            new AssetItemLink
            {
                ItemId = Guid.NewGuid(),
                AssetIds = new List<Guid>
                {
                    Guid.Parse("3FFF377C-986E-45A1-A854-2131EC2647C5")
                },
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            }
        };

        await AssetItemLinks.InsertManyAsync(seedData);
    }
}
