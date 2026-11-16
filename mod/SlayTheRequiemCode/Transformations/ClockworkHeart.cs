using BaseLib.Utils;
using Godot;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.CardPools;
using MegaCrit.Sts2.Core.Models.RelicPools;
using MegaCrit.Sts2.Core.Saves.Runs;
using MegaCrit.Sts2.Core.ValueProps;
using SlayTheRequiem.SlayTheRequiemCode.Cards;
using SlayTheRequiem.SlayTheRequiemCode.Core;
using SlayTheRequiem.SlayTheRequiemCode.Pantheon;
using SlayTheRequiem.SlayTheRequiemCode.Powers;
using SlayTheRequiem.SlayTheRequiemCode.Resources;
using SlayTheRequiem.SlayTheRequiemCode.Rules;
using VanillaPoison = MegaCrit.Sts2.Core.Models.Powers.PoisonPower;

namespace SlayTheRequiem.SlayTheRequiemCode.Transformations;

/// <summary>
/// Заводное сердце (docs/design/06 §1.5, облик Дефекта). Сущность «Ядро»: Латы +2; иммунитет к Яду, Хвори, Кровотечению;
/// лечение −50 % (это же «Восстановление лечит вдвое слабее» — штрафы не складываются вдвойне). Сферы стихий: в начале боя
/// 1 сфера (по кругу Пар → Искра → Иней от боя к бою), новые — картой «Подзавод», до 3. Минус: разряд по вам выжигает и ◆.
/// Совместимо только с Праведником; не делает Отступником.
/// </summary>
[Pool(typeof(SharedRelicPool))]
public sealed class ClockworkHeart : FormRelic
{
    public override FormKind Kind => FormKind.Clockwork;
    protected override IEnumerable<Type> FormCards => [typeof(Windup)];
    public override Color? Tint => new Color(0.9f, 0.85f, 0.75f);
    public override Color FlashColor => new(0.4f, 0.8f, 1f);

    private int _combats;

    /// <summary>Номер боя — от него стихия первой сферы.</summary>
    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public int Combats
    {
        get => _combats;
        set
        {
            AssertMutable();
            _combats = Math.Max(0, value);
        }
    }

    protected override Boon ComputeBoon() => new() { Plating = TransformationRules.ClockworkPlating };

    public override int HealPercent(HealSource source) => TransformationRules.ClockworkHealPercent;

    public override bool ImmuneTo(PowerModel canonicalPower) =>
        canonicalPower is VanillaPoison or WeaponPoisonPower or BlightPower or BleedPower;

    protected override async Task OnCombatStarted(ICombatState combatState)
    {
        await base.OnCombatStarted(combatState);
        var element = TransformationRules.OrbFor(_combats);
        Combats = _combats + 1;
        await ElementOrbs.Add(new ThrowingPlayerChoiceContext(), Owner.Creature, element);
    }

    /// <summary>Разряд по вам выжигает и ◆ (✦ — общим правилом).</summary>
    public override Task AfterDamageReceived(PlayerChoiceContext choiceContext, Creature target, DamageResult result, ValueProp props, Creature? dealer, CardModel? cardSource)
    {
        if (!Owned || target != Owner.Creature || result.UnblockedDamage <= 0 || !target.IsAlive) return Task.CompletedTask;
        if (DamageTyping.Resolve(props, dealer, cardSource) != DamageType.Shock) return Task.CompletedTask;
        if (RequiemResource.Of<StaminaResource>(Owner) is { } stamina)
            stamina.Amount = Math.Max(0, stamina.Amount - result.UnblockedDamage);
        return Task.CompletedTask;
    }
}

/// <summary>Сферы стихий героя: по силе на стихию, Amount — число сфер.</summary>
public static class ElementOrbs
{
    public static int Count(Creature hero) => hero.Powers.OfType<ElementOrbPower>().Sum(p => p.Amount);

    public static async Task<bool> Add(PlayerChoiceContext ctx, Creature hero, Element element)
    {
        if (Count(hero) >= TransformationRules.MaxOrbs) return false;
        switch (element)
        {
            case Element.Fire: await PowerCmd.Apply<SteamOrbPower>(ctx, hero, 1, hero, null); break;
            case Element.Shock: await PowerCmd.Apply<SparkOrbPower>(ctx, hero, 1, hero, null); break;
            default: await PowerCmd.Apply<RimeOrbPower>(ctx, hero, 1, hero, null); break;
        }
        return true;
    }
}

/// <summary>Сфера стихии: в конце хода героя каждая сфера накладывает свою стихию случайному врагу.</summary>
public abstract class ElementOrbPower : RequiemPower
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    protected abstract Task Pulse(PlayerChoiceContext ctx, Creature enemy);

    // Сферы видны над героем как сферы Дефекта (M6 п. 3, HeroOverlays).
    public override Task AfterApplied(Creature? applier, CardModel? cardSource)
    {
        HeroOverlays.RefreshOrbs(Owner);
        return Task.CompletedTask;
    }

    public override Task AfterPowerAmountChanged(PlayerChoiceContext choiceContext, PowerModel power, decimal amount, Creature? applier, CardModel? cardSource)
    {
        if (power == this) HeroOverlays.RefreshOrbs(Owner);
        return Task.CompletedTask;
    }

    public override Task AfterRemoved(Creature oldOwner)
    {
        HeroOverlays.RefreshOrbs(oldOwner);
        return Task.CompletedTask;
    }

    public override async Task BeforeSideTurnEnd(PlayerChoiceContext choiceContext, CombatSide side, IEnumerable<Creature> participants)
    {
        if (!participants.Contains(Owner) || Owner.CombatState == null || Owner.Player == null) return;
        for (var i = 0; i < Amount; i++)
        {
            var enemies = Owner.CombatState.HittableEnemies.Where(e => e.IsAlive).ToList();
            if (enemies.Count == 0) return;
            var enemy = Owner.Player.RunState.Rng.CombatTargets.NextItem(enemies)!;
            Flash();
            await Pulse(choiceContext, enemy);
        }
    }
}

/// <summary>Пар: Горение 3.</summary>
public sealed class SteamOrbPower : ElementOrbPower
{
    protected override Task Pulse(PlayerChoiceContext ctx, Creature enemy) =>
        PowerCmd.Apply<BurningPower>(ctx, enemy, TransformationRules.SteamBurning, Owner, null);
}

/// <summary>Искра: Статика 1 и 3 урона разрядом.</summary>
public sealed class SparkOrbPower : ElementOrbPower
{
    protected override async Task Pulse(PlayerChoiceContext ctx, Creature enemy)
    {
        await PowerCmd.Apply<StaticPower>(ctx, enemy, TransformationRules.SparkStatic, Owner, null);
        if (!enemy.IsAlive) return;
        using (DamageTyping.As(DamageType.Shock))
            await CreatureCmd.Damage(ctx, enemy, TransformationRules.SparkDamage, ValueProp.Unpowered, Owner, null);
    }
}

/// <summary>Иней: Озноб 2.</summary>
public sealed class RimeOrbPower : ElementOrbPower
{
    protected override Task Pulse(PlayerChoiceContext ctx, Creature enemy) =>
        PowerCmd.Apply<ChillPower>(ctx, enemy, TransformationRules.RimeChill, Owner, null);
}

/// <summary>Подзавод: 1⚡ — новая сфера стихии (по кругу Пар → Искра → Иней; до 3). Улучшение — 0⚡.</summary>
[Pool(typeof(TokenCardPool))]
public sealed class Windup : RequiemCard
{
    public Windup() : base(1, CardType.Skill, CardRarity.Event, TargetType.Self)
    {
        WithVar("Max", TransformationRules.MaxOrbs);
        WithCostUpgradeBy(-1);
    }

    protected override async Task OnPlay(PlayerChoiceContext ctx, CardPlay play)
    {
        var next = TransformationRules.OrbFor(ElementOrbs.Count(Owner.Creature) + (Forms.Of<ClockworkHeart>(Owner)?.Combats ?? 0) + 2);
        await ElementOrbs.Add(ctx, Owner.Creature, next);
    }
}
