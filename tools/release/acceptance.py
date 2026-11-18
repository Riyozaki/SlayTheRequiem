#!/usr/bin/env python3
"""Bind FINAL human/native acceptance to one immutable candidate archive.

This validates recorded identities, completeness and evidence hashes, NOT the truth of a human assertion.
It cannot play the game, certify balance/rights or turn CI/fixtures into native acceptance. No publication.
Evidence stays private by default; seal output is a small allowlisted summary, never logs/saves/Steam IDs.
"""
from __future__ import annotations

import argparse
from collections import Counter
import copy
import datetime as dt
import json
from pathlib import Path
import re
import sys

import catalogue
import package_release as package

sys.path.insert(0, str(package.ROOT / "tools/perf"))
import native_metrics

build = package.build
STATUSES = {"NOT_RUN", "PASS", "FAIL", "BLOCKED"}
KINDS = {"review", "native_log", "screenshot", "metrics", "baseline_metrics", "passport"}


def require(condition, message):
    package.require(condition, message)


def binding(result):
    passport = result["passport"]
    return {**{key: passport[key] for key in ("git_sha", "source_tree_sha256", "artifacts", "game_version", "baselib_version", "dependencies")},
            "archive_sha256": result["archive_sha256"], "passport_sha256": result["passport_sha256"]}


def create_matrix(archive):
    result = package.verify(archive)
    require(build.source_identity()["source_tree_sha256"] == result["passport"]["source_tree_sha256"], "Source changed: rebuild/repackage before freezing")
    cases = catalogue.requirements()
    require(result["plan"] == catalogue.plan(), "Archive has stale requirements/docs: rebuild/repackage the current frozen source")
    return dict(schema=1, producer="requiem-final-acceptance", candidate=binding(result), requirements_sha256=catalogue.digest(cases),
                note="Prepared records are NOT_RUN. New source/pair/archive/requirements invalidate old PASS. Do not copy PASS to a new candidate.",
                cases=[dict(id=case["id"], status="NOT_RUN", attempt=None, history=[]) for case in cases])


def evidence_file(root, name):
    require(isinstance(name, str), "Evidence path must be relative")
    package.safe_name(name)
    path = root / name
    require(path.resolve().is_relative_to(root.resolve()) and not any(parent.is_symlink() for parent in (path, *path.parents) if parent != root.parent), "Evidence path/symlink escapes its private matrix directory")
    require(path.is_file() and 0 < path.stat().st_size <= package.MAX_BYTES, "Missing/empty/oversized evidence: " + name)
    return path


def verify_passport(data, candidate, minimum_clients):
    require(isinstance(data, dict) and type(data.get("schema")) is int and data["schema"] == 1 and data.get("degraded") is False
            and data.get("failed_patches") == [], "Native passport is missing/degraded; CI is not a native passport")
    artifacts = data.get("artifacts", {})
    require(isinstance(artifacts, dict), "Missing native artifact identity")
    for key in ("git_sha", "source_tree_sha256"):
        require(artifacts.get(key) == candidate[key], "Native passport is from another source")
    require(artifacts.get("dirty") == "false", "Unidentified/dirty native assembly")
    for suffix in ("dll", "pck"):
        value = artifacts.get(suffix + "_sha256")
        require(isinstance(value, str) and value.lower() == candidate["artifacts"][build.MOD_ID + "." + suffix]["sha256"], "Native DLL/PCK is not the candidate")
    value = artifacts.get("build_passport_sha256")
    require(isinstance(value, str) and value.lower() == candidate["passport_sha256"], "Installed passport is not from the candidate archive")
    proof = artifacts.get("build", {})
    require(isinstance(proof, dict) and all(proof.get(key) is True for key in ("contracts_match_dll", "source_matches_dll", "files_match_passport")), "Native build/file contracts disagree")
    mods = artifacts.get("loaded_mods")
    require(isinstance(mods, list) and len(mods) == 2 and all(isinstance(mod, dict) and isinstance(mod.get("id"), str) for mod in mods)
            and {mod["id"] for mod in mods} == {build.MOD_ID, "BaseLib"}, "Required baseline acceptance uses only BaseLib + mod")
    baselib = next(mod for mod in mods if mod["id"] == "BaseLib")
    require(isinstance(baselib.get("version"), str) and baselib["version"].lstrip("v") == candidate["baselib_version"]
            and isinstance(baselib.get("dll_sha256"), str)
            and baselib["dll_sha256"].lower() == candidate["dependencies"]["installed_baselib_sha256"], "Wrong loaded BaseLib version/binary")
    require(isinstance(data.get("game"), str) and data["game"].lstrip("v") == candidate["game_version"], "Wrong native game version")
    network = data.get("network", {})
    require(isinstance(network, dict) and network.get("role") in ("Singleplayer", "Host", "Client")
            and type(network.get("local_net_id")) is int and 0 <= network["local_net_id"] <= 2**64 - 1, "Missing native client role/identity")
    players = data.get("players")
    require(isinstance(players, list) and len(players) >= minimum_clients and all(isinstance(player, dict)
            and type(player.get("id")) is int and 0 <= player["id"] <= 2**64 - 1
            and isinstance(player.get("hero"), str) and player["hero"] for player in players), "Not enough/invalid native players")
    identities = {player["id"] for player in players}
    require(len(identities) == len(players) and network["local_net_id"] in identities, "Native local player is missing/duplicated")
    return network, players


def validate_attempt(attempt, spec, candidate, root):
    require(isinstance(attempt, dict) and attempt.get("status") in STATUSES - {"NOT_RUN"}, "Invalid attempt status")
    require(attempt.get("candidate") == candidate, "Stale attempt binding")
    for key in ("reviewer", "notes", "observed_at_utc"):
        require(isinstance(attempt.get(key), str) and attempt[key].strip(), "Missing observed " + key)
    try:
        observed = dt.datetime.fromisoformat(attempt["observed_at_utc"].replace("Z", "+00:00"))
        require(observed.utcoffset() == dt.timedelta(0), "Use UTC observation time")
    except ValueError as error:
        raise build.BuildError("Invalid observation time") from error
    if attempt["status"] != "PASS":
        return  # FAIL/BLOCKED need an honest reason; they never close a release requirement.
    context = attempt.get("context", {})
    require(isinstance(context, dict) and catalogue.canonical({key: context.get(key) for key in spec["context"]}) == catalogue.canonical(spec["context"]), "Observed case context does not match required setup")
    evidence = attempt.get("evidence")
    require(isinstance(evidence, list) and evidence and all(isinstance(row, dict) for row in evidence), "PASS needs hashed evidence")
    require(all(row.get("kind") in KINDS for row in evidence), "Unknown evidence kind")
    require(set(spec["evidence"]) <= {row["kind"] for row in evidence}, "PASS is missing required evidence kinds")
    passports, metrics, baselines = [], [], []
    for row in evidence:
        path = evidence_file(root, row.get("path"))
        require(type(row.get("bytes")) is int and row["bytes"] == path.stat().st_size and row.get("sha256") == build.sha(path), "Changed/unhashed evidence: " + str(row.get("path")))
        if row["kind"] == "passport":
            data = package.read_json(path.read_bytes())
            verify_passport(data, candidate, spec["minimum_clients"])
            passports.append(data)
        elif row["kind"] in ("metrics", "baseline_metrics"):
            try:
                data = native_metrics.parse(path.read_bytes())
                native_metrics.validate(data)
            except ValueError as error:
                raise build.BuildError(str(error)) from error
            if row["kind"] == "metrics":
                verify_passport(data["passport"], candidate, spec["minimum_clients"])
                metrics.append(data)
            else:
                # Baseline has its OWN identified assembly/passport, but the same game and exact BaseLib binary.
                # Its native log is an operator record, not cryptographically signed proof of the old build.
                original = data["passport"]["artifacts"]
                expected = copy.deepcopy(candidate)
                expected.update(git_sha=original["git_sha"], source_tree_sha256=original["source_tree_sha256"],
                                passport_sha256=original["build_passport_sha256"].lower())
                for suffix in ("dll", "pck"):
                    expected["artifacts"][build.MOD_ID + "." + suffix]["sha256"] = original[suffix + "_sha256"].lower()
                verify_passport(data["passport"], expected, spec["minimum_clients"])
                baselines.append(data)
    if "baseline_metrics" in spec["evidence"]:
        require(len(baselines) == len(metrics) == 1, "This case needs one frozen baseline/result pair; retain extra repetitions in its review/logs")
        require(baselines[0]["passport"]["artifacts"]["source_tree_sha256"] != candidate["source_tree_sha256"], "Before/after needs an identified pre-change baseline, not two copies of the candidate")
        try:
            native_metrics.compare(baselines[0], metrics[0], context.get("performance_setup"))
        except ValueError as error:
            raise build.BuildError(str(error)) from error
    if not spec["minimum_clients"]:
        return
    require(attempt.get("executed_in_game") is True and len(passports) >= spec["minimum_clients"], "A source plan/fixture is not actual native acceptance")
    require(isinstance(context.get("run_id"), str) and re.fullmatch(r"[0-9a-f]{32}", context["run_id"])
            and all(data.get("run_id") == context["run_id"] for data in passports), "Run ID/passport mismatch; record the actual nonce, including fixture runs")
    require(isinstance(context.get("seed"), str) and context["seed"].strip() and all(data.get("seed") == context["seed"] for data in passports), "Seed/passport mismatch")
    require(all(data["passport"]["run_id"] == context["run_id"] and data["passport"]["seed"] == context["seed"] for data in metrics)
            and all(data["passport"]["seed"] == context["seed"] for data in baselines), "Metrics from another run/seed cannot close this case")
    if "hero" in spec["context"]:
        require(any(str(player.get("hero", "")).upper() == context["hero"].upper() for data in passports for player in data["players"]), "Wrong native hero")
    if spec["category"] in ("full-route", "short-route", "first-act", "balance-observation"):
        require(all(data["network"]["role"] == "Singleplayer" and len(data["players"]) == 1 for data in passports), "Solo requirement cannot be replaced by co-op")
    if spec["minimum_clients"] == 2:
        identities = {data["network"]["local_net_id"] for data in passports}
        require(len(identities) >= 2 and {data["network"]["role"] for data in passports} >= {"Host", "Client"}, "Two distinct native processes/roles are required, not two copies of the host log")
        parties = {tuple(sorted((player["id"], player["hero"]) for player in data["players"])) for data in passports}
        require(len(parties) == 1, "Host/client passports disagree about the party")


def validate_matrix(matrix, archive, root, *, check_source=True):
    require(isinstance(matrix, dict) and type(matrix.get("schema")) is int and matrix["schema"] == 1
            and matrix.get("producer") == "requiem-final-acceptance", "Not a final acceptance record")
    result = package.verify(archive)
    require(matrix.get("candidate") == binding(result), "Different archive/build: create a new NOT_RUN matrix")
    if check_source:
        require(build.source_identity()["source_tree_sha256"] == result["passport"]["source_tree_sha256"], "Late source fix: previous acceptance is stale")
    specs = catalogue.requirements()
    require(result["plan"] == catalogue.plan(), "Archive has stale requirements/docs: new candidate matrix required")
    require(matrix.get("requirements_sha256") == catalogue.digest(specs), "Requirements changed: reopen affected acceptance (new matrix required)")
    cases = matrix.get("cases")
    require(isinstance(cases, list) and len(cases) == len(specs) and all(isinstance(row, dict) for row in cases)
            and {row.get("id") for row in cases} == {spec["id"] for spec in specs}, "Incomplete/duplicate/unknown case set")
    by_id = {spec["id"]: spec for spec in specs}
    run_ids = {}
    for case in cases:
        require(case.get("status") in STATUSES and isinstance(case.get("history"), list), "Invalid case state/history")
        if case["status"] == "NOT_RUN":
            require(case.get("attempt") is None, "NOT_RUN has an unexpected attempt")
            continue
        require(isinstance(case.get("attempt"), dict) and case["attempt"].get("status") == case["status"], "Case/attempt status mismatch")
        spec = by_id[case["id"]]
        validate_attempt(case["attempt"], spec, matrix["candidate"], root)
        if case["status"] != "PASS":
            continue
        if spec["category"] in ("full-route", "short-route", "first-act", "balance-observation", "coop-route"):
            context = case["attempt"]["context"]
            key = (spec["category"], context.get("hero"), context["run_id"])
            require(key not in run_ids, "One run cannot count twice within the same route/observation series")
            run_ids[key] = case["id"]
    counts = dict(sorted(Counter(case["status"] for case in cases).items()))
    return dict(schema=1, producer="release-acceptance-record-check", candidate=matrix["candidate"],
                requirements_sha256=matrix["requirements_sha256"], cases=len(cases), counts=counts,
                complete=counts.get("PASS", 0) == len(specs),
                note="Completeness/identity/hash check of operator observations, not automated gameplay, rights certification or authorization to publish.")


def record(matrix, ident, result, root):
    specs = {case["id"]: case for case in catalogue.requirements()}
    require(ident in specs, "Unknown case ID")
    attempt = copy.deepcopy(result)
    attempt["candidate"] = copy.deepcopy(matrix["candidate"])
    for row in attempt.get("evidence", []):
        path = evidence_file(root, row.get("path"))
        row.update(sha256=build.sha(path), bytes=path.stat().st_size)
    validate_attempt(attempt, specs[ident], matrix["candidate"], root)
    updated = copy.deepcopy(matrix)
    case = next(row for row in updated["cases"] if row["id"] == ident)
    if case["attempt"] is not None:
        case["history"].append(case["attempt"])
    case.update(status=attempt["status"], attempt=attempt)
    return updated


def write_new(path, value):
    require(not path.exists(), "Use a fresh output file")
    path.parent.mkdir(parents=True, exist_ok=True)
    with path.open("xb") as stream:
        stream.write(package.encoded(value))


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    sub = parser.add_subparsers(dest="command", required=True)
    plan = sub.add_parser("plan")
    plan.add_argument("--out", required=True, type=Path)
    init = sub.add_parser("init")
    init.add_argument("--archive", required=True, type=Path)
    init.add_argument("--out", required=True, type=Path)
    for command in ("record", "check", "seal"):
        action = sub.add_parser(command)
        action.add_argument("--archive", required=True, type=Path)
        action.add_argument("--matrix", required=True, type=Path)
        if command == "record":
            action.add_argument("--case", required=True)
            action.add_argument("--result", required=True, type=Path, help="Observed result JSON; evidence paths relative to the matrix directory")
        if command == "seal":
            action.add_argument("--out", required=True, type=Path, help="New PUBLIC summary; no private evidence is copied")
    args = parser.parse_args()
    try:
        if args.command == "plan":
            write_new(args.out, catalogue.plan())
            print("Prepared final cases:", len(catalogue.requirements()), "— all NOT_RUN")
            return 0
        if args.command == "init":
            write_new(args.out, create_matrix(args.archive))
            print("Frozen candidate; all native/manual cases remain NOT_RUN")
            return 0
        matrix = package.read_json(args.matrix.read_bytes())
        report = validate_matrix(matrix, args.archive, args.matrix.parent)
        if args.command == "record":
            updated = record(matrix, args.case, package.read_json(args.result.read_bytes()), args.matrix.parent)
            validate_matrix(updated, args.archive, args.matrix.parent)
            temporary = args.matrix.with_suffix(args.matrix.suffix + ".new")
            write_new(temporary, updated)
            temporary.replace(args.matrix)
            print("Recorded observation; previous attempt retained. This does not publish a release.")
            return 0
        print(json.dumps(report, ensure_ascii=False, indent=2))
        if args.command == "seal":
            require(report["complete"], "Release BLOCKED: every required case must have current PASS evidence")
            write_new(args.out, report)
        return 0 if report["complete"] else 1
    except (build.BuildError, OSError, ValueError, KeyError, TypeError) as error:
        print("BLOCKED:", error, file=sys.stderr)
        return 1


if __name__ == "__main__":
    raise SystemExit(main())
