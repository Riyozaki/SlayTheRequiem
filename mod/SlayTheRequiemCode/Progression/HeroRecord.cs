using BaseLib.Utils;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Commands.Builders;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.RelicPools;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Saves.Runs;
using MegaCrit.Sts2.Core.ValueProps;
using SlayTheRequiem.SlayTheRequiemCode.Alchemy;
using SlayTheRequiem.SlayTheRequiemCode.Cards;
using SlayTheRequiem.SlayTheRequiemCode.Core;
using SlayTheRequiem.SlayTheRequiemCode.Equipment;
using SlayTheRequiem.SlayTheRequiemCode.Pantheon;
using SlayTheRequiem.SlayTheRequiemCode.Powers;
using SlayTheRequiem.SlayTheRequiemCode.Relics;
using SlayTheRequiem.SlayTheRequiemCode.Resources;
using SlayTheRequiem.SlayTheRequiemCode.Rules;
using SlayTheRequiem.SlayTheRequiemCode.Spells;
using VanillaPoison = MegaCrit.Sts2.Core.Models.Powers.PoisonPower;

namespace SlayTheRequiem.SlayTheRequiemCode.Progression;

/// <summary>
/// Летопись героя (docs/design/02 §5, 04, 10) — служебная стартовая реликвия прогрессии забега:
/// Слава и уровень, очки характеристик и их ступени, деревья навыков, Сытость, Бодрость, Медитация, Усталость похода,
/// Камни душ и очки лагеря. Числовые бонусы идут общим путём <see cref="BoonRelic"/> (♥, пулы, Латы, Покров,
/// сопротивления, рука, цены), а условные эффекты узлов — собственными хуками ниже.
/// Счётчик на иконке — уровень.
/// </summary>
[Pool(typeof(SharedRelicPool))]
public sealed partial class HeroRecord : BoonRelic, IAttackStaminaModifier, IPoisonChargeSource
{
    public override RelicRarity Rarity => RelicRarity.Starter;
    public override bool ShowCounter => true;
    public override int DisplayAmount => Owned ? Level : 1;

    public static HeroRecord? Of(Player? player) => player?.Relics.OfType<HeroRecord>().FirstOrDefault();

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DynamicVar("Level", 1), new DynamicVar("Renown", 0), new DynamicVar("ToNext", 3),
        new DynamicVar("StatPoints", 0), new DynamicVar("SkillPoints", 0), new DynamicVar("Skills", 0),
        new DynamicVar("Fed", 0), new DynamicVar("Fatigue", 0), new DynamicVar("Stones", 0),
    ];

    // ═════════════ сохраняемое состояние ═════════════

    private int _renown;
    private int _statHealth, _statStamina, _statMana, _statBurden, _statMastery;
    private string _skills = "";
    private int _skillPointsSpent;
    private int _nativeMask;
    private string _meals = "";
    private int _mushroomFights;
    private int _vigorFights;
    private int _meditation;
    private int _nodesSinceRest;
    private int _stoneSmall, _stoneLarge, _stoneBlack;
    private int _campPoints;
    private bool _artisanUsed;

    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public int Renown { get => _renown; set { AssertMutable(); _renown = Math.Max(0, value); } }

    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public int StatHealth { get => _statHealth; set { AssertMutable(); _statHealth = Math.Max(0, value); } }

    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public int StatStamina { get => _statStamina; set { AssertMutable(); _statStamina = Math.Max(0, value); } }

    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public int StatMana { get => _statMana; set { AssertMutable(); _statMana = Math.Max(0, value); } }

    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public int StatBurden { get => _statBurden; set { AssertMutable(); _statBurden = Math.Max(0, value); } }

    /// <summary>Очки характеристик, потраченные на владение школами (по 1 или 2 за +1).</summary>
    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public int StatMastery { get => _statMastery; set { AssertMutable(); _statMastery = Math.Max(0, value); } }

    /// <summary>Взятые узлы — имена через запятую (<see cref="SkillTree.Serialize"/>).</summary>
    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public string Skills { get => _skills; set { AssertMutable(); _skills = value ?? ""; _learned = null; } }

    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public int SkillPointsSpent { get => _skillPointsSpent; set { AssertMutable(); _skillPointsSpent = Math.Max(0, value); } }

    /// <summary>Родные школы (бит на школу; бит 7 — «записано»), фиксируются после выдачи происхождения и предыстории.</summary>
    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public int NativeMask { get => _nativeMask; set { AssertMutable(); _nativeMask = value; } }

    /// <summary>Сытость — «Блюдо:боёв» через запятую (<see cref="FoodRules.Serialize"/>).</summary>
    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public string Meals { get => _meals; set { AssertMutable(); _meals = value ?? ""; } }

    /// <summary>Грибная похлёбка — свой «слот», совместима с любой едой.</summary>
    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public int MushroomFights { get => _mushroomFights; set { AssertMutable(); _mushroomFights = Math.Max(0, value); } }

    /// <summary>Бодрость («Отдохнувший»): +1 прирост ◆ и ✦ на столько боёв.</summary>
    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public int VigorFights { get => _vigorFights; set { AssertMutable(); _vigorFights = Math.Max(0, value); } }

    /// <summary>Медитация до конца следующего боя: 0 — нет, 1 — ◆, 2 — ✦.</summary>
    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public int Meditation { get => _meditation; set { AssertMutable(); _meditation = Math.Clamp(value, 0, 2); } }

    /// <summary>Узлов карты с последнего Отдыха (Усталость похода).</summary>
    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public int NodesSinceRest { get => _nodesSinceRest; set { AssertMutable(); _nodesSinceRest = Math.Max(0, value); } }

    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public int StoneSmall { get => _stoneSmall; set { AssertMutable(); _stoneSmall = Math.Max(0, value); } }

    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public int StoneLarge { get => _stoneLarge; set { AssertMutable(); _stoneLarge = Math.Max(0, value); } }

    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public int StoneBlack { get => _stoneBlack; set { AssertMutable(); _stoneBlack = Math.Max(0, value); } }

    /// <summary>Оставшиеся очки текущего привала.</summary>
    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public int CampPoints { get => _campPoints; set { AssertMutable(); _campPoints = Math.Max(0, value); } }

    /// <summary>«Мастер-ремесленник» уже вернул очко на этом привале.</summary>
    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public bool ArtisanUsed { get => _artisanUsed; set { AssertMutable(); _artisanUsed = value; } }

    // ═════════════ уровень и очки ═════════════

    public int Level => ProgressionRules.LevelFor(_renown);
    public int StatPointsSpent => _statHealth + _statStamina + _statMana + _statBurden + _statMastery;
    public int UnspentStatPoints => Math.Max(0, ProgressionRules.StatPointsFor(Level) + BonusStatPoints - StatPointsSpent);

    /// <summary>«Выучка» Цитадельцев (04): +1 очко характеристик за каждые 3 уровня.</summary>
    public int BonusStatPoints => IsMutable && Owner?.Relics.OfType<CitadelOrigin>().Any() == true
        ? OriginRules.TrainingBonusPoints(Level) : 0;
    public int UnspentSkillPoints => Math.Max(0, ProgressionRules.SkillPointsFor(Level) - _skillPointsSpent);
    public bool HasUnspentPoints => UnspentStatPoints > 0 || UnspentSkillPoints > 0;

    private string HeroName => Owner.Character.GetType().Name;

    /// <summary>Слава (бои, события 1–3). Новый уровень — вспышка; очки тратятся в лагере («Развитие»).</summary>
    public void GainRenown(int amount)
    {
        if (amount <= 0) return;
        var before = Level;
        Renown += amount;
        if (Level > before) Flash();
        Refresh();
    }

    public int Points(StatAxis axis) => axis switch
    {
        StatAxis.Health => _statHealth,
        StatAxis.Stamina => _statStamina,
        StatAxis.Mana => _statMana,
        StatAxis.Burden => _statBurden,
        _ => _statMastery,
    };

    public int BaseTier(StatAxis axis) => axis is StatAxis.Burden or StatAxis.Mastery ? 0 : ProgressionRules.Tier(Points(axis));

    /// <summary>Ступень с учётом Медитации.</summary>
    public int TierOf(StatAxis axis) => ProgressionRules.WithMeditation(BaseTier(axis),
        (axis == StatAxis.Stamina && _meditation == 1) || (axis == StatAxis.Mana && _meditation == 2));

    public TierBonuses Tiers => ProgressionRules.Tiers(TierOf(StatAxis.Health), TierOf(StatAxis.Stamina), TierOf(StatAxis.Mana));

    // ───── родные школы ─────

    private const int NativeRecordedBit = 1 << 7;

    public bool IsNative(SpellSchool school) =>
        SpellAffinity.IsNativeSchool(HeroName, school) || (_nativeMask & (1 << (int)school)) != 0;

    private bool _grantSeen;

    private void RecordNativeSchools()
    {
        if ((_nativeMask & NativeRecordedBit) != 0) return;
        // Происхождение и предыстория выдаются Гримуаром при входе в первую комнату; ждём предысторию
        // (если её так и не выдали — записываем со следующей комнаты).
        if (!Owner.Relics.OfType<BackgroundRelic>().Any())
        {
            if (Mastery.GrimoireOf(Owner) is not { OriginGranted: true }) return;
            if (!_grantSeen) { _grantSeen = true; return; }
        }
        var mask = NativeRecordedBit;
        foreach (var school in Mastery.Schools)
            if ((Mastery.GrimoireOf(Owner)?.Level(school) ?? 0) > 0) mask |= 1 << (int)school;
        NativeMask = mask;
    }

    public int MasteryCost(SpellSchool school) => ProgressionRules.MasteryCost(IsNative(school));

    /// <summary>Потратить очко характеристики (владение — 1/2 очка за +1 школы).</summary>
    public async Task<bool> SpendStat(StatAxis axis, SpellSchool school = SpellSchool.Destruction)
    {
        var free = UnspentStatPoints;
        switch (axis)
        {
            case StatAxis.Mastery:
            {
                var cost = MasteryCost(school);
                var grimoire = Mastery.GrimoireOf(Owner);
                if (free < cost || grimoire == null || !grimoire.Raise(school)) return false;
                StatMastery += cost;
                break;
            }
            case StatAxis.Health:
                if (free < 1) return false;
                StatHealth++;
                break;
            case StatAxis.Stamina:
                if (free < 1) return false;
                StatStamina++;
                break;
            case StatAxis.Mana:
                if (free < 1) return false;
                StatMana++;
                break;
            default:
                if (free < 1) return false;
                StatBurden++;
                break;
        }
        await SyncMaxHp();
        TravelPack.Of(Owner)?.Refresh();
        Flash();
        Refresh();
        return true;
    }

    // ───── навыки ─────

    private HashSet<Skill>? _learned;
    public IReadOnlySet<Skill> Learned => _learned ??= SkillTree.Parse(_skills);
    public bool Has(Skill skill) => Owned && Learned.Contains(skill);

    public int CostOf(SkillNode node) => SkillTree.Cost(node, SkillTree.SchoolOf(node.Branch) is { } s && IsNative(s));

    public List<SkillNode> Learnable() =>
        SkillTree.Learnable(HeroName, Learned.ToList(), UnspentSkillPoints, IsNative);

    public async Task<bool> Learn(Skill skill)
    {
        var node = SkillTree.Node(skill);
        if (!Learnable().Contains(node)) return false;
        var set = Learned.ToHashSet();
        set.Add(skill);
        SkillPointsSpent += CostOf(node);
        Skills = SkillTree.Serialize(SkillTree.Nodes.Select(n => n.Skill).Where(set.Contains));
        if (SkillTree.SchoolOf(node.Branch) is { } school) Mastery.GrimoireOf(Owner)?.Raise(school);
        await SyncMaxHp();
        TravelPack.Of(Owner)?.Refresh();
        Flash();
        Refresh();
        return true;
    }

    // ───── Сытость ─────

    public List<Meal> MealList => FoodRules.Parse(_meals);
    public bool HasRevel => Owner.Relics.OfType<GiftOfRevel>().Any();
    public DishEffect Food => FoodRules.Total(MealList, _mushroomFights);

    public async Task Eat(Dish dish)
    {
        if (FoodRules.IsCompanion(dish)) MushroomFights = FoodRules.DurationOf(dish);
        else Meals = FoodRules.Serialize(FoodRules.Eat(MealList, dish, FoodRules.Slots(HasRevel)));
        // Пир снимает Усталость похода (10 §4).
        if (dish == Dish.Feast) NodesSinceRest = 0;
        await SyncMaxHp();
        Flash();
        Refresh();
    }

    // ───── Усталость похода ─────

    public bool FatigueEnabled => CampRules.FatigueEnabled(Acts.RequiemLevel.Of(Owner.RunState));
    public int FatigueStacks => CampRules.FatigueStacks(_nodesSinceRest, FatigueEnabled);

    /// <summary>После Отдыха: Усталость снята; «Отдохнувший» даёт Бодрость на 2 боя.</summary>
    public void AfterRest()
    {
        NodesSinceRest = 0;
        if (Has(Skill.WellRested)) VigorFights = 2;
        Refresh();
    }

    // ───── Камни душ ─────

    public int Stones(int power) => power switch { 1 => _stoneSmall, 2 => _stoneLarge, _ => _stoneBlack };
    public int TotalStones => _stoneSmall + _stoneLarge + _stoneBlack;

    public void AddStone(int power, int amount = 1)
    {
        switch (power)
        {
            case 1: StoneSmall += amount; break;
            case 2: StoneLarge += amount; break;
            case 3: StoneBlack += amount; break;
        }
        Refresh();
    }

    public bool SpendStone(int power)
    {
        if (Stones(power) <= 0) return false;
        AddStone(power, -1);
        return true;
    }

    // ═════════════ числовой набор (BoonRelic) ═════════════

    protected override Boon ComputeBoon()
    {
        var tiers = Tiers;
        var food = Food;
        var gear = Loadout.Equipped(Owner);
        var heavy = gear.Count(e => EquipmentRules.IsArmorSlot(e.Slot) && e.Armor == ArmorClass.Heavy);
        var light = gear.Count(e => EquipmentRules.IsArmorSlot(e.Slot) && e.Armor == ArmorClass.Light);
        var main = MainHand;
        var shield = HasShield;
        var ench = Enchanting.Totals(Owner);
        var vigor = _vigorFights > 0 ? 1 : 0;
        var heavyManaPenalty = Loadout.Bonuses(Owner).ManaRegenPenalty;

        return new Boon
        {
            MaxHp = _statHealth * ProgressionRules.HealthPerPoint + food.MaxHp + ench.MaxHp,
            StaminaPool = _statStamina * ProgressionRules.StaminaPoolPerPoint + ench.StaminaPool
                          + (Has(Skill.Conditioning) ? 1 : 0)
                          + (Has(Skill.LightStep) && light >= 2 ? 1 : 0)
                          - FatigueStacks,
            StaminaRegen = tiers.StaminaRegen + food.StaminaRegen + vigor,
            ManaPool = _statMana * ProgressionRules.ManaPoolPerPoint + ench.ManaPool,
            // «Привычка к весу» снимает штраф ✦ тяжёлой брони.
            ManaRegen = tiers.ManaRegen + food.ManaRegen + vigor + (Has(Skill.WeightBearer) ? heavyManaPenalty : 0),
            Poise = (Has(Skill.Steadfast) ? heavy / 2 : 0) + (Has(Skill.Veteran) ? 1 : 0),
            Burden = _statBurden * ProgressionRules.BurdenPerPoint + tiers.BurdenBonus + ench.Burden
                     + (Has(Skill.DeftPacking) ? 4 : 0),
            Plating = tiers.Plating
                      + (Has(Skill.Hardened) && heavy >= 2 ? 1 : 0)
                      + (Has(Skill.ShieldWall) && shield ? 1 : 0)
                      + (Has(Skill.Stoneskin) ? 1 : 0),
            Ward = tiers.Ward + (Has(Skill.Warden) ? 2 : 0),
            Regen = food.Regen,
            AttackDamage = tiers.AttackDamage,
            Pierce = (Has(Skill.Shatter) && main == WeaponKind.TwoHanded ? 1 : 0)
                     + (Has(Skill.ArmorPiercer) && IsRanged(main) ? 1 : 0),
            OpeningHand = food.OpeningDraw,
            FireRes = ench.FireRes,
            FrostRes = food.FrostResistance + ench.FrostRes,
            ShockRes = ench.ShockRes,
            PoisonRes = tiers.PoisonResistance + (Has(Skill.VenomWard) ? 1 : 0),
            MerchantPricePercent = Has(Skill.TradeName) ? -10 : 0,
            SummonDamage = Has(Skill.BoundMaster) ? 2 : 0,
        };
    }

    // ═════════════ снаряжение и «Неумелый хват» ═════════════

    public WeaponKind MainHand => Loadout.InSlot(Owner, EquipSlot.MainHand)?.Weapon ?? WeaponKind.None;
    public bool HasShield => Loadout.InSlot(Owner, EquipSlot.OffHand) is { Weapon: WeaponKind.Shield };
    public bool HeavyShield => Loadout.InSlot(Owner, EquipSlot.OffHand) is { Weapon: WeaponKind.Shield } s
                               && s.Weight >= ProgressionRules.HeavyShieldWeight;

    public static bool IsOneHanded(WeaponKind kind) =>
        kind is WeaponKind.Dagger or WeaponKind.Sword or WeaponKind.Axe or WeaponKind.Mace or WeaponKind.Spear;

    public static bool IsRanged(WeaponKind kind) => kind is WeaponKind.Bow or WeaponKind.Crossbow;

    public static bool IsBlade(WeaponKind kind) => kind is WeaponKind.Dagger or WeaponKind.Spear;

    /// <summary>Неумелый хват (08 §8.5): +1 ◆ и −25 % урона «Оружейных» атак.</summary>
    public bool Clumsy => Owned && RequiemMode.IsInSystem(Owner)
                          && ProgressionRules.IsClumsy(HeroName, MainHand, HeavyShield, Has(Skill.WeaponTraining));

    private bool IsMyWeaponAttack(CardModel? card) =>
        card != null && card.Owner == Owner && EquipmentCombatRules.IsWeaponAttack(card);

    // ═════════════ бой ═════════════

    private int _attacksThisTurn, _spellsThisTurn, _illusionsThisTurn, _twoHandedThisTurn, _rangedThisTurn;
    private int _weaponAttacksThisCombat;
    private bool _bashThisTurn, _secondBreathUsed, _againstAllOddsUsed;

    public override Task BeforeCombatStart()
    {
        _attacksThisTurn = _spellsThisTurn = _illusionsThisTurn = _twoHandedThisTurn = _rangedThisTurn = 0;
        _weaponAttacksThisCombat = 0;
        _bashThisTurn = _secondBreathUsed = _againstAllOddsUsed = false;
        return base.BeforeCombatStart();
    }

    protected override async Task OnCombatStarted(ICombatState combatState)
    {
        var hero = Owner.Creature;
        var ctx = new ThrowingPlayerChoiceContext();
        if (Has(Skill.Nimble)) await PowerCmd.Apply<DodgePower>(ctx, hero, 1, hero, null, silent: true);
        if (Has(Skill.Counterstrike)) await PowerCmd.Apply<RipostePower>(ctx, hero, 2, hero, null, silent: true);
        // Акклиматизация: сопротивление огню и холоду I на первые 2 хода.
        if (Has(Skill.Acclimatization))
        {
            await PowerCmd.Apply<FireResistancePower>(ctx, hero, 1, hero, null, silent: true);
            await PowerCmd.Apply<FrostResistancePower>(ctx, hero, 1, hero, null, silent: true);
        }
        var start = Food.StartStamina;
        if (start > 0) RequiemResource.Of<StaminaResource>(Owner)?.ModifyAmount(start);
        NoteCombatStart(combatState);
    }

    protected override async Task OnTurnStarted(PlayerChoiceContext ctx)
    {
        _attacksThisTurn = _spellsThisTurn = _illusionsThisTurn = _twoHandedThisTurn = _rangedThisTurn = 0;
        _bashThisTurn = false;
        var hero = Owner.Creature;
        var round = hero.CombatState?.RoundNumber ?? 1;

        if (round == 3 && Has(Skill.Acclimatization))
        {
            await PowerCmd.Apply<FireResistancePower>(ctx, hero, -1, hero, null, silent: true);
            await PowerCmd.Apply<FrostResistancePower>(ctx, hero, -1, hero, null, silent: true);
        }
        if (Has(Skill.Bastion) && HasShield) await CreatureCmd.GainBlock(hero, 2, ValueProp.Unpowered, null);
        if (Has(Skill.Elusive) && round % 3 == 0) await PowerCmd.Apply<DodgePower>(ctx, hero, 1, hero, null);
        // Второе дыхание: раз за бой, если ход начался без ◆ — +3 ◆.
        if (Has(Skill.SecondBreath) && !_secondBreathUsed && RequiemResource.Of<StaminaResource>(Owner) is { } stamina
            && stamina.Amount <= 0)
        {
            _secondBreathUsed = true;
            stamina.ModifyAmount(3);
            Flash();
        }
    }

    public override async Task AfterCardPlayed(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        var card = cardPlay.Card;
        if (card.Owner != Owner || !cardPlay.IsLastInSeries) return;
        // Клич — на полку (карта снимается со стола, но остаётся в бою; вернётся в руку через N ходов).
        if (card is RequiemCard { ShelvedByThisPlay: true } shoutCard && card is IShoutCard shout)
        {
            shoutCard.ShelvedByThisPlay = false;
            await Shouts.Shelve(card, shout);
        }
        if (card is SpellCard spell)
        {
            _spellsThisTurn++;
            if (spell.School == SpellSchool.Illusion) _illusionsThisTurn++;
            return;
        }
        if (card.Type != CardType.Attack) return;
        _attacksThisTurn++;
        if (EquipmentCombatRules.IsWeaponAttack(card))
        {
            if (MainHand == WeaponKind.TwoHanded) _twoHandedThisTurn++;
            if (IsRanged(MainHand)) _rangedThisTurn++;
        }
        await Task.CompletedTask;
    }

    // ───── цена ◆ и ✦ ─────

    public int ModifyAttackStamina(CardModel card, int cost)
    {
        if (!Owned || card.Owner != Owner || card.Type != CardType.Attack || card is SpellCard) return cost;
        if (!RequiemMode.IsInSystem(Owner)) return cost;
        var weapon = EquipmentCombatRules.IsWeaponAttack(card);
        if (weapon && Clumsy) cost += ProgressionRules.ClumsyStaminaPenalty;
        if (weapon && Has(Skill.Relentless) && MainHand == WeaponKind.TwoHanded && _twoHandedThisTurn == 0) cost--;
        if (weapon && Has(Skill.QuickDraw) && IsRanged(MainHand) && _rangedThisTurn == 0) cost--;
        // ◆ III «Проворность»: первая атака хода — 0 ◆.
        if (Tiers.FreeFirstAttack && _attacksThisTurn == 0) cost = 0;
        return Math.Max(0, cost);
    }

    public override bool TryModifyStarCost(CardModel card, decimal originalCost, out decimal modifiedCost)
    {
        modifiedCost = originalCost;
        if (!Owned || card is not SpellCard spell || card.Owner != Owner || originalCost <= 0) return false;
        if (!RequiemMode.IsInSystem(Owner)) return false;
        var discount = 0;
        if (_spellsThisTurn == 0) discount += Tiers.FirstSpellDiscount;
        if (Has(Skill.Phantasm) && spell.School == SpellSchool.Illusion && _illusionsThisTurn == 0) discount++;
        if (discount == 0) return false;
        modifiedCost = Math.Max(0, originalCost - discount);
        return true;
    }

    public int ExtraPoisonCharges => Has(Skill.Poisoner) ? 1 : 0;

    // ───── урон и блок ─────

    protected override decimal ExtraDamage(Creature? target, ValueProp props, Creature? dealer, CardModel? cardSource)
    {
        if (cardSource == null || dealer != Owner.Creature || cardSource.Owner != Owner) return 0m;
        decimal bonus = 0;

        // «Повелитель стихии»: заклинания Разрушения +2.
        if (cardSource is SpellCard { School: SpellSchool.Destruction } && Has(Skill.ElementLord)) bonus += 2;

        if (!IsOwnAttack(dealer, props, cardSource)) return bonus;
        if (Has(Skill.Hemorrhage) && target != null && target.HasPower<BleedPower>()) bonus += 2;
        if (!EquipmentCombatRules.IsWeaponAttack(cardSource)) return bonus;

        var kind = MainHand;
        bonus += Enchanting.Totals(Owner).WeaponMight;
        if (Has(Skill.Fighter) && IsOneHanded(kind)) bonus += 1;
        if (Has(Skill.Butcher) && kind == WeaponKind.Axe && target != null && target.CurrentHp * 2 < target.MaxHp) bonus += 3;
        if (Has(Skill.Sweep) && kind == WeaponKind.TwoHanded) bonus += 2;
        if (IsBlade(kind))
        {
            if (Has(Skill.Precision)) bonus += 1;
            if (Has(Skill.Ambush) && target != null && SneakRules.IsSneak(Owner.Creature, target)) bonus += 3;
        }
        if (IsRanged(kind))
        {
            if (Has(Skill.Marksman)) bonus += 1;
            if (Has(Skill.AimedShot) && Owner.Creature.CombatState?.RoundNumber == 1) bonus += 3;
        }
        return bonus;
    }

    public override decimal ModifyDamageMultiplicative(Creature? target, decimal amount, ValueProp props, Creature? dealer, CardModel? cardSource)
    {
        if (!Owned || dealer != Owner.Creature || !props.IsPoweredAttack() || !IsMyWeaponAttack(cardSource)) return 1m;
        var mult = 1m;
        if (Clumsy) mult *= ProgressionRules.ClumsyDamageMultiplier;
        // «Фехтовальщик»: крит «Острого» меча (каждая 3-я Оружейная атака) ×2 вместо ×1.5.
        if (Has(Skill.Fencer) && MainHand == WeaponKind.Sword && (_weaponAttacksThisCombat + 1) % 3 == 0) mult *= 4m / 3m;
        return mult;
    }

    public override decimal ModifyBlockAdditive(Creature target, decimal block, ValueProp props, CardModel? cardSource, CardPlay? cardPlay) =>
        Owned && target == Owner.Creature && cardSource != null && Has(Skill.ShieldBearer) && HasShield ? 1m : 0m;

    protected override async Task OnOwnAttack(PlayerChoiceContext ctx, AttackCommand command, CardModel card)
    {
        if (!EquipmentCombatRules.IsWeaponAttack(card)) return;
        _weaponAttacksThisCombat++;
        var kind = MainHand;
        var hero = Owner.Creature;
        var targets = command.Results.SelectMany(h => h).Select(r => r.Receiver).OfType<Creature>()
            .Where(t => t.IsAlive).Distinct().ToList();

        var bash = Has(Skill.ShieldBash) && HasShield && !_bashThisTurn && _attacksThisTurn == 0;
        if (bash) _bashThisTurn = true;

        foreach (var target in targets)
        {
            if (Has(Skill.Crusher) && kind == WeaponKind.Mace) await PowerCmd.Apply<SunderPower>(ctx, target, 1, hero, card);
            if (Has(Skill.Bloodletter) && IsBlade(kind)) await PowerCmd.Apply<BleedPower>(ctx, target, 1, hero, card);
            if (Has(Skill.HeavyHand) && kind == WeaponKind.TwoHanded) await PowerCmd.Apply<StaggerPower>(ctx, target, 1, hero, card);
            if (bash) await PowerCmd.Apply<StaggerPower>(ctx, target, 2, hero, card);
        }
        await Enchanting.OnWeaponHit(Owner, ctx, targets, card);
    }

    public override async Task AfterDamageReceived(PlayerChoiceContext choiceContext, Creature target, DamageResult result, ValueProp props, Creature? dealer, CardModel? cardSource)
    {
        // «Вопреки всему»: раз за бой при ♥ ниже 25 % — Латы +3 до конца боя.
        if (!Owned || target != Owner.Creature || _againstAllOddsUsed || !Has(Skill.AgainstAllOdds)) return;
        if (target.CurrentHp <= 0 || target.CurrentHp * 4 >= target.MaxHp) return;
        _againstAllOddsUsed = true;
        Flash();
        await PowerCmd.Apply<PlatingPower>(choiceContext, target, 3, target, null);
    }

    public override bool TryModifyPowerAmountReceived(PowerModel canonicalPower, Creature target, decimal amount, Creature? applier, out decimal modifiedAmount)
    {
        modifiedAmount = amount;
        // «Токсиколог»: ваш Яд +1.
        if (!Owned || !Has(Skill.Toxicologist) || canonicalPower is not VanillaPoison || applier != Owner.Creature
            || target == Owner.Creature || amount <= 0) return false;
        modifiedAmount = amount + 1;
        return true;
    }

    // ═════════════ после боя, комнаты ═════════════

    public override async Task AfterCombatVictory(CombatRoom room)
    {
        if (!RequiemMode.IsInSystem(Owner)) return;
        var rank = room.RoomType switch
        {
            RoomType.Boss => EnemyRank.Boss,
            RoomType.Elite => EnemyRank.Elite,
            _ => EnemyRank.Normal,
        };
        GainRenown(ProgressionRules.RenownFor(rank));
        var stone = CampRules.SoulStoneFor(rank, Acts.ActKinds.Slot(Owner.RunState));
        if (stone > 0) AddStone(stone);

        Meals = FoodRules.Serialize(FoodRules.AfterCombat(MealList));
        if (_mushroomFights > 0) MushroomFights--;
        if (_vigorFights > 0) VigorFights--;
        Meditation = 0;
        await AfterVictoryPaths(room);
        await SyncMaxHp();
        Refresh();
    }

    public override async Task AfterRoomEntered(AbstractRoom room)
    {
        await base.AfterRoomEntered(room);
        if (!RequiemMode.IsInSystem(Owner)) return;
        RecordNativeSchools();
        if (room.RoomType != RoomType.RestSite) NodesSinceRest++;
        Refresh();
    }

    public override Task AfterObtained()
    {
        Refresh();
        return base.AfterObtained();
    }

    // ═════════════ лагерь ═════════════

    public override bool ShouldDisableRemainingRestSiteOptions(Player player) =>
        player != Owner || !Camp.AnyRemaining(Owner);

    // ═════════════ описание ═════════════

    public new void Refresh()
    {
        if (!Owned) return;
        Set("Level", Level);
        Set("Renown", _renown);
        Set("ToNext", ProgressionRules.RenownToNext(_renown));
        Set("StatPoints", UnspentStatPoints);
        Set("SkillPoints", UnspentSkillPoints);
        Set("Skills", Learned.Count);
        Set("Fed", MealList.Count + (_mushroomFights > 0 ? 1 : 0));
        Set("Fatigue", FatigueStacks);
        Set("Stones", TotalStones);
        InvokeDisplayAmountChanged();
    }

    private void Set(string name, int value)
    {
        if (DynamicVars.TryGetValue(name, out var v)) v.BaseValue = value;
    }
}
