using Game.Core.Definitions;
using Game.Core.Model;

namespace Game.Application;

public sealed class ShopService
{
    private const decimal SellPriceRatio = 0.5m;
    private readonly GameSession _session;

    public ShopService(GameSession session)
    {
        ArgumentNullException.ThrowIfNull(session);
        _session = session;
    }

    private GameState State => _session.State;

    public ShopView Open(string shopId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(shopId);

        var shop = _session.ContentRepository.GetShop(shopId);
        var products = shop.Products
            .Select((product, index) => (Product: product, Index: index))
            .Where(entry => IsAvailable(entry.Product.Reward))
            .Select(entry => CreateProductView(shop.Id, entry.Index, entry.Product))
            .ToList();

        return new ShopView(shop, products);
    }

    public ShopTransactionResult Buy(
        string shopId,
        int productIndex,
        int quantity = 1,
        ShopCurrencyKind? currencyKind = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(shopId);
        ArgumentOutOfRangeException.ThrowIfNegative(productIndex);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(quantity);

        var shop = _session.ContentRepository.GetShop(shopId);
        if (productIndex >= shop.Products.Count)
        {
            throw new InvalidOperationException($"Shop '{shopId}' has no product at index {productIndex}.");
        }

        var productDefinition = shop.Products[productIndex];
        if (!IsAvailable(productDefinition.Reward))
        {
            return ShopTransactionResult.Failed($"【{_session.RewardGrantService.GetDisplayName(productDefinition.Reward)}】已达等级上限。");
        }

        var product = CreateProductView(shop.Id, productIndex, productDefinition);
        var selectedCurrency = currencyKind ?? product.DefaultCurrencyKind;
        var unitPrice = product.GetUnitPrice(selectedCurrency);
        if (unitPrice is null)
        {
            throw new InvalidOperationException($"Shop product '{product.DisplayName}' cannot be bought with {selectedCurrency}.");
        }

        if (product.RemainingLimit is not null && quantity > product.RemainingLimit.Value)
        {
            return ShopTransactionResult.Failed($"【{product.DisplayName}】已达购买上限。");
        }

        var totalPrice = checked(unitPrice.Value * quantity);
        if (!CanSpend(selectedCurrency, totalPrice))
        {
            return ShopTransactionResult.Failed(selectedCurrency == ShopCurrencyKind.Silver ? "银两不足。" : "元宝不足。");
        }

        if (productDefinition.Reward is SkillMaxLevelRewardDefinition fragment &&
            checked(fragment.Levels * quantity) >
            _session.RewardGrantService.GetRemainingSkillMaxLevelBonus(fragment.SkillKind, fragment.SkillId))
        {
            return ShopTransactionResult.Failed($"【{product.DisplayName}】购买数量超过剩余可提升等级。");
        }

        Spend(selectedCurrency, totalPrice);
        State.Shop.AddPurchasedQuantity(product.PurchaseKey, quantity);
        if (selectedCurrency == ShopCurrencyKind.Silver)
        {
            _session.Events.Publish(new CurrencyChangedEvent());
        }

        _session.RewardGrantService.Apply(_session.RewardGrantService.Resolve(productDefinition.Reward, quantity));
        return ShopTransactionResult.Succeeded(FormatTransactionMessage("买入", product.DisplayName, quantity));
    }

    public ShopTransactionResult Sell(InventoryEntry entry, int quantity = 1)
    {
        ArgumentNullException.ThrowIfNull(entry);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(quantity);

        if (!CanSell(entry.Definition))
        {
            return ShopTransactionResult.Failed($"【{entry.Definition.Name}】不能出售。");
        }

        var unitPrice = GetSellPrice(entry.Definition);

        switch (entry)
        {
            case StackInventoryEntry stack:
                if (quantity > stack.Quantity)
                {
                    return ShopTransactionResult.Failed($"【{entry.Definition.Name}】数量不足。");
                }

                State.Inventory.RemoveItem(stack.Definition, quantity);
                break;

            case EquipmentInstanceInventoryEntry equipment:
                if (quantity != 1)
                {
                    return ShopTransactionResult.Failed("独立装备一次只能出售 1 件。");
                }

                State.Inventory.RemoveEquipmentInstance(equipment.Equipment.Id);
                break;

            default:
                throw new InvalidOperationException($"Unsupported inventory entry type '{entry.GetType().Name}'.");
        }

        var totalPrice = checked(unitPrice * quantity);
        State.Currency.AddSilver(totalPrice);
        _session.Events.Publish(new InventoryChangedEvent());
        _session.Events.Publish(new CurrencyChangedEvent());
        return ShopTransactionResult.Succeeded(FormatTransactionMessage("卖出", entry.Definition.Name, quantity));
    }

    public int GetSellPrice(ItemDefinition item)
    {
        ArgumentNullException.ThrowIfNull(item);
        return item.Price <= 0 ? 0 : Math.Max(1, (int)Math.Floor(item.Price * SellPriceRatio));
    }

    public bool CanSell(ItemDefinition item)
    {
        ArgumentNullException.ThrowIfNull(item);
        return item.Price > 0;
    }

    private ShopProductView CreateProductView(string shopId, int productIndex, ShopProductDefinition product)
    {
        var rewardKey = product.Reward.GetStableKey();
        var purchaseKey = BuildPurchaseKey(shopId, rewardKey);
        var purchasedQuantity = State.Shop.GetPurchasedQuantity(purchaseKey);
        int? remainingLimit = product.PurchaseLimit is null
            ? null
            : Math.Max(0, product.PurchaseLimit.Value - purchasedQuantity);
        var item = product.Reward is ItemRewardDefinition itemReward
            ? _session.ContentRepository.GetItem(itemReward.ItemId)
            : null;
        int? price = product.PremiumPrice is not null && product.Price is null
            ? null
            : product.Price ?? item?.Price;
        var displayName = _session.RewardGrantService.GetDisplayName(product.Reward);
        var (picture, description) = ResolvePresentation(product.Reward, item);

        return new ShopProductView(
            productIndex,
            product,
            item,
            displayName,
            picture,
            description,
            product.Reward is not ItemRewardDefinition,
            purchaseKey,
            price,
            product.PremiumPrice,
            purchasedQuantity,
            remainingLimit);
    }

    private bool IsAvailable(RewardDefinition reward) =>
        reward is not SkillMaxLevelRewardDefinition fragment ||
        _session.RewardGrantService.GetRemainingSkillMaxLevelBonus(fragment.SkillKind, fragment.SkillId) > 0;

    private (string Picture, string Description) ResolvePresentation(
        RewardDefinition reward,
        ItemDefinition? item) =>
        reward switch
        {
            ItemRewardDefinition => (item!.Picture, item.Description),
            YuanbaoRewardDefinition yuanbao => (
                "物品.元宝",
                $"兑换 {yuanbao.Amount} 枚跨存档、跨周目共享的元宝。"),
            SkillMaxLevelRewardDefinition fragment => (
                ResolveSkillIcon(fragment),
                $"立即永久提高【{ResolveSkillName(fragment)}】等级上限 {fragment.Levels} 级。"),
            _ => throw new NotSupportedException($"Unsupported shop reward '{reward.GetType().Name}'."),
        };

    private string ResolveSkillName(SkillMaxLevelRewardDefinition fragment) =>
        fragment.SkillKind switch
        {
            SkillFragmentKind.External => _session.ContentRepository.GetExternalSkill(fragment.SkillId).Name,
            SkillFragmentKind.Internal => _session.ContentRepository.GetInternalSkill(fragment.SkillId).Name,
            _ => throw new ArgumentOutOfRangeException(nameof(fragment.SkillKind), fragment.SkillKind, null),
        };

    private string ResolveSkillIcon(SkillMaxLevelRewardDefinition fragment)
    {
        var icon = fragment.SkillKind switch
        {
            SkillFragmentKind.External => _session.ContentRepository.GetExternalSkill(fragment.SkillId).Icon,
            SkillFragmentKind.Internal => _session.ContentRepository.GetInternalSkill(fragment.SkillId).Icon,
            _ => throw new ArgumentOutOfRangeException(nameof(fragment.SkillKind), fragment.SkillKind, null),
        };
        return string.IsNullOrWhiteSpace(icon) ? "物品.剑谱" : icon;
    }

    private bool CanSpend(ShopCurrencyKind currencyKind, int amount) =>
        currencyKind switch
        {
            ShopCurrencyKind.Silver => State.Currency.CanSpendSilver(amount),
            ShopCurrencyKind.Gold => _session.ProfileService.CanSpendYuanbao(amount),
            _ => throw new ArgumentOutOfRangeException(nameof(currencyKind), currencyKind, null)
        };

    private void Spend(ShopCurrencyKind currencyKind, int amount)
    {
        switch (currencyKind)
        {
            case ShopCurrencyKind.Silver:
                State.Currency.SpendSilver(amount);
                return;
            case ShopCurrencyKind.Gold:
                _session.ProfileService.SpendYuanbao(amount);
                return;
            default:
                throw new ArgumentOutOfRangeException(nameof(currencyKind), currencyKind, null);
        }
    }

    private static string BuildPurchaseKey(string shopId, string rewardKey) =>
        $"{shopId}|{rewardKey}";

    private static string FormatTransactionMessage(string verb, string itemName, int quantity) =>
        quantity == 1
            ? $"{verb}【{itemName}】"
            : $"{verb}【{itemName}】 x{quantity}";
}

public enum ShopCurrencyKind
{
    Silver,
    Gold,
}

public sealed record ShopView(
    ShopDefinition Definition,
    IReadOnlyList<ShopProductView> Products);

public sealed record ShopProductView(
    int ProductIndex,
    ShopProductDefinition Definition,
    ItemDefinition? Item,
    string DisplayName,
    string Picture,
    string Description,
    bool IsSpecial,
    string PurchaseKey,
    int? Price,
    int? PremiumPrice,
    int PurchasedQuantity,
    int? RemainingLimit)
{
    public ShopCurrencyKind DefaultCurrencyKind =>
        Price is not null ? ShopCurrencyKind.Silver : ShopCurrencyKind.Gold;

    public int? GetUnitPrice(ShopCurrencyKind currencyKind) =>
        currencyKind switch
        {
            ShopCurrencyKind.Silver => Price,
            ShopCurrencyKind.Gold => PremiumPrice,
            _ => throw new ArgumentOutOfRangeException(nameof(currencyKind), currencyKind, null)
        };
}

public sealed record ShopTransactionResult(
    bool Success,
    string Message)
{
    public static ShopTransactionResult Succeeded(string message) => new(true, message);

    public static ShopTransactionResult Failed(string message) => new(false, message);
}
