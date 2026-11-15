using BaseLib.Utils;
using Godot;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.CardPools;
using MegaCrit.Sts2.Core.Models.RelicPools;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Saves.Runs;
using MegaCrit.Sts2.Core.ValueProps;
using SlayTheRequiem.SlayTheRequiemCode.Cards;
using SlayTheRequiem.SlayTheRequiemCode.Core;
using SlayTheRequiem.SlayTheRequiemCode.Pantheon;
using SlayTheRequiem.SlayTheRequiemCode.Powers;
using SlayTheRequiem.SlayTheRequiemCode.Rules;
using VanillaIntangible = MegaCrit.Sts2.Core.Models.Powers.IntangiblePower;

namespace SlayTheRequiem.SlayTheRequiemCode.Transformations;

/// <summary>
/// Дитя Ночи — вампир (docs/design/06 §1.2, путь Владыки Цепей). Сущность «Жажда» 0–4: +1 за каждый бой без кормления.
/// 0 — Регенерация 1, уязвимость к огню I; 2–3 — атаки +2, огонь и свет ×1.5, лечение −50 %; 4 — атаки +4, вампиризм 25 %,
/// огонь/свет ×2, лечение только кормлением. Кормление — карта «Укус» (Жажда → 0). «Крылья ночи» — раз за бой при
/// ♥ &lt; 25 %: Неуязвимость 1 и бесплатный ход. После 3 кормлений — «Кровавый лорд» (карта-стойка).
/// Пока форма есть — Отступник.
/// </summary>
[Pool(typeof(SharedRelicPool))]
public sealed class VampireForm : FormRelic, ITransformationForm
{
    public override FormKind Kind => FormKind.Vampire;
    public bool IsActiveForm => true;
    protected override IEnumerable<Type> FormCards => [typeof(Bite)];
    public override Color? Tint => new Color(0.86f, 0.84f, 0.96f);
    public override Color FlashColor => new(0.75f, 0.05f, 0.1f);

    public override bool ShowCounter => true;
    public override int DisplayAmount => Owned ? _thirst : 0;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
        [new DynamicVar("Thirst", 0), new DynamicVar("Feedings", 0)];

    private int _thirst;
    private int _feedings;
    private bool _bloodLord;

    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public int Thirst
    {
        get => _thirst;
        set
        {
            AssertMutable();
            _thirst = Math.Clamp(value, 0, TransformationRules.MaxThirst);
            SyncVars();
        }
    }

    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public int Feedings
    {
        get => _feedings;
        set
        {
            AssertMutable();
            _feedings = Math.Max(0, value);
            SyncVars();
        }
    }

    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public bool BloodLord
    {
        get => _bloodLord;
        set
        {
            AssertMutable();
            _bloodLord = value;
        }
    }

    private void SyncVars()
    {
        if (DynamicVars.TryGetValue("Thirst", out var t)) t.BaseValue = _thirst;
        if (DynamicVars.TryGetValue("Feedings", out var f)) f.BaseValue = _feedings;
        InvokeDisplayAmountChanged();
    }

    public ThirstRow Row => TransformationRules.Thirst(_thirst);

    protected override Boon ComputeBoon()
    {
        var row = Row;
        return new Boon { Regen = row.Regen, FireRes = row.FireResistance, AttackDamage = row.AttackBonus };
    }

    public override int HealPercent(HealSource source) => source == HealSource.Feeding ? 0 : Row.HealPercent;
    public override bool BlocksHealing(HealSource source) => Row.HealingOnlyByFeeding && source != HealSource.Feeding;

    /// <summary>Огонь и свет по вампиру ×1.5 / ×2 (уязвимость к огню при сытости — через сопротивление −1).</summary>
    public override decimal ModifyDamageMultiplicative(Creature? target, decimal amount, ValueProp props, Creature? dealer, CardModel? cardSource)
    {
        if (!Owned || target != Owner.Creature || amount <= 0) return 1m;
        var type = DamageTyping.Resolve(props, dealer, cardSource);
        return type is DamageType.Fire or DamageType.Light ? Row.FireLightTakenMultiplier : 1m;
    }

    // ───── бой ─────

    private bool _fedThisCombat;
    private bool _wingsUsed;
    private bool _extraTurnPending;

    public override Task BeforeCombatStart()
    {
        _fedThisCombat = _wingsUsed = _extraTurnPending = false;
        return base.BeforeCombatStart();
    }

    /// <summary>Кормление (Укус): Жажда → 0; третье — «Кровавый лорд».</summary>
    public async Task Feed()
    {
        _fedThisCombat = true;
        Thirst = 0;
        Feedings = _feedings + 1;
        Flash();
        if (!_bloodLord && TransformationRules.BecomesBloodLord(_feedings))
        {
            BloodLord = true;
            var canonical = ModelDb.GetById<CardModel>(ModelDb.GetId(typeof(BloodLordStance)));
            await CardPileCmd.Add(Owner.RunState.CreateCard(canonical, Owner), PileType.Deck);
        }
    }

    /// <summary>Вампиризм (голод — 25 %; стойка Кровавого лорда — ещё 25 %) и Хворь Кровавого лорда.</summary>
    public override async Task AfterDamageGiven(PlayerChoiceContext choiceContext, Creature? dealer, DamageResult result, ValueProp props, Creature target, CardModel? cardSource)
    {
        if (!Owned || dealer != Owner.Creature || target == Owner.Creature || !props.IsPoweredAttack()) return;
        var lord = Owner.Creature.HasPower<BloodLordStancePower>();
        if (lord && target.IsAlive && !EnemySetup.IsBlightImmune(target))
            await PowerCmd.Apply<BlightPower>(choiceContext, target, TransformationRules.BloodLordBlight, Owner.Creature, null);
        var percent = Row.LifestealPercent + (lord ? TransformationRules.BloodLordLifestealPercent : 0);
        var heal = TransformationRules.Lifesteal(result.UnblockedDamage, percent);
        if (heal <= 0 || !Owner.Creature.IsAlive) return;
        using (Healing.As(HealSource.Feeding))
            await CreatureCmd.Heal(Owner.Creature, heal);
    }

    /// <summary>Крылья ночи: раз за бой при ♥ &lt; 25 % — Неуязвимость 1 и бесплатный ход.</summary>
    public override async Task AfterDamageReceived(PlayerChoiceContext choiceContext, Creature target, DamageResult result, ValueProp props, Creature? dealer, CardModel? cardSource)
    {
        if (!Owned || target != Owner.Creature || !target.IsAlive) return;
        if (!TransformationRules.NightWingsTrigger(target.CurrentHp, target.MaxHp, _wingsUsed)) return;
        _wingsUsed = true;
        _extraTurnPending = true;
        Flash();
        FormVisuals.Refresh(Owner, flash: new Color(0.1f, 0.05f, 0.15f));
        await PowerCmd.Apply<VanillaIntangible>(choiceContext, target, 1, target, null);
    }

    public override bool ShouldTakeExtraTurn(Player player) => Owned && player == Owner && _extraTurnPending;

    public override Task AfterTakingExtraTurn(Player player)
    {
        if (Owned && player == Owner) _extraTurnPending = false;
        return Task.CompletedTask;
    }

    public override Task AfterCombatVictory(CombatRoom room)
    {
        if (!Owned) return Task.CompletedTask;
        Thirst = TransformationRules.ThirstAfterCombat(_thirst, _fedThisCombat);
        return Task.CompletedTask;
    }
}

/// <summary>Укус: 0⚡, Изгнание — 6 урона и вылечить нанесённое; Жажда → 0 (кормление).</summary>
[Pool(typeof(TokenCardPool))]
public sealed class Bite : RequiemCard
{
    public Bite() : base(0, CardType.Attack, CardRarity.Event, TargetType.AnyEnemy)
    {
        WithDamage(TransformationRules.BiteDamage, TransformationRules.BiteDamageUpgrade);
        WithKeyword(CardKeyword.Exhaust);
    }

    protected override async Task OnPlay(PlayerChoiceContext ctx, CardPlay play)
    {
        ArgumentNullException.ThrowIfNull(play.Target);
        var before = play.Target.CurrentHp;
        await Hit(ctx, play.Target, "vfx/vfx_attack_slash");
        var dealt = Math.Max(0, before - play.Target.CurrentHp);
        if (dealt > 0 && Owner.Creature.IsAlive)
            using (Healing.As(HealSource.Feeding))
                await CreatureCmd.Heal(Owner.Creature, dealt);
        if (Forms.Of<VampireForm>(Owner) is { } form) await form.Feed();
    }
}

/// <summary>Кровавый лорд: 0⚡ — войти в форму-стойку или выйти из неё. В стойке атаки — Хворь 1 и вампиризм 25 %.</summary>
[Pool(typeof(TokenCardPool))]
public sealed class BloodLordStance : RequiemCard
{
    /// <summary>Карта облика: растёт ступенями формы (06), а не улучшением — решение card spec §3.4 п. 3, док 18.</summary>
    public override int MaxUpgradeLevel => 0;

    public BloodLordStance() : base(0, CardType.Skill, CardRarity.Event, TargetType.Self)
    {
    }

    protected override async Task OnPlay(PlayerChoiceContext ctx, CardPlay play)
    {
        if (Owner.Creature.GetPower<BloodLordStancePower>() is { } stance) await PowerCmd.Remove(stance);
        else await Apply<BloodLordStancePower>(ctx, Owner.Creature, 1);
    }
}

/// <summary>Стойка Кровавого лорда (эффект — в <see cref="VampireForm.AfterDamageGiven"/>).</summary>
public sealed class BloodLordStancePower : RequiemPower
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Single;

    // Облик «Кровавый лорд»: спрайт вампира с крыльями (M6 п. 3).
    public override Task AfterApplied(Creature? applier, CardModel? cardSource)
    {
        FormVisuals.Refresh(Owner.Player, flash: new Color(0.9f, 0.1f, 0.2f));
        return Task.CompletedTask;
    }

    public override Task AfterRemoved(Creature oldOwner)
    {
        FormVisuals.Refresh(oldOwner.Player);
        return Task.CompletedTask;
    }
}
