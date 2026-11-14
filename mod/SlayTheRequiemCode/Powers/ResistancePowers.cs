using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using SlayTheRequiem.SlayTheRequiemCode.Core;

namespace SlayTheRequiem.SlayTheRequiemCode.Powers;

/// <summary>
/// Сопротивление стихии: Amount = ступень. −1 уязвимость (×1.5), 1/2/3 = −25/50/75 %, 4+ иммунитет.
/// Сопротивление режет и урон, и накладываемые стаки статуса этой стихии (docs/design/02 §4.5).
/// Сама сила ничего не считает — множитель применяет RequiemCombatRules, чтобы порядок был единым.
/// </summary>
public abstract class ResistancePower : RequiemPower
{
    public abstract DamageType Element { get; }
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;
    public override bool AllowNegative => true;

    public static int TierOf(Creature creature, DamageType type) => type switch
    {
        DamageType.Fire => creature.GetPowerAmount<FireResistancePower>(),
        DamageType.Frost => creature.GetPowerAmount<FrostResistancePower>(),
        DamageType.Shock => creature.GetPowerAmount<ShockResistancePower>(),
        DamageType.Poison or DamageType.Blight => creature.GetPowerAmount<PoisonResistancePower>(),
        DamageType.Magic => creature.GetPowerAmount<MagicResistancePower>(),
        _ => 0,
    };
}

public sealed class FireResistancePower : ResistancePower { public override DamageType Element => DamageType.Fire; }
public sealed class FrostResistancePower : ResistancePower { public override DamageType Element => DamageType.Frost; }
public sealed class ShockResistancePower : ResistancePower { public override DamageType Element => DamageType.Shock; }
public sealed class PoisonResistancePower : ResistancePower { public override DamageType Element => DamageType.Poison; }
public sealed class MagicResistancePower : ResistancePower { public override DamageType Element => DamageType.Magic; }
