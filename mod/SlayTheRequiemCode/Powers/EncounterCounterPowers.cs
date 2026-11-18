using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;
using SlayTheRequiem.SlayTheRequiemCode.Core;

namespace SlayTheRequiem.SlayTheRequiemCode.Powers;

/// <summary>Two full enemy-side actions, including the smelting action. Hit refreshes to two, never adds two per hit.</summary>
public sealed class ValveClosedPower : RequiemPower
{
    public override PowerType Type => PowerType.Debuff;
    public override PowerStackType StackType => PowerStackType.Counter;
    public override async Task AfterSideTurnEnd(PlayerChoiceContext choiceContext, CombatSide side, IEnumerable<Creature> participants)
    {
        if (side != CombatSide.Enemy || !Owner.IsAlive) return;
        if (BossCounterplayRules.TurnsAfterEnemyAction(Amount) == 0) await PowerCmd.Remove(this);
        else await PowerCmd.Decrement(this);
    }
}

/// <summary>Source-qualified protection: only this Envoy's move damage, to this hero, for the next enemy turn.</summary>
public sealed class SolarStandardGuardPower : RequiemPower
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Single;
    public override decimal ModifyDamageMultiplicative(Creature? target, decimal amount, ValueProp props, Creature? dealer, CardModel? cardSource) =>
        target == Owner && dealer != null && dealer == Applier && props.IsCardOrMonsterMove() ? 0.5m : 1m;
    public override async Task AfterSideTurnEnd(PlayerChoiceContext choiceContext, CombatSide side, IEnumerable<Creature> participants)
    {
        if (side == CombatSide.Enemy) await PowerCmd.Remove(this);
    }
}

/// <summary>Read-only aura queried by ResistancePower. Does not subtract from permanent hero resistance counters.</summary>
public sealed class BlackBookAuraPower : RequiemPower
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Single;
}

/// <summary>World-Eater ticks this after the whole enemy side acts, then restores Flight exactly once.</summary>
public sealed class GroundedByShoutPower : RequiemPower
{
    public override PowerType Type => PowerType.Debuff;
    public override PowerStackType StackType => PowerStackType.Counter;
}

public sealed class MeteorWarningPower : RequiemPower
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Single;
}

/// <summary>Visible full-hand entitlement; no card/rarity/mastery/claim in the persistent deck or TravelPack.</summary>
public sealed class EncounterCounterPendingPower : RequiemPower
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;
}


/// <summary>Explicit, visible Pierce for enemy Attacks too. Shared armor stage reads it before native caps.</summary>
public sealed class ArmorPiercingPower : RequiemPower
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;
}
