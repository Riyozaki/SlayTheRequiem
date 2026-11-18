#!/usr/bin/env python3
"""Opt-in CI entry point for a trusted, isolated self-hosted runner with a legally installed game.

No game downloads, no automatic installation into the real game, no gameplay, no automatic publication.
Detailed logs/binlog/staged references stay LOCAL to the runner. Only the explicitly whitelisted public JSON
report is eligible for upload. Do not upload the bundle/project, whole output directory, native DLLs or saves.
"""
from __future__ import annotations

import argparse
import contextlib
import importlib.util
import json
import os
from pathlib import Path
import re
import sys
import traceback

ROOT = Path(__file__).resolve().parents[2]
_spec = importlib.util.spec_from_file_location("requiem_mod_build", ROOT / "tools/g0/mod_build.py")
assert _spec and _spec.loader
build_tool = importlib.util.module_from_spec(_spec)
_spec.loader.exec_module(build_tool)


def public_passport(passport: dict) -> dict:
    """Allowlist, not path redaction: arbitrary/private installed-mod IDs and log text must never leak."""
    keys = ("schema", "git_sha", "dirty", "source_tree_sha256", "built_at_utc", "exported_at_utc", "game_version",
            "baselib_version", "dotnet_sdk", "save_schema", "shop_protocol", "artifacts", "dependencies", "godot_executable_sha256")
    public = {key: passport[key] for key in keys if key in passport}
    proof = passport["proof"]
    pack = proof["pack_resource_loader"]
    public["proof"] = {"full_mod_build": proof["full_mod_build"], "core_tests": proof["core_tests"],
                       "core_test_results": proof["core_test_results"], "pack_export": proof["pack_export"],
                       "pack_resource_loader": {key: pack[key] for key in ("checked", "failed", "isolated_project", "game_runtime")},
                       "game_runtime": False, "co_op": False}
    # Executable version strings come from an external program: keep their hash, not an unbounded vendor/user label.
    public["godot_version_sha256"] = __import__("hashlib").sha256(passport.get("godot", "").encode()).hexdigest()
    return public


def warning_counts(log: Path) -> dict[str, int]:
    from collections import Counter
    if not log.is_file():
        return {}
    return dict(sorted(Counter(re.findall(r"\bwarning ([A-Za-z]+\d+)\b", log.read_text(encoding="utf-8", errors="replace"))).items()))


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--output", type=Path, required=True, help="New ignored directory, e.g. .arena/legal-ci/run-1")
    parser.add_argument("--acknowledge-legal-environment", action="store_true")
    args = parser.parse_args()
    if not args.acknowledge_legal_environment:
        parser.error("Explicit legal/trusted-runner acknowledgement is required")
    output = args.output.resolve()
    # Fail before creating directories; this wrapper must not add generated inputs to the source identity.
    if not output.is_relative_to(ROOT / ".arena") or output.exists():
        parser.error("Use a fresh directory inside .arena/")
    output.mkdir(parents=True)
    private = output / "private"
    private.mkdir()
    report: dict = {"state": "failed", "game_runtime": False, "co_op": False, "public_report_schema": 1,
                    **build_tool.source_identity()}
    code = 1
    with (private / "workflow.log").open("w", encoding="utf-8") as log, contextlib.redirect_stdout(log), contextlib.redirect_stderr(log):
        try:
            if not all(os.environ.get(name) for name in ("STS2_GAME_DIR", "MEGADOT")):
                raise build_tool.BuildError("Configure STS2_GAME_DIR and MEGADOT in the runner's private environment")
            env_path = lambda name: Path(os.environ[name]) if os.environ.get(name) else None
            build_args = argparse.Namespace(game_dir=env_path("STS2_GAME_DIR"), game_data=env_path("STS2_GAME_DATA"),
                                            mods_dir=env_path("STS2_MODS_DIR"), godot=env_path("MEGADOT"),
                                            dotnet=os.environ.get("DOTNET_EXE", "dotnet"), output=private / "bundle")
            bundle = build_tool.build(build_args)
            passport = build_tool.verify_bundle(bundle)
            # Exercise the real installer and backup/swap against this actual pair, but NOT a user's game folder.
            smoke_mods = private / "install-smoke/mods"
            target = build_tool.install(bundle, smoke_mods, game_closed=True)
            (target / "obsolete-fixture.txt").write_text("removed by next clean swap", encoding="utf-8")
            build_tool.install(bundle, smoke_mods, game_closed=True)
            if (target / "obsolete-fixture.txt").exists():
                raise build_tool.BuildError("Clean installation left a stale file")
            build_tool.verify_bundle(target)
            report.update(public_passport(passport))
            report.update(state="success", clean_install_smoke="isolated directory, not the installed game",
                          warning_code_occurrences=warning_counts(bundle / "build.log"))
            code = 0
        except Exception as error:
            traceback.print_exc()
            report["failure_type"] = type(error).__name__
            # Exception messages/tracebacks often contain local paths. They remain in workflow.log on the runner.
            report["instruction"] = "Inspect private/workflow.log and bundle stage logs locally on the trusted runner. No usable bundle is declared."
        finally:
            bundle = private / "bundle"
            report["log_sha256"] = {path.name: build_tool.sha(path) for path in sorted(bundle.glob("*.log"))}
            log.flush()
            report["workflow_log_sha256"] = build_tool.sha(private / "workflow.log")
    build_tool.write_json(output / "public-result.json", report)
    print(json.dumps(report, ensure_ascii=False, indent=2))
    if code:
        print("::error::Full legal-game build/export/isolated-pack verification failed. Detailed logs remain private on the runner.")
    return code


if __name__ == "__main__":
    sys.exit(main())
