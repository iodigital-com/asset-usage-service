using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace AssetUsageService.Data;

public class AssetItemLink
{
    [BsonId]
    [BsonRepresentation(BsonType.String)]
    public Guid ItemId { get; set; }

    [BsonElement("assetIds")]
    [BsonRepresentation(BsonType.String)]
    public List<int> AssetIds { get; set; } = new();

    [BsonElement("createdAt")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [BsonElement("updatedAt")]
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
