using Game.Core.Persistence;

namespace Game.Core.Model;

public sealed class ShopState
{
    private readonly Dictionary<ShopProductKey, int> _purchasedQuantities = [];

    public IReadOnlyDictionary<ShopProductKey, int> PurchasedQuantities => _purchasedQuantities;

    public static ShopState Restore(ShopStateRecord? record)
    {
        var state = new ShopState();
        if (record is null)
        {
            return state;
        }

        foreach (var purchase in record.Purchases ?? [])
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(purchase.ShopId);
            ArgumentException.ThrowIfNullOrWhiteSpace(purchase.ProductId);
            ArgumentOutOfRangeException.ThrowIfNegative(purchase.Quantity);
            if (purchase.Quantity > 0)
            {
                state._purchasedQuantities.Add(
                    new ShopProductKey(purchase.ShopId, purchase.ProductId),
                    purchase.Quantity);
            }
        }

        return state;
    }

    public int GetPurchasedQuantity(string shopId, string productId)
    {
        var key = CreateKey(shopId, productId);
        return _purchasedQuantities.GetValueOrDefault(key);
    }

    public void AddPurchasedQuantity(string shopId, string productId, int quantity)
    {
        var key = CreateKey(shopId, productId);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(quantity);
        _purchasedQuantities[key] = checked(GetPurchasedQuantity(shopId, productId) + quantity);
    }

    public ShopStateRecord ToRecord() =>
        new(_purchasedQuantities
            .OrderBy(static entry => entry.Key.ShopId, StringComparer.Ordinal)
            .ThenBy(static entry => entry.Key.ProductId, StringComparer.Ordinal)
            .Select(static entry => new ShopPurchaseRecord(
                entry.Key.ShopId,
                entry.Key.ProductId,
                entry.Value))
            .ToArray());

    private static ShopProductKey CreateKey(string shopId, string productId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(shopId);
        ArgumentException.ThrowIfNullOrWhiteSpace(productId);
        return new ShopProductKey(shopId, productId);
    }
}

public readonly record struct ShopProductKey(string ShopId, string ProductId);
