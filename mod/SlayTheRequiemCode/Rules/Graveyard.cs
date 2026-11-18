using System.Runtime.CompilerServices;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Creatures;
using SlayTheRequiem.SlayTheRequiemCode.Core;

namespace SlayTheRequiem.SlayTheRequiemCode.Rules;

/// <summary>
/// Последний павший враг боя — для «Поднять мертвеца» (08 §5): его макс. ♥ (боссы не в счёт, их прислужники — да).
/// Пишет <see cref="RequiemCombatRules"/> (AfterDeath); запись живёт, пока жив объект боя.
/// </summary>
public static class Graveyard
{
    private sealed class Grave(int maxHp)
    {
        public int MaxHp { get; } = maxHp;
    }

    private static readonly ConditionalWeakTable<ICombatState, Grave> Last = new();

    public static void Note(ICombatState? state, Creature creature)
    {
        if (state == null || creature.Monster == null || creature.IsPet) return;
        if (!creature.IsEnemy || !EnemyRoleRules.CanRaise(EnemySetup.RoleOf(creature))) return;
        Last.AddOrUpdate(state, new Grave(creature.MaxHp));
    }

    /// <summary>Макс. ♥ последнего павшего врага этого боя; 0 — поднимать некого.</summary>
    public static int LastFallenMaxHp(ICombatState? state) =>
        state != null && Last.TryGetValue(state, out var grave) ? grave.MaxHp : 0;
}
