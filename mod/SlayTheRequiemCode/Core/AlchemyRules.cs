namespace SlayTheRequiem.SlayTheRequiemCode.Core;

/// <summary>Реагенты (docs/design/09 §2). Порядок = индекс в Сумке травника (сохраняется массивом).</summary>
public enum Reagent
{
    Bloodmoss,      // Кровяной мох — Здоровье
    Bluebloom,      // Синецвет — Мана
    Yellowsorrel,   // Жёлтая кислица — Выносливость
    AshSalt,        // Пепельная соль — Огонь
    FrostSalt,      // Ледяная соль — Холод
    StormCrystal,   // Грозовой кристалл — Разряд
    Nightcap,       // Ночная поганка — Яд
    GraveDust,      // Прах мертвеца — Хворь
    DragonScale,    // Драконий чешуйчатый порошок — Сопротивление
    FragrantTear,   // Душистая слеза — Иллюзия
}

/// <summary>Свойство-основа реагента.</summary>
public enum Essence { Health, Mana, Stamina, Fire, Frost, Shock, Poison, Blight, Resistance, Illusion }

/// <summary>Что получается в котле (семейство зелья/яда; ступень — отдельно).</summary>
public enum Brew
{
    // Восполнения
    Health, Stamina, Mana,
    // Восстановления (длительные)
    Rejuvenation, SecondWind, Clarity,
    // Усиления
    Giant, Knight, Sorcerer, FireWard, FrostWard, ShockWard, Feather, WitchHunter, Invisibility,
    // Особые
    Panacea,
    // Яды на оружие
    SnakeBite, Toxin, Rot, Exhaustion, Emptybloom, Weakness, Corrosion, Paralytic,
}

/// <summary>
/// Алхимия: рецепты, ступени и выпадение реагентов (docs/design/09). Чистые функции — зеркало в tools/sim.
/// Рецепт — пара свойств. Два реагента дают ступень I, третий реагент одного из свойств рецепта — ступень III,
/// «Знаток трав» (Выживание) и «Алхимия» (Ремесло) поднимают на ступень (I → III → V), яды — ещё и «Мастер ядов».
/// </summary>
public static class AlchemyRules
{
    public const int PouchCapacity = 12;
    public static readonly int ReagentCount = Enum.GetValues<Reagent>().Length;

    public static Essence EssenceOf(Reagent reagent) => reagent switch
    {
        Reagent.Bloodmoss => Essence.Health,
        Reagent.Bluebloom => Essence.Mana,
        Reagent.Yellowsorrel => Essence.Stamina,
        Reagent.AshSalt => Essence.Fire,
        Reagent.FrostSalt => Essence.Frost,
        Reagent.StormCrystal => Essence.Shock,
        Reagent.Nightcap => Essence.Poison,
        Reagent.GraveDust => Essence.Blight,
        Reagent.DragonScale => Essence.Resistance,
        _ => Essence.Illusion,
    };

    public static Reagent ReagentOf(Essence essence) => essence switch
    {
        Essence.Health => Reagent.Bloodmoss,
        Essence.Mana => Reagent.Bluebloom,
        Essence.Stamina => Reagent.Yellowsorrel,
        Essence.Fire => Reagent.AshSalt,
        Essence.Frost => Reagent.FrostSalt,
        Essence.Shock => Reagent.StormCrystal,
        Essence.Poison => Reagent.Nightcap,
        Essence.Blight => Reagent.GraveDust,
        Essence.Resistance => Reagent.DragonScale,
        _ => Reagent.FragrantTear,
    };

    public readonly record struct Recipe(Brew Brew, Essence A, Essence B)
    {
        public bool IsPoison => AlchemyRules.IsPoison(Brew);
    }

    public static readonly IReadOnlyList<Recipe> Recipes =
    [
        new(Brew.Health, Essence.Health, Essence.Health),
        new(Brew.Stamina, Essence.Stamina, Essence.Stamina),
        new(Brew.Mana, Essence.Mana, Essence.Mana),
        new(Brew.Rejuvenation, Essence.Health, Essence.Stamina),
        new(Brew.SecondWind, Essence.Stamina, Essence.Shock),
        new(Brew.Clarity, Essence.Mana, Essence.Illusion),
        new(Brew.Giant, Essence.Health, Essence.Resistance),
        new(Brew.Knight, Essence.Stamina, Essence.Resistance),
        new(Brew.Sorcerer, Essence.Mana, Essence.Shock),
        new(Brew.FireWard, Essence.Fire, Essence.Resistance),
        new(Brew.FrostWard, Essence.Frost, Essence.Resistance),
        new(Brew.ShockWard, Essence.Shock, Essence.Resistance),
        new(Brew.Feather, Essence.Stamina, Essence.Illusion),
        new(Brew.WitchHunter, Essence.Mana, Essence.Resistance),
        new(Brew.Invisibility, Essence.Illusion, Essence.Illusion),
        new(Brew.Panacea, Essence.Health, Essence.Blight),
        new(Brew.SnakeBite, Essence.Poison, Essence.Fire),
        new(Brew.Toxin, Essence.Poison, Essence.Poison),
        new(Brew.Rot, Essence.Poison, Essence.Blight),
        new(Brew.Exhaustion, Essence.Poison, Essence.Frost),
        new(Brew.Emptybloom, Essence.Poison, Essence.Shock),
        new(Brew.Weakness, Essence.Poison, Essence.Stamina),
        new(Brew.Corrosion, Essence.Fire, Essence.Frost),
        new(Brew.Paralytic, Essence.Poison, Essence.Illusion),
    ];

    public static bool IsPoison(Brew brew) => brew >= Brew.SnakeBite;

    public static Recipe RecipeFor(Brew brew) => Recipes.First(r => r.Brew == brew);

    /// <summary>Ступени I/III/V как 1/3/5. Число реагентов 2 → I, 3 → III; бонус перка — +2 (одна ступень).</summary>
    public static int Tier(int reagentsUsed, int herbalismSteps = 0)
    {
        var tier = reagentsUsed >= 3 ? 3 : 1;
        return Math.Min(5, tier + 2 * Math.Max(0, herbalismSteps));
    }

    /// <summary>Реагенты для варки рецепта: 2 (A, B) или 3 (A, B и ещё один A — для пар одного свойства то же самое).</summary>
    public static Reagent[] Ingredients(Recipe recipe, bool strong)
    {
        var list = new List<Reagent> { ReagentOf(recipe.A), ReagentOf(recipe.B) };
        if (strong) list.Add(ReagentOf(recipe.A));
        return list.ToArray();
    }

    public static bool CanAfford(IReadOnlyList<int> pouch, IReadOnlyList<Reagent> ingredients) =>
        ingredients.GroupBy(r => r).All(g => (int)g.Key < pouch.Count && pouch[(int)g.Key] >= g.Count());

    public readonly record struct BrewOption(Recipe Recipe, int Tier, Reagent[] Ingredients);

    /// <summary>
    /// Что можно сварить из содержимого сумки: для каждого рецепта — сильнейший вариант (III, если хватает
    /// третьего реагента, иначе I). Порядок — по ступени, затем по таблице рецептов.
    /// </summary>
    public static List<BrewOption> Available(IReadOnlyList<int> pouch, int herbalismSteps = 0)
    {
        var result = new List<BrewOption>();
        foreach (var recipe in Recipes)
        {
            var strong = Ingredients(recipe, true);
            if (CanAfford(pouch, strong)) { result.Add(new(recipe, Tier(3, herbalismSteps), strong)); continue; }
            var weak = Ingredients(recipe, false);
            if (CanAfford(pouch, weak)) result.Add(new(recipe, Tier(2, herbalismSteps), weak));
        }
        return result.OrderByDescending(o => o.Tier).ToList();
    }

    // ───── выпадение ─────

    /// <summary>Сколько реагентов за бой: обычный 1, элита 2, босс 3 (без перков).</summary>
    public static int DropCount(EnemyRank rank) => rank switch
    {
        EnemyRank.Boss => 3,
        EnemyRank.Elite => 2,
        _ => 1,
    };

    /// <summary>Какие реагенты могут выпасть с врага (фракция и тип атаки из таблицы врагов).</summary>
    public static IReadOnlyList<Reagent> DropCandidates(string faction, DamageType attackType)
    {
        var list = new List<Reagent>(faction switch
        {
            "beast" => [Reagent.Bloodmoss, Reagent.Yellowsorrel],
            "plant" => [Reagent.Bloodmoss, Reagent.Nightcap],
            "insect" => [Reagent.Nightcap, Reagent.Yellowsorrel],
            "humanoid" or "bandit" => [Reagent.Yellowsorrel, Reagent.Bloodmoss],
            "cultist" => [Reagent.Bluebloom, Reagent.GraveDust],
            "construct" => [Reagent.StormCrystal],
            "aquatic" => [Reagent.Nightcap, Reagent.FrostSalt],
            "spirit" => [Reagent.FragrantTear, Reagent.Bluebloom],
            "abyss" => [Reagent.AshSalt],
            "undead" => [Reagent.GraveDust, Reagent.FrostSalt],
            "slime" => [Reagent.Nightcap],
            _ => Array.Empty<Reagent>(),
        });
        switch (attackType)
        {
            case DamageType.Magic: list.Add(Reagent.Bluebloom); break;
            case DamageType.Fire: list.Add(Reagent.AshSalt); break;
            case DamageType.Shock: list.Add(Reagent.StormCrystal); break;
            case DamageType.Frost: list.Add(Reagent.FrostSalt); break;
            case DamageType.Poison: list.Add(Reagent.Nightcap); break;
        }
        if (list.Count == 0) list.Add(Reagent.Bloodmoss);
        return list;
    }

    /// <summary>Сколько из выпавшего поместится в сумку.</summary>
    public static int Fit(int current, int incoming) => Math.Max(0, Math.Min(incoming, PouchCapacity - current));

    // ───── яды на оружии ─────

    /// <summary>Заряды яда: 3 базово (+2 Знамение Змея, +1 Заговорщик — этапы 4/1).</summary>
    public const int BasePoisonCharges = 3;

    /// <summary>Сила яда по ступени (I/III/V) — таблица docs/design/09 §4.</summary>
    public static int PoisonPotency(Brew brew, int tier) => (brew, tier) switch
    {
        (Brew.SnakeBite, >= 5) => 10, (Brew.SnakeBite, >= 3) => 6, (Brew.SnakeBite, _) => 3,
        (Brew.Toxin, >= 5) => 7, (Brew.Toxin, >= 3) => 4, (Brew.Toxin, _) => 2,
        (Brew.Rot, >= 5) => 3, (Brew.Rot, >= 3) => 2, (Brew.Rot, _) => 1,
        (Brew.Exhaustion or Brew.Emptybloom, >= 5) => 7, (Brew.Exhaustion or Brew.Emptybloom, >= 3) => 4, (Brew.Exhaustion or Brew.Emptybloom, _) => 2,
        (Brew.Weakness or Brew.Corrosion, >= 5) => 3, (Brew.Weakness or Brew.Corrosion, >= 3) => 2, (Brew.Weakness or Brew.Corrosion, _) => 1,
        // Паралитик: шанс паралича в процентах (V — гарантированный, один раз).
        (Brew.Paralytic, >= 5) => 100, (Brew.Paralytic, >= 3) => 25, (Brew.Paralytic, _) => 10,
        _ => 0,
    };

    // ───── зелья ─────

    /// <summary>Числа зелий по ступени (docs/design/09 §3).</summary>
    public static int PotionPotency(Brew brew, int tier) => (brew, tier) switch
    {
        (Brew.Health, >= 5) => 28, (Brew.Health, >= 3) => 16, (Brew.Health, _) => 8,
        (Brew.Stamina or Brew.Mana, >= 3) => 6, (Brew.Stamina or Brew.Mana, _) => 3,
        (Brew.Rejuvenation, >= 5) => 6, (Brew.Rejuvenation, >= 3) => 4, (Brew.Rejuvenation, _) => 2,
        (Brew.SecondWind or Brew.Clarity, >= 5) => 3, (Brew.SecondWind or Brew.Clarity, >= 3) => 2, (Brew.SecondWind or Brew.Clarity, _) => 1,
        (Brew.Giant, >= 5) => 8, (Brew.Giant, >= 3) => 5, (Brew.Giant, _) => 3,
        (Brew.Knight, >= 5) => 3, (Brew.Knight, >= 3) => 2, (Brew.Knight, _) => 1,
        (Brew.Sorcerer, >= 5) => 3, (Brew.Sorcerer, >= 3) => 2, (Brew.Sorcerer, _) => 1,
        (Brew.FireWard or Brew.FrostWard or Brew.ShockWard, >= 5) => 3,
        (Brew.FireWard or Brew.FrostWard or Brew.ShockWard, >= 3) => 2,
        (Brew.FireWard or Brew.FrostWard or Brew.ShockWard, _) => 1,
        (Brew.Feather, >= 5) => 20, (Brew.Feather, >= 3) => 12, (Brew.Feather, _) => 6,
        (Brew.WitchHunter, >= 5) => 3, (Brew.WitchHunter, >= 3) => 2, (Brew.WitchHunter, _) => 1,
        (Brew.Invisibility, >= 5) => 3, (Brew.Invisibility, >= 3) => 2, (Brew.Invisibility, _) => 1,
        _ => 0,
    };

    /// <summary>Длительность восстанавливающих зелий в ходах.</summary>
    public const int RestorationTurns = 4;
}
