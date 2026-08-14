namespace Game.Core.Persistence;

public sealed record ShopStateRecord(
    IReadOnlyList<ShopPurchaseRecord>? Purchases = null);

public sealed record ShopPurchaseRecord(
    string ShopId,
    string ProductId,
    int Quantity);
