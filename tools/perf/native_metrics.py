#!/usr/bin/env python3
"""Validate/compare FROZEN str_metrics schema-2 captures from actual clients. Never generates measurements.

This checks recorded structure/configuration, not the truth of a file, workload equivalence or native acceptance.
Use on private evidence: passports contain run/player IDs. A comparison is NOT an automatic performance PASS.
"""
from __future__ import annotations

import argparse
import hashlib
import json
import math
from pathlib import Path
import re

MAX_BYTES = 2 * 1024 * 1024
MEMORY_INTS = ("AllocatedBytes", "ManagedBytes", "Gen0", "Gen1", "Gen2")
MEMORY_NUMBERS = ("Nodes", "OrphanNodes", "EngineStaticBytes")
TIMINGS = ("frame", "reward_refresh", "trait_rebuild")


def require(condition, message):
    if not condition:
        raise ValueError(message)


def number(value, *, positive=False):
    return type(value) in (int, float) and math.isfinite(value) and (value > 0 if positive else value >= 0)


def parse(data):
    require(len(data) <= MAX_BYTES, "Oversized native metrics JSON")

    def unique(pairs):
        result = {}
        for key, value in pairs:
            require(key not in result, "Duplicate native metrics JSON field")
            result[key] = value
        return result

    return json.loads(data, object_pairs_hook=unique,
                      parse_constant=lambda _: (_ for _ in ()).throw(ValueError("Non-finite native JSON")))


def rules(code, run_id):
    require(isinstance(code, str) and 0 < len(code) <= 16384, "Missing effective run settings")
    result = {}
    for part in code.split(";"):
        key, separator, value = part.partition("=")
        require(key and separator and key not in result, "Invalid/duplicate effective setting")
        result[key] = value
    require(result.get("v") == "1" and result.get("run") == run_id, "Effective settings/run identity mismatch")
    result.pop("run")  # different real runs have different nonces, not different gameplay configuration
    if "p" in result:
        choices, ids = [], set()
        for entry in result["p"].split("|"):
            ident, separator, values = entry.partition(":")
            require(separator and ident.isascii() and ident.isdigit() and 0 <= int(ident) <= 2**64 - 1
                    and ident not in ids and re.fullmatch(r"-?\d+,-?\d+,-?\d+,-?\d+", values), "Invalid personal settings")
            ids.add(ident); choices.append(values)
        result["p"] = sorted(choices)  # account IDs are not a rendering/gameplay setting
    return result


def validate(data):
    require(isinstance(data, dict) and type(data.get("schema")) is int and data["schema"] == 2
            and data.get("producer") == "native-game-opt-in" and data.get("enabled") is False,
            "Need a frozen actual native schema-2 capture, not an active sampler or cache microbenchmark")
    require(type(data.get("window")) is int and data["window"] == 1024 and data.get("unit") == "milliseconds", "Wrong native metric window/unit")
    require(type(data.get("total_sampled_frames")) is int and data["total_sampled_frames"] > 0
            and number(data.get("elapsed_seconds"), positive=True), "No captured native frames/time")
    for name in TIMINGS:
        timing = data.get(name)
        require(isinstance(timing, dict) and type(timing.get("Samples")) is int and 0 <= timing["Samples"] <= data["window"]
                and all(number(timing.get(key)) for key in ("MedianMs", "P95Ms", "MaxMs")), "Invalid native timing: " + name)
        require(timing["MedianMs"] <= timing["P95Ms"] <= timing["MaxMs"]
                and (timing["Samples"] > 0 or timing["MaxMs"] == 0), "Inconsistent native timing: " + name)
    require(data["frame"]["Samples"] == min(data["window"], data["total_sampled_frames"]), "Frame count/rolling window mismatch")
    for name in ("trait_rebuilds", "trait_redraw_requests"):
        require(type(data.get(name)) is int and data[name] >= 0, "Invalid native trait counter")
    require(data["trait_rebuilds"] >= data["trait_rebuild"]["Samples"], "Trait counter/window mismatch")
    for side in ("memory_before", "memory_after"):
        memory = data.get(side)
        require(isinstance(memory, dict) and all(type(memory.get(key)) is int and memory[key] >= 0 for key in MEMORY_INTS)
                and all(number(memory.get(key)) for key in MEMORY_NUMBERS), "Invalid native memory sample")
    before, after = data["memory_before"], data["memory_after"]
    require(type(data.get("allocated_bytes_delta")) is int and data["allocated_bytes_delta"] >= 0
            and data["allocated_bytes_delta"] == after["AllocatedBytes"] - before["AllocatedBytes"], "Allocation delta mismatch")
    gc = data.get("gc_delta")
    require(isinstance(gc, dict) and all(type(gc.get(key)) is int and gc[key] >= 0
            and gc[key] == after["Gen" + str(index)] - before["Gen" + str(index)] for index, key in enumerate(("gen0", "gen1", "gen2"))), "GC delta mismatch")
    machine = data.get("machine")
    require(isinstance(machine, dict) and all(isinstance(machine.get(key), str) and machine[key].strip()
            for key in ("os", "runtime", "architecture", "cpu", "gpu")) and type(machine.get("logical_processors")) is int
            and machine["logical_processors"] > 0, "Missing native hardware/runtime")
    passport = data.get("passport")
    require(isinstance(passport, dict) and isinstance(passport.get("run_id"), str)
            and re.fullmatch(r"[0-9a-f]{32}", passport["run_id"]) and isinstance(passport.get("seed"), str)
            and passport["seed"].strip(), "Missing native seed/run identity")
    require(type(passport.get("schema")) is int and passport["schema"] == 1 and passport.get("degraded") is False
            and passport.get("failed_patches") == [], "Missing/degraded native passport")
    artifacts = passport.get("artifacts")
    require(isinstance(artifacts, dict) and isinstance(artifacts.get("git_sha"), str) and re.fullmatch(r"[0-9a-f]{40}", artifacts["git_sha"])
            and isinstance(artifacts.get("source_tree_sha256"), str) and re.fullmatch(r"[0-9a-f]{64}", artifacts["source_tree_sha256"])
            and all(isinstance(artifacts.get(key), str) and re.fullmatch(r"[0-9a-fA-F]{64}", artifacts[key])
                    for key in ("dll_sha256", "pck_sha256", "build_passport_sha256")), "Unidentified native binaries")
    require(artifacts.get("dirty") == "false" and isinstance(artifacts.get("build"), dict)
            and all(artifacts["build"].get(key) is True for key in ("contracts_match_dll", "source_matches_dll", "files_match_passport")),
            "Native binary/build proof is incomplete")
    mods = artifacts.get("loaded_mods")
    require(isinstance(mods, list) and len(mods) == 2 and all(isinstance(mod, dict) and isinstance(mod.get("id"), str)
            and isinstance(mod.get("version"), str) and isinstance(mod.get("dll_sha256"), str)
            and re.fullmatch(r"[0-9a-fA-F]{64}", mod["dll_sha256"]) for mod in mods)
            and {mod["id"] for mod in mods} == {"BaseLib", "SlayTheRequiem"}, "Baseline metrics require BaseLib + mod only, both identified")
    display = passport.get("display")
    require(isinstance(display, dict) and all(isinstance(display.get(key), str) and display[key].strip()
            for key in ("window", "viewport", "canvas_scale", "vsync")) and number(display.get("content_scale_factor"), positive=True)
            and number(display.get("screen_refresh_hz"), positive=True) and type(display.get("max_fps")) is int and display["max_fps"] >= 0,
            "Missing native display/scale/vsync/frame-cap configuration")
    presentation = passport.get("presentation")
    require(isinstance(presentation, dict) and all(type(presentation.get(key)) is bool
            for key in ("reduced_motion", "combat_vfx", "sounds", "compact_enemy_statuses", "paper_doll", "metrics", "balance_trace"))
            and presentation["metrics"] is False and presentation["balance_trace"] is False, "Freeze metrics with str_trace off before comparing")
    players = passport.get("players")
    require(isinstance(players, list) and players and all(isinstance(player, dict) and all(key in player
            for key in ("hero", "requiem_level", "hero_record_level", "origin", "background")) for player in players), "Missing native player setup")
    require(isinstance(passport.get("language"), str) and passport["language"].strip()
            and isinstance(passport.get("network"), dict) and passport["network"].get("role") in ("Singleplayer", "Host", "Client"), "Missing native locale/role")
    rules(passport.get("effective_settings"), passport["run_id"])
    return data


def configuration(data):
    validate(data)
    passport = data["passport"]
    player_keys = ("hero", "requiem_level", "hero_record_level", "origin", "background")
    players = [{key: player[key] for key in player_keys} for player in passport["players"]]
    dependencies = [dict(id=mod["id"], version=mod["version"].lstrip("v"), dll_sha256=mod["dll_sha256"].lower())
                    for mod in passport["artifacts"]["loaded_mods"] if mod["id"] != "SlayTheRequiem"]
    return dict(machine=data["machine"], display=passport["display"], presentation=passport["presentation"], dependencies=dependencies,
                game=passport.get("game"), seed=passport["seed"], language=passport["language"],
                role=passport["network"]["role"], act=passport.get("act"), floor=passport.get("floor"),
                rules=rules(passport["effective_settings"], passport["run_id"]),
                players=sorted(players, key=lambda value: json.dumps(value, sort_keys=True)))


def compare(baseline, candidate, setup):
    require(isinstance(setup, str) and setup.strip(), "Describe the actual identical workload/warm-up, vsync and frame-cap setup")
    before, after = configuration(baseline), configuration(candidate)
    changed = sorted(key for key in before if before[key] != after[key])
    require(not changed, "Not a same-configuration comparison; changed: " + ", ".join(changed))
    measurements = {}
    for name in TIMINGS:
        for key in ("Samples", "MedianMs", "P95Ms", "MaxMs"):
            measurements[name + "." + key] = (baseline[name][key], candidate[name][key])
    for name in ("total_sampled_frames", "elapsed_seconds", "allocated_bytes_delta", "trait_rebuilds", "trait_redraw_requests"):
        measurements[name] = (baseline[name], candidate[name])
    for key in MEMORY_INTS + MEMORY_NUMBERS:
        measurements["memory_after." + key] = (baseline["memory_after"][key], candidate["memory_after"][key])
    for key in ("gen0", "gen1", "gen2"):
        measurements["gc_delta." + key] = (baseline["gc_delta"][key], candidate["gc_delta"][key])
    return dict(schema=1, producer="recorded-native-metrics-comparison", setup=setup, same_recorded_configuration=True,
                measurements={name: dict(baseline=values[0], candidate=values[1], delta=values[1] - values[0]) for name, values in measurements.items()},
                note="Compared existing frozen records, not executed gameplay or an automatic performance PASS. Hardware/settings match; reviewer must verify workload, capture duration, checkpoints, equivalence and regressions. No FPS guarantee.")


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--baseline", required=True, type=Path)
    parser.add_argument("--candidate", required=True, type=Path)
    parser.add_argument("--setup", required=True)
    parser.add_argument("--out", required=True, type=Path, help="Fresh PRIVATE comparison file, not automatically published")
    args = parser.parse_args()
    try:
        raw = [path.read_bytes() for path in (args.baseline, args.candidate)]
        report = compare(*(parse(data) for data in raw), args.setup)
        report["captures_sha256"] = dict(zip(("baseline", "candidate"), (hashlib.sha256(data).hexdigest() for data in raw)))
        args.out.parent.mkdir(parents=True, exist_ok=True)
        with args.out.open("x", encoding="utf-8") as stream:
            json.dump(report, stream, ensure_ascii=False, indent=2, allow_nan=False)
            stream.write("\n")
        print("Compared frozen records; operator review still required. Not an automatic performance PASS.")
        return 0
    except (OSError, ValueError, TypeError) as error:
        print("BLOCKED:", error)
        return 1


if __name__ == "__main__":
    raise SystemExit(main())
