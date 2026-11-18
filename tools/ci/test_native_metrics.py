"""Fabricated schema-2 records only. No test here executes a client or supplies native performance evidence."""
import copy
import importlib.util
import json
from pathlib import Path
import unittest

PATH = Path(__file__).resolve().parents[1] / "perf/native_metrics.py"
spec = importlib.util.spec_from_file_location("str_native_metrics_contracts", PATH)
metrics = importlib.util.module_from_spec(spec)
spec.loader.exec_module(metrics)


def fixture():
    memory = dict(AllocatedBytes=2000, ManagedBytes=1000, Gen0=1, Gen1=0, Gen2=0, Nodes=120, OrphanNodes=0, EngineStaticBytes=5000)
    passport = dict(schema=1, game="0.107.1", run_id="1" * 32, seed="SYNTHETIC-NOT-A-RUN", act=0, floor=1,
                    degraded=False, failed_patches=[], network=dict(role="Singleplayer", local_net_id=0), language="eng",
                    effective_settings="v=1;sys=1;hp=110;run=" + "1" * 32 + ";p=0:1,0,0,1",
                    display=dict(window="(1280, 720)", viewport="(1280, 720)", canvas_scale="(1, 1)", content_scale_factor=1.0,
                                 vsync="Enabled", screen_refresh_hz=60, max_fps=60),
                    presentation=dict(reduced_motion=False, combat_vfx=True, sounds=True, compact_enemy_statuses=True,
                                      paper_doll=True, metrics=False, balance_trace=False),
                    players=[dict(id=0, hero="IRONCLAD", requiem_level=1, hero_record_level=1, origin="SYNTHETIC", background="SYNTHETIC")],
                    artifacts=dict(git_sha="a" * 40, source_tree_sha256="b" * 64, dll_sha256="c" * 64, pck_sha256="d" * 64,
                                   build_passport_sha256="e" * 64, dirty="false",
                                   loaded_mods=[dict(id="SlayTheRequiem", version="synthetic", dll_sha256="c" * 64),
                                                dict(id="BaseLib", version="3.4.8", dll_sha256="f" * 64)],
                                   build=dict(contracts_match_dll=True, source_matches_dll=True, files_match_passport=True)))
    return dict(schema=2, producer="native-game-opt-in", enabled=False, window=1024, unit="milliseconds", elapsed_seconds=10,
                total_sampled_frames=600, frame=dict(Samples=600, MedianMs=16, P95Ms=17, MaxMs=18),
                reward_refresh=dict(Samples=1, MedianMs=2, P95Ms=2, MaxMs=2),
                trait_rebuild=dict(Samples=5, MedianMs=.01, P95Ms=.02, MaxMs=.02), trait_rebuilds=5, trait_redraw_requests=5,
                memory_before=memory, memory_after=dict(memory, AllocatedBytes=2400), allocated_bytes_delta=400,
                gc_delta=dict(gen0=0, gen1=0, gen2=0),
                machine=dict(os="SYNTHETIC", runtime="SYNTHETIC .NET", architecture="X64", logical_processors=4, cpu="SYNTHETIC CPU", gpu="SYNTHETIC GPU"),
                passport=passport)


class NativeMetricContracts(unittest.TestCase):
    def test_frozen_consistent_record_can_be_compared_without_becoming_gameplay_acceptance(self):
        baseline = fixture(); candidate = fixture()
        candidate["frame"]["MedianMs"] = 15
        report = metrics.compare(baseline, candidate, "Fabricated unit-test setup, NOT executed gameplay")
        self.assertTrue(report["same_recorded_configuration"])
        self.assertEqual(-1, report["measurements"]["frame.MedianMs"]["delta"])
        self.assertNotIn("PASS", report)
        self.assertNotIn("complete", report)

    def test_different_real_run_and_account_ids_are_not_different_graphics_or_gameplay_settings(self):
        candidate = fixture(); baseline = fixture()
        baseline["passport"]["run_id"] = "2" * 32
        baseline["passport"]["effective_settings"] = baseline["passport"]["effective_settings"].replace("1" * 32, "2" * 32).replace("p=0:", "p=99:")
        baseline["passport"]["players"][0]["id"] = 99
        baseline["passport"]["network"]["local_net_id"] = 99
        self.assertTrue(metrics.compare(baseline, candidate, "SYNTHETIC")["same_recorded_configuration"])

    def test_machine_display_language_flags_seed_rules_and_player_setup_mismatches_fail(self):
        for change in ("cpu", "scale", "vsync", "cap", "language", "seed", "rules", "origin", "baselib", "motion"):
            with self.subTest(change=change):
                baseline, candidate = fixture(), fixture()
                if change == "cpu": candidate["machine"]["cpu"] = "OTHER"
                if change == "scale": candidate["passport"]["display"]["content_scale_factor"] = 1.25
                if change == "vsync": candidate["passport"]["display"]["vsync"] = "Disabled"
                if change == "cap": candidate["passport"]["display"]["max_fps"] = 0
                if change == "language": candidate["passport"]["language"] = "rus"
                if change == "seed": candidate["passport"]["seed"] = "OTHER"
                if change == "rules": candidate["passport"]["effective_settings"] = candidate["passport"]["effective_settings"].replace("hp=110", "hp=125")
                if change == "origin": candidate["passport"]["players"][0]["origin"] = "OTHER"
                if change == "baselib": candidate["passport"]["artifacts"]["loaded_mods"][1]["dll_sha256"] = "0" * 64
                if change == "motion": candidate["passport"]["presentation"]["reduced_motion"] = True
                with self.assertRaisesRegex(ValueError, "same-configuration"):
                    metrics.compare(baseline, candidate, "SYNTHETIC")

    def test_active_empty_microbenchmark_wrong_window_or_bool_schema_are_not_native_samples(self):
        for field, value in (("schema", True), ("producer", "actual-csharp-cache-microbenchmark"), ("enabled", True),
                             ("total_sampled_frames", 0), ("elapsed_seconds", 0), ("window", 256), ("unit", "FPS")):
            data = fixture(); data[field] = value
            with self.assertRaises(ValueError): metrics.validate(data)

    def test_inconsistent_or_nonfinite_timings_and_rolling_counts_fail(self):
        for change in ("order", "nan", "bool", "count", "trait-count"):
            data = fixture()
            if change == "order": data["frame"]["MedianMs"] = 99
            if change == "nan": data["frame"]["P95Ms"] = float("nan")
            if change == "bool": data["frame"]["Samples"] = True
            if change == "count": data["frame"]["Samples"] = 1024
            if change == "trait-count": data["trait_rebuilds"] = 0
            with self.assertRaises(ValueError): metrics.validate(data)

    def test_memory_allocation_and_gc_deltas_are_recomputed_not_assumed(self):
        for change in ("alloc", "gc", "negative", "bool", "missing"):
            data = fixture()
            if change == "alloc": data["allocated_bytes_delta"] = 100
            if change == "gc": data["gc_delta"]["gen0"] = 1
            if change == "negative": data["memory_after"]["Nodes"] = -1
            if change == "bool": data["memory_after"]["AllocatedBytes"] = True
            if change == "missing": del data["memory_before"]
            with self.assertRaises(ValueError): metrics.validate(data)

    def test_dirty_degraded_unidentified_and_unfrozen_passports_fail(self):
        for change in ("dirty", "degraded", "hash", "proof", "trace", "metrics", "display", "run_id"):
            data = fixture()
            if change == "dirty": data["passport"]["artifacts"]["dirty"] = "true"
            if change == "degraded": data["passport"]["degraded"] = True
            if change == "hash": data["passport"]["artifacts"]["dll_sha256"] = "unknown"
            if change == "proof": data["passport"]["artifacts"]["build"]["files_match_passport"] = False
            if change == "trace": data["passport"]["presentation"]["balance_trace"] = True
            if change == "metrics": data["passport"]["presentation"]["metrics"] = True
            if change == "display": del data["passport"]["display"]["vsync"]
            if change == "run_id": data["passport"]["run_id"] = "other"
            with self.assertRaises(ValueError): metrics.validate(data)

    def test_duplicate_invalid_and_unknown_effective_settings_are_not_silently_discarded(self):
        for code in ("v=1;v=1", "run=" + "1" * 32, "v=1;run=" + "2" * 32, "v=1;run=" + "1" * 32 + ";p=not-an-id:a"):
            with self.assertRaises(ValueError): metrics.rules(code, "1" * 32)
        baseline, candidate = fixture(), fixture()
        candidate["passport"]["effective_settings"] += ";future=one"
        with self.assertRaisesRegex(ValueError, "rules"): metrics.compare(baseline, candidate, "SYNTHETIC")

    def test_nan_duplicate_and_oversized_json_fail_before_use(self):
        for raw in (b'{"x":NaN}', b'{"x":1,"x":2}', b" " * (metrics.MAX_BYTES + 1)):
            with self.assertRaises(ValueError): metrics.parse(raw)
        self.assertEqual(fixture(), metrics.parse(json.dumps(fixture()).encode()))

    def test_regressions_are_reported_not_hidden_by_an_automatic_faster_label(self):
        baseline, candidate = fixture(), fixture()
        candidate["frame"].update(MedianMs=30, P95Ms=32, MaxMs=40)
        candidate["memory_after"]["ManagedBytes"] = 9000
        report = metrics.compare(baseline, candidate, "SYNTHETIC")
        self.assertEqual(14, report["measurements"]["frame.MedianMs"]["delta"])
        self.assertEqual(8000, report["measurements"]["memory_after.ManagedBytes"]["delta"])
        self.assertNotIn("accepted", report)
        with self.assertRaises(ValueError): metrics.compare(baseline, candidate, "")


if __name__ == "__main__":
    unittest.main()
