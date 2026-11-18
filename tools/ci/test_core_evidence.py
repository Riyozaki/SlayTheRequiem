"""Fabricated transport fixtures only, never generated C# or native gameplay evidence."""
import base64
import copy
import gzip
import hashlib
import importlib.util
import json
from pathlib import Path
import unittest

PATH = Path(__file__).resolve().parents[1] / "balance" / "collect_core_evidence.py"
spec = importlib.util.spec_from_file_location("core_evidence", PATH)
evidence = importlib.util.module_from_spec(spec)
spec.loader.exec_module(evidence)
COMMIT = "a" * 40


def fixture():
    batch = dict(schema=1, producer="core-scenario", runs=[dict(acts=[{}] * 5) for _ in range(60)])
    text = json.dumps(batch)
    summary = dict(schema=1, producer="core-scenario", source_commit=COMMIT, actual_native_runs=0,
                   scenarios=60, act_rows=300, paired_comparisons=30, paired_seeds_per_hero=10,
                   json_sha256=hashlib.sha256(text.encode()).hexdigest())
    return {"core-scenarios.json": text, "core-scenarios.summary.json": json.dumps(summary),
            "core-scenarios.md": "Fabricated transport fixture, not C# or native evidence.\n"}


def packets(files=None, chunk=120):
    raw = json.dumps(fixture() if files is None else files).encode()
    encoded = base64.urlsafe_b64encode(gzip.compress(raw)).decode()
    total = (len(encoded) + chunk - 1) // chunk
    return [dict(title=f"{evidence.TITLE}{part + 1}/{total}", message=
                 f"schema=1;producer=core-scenario;encoding=gzip-base64url;sha256={hashlib.sha256(raw).hexdigest()};"
                 f"part={part + 1};total={total};data={encoded[part * chunk:(part + 1) * chunk]}")
            for part in range(total)]


class CoreEvidenceTests(unittest.TestCase):
    def test_order_independent_complete_bundle_and_exact_bytes(self):
        values = packets()
        files, receipt = evidence.verify(list(reversed(values)), COMMIT)
        self.assertEqual(fixture(), files)
        self.assertEqual(0, receipt["actual_native_runs"])
        self.assertEqual(len(values), receipt["annotation_parts"])

    def test_missing_or_duplicate_parts_never_reconstructed(self):
        values = packets()
        for broken in (values[:-1], values + values[:1], []):
            with self.assertRaisesRegex(ValueError, "Incomplete|Duplicate"):
                evidence.verify(broken, COMMIT)

    def test_observed_twelve_part_attempt_exceeds_notice_budget(self):
        # CI only retained parts 1..9 of the old 12-part attempt; it is NOT complete evidence.
        values = packets()
        values[0]["title"] = evidence.TITLE + "1/12"
        values[0]["message"] = values[0]["message"].replace(f"total={len(values)};", "total=12;")
        with self.assertRaisesRegex(ValueError, "budget"):
            evidence.verify(values, COMMIT)

    def test_corruption_or_wrong_bundle_hash_fails(self):
        for broken in ("#", "a"):
            values = packets()
            prefix, data = values[-1]["message"].rsplit("data=", 1)
            values[-1]["message"] = prefix + "data=" + broken + data[1:]
            with self.assertRaises(ValueError):
                evidence.verify(values, COMMIT)

    def test_wrong_commit_native_producer_and_json_hash_fail(self):
        for change in (dict(source_commit="b" * 40), dict(producer="native-game"),
                       dict(actual_native_runs=1), dict(json_sha256="0" * 64), dict(scenarios=True)):
            files = fixture()
            summary = json.loads(files["core-scenarios.summary.json"])
            summary.update(change)
            files["core-scenarios.summary.json"] = json.dumps(summary)
            with self.assertRaises(ValueError):
                evidence.verify(packets(files), COMMIT)

    def test_unknown_filename_rejected_not_written(self):
        files = fixture()
        files["../outside.json"] = files.pop("core-scenarios.md")
        with self.assertRaisesRegex(ValueError, "file set"):
            evidence.verify(packets(files), COMMIT)

    def test_mixed_packet_identity_fails(self):
        values = packets()
        values[-1]["message"] = values[-1]["message"].replace("sha256=", "sha256=0", 1)
        with self.assertRaises(ValueError):
            evidence.verify(values, COMMIT)

    def test_decompression_bounded_before_json_parse(self):
        files = fixture()
        files["core-scenarios.md"] = "x" * (evidence.MAX_BYTES + 1)
        with self.assertRaisesRegex(ValueError, "Decompressed"):
            evidence.verify(packets(files, chunk=2800), COMMIT)

    def test_duplicate_json_keys_rejected(self):
        with self.assertRaisesRegex(ValueError, "Duplicate JSON"):
            evidence.load_json('{"runs": [], "runs": [1]}')

    def test_producer_limits_stay_inside_notice_count_and_size(self):
        source = (PATH.parent / "Trace" / "Program.cs").read_text()
        self.assertIn(f"chunkChars = {evidence.CHUNK_CHARS}, maxParts = {evidence.MAX_PARTS}", source)
        self.assertLessEqual(evidence.MAX_PARTS + 1, 10)
        self.assertLess(evidence.CHUNK_CHARS + 160, 3796)


if __name__ == "__main__":
    unittest.main()
