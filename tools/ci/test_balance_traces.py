"""Synthetic parser fixtures ONLY. No native game executions are performed by this module."""
import importlib.util
import json
from pathlib import Path
import tempfile
import unittest

PATH = Path(__file__).resolve().parents[1] / "balance/compare_traces.py"
SPEC = importlib.util.spec_from_file_location("balance_compare", PATH)
compare = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(compare)


def synthetic_parser_row(**changes):
    # Fabricated parser input, not evidence, never written into checked-in native logs.
    row = dict(schema=1, producer="native-game", phase="combat:victory-pre-rewards", segment="parser-fixture-only", sequence=1,
               run_id="parser-fixture-only", seed="STRB2B0000", hero="Regent", player="fixture", act_index=0, act_count=5,
               route_slot=0, floor=15, economy_revision=1, requiem_level=1, ascension=0, gold=100, hp=20, max_hp=50,
               renown=15, level=4, weight=25, capacity=24, excess=1, pending_cards=0, stored_count=0, operation_blocked=False,
               operation_timeline="fixture-timeline", settings="v=1;sys=1;five=1;short=0;hp=110;economy=1")
    row.update(changes)
    return row


class BalanceTraceTests(unittest.TestCase):
    def with_log(self, lines, run):
        with tempfile.TemporaryDirectory() as directory:
            path = Path(directory) / "synthetic-parser-input.log"
            path.write_text("\n".join(lines))
            return run(path)

    def test_no_native_rows_does_not_become_a_successful_comparison(self):
        with self.assertRaisesRegex(ValueError, "No native"):
            self.with_log(["Scenario ledgers PASS"], lambda p: compare.load_native([p]))

    def test_core_scenario_cannot_be_relabelled_by_the_native_marker(self):
        text = compare.MARKER + json.dumps(dict(schema=1, producer="core-scenario", runs=[]))
        with self.assertRaisesRegex(ValueError, "never native evidence"):
            self.with_log([text], lambda p: compare.load_native([p]))

    def test_duplicate_file_does_not_double_observations(self):
        text = "INFO " + compare.MARKER + json.dumps(synthetic_parser_row())
        rows = self.with_log([text], lambda p: compare.load_native([p, p]))
        self.assertEqual(1, len(rows))

    def test_conflicting_duplicate_and_bad_sequence_fail_closed(self):
        for second in (synthetic_parser_row(gold=999), synthetic_parser_row(sequence=0)):
            lines = [compare.MARKER + json.dumps(r) for r in (synthetic_parser_row(), second)]
            with self.assertRaisesRegex(ValueError, "Conflicting duplicate|Out-of-order"):
                self.with_log(lines, lambda p: compare.load_native([p]))

    def test_contradictory_burden_and_future_policy_are_rejected(self):
        for change in (dict(excess=0), dict(economy_revision=2), dict(gold=True), dict(level=16), dict(phase="invented"), dict(schema=True)):
            with self.assertRaises(ValueError):
                compare.validate_native(synthetic_parser_row(**change))

    def test_victory_pre_rewards_is_not_subtracted_from_synthetic_end_gold(self):
        native = [synthetic_parser_row()]
        scenario = dict(runs=[dict(hero="Regent", seed="STRB2B0000", revision=1, short_run=False,
                                   acts=[dict(act_index=0, budget_end=987)])])
        report = compare.render(native, scenario)
        self.assertIn("100", report)
        self.assertIn("987", report)
        self.assertIn("до сбора наград", report)
        self.assertIn("Числовой delta не вычисляется", report)
        self.assertNotIn("887", report)

    def test_short_or_difficulty_rows_do_not_compare_to_full_baseline(self):
        scenario = dict(runs=[dict(hero="Regent", seed="STRB2B0000", revision=1, short_run=False,
                                   acts=[dict(act_index=0, budget_end=987)])])
        report = compare.render([synthetic_parser_row(act_count=3, requiem_level=7)], scenario)
        self.assertIn("сравнение выключено", report)
        self.assertNotIn("| 987 |", report)

    def test_missing_changed_or_duplicate_settings_disable_the_seed_match(self):
        baseline = synthetic_parser_row()["settings"]
        for settings in (None, baseline.replace("hp=110", "hp=120"), baseline + ";economy=1", baseline.replace("five=1", "five=0")):
            self.assertFalse(compare.baseline_compatible(synthetic_parser_row(settings=settings)))
        self.assertTrue(compare.baseline_compatible(synthetic_parser_row()))
        self.assertTrue(compare.baseline_compatible(synthetic_parser_row(economy_revision=0, settings=baseline.replace(";economy=1", ""))))

    def test_different_timelines_are_not_stitched_into_a_complete_run(self):
        rows = [synthetic_parser_row(), synthetic_parser_row(sequence=2, operation_timeline="after-rollback")]
        report = compare.render(rows)
        self.assertEqual(2, report.count("## Regent"))
        self.assertIn("fixture-timeline", report)
        self.assertIn("after-rollback", report)

    def test_old_burden_proxy_is_not_a_core_trace(self):
        with tempfile.TemporaryDirectory() as directory:
            path = Path(directory) / "proxy.json"
            path.write_text(json.dumps(dict(schema=1, producer="burden-proxy", runs=[{}])))
            with self.assertRaisesRegex(ValueError, "Core-scenario"):
                compare.load_scenario(path)
