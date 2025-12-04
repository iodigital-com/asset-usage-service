using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace AssetUsageService.Domain.Models;

public class PublishedItem
{
    public Guid ItemId { get; set; }
    public string? Language { get; set; }
    public string? ItemName { get; set; }
    public int? Version { get; set; }
    public string? ItemPath { get; set; }
    public List<int> AssetIds { get; set; }
    public List<string>? PublicLinks { get; set; }

    private PublishedItem(Guid itemId, string? language, string? itemName, int? version, string? itemPath, List<int> assetIds, List<string>? publicLinks)
    {
        ItemId = itemId;
        Language = language;
        ItemName = itemName;
        Version = version;
        ItemPath = itemPath;
        AssetIds = assetIds;
        PublicLinks = publicLinks;
    }

    [JsonConstructor]
    public PublishedItem()
    {
        AssetIds = new List<int>();
    }

    public static PublishedItem Create(Guid itemId, string? language = null, string? itemName = null, int? version = null, string? itemPath = null, List<int>? assetIds = null, List<string>? publicLinks = null)
    {
        return new PublishedItem(
            itemId,
            language,
            itemName,
            version,
            itemPath,
            assetIds ?? new List<int>(),
            publicLinks);
    }
    public JObject GetUsageTrackingJson()
    {
        return new JObject
        {
            ["itemName"] = ItemName,
            ["itemPath"] = ItemPath,
            ["language"] = Language,
            ["version"] = Version
        };
    }
}

