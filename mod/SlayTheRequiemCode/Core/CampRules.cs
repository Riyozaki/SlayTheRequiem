namespace SlayTheRequiem.SlayTheRequiemCode.Core;

/// <summary>Действия лагеря (docs/design/10 §1). Other — чужие действия (ванильные реликвии, другие моды).</summary>
public enum CampAction { Rest, ShortRest, Smith, Forge, Brew, Enchant, Cook, Pray, Chest, Meditate, Train, Other, Warm, Tavern }

/// <summary>
/// Постройки лагеря (10 §5) в порядке открытия победами: Котёл — Готовка без «Походного котла»; Наковальня — Кузня за
/// 1 очко; Зачаровальный стол и Алтарь — в каждом лагере; Палатка травника — Варка ступенью выше.
/// </summary>
public enum CampBuilding { Kettle = 1, Anvil, EnchantingTable, Altar, HerbalistTent }

/// <summary>
/// Лагерь: очки лагеря, лечение, Усталость похода, Медитация (docs/design/10). Чистые функции — зеркало в tools/sim.
/// </summary>
public static class CampRules
{
    public const int BasePoints = 2;
    public const int RestHealPercent = 30;
    public const int ShortRestHealPercent = 15;
    public const int FirstAidHealPercent = 20;

    /// <summary>Очков за привал: 2 (+1 «Опытный путник», +1 «Мастер привала»).</summary>
    public static int Budget(bool wayfarer, bool campMaster) => BasePoints + (wayfarer ? 1 : 0) + (campMaster ? 1 : 0);

    /// <summary>Цена действия. «Мастер привала»: Кузня и Зачарование — 1. Чужие действия ≈ ванильное «одно действие» = 2.</summary>
    public static int Cost(CampAction action, bool campMaster) => action switch
    {
        CampAction.Rest => 2,
        CampAction.ShortRest => 1,
        CampAction.Smith => 2,
        CampAction.Forge => campMaster ? 1 : 2,
        CampAction.Brew => 1,
        CampAction.Enchant => campMaster ? 1 : 2,
        CampAction.Cook => 1,
        CampAction.Pray => 1,
        CampAction.Chest => 0,
        CampAction.Meditate => 1,
        CampAction.Train => 0,
        CampAction.Warm => 0,
        CampAction.Tavern => 0,
        _ => 2,
    };

    /// <summary>Цена с учётом построек метапрогресса (§5): «Наковальня» — Кузня за 1 очко.</summary>
    public static int Cost(CampAction action, bool campMaster, bool anvil) =>
        action == CampAction.Forge && anvil ? 1 : Cost(action, campMaster);

    public static bool CanAfford(int points, int cost) => cost <= points;

    /// <summary>Действия ремесла, для которых «Мастер-ремесленник» раз за привал возвращает 1 очко.</summary>
    public static bool IsCraft(CampAction action) => action is CampAction.Brew or CampAction.Forge or CampAction.Enchant;

    public static int ShortRestPercent(bool firstAid) => firstAid ? FirstAidHealPercent : ShortRestHealPercent;

    public static decimal HealAmount(int maxHp, int percent) => maxHp * Math.Max(0, percent) / 100m;

    // ───── Усталость похода (10 §4) ─────

    public const int FatigueMinRequiemLevel = 5;
    public const int FatigueNodesPerStack = 3;
    public const int FatigueMaxStacks = 3;

    public static bool FatigueEnabled(int requiemLevel) => requiemLevel >= FatigueMinRequiemLevel;

    /// <summary>Стаки Усталости: каждые 3 узла без Отдыха, максимум 3 (−1 пул ◆ за стак).</summary>
    public static int FatigueStacks(int nodesSinceRest, bool enabled) =>
        enabled ? Math.Clamp(nodesSinceRest / FatigueNodesPerStack, 0, FatigueMaxStacks) : 0;

    // ───── Камни душ (07 §6.1) ─────

    /// <summary>Сила камня с элиты: акты 1–2 — малый (1), 3–4 — большой (2), 5 — чёрный (3). Босс — на ступень выше.</summary>
    public static int SoulStoneFor(EnemyRank rank, int actIndex)
    {
        if (rank == EnemyRank.Normal) return 0;
        var power = actIndex <= 1 ? 1 : actIndex <= 3 ? 2 : 3;
        if (rank == EnemyRank.Boss) power++;
        return Math.Clamp(power, 1, 3);
    }

    // ───── Вьючный зверь (10 §6) ─────

    public const int MuleBurden = 10;
    public const int ShodBurden = 5;
    /// <summary>Мул у торговца (Странник, Караван) — до скидки доверия.</summary>
    public const int MulePrice = 120;
    /// <summary>«Подковать» у Оружейника (в лагере — Кузня).</summary>
    public const int MuleShoePrice = 40;

    /// <summary>Вместимость Груза от зверя: +10, «Подкованный» — ещё +5.</summary>
    public static int MuleCapacity(bool shod) => MuleBurden + (shod ? ShodBurden : 0);

    /// <summary>«Засада»: со зверем пропадает половина Сундука (с округлением вверх).</summary>
    public static int ChestLoss(int stored) => (Math.Max(0, stored) + 1) / 2;

    // ───── Постройки лагеря (10 §5, метапрогресс — отдельная настройка) ─────

    public const int BuildingCount = 5;

    /// <summary>Открыта ли постройка при <paramref name="unlocked"/> открытых (по порядку §5).</summary>
    public static bool HasBuilding(int unlocked, CampBuilding building) => unlocked >= (int)building;

    /// <summary>Победа в забеге открывает следующую постройку.</summary>
    public static int UnlockAfterVictory(int unlocked) => Math.Clamp(unlocked + 1, 0, BuildingCount);
}

// ═════════════ Еда (10 §3) ═════════════

public enum Dish { BloodmossStew, VenisonRoast, FishSoup, SpicedSausage, MushroomStew, SweetRoll, Feast }

/// <summary>Действие Сытости в бою.</summary>
public readonly record struct DishEffect(
    int MaxHp = 0,
    int StaminaRegen = 0,
    int ManaRegen = 0,
    int FrostResistance = 0,
    int StartStamina = 0,
    int Regen = 0,
    int OpeningDraw = 0)
{
    public static DishEffect operator +(DishEffect a, DishEffect b) => new(
        a.MaxHp + b.MaxHp, a.StaminaRegen + b.StaminaRegen, a.ManaRegen + b.ManaRegen,
        Math.Max(a.FrostResistance, b.FrostResistance), a.StartStamina + b.StartStamina,
        a.Regen + b.Regen, a.OpeningDraw + b.OpeningDraw);
}

/// <summary>Сытость в слоте: блюдо и сколько боёв осталось.</summary>
public readonly record struct Meal(Dish Dish, int Fights);

public static class FoodRules
{
    public const int Duration = 3;
    public const int FeastDuration = 1;
    public const int FeastReagents = 5;

    public static readonly Dish[] Dishes = Enum.GetValues<Dish>();

    public static DishEffect Effect(Dish dish) => dish switch
    {
        Dish.BloodmossStew => new DishEffect(MaxHp: 6),
        Dish.VenisonRoast => new DishEffect(StaminaRegen: 1),
        Dish.FishSoup => new DishEffect(ManaRegen: 1),
        Dish.SpicedSausage => new DishEffect(FrostResistance: 1, StartStamina: 2),
        Dish.MushroomStew => new DishEffect(Regen: 1),
        Dish.SweetRoll => new DishEffect(OpeningDraw: 1),
        // Пир: всё перечисленное, но на 1 бой.
        _ => Dishes.Where(d => d != Dish.Feast).Aggregate(new DishEffect(), (acc, d) => acc + Effect(d)),
    };

    public static int DurationOf(Dish dish) => dish == Dish.Feast ? FeastDuration : Duration;

    /// <summary>
    /// Походный котелок (07, «Новые реликвии рюкзака»): еда даёт +1 к каждому своему действию
    /// (Кровяная похлёбка — +7 ♥ вместо +6, Оленина — +2 прироста ◆ и т. д.). Пустой Сытости не прибавляет.
    /// </summary>
    public static DishEffect Boosted(DishEffect e)
    {
        static int Up(int v) => v > 0 ? v + KettleBonus : v;
        return new DishEffect(Up(e.MaxHp), Up(e.StaminaRegen), Up(e.ManaRegen), Up(e.FrostResistance), Up(e.StartStamina),
            Up(e.Regen), Up(e.OpeningDraw));
    }

    public const int KettleBonus = 1;

    /// <summary>Грибная похлёбка совместима с любой другой едой — свой слот.</summary>
    public static bool IsCompanion(Dish dish) => dish == Dish.MushroomStew;

    /// <summary>Слотов Сытости: 1, с даром Хмельного Князя — 2.</summary>
    public static int Slots(bool revel) => revel ? 2 : 1;

    /// <summary>Рецепт (мясо зверей — с Логовом и событиями этапа 7; пока блюда из реагентов).</summary>
    public static Reagent[] Ingredients(Dish dish) => dish switch
    {
        Dish.BloodmossStew => [Reagent.Bloodmoss, Reagent.Bloodmoss],
        Dish.VenisonRoast => [Reagent.Yellowsorrel, Reagent.Yellowsorrel],
        Dish.FishSoup => [Reagent.Bluebloom, Reagent.Bluebloom],
        Dish.SpicedSausage => [Reagent.AshSalt, Reagent.Yellowsorrel],
        Dish.MushroomStew => [Reagent.Nightcap, Reagent.Bloodmoss],
        Dish.SweetRoll => [Reagent.FragrantTear, Reagent.Bluebloom],
        _ => [],
    };

    /// <summary>
    /// Пир берёт 5 любых реагентов (кроме Драконьего порошка): по одному из самых многочисленных, по кругу.
    /// null — не хватает.
    /// </summary>
    public static Reagent[]? FeastIngredients(IReadOnlyList<int> pouch)
    {
        var left = pouch.ToArray();
        var picked = new List<Reagent>();
        while (picked.Count < FeastReagents)
        {
            var best = -1;
            for (var i = 0; i < left.Length; i++)
            {
                if ((Reagent)i == Reagent.DragonScale || left[i] <= 0) continue;
                if (best < 0 || left[i] > left[best]) best = i;
            }
            if (best < 0) return null;
            left[best]--;
            picked.Add((Reagent)best);
        }
        return picked.ToArray();
    }

    public static Reagent[]? IngredientsFor(Dish dish, IReadOnlyList<int> pouch)
    {
        if (dish == Dish.Feast) return FeastIngredients(pouch);
        var need = Ingredients(dish);
        return AlchemyRules.CanAfford(pouch, need) ? need : null;
    }

    /// <summary>
    /// Съесть блюдо: такое же — продлевается; есть свободный слот — занимает; иначе вытесняет самое старое.
    /// Грибная похлёбка в слоты не входит (см. <see cref="IsCompanion"/>).
    /// </summary>
    public static List<Meal> Eat(IReadOnlyList<Meal> meals, Dish dish, int slots)
    {
        var list = meals.Where(m => m.Fights > 0).ToList();
        var meal = new Meal(dish, DurationOf(dish));
        var same = list.FindIndex(m => m.Dish == dish);
        if (same >= 0) list[same] = meal;
        else
        {
            while (list.Count >= Math.Max(1, slots)) list.RemoveAt(0);
            list.Add(meal);
        }
        return list;
    }

    /// <summary>После боя: −1 бой у каждой Сытости, закончившиеся уходят.</summary>
    public static List<Meal> AfterCombat(IReadOnlyList<Meal> meals) =>
        meals.Select(m => m with { Fights = m.Fights - 1 }).Where(m => m.Fights > 0).ToList();

    public static DishEffect Total(IReadOnlyList<Meal> meals, int mushroomFights)
    {
        var total = meals.Where(m => m.Fights > 0).Aggregate(new DishEffect(), (acc, m) => acc + Effect(m.Dish));
        if (mushroomFights > 0) total += Effect(Dish.MushroomStew);
        return total;
    }

    /// <summary>Сохранение: «Блюдо:боёв» через запятую.</summary>
    public static string Serialize(IEnumerable<Meal> meals) => string.Join(",", meals.Select(m => $"{m.Dish}:{m.Fights}"));

    public static List<Meal> Parse(string? text)
    {
        var list = new List<Meal>();
        if (string.IsNullOrEmpty(text)) return list;
        foreach (var part in text.Split(',', StringSplitOptions.RemoveEmptyEntries))
        {
            var bits = part.Split(':');
            if (bits.Length == 2 && Enum.TryParse<Dish>(bits[0], out var dish) && int.TryParse(bits[1], out var fights) && fights > 0)
                list.Add(new Meal(dish, fights));
        }
        return list;
    }
}

// ═════════════ Зачарование снаряжения (07 §6.1) ═════════════

public enum Enchant
{
    Health, Stamina, Magic, FireResistance, FrostResistance, ShockResistance, WeaponMight, Burden,
    // Оружейные чары (с зарядами)
    FireBrand, FrostBrand, ShockBrand,
    // Отложенные чары 07 §6.1 (M4 п. 6). Ремесло школы — по чарам на школу (школа хранится видом чар).
    CraftDestruction, CraftRestoration, CraftAlteration, CraftConjuration, CraftIllusion,
    AbsorbHealth, AbsorbStamina, AbsorbMagic, Banish, Paralysis,
}

/// <summary>Чары на предмете: вид, сила 1–3, заряды (только оружейные).</summary>
public readonly record struct EnchantSlot(Enchant Enchant, int Power, int Charges = 0);

public static class EnchantRules
{
    public static readonly Enchant[] All = Enum.GetValues<Enchant>();

    public static bool IsWeaponBrand(Enchant e) => e is Enchant.FireBrand or Enchant.FrostBrand or Enchant.ShockBrand
        or Enchant.AbsorbHealth or Enchant.AbsorbStamina or Enchant.AbsorbMagic or Enchant.Banish or Enchant.Paralysis;

    /// <summary>Оружейные чары, которые тратят заряд только при срабатывании (Изгнание — по призванным, Паралич — раз в бой).</summary>
    public static bool ChargesOnTrigger(Enchant e) => e is Enchant.Banish or Enchant.Paralysis;

    /// <summary>Школа «Ремесла школы» (null — это не Ремесло).</summary>
    public static SpellSchool? SchoolOf(Enchant e) => e switch
    {
        Enchant.CraftDestruction => SpellSchool.Destruction,
        Enchant.CraftRestoration => SpellSchool.Restoration,
        Enchant.CraftAlteration => SpellSchool.Alteration,
        Enchant.CraftConjuration => SpellSchool.Conjuration,
        Enchant.CraftIllusion => SpellSchool.Illusion,
        _ => null,
    };

    /// <summary>Ремесло школы: −1 ✦ первым N заклинаниям этой школы за ход (N = сумма чар на надетых вещах).</summary>
    public static int CraftDiscount(int perTurn, int castThisTurn) => castThisTurn < perTurn ? 1 : 0;

    /// <summary>Ступень контроля Паралича по силе чар 1/2/3: Ученик / Адепт / Эксперт (порог ♥ цели — <see cref="SpellAffinity.ControlHpCap"/>).</summary>
    public static SpellTier ParalysisTier(int power) => Math.Clamp(power, 1, 3) switch
    {
        1 => SpellTier.Apprentice,
        2 => SpellTier.Adept,
        _ => SpellTier.Expert,
    };

    public static bool IsResistance(Enchant e) => e is Enchant.FireResistance or Enchant.FrostResistance or Enchant.ShockResistance;

    public static bool Allowed(Enchant e, EquipSlot slot) => e switch
    {
        Enchant.Health => slot is EquipSlot.Head or EquipSlot.Body,
        Enchant.Stamina => slot is EquipSlot.Feet or EquipSlot.Body,
        Enchant.Magic => slot is EquipSlot.Head or EquipSlot.Ring,
        Enchant.FireResistance or Enchant.FrostResistance or Enchant.ShockResistance =>
            slot is EquipSlot.OffHand or EquipSlot.Ring or EquipSlot.Feet,
        Enchant.WeaponMight => slot is EquipSlot.Hands or EquipSlot.Ring,
        Enchant.Burden => slot is EquipSlot.Hands or EquipSlot.Feet,
        Enchant.CraftDestruction or Enchant.CraftRestoration or Enchant.CraftAlteration or Enchant.CraftConjuration
            or Enchant.CraftIllusion => slot is EquipSlot.Head or EquipSlot.Amulet,
        _ => slot == EquipSlot.MainHand,
    };

    /// <summary>Величина чар по силе 1/2/3.</summary>
    public static int Value(Enchant e, int power)
    {
        var p = Math.Clamp(power, 1, 3);
        return e switch
        {
            Enchant.Health => 4 * p,
            Enchant.Stamina => p,
            Enchant.Magic => 2 * p,
            Enchant.FireResistance or Enchant.FrostResistance or Enchant.ShockResistance => p >= 3 ? 2 : 1,
            Enchant.WeaponMight => p,
            Enchant.Burden => 4 * p,
            Enchant.FireBrand => 2 * p,   // Горение
            Enchant.FrostBrand => p,      // Озноб
            Enchant.Banish => 1,          // мгновенная смерть призванного
            _ => p,                       // Разряд: выжечь ✦; Ремесло: заклинаний за ход; Поглощение ♥/◆/✦; Паралич: ступень
        };
    }

    /// <summary>Заряды оружейных чар: 8/12/16.</summary>
    public static int Charges(int power) => 4 + 4 * Math.Clamp(power, 1, 3);

    /// <summary>Чар на предмет: 1, «Ремесло: Зачарование» — 2, Дух божества Хранителя Свитков — 3 (07 §6.1).</summary>
    public static int SlotsPerItem(bool craftEnchanting, bool scrollKeeperSpirit = false) =>
        scrollKeeperSpirit ? 3 : craftEnchanting ? 2 : 1;

    /// <summary>
    /// Наложить чары: такие же чары заменяются (заряды обновляются); свободный слот — добавить; иначе вытесняется первая.
    /// </summary>
    public static List<EnchantSlot> Apply(IReadOnlyList<EnchantSlot> current, Enchant e, int power, int slots)
    {
        var list = current.ToList();
        var slot = new EnchantSlot(e, Math.Clamp(power, 1, 3), IsWeaponBrand(e) ? Charges(power) : 0);
        var same = list.FindIndex(s => s.Enchant == e);
        if (same >= 0) list[same] = slot;
        else
        {
            while (list.Count >= Math.Max(1, slots)) list.RemoveAt(0);
            list.Add(slot);
        }
        return list;
    }

    public static string Serialize(IEnumerable<EnchantSlot> slots) =>
        string.Join(",", slots.Select(s => $"{s.Enchant}:{s.Power}:{s.Charges}"));

    public static List<EnchantSlot> Parse(string? text)
    {
        var list = new List<EnchantSlot>();
        if (string.IsNullOrEmpty(text)) return list;
        foreach (var part in text.Split(',', StringSplitOptions.RemoveEmptyEntries))
        {
            var bits = part.Split(':');
            if (bits.Length < 2 || !Enum.TryParse<Enchant>(bits[0], out var e) || !int.TryParse(bits[1], out var power)) continue;
            var charges = bits.Length > 2 && int.TryParse(bits[2], out var c) ? c : 0;
            list.Add(new EnchantSlot(e, Math.Clamp(power, 1, 3), Math.Max(0, charges)));
        }
        return list;
    }
}
