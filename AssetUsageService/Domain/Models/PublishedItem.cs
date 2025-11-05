using Newtonsoft.Json.Linq;

namespace AssetUsageService.Domain.Models;

public class PublishedItem
{
    public Guid ItemId { get; private set; }
    public string? Language { get; private set; }
    public string? ItemName { get; private set; }
    public int? Version { get; private set; }
    public string? ItemPath { get; private set; }
    public List<int> AssetIds { get; private set; }
    public List<string>? PublicLinks { get; private set; }

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

