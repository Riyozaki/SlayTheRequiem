"""Negative fixtures for source wiring only; no native shop, boss or trace execution."""
import importlib.util
from pathlib import Path
import tempfile
import unittest

PATH = Path(__file__).resolve().with_name("check_economy.py")
SPEC = importlib.util.spec_from_file_location("economy_wiring", PATH)
economy = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(economy)
FILES = (
    "mod/SlayTheRequiemCode/Shops/ShopStockPatches.cs",
    "mod/SlayTheRequiemCode/Shops/ShopServices.cs",
    "mod/SlayTheRequiemCode/Shops/ShopActionPolicy.cs",
    "mod/SlayTheRequiemCode/Shops/ShopOperations.cs",
    "mod/SlayTheRequiemCode/Rules/EnemySetup.cs",
    "mod/SlayTheRequiemCode/Debugging/BalanceTrace.cs",
    "mod/SlayTheRequiemCode/Debugging/CardRewardDiagnostics.cs",
    "mod/SlayTheRequiemCode/Progression/HeroRecord.cs",
    "mod/SlayTheRequiemCode/Alchemy/CampOptions.cs",
    "tools/balance/Trace/Scenario.cs",
)


class EconomyWiringTests(unittest.TestCase):
    def changed(self, file, before, after):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            for relative in FILES:
                path = root / relative
                path.parent.mkdir(parents=True, exist_ok=True)
                path.write_text((economy.ROOT / relative).read_text())
            path = root / file
            text = path.read_text()
            self.assertIn(before, text)
            path.write_text(text.replace(before, after))
            return economy.check(root)

    def test_current_bindings_are_present(self):
        self.assertEqual([], economy.check())

    def test_quote_and_executor_must_not_bypass_the_saved_policy(self):
        errors = self.changed(FILES[2], "EconomyRules.SoulStonePower(economy)", "ShopRules.SoulStonePower(0)")
        self.assertTrue(any("bypasses saved route" in e for e in errors), errors)

    def test_hp_must_use_authored_class_and_shared_composition(self):
        errors = self.changed(FILES[4], "EconomyRules.EnemyHpMultiplier", "UnversionedMultiplier")
        self.assertTrue(any("Authored boss budget" in e for e in errors), errors)

    def test_trace_must_not_turn_observation_into_a_gate_recovery_or_rng_draw(self):
        for replacement in ("InventoryGate.For(player).Blocked", "player.PlayerRng.Rewards.NextInt(1)"):
            errors = self.changed(FILES[5], "InventoryGate.ObserveBlocked(player)", replacement)
            self.assertTrue(any("must not consume RNG" in e for e in errors), errors)

    def test_actual_boundary_name_is_not_a_post_reward_claim(self):
        errors = self.changed(FILES[7], '"combat:victory-pre-rewards"', '"combat:all-rewards-collected"')
        self.assertTrue(any("missing actual boundary" in e for e in errors), errors)

    def test_core_workload_cannot_be_relabelled_native(self):
        errors = self.changed(FILES[9], '"core-scenario"', '"native-game"')
        self.assertTrue(any("distinct from native" in e for e in errors), errors)
