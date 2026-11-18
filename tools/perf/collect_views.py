#!/usr/bin/env python3
"""Verify bounded ACTUAL C# microbenchmark annotations. Not Godot/FPS/native-event acceptance.

Fetch with gh api repos/<owner>/<repo>/check-runs/<id>/annotations --paginate, then:
python tools/perf/collect_views.py --annotations ... --commit <full SHA> --out .arena/perf/<SHA>
Only complete matching summary + all 28 trials are written. No reconstruction or invented trials.
"""
from __future__ import annotations

import argparse
import base64
import gzip
import hashlib
import io
import json
import math
from pathlib import Path
import re
import statistics
import subprocess

ROOT = Path(__file__).resolve().parents[2]
TITLE, TRIALS_TITLE = "STR_VIEW_BENCH_V1", "STR_VIEW_TRIALS_V1"
MAX_BYTES = 64 * 1024
WORKLOADS = {"idle": 5, "one-change-per-60-frames": 204, "rebind-every-120-frames": 500, "change-every-frame": 12004}


def parse(text):
    def unique(pairs):
        result = {}
        for key, value in pairs:
            if key in result:
                raise ValueError("Duplicate JSON key")
            result[key] = value
        return result
    return json.loads(text, object_pairs_hook=unique, parse_constant=lambda _: (_ for _ in ()).throw(ValueError("Non-finite JSON")))


def require(condition, message):
    if not condition:
        raise ValueError(message)


def source_hash(commit, name):
    data = subprocess.check_output(["git", "show", f"{commit}:{name}"], cwd=ROOT)
    return hashlib.sha256(data).hexdigest()


def source_hashes(commit):
    paths = {"cache": "mod/SlayTheRequiemCode/Core/InvalidatedView.cs", "benchmark": "tools/perf/Views/Program.cs"}
    # Historical single-buffer checkpoints predate this helper; never silently attribute the new code to them.
    helper = "mod/SlayTheRequiemCode/Core/InvalidatedListView.cs"
    if subprocess.check_output(["git", "ls-tree", "--name-only", commit, "--", helper], cwd=ROOT).strip():
        paths["list_cache"] = helper
    return {key: source_hash(commit, path) for key, path in paths.items()}


def verify(annotations, commit, hashes):
    """hashes come from the identified git object, not the annotation's own claims."""
    require(isinstance(commit, str) and re.fullmatch(r"[0-9a-f]{40}", commit), "Full source commit required")
    require(isinstance(annotations, list), "Expected annotation list")
    packets = {}
    for title in (TITLE, TRIALS_TITLE):
        rows = [item for item in annotations if isinstance(item, dict) and item.get("title") == title]
        require(len(rows) == 1, "Missing or duplicate " + title)
        message = rows[0].get("message")
        require(isinstance(message, str) and len(message) <= 3500, "Oversized/invalid notice")
        packets[title] = parse(message)
    summary, packet = packets[TITLE], packets[TRIALS_TITLE]
    for value in (summary, packet):
        require(isinstance(value, dict) and type(value.get("schema")) is int and value["schema"] == 1
                and value.get("source_commit") == commit, "Wrong summary/trials identity")
    require(summary.get("producer") == "actual-csharp-cache-microbenchmark"
            and summary.get("scope") == "not-engine-not-FPS-not-native-event-validation", "Wrong producer/scope")
    require(summary.get("cache_sha256") == hashes["cache"] and summary.get("benchmark_sha256") == hashes["benchmark"]
            and summary.get("list_cache_sha256") == hashes.get("list_cache"), "Source hashes disagree")
    require(summary.get("baseline_source_commit") == "65af8b30bb19ed86fd7e3a1ae45d319df45958d6"
            and summary.get("baseline_is_collection_reimplementation") is True, "Wrong baseline/scope")
    require(summary.get("tiered_compilation_config") is False and summary.get("tiered_env_override") is None,
            "Unexpected tiered compilation setup")
    require(all(isinstance(summary.get(key), str) and summary[key].strip() for key in ("runtime", "os", "architecture"))
            and type(summary.get("logical_processors")) is int and summary["logical_processors"] > 0, "Missing measurement environment")
    require(packet.get("encoding") == "gzip-base64url" and isinstance(packet.get("data"), str)
            and re.fullmatch(r"[A-Za-z0-9_-]+", packet["data"]), "Invalid bounded trials encoding")
    try:
        compressed = base64.b64decode(packet["data"] + "=" * (-len(packet["data"]) % 4), altchars=b"-_", validate=True)
        with gzip.GzipFile(fileobj=io.BytesIO(compressed)) as stream:
            raw = stream.read(MAX_BYTES + 1)
    except (ValueError, OSError, EOFError) as error:
        raise ValueError("Corrupt compressed trials") from error
    require(len(raw) <= MAX_BYTES, "Trials exceed decompression limit")
    digest = hashlib.sha256(raw).hexdigest()
    require(digest == packet.get("sha256") == summary.get("trials_sha256"), "Trials hash mismatch")
    trials = parse(raw)
    require(isinstance(trials, list) and len(trials) == 28, "Exactly 4 x 7 trials required")
    for key, expected in (("enemies", 5), ("powers_per_enemy", 24), ("frames_per_trial", 12000), ("trials", 7)):
        require(type(summary.get(key)) is int and summary[key] == expected, "Wrong measurement configuration")
    cases = summary.get("cases")
    require(isinstance(cases, list) and len(cases) == 4 and all(isinstance(row, dict) for row in cases), "Four case summaries required")
    require(all(isinstance(case.get("workload"), str) for case in cases)
            and {case["workload"] for case in cases} == set(WORKLOADS), "Wrong workload set")
    for case in cases:
        work = case["workload"]
        observed = [row for row in trials if isinstance(row, dict) and row.get("workload") == work]
        require(len(observed) == 7 and all(type(row.get("trial")) is int for row in observed)
                and {row["trial"] for row in observed} == set(range(7)), "Missing/duplicate trial")
        for row in observed:
            require(type(row["trial"]) is int and row.get("first") == ("baseline" if row["trial"] % 2 == 0 else "candidate"), "Trial order changed")
            for side in ("baseline", "candidate"):
                value = row.get(side)
                require(isinstance(value, dict) and set(value) == {"Milliseconds", "AllocatedBytes", "Gen0", "Gen1", "Gen2", "Builds", "Checksum"}, "Malformed sample")
                require(type(value["Milliseconds"]) in (float, int) and math.isfinite(value["Milliseconds"]) and value["Milliseconds"] >= 0, "Invalid duration")
                require(all(type(value[key]) is int and value[key] >= 0 for key in ("AllocatedBytes", "Gen0", "Gen1", "Gen2", "Builds")), "Invalid allocation/GC/count")
                require(type(value["Checksum"]) is int, "Invalid count checksum")
                require(value["Builds"] == (60000 if side == "baseline" else WORKLOADS[work]), "Wrong rebuild count")
            require(row["baseline"]["Checksum"] == row["candidate"]["Checksum"], "Frame-count trajectory mismatch")
        require(case.get("frame_count_checksums_equal") is True and case.get("synthetic_views_equal_each_frame") is True, "Producer correctness precheck did not pass")
        for side in ("baseline", "candidate"):
            expected = case.get(side)
            require(isinstance(expected, dict) and type(expected.get("median_ms")) in (float, int)
                    and math.isfinite(expected["median_ms"]), "Missing/invalid summary side")
            require(all(type(expected.get(key)) is int and expected[key] >= 0 for key in ("allocated_bytes", "builds", "gc_collections")), "Invalid summary counts")
            require(abs(expected["median_ms"] - statistics.median(row[side]["Milliseconds"] for row in observed)) <= .000501, "Duration median mismatch")
            require(expected.get("allocated_bytes") == statistics.median(row[side]["AllocatedBytes"] for row in observed), "Allocation median mismatch")
            require(expected.get("builds") == observed[0][side]["Builds"], "Build summary mismatch")
            require(expected.get("gc_collections") == sum(row[side][key] for row in observed for key in ("Gen0", "Gen1", "Gen2")), "GC summary mismatch")
    receipt = {"schema": 1, "producer": "verified-csharp-microbenchmark-transport", "source_commit": commit,
               "trials_sha256": digest, "trials": len(trials), "scope": summary["scope"],
               "native_runs": 0, "note": "API annotations plus identified source; hash consistency, not signed provenance or native performance."}
    return summary, raw, receipt


def main():
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("--annotations", type=Path, required=True)
    parser.add_argument("--commit", required=True)
    parser.add_argument("--out", type=Path, required=True)
    args = parser.parse_args()
    try:
        if not re.fullmatch(r"[0-9a-f]{40}", args.commit):
            raise ValueError("Use a full lowercase source SHA")
        hashes = source_hashes(args.commit)
        summary, raw, receipt = verify(parse(args.annotations.read_bytes()), args.commit, hashes)
        if args.out.exists():
            raise ValueError("Use a fresh output directory")
        args.out.mkdir(parents=True)
        for name, value in (("summary.json", summary), ("receipt.json", receipt)):
            (args.out / name).write_text(json.dumps(value, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
        (args.out / "trials.json").write_bytes(raw)
        print("Verified 28 actual C# trials; native runs: 0")
        return 0
    except (ValueError, OSError, subprocess.CalledProcessError) as error:
        print("ERROR:", error)
        return 1


if __name__ == "__main__":
    raise SystemExit(main())
