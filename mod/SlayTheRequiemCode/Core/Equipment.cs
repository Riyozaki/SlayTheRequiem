namespace SlayTheRequiem.SlayTheRequiemCode.Core;

/// <summary>Слоты снаряжения (docs/design/07 §1). Кольца — два слота.</summary>
public enum EquipSlot
{
    None,
    MainHand,
    OffHand,
    Head,
    Body,
    Hands,
    Feet,
    Cloak,
    Amulet,
    Ring,
}

public enum ArmorClass { None, Heavy, Light, Robe }

public enum WeaponKind { None, Dagger, Sword, Axe, Mace, Spear, TwoHanded, Bow, Crossbow, Staff, Shield }

/// <summary>Описание надетого предмета для расчёта (без зависимостей от игры).</summary>
public sealed record EquipItem(
    string Key,
    EquipSlot Slot,
    int Weight,
    int Tier = 1,
    ArmorClass Armor = ArmorClass.None,
    WeaponKind Weapon = WeaponKind.None,
    string? Set = null,
    int Ward = 0);

/// <summary>Сводные бонусы снаряжения (броня, комплекты не включены — они у предметов-комплектов).</summary>
public readonly record struct LoadoutBonuses(
    int Plating,
    int DodgeCharges,
    int PoiseBonus,
    int StaminaPoolBonus,
    int StaminaRegenBonus,
    int ManaRegenPenalty,
    int Ward,
    int WeaponDamage,
    int WeaponStaminaDelta,
    bool Duelist,
    bool Monk);

public static class EquipmentRules
{
    public static int SlotCapacity(EquipSlot slot) => slot switch
    {
        EquipSlot.Ring => 2,
        EquipSlot.None => int.MaxValue,
        _ => 1,
    };

    /// <summary>Двуручное оружие занимает и левую руку.</summary>
    public static bool IsTwoHanded(WeaponKind kind) => kind is WeaponKind.TwoHanded or WeaponKind.Bow or WeaponKind.Crossbow;

    public static bool IsArmorSlot(EquipSlot slot) => slot is EquipSlot.Head or EquipSlot.Body or EquipSlot.Hands or EquipSlot.Feet;

    /// <summary>
    /// Какие предметы надо снять, чтобы надеть <paramref name="item"/>: при переполнении слота — самый ранний,
    /// двуручное снимает левую руку, предмет в левую руку снимает двуручное.
    /// </summary>
    public static IReadOnlyList<EquipItem> Displaced(IReadOnlyList<EquipItem> equipped, EquipItem item)
    {
        var result = new List<EquipItem>();
        if (item.Slot == EquipSlot.None) return result;

        var sameSlot = equipped.Where(e => e.Slot == item.Slot).ToList();
        var overflow = sameSlot.Count - SlotCapacity(item.Slot) + 1;
        if (overflow > 0) result.AddRange(sameSlot.Take(overflow));

        if (item.Slot == EquipSlot.MainHand && IsTwoHanded(item.Weapon))
            result.AddRange(equipped.Where(e => e.Slot == EquipSlot.OffHand));
        if (item.Slot == EquipSlot.OffHand)
            result.AddRange(equipped.Where(e => e.Slot == EquipSlot.MainHand && IsTwoHanded(e.Weapon)));

        return result.Distinct().ToList();
    }

    /// <summary>Бонус урона оружия по ступени I–V (docs/design/07 §3.1).</summary>
    public static int WeaponDamage(WeaponKind kind, int tier)
    {
        var t = Math.Clamp(tier, 1, 5) - 1;
        return kind switch
        {
            WeaponKind.Dagger => new[] { 0, 1, 2, 2, 3 }[t],
            WeaponKind.Sword or WeaponKind.Axe or WeaponKind.Mace or WeaponKind.Spear => new[] { 1, 2, 3, 3, 4 }[t],
            WeaponKind.TwoHanded => new[] { 3, 4, 5, 7, 8 }[t],
            WeaponKind.Bow => new[] { 2, 3, 4, 5, 6 }[t],
            WeaponKind.Crossbow => new[] { 4, 5, 6, 7, 8 }[t],
            _ => 0,
        };
    }

    /// <summary>Сдвиг цены ◆ «Оружейных» атак от типа оружия.</summary>
    public static int WeaponStaminaDelta(WeaponKind kind) => kind switch
    {
        WeaponKind.Dagger => -1,
        WeaponKind.TwoHanded or WeaponKind.Crossbow => 1,
        _ => 0,
    };

    public static LoadoutBonuses Evaluate(IReadOnlyList<EquipItem> equipped)
    {
        var armor = equipped.Where(e => IsArmorSlot(e.Slot)).ToList();
        var heavy = armor.Where(e => e.Armor == ArmorClass.Heavy).ToList();
        var light = armor.Count(e => e.Armor == ArmorClass.Light);

        // Тяжёлая: Латы +1 за каждые 2 части ступени II+, плюс +1 за каждую часть IV+; Стойкость +1 за часть.
        var plating = heavy.Count(e => e.Tier >= 2) / 2 + heavy.Count(e => e.Tier >= 4);
        var poise = heavy.Count;
        var manaPenalty = heavy.Count >= 3 ? 1 : 0;

        // Лёгкая: Уворот 1 за 2 части, 2 за 4; +1 пул ◆ за 2 части; при ≥3 тяжёлых Уворот не работает.
        var dodge = heavy.Count >= 3 ? 0 : light >= 4 ? 2 : light >= 2 ? 1 : 0;
        var staminaPool = light / 2;

        var ward = equipped.Sum(e => e.Ward);

        var main = equipped.FirstOrDefault(e => e.Slot == EquipSlot.MainHand);
        var off = equipped.FirstOrDefault(e => e.Slot == EquipSlot.OffHand);
        var weapon = main?.Weapon ?? WeaponKind.None;
        var twoHanded = IsTwoHanded(weapon);
        var duelist = main != null && !twoHanded && off == null;
        var monk = main == null && off == null;

        return new LoadoutBonuses(
            Plating: plating,
            DodgeCharges: dodge,
            PoiseBonus: poise,
            StaminaPoolBonus: staminaPool,
            StaminaRegenBonus: duelist || monk ? 1 : 0,
            ManaRegenPenalty: manaPenalty,
            Ward: ward,
            WeaponDamage: main == null ? 0 : WeaponDamage(weapon, main.Tier),
            WeaponStaminaDelta: WeaponStaminaDelta(weapon),
            Duelist: duelist,
            Monk: monk);
    }

    /// <summary>Сколько частей комплекта надето.</summary>
    public static int SetPieces(IReadOnlyList<EquipItem> equipped, string set) => equipped.Count(e => e.Set == set);

    /// <summary>Цена ◆ «Оружейной» атаки: база + сдвиг оружия, не ниже 0.</summary>
    public static int WeaponAttackCost(int baseCost, LoadoutBonuses bonuses) => Math.Max(0, baseCost + bonuses.WeaponStaminaDelta);
}
