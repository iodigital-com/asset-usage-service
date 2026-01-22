using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace AssetUsageService.Domain.Data;

public class AssetItemLink
{
    [BsonId]
    [BsonRepresentation(BsonType.String)]
    public Guid ItemId { get; set; }

    [BsonElement("languages")]
    public Dictionary<string, LanguageAssetData> Languages { get; set; } = new();

    [BsonElement("createdAt")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [BsonElement("updatedAt")]
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

public class LanguageAssetData
{
    [BsonElement("assetIds")]
    public List<int> AssetIds { get; set; } = new();

    [BsonElement("version")]
    public int? Version { get; set; }
}
