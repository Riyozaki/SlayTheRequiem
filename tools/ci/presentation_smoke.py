#!/usr/bin/env python3
"""Run the real production node/focus helpers in public Godot 4.5.1 mono, WITHOUT licensed game files.

This is NOT the mod build, MegaDot/PCK verification, a gameplay run or visual/performance acceptance.
All build/generated files remain ignored; output is a bounded public helper-smoke report only.
"""
from __future__ import annotations

import argparse
import hashlib
import json
import os
from pathlib import Path
import re
import subprocess
import sys

ROOT = Path(__file__).resolve().parents[2]
PROJECT = ROOT / "tools/ci/Presentation"
SCOPE = "public-Godot-NOT-MegaDot-NOT-full-mod-build-NOT-native-game-NOT-FPS-NOT-art-acceptance"
SOURCES = ("mod/SlayTheRequiemCode/UI/RequiemNodes.cs", "mod/SlayTheRequiemCode/UI/RequiemFocus.cs",
           "tools/ci/Presentation/PresentationSmoke.cs", "tools/ci/Presentation/Presentation.csproj",
           "tools/ci/Presentation/project.godot", "tools/ci/Presentation/PresentationSmoke.tscn",
           "tools/ci/presentation_smoke.py", ".github/workflows/ci.yml")
CASES = ["NODE-01-immediate-named-replacement-100", "NODE-02-never-reuse-queued-child",
         "NODE-03-clear-children-100-and-exit-signals", "NODE-04-idempotent-null-queued-and-freed-retirement",
         "FOCUS-01-wire-only-live-enabled-visible-controls", "FOCUS-02-restore-skips-disabled-hidden-rows",
         "FOCUS-03-ignore-retired-deferred-target", "FOCUS-04-empty-and-ineligible-ring-does-not-steal-focus",
         "FOCUS-05-ignore-children-of-queued-modal", "FOCUS-06-owner-change-cancels-deferred-restore",
         "INPUT-01-gamepad-accept-never-invokes-disabled-offer", "INPUT-02-keyboard-and-gamepad-wrap-enabled-ring"]


def sha(path):
    with path.open("rb") as stream:
        return hashlib.file_digest(stream, "sha256").hexdigest()


def require(ok, message):
    if not ok:
        raise ValueError(message)


def validate_report(info, commit, hashes, engine_hash):
    require(isinstance(info, dict), "Expected smoke report object")
    require(type(info.get("schema")) is int and info["schema"] == 1, "Unknown helper report schema")
    require(info.get("producer") == "actual-godot-presentation-helper-smoke" and info.get("scope") == SCOPE, "Wrong smoke scope")
    require(info.get("source_commit") == commit and info.get("source_sha256") == hashes, "Smoke/source identity mismatch")
    require(info.get("godot_executable_sha256") == engine_hash, "Wrong engine hash")
    # Engine.GetVersionInfo()["string"] is NOT the --version CLI format. The managed scene executing
    # proves C# support; the workflow pins the official mono archive and the report binds its executable hash.
    require(info.get("engine") == "4.5.1-stable (official)", "Wrong Engine.GetVersionInfo version")
    require(info.get("passed") == CASES, "Missing, duplicate or unknown helper cases")
    for name, count in (("actual_native_runs", 0), ("same_frame_replacement_cycles", 100), ("child_clear_cycles", 100)):
        require(type(info.get(name)) is int and info[name] == count, "Wrong scope/count: " + name)


def run(command, log, env):
    try:
        result = subprocess.run(command, cwd=ROOT, env=env, stdout=subprocess.PIPE, stderr=subprocess.STDOUT,
                                text=True, encoding="utf-8", errors="replace", timeout=180)
    except subprocess.TimeoutExpired as error:
        partial = error.stdout or b""
        log.write_text(partial.decode("utf-8", errors="replace") if isinstance(partial, bytes) else partial, encoding="utf-8")
        raise ValueError("Presentation helper stage timed out; see " + log.name) from error
    log.write_text(result.stdout, encoding="utf-8")
    print(result.stdout, end="", flush=True)
    if result.returncode or re.search(r"(?im)^\s*(?:SCRIPT ERROR:|ERROR:)", result.stdout):
        # Bounded source-only diagnosis survives APIs that expose annotations but not full job logs.
        lines = [line.replace(str(ROOT), "<repo>") for line in result.stdout.splitlines() if "error" in line.lower()]
        diagnostic = ("; ".join(lines[:4]) or "Stage failed without a compiler/engine error line")[:2200]
        diagnostic = diagnostic.replace("%", "%25").replace("\r", "%0D").replace("\n", "%0A")
        print("::error title=STR_PRESENTATION_STAGE::" + log.name + ": " + diagnostic)
        raise ValueError("Presentation helper stage failed; see " + log.name)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--godot", required=True, type=Path)
    parser.add_argument("--dotnet", default="dotnet")
    parser.add_argument("--out", required=True, type=Path)
    args = parser.parse_args()
    try:
        target = args.out.resolve()
        if target.exists() or not target.is_relative_to(ROOT) or not any(part in {".arena", ".arena-ci"} for part in target.relative_to(ROOT).parts):
            raise ValueError("Use a fresh ignored .arena/ or .arena-ci/ output")
        engine = args.godot.resolve()
        commit = os.environ.get("GITHUB_SHA") or subprocess.check_output(["git", "rev-parse", "HEAD"], cwd=ROOT, text=True).strip()
        hashes = {name: sha(ROOT / name) for name in SOURCES}
        engine_hash = sha(engine)
        target.mkdir(parents=True)
        env = dict(os.environ, STR_PRESENTATION_REPORT=str(target / "summary.json"), STR_ENGINE_SHA256=engine_hash,
                   GITHUB_SHA=commit, GODOT_SILENCE_ROOT_WARNING="1", DOTNET_CLI_TELEMETRY_OPTOUT="1", DOTNET_NOLOGO="1")
        run([args.dotnet, "build", str(PROJECT / "Presentation.csproj"), "-c", "Debug", "--no-incremental"], target / "build.log", env)
        run([str(engine), "--headless", "--path", str(PROJECT), "--editor", "--import"], target / "import.log", env)
        run([str(engine), "--headless", "--path", str(PROJECT)], target / "smoke.log", env)
        info = json.loads((target / "summary.json").read_bytes())
        validate_report(info, commit, hashes, engine_hash)
        if hashes != {name: sha(ROOT / name) for name in SOURCES}:
            raise ValueError("Helper inputs changed during smoke")
        notice = json.dumps(info, ensure_ascii=False, separators=(",", ":"), allow_nan=False)
        if len(notice) > 3500:
            raise ValueError("Helper report exceeds bounded notice")
        print("::notice title=STR_PRESENTATION_SMOKE_V1::" + notice)
        return 0
    except (ValueError, OSError, subprocess.SubprocessError) as error:
        diagnostic = str(error).replace(str(ROOT), "<repo>")[:2200]
        diagnostic = diagnostic.replace("%", "%25").replace("\r", "%0D").replace("\n", "%0A")
        print("::error title=STR_PRESENTATION_FAILURE::" + diagnostic)
        print("ERROR:", error)
        return 1


if __name__ == "__main__":
    sys.exit(main())
