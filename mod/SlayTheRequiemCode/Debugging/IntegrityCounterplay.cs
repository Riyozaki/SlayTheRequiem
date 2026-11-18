using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.MonsterMoves.Intents;
using MegaCrit.Sts2.Core.MonsterMoves.MonsterMoveStateMachine;
using MegaCrit.Sts2.Core.ValueProps;
using SlayTheRequiem.SlayTheRequiemCode.Cards;
using SlayTheRequiem.SlayTheRequiemCode.Core;
using SlayTheRequiem.SlayTheRequiemCode.Monsters;
using SlayTheRequiem.SlayTheRequiemCode.Powers;
using SlayTheRequiem.SlayTheRequiemCode.Rules;
using SlayTheRequiem.SlayTheRequiemCode.Transformations;

namespace SlayTheRequiem.SlayTheRequiemCode.Debugging;

/// <summary>Native model adapter fixtures for final legal-game automation, NOT executed by public Core CI.
/// No live combat manager mutation, real fight, generated map or UI acceptance is implied by these fixtures.</summary>
public static partial class IntegrityRegression
{
    private static async Task CombatValveCounter()
    {
        var (owner, combat, smithBody) = CombatFixture<DeepMasterSmith>();
        var smith = (DeepMasterSmith)smithBody.Monster!;
        var valves = Enumerable.Range(0, 2).Select(_ =>
        {
            var body = combat.CreateCreature(ModelDb.Monster<SteamValve>().ToMutable(), CombatSide.Enemy, null);
            combat.AddCreature(body); return body;
        }).ToArray();
        var attack = combat.CreateCard<MegaCrit.Sts2.Core.Models.Cards.StrikeIronclad>(owner);
        var ctx = new ThrowingPlayerChoiceContext();
        Require(smith.CurrentSmeltingDamage == DeepMasterSmith.SmeltingDamage, "Open valves omitted from blast query");
        foreach (var valve in valves)
            for (var hit = 0; hit < 3; hit++)
                await valve.Monster!.AfterDamageReceived(ctx, valve,
                    new DamageResult(valve, ValueProp.Move) { BlockedDamage = 1, WasFullyBlocked = true }, ValueProp.Move, owner.Creature, attack);
        Require(valves.All(v => v.GetPowerAmount<ValveClosedPower>() == 2) && smith.CurrentSmeltingDamage == 0,
            "Blocked Attack did not close valve, multihit stacked, or intent still shows a blast");
        foreach (var valve in valves)
            await valve.GetPower<ValveClosedPower>()!.AfterSideTurnEnd(ctx, CombatSide.Enemy, valves);
        Require(smith.CurrentSmeltingDamage == 0 && valves.All(v => v.GetPowerAmount<ValveClosedPower>() == 1), "Valve reopened after only one enemy action");
        foreach (var valve in valves)
            await valve.GetPower<ValveClosedPower>()!.AfterSideTurnEnd(ctx, CombatSide.Enemy, valves);
        Require(smith.CurrentSmeltingDamage == DeepMasterSmith.SmeltingDamage, "Valve did not reopen after exactly two enemy actions");
    }

    private static async Task CombatCounterEntitlements()
    {
        var (owner, combat, enemy) = CombatFixture<BloodCourtLord>();
        var boss = (BloodCourtLord)enemy.Monster!;
        var grants = new EncounterCardGrants(); var before = owner.Deck.Cards.Count;
        grants.Request<CocoonSunbeam>(boss, 1);
        var card = grants.ReservedCards.Single();
        grants.Request<CocoonSunbeam>(boss, 1);
        grants.Request<CocoonSunbeam>(boss, 2); // another phase while Hand is still full/undelivered
        Require(ReferenceEquals(card, grants.ReservedCards.Single()) && card.Owner == owner && card.Pile == null && combat.ContainsCard(card),
            "Undelivered encounter answer was cloned or lost between phases");
        var foreign = Fixture();
        grants.Request<CocoonSunbeam>(boss, 3, foreign);
        Require(grants.ReservedCards.Count == 1 && owner.Deck.Cards.Count == before && card.CanonicalStarCost == 0
            && Equipment.Loadout.CardWeight(card) == 0 && !new EncounterCardDeckPolicy().ShouldAddToDeck(card),
            "Foreign recipient/permanent deck/mana/burden gate leaked into a combat-only answer");
        var beast = FixturePower<BeastFormPower>(owner.Creature, 2);
        Require(beast.ShouldPlay(card, AutoPlayType.None), "Beast form vetoed the mandatory encounter answer");
        combat.RemoveCreature(enemy); // death/removal may detach the source before its cleanup hook
        await grants.Clear(boss);
        Require(enemy.CombatState == null && grants.ReservedCards.Count == 0 && owner.Deck.Cards.Count == before && !combat.ContainsCard(card),
            "Combat-local pending answer leaked across encounter cleanup");
    }

    private static Task CombatCounterProtectionSources()
    {
        var (owner, _, enemy) = CombatFixture<GatebreakerEnvoy>();
        var guard = FixturePower<SolarStandardGuardPower>(owner.Creature, 1); guard.Applier = enemy;
        Require(guard.ModifyDamageMultiplicative(owner.Creature, 20, ValueProp.Move, enemy, null) == 0.5m,
            "Standard did not protect its recipient against the source Envoy");
        Require(guard.ModifyDamageMultiplicative(owner.Creature, 20, ValueProp.Unpowered, enemy, null) == 1m
            && guard.ModifyDamageMultiplicative(enemy, 20, ValueProp.Move, enemy, null) == 1m,
            "Standard reduced status ticks or a different target");
        FixturePower<ArmorPiercingPower>(enemy, GatebreakerEnvoy.HammerPierce);
        Require(PlatingPower.PierceOf(enemy) == 4, "Enemy Pierce was omitted from the shared armor query");
        var (mage, _, summoner) = CombatFixture<FirstSummoner>();
        var resistance = FixturePower<MagicResistancePower>(mage.Creature, 2);
        var aura = FixturePower<BlackBookAuraPower>(summoner, 1);
        Require(ResistancePower.TierOf(mage.Creature, DamageType.Magic) == 1 && resistance.Amount == 2,
            "Black Book aura was absent or mutated permanent resistance");
        var word = FixturePower<WordOfBindingPower>(mage.Creature, 3);
        Require(ResistancePower.TierOf(mage.Creature, DamageType.Magic) == -2 && resistance.Amount == 2,
            "Word did not lower the queried resistance tiers alongside the live aura");
        aura.RemoveInternal();
        Require(ResistancePower.TierOf(mage.Creature, DamageType.Magic) == -1 && resistance.Amount == 2,
            "Removing only the aura also removed Word or damaged the permanent counter");
        word.RemoveInternal();
        Require(ResistancePower.TierOf(mage.Creature, DamageType.Magic) == 2 && resistance.Amount == 2,
            "Removing the sources failed to restore the read-only resistance view");
        var (caster, spells, blood) = CombatFixture<BloodCourtLord>();
        var mist = FixturePower<MistFormPower>(blood, 1);
        decimal InMist(CardModel card) => mist.ModifyDamageMultiplicative(blood, 10, ValueProp.Move | ValueProp.Unpowered, caster.Creature, card);
        Require(InMist(spells.CreateCard<Spells.Firebolt>(caster)) == 0m
            && InMist(spells.CreateCard<Spells.Fireball>(caster)) == 1m
            && InMist(spells.CreateCard<Spells.Sunbeam>(caster)) == 1m,
            "Native single-target unpowered spell bypassed Mist, or area/Light answer was blocked");
        return Task.CompletedTask;
    }

    private static async Task CombatPressureIntent()
    {
        var (owner, combat, enemy) = CombatFixture<BoilerWyrm>();
        var boss = (BoilerWyrm)enemy.Monster!; var ctx = new ThrowingPlayerChoiceContext();
        boss.SetUpForCombat(); boss.RollMove([owner.Creature]);
        var logCount = boss.MoveStateMachine!.StateLog.Count;
        for (var round = 1; round <= 3; round++)
        {
            combat.RoundNumber = round;
            await boss.AfterSideTurnStart(CombatSide.Player, [owner.Creature], combat);
            await boss.AfterSideTurnStart(CombatSide.Player, [owner.Creature], combat); // repeated/extra same-round setup
            Require(enemy.GetPowerAmount<PressurePower>() == round, "An extra player turn inflated enemy pressure");
        }
        Require(boss.NextMove.Id == "PRESSURE_RELEASE", "Pressure reached three but the old bite remained telegraphed");
        var attack = combat.CreateCard<MegaCrit.Sts2.Core.Models.Cards.StrikeIronclad>(owner);
        using (DamageTyping.As(DamageType.Frost))
        {
            await boss.AfterDamageReceived(ctx, enemy, new DamageResult(enemy, ValueProp.Move) { UnblockedDamage = 1 },
                ValueProp.Move, owner.Creature, attack);
            Require(enemy.GetPowerAmount<PressurePower>() == 2 && boss.NextMove.Id == "SCALDING_BITE",
                "Cold failed to cancel pressure release immediately without consuming the next action");
            await boss.AfterDamageReceived(ctx, enemy, new DamageResult(enemy, ValueProp.Move) { BlockedDamage = 1 },
                ValueProp.Move, owner.Creature, attack);
            Require(enemy.GetPowerAmount<PressurePower>() == 2, "A zero Frost packet vented pressure");
        }
        boss.SetMoveImmediate(new MoveState("STUNNED", _ => Task.CompletedTask, new StunIntent()), forceTransition: true);
        combat.RoundNumber++;
        await boss.AfterSideTurnStart(CombatSide.Player, [owner.Creature], combat);
        Require(boss.NextMove.Id == "STUNNED" && boss.MoveStateMachine.StateLog.Count == logCount,
            "Pressure refresh erased native control or rerolled/logged another action");
    }

    private static async Task CombatWorldCounterWindows()
    {
        var (_, _, enemy) = CombatFixture<WorldEater>();
        var boss = (WorldEater)enemy.Monster!; var ctx = new ThrowingPlayerChoiceContext();
        FixturePower<FlightPower>(enemy, 1);
        await boss.Ground(ctx); await boss.Ground(ctx); // replay cannot stack a second two-turn duration
        Require(!boss.Flying && enemy.GetPowerAmount<GroundedByShoutPower>() == 2, "Shout did not ground or replay stacked the duration");
        await boss.AfterSideTurnEnd(ctx, CombatSide.Enemy, [enemy]);
        Require(!boss.Flying && enemy.GetPowerAmount<GroundedByShoutPower>() == 1, "Grounding lasted less than two enemy turns");
        await boss.AfterSideTurnEnd(ctx, CombatSide.Enemy, [enemy]);
        Require(boss.Flying && !enemy.HasPower<GroundedByShoutPower>(), "Flight did not return after exactly two enemy turns");
        FixturePower<MeteorWarningPower>(enemy, 1);
        Require(boss.MeteorThreat && boss.CurrentMeteorDamage == WorldEater.MeteorDamage, "Warning/intended meteor damage missing");
        await boss.ClearMeteors(ctx);
        Require(!boss.MeteorThreat && boss.CurrentMeteorDamage == 0 && !enemy.HasPower<MeteorWarningPower>(),
            "Clear Skies did not update both the warning and actual/preview damage query");
    }
}
