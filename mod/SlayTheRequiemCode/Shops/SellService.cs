using MegaCrit.Sts2.Core.Entities.Players;
using SlayTheRequiem.SlayTheRequiemCode.Core;
using SlayTheRequiem.SlayTheRequiemCode.Equipment;
using SlayTheRequiem.SlayTheRequiemCode.Rules;

namespace SlayTheRequiem.SlayTheRequiemCode.Shops;

/// <summary>
/// Продажа (docs/design/14 §1): снаряжение из рюкзака (снятое, только тянет Груз) — за 35 % цены, при доверии 3 — за 50 %.
/// Ванильная сцена магазина не имеет слота «продать», поэтому продажа — карта «Продать снятое» на прилавке
/// (<see cref="ShopServices"/>): весь рюкзак разом. Карты не продаются (их удаляют).
/// Выполняется синхронным действием <see cref="ShopAction.Sell"/> — в коопе предметы исчезают и золото приходит
/// у продавца на всех машинах.
/// </summary>
public static class SellService
{
    public static IReadOnlyList<EquipmentRelic> Sellable(Player player) => Loadout.All(player).Where(r => r.IsStowed).ToList();

    /// <summary>Выручка за предмет: обычная лавка — 35 / 50 %, Скупщик краденого — 60 % (14 §2.7).</summary>
    public static int Value(EquipmentRelic item, int trust, bool fence = false) =>
        fence ? ShopRules.FenceSellValue(item.MerchantCost) : ShopRules.SellValue(item.MerchantCost, trust);

    public static Task Sell(Player player, IReadOnlyList<EquipmentRelic> items, int trust, bool fence = false)
    {
        // Состав рюкзака мог измениться, пока экран был открыт.
        var still = Sellable(player).Where(i => items.Contains(i)).ToList();
        if (still.Count == 0) return Task.CompletedTask;
        if (MerchantLedger.Of(player) is not { } ledger) return Task.CompletedTask;
        var gold = still.Sum(i => Value(i, ledger.CurrentTrust, ledger.CurrentShop == ShopType.Fence));
        var ids = string.Join(",", still.Select(InventoryIdentity.RelicKey));
        return ShopActions.Perform(player, ShopAction.Sell, gold, 0, ids);
    }
}
