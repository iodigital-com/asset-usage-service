namespace AssetUsageService.Business.Events;

public sealed class PushToDamEvent
{
    public required Guid ItemId { get; init; }
    public required List<int> AssetIds { get; init; }
    public required DamUpdateOperation Operation { get; init; }
}

public enum DamUpdateOperation
{
    Add,
    Remove
}
