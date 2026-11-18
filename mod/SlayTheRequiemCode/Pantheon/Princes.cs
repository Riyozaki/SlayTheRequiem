using BaseLib.Utils;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Commands.Builders;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.Models.RelicPools;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Saves.Runs;
using MegaCrit.Sts2.Core.ValueProps;
using SlayTheRequiem.SlayTheRequiemCode.Core;
using SlayTheRequiem.SlayTheRequiemCode.Equipment;
using SlayTheRequiem.SlayTheRequiemCode.Powers;
using SlayTheRequiem.SlayTheRequiemCode.Rules;
using SlayTheRequiem.SlayTheRequiemCode.Shops;
using SlayTheRequiem.SlayTheRequiemCode.Spells;
using VanillaPoison = MegaCrit.Sts2.Core.Models.Powers.PoisonPower;

namespace SlayTheRequiem.SlayTheRequiemCode.Pantheon;

/// <summary>
/// Пассив Пеплорождённых «Наследники Тройственных» (docs/design/04): дары Тройственных (Измена, Паутина, Сумеречная Звезда)
/// без потери праведности, «без условий» (Тёмное святилище всегда предлагает одного из них) и с ослабленной Ценой
/// (<see cref="PantheonRules.HeirPriceRelief"/>).
/// </summary>
public interface ITribunalHeir { }

/// <summary>Предыстория Ординатора: держит все три дара Тройственных, чужих Князей не принимает.</summary>
public interface ITribunalOrdinator { }

/// <summary>Предыстория Пепельного кочевника: дары Тройственных на ступень сильнее (<see cref="PantheonRules.TribunalTierUp"/>).</summary>
public interface ITribunalAdept { }

/// <summary>
/// Дар Князя Бездны (docs/design/05 §4): Дар и Цена сразу, Обет — условие, после которого дар усиливается.
/// Держать можно один. Принятие — договор с Бездной: сразу Отступник (кроме Тройственных у Пеплорождённых),
/// а пока дар у вас — праведность не выше «Запятнанного» (<see cref="IAbyssalItem"/>).
/// </summary>
public abstract class AbyssGift : BoonRelic, IAbyssalItem
{
    public abstract AbyssPrince Prince { get; }

    private int _vowProgress;
    private bool _vowDone;

    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public int VowProgress
    {
        get => _vowProgress;
        set
        {
            AssertMutable();
            _vowProgress = value;
        }
    }

    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public bool VowDone
    {
        get => _vowDone;
        set
        {
            AssertMutable();
            _vowDone = value;
        }
    }

    public int VowTarget => PantheonRules.VowTarget(Prince);

    /// <summary>Счётчик на иконке — прогресс Обета; после выполнения — цель.</summary>
    public override bool ShowCounter => true;
    public override int DisplayAmount => _vowDone ? VowTarget : _vowProgress;

    /// <summary>Тройственный дар у Наследника — не марает праведность.</summary>
    public bool IsExempt => PantheonRules.IsTribunal(Prince) && IsHeir(Owner);

    /// <summary>Цена ослаблена: Тройственный дар у Пеплорождённого.</summary>
    protected bool HeirPrice => IsExempt;

    /// <summary>Ступень вверх: Тройственный дар у Пепельного кочевника.</summary>
    protected bool TierUp => PantheonRules.IsTribunal(Prince) && Owner != null && Owner.Relics.Any(r => r is ITribunalAdept);

    public static bool IsHeir(Player? player) => player != null && player.Relics.Any(r => r is ITribunalHeir);

    public static bool IsOrdinator(Player? player) => player != null && player.Relics.Any(r => r is ITribunalOrdinator);

    protected override Boon ComputeBoon()
    {
        var boon = PantheonRules.TribunalGift(Prince, HeirPrice, TierUp);
        if (_vowDone) boon += PantheonRules.VowReward(Prince);
        return boon;
    }

    /// <summary>Шаг Обета; по достижении цели — усиление.</summary>
    protected async Task AdvanceVow(int amount = 1)
    {
        if (_vowDone || amount <= 0) return;
        VowProgress = Math.Min(VowTarget, _vowProgress + amount);
        if (_vowProgress >= VowTarget)
        {
            VowDone = true;
            Flash();
            await SyncMaxHp();
            await OnVowFulfilled();
        }
        InvokeDisplayAmountChanged();
    }

    protected virtual Task OnVowFulfilled() => Task.CompletedTask;

    /// <summary>Дар Князя (у Ординатора — первый из трёх Тройственных; все — <see cref="AllOf"/>).</summary>
    public static AbyssGift? Of(Player? player) => player?.Relics.OfType<AbyssGift>().FirstOrDefault();

    public static IEnumerable<AbyssGift> AllOf(Player? player) => player?.Relics.OfType<AbyssGift>() ?? Enumerable.Empty<AbyssGift>();

    /// <summary>Можно ли принять дар: Ординатор принимает только Тройственных (и держит их все разом).</summary>
    public static bool MayAccept(Player player, AbyssPrince prince) =>
        !IsOrdinator(player) || PantheonRules.OrdinatorMayAccept(prince);

    public static readonly Type[] All =
    [
        typeof(GiftOfPlagueLord), typeof(GiftOfTwilightWidow), typeof(GiftOfWebWeaver), typeof(GiftOfGateBreaker),
        typeof(GiftOfTreachery), typeof(GiftOfRevel), typeof(GiftOfOutcast), typeof(GiftOfDuskStar),
    ];

    public static RelicModel Create(AbyssPrince prince) => ModelDb.GetById<RelicModel>(ModelDb.GetId(All[(int)prince])).ToMutable();

    /// <summary>
    /// Принять дар Князя: прежний дар уходит, праведность падает до Отступника (кроме Тройственных у Наследника).
    /// Ординатор держит все три Тройственных разом — у него прежние дары не уходят.
    /// </summary>
    public static async Task Accept(Player player, AbyssPrince prince)
    {
        if (!MayAccept(player, prince)) return;
        if (AllOf(player).Any(g => g.Prince == prince)) return;
        if (!IsOrdinator(player))
            foreach (var old in AllOf(player).ToList())
                await old.RemoveSelf();
        var tribunalHeir = PantheonRules.IsTribunal(prince) && IsHeir(player);
        if (!tribunalHeir) Faith.GraveSin(player);
        await RelicCmd.Obtain(Create(prince), player);
        // Благословение Небесного могло угаснуть — пересчитать его ♥.
        if (CelestialBlessing.Of(player) is { } blessing) await blessing.SyncMaxHp();
        Faith.RefreshAll(player);
    }

    /// <summary>Детерминированный «случай» (одинаков у всех игроков коопа).</summary>
    protected int Roll(int salt, int n) =>
        n <= 0 ? 0 : (int)(ShopRules.Fnv1a((Owner.RunState.Rng.StringSeed ?? string.Empty) + "|" + Prince, Owner.RunState.TotalFloor * 131 + salt) % (uint)n);

    protected bool IsElite(Creature creature) => EnemySetup.RoleOf(creature) == EnemyRole.Elite;
}

// ───────────── Повелитель Мора ─────────────

/// <summary>
/// Повелитель Мора: атаки накладывают Хворь 1; +1 прирост ◆. Цена: в начале каждого акта — случайная Болезнь.
/// Обет «Убей 3 врагов под Хворью» → болезни дают и плюс, и минус.
/// </summary>
[Pool(typeof(SharedRelicPool))]
public sealed class GiftOfPlagueLord : AbyssGift
{
    public override AbyssPrince Prince => AbyssPrince.PlagueLord;

    private string _diseases = string.Empty;  // "0,3,3"
    private int _diseaseAct;                   // акт последней Болезни + 1
    private bool _spellPlayedThisTurn;
    private bool _spellPlayedThisCombat;

    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public string DiseaseList
    {
        get => _diseases;
        set
        {
            AssertMutable();
            _diseases = value ?? string.Empty;
        }
    }

    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public int DiseaseAct
    {
        get => _diseaseAct;
        set
        {
            AssertMutable();
            _diseaseAct = value;
        }
    }

    public IReadOnlyList<int> Diseases =>
        _diseases.Split(',', StringSplitOptions.RemoveEmptyEntries).Select(s => int.TryParse(s, out var v) ? v : -1).Where(v => v >= 0).ToList();

    protected override Boon ComputeBoon() => base.ComputeBoon() + PantheonRules.Diseases(Diseases, VowDone);

    protected override async Task OnObtained()
    {
        // Первая Болезнь — сразу (Цена «сразу»), дальше — в начале каждого акта.
        await Infect();
    }

    public override async Task AfterActEntered()
    {
        if (_diseaseAct != Owner.RunState.CurrentActIndex + 1) await Infect();
    }

    private async Task Infect()
    {
        DiseaseAct = Owner.RunState.CurrentActIndex + 1;
        var disease = Roll(7, PantheonRules.DiseaseCount);
        DiseaseList = string.Join(",", Diseases.Append(disease));
        Flash();
        await SyncMaxHp();
        // Костолом: −3 макс. ♥ за акт за каждый.
        var bonebreak = PantheonRules.Count(Diseases, Disease.Bonebreak);
        if (bonebreak > 0 && Owner.Creature.MaxHp > 1)
            await CreatureCmd.LoseMaxHp(new ThrowingPlayerChoiceContext(), Owner.Creature,
                Math.Min(PantheonRules.BonebreakHpPerAct * bonebreak, Owner.Creature.MaxHp - 1), cardSource: null);
    }

    public override async Task AfterDamageGiven(PlayerChoiceContext choiceContext, Creature? dealer, DamageResult result, ValueProp props, Creature target, CardModel? cardSource)
    {
        if (!IsOwnAttack(dealer, props, cardSource) || target.Monster == null) return;
        var blighted = target.HasPower<BlightPower>();
        if (result.WasTargetKilled)
        {
            if (blighted) await AdvanceVow();
            return;
        }
        if (target.IsAlive && !EnemySetup.IsBlightImmune(target))
            await PowerCmd.Apply<BlightPower>(choiceContext, target, 1, Owner.Creature, cardSource);
    }

    // Размягчение разума: первое заклинание хода +1 энергия. Заумь: первое заклинание боя +2 ✦.

    public override Task BeforeCombatStart()
    {
        _spellPlayedThisCombat = false;
        return base.BeforeCombatStart();
    }

    protected override Task OnTurnStarted(PlayerChoiceContext ctx)
    {
        _spellPlayedThisTurn = false;
        return Task.CompletedTask;
    }

    public override Task AfterCardPlayed(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        if (cardPlay.Card is SpellCard && cardPlay.Card.Owner == Owner && cardPlay.IsLastInSeries)
        {
            _spellPlayedThisTurn = true;
            _spellPlayedThisCombat = true;
        }
        return Task.CompletedTask;
    }

    public override bool TryModifyEnergyCostInCombat(CardModel card, decimal originalCost, out decimal modifiedCost)
    {
        modifiedCost = originalCost;
        if (card.Owner != Owner || card is not SpellCard || _spellPlayedThisTurn) return false;
        var rot = PantheonRules.Count(Diseases, Disease.MindRot);
        if (rot <= 0) return false;
        modifiedCost = originalCost + rot;
        return true;
    }

    public override bool TryModifyStarCost(CardModel card, decimal originalCost, out decimal modifiedCost)
    {
        modifiedCost = originalCost;
        if (card.Owner != Owner || card is not SpellCard || _spellPlayedThisCombat) return false;
        var babble = PantheonRules.Count(Diseases, Disease.Babble);
        if (babble <= 0) return false;
        modifiedCost = originalCost + PantheonRules.BabbleSurcharge * babble;
        return true;
    }
}

// ───────────── Сумеречная Вдова ─────────────

/// <summary>
/// Сумеречная Вдова: первый ход — Невидимость. Цена: −5 макс. ♥.
/// Обет «Выиграй 2 боя, не получив урона в первый ход» → +1 прирост ✦ и Невидимость на 2 хода.
/// </summary>
[Pool(typeof(SharedRelicPool))]
public sealed class GiftOfTwilightWidow : AbyssGift
{
    public override AbyssPrince Prince => AbyssPrince.TwilightWidow;

    private bool _hurtOnFirstTurn;

    public override async Task BeforeCombatStart()
    {
        await base.BeforeCombatStart();
        _hurtOnFirstTurn = false;
        // Спадает на 1 в начале каждого хода героя: 2 — весь первый круг, 3 — два круга.
        await PowerCmd.Apply<InvisibilityPower>(new ThrowingPlayerChoiceContext(), Owner.Creature, VowDone ? 3 : 2, Owner.Creature, null, silent: true);
    }

    public override Task AfterDamageReceived(PlayerChoiceContext choiceContext, Creature target, DamageResult result, ValueProp props, Creature? dealer, CardModel? cardSource)
    {
        if (target == Owner.Creature && result.UnblockedDamage > 0 && (target.CombatState?.RoundNumber ?? 99) <= 1)
            _hurtOnFirstTurn = true;
        return Task.CompletedTask;
    }

    public override async Task AfterCombatVictory(CombatRoom room)
    {
        if (!_hurtOnFirstTurn) await AdvanceVow();
    }
}

// ───────────── Ткачиха Паутины ─────────────

/// <summary>
/// Ткачиха Паутины: вампиризм — 1 ♥ за удар по отравленной цели (до 5 за ход). Цена: лечение на отдыхе −30 %.
/// Обет «Отрави 3 элиты» → +2 урона атакам по целям с ♥ &lt; 50 %.
/// </summary>
[Pool(typeof(SharedRelicPool))]
public sealed class GiftOfWebWeaver : AbyssGift
{
    public override AbyssPrince Prince => AbyssPrince.WebWeaver;

    private int _leechedThisTurn;
    private readonly HashSet<Creature> _poisonedElites = new();

    public override Task BeforeCombatStart()
    {
        _poisonedElites.Clear();
        return base.BeforeCombatStart();
    }

    protected override Task OnTurnStarted(PlayerChoiceContext ctx)
    {
        _leechedThisTurn = 0;
        return Task.CompletedTask;
    }

    public override async Task AfterDamageGiven(PlayerChoiceContext choiceContext, Creature? dealer, DamageResult result, ValueProp props, Creature target, CardModel? cardSource)
    {
        if (!IsOwnAttack(dealer, props, cardSource) || !target.HasPower<VanillaPoison>()) return;
        if (_leechedThisTurn >= PantheonRules.WebLeechCap(TierUp) || Owner.Creature.CurrentHp >= Owner.Creature.MaxHp) return;
        _leechedThisTurn++;
        await CreatureCmd.Heal(Owner.Creature, 1);
    }

    public override async Task AfterPowerAmountChanged(PlayerChoiceContext choiceContext, PowerModel power, decimal amount, Creature? applier, CardModel? cardSource)
    {
        if (VowDone || power is not VanillaPoison || amount <= 0 || applier != Owner.Creature || power.Owner.Monster == null) return;
        if (!IsElite(power.Owner) || !_poisonedElites.Add(power.Owner)) return;
        await AdvanceVow();
    }

    protected override decimal ExtraDamage(Creature? target, ValueProp props, Creature? dealer, CardModel? cardSource) =>
        VowDone && target != null && IsOwnAttack(dealer, props, cardSource) && PantheonRules.BelowHalf(target.CurrentHp, target.MaxHp)
            ? PantheonRules.WebBelowHalfBonus(TierUp) : 0m;
}

// ───────────── Разрушитель Врат ─────────────

/// <summary>
/// Разрушитель Врат: призывы и Бездонное оружие +2 урона; первое заклинание хода −1 ✦. Цена: Отступник навсегда.
/// Обет «Убей элиту Бездны» → аура: враги — уязвимость к огню.
/// </summary>
[Pool(typeof(SharedRelicPool))]
public sealed class GiftOfGateBreaker : AbyssGift, IPermanentApostasy
{
    public const int AbyssalWeaponBonus = 2;

    public override AbyssPrince Prince => AbyssPrince.GateBreaker;

    private bool _spellPlayedThisTurn;

    protected override Task OnTurnStarted(PlayerChoiceContext ctx)
    {
        _spellPlayedThisTurn = false;
        return Task.CompletedTask;
    }

    public override Task AfterCardPlayed(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        if (cardPlay.Card is SpellCard && cardPlay.Card.Owner == Owner && cardPlay.IsLastInSeries) _spellPlayedThisTurn = true;
        return Task.CompletedTask;
    }

    public override bool TryModifyStarCost(CardModel card, decimal originalCost, out decimal modifiedCost)
    {
        modifiedCost = originalCost;
        if (card.Owner != Owner || card is not SpellCard || _spellPlayedThisTurn || originalCost <= 0) return false;
        modifiedCost = originalCost - 1;
        return true;
    }

    protected override decimal ExtraDamage(Creature? target, ValueProp props, Creature? dealer, CardModel? cardSource) =>
        IsOwnAttack(dealer, props, cardSource) && Loadout.InSlot(Owner, EquipSlot.MainHand) is IAbyssalItem ? AbyssalWeaponBonus : 0m;

    public override async Task AfterDamageGiven(PlayerChoiceContext choiceContext, Creature? dealer, DamageResult result, ValueProp props, Creature target, CardModel? cardSource)
    {
        if (VowDone || !result.WasTargetKilled || dealer != Owner.Creature) return;
        if (EnemySetup.IsAbyss(target) && IsElite(target)) await AdvanceVow();
    }
}

// ───────────── Князь Измены ─────────────

/// <summary>
/// Князь Измены: атаки +2, Пробитие 1. Цена: призывы получают на 50 % больше урона.
/// Обет «Победи элиту в одиночку (без призывов)» → +1 карта в первой руке.
/// </summary>
[Pool(typeof(SharedRelicPool))]
public sealed class GiftOfTreachery : AbyssGift
{
    public override AbyssPrince Prince => AbyssPrince.Treachery;

    private bool _hadSummon;

    public override Task BeforeCombatStart()
    {
        _hadSummon = false;
        return base.BeforeCombatStart();
    }

    public override Task AfterPowerAmountChanged(PlayerChoiceContext choiceContext, PowerModel power, decimal amount, Creature? applier, CardModel? cardSource)
    {
        if (power is SummonPower && power.Owner == Owner.Creature) _hadSummon = true;
        return Task.CompletedTask;
    }

    public override async Task AfterCombatVictory(CombatRoom room)
    {
        if (_hadSummon || Owner.Creature.Powers.OfType<SummonPower>().Any()) return;
        if (room.RoomType == RoomType.Elite) await AdvanceVow();
    }
}

// ───────────── Хмельной Князь ─────────────

/// <summary>
/// Хмельной Князь: две Сытости одновременно (<see cref="FoodRules.Slots"/>, Летопись героя). Цена: в начале боя в руку
/// приходит «Похмелье» (Головокружение). Обет «Выпей 3 напитка» (зелья) → вторые свойства напитков
/// (<see cref="PantheonRules.RevelSecondProperty"/>).
/// </summary>
[Pool(typeof(SharedRelicPool))]
public sealed class GiftOfRevel : AbyssGift
{
    public const int SpecialFoodSlots = 2;

    public override AbyssPrince Prince => AbyssPrince.Revel;

    protected override async Task OnCombatStarted(ICombatState combatState)
    {
        var hangover = combatState.CreateCard<Dazed>(Owner);
        await CardPileCmd.AddGeneratedCardToCombat(hangover, PileType.Hand, Owner);
    }

    public override async Task AfterPotionUsed(PotionModel potion, Creature? target)
    {
        if (potion.Owner != Owner) return;
        if (VowDone && potion is Potions.RequiemPotion drink) await SecondProperty(drink);
        await AdvanceVow();
    }

    private async Task SecondProperty(Potions.RequiemPotion drink)
    {
        var (kind, amount) = PantheonRules.RevelSecondProperty(drink.Kind, drink.Tier);
        if (amount <= 0 || !Owner.Creature.IsAlive) return;
        switch (kind)
        {
            case PantheonRules.DrinkBonus.Stamina:
                Resources.RequiemResource.Of<Resources.StaminaResource>(Owner)?.ModifyAmount(amount);
                break;
            case PantheonRules.DrinkBonus.Mana:
                await Mana.Gain(Owner, amount);
                break;
            case PantheonRules.DrinkBonus.Heal:
                await CreatureCmd.Heal(Owner.Creature, amount);
                break;
            case PantheonRules.DrinkBonus.Block:
                if (Owner.Creature.CombatState != null)
                    await CreatureCmd.GainBlock(Owner.Creature, amount, ValueProp.Unpowered, null);
                break;
            default:
                return;
        }
        Flash();
    }
}

// ───────────── Отверженный ─────────────

/// <summary>
/// Отверженный: лечение 2 после каждого боя; тяжёлые атаки +2. Цена: праведность недоступна; лавки +15 %.
/// Обет «Победи босса без зелий» → бонусы удваиваются.
/// </summary>
[Pool(typeof(SharedRelicPool))]
public sealed class GiftOfOutcast : AbyssGift, IPietyForbidder
{
    public override AbyssPrince Prince => AbyssPrince.Outcast;

    private bool _usedPotion;

    protected override Boon ComputeBoon()
    {
        var gift = PantheonRules.Gift(Prince);
        if (!VowDone) return gift;
        // Удваиваются бонусы, а не Цена.
        return gift with { HeavyAttackDamage = gift.HeavyAttackDamage * PantheonRules.OutcastMultiplier(true) };
    }

    public override Task BeforeCombatStart()
    {
        _usedPotion = false;
        return base.BeforeCombatStart();
    }

    public override Task AfterPotionUsed(PotionModel potion, Creature? target)
    {
        if (potion.Owner == Owner) _usedPotion = true;
        return Task.CompletedTask;
    }

    public override async Task AfterCombatVictory(CombatRoom room)
    {
        if (Owner.Creature.IsAlive && Owner.Creature.CurrentHp < Owner.Creature.MaxHp)
        {
            Flash();
            await CreatureCmd.Heal(Owner.Creature, PantheonRules.OutcastAfterCombatHeal(VowDone));
        }
        if (!_usedPotion && room.RoomType == RoomType.Boss) await AdvanceVow();
    }
}

// ───────────── Сумеречная Звезда ─────────────

/// <summary>
/// Сумеречная Звезда: +6 ♥, +2 пул ✦; сопр. огню и холоду I. Цена: звёздные метки — с каждым актом на одну больше;
/// в начале боя столько случайных карт колоды стоят на 1 энергию дороже до конца боя.
/// Обет «Пройди акт без смерти союзников» (призывов и спутников) → Цена снимается.
/// </summary>
[Pool(typeof(SharedRelicPool))]
public sealed class GiftOfDuskStar : AbyssGift, ISummonListener
{
    public override AbyssPrince Prince => AbyssPrince.DuskStar;

    private int _marks = 1;
    private int _act;            // акт, с которого считается Обет, + 1
    private bool _allyDied;
    private readonly HashSet<CardModel> _marked = new();

    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public int StarMarks
    {
        get => _marks;
        set
        {
            AssertMutable();
            _marks = Math.Max(0, value);
        }
    }

    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public int VowAct
    {
        get => _act;
        set
        {
            AssertMutable();
            _act = value;
        }
    }

    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public bool AllyDied
    {
        get => _allyDied;
        set
        {
            AssertMutable();
            _allyDied = value;
        }
    }

    protected override Task OnObtained()
    {
        StarMarks = 1;
        VowAct = Owner.RunState.CurrentActIndex + 1;
        // Акт, начатый без дара, не считается «пройденным с даром»: Обет — со следующего полного акта.
        AllyDied = true;
        return Task.CompletedTask;
    }

    public override async Task AfterActEntered()
    {
        var act = Owner.RunState.CurrentActIndex + 1;
        if (_act == 0 || act == _act) return;
        var keptAllies = !_allyDied;
        VowAct = act;
        AllyDied = false;
        if (keptAllies) await AdvanceVow();
        StarMarks = PantheonRules.StarMarksAfterAct(_marks, VowDone);
        InvokeDisplayAmountChanged();
    }

    public Task AfterSummonDestroyed(SummonPower summon)
    {
        if (summon.Owner == Owner.Creature) AllyDied = true;
        return Task.CompletedTask;
    }

    public override Task AfterDeath(PlayerChoiceContext choiceContext, Creature creature, CardModel? source, bool wasRemovalPrevented, float deathAnimLength)
    {
        // Смерть спутника в коопе или союзного существа на стороне игроков.
        // Гибель призывов считает AfterSummonDestroyed: развеянный (вытесненный из слота) призыв — не смерть союзника.
        if (!wasRemovalPrevented && creature != Owner.Creature && creature.Side == CombatSide.Player
            && creature.Monster is not Monsters.SummonCreature) AllyDied = true;
        return Task.CompletedTask;
    }

    protected override Task OnCombatStarted(ICombatState combatState)
    {
        _marked.Clear();
        var marks = HeirPrice ? PantheonRules.HeirStarMarks(_marks) : _marks;
        if (VowDone || marks <= 0) return Task.CompletedTask;
        var pool = PileType.Draw.GetPile(Owner).Cards.Concat(PileType.Hand.GetPile(Owner).Cards)
            .Where(c => c.Type is CardType.Attack or CardType.Skill or CardType.Power).ToList();
        for (var i = 0; i < marks && pool.Count > 0; i++)
        {
            var pick = pool[Roll(100 + i, pool.Count)];
            pool.Remove(pick);
            _marked.Add(pick);
        }
        if (_marked.Count > 0) Flash();
        return Task.CompletedTask;
    }

    public override Task AfterCombatEnd(CombatRoom room)
    {
        _marked.Clear();
        return Task.CompletedTask;
    }

    public override bool TryModifyEnergyCostInCombat(CardModel card, decimal originalCost, out decimal modifiedCost)
    {
        modifiedCost = originalCost;
        if (VowDone || !_marked.Contains(card) || originalCost < 0) return false;
        modifiedCost = originalCost + 1;
        return true;
    }
}
