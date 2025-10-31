namespace AssetUsageService.Business.Models;
public class ItemAssetChanges
{
    public Guid ItemId { get; set; }
    public List<int>? ToAddAssetIds { get; set; } = new List<int>();
    public List<int>? ToRemoveAssetIds { get; set; } = new List<int>();

}

