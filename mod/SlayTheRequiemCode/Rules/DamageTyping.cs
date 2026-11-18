using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;
using SlayTheRequiem.SlayTheRequiemCode.Core;

namespace SlayTheRequiem.SlayTheRequiemCode.Rules;

/// <summary>
/// Определяет тип урона удара. Игра знает только «атака/не атака», поэтому тип выводится:
/// 1) явный контекст (статусы и спец-эффекты оборачивают свой урон в <see cref="As"/>);
/// 2) карта-источник (карты мода — IRequiemCard, ванильные — CSV-разметка);
/// 3) враг-атакующий — тип из таблицы врагов;
/// 4) иначе физический.
/// </summary>
public static class DamageTyping
{
    private static readonly AsyncLocal<DamageType?> Ambient = new();

    /// <summary>Пометить весь урон внутри блока как <paramref name="type"/> (переживает await).</summary>
    public static IDisposable As(DamageType type)
    {
        var previous = Ambient.Value;
        Ambient.Value = type;
        return new Restore(previous);
    }

    public static DamageType Resolve(ValueProp props, Creature? dealer, CardModel? cardSource)
    {
        if (Ambient.Value is { } ambient) return ambient;
        if (cardSource != null) return StaminaCosts.DamageTypeFor(cardSource);
        if (dealer?.Monster != null && props.IsCardOrMonsterMove())
        {
            var declared = dealer.Monster is IEnemyDamageTypeSource typed ? typed.CurrentAttackType
                : RequiemData.MonsterEntry(dealer.Monster.GetType().Name).AttackType;
            return EnemyActionBudget.TypeOf(dealer, declared);
        }
        return DamageType.Physical;
    }

    private sealed class Restore(DamageType? previous) : IDisposable
    {
        public void Dispose() => Ambient.Value = previous;
    }
}
