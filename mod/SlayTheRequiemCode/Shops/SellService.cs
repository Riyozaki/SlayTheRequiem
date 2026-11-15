using MegaCrit.Sts2.Core.Entities.Players;
using SlayTheRequiem.SlayTheRequiemCode.Core;
using SlayTheRequiem.SlayTheRequiemCode.Equipment;

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

    public static int Value(EquipmentRelic item, int trust) => ShopRules.SellValue(item.MerchantCost, trust);

    public static Task Sell(Player player, IReadOnlyList<EquipmentRelic> items, int trust)
    {
        // Состав рюкзака мог измениться, пока экран был открыт.
        var still = Sellable(player).Where(i => items.Contains(i)).ToList();
        if (still.Count == 0) return Task.CompletedTask;
        var gold = still.Sum(i => Value(i, trust));
        var ids = string.Join(",", still.Select(i => i.Id.Entry));
        return ShopActions.Perform(player, ShopAction.Sell, gold, 0, ids);
    }
}
