namespace AssetUsageService.Integration.Models;

public class PublishedItem
{
    public string ItemId { get; set; }
    public List<string>? AssetIds { get; set; } = new List<string>();
    public string? PublicLink { get; set; }
}

