using System.Runtime.CompilerServices;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.MonsterMoves.Intents;
using SlayTheRequiem.SlayTheRequiemCode.Core;
using SlayTheRequiem.SlayTheRequiemCode.Powers;

namespace SlayTheRequiem.SlayTheRequiemCode.Rules;

/// <summary>
/// One enemy action, one price. Multihit/AoE/several AttackCommands in the same move never pay per target or
/// command. Price is shared with the intent UI and based on declared hits/base damage. The physical damage
/// multiplier is frozen immediately before payment, so spending ◆ cannot make the actual hit weaker than its
/// intent. External Frost/Chill before the action still changes both preview and that snapshot.
/// </summary>
public static class EnemyActionBudget
{
    private sealed class Action(DamageType type)
    {
        internal readonly DamageType Type = type;
        internal bool Paid;
        internal bool Active = true;
        internal StaminaState? Stamina;
        internal int Cost;
        internal decimal? MagicMultiplier;
    }
    private static readonly ConditionalWeakTable<Creature, Action> Actions = new();

    internal static bool IsActive(Creature creature) => Actions.TryGetValue(creature, out var action) && action.Active;

    public static DamageType TypeOf(Creature creature, DamageType declared) =>
        Actions.TryGetValue(creature, out var action) && action.Active ? action.Type : declared;

    public static int CostOf(Creature creature)
    {
        var intents = creature.Monster?.NextMove?.Intents.OfType<AttackIntent>().ToList();
        if (intents is not { Count: > 0 }) return creature.HasPower<EnemyManaPower>() ? EnemyRules.MinSpellMana : 1;
        var hits = intents.Sum(a => Math.Max(1, a.Repeats));
        var total = intents.Sum(a => Math.Max(0m, a.DamageCalc?.Invoke() ?? 0m) * Math.Max(1, a.Repeats));
        return MonsterMoveRules.ActionCost(creature.HasPower<EnemyManaPower>(), hits, total);
    }

    internal static void Begin(MonsterModel monster)
    {
        var declared = monster is IEnemyDamageTypeSource typed ? typed.CurrentAttackType : RequiemData.MonsterEntry(monster.GetType().Name).AttackType;
        Actions.AddOrUpdate(monster.Creature, new Action(declared));
    }

    internal static void End(Creature creature)
    {
        if (Actions.TryGetValue(creature, out var action)) action.Active = false;
    }

    internal static void Pay(Creature creature)
    {
        if (!Config.RunSettings.EnableRequiemSystems || !creature.IsEnemy || creature.Monster == null) return;
        if (!Actions.TryGetValue(creature, out var action) || !action.Active)
        {
            Begin(creature.Monster);
            action = Actions.GetValue(creature, _ => throw new InvalidOperationException("Enemy action missing"));
        }
        if (action.Paid) return;
        action.Paid = true;
        action.Cost = CostOf(creature);
        if (creature.GetPower<EnemyManaPower>() is { } mana)
        {
            action.MagicMultiplier = EnemyRules.InsufficientManaMultiplier(mana.DisplayAmount, action.Cost);
            mana.Set(mana.DisplayAmount - action.Cost);
        }
        else if (creature.GetPower<EnemyStaminaPower>() is { } stamina)
        {
            action.Stamina = StaminaRules.StateFor(stamina.DisplayAmount, stamina.Pool, action.Cost);
            stamina.Set(stamina.DisplayAmount - action.Cost);
        }
    }

    internal static decimal ManaDamageMultiplier(Creature creature, int current) =>
        Actions.TryGetValue(creature, out var action) && action.Active && action.MagicMultiplier is { } paid
            ? paid : EnemyRules.InsufficientManaMultiplier(current, CostOf(creature));

    internal static StaminaState StaminaBeforePayment(Creature creature, int current, int pool) =>
        Actions.TryGetValue(creature, out var action) && action.Active && action.Stamina is { } paid
            ? paid : StaminaRules.StateFor(current, pool, CostOf(creature));
}
