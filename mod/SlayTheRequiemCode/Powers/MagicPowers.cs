using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.ValueProps;
using SlayTheRequiem.SlayTheRequiemCode.Core;
using SlayTheRequiem.SlayTheRequiemCode.Rules;

namespace SlayTheRequiem.SlayTheRequiemCode.Powers;

/// <summary>Магический ли урон (заклинание героя или стихийная/магическая атака врага-мага).</summary>
internal static class MagicDamage
{
    public static bool IsSpell(ValueProp props, Creature? dealer, CardModel? cardSource)
    {
        if (cardSource is Spells.SpellCard) return true;
        // Тики статусов (Горение, Статика) — не заклинания; заклинание врага — его стихийная/магическая атака.
        if (dealer?.Monster == null || !props.IsCardOrMonsterMove()) return false;
        var type = DamageTyping.Resolve(props, dealer, cardSource);
        return type is not (DamageType.Physical or DamageType.True or DamageType.Poison or DamageType.Blight);
    }
}

/// <summary>
/// Оберег N — «Блок против заклинаний» (docs/design/03): поглощает урон магией и стихиями до N, сгорает в начале хода носителя.
/// </summary>
public sealed class SpellShieldPower : RequiemPower
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    private int _pending;
    private int _pendingStatus;

    internal decimal MitigateHit(Creature target, decimal amount, ValueProp props, Creature? dealer, CardModel? cardSource)
    {
        if (target != Owner || amount <= 0 || !MagicDamage.IsSpell(props, dealer, cardSource)) return amount;
        var absorbed = SpellDefenseRules.WardUsed(amount, Amount - _pending - _pendingStatus);
        if (absorbed <= 0) return amount;
        if (!HitDefensePipeline.WillCommit) return amount - absorbed;
        _pending += absorbed;
        var owner = Owner;
        HitDefensePipeline.Defer(async () =>
        {
            _pending -= absorbed;
            Flash();
            if (absorbed >= Amount) await PowerCmd.Remove(this);
            else await PowerCmd.ModifyAmount(new ThrowingPlayerChoiceContext(), this, -absorbed, null, null, silent: true);
            await Transformations.ReflectorEssence.OnWardAbsorbed(new ThrowingPlayerChoiceContext(), owner, dealer, absorbed);
        }, () => _pending -= absorbed);
        return amount - absorbed;
    }

    internal decimal ProtectStatus(PowerModel power, decimal amount, Creature? dealer)
    {
        var used = SpellDefenseRules.WardUsed(amount, Amount - _pending - _pendingStatus);
        if (used <= 0) return amount;
        _pendingStatus += used;
        var owner = Owner;
        try
        {
            PowerDefensePipeline.Defer(async () =>
            {
                _pendingStatus -= used;
                Flash();
                if (used >= Amount) await PowerCmd.Remove(this);
                else await PowerCmd.ModifyAmount(new ThrowingPlayerChoiceContext(), this, -used, null, null, silent: true);
                await Transformations.ReflectorEssence.OnWardAbsorbed(new ThrowingPlayerChoiceContext(), owner, dealer, used);
            }, () => _pendingStatus -= used);
        }
        catch { _pendingStatus -= used; throw; }
        return amount - used;
    }

    public override Task AfterModifyingPowerAmountReceived(PowerModel power) => PowerDefensePipeline.Commit();

    public override async Task AfterSideTurnStart(CombatSide side, IReadOnlyList<Creature> participants, ICombatState combatState)
    {
        if (participants.Contains(Owner)) await PowerCmd.Remove(this);
    }
}

/// <summary>Основа Поглощения: магический удар по носителю гасится целиком и даёт ✦.</summary>
public abstract class AbsorptionPowerBase : RequiemPower
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    protected abstract int ManaPerAbsorb(Creature? dealer);

    internal async Task Absorb(PlayerChoiceContext ctx, Creature? dealer)
    {
        if (Amount <= 0) return;
        var gain = ManaPerAbsorb(dealer);
        Flash();
        // Spend first: a nested mana-gain callback cannot reuse this charge.
        await PowerCmd.Decrement(this);
        if (Owner.IsPlayer) await Mana.Gain(Owner.Player, gain);
        else if (Owner.GetPower<EnemyManaPower>() is { } mana) mana.Set(mana.DisplayAmount + gain);
    }
}

/// <summary>Первые N вражеских заклинаний по носителю гасятся (все удары и прямые статусы) и дают 2 ✦. Свет — исключение.</summary>
public sealed class SpellAbsorptionPower : AbsorptionPowerBase
{
    protected override int ManaPerAbsorb(Creature? dealer) => 2;

    public override async Task AfterSideTurnStart(CombatSide side, IReadOnlyList<Creature> participants, ICombatState combatState)
    {
        if (participants.Contains(Owner)) await PowerCmd.Remove(this);
    }
}

/// <summary>Туманная кровь (бретонцы): первое вражеское заклинание в бою поглощается; ✦ = цена заклинания врага.</summary>
public sealed class MistbloodPower : AbsorptionPowerBase
{
    protected override int ManaPerAbsorb(Creature? dealer) => dealer == null ? EnemyRules.MinSpellMana : EnemyActionBudget.CostOf(dealer);
}

/// <summary>
/// Плащ стихии N (docs/design/03): когда носителя бьют атакой, атакующий получает N урона стихией и её статус (N/2, мин. 1).
/// Длится <see cref="Duration"/> ходов носителя.
/// </summary>
public abstract class CloakPower : RequiemPower
{
    public const int Duration = 3;
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    protected abstract DamageType Element { get; }
    protected abstract Task ApplyStatus(PlayerChoiceContext ctx, Creature attacker, int stacks);

    private int _turnsLeft = Duration;
    private bool _permanent;

    /// <summary>Плащ аффикса «Огненный/Ледяной/Грозовой» — не спадает.</summary>
    public void MakePermanent() => _permanent = true;

    /// <summary>Повторное наложение продлевает плащ.</summary>
    public override Task AfterPowerAmountChanged(PlayerChoiceContext choiceContext, PowerModel power, decimal amount, Creature? applier, CardModel? cardSource)
    {
        if (power == this && amount > 0) _turnsLeft = Duration;
        return Task.CompletedTask;
    }

    public override async Task AfterDamageReceived(PlayerChoiceContext choiceContext, Creature target, DamageResult result, ValueProp props, Creature? dealer, CardModel? cardSource)
    {
        if (target != Owner || dealer == null || dealer == Owner || !dealer.IsAlive || !props.IsPoweredAttack()) return;
        Flash();
        using (DamageTyping.As(Element))
            await CreatureCmd.Damage(choiceContext, dealer, Amount, ValueProp.Unpowered, Owner, null, null);
        if (dealer.IsAlive) await ApplyStatus(choiceContext, dealer, Math.Max(1, Amount / 2));
    }

    public override async Task AfterSideTurnEnd(PlayerChoiceContext choiceContext, CombatSide side, IEnumerable<Creature> participants)
    {
        if (!participants.Contains(Owner) || _permanent) return;
        if (--_turnsLeft <= 0) await PowerCmd.Remove(this);
    }
}

public sealed class FireCloakPower : CloakPower
{
    protected override DamageType Element => DamageType.Fire;
    protected override Task ApplyStatus(PlayerChoiceContext ctx, Creature attacker, int stacks) =>
        PowerCmd.Apply<BurningPower>(ctx, attacker, stacks, Owner, null);
}

public sealed class FrostCloakPower : CloakPower
{
    protected override DamageType Element => DamageType.Frost;
    protected override Task ApplyStatus(PlayerChoiceContext ctx, Creature attacker, int stacks) =>
        PowerCmd.Apply<ChillPower>(ctx, attacker, stacks, Owner, null);
}

public sealed class ShockCloakPower : CloakPower
{
    protected override DamageType Element => DamageType.Shock;
    protected override Task ApplyStatus(PlayerChoiceContext ctx, Creature attacker, int stacks) =>
        PowerCmd.Apply<StaticPower>(ctx, attacker, stacks, Owner, null);
}

/// <summary>
/// Хворь N (docs/design/03): носитель не лечится (патч CreatureCmd.Heal), каждый тик Яда по нему — ещё +N урона хворью.
/// Не убывает до конца боя.
/// </summary>
public sealed class BlightPower : RequiemPower
{
    public override PowerType Type => PowerType.Debuff;
    public override PowerStackType StackType => PowerStackType.Counter;

    public override async Task AfterSideTurnStart(CombatSide side, IReadOnlyList<Creature> participants, ICombatState combatState)
    {
        if (!participants.Contains(Owner) || Amount <= 0 || !Owner.HasPower<PoisonPower>()) return;
        using (DamageTyping.As(DamageType.Blight))
            await CreatureCmd.Damage(new ThrowingPlayerChoiceContext(), Owner, Amount, ValueProp.Unblockable | ValueProp.Unpowered, null, null, null);
    }
}

/// <summary>Временные Латы (Дубовая/Каменная плоть): +N Лат на 3 хода, затем снимаются вместе с этими Латами.</summary>
public sealed class TemporaryPlatingPower : RequiemPower
{
    public const int Duration = 3;
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    private int _turnsLeft = Duration;

    public override Task AfterPowerAmountChanged(PlayerChoiceContext choiceContext, PowerModel power, decimal amount, Creature? applier, CardModel? cardSource)
    {
        if (power == this && amount > 0) _turnsLeft = Duration;
        return Task.CompletedTask;
    }

    public override async Task AfterSideTurnEnd(PlayerChoiceContext choiceContext, CombatSide side, IEnumerable<Creature> participants)
    {
        if (participants.Contains(Owner) && --_turnsLeft <= 0) await PowerCmd.Remove(this);
    }
}

/// <summary>Нельзя Парализовать до конца боя (Великий оберег).</summary>
public sealed class ParalysisWardPower : RequiemPower
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Single;
}

/// <summary>Огненная руна N: следующий враг, ударивший носителя атакой, получает N урона огнём и Горение N/2. Одноразовая.</summary>
public sealed class FireRunePower : RequiemPower
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    public override async Task AfterDamageReceived(PlayerChoiceContext choiceContext, Creature target, DamageResult result, ValueProp props, Creature? dealer, CardModel? cardSource)
    {
        if (target != Owner || dealer == null || dealer == Owner || !dealer.IsAlive || !props.IsPoweredAttack()) return;
        var amount = Amount;
        Flash();
        await PowerCmd.Remove(this);
        using (DamageTyping.As(DamageType.Fire))
            await CreatureCmd.Damage(choiceContext, dealer, amount, ValueProp.Unpowered, Owner, null, null);
        if (dealer.IsAlive) await PowerCmd.Apply<BurningPower>(choiceContext, dealer, Math.Max(1, amount / 2), Owner, null);
    }
}

/// <summary>Стена пламени N: в начале каждого из N следующих ходов носителя-героя все враги получают Горение (сила хранится в <see cref="Burn"/>).</summary>
public sealed class FlameWallPower : RequiemPower
{
    public const int Burn = 6;
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    public override async Task AfterSideTurnStart(CombatSide side, IReadOnlyList<Creature> participants, ICombatState combatState)
    {
        if (!participants.Contains(Owner) || Amount <= 0) return;
        Flash();
        await PowerCmd.Apply<BurningPower>(new ThrowingPlayerChoiceContext(), combatState.HittableEnemies, Burn, Owner, null);
        await PowerCmd.Decrement(this);
    }
}

/// <summary>Страх: носитель наносит на 50 % меньше урона атаками. −1 в конце хода носителя.</summary>
public sealed class FearPower : RequiemPower
{
    public override PowerType Type => PowerType.Debuff;
    public override PowerStackType StackType => PowerStackType.Counter;

    public override decimal ModifyDamageMultiplicative(Creature? target, decimal amount, ValueProp props, Creature? dealer, CardModel? cardSource, CardPlay? cardPlay) =>
        dealer == Owner && props.IsCardOrMonsterMove() ? 0.5m : 1m;

    public override async Task AfterSideTurnEnd(PlayerChoiceContext choiceContext, CombatSide side, IEnumerable<Creature> participants)
    {
        if (participants.Contains(Owner)) await PowerCmd.Decrement(this);
    }
}

/// <summary>
/// Рыцари-духи N (Хранитель Гробницы): пока N &gt; 0, носитель получает вдвое меньше урона, а отнятая половина бьёт
/// по рыцарям (свет и огонь — вдвое). Ударивший носителя в ближнем бою получает Шаткость 2 (щиты рыцарей).
/// </summary>
public sealed class SpectralGuardPower : RequiemPower
{
    public const int MeleeStagger = 2;
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    private int _pending;

    internal decimal MitigateHit(Creature target, decimal amount, ValueProp props, Creature? dealer, CardModel? cardSource)
    {
        if (target != Owner || amount <= 0 || Amount <= 0) return amount;
        var half = (int)Math.Ceiling(amount / 2m);
        var type = DamageTyping.Resolve(props, dealer, cardSource);
        var toKnights = type is DamageType.Light or DamageType.Fire ? half * 2 : half;
        var hit = Math.Min(Math.Max(0, Amount - _pending), toKnights);
        if (hit <= 0) return amount;
        var protection = type is DamageType.Light or DamageType.Fire ? (int)Math.Ceiling(hit / 2m) : hit;
        var remaining = amount - Math.Min(half, protection);
        if (!HitDefensePipeline.WillCommit) return remaining;
        _pending += hit;
        var owner = Owner;
        HitDefensePipeline.Defer(async () =>
        {
            _pending -= hit;
            Flash();
            if (hit >= Amount) await PowerCmd.Remove(this);
            else await PowerCmd.ModifyAmount(new ThrowingPlayerChoiceContext(), this, -hit, null, null, silent: true);
            if (dealer is { IsPlayer: true, IsAlive: true } && props.IsPoweredAttack() && cardSource is not Spells.SpellCard)
                await PowerCmd.Apply<StaggerPower>(new ThrowingPlayerChoiceContext(), dealer, MeleeStagger, owner, null);
        }, () => _pending -= hit);
        // A nearly destroyed guard cannot absorb an unbounded half-hit with its last HP.
        return remaining;
    }
}
