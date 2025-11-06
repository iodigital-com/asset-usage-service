namespace AssetUsageService.Domain.Models;

public class PublishedItemDto
{
    public required string ItemId { get; set; }
    public string? Language { get; set; }
    public string? ItemName { get; set; }
    public int? Version { get; set; }
    public string? ItemPath { get; set; }
    public List<string>? AssetIds { get; set; } = new List<string>();
    public List<string>? PublicLink { get; set; }
}