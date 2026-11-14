using MegaCrit.Sts2.Core.Entities.Creatures;
using SlayTheRequiem.SlayTheRequiemCode.Powers;

namespace SlayTheRequiem.SlayTheRequiemCode.Rules;

/// <summary>
/// Подлые удары (docs/design/07, 04): атака «в спину» — по цели без атакующего намерения, либо любая атака
/// из Невидимости / после «Приглушённых шагов». Кинжал даёт Подлой Оружейной атаке ×1.5 и Кровотечение 1;
/// карты Безмолвной с пометкой «Подлый:» получают дополнительный эффект.
/// </summary>
public static class SneakRules
{
    public const decimal DaggerMultiplier = 1.5m;

    public static bool IsSneak(Creature? attacker, Creature? target)
    {
        if (target?.Monster == null || attacker == null) return false;
        if (attacker.HasPower<InvisibilityPower>() || attacker.HasPower<SneakNextPower>()) return true;
        return !target.Monster.IntendsToAttack;
    }
}
