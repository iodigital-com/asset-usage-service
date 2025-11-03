using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AssetUsageService.Domain.Models;

public class PublishedItem
{
    public Guid ItemId { get; private set; }
    public string? Language { get; private set; }
    public string? ItemName { get; private set; }
    public int? Version { get; private set; }
    public string? ItemPath { get; private set; }
    public List<int> AssetIds { get; private set; }
    public string? PublicLink { get; private set; }

    private PublishedItem(Guid itemId,string? language, string? itemName, int? version, string? itemPath, List<int> assetIds, string? publicLink)
    {
        ItemId = itemId;
        Language = language;
        ItemName = itemName;
        Version = version;
        ItemPath = itemPath;
        AssetIds = assetIds;
        PublicLink = publicLink;
    }

    public static PublishedItem Create(Guid itemId, string? language = null, string? itemName = null, int? version = null, string? itemPath = null, List<int>? assetIds = null, string? publicLink = null)
    {
        return new PublishedItem(
            itemId,
            language,
            itemName,
            version,
            itemPath,
            assetIds ?? new List<int>(),
            publicLink);
    }
    public JObject GetUsageTrackingJson()
    {
        return new JObject
        {
            ["itemName"] = ItemName,
            ["itemPath"] = ItemPath,
            ["language"] = Language,
            ["version"] = Version,
        };
    }
}

