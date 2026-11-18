namespace SlayTheRequiem.SlayTheRequiemCode.Core;

/// <summary>Saved-run coordinates, not a cached global tier. Old snapshots keep revision 0 exactly.
/// The short route and a vanilla three-act run retain their separate legacy schedule in this baseline iteration.</summary>
public readonly record struct EconomyRoute(int Revision, int ActIndex, int ActCount, bool FiveActs, bool ShortRun)
{
    public bool FullFive => FiveActs && !ShortRun && ActCount >= ActRules.FullRunActs;
    public bool UsesFiveStageStock => Revision == EconomyRules.CurrentRevision && FullFive;
    public bool ExplicitShort => BuildAccessRules.IsShortRoute(FiveActs, ShortRun, ActCount);
    public int NativeTier => ActRules.VanillaTier(ActIndex, ActCount);
    public int Slot => FullFive ? Math.Clamp(ActIndex, 0, 4) : ExplicitShort ? Math.Clamp(ActIndex, 0, 2) * 2 : NativeTier;
}

/// <summary>B2.5 baseline iteration 1: change stock timing and remove the repeated act HP overlay on authored
/// bosses. Do NOT rebalance gold/price/stat/Burden/difficulty numbers at the same time. Not a combat simulator.</summary>
public static class EconomyRules
{
    public const int CurrentRevision = 1;

    /// <summary>Actual five-act equipment schedule. A guaranteed Wanderer remains one tier behind a specialist,
    /// not permanently stuck at I–II. Orders use the same specialist ceiling as the visible stock.</summary>
    public static (int Min, int Max) EquipmentTiers(ShopType shop, EconomyRoute route)
    {
        if (!route.UsesFiveStageStock) return ShopRules.EquipmentTiers(shop, route.NativeTier);
        var slot = route.Slot;
        return shop switch
        {
            ShopType.Armorer or ShopType.Jeweler or ShopType.Caravan => (Math.Clamp(slot + 1, 1, 4), Math.Clamp(slot + 2, 2, 5)),
            ShopType.Fence => (4, 5),
            _ => (Math.Max(1, slot), Math.Clamp(slot + 1, 2, 5)),
        };
    }

    /// <summary>Keep cheap I brews through act 2; III in 3–4; V in 5. Not the compressed 1/3/3/5/5 curve.</summary>
    public static int BrewTier(EconomyRoute route) => !route.UsesFiveStageStock ? ShopRules.BrewTier(route.NativeTier)
        : route.Slot <= 1 ? 1 : route.Slot <= 3 ? 3 : 5;

    public static (int Min, int Max) StaffTiers(EconomyRoute route) => !route.UsesFiveStageStock ? ShopRules.StaffTiers(route.NativeTier)
        : route.Slot switch { 0 => (1, 1), 1 => (2, 2), 2 => (2, 3), 3 => (3, 4), _ => (4, 5) };

    public static int ScrollTier(EconomyRoute route) => !route.UsesFiveStageStock ? ShopRules.ScrollTier(route.NativeTier)
        : route.Slot <= 1 ? 1 : route.Slot <= 3 ? 2 : 3;

    public static int SoulStonePower(EconomyRoute route) => !route.UsesFiveStageStock ? ShopRules.SoulStonePower(route.NativeTier)
        : route.Slot <= 1 ? 1 : route.Slot <= 3 ? 2 : 3;

    // Reagent kits are not another tuning lever in iteration 1; preserve the existing price on every route.
    public static int ReagentKitPrice(EconomyRoute route) => ShopRules.ReagentKitPrice(route.NativeTier);

    public static int OrderTier(ShopType shop, EconomyRoute route) => !route.UsesFiveStageStock
        ? ShopRules.OrderTier(shop, route.NativeTier) : Math.Max(2, EquipmentTiers(ShopType.Armorer, route).Max);

    public static int OrderItemPrice(OrderCategory category, EconomyRoute route)
    {
        if (!route.UsesFiveStageStock) return ShopRules.OrderItemPrice(category, route.NativeTier);
        var tier = OrderTier(ShopType.Armorer, route);
        return category switch
        {
            OrderCategory.Weapon or OrderCategory.Equipment => ShopRules.EquipmentPrice(EquipCategory.Weapon, tier),
            OrderCategory.Armor => ShopRules.EquipmentPrice(EquipCategory.Armor, tier),
            OrderCategory.Shield => ShopRules.EquipmentPrice(EquipCategory.Shield, tier),
            OrderCategory.Spell => ShopRules.SpellPrice((SpellTier)tier, single: true),
            OrderCategory.Potion => ShopRules.BrewPrice(5, poison: false),
            OrderCategory.Poison => ShopRules.BrewPrice(5, poison: true),
            _ => 100,
        };
    }

    /// <summary>Authored act/superboss HP already uses the route's table. Apply the user's HP % and difficulty
    /// once as usual, but do not add the borrowed-vanilla act overlay again. Ordinary/elite/minion overlays and
    /// Blood Court night stay unchanged. Revision 0 reproduces the saved old contract.</summary>
    public static int ActHpBonusPercent(ActKind kind, bool fullRun, EnemyRank rank, bool authoredBoss, bool night, int revision)
    {
        var bonus = revision == CurrentRevision && authoredBoss && rank == EnemyRank.Boss ? 0 : ActRules.EnemyHpBonusPercent(kind, fullRun);
        if (kind == ActKind.BloodCourt && rank != EnemyRank.Boss && night) bonus += ActRules.NightHpBonusPercent;
        return bonus;
    }

    public static decimal EnemyHpMultiplier(int configuredPercent, int requiemLevel, int actBonusPercent) =>
        configuredPercent / 100m * RequiemLevelRules.EnemyHpMultiplier(requiemLevel) * (100 + actBonusPercent) / 100m;
}
