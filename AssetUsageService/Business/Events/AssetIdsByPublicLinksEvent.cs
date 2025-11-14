namespace AssetUsageService.Business.Events;

public sealed class AssetIdsByPublicLinksEvent
{
    public required List<string> PublicLinks { get; init; }
    public List<int>? AssetIds { get; set; }
}

