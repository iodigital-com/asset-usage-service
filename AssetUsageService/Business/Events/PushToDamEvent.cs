namespace AssetUsageService.Business.Events;

public sealed class PushToDamEvent
{
    public required Guid ItemId { get; init; }
    public required List<int> AssetIds { get; init; }
    public required DamOperation Operation { get; init; }
}

public enum DamOperation
{
    Add,
    Remove
}
