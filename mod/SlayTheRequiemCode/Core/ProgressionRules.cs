namespace SlayTheRequiem.SlayTheRequiemCode.Core;

/// <summary>Ось очков характеристик (docs/design/02 §5). Владение школой в ступени не входит.</summary>
public enum StatAxis { Health, Stamina, Mana, Burden, Mastery }

/// <summary>Бонусы ступеней характеристик (каждые 3 очка в одну ось, docs/design/02 §5).</summary>
public readonly record struct TierBonuses(
    int PoisonResistance,
    int BurdenBonus,
    int Plating,
    int StaminaRegen,
    int AttackDamage,
    bool FreeFirstAttack,
    int ManaRegen,
    int FirstSpellDiscount,
    int Ward);

/// <summary>
/// Прогрессия забега: Слава → уровень 1–15 → очки характеристик и навыков (docs/design/02 §5, 04 «Деревья навыков»).
/// Чистые функции — зеркало в tools/sim.
/// </summary>
public static class ProgressionRules
{
    public const int MaxLevel = 15;
    public const int StatPointsPerLevel = 2;
    public const int PointsPerTier = 3;
    public const int MaxTier = 3;

    public const int HealthPerPoint = 4;
    public const int StaminaPoolPerPoint = 1;
    public const int ManaPoolPerPoint = 1;
    public const int BurdenPerPoint = 3;

    /// <summary>Слава за бой: обычный 1, элита 3, босс 6.</summary>
    public static int RenownFor(EnemyRank rank) => rank switch
    {
        EnemyRank.Boss => 6,
        EnemyRank.Elite => 3,
        _ => 1,
    };

    /// <summary>Сколько Славы нужно, чтобы с уровня <paramref name="level"/> подняться на следующий: 3/4/5/…</summary>
    public static int Threshold(int level) => Math.Max(1, level) + 2;

    /// <summary>Суммарная Слава для достижения уровня (1 → 0, 2 → 3, 3 → 7, 4 → 12…).</summary>
    public static int RenownToReach(int level)
    {
        level = Math.Clamp(level, 1, MaxLevel);
        return (level - 1) * (level + 4) / 2;
    }

    public static int LevelFor(int renown)
    {
        var level = 1;
        while (level < MaxLevel && renown >= RenownToReach(level + 1)) level++;
        return level;
    }

    /// <summary>Слава до следующего уровня (0 на максимуме).</summary>
    public static int RenownToNext(int renown)
    {
        var level = LevelFor(renown);
        return level >= MaxLevel ? 0 : RenownToReach(level + 1) - renown;
    }

    /// <summary>Очки характеристик, заработанные к уровню: 2 за каждый уровень после первого.</summary>
    public static int StatPointsFor(int level) => StatPointsPerLevel * (Math.Clamp(level, 1, MaxLevel) - 1);

    /// <summary>Очки навыков: каждый 2-й уровень.</summary>
    public static int SkillPointsFor(int level) => Math.Clamp(level, 1, MaxLevel) / 2;

    /// <summary>
    /// «Опыт поражения» (docs/design/15 §1): после возврата Камнем Памяти остаётся 50 % Славы,
    /// набранной в погибшей попытке сверх Отпечатка.
    /// </summary>
    public static int RetainedRenown(int imprintRenown, int deadRenown) =>
        imprintRenown + Math.Max(0, deadRenown - imprintRenown) / 2;

    /// <summary>Цена +1 владения школой очками характеристик/навыков: родная 1, чужая 2.</summary>
    public static int MasteryCost(bool nativeSchool) => SpellAffinity.PointsPerMastery(nativeSchool);

    /// <summary>Ступень оси: каждые 3 очка, максимум III.</summary>
    public static int Tier(int points) => Math.Clamp(points / PointsPerTier, 0, MaxTier);

    /// <summary>Медитация (10 §1): +1 к ступени до конца следующего боя, не выше III.</summary>
    public static int WithMeditation(int tier, bool meditated) => meditated ? Math.Min(MaxTier, tier + 1) : tier;

    public static TierBonuses Tiers(int healthTier, int staminaTier, int manaTier) => new(
        PoisonResistance: healthTier >= 1 ? 1 : 0,
        BurdenBonus: healthTier >= 2 ? 6 : 0,
        Plating: healthTier >= 3 ? 1 : 0,
        StaminaRegen: staminaTier >= 1 ? 1 : 0,
        AttackDamage: staminaTier >= 2 ? 1 : 0,
        FreeFirstAttack: staminaTier >= 3,
        ManaRegen: manaTier >= 1 ? 1 : 0,
        FirstSpellDiscount: manaTier >= 2 ? 1 : 0,
        Ward: manaTier >= 3 ? 3 : 0);

    // ───── «Неумелый хват» (08 §8.5) ─────

    /// <summary>Щит с весом от 4 — тяжёлый.</summary>
    public const int HeavyShieldWeight = 4;

    public const int ClumsyStaminaPenalty = 1;
    public const decimal ClumsyDamageMultiplier = 0.75m;

    /// <summary>
    /// Неумелый хват: Регент с двуручным, арбалетом или тяжёлым щитом; Безмолвная с двуручным. Латоносец владеет всем.
    /// Снимается узлом «Владение оружием» (Ратное дело) или уроком Наставника.
    /// </summary>
    public static bool IsClumsy(string? heroTypeName, WeaponKind mainHand, bool heavyShield, bool trained)
    {
        if (trained) return false;
        return heroTypeName switch
        {
            "Regent" => mainHand is WeaponKind.TwoHanded or WeaponKind.Crossbow || heavyShield,
            "Silent" => mainHand == WeaponKind.TwoHanded,
            _ => false,
        };
    }

    /// <summary>Классы оружия, для которых у героя бывает «Неумелый хват» — их снимает урок Наставника (14 §2.9).</summary>
    public static IReadOnlyList<WeaponKind> LessonKinds(string? heroTypeName) => heroTypeName switch
    {
        "Regent" => [WeaponKind.TwoHanded, WeaponKind.Crossbow, WeaponKind.Shield],
        "Silent" => [WeaponKind.TwoHanded],
        _ => [],
    };

    /// <summary>Урок владения взят (бит на <see cref="WeaponKind"/>; тяжёлый щит — бит Shield).</summary>
    public static bool HasLesson(int lessonMask, WeaponKind kind) => kind != WeaponKind.None && (lessonMask & (1 << (int)kind)) != 0;

    public static int WithLesson(int lessonMask, WeaponKind kind) => kind == WeaponKind.None ? lessonMask : lessonMask | (1 << (int)kind);

    /// <summary>Неумелый хват с учётом уроков Наставника: урок снимает штраф только для своего класса оружия.</summary>
    public static bool IsClumsy(string? heroTypeName, WeaponKind mainHand, bool heavyShield, bool trained, int lessonMask) =>
        IsClumsy(heroTypeName, HasLesson(lessonMask, mainHand) ? WeaponKind.None : mainHand,
            heavyShield && !HasLesson(lessonMask, WeaponKind.Shield), trained);
}

// ═════════════ Деревья навыков ═════════════

public enum SkillBranch
{
    // Общие
    Craft, Martial, Survival,
    // Латоносец
    OneHanded, TwoHanded, Block, HeavyArmor,
    // Безмолвная
    Blades, Archery, Evasion, Poisons,
    // Школы магии (открыты всем; у Регента это его ветки)
    Destruction, Restoration, Alteration, Conjuration, Illusion,
}

/// <summary>Узлы деревьев. Порядок внутри ветки = ранг (у Выживания — ярус I–III по три узла).</summary>
public enum Skill
{
    // Ремесло
    CraftAlchemy, CraftEnchanting, CraftSmithing, CraftArtisan,
    // Ратное дело
    WeaponTraining, Conditioning, SecondBreath, Veteran,
    // Выживание (10 §2)
    CampKettle, DeftPacking, FirstAid,
    Wayfarer, Acclimatization, TradeName,
    WellRested, CampMaster, HerbLore,
    // Одноручное
    Fighter, Fencer, Butcher, Crusher,
    // Двуручное
    Sweep, HeavyHand, Shatter, Relentless,
    // Блок
    ShieldBearer, ShieldWall, ShieldBash, Bastion,
    // Тяжёлая броня
    Hardened, Steadfast, WeightBearer, AgainstAllOdds,
    // Кинжалы и копья
    Bloodletter, Ambush, Precision, Hemorrhage,
    // Стрельба
    Marksman, QuickDraw, AimedShot, ArmorPiercer,
    // Уклонение
    Nimble, Counterstrike, LightStep, Elusive,
    // Яды и токсины
    Poisoner, Toxicologist, VenomWard, VenomMaster,
    // Школы: три ступени владения + узел-перк
    Destruction1, Destruction2, Destruction3, ElementLord,
    Restoration1, Restoration2, Restoration3, Warden,
    Alteration1, Alteration2, Alteration3, Stoneskin,
    Conjuration1, Conjuration2, Conjuration3, BoundMaster,
    Illusion1, Illusion2, Illusion3, Phantasm,
}

public sealed record SkillNode(Skill Skill, SkillBranch Branch, int Rank);

public static class SkillTree
{
    private static readonly (SkillBranch Branch, Skill[] Skills)[] Layout =
    [
        (SkillBranch.Craft, new[] { Skill.CraftAlchemy, Skill.CraftEnchanting, Skill.CraftSmithing, Skill.CraftArtisan }),
        (SkillBranch.Martial, new[] { Skill.WeaponTraining, Skill.Conditioning, Skill.SecondBreath, Skill.Veteran }),
        (SkillBranch.Survival, new[] { Skill.CampKettle, Skill.DeftPacking, Skill.FirstAid, Skill.Wayfarer, Skill.Acclimatization, Skill.TradeName, Skill.WellRested, Skill.CampMaster, Skill.HerbLore }),
        (SkillBranch.OneHanded, new[] { Skill.Fighter, Skill.Fencer, Skill.Butcher, Skill.Crusher }),
        (SkillBranch.TwoHanded, new[] { Skill.Sweep, Skill.HeavyHand, Skill.Shatter, Skill.Relentless }),
        (SkillBranch.Block, new[] { Skill.ShieldBearer, Skill.ShieldWall, Skill.ShieldBash, Skill.Bastion }),
        (SkillBranch.HeavyArmor, new[] { Skill.Hardened, Skill.Steadfast, Skill.WeightBearer, Skill.AgainstAllOdds }),
        (SkillBranch.Blades, new[] { Skill.Bloodletter, Skill.Ambush, Skill.Precision, Skill.Hemorrhage }),
        (SkillBranch.Archery, new[] { Skill.Marksman, Skill.QuickDraw, Skill.AimedShot, Skill.ArmorPiercer }),
        (SkillBranch.Evasion, new[] { Skill.Nimble, Skill.Counterstrike, Skill.LightStep, Skill.Elusive }),
        (SkillBranch.Poisons, new[] { Skill.Poisoner, Skill.Toxicologist, Skill.VenomWard, Skill.VenomMaster }),
        (SkillBranch.Destruction, new[] { Skill.Destruction1, Skill.Destruction2, Skill.Destruction3, Skill.ElementLord }),
        (SkillBranch.Restoration, new[] { Skill.Restoration1, Skill.Restoration2, Skill.Restoration3, Skill.Warden }),
        (SkillBranch.Alteration, new[] { Skill.Alteration1, Skill.Alteration2, Skill.Alteration3, Skill.Stoneskin }),
        (SkillBranch.Conjuration, new[] { Skill.Conjuration1, Skill.Conjuration2, Skill.Conjuration3, Skill.BoundMaster }),
        (SkillBranch.Illusion, new[] { Skill.Illusion1, Skill.Illusion2, Skill.Illusion3, Skill.Phantasm }),
    ];

    /// <summary>Узлов в ярусе Выживания.</summary>
    public const int SurvivalTierSize = 3;

    public static readonly IReadOnlyList<SkillNode> Nodes = Build();

    private static List<SkillNode> Build()
    {
        var list = new List<SkillNode>();
        foreach (var (branch, skills) in Layout)
            for (var i = 0; i < skills.Length; i++)
            {
                var rank = branch == SkillBranch.Survival ? i / SurvivalTierSize + 1 : i + 1;
                list.Add(new SkillNode(skills[i], branch, rank));
            }
        return list;
    }

    public static SkillNode Node(Skill skill) => Nodes.First(n => n.Skill == skill);

    public static IEnumerable<SkillNode> InBranch(SkillBranch branch) => Nodes.Where(n => n.Branch == branch);

    public static SpellSchool? SchoolOf(SkillBranch branch) => branch switch
    {
        SkillBranch.Destruction => SpellSchool.Destruction,
        SkillBranch.Restoration => SpellSchool.Restoration,
        SkillBranch.Alteration => SpellSchool.Alteration,
        SkillBranch.Conjuration => SpellSchool.Conjuration,
        SkillBranch.Illusion => SpellSchool.Illusion,
        _ => null,
    };

    public static readonly SkillBranch[] Common = [SkillBranch.Craft, SkillBranch.Martial, SkillBranch.Survival];

    public static readonly SkillBranch[] Schools =
        [SkillBranch.Destruction, SkillBranch.Restoration, SkillBranch.Alteration, SkillBranch.Conjuration, SkillBranch.Illusion];

    /// <summary>Собственные ветки героя (у Регента — школы, они и так открыты всем).</summary>
    public static SkillBranch[] HeroBranches(string? heroTypeName) => heroTypeName switch
    {
        "Ironclad" => new[] { SkillBranch.OneHanded, SkillBranch.TwoHanded, SkillBranch.Block, SkillBranch.HeavyArmor },
        "Silent" => new[] { SkillBranch.Blades, SkillBranch.Archery, SkillBranch.Evasion, SkillBranch.Poisons },
        _ => Array.Empty<SkillBranch>(),
    };

    /// <summary>Все ветки, доступные герою: свои + общие + школы.</summary>
    public static IReadOnlyList<SkillBranch> BranchesFor(string? heroTypeName) =>
        HeroBranches(heroTypeName).Concat(Common).Concat(Schools).Distinct().ToList();

    /// <summary>Цена узла: обычный — 1 очко; узел школы — 1 для родной, 2 для чужой.</summary>
    public static int Cost(SkillNode node, bool nativeSchool) =>
        SchoolOf(node.Branch) == null ? 1 : ProgressionRules.MasteryCost(nativeSchool);

    /// <summary>
    /// Можно ли взять узел: ветка доступна, узел не взят; обычная ветка — по порядку (ранг N требует ранг N−1);
    /// Выживание — ярус N требует 2·(N−1) взятых узлов Выживания (любых).
    /// </summary>
    public static bool CanLearn(SkillNode node, IReadOnlyCollection<Skill> learned, IReadOnlyList<SkillBranch> branches)
    {
        if (!branches.Contains(node.Branch) || learned.Contains(node.Skill)) return false;
        if (node.Branch == SkillBranch.Survival)
            return learned.Count(s => Node(s).Branch == SkillBranch.Survival) >= 2 * (node.Rank - 1);
        if (node.Rank == 1) return true;
        return InBranch(node.Branch).Any(n => n.Rank == node.Rank - 1 && learned.Contains(n.Skill));
    }

    /// <summary>Узлы, которые можно взять сейчас за имеющиеся очки (в порядке дерева).</summary>
    public static List<SkillNode> Learnable(string? heroTypeName, IReadOnlyCollection<Skill> learned, int points, Func<SpellSchool, bool> isNative)
    {
        var branches = BranchesFor(heroTypeName);
        return Nodes
            .Where(n => CanLearn(n, learned, branches))
            .Where(n => Cost(n, SchoolOf(n.Branch) is { } school && isNative(school)) <= points)
            .ToList();
    }

    /// <summary>Узел школы даёт +1 владение (узлы-перки IV — тоже).</summary>
    public static bool GrantsMastery(SkillNode node) => SchoolOf(node.Branch) != null;

    /// <summary>Сохранение: имена узлов через запятую (устойчиво к перестановкам перечисления).</summary>
    public static string Serialize(IEnumerable<Skill> skills) => string.Join(",", skills.Select(s => s.ToString()));

    public static HashSet<Skill> Parse(string? text)
    {
        var set = new HashSet<Skill>();
        if (string.IsNullOrEmpty(text)) return set;
        foreach (var part in text.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            if (Enum.TryParse<Skill>(part, out var skill)) set.Add(skill);
        return set;
    }
}
