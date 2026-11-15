namespace SlayTheRequiem.SlayTheRequiemCode.Core;

/// <summary>Вкладка лавки (docs/design/14 §4, art spec 4.9).</summary>
public enum ShopTab { Buy, Sell, Services }

/// <summary>
/// Груз до и после покупки или продажи: вес, вместимость, перегруз и его ступень (0 — нет, 1 — 1–5, 2 — 6–10, 3 — больше 10).
/// </summary>
public readonly record struct BurdenPreview(int Before, int After, int Capacity)
{
    public int Delta => After - Before;
    public int ExcessBefore => Math.Max(0, Before - Capacity);
    public int ExcessAfter => Math.Max(0, After - Capacity);
    public int TierBefore => ShopUiRules.OverloadTier(ExcessBefore);
    public int TierAfter => ShopUiRules.OverloadTier(ExcessAfter);
    public bool Worsens => TierAfter > TierBefore;
    public bool Improves => TierAfter < TierBefore;
}

/// <summary>
/// Интерфейс лавки (M6 п. 5, 14 §1 и §4): вкладки «Купить / Продать / Услуги», шкала груза с превью веса покупки и
/// продажи, предупреждение о перегрузе. Чистые правила (зеркало — tools/sim/requiem_rules.py, раздел M6 п. 5);
/// узлы Godot — Shops/ShopTabs.cs.
/// </summary>
public static class ShopUiRules
{
    /// <summary>Ступень перегруза — та же шкала, что у <see cref="BurdenRules.Evaluate"/>: 0 / 1–5 / 6–10 / больше 10.</summary>
    public static int OverloadTier(int excess) => excess switch
    {
        <= 0 => 0,
        <= 5 => 1,
        <= 10 => 2,
        _ => 3,
    };

    public static BurdenPreview Preview(int weight, int capacity, int delta) =>
        new(Math.Max(0, weight), Math.Max(0, weight + delta), Math.Max(0, capacity));

    /// <summary>
    /// Спросить перед покупкой (14 §1 «при перегрузе кнопка „Купить“ предупреждает»): только если покупка что-то весит и
    /// поднимает ступень перегруза — внутри одной ступени и при снятии груза окна нет.
    /// </summary>
    public static bool NeedsOverloadWarning(BurdenPreview preview) => preview.Delta > 0 && preview.Worsens;

    /// <summary>
    /// Доступна ли вкладка: «Купить» — всегда; «Продать» — если есть что продать (снятое снаряжение, реагенты у Травницы);
    /// «Услуги» — если у прилавка есть хоть одна услуга.
    /// </summary>
    public static bool TabEnabled(ShopTab tab, int sellCount, int serviceCount) => tab switch
    {
        ShopTab.Buy => true,
        ShopTab.Sell => sellCount > 0,
        ShopTab.Services => serviceCount > 0,
        _ => false,
    };

    /// <summary>Число на ярлыке вкладки (0 — без числа): у «Продать» — предметы, у «Услуг» — услуги.</summary>
    public static int TabBadge(ShopTab tab, int sellCount, int serviceCount) => tab switch
    {
        ShopTab.Sell => Math.Max(0, sellCount),
        ShopTab.Services => Math.Max(0, serviceCount),
        _ => 0,
    };

    /// <summary>Порядок строк «Продать»: дороже — выше; при равной цене тяжелее — выше; дальше — как в рюкзаке.</summary>
    public static IReadOnlyList<int> SellOrder(IReadOnlyList<(int Value, int Weight)> items) =>
        Enumerable.Range(0, items.Count)
            .OrderByDescending(i => items[i].Value)
            .ThenByDescending(i => items[i].Weight)
            .ThenBy(i => i)
            .ToList();

    /// <summary>Сколько строк списка продажи помещается на экран; остальное — кнопкой «Продать всё» или после продажи.</summary>
    public const int SellRowsVisible = 8;
}
