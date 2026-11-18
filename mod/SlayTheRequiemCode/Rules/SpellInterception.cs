using System.Runtime.CompilerServices;
using HarmonyLib;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;
using SlayTheRequiem.SlayTheRequiemCode.Core;
using SlayTheRequiem.SlayTheRequiemCode.Powers;
using SlayTheRequiem.SlayTheRequiemCode.Spells;

namespace SlayTheRequiem.SlayTheRequiemCode.Rules;

/// <summary>
/// Whole hostile casts are absorbed once per target: all hits and direct secondary effects of that cast are
/// cancelled, not status ticks/retaliation. A card Replay is a fresh cast; a monster move is one cast even when it
/// uses several attack commands. Light bypasses absorption. Pure damage previews predict cancellation but do
/// not create a receipt or spend a charge. Actual damage/power commands perform the interception.
/// </summary>
public static class SpellInterception
{
    private sealed class Cast(ICombatState? combat)
    {
        internal readonly ICombatState? Combat = combat;
        internal readonly SpellDefenseRules.Cast<Creature> Targets = new();
        internal bool Active = true;
        internal DamageType? ObservedElement;
    }
    private sealed record PowerAttempt(Creature Target, Creature? Dealer, CardModel? Card, bool HostileSpell);
    private static readonly ConditionalWeakTable<CardModel, Cast> Cards = new();
    private static readonly ConditionalWeakTable<Creature, Cast> Monsters = new();
    private static readonly ConditionalWeakTable<PowerModel, PowerAttempt> Powers = new();

    public static void Begin(CardModel card)
    {
        if (card is SpellCard) Cards.AddOrUpdate(card, new Cast(card.CombatState));
    }
    public static void End(CardModel card)
    {
        if (Cards.TryGetValue(card, out var cast)) cast.Active = false;
    }
    internal static void Begin(MonsterModel monster) => Monsters.AddOrUpdate(monster.Creature, new Cast(monster.Creature.CombatState));
    internal static void End(Creature dealer) { if (Monsters.TryGetValue(dealer, out var cast)) cast.Active = false; }

    private static Cast? Current(Creature? dealer, CardModel? card, bool create)
    {
        if (card is SpellCard)
        {
            if (Cards.TryGetValue(card, out var c) && c.Active && ReferenceEquals(c.Combat, card.CombatState)) return c;
            if (create) { Begin(card); return Cards.GetValue(card, _ => new Cast(card.CombatState)); }
        }
        else if (dealer?.Monster is { IsPerformingMove: true } && EnemyActionBudget.IsActive(dealer))
        {
            if (Monsters.TryGetValue(dealer, out var c) && c.Active && ReferenceEquals(c.Combat, dealer.CombatState)) return c;
            if (create) return Monsters.GetValue(dealer, _ => new Cast(dealer.CombatState));
        }
        return null;
    }

    private static bool Hostile(Creature target, Creature? dealer) => dealer != null && target.Side != dealer.Side;
    private static AbsorptionPowerBase? Source(Creature target) =>
        target.Powers.OfType<SpellAbsorptionPower>().FirstOrDefault(p => p.Amount > 0)
        ?? (AbsorptionPowerBase?)target.Powers.OfType<MistbloodPower>().FirstOrDefault(p => p.Amount > 0);

    public static bool Predict(Creature target, ValueProp props, Creature? dealer, CardModel? card)
    {
        if (!Config.RunSettings.EnableRequiemSystems || !Hostile(target, dealer) || !MagicDamage.IsSpell(props, dealer, card)) return false;
        var type = DamageTyping.Resolve(props, dealer, card);
        if (type == DamageType.Light) return false;
        return Current(dealer, card, false)?.Targets.IsAbsorbed(target) == true
            || SpellDefenseRules.CanAbsorb(true, type, Source(target)?.Amount ?? 0);
    }

    public static async Task<bool> Intercept(PlayerChoiceContext ctx, Creature target, ValueProp props, Creature? dealer, CardModel? card)
    {
        if (!Config.RunSettings.EnableRequiemSystems || !Hostile(target, dealer) || !MagicDamage.IsSpell(props, dealer, card)) return false;
        var type = DamageTyping.Resolve(props, dealer, card);
        if (type == DamageType.Light) return false;
        var cast = Current(dealer, card, true);
        if (cast != null) cast.ObservedElement = type;
        if (cast?.Targets.IsAbsorbed(target) == true) return true;
        if (Source(target) is not { } source) return false;
        // Receipt before the awaited mutation: all following hits/effects see one committed interception.
        cast?.Targets.Absorb(target);
        await source.Absorb(ctx, dealer);
        return true;
    }

    /// <summary>Direct hostile effects (control, execution, resource burn) use the same cast receipt as damage.</summary>
    public static async Task<bool> InterceptEffect(PlayerChoiceContext ctx, Creature target, Creature? dealer, CardModel? card)
    {
        var cast = Current(dealer, card, false);
        if (cast?.Targets.IsAbsorbed(target) == true) return true;
        // A status-only monster spell has no powered hit from which to discover its element.
        var type = cast?.ObservedElement ?? DamageTyping.Resolve(ValueProp.Move, dealer, card);
        using var element = DamageTyping.As(type);
        return await Intercept(ctx, target, ValueProp.Move, dealer, card);
    }

    internal static async Task BeforePower(PowerModel power, decimal amount, Creature target, Creature? dealer, CardModel? card)
    {
        var cast = Current(dealer, card, false);
        var spell = amount > 0m && power.Type == PowerType.Debuff && Hostile(target, dealer)
            && (card is SpellCard || cast?.ObservedElement is { } e && e is not (DamageType.Physical or DamageType.True)
                || dealer?.Monster is { IsPerformingMove: true } && EnemyRules.IsCaster(DamageTyping.Resolve(ValueProp.Move, dealer, card)));
        Powers.AddOrUpdate(power, new PowerAttempt(target, dealer, card, spell));
        if (spell) await InterceptEffect(new ThrowingPlayerChoiceContext(), target, dealer, card);
    }

    internal static decimal ProtectPower(PowerModel power, Creature target, Creature? giver, decimal amount, ref IEnumerable<AbstractModel> modifiers)
    {
        if (!Powers.TryGetValue(power, out var attempt)) return amount;
        Powers.Remove(power);
        if (!Config.RunSettings.EnableRequiemSystems || !attempt.HostileSpell || attempt.Target != target || attempt.Dealer != giver || amount <= 0m) return amount;
        if (Current(giver, attempt.Card, false)?.Targets.IsAbsorbed(target) == true) return 0m;
        if (target.GetPower<SpellShieldPower>() is not { } shield) return amount;
        var result = shield.ProtectStatus(power, amount, giver);
        if (result != amount) modifiers = modifiers.Append(shield).Distinct().ToList();
        return result;
    }
}

[CriticalPatch]
[HarmonyPatch(typeof(MonsterModel), nameof(MonsterModel.PerformMove))]
public static class MonsterSpellCastPatch
{
    [HarmonyPrefix]
    public static void Prefix(MonsterModel __instance)
    {
        SpellInterception.Begin(__instance);
        EnemyActionBudget.Begin(__instance);
    }

    [HarmonyPostfix]
    public static void Postfix(MonsterModel __instance, ref Task __result) => __result = Finish(__result, __instance.Creature);

    private static async Task Finish(Task original, Creature creature)
    {
        try { await original; }
        finally { EnemyActionBudget.End(creature); SpellInterception.End(creature); }
    }
}

/// <summary>Final status defense, after native resistance/Artifact. Never call modifiers twice to estimate them.</summary>
[CriticalPatch]
[HarmonyPatch(typeof(Hook), nameof(Hook.ModifyPowerAmountReceived))]
public static class SpellStatusDefensePatch
{
    [HarmonyPostfix]
    public static void Postfix(PowerModel canonicalPower, Creature target, Creature? giver,
        ref IEnumerable<AbstractModel> modifiers, ref decimal __result) =>
        __result = SpellInterception.ProtectPower(canonicalPower, target, giver, __result, ref modifiers);
}
