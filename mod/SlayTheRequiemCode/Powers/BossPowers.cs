using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;
using SlayTheRequiem.SlayTheRequiemCode.Core;
using SlayTheRequiem.SlayTheRequiemCode.Rules;

namespace SlayTheRequiem.SlayTheRequiemCode.Powers;

// Силы боссов актов 2 и 4 (docs/design/11 §6.1, M5 п. 1).

/// <summary>Чистые правила сил боссов (зеркало в tools/sim/test_requiem_rules.py).</summary>
public static class BossPowerRules
{
    /// <summary>Страж Затмения днём: урон по нему −50 %, кроме Света.</summary>
    public const int SunStanceReductionPercent = 50;
    /// <summary>Страж Затмения ночью: Свет по нему ×1.5.</summary>
    public const decimal MoonStanceLightMultiplier = 1.5m;

    /// <summary>Множитель «Клятвы стражи»: −N % урона, пока жива свита.</summary>
    public static decimal OathMultiplier(int percent, bool guardAlive) =>
        guardAlive ? (100 - Math.Clamp(percent, 0, 100)) / 100m : 1m;

    /// <summary>Множитель стойки Стража Затмения.</summary>
    public static decimal StanceMultiplier(bool night, DamageType type) => night
        ? (type == DamageType.Light ? MoonStanceLightMultiplier : 1m)
        : (type == DamageType.Light ? 1m : (100 - SunStanceReductionPercent) / 100m);
}

/// <summary>Клятва стражи N: пока рядом жив хоть один союзник (свита), урон по носителю −N %.</summary>
public sealed class OathGuardPower : RequiemPower
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    private bool GuardAlive => Owner.CombatState?.Enemies.Any(e => e.IsAlive && e != Owner) == true;

    public override decimal ModifyDamageMultiplicative(Creature? target, decimal amount, ValueProp props, Creature? dealer, CardModel? cardSource) =>
        target == Owner && dealer != Owner ? BossPowerRules.OathMultiplier(Amount, GuardAlive) : 1m;
}

/// <summary>Давление N (Котловой змей): копится каждый ход; холод сбивает; на пороге — «Сброс давления».</summary>
public sealed class PressurePower : RequiemPower
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;
}

/// <summary>Солнечная стойка (Страж Затмения): урон по носителю −50 %, кроме Света.</summary>
public sealed class SunStancePower : RequiemPower
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Single;

    public override decimal ModifyDamageMultiplicative(Creature? target, decimal amount, ValueProp props, Creature? dealer, CardModel? cardSource) =>
        target == Owner && dealer != null && dealer != Owner
            ? BossPowerRules.StanceMultiplier(false, DamageTyping.Resolve(props, dealer, cardSource))
            : 1m;
}

/// <summary>Лунная стойка (Страж Затмения): бьёт сильнее (Сила даёт ход), но Свет по нему ×1.5.</summary>
public sealed class MoonStancePower : RequiemPower
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Single;

    public override decimal ModifyDamageMultiplicative(Creature? target, decimal amount, ValueProp props, Creature? dealer, CardModel? cardSource) =>
        target == Owner && dealer != null && dealer != Owner
            ? BossPowerRules.StanceMultiplier(true, DamageTyping.Resolve(props, dealer, cardSource))
            : 1m;
}

/// <summary>
/// Чёрный Странник: единственная временная стойка +3 Лат и +I сопротивлений. Bonuses are queried, not added
/// to permanent counters, so waiting/removal cannot steal another source or leave permanent hidden armor.
/// Removed by the boss AFTER the party's first Attack resolves (all hits of that play use the stance).
/// </summary>
public sealed class WandererGuardPower : RequiemPower
{
    public const int PlatingBonus = BossTimingRules.WandererGuardPlating;
    public const int ResistanceBonus = BossTimingRules.WandererGuardResistance;
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Single;
}
