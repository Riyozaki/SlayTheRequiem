"""Fabricated transport fixtures ONLY; this suite does not run C#, Godot or a native performance trial."""
import base64
import copy
import gzip
import hashlib
import importlib.util
import json
from pathlib import Path
import statistics
import unittest

PATH = Path(__file__).resolve().parents[1] / "perf/collect_views.py"
spec = importlib.util.spec_from_file_location("str_view_evidence", PATH)
evidence = importlib.util.module_from_spec(spec)
spec.loader.exec_module(evidence)
COMMIT = "a" * 40
HASHES = dict(cache="c" * 64, benchmark="d" * 64)


def fixture():
    trials, cases = [], []
    for work, builds in evidence.WORKLOADS.items():
        rows = []
        for trial in range(7):
            sample = dict(Milliseconds=1.0 + trial / 10, AllocatedBytes=120, Gen0=0, Gen1=0, Gen2=0, Builds=60000, Checksum=-51)
            row = dict(workload=work, trial=trial, first="baseline" if trial % 2 == 0 else "candidate",
                       baseline=sample, candidate=dict(sample, Milliseconds=.5 + trial / 10, Builds=builds))
            trials.append(row); rows.append(row)
        sides = {side: dict(median_ms=statistics.median(row[side]["Milliseconds"] for row in rows), allocated_bytes=120,
                           builds=rows[0][side]["Builds"], gc_collections=0) for side in ("baseline", "candidate")}
        cases.append(dict(workload=work, **sides, frame_count_checksums_equal=True, synthetic_views_equal_each_frame=True))
    summary = dict(schema=1, producer="actual-csharp-cache-microbenchmark", scope="not-engine-not-FPS-not-native-event-validation",
                   source_commit=COMMIT, cache_sha256=HASHES["cache"], benchmark_sha256=HASHES["benchmark"],
                   baseline_source_commit="65af8b30bb19ed86fd7e3a1ae45d319df45958d6", baseline_is_collection_reimplementation=True,
                   tiered_compilation_config=False, tiered_env_override=None, runtime="synthetic .NET", os="synthetic OS",
                   architecture="X64", logical_processors=4,
                   enemies=5, powers_per_enemy=24, frames_per_trial=12000, trials=7, cases=cases)
    return summary, trials


def packets(summary=None, trials=None):
    default_summary, default_trials = fixture()
    summary = copy.deepcopy(default_summary if summary is None else summary)
    raw = json.dumps(default_trials if trials is None else trials).encode()
    digest = hashlib.sha256(raw).hexdigest()
    summary["trials_sha256"] = digest
    packet = dict(schema=1, source_commit=COMMIT, encoding="gzip-base64url", sha256=digest,
                  data=base64.urlsafe_b64encode(gzip.compress(raw)).decode().rstrip("="))
    return [dict(title=evidence.TITLE, message=json.dumps(summary)), dict(title=evidence.TRIALS_TITLE, message=json.dumps(packet))]


class ViewEvidenceTests(unittest.TestCase):
    def test_complete_bounded_trials_keep_exact_raw_bytes(self):
        values = packets()
        summary, raw, receipt = evidence.verify(list(reversed(values)), COMMIT, HASHES)
        self.assertEqual(28, len(json.loads(raw)))
        self.assertEqual(28, receipt["trials"])
        self.assertEqual(0, receipt["native_runs"])
        self.assertEqual(summary["trials_sha256"], hashlib.sha256(raw).hexdigest())

    def test_missing_duplicate_summary_or_trials_is_not_reconstructed(self):
        values = packets()
        for broken in ([], values[:1], values[1:], values + values[:1], values + values[1:]):
            with self.assertRaises(ValueError): evidence.verify(broken, COMMIT, HASHES)

    def test_mixed_source_hashes_commit_and_native_producer_fail(self):
        for change in (dict(source_commit="b" * 40), dict(cache_sha256="0" * 64), dict(benchmark_sha256="0" * 64),
                       dict(producer="native-game-opt-in"), dict(scope="engine-performance"), dict(schema=True)):
            summary, _ = fixture()
            summary.update(change)
            with self.assertRaises(ValueError): evidence.verify(packets(summary), COMMIT, HASHES)

    def test_double_buffer_hash_and_environment_cannot_be_omitted_or_relabelled(self):
        summary, _ = fixture()
        hashes = dict(HASHES, list_cache="e" * 64)
        with self.assertRaisesRegex(ValueError, "hashes"):
            evidence.verify(packets(summary), COMMIT, hashes)
        summary["list_cache_sha256"] = hashes["list_cache"]
        self.assertEqual(28, evidence.verify(packets(summary), COMMIT, hashes)[2]["trials"])
        for change in (dict(tiered_compilation_config=True), dict(tiered_env_override="1"), dict(logical_processors=True),
                       dict(os=""), dict(baseline_is_collection_reimplementation=False), dict(baseline_source_commit=COMMIT)):
            broken = dict(summary, **change)
            with self.assertRaises(ValueError): evidence.verify(packets(broken), COMMIT, hashes)

    def test_incomplete_repeated_and_out_of_order_trial_ids_fail(self):
        summary, trials = fixture()
        for rows in (trials[:-1], trials[:-1] + trials[:1], [dict(trials[0], trial=True)] + trials[1:]):
            with self.assertRaises(ValueError): evidence.verify(packets(summary, rows), COMMIT, HASHES)
        # Transport order is irrelevant, but the declared baseline-first/candidate-first alternation is checked.
        trials[0]["first"] = "candidate"
        with self.assertRaises(ValueError): evidence.verify(packets(summary, trials), COMMIT, HASHES)

    def test_corrupt_encoding_or_sha_fails(self):
        for key, value in (("data", "#"), ("sha256", "0" * 64), ("encoding", "raw")):
            values = packets()
            packet = json.loads(values[1]["message"]); packet[key] = value
            values[1]["message"] = json.dumps(packet)
            with self.assertRaises(ValueError): evidence.verify(values, COMMIT, HASHES)

    def test_decompression_is_bounded_before_parsing(self):
        values = packets()
        raw = b"x" * (evidence.MAX_BYTES + 1)
        packet = json.loads(values[1]["message"])
        packet.update(data=base64.urlsafe_b64encode(gzip.compress(raw)).decode().rstrip("="), sha256=hashlib.sha256(raw).hexdigest())
        values[1]["message"] = json.dumps(packet)
        with self.assertRaisesRegex(ValueError, "decompression"):
            evidence.verify(values, COMMIT, HASHES)

    def test_summary_medians_counts_and_correctness_are_recomputed_or_required(self):
        for field, value in (("median_ms", 100), ("allocated_bytes", 121), ("gc_collections", 1), ("builds", 1),
                             ("median_ms", "fast"), ("allocated_bytes", True)):
            summary, _ = fixture()
            summary["cases"][0]["candidate"][field] = value
            with self.assertRaises(ValueError): evidence.verify(packets(summary), COMMIT, HASHES)
        summary, _ = fixture()
        summary["cases"][0]["synthetic_views_equal_each_frame"] = False
        with self.assertRaises(ValueError): evidence.verify(packets(summary), COMMIT, HASHES)

    def test_negative_allocations_gc_and_trajectory_mismatch_are_not_valid_trials(self):
        for key, value in (("AllocatedBytes", -1), ("Gen0", True), ("Builds", 1), ("Checksum", 99), ("Milliseconds", -1)):
            summary, trials = fixture()
            trials[0]["candidate"][key] = value
            with self.assertRaises(ValueError): evidence.verify(packets(summary, trials), COMMIT, HASHES)

    def test_duplicate_keys_and_nan_json_fail(self):
        for raw in ('{"schema":1,"schema":2}', '{"median_ms":NaN}'):
            with self.assertRaises(ValueError): evidence.parse(raw)


if __name__ == "__main__":
    unittest.main()
