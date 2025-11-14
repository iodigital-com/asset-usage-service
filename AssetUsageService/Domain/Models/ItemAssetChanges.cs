namespace AssetUsageService.Domain.Models;

public class ItemAssetChanges
{
    public PublishedItem Item { get; set; }
    public List<int>? ToAddAssetIds { get; set; } = new List<int>();
    public List<int>? ToRemoveAssetIds { get; set; } = new List<int>();

}

