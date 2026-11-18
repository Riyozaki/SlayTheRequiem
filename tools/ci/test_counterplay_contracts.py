"""Source wiring contracts only, not a semantic full-mod build or native encounter execution."""
import csv
from pathlib import Path
import re
import unittest

ROOT = Path(__file__).resolve().parents[2]
CODE = ROOT / "mod/SlayTheRequiemCode"


def source(path):
    return (CODE / path).read_text(encoding="utf-8")


class CounterplaySourceTests(unittest.TestCase):
    def test_five_combat_answers_are_tokens_not_hero_reward_cards_or_spells(self):
        text = source("Cards/EncounterCounters.cs")
        names = set(re.findall(r"public sealed class (\w+) : EncounterCounterCard", text))
        self.assertEqual({"EnvoySidestep", "CarrySolarStandard", "CocoonSunbeam", "TearTheWing", "ClearTheSkies"}, names)
        self.assertEqual(5, text.count("[Pool(typeof(TokenCardPool))]"))
        self.assertIn("CardRarity.Token, TargetType.Self", text)
        self.assertIn("MaxUpgradeLevel => 0", text)
        self.assertIn("CardKeyword.Retain", text)
        self.assertNotRegex(text, r"\b(?:SpellCard|SpellAffinity|Mastery\.ForSpell)\b")
        self.assertNotRegex(text, r"\[Pool\(typeof\((?:Ironclad|Silent|Regent)CardPool")

    def test_every_answer_has_a_real_boss_issuer(self):
        text = source("Monsters/Superbosses.cs")
        for name in ("EnvoySidestep", "CarrySolarStandard", "CocoonSunbeam", "TearTheWing", "ClearTheSkies"):
            self.assertIn(f"RequestCounter<{name}>", text)
            self.assertIn(f"typeof({name})", text)
        self.assertIn("PrepareCountersForTurn(player, NextCounterWindow())", text)
        self.assertIn("AfterCardChangedPilesLate", text)
        self.assertIn("AfterCardPlayedLate", text)

    def test_full_hand_preserves_exact_combat_instance_and_bounds_reentrant_retry(self):
        text = source("Rules/EncounterCardGrants.cs")
        for fragment in ("CardPile.MaxCardsInHand", "card.Owner != player", "card.HasBeenRemovedFromState",
                         "ContainsCaptured(card, combat)", "CanDeliver(player, combat)", "pass < 2", "_flushRequested = true",
                         "GenerationAttempted = true", "CardPileCmd.AddGeneratedCardToCombat(card", "CardPileCmd.Add(card, PileType.Hand)"):
            self.assertIn(fragment, text)
        self.assertNotRegex(text, r"\b(?:AddOrDefer|GrantOnceOrDefer|PrepareDeferredCard)\s*\(")
        self.assertNotIn("PileType.Deck", text)
        self.assertIn("ShouldAddToDeck(CardModel card) => card is not EncounterCounterCard", text)
        self.assertIn("card is Cards.EncounterCounterCard ? 0", source("Equipment/Loadout.cs"))
        self.assertIn("PlayerTurnPhase.Start or PlayerTurnPhase.AutoPrePlay or PlayerTurnPhase.Play", text)
        self.assertIn("combat.RemoveCard(card)", text)

    def test_previews_and_effects_share_valve_meteor_and_flight_rules(self):
        bosses = source("Monsters/Superbosses.cs")
        self.assertIn("new SingleAttackIntent(() => CurrentSmeltingDamage)", bosses)
        self.assertIn("var damage = CurrentSmeltingDamage", bosses)
        self.assertIn("new SingleAttackIntent(() => CurrentMeteorDamage)", bosses)
        self.assertIn("var damage = CurrentMeteorDamage", bosses)
        self.assertIn("rollNewMove: false", bosses)  # refreshing a cancellation must not roll RNG/new AI
        self.assertIn("BossCounterplayRules.FlightMultiplier", source("Powers/ActPowers.cs"))
        self.assertIn("EquipmentCombatRules.IsWeaponAttack(cardSource)", source("Powers/ActPowers.cs"))

    def test_finite_eggs_aura_and_four_turn_meteor_schedule_are_runtime_wired(self):
        bosses = source("Monsters/Superbosses.cs")
        self.assertIn("i < BossCounterplayRules.SummonerEggs", bosses)
        self.assertIn("BossCounterplayRules.CanDevour", bosses)
        self.assertIn("if (egg.IsAlive || !Creature.IsAlive) return", bosses)
        self.assertIn("WorldEaterMoveAt(turn)", bosses)
        self.assertIn("BossCounterplayRules.AuraPenalty", source("Powers/ResistancePowers.cs"))
        self.assertIn("e.IsAlive && e.HasPower<BlackBookAuraPower>()", source("Powers/ResistancePowers.cs"))

    def test_required_skill_payment_and_ui_do_not_turn_it_into_an_overstrained_attack(self):
        cards = source("Cards/EncounterCounters.cs")
        for token in ("IRequiredStaminaCard", "_paidThisSeries = false", "optional: true", "if (_paidThisSeries)"):
            self.assertIn(token, cards)
        self.assertIn("cardPlay.Card is not EnvoySidestep", source("Monsters/Superbosses.cs"))
        self.assertIn("required.RequiredStaminaCost", source("Rules/StaminaCosts.cs"))
        self.assertIn("card is not IRequiredStaminaCard", source("UI/RequiemUi.cs"))
        self.assertIn("model is IRequiredStaminaCard", source("UI/NStaminaCostBadge.cs"))
        self.assertIn("card is not IRequiredStaminaCard", source("UI/CardHoverTipsPatch.cs"))
        self.assertIn("RequiredStamina", source("Cards/RequiemKeywords.cs"))

    def test_reissue_delay_uses_owner_turn_and_is_recorded_before_any_pile_rescue(self):
        grants = source("Rules/EncounterCardGrants.cs")
        for token in ("EncounterShoutCooldowns<ulong>", "PlayerCombatState.TurnNumber", "Cooling(player, card)",
                      "_shoutCooldowns.Ready", "_shoutCooldowns.Clear()", "!combat.ContainsCard(card)",
                      "card.Pile.Type is not (PileType.Draw or PileType.Discard)"):
            self.assertIn(token, grants)
        bosses = source("Monsters/Superbosses.cs")
        before = bosses.split("public override Task BeforeCardPlayed(CardPlay cardPlay)", 1)[1].split("public override", 1)[0]
        self.assertIn("_counters.RecordPlay(this, cardPlay)", before)
        self.assertIn("if (this is not IShoutCard) WithKeyword(CardKeyword.Exhaust)", source("Cards/EncounterCounters.cs"))

    def test_original_pierce_binding_and_mist_mechanics_are_not_silently_replaced(self):
        self.assertIn("Buff<ArmorPiercingPower>(HammerPierce)", source("Monsters/Superbosses.cs"))
        self.assertIn("dealer.GetPowerAmount<ArmorPiercingPower>()", source("Powers/DefensePowers.cs"))
        self.assertIn("BossCounterplayRules.BindingPenalty", source("Powers/ResistancePowers.cs"))
        self.assertIn("BossCounterplayRules.BindingStacksToApply", source("Rules/RequiemCombatRules.cs"))
        powers = source("Powers/ActPowers.cs")
        self.assertIn("BossCounterplayRules.MistMultiplier", powers)
        self.assertIn("cardSource?.TargetType == TargetType.AllEnemies", powers)
        self.assertNotIn("BindingMultiplier", powers)
        self.assertNotIn("HammerBleed", source("Monsters/Superbosses.cs"))
        self.assertIn("BossCounterplayRules.CanStartCocoon", source("Monsters/Superbosses.cs"))

    def test_phase_boundaries_keep_finite_guards_real_sacrifices_and_live_pressure(self):
        self.assertIn("BossCounterplayRules.MissingCocoonGuards", source("Monsters/Superbosses.cs"))
        self.assertIn("!wasRemovalPrevented && !creature.IsAlive", source("Monsters/BarrowsBosses.cs"))
        self.assertIn("if (victim.IsAlive || !Creature.IsAlive) return", source("Monsters/CourtBosses.cs"))
        pressure = source("Monsters/DepthsBosses.cs")
        for token in ("BossCounterplayRules.BoilerMoveAt", "_pressureRound != combatState.RoundNumber", "RefreshPressureIntent()",
                      "SetMoveImmediate(move, forceTransition: true)", "IsPerformingMove", '"SCALDING_BITE" or "IRON_COIL" or "PRESSURE_RELEASE"'):
            self.assertIn(token, pressure)
        self.assertNotIn("RollMove(", pressure)
        grants = source("Rules/EncounterCardGrants.cs")
        self.assertIn("var combat = entry.Combat", grants)
        self.assertIn("pending.Applier == boss.Creature", grants)

    def test_new_bodies_have_explicit_roles_and_declared_preload(self):
        with (ROOT / "mod/SlayTheRequiem/data/monsters_requiem.csv").open() as stream:
            rows = {r["key"]: r for r in csv.DictReader(line for line in stream if not line.startswith("#"))}
        self.assertEqual("prop", rows["SolarStandard"]["role"])
        self.assertEqual("minion", rows["DevouredSoulGhost"]["role"])
        bosses = source("Monsters/Superbosses.cs")
        for name in ("SolarStandard", "DevouredSoulGhost"):
            self.assertIn(f"ModelDb.Monster<{name}>()", bosses)
            self.assertIn(f"await Summon<{name}>()", bosses)

    def test_beast_form_exception_is_narrow_and_native_fixtures_remain_separate(self):
        self.assertIn("card is IBeastCard or Cards.EncounterCounterCard", source("Transformations/WerewolfForm.cs"))
        self.assertIn("Where(c => c is not EncounterCounterCard)", source("Transformations/WerewolfForm.cs"))
        tests = source("Debugging/IntegrityCounterplay.cs")
        suite = source("Debugging/IntegrityRegression.cs")
        for name in ("CombatValveCounter", "CombatCounterEntitlements", "CombatCounterProtectionSources", "CombatWorldCounterWindows", "CombatPressureIntent"):
            self.assertIn(name, tests)
            self.assertIn(", " + name + ")", suite)
        self.assertNotRegex(tests, r"CombatManager\.Instance\.\w+\s*=")


if __name__ == "__main__":
    unittest.main()
