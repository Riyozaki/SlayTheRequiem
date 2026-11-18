#!/usr/bin/env python3
"""Full legal-game build/export/pack verification/clean installation. Stdlib only; never downloads game files.

python tools/g0/mod_build.py probe --game-dir ... --godot ...
python tools/g0/mod_build.py build --game-dir ... --godot ... --output .arena/builds/g1
python tools/g0/mod_build.py install --bundle .arena/builds/g1 --mods-dir ... --game-closed
python tools/g0/mod_build.py verify --bundle .arena/builds/g1

Private logs live in the bundle, not in Git. A build passport is produced ONLY after every stage succeeds.
"""
from __future__ import annotations

import argparse
import datetime as dt
import hashlib
import json
import os
from pathlib import Path
import platform
import shutil
import subprocess
import sys
import uuid

ROOT = Path(__file__).resolve().parents[2]
MOD_ID = "SlayTheRequiem"
GAME_VERSION = "0.107.1"
BASELIB_VERSION = "3.4.8"
GODOT_VERSION = "4.5.1"
SAVE_SCHEMA = 3
SHOP_PROTOCOL = 1
ARTIFACTS = (f"{MOD_ID}.dll", f"{MOD_ID}.pck", f"{MOD_ID}.json")
PASSPORT = f"{MOD_ID}.build.json"


class BuildError(RuntimeError):
    pass


def sha(path: Path) -> str:
    with path.open("rb") as stream:
        return hashlib.file_digest(stream, "sha256").hexdigest()


def utc() -> str:
    return dt.datetime.now(dt.timezone.utc).isoformat()


def write_json(path: Path, data: object) -> None:
    path.write_text(json.dumps(data, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")


def read_object(path: Path) -> dict:
    info = json.loads(path.read_text(encoding="utf-8-sig"))
    if not isinstance(info, dict):
        raise BuildError(f"Expected a JSON object in {path.name}")
    return info


def run(command: list[str], log: Path, *, env: dict | None = None, cwd: Path = ROOT) -> str:
    # No shell interpolation of user-supplied paths; a failing stage is always fatal.
    with log.open("w", encoding="utf-8") as output:
        output.write("Command: " + repr(command) + "\n")
        output.flush()
        with subprocess.Popen(command, cwd=cwd, env=env, stdout=subprocess.PIPE, stderr=subprocess.STDOUT,
                              text=True, encoding="utf-8", errors="replace") as proc:
            lines = []
            for line in proc.stdout:
                output.write(line)
                print(line, end="", flush=True)
                lines.append(line)
            code = proc.wait()
        if code:
            raise BuildError(f"Stage failed ({code}); see {log.name}")
    return "".join(lines)


def output(command: list[str]) -> str:
    result = subprocess.run(command, cwd=ROOT, capture_output=True, text=True, encoding="utf-8", errors="replace")
    if result.returncode:
        raise BuildError(f"Command failed: {command[0]}: {result.stderr.strip()}")
    return result.stdout.strip()


def default_data(game: Path) -> Path:
    suffix = {"Windows": "data_sts2_windows_x86_64", "Linux": "data_sts2_linuxbsd_x86_64",
              "Darwin": "SlayTheSpire2.app/Contents/Resources/data_sts2_macos_x86_64"}.get(platform.system())
    if not suffix:
        raise BuildError("Set --game-data on this OS")
    return game / suffix


def default_mods(game: Path) -> Path:
    return game / ("SlayTheSpire2.app/Contents/MacOS/mods" if platform.system() == "Darwin" else "mods")


def preflight(args: argparse.Namespace) -> dict:
    game = args.game_dir.resolve()
    data = (args.game_data or default_data(game)).resolve()
    mods = (args.mods_dir or default_mods(game)).resolve()
    errors = []
    for name in ("sts2.dll", "0Harmony.dll"):
        if not (data / name).is_file():
            errors.append(f"Missing legal game reference: {name} (set --game-data if necessary)")
    release_paths = [game / "release_info.json", game / "SlayTheSpire2.app/Contents/Resources/release_info.json",
                     game / "SlayTheSpire2.app/Contents/MacOS/release_info.json"]
    release = next((p for p in release_paths if p.is_file()), None)
    version = str(read_object(release).get("version", "unknown")).lstrip("v") if release else "unknown"
    if version != GAME_VERSION:
        errors.append(f"Expected installed game {GAME_VERSION}, found {version}; do not substitute a decompiled/stub DLL")
    base_manifest = mods / "BaseLib/BaseLib.json"
    base_version = str(read_object(base_manifest).get("version", "unknown")).lstrip("v") if base_manifest.is_file() else "unknown"
    if base_version != BASELIB_VERSION or not (mods / "BaseLib/BaseLib.dll").is_file():
        errors.append(f"Install BaseLib {BASELIB_VERSION} (BaseLib.json + BaseLib.dll) separately")
    sdk = shutil.which(args.dotnet)
    sdk_version = "missing"
    if sdk:
        try:
            sdk_version = output([sdk, "--version"])
            if not sdk_version.startswith("9.0."):
                errors.append(f"Expected .NET 9 SDK, found {sdk_version}")
        except BuildError as e:
            errors.append(str(e))
    else:
        errors.append(".NET 9 SDK not on PATH")
    godot = args.godot.resolve()
    godot_version = "missing"
    if godot.is_file():
        try:
            godot_version = output([str(godot), "--version"])
            if not godot_version.startswith(GODOT_VERSION + ".") or ".mono." not in godot_version.lower():
                errors.append(f"Use MegaDot mono {GODOT_VERSION}, not {godot_version}")
        except BuildError as e:
            errors.append(str(e))
    else:
        errors.append("MegaDot 4.5.1 mono executable not found")
    return {"errors": errors, "game": version, "baselib": base_version, "dotnet": sdk_version,
            "godot": godot_version, "sdk_path": sdk, "data": data, "mods": mods}


def source_inputs() -> list[bytes]:
    names = subprocess.check_output(["git", "ls-files", "-z", "--cached", "--others", "--exclude-standard", "--",
                                     "mod", "tools", ".github", "global.json"], cwd=ROOT).split(b"\0")
    return sorted(set(n for n in names if n))


def source_identity() -> dict:
    digest = hashlib.sha256()
    for raw in source_inputs():
        name = raw.decode("utf-8")
        path = ROOT / name
        if not path.is_file():
            digest.update(raw + b"\0deleted\0")
        else:
            digest.update(raw + b"\0" + sha(path).encode("ascii") + b"\0")
    return {"git_sha": output(["git", "rev-parse", "HEAD"]),
            "dirty": bool(output(["git", "status", "--porcelain"])), "source_tree_sha256": digest.hexdigest()}


def pack_resources(project: Path | None = None) -> list[dict]:
    project = project or ROOT / "mod"
    resources = []
    for path in sorted((project / MOD_ID).rglob("*")):
        if not path.is_file():
            continue
        kind = {".png": "texture", ".jpg": "texture", ".jpeg": "texture", ".wav": "audio",
                ".ogg": "audio", ".json": "json", ".csv": "csv"}.get(path.suffix.lower())
        if kind:
            resources.append({"path": "res://" + path.relative_to(project).as_posix(), "kind": kind})
    if not resources:
        raise BuildError("Resource list is empty")
    return resources


def build(args: argparse.Namespace) -> Path:
    facts = preflight(args)
    if facts["errors"]:
        raise BuildError("Preflight failed:\n- " + "\n- ".join(facts["errors"]))
    bundle = args.output.resolve()
    if bundle == ROOT or (ROOT / "mod") == bundle or (ROOT / "mod") in bundle.parents:
        raise BuildError("Output must be outside the Godot project (use .arena/builds/...)")
    if bundle.exists():
        raise BuildError("Output already exists; choose a fresh directory to rule out stale artifacts")
    if ROOT in bundle.parents:
        ignored = subprocess.run(["git", "check-ignore", "-q", str(bundle)], cwd=ROOT)
        if ignored.returncode != 0:
            raise BuildError("An in-repository output must be ignored by Git (use .arena/builds/...)")
    bundle.mkdir(parents=True)
    identity = source_identity()
    # Staged inputs only: never import into checkout or copy ignored local paths/old artifacts.
    project = bundle / "project"
    project.mkdir()
    for raw in source_inputs():
        name = raw.decode("utf-8")
        path = ROOT / name
        if not name.startswith("mod/") or not path.is_file():
            continue
        if path.is_symlink():
            raise BuildError(f"Source symlink not supported: {name}")
        dest = project / Path(name).relative_to("mod")
        dest.parent.mkdir(parents=True, exist_ok=True)
        shutil.copy2(path, dest)
    if (ROOT / "global.json").is_file():
        shutil.copy2(ROOT / "global.json", bundle / "global.json")
    env = os.environ.copy()
    env.update({"CopyModOnBuild": "false", "IsInnerGodotExport": "true", "MSBUILDDISABLENODEREUSE": "1",
                "DOTNET_CLI_TELEMETRY_OPTOUT": "1", "DOTNET_NOLOGO": "1",
                "Sts2DataDir": str(facts["data"]), "SourceRevisionId": identity["git_sha"],
                "SourceTreeHash": identity["source_tree_sha256"], "GitDirty": str(identity["dirty"]).lower()})
    run([facts["sdk_path"], "--info"], bundle / "dotnet-info.log", env=env)
    managed = bundle / "managed"
    intermediate = bundle / "intermediate"
    run([facts["sdk_path"], "build", str(project / "SlayTheRequiem.csproj"), "-c", "Release", "--no-incremental",
         "--verbosity", "normal", "--output", str(managed), f"-bl:{bundle / 'build.binlog'}",
         "-p:CopyModOnBuild=false", f"-p:Sts2DataDir={facts['data']}",
         f"-p:SourceRevisionId={identity['git_sha']}", f"-p:SourceTreeHash={identity['source_tree_sha256']}",
         f"-p:GitDirty={str(identity['dirty']).lower()}", f"-p:MSBuildProjectExtensionsPath={intermediate}{os.sep}",
         f"-p:BaseIntermediateOutputPath={intermediate}{os.sep}"],
        bundle / "build.log", env=env)
    dll = managed / f"{MOD_ID}.dll"
    if not dll.is_file():
        raise BuildError("Build returned without the mod DLL")
    assets = read_object(intermediate / "project.assets.json")
    if f"Alchyr.Sts2.BaseLib/{BASELIB_VERSION}" not in assets.get("libraries", {}):
        raise BuildError("Restored BaseLib version does not match 3.4.8")
    if "Alchyr.Sts2.ModAnalyzers/0.2.1" not in assets.get("libraries", {}):
        raise BuildError("Restored ModAnalyzers version does not match 0.2.1")
    run([facts["sdk_path"], "test", str(project / "SlayTheRequiem.Tests/SlayTheRequiem.Tests.csproj"),
         "-c", "Release", "--logger", "trx;LogFileName=core.trx", "--results-directory", str(bundle / "test-results")],
        bundle / "core-tests.log", env=env)
    shutil.copy2(dll, bundle / dll.name)
    pdb = managed / f"{MOD_ID}.pdb"
    if pdb.is_file():
        shutil.copy2(pdb, bundle / pdb.name)
    shutil.copy2(project / f"{MOD_ID}.json", bundle / f"{MOD_ID}.json")
    run([str(args.godot.resolve()), "--headless", "--editor", "--path", str(project), "--import"],
        bundle / "import.log", env=env)
    pck = bundle / f"{MOD_ID}.pck"
    run([str(args.godot.resolve()), "--headless", "--path", str(project), "--export-pack", "BasicExport", str(pck)],
        bundle / "export.log", env=env)
    if not pck.is_file() or pck.stat().st_size < 16:
        raise BuildError("Export returned without a nonempty PCK")
    # EMPTY project: source assets cannot hide a broken pack.
    check_project = bundle / "pack-check-project"
    check_project.mkdir()
    (check_project / "project.godot").write_text('config_version=5\n[application]\nconfig/name="Pack verifier"\n', encoding="utf-8")
    shutil.copy2(Path(__file__).with_name("verify_pack.gd"), check_project / "verify_pack.gd")
    required = bundle / "pack-resources.json"
    resources = pack_resources(project)
    write_json(required, resources)
    run([str(args.godot.resolve()), "--headless", "--path", str(check_project), "--script", "verify_pack.gd", "--",
         str(pck), str(required), str(bundle / "pack-check.json")], bundle / "pack-check.log", env=env)
    report = read_object(bundle / "pack-check.json")
    if report.get("failed") != [] or report.get("checked", 0) != len(resources) or report.get("isolated_project") is not True:
        raise BuildError("PCK resource verification did not pass")
    if sha(dll) != sha(bundle / dll.name) or source_identity()["source_tree_sha256"] != identity["source_tree_sha256"]:
        raise BuildError("Build inputs or managed output changed during export; discard this bundle and inspect logs")
    mods = []
    for path in sorted(facts["mods"].glob("*/*.json")):
        try:
            info = read_object(path)
            if info.get("id"):
                mods.append({"id": info["id"], "version": info.get("version", "unknown"), "state": "installed-not-runtime-confirmed"})
        except (ValueError, OSError, BuildError):
            pass
    passport = {"schema": 1, **identity, "built_at_utc": utc(), "game_version": GAME_VERSION,
                "baselib_version": BASELIB_VERSION, "dotnet_sdk": facts["dotnet"], "godot": facts["godot"],
                "godot_executable_sha256": sha(args.godot.resolve()), "exported_at_utc": utc(),
                "save_schema": SAVE_SCHEMA, "shop_protocol": SHOP_PROTOCOL, "installed_mods": mods,
                "dependencies": {**{name: sha(facts["data"] / name) for name in ("sts2.dll", "0Harmony.dll")},
                                 "installed_baselib_sha256": sha(facts["mods"] / "BaseLib/BaseLib.dll")},
                "artifacts": {name: {"sha256": sha(bundle / name), "bytes": (bundle / name).stat().st_size} for name in ARTIFACTS},
                "proof": {"full_mod_build": True, "core_tests": True, "pack_export": True, "pack_resource_loader": report,
                          "game_runtime": False, "co_op": False},
                "logs": ["dotnet-info.log", "build.log", "build.binlog", "core-tests.log", "test-results/core.trx", "import.log", "export.log", "pack-check.log"]}
    write_json(bundle / PASSPORT, passport)
    return bundle


def verify_bundle(bundle: Path) -> dict:
    if bundle.is_symlink() or (bundle / PASSPORT).is_symlink():
        raise BuildError("Bundle/passport symlinks are not accepted")
    passport = read_object(bundle / PASSPORT)
    if passport.get("schema") != 1 or passport.get("game_version") != GAME_VERSION or passport.get("baselib_version") != BASELIB_VERSION:
        raise BuildError("Unsupported bundle schema/dependencies")
    if type(passport.get("save_schema")) is not int or passport["save_schema"] != SAVE_SCHEMA \
            or type(passport.get("shop_protocol")) is not int or passport["shop_protocol"] != SHOP_PROTOCOL:
        raise BuildError("Bundle save schema/shop protocol does not match this installer; use the matching source checkout")
    proof = passport.get("proof", {})
    if not isinstance(proof, dict) or not all(proof.get(stage) is True for stage in ("full_mod_build", "core_tests", "pack_export")):
        raise BuildError("Incomplete build/test/export proof")
    report = proof.get("pack_resource_loader")
    if not isinstance(report, dict) or report.get("failed") != [] or report.get("isolated_project") is not True \
            or type(report.get("checked")) is not int or report["checked"] < 1:
        raise BuildError("Isolated pack resource check incomplete")
    if not isinstance(passport.get("artifacts"), dict) or set(passport["artifacts"]) != set(ARTIFACTS):
        raise BuildError("Unexpected artifact list")
    for name in ARTIFACTS:
        path = bundle / name
        expected = passport["artifacts"][name]
        if not isinstance(expected, dict) or type(expected.get("bytes")) is not int or not isinstance(expected.get("sha256"), str):
            raise BuildError(f"Invalid artifact metadata: {name}")
        if path.is_symlink() or not path.is_file() or path.stat().st_size != expected["bytes"] or sha(path) != expected["sha256"]:
            raise BuildError(f"Artifact missing or hash mismatch: {name}")
    manifest = read_object(bundle / f"{MOD_ID}.json")
    if manifest.get("id") != MOD_ID or manifest.get("has_dll") is not True or manifest.get("has_pck") is not True:
        raise BuildError("Bundle manifest is not this DLL/PCK mod")
    return passport


def install(bundle: Path, mods: Path, *, game_closed: bool) -> Path:
    if not game_closed:
        raise BuildError("Close the game, then explicitly pass --game-closed")
    if mods.is_symlink():
        raise BuildError("Use the actual mods directory, not a symlink")
    passport = verify_bundle(bundle)
    mods = mods.resolve()
    target = mods / MOD_ID
    if target.is_symlink():
        raise BuildError("Refusing to replace a symlinked installation")
    for name in ARTIFACTS:
        if (mods / name).exists():
            raise BuildError(f"Loose legacy mod artifact exists in mods/: {name}; move it outside mods before installing")
    for path in mods.glob("*/*.json"):
        if path.parent == target:
            continue
        try:
            manifest = read_object(path)
        except (OSError, ValueError, BuildError):
            continue
        if manifest.get("id") == MOD_ID:
            raise BuildError("Another directory with the same mod ID exists; move it outside mods before installing")
    if target.exists():
        try:
            old = read_object(target / f"{MOD_ID}.json")
        except (ValueError, OSError, BuildError) as e:
            raise BuildError("Existing directory is not a recognized mod installation; no files changed") from e
        if old.get("id") != MOD_ID:
            raise BuildError("Existing directory belongs to another mod; no files changed")
    mods.mkdir(parents=True, exist_ok=True)
    staging = mods / f".requiem-install-{uuid.uuid4().hex}"
    staging.mkdir()
    backup = None
    try:
        for name in (*ARTIFACTS, PASSPORT):
            shutil.copy2(bundle / name, staging / name)
        pdb = bundle / f"{MOD_ID}.pdb"
        if pdb.is_file() and not pdb.is_symlink():
            shutil.copy2(pdb, staging / pdb.name)
        verify_bundle(staging)
        if target.exists():
            backups = mods.parent / ".requiem-backups"
            backups.mkdir(exist_ok=True)
            backup = backups / f"{MOD_ID}-{uuid.uuid4().hex}"
            target.rename(backup)
        try:
            staging.rename(target)
            verify_bundle(target)
        except BaseException:
            if target.exists():
                target.rename(staging)
            if backup is not None:
                backup.rename(target)
            raise
        write_json(bundle / "install-receipt.json", {"installed_at_utc": utc(), "artifacts": passport["artifacts"],
                   "clean_directory_swap": True, "previous_installation_backed_up": backup is not None, "runtime_verified": False})
    finally:
        if staging.exists():
            shutil.rmtree(staging)  # only our newly created staging directory, never user content
    return target


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    sub = parser.add_subparsers(dest="command", required=True)
    for name in ("probe", "build"):
        p = sub.add_parser(name)
        p.add_argument("--game-dir", type=Path, required=True)
        p.add_argument("--game-data", type=Path)
        p.add_argument("--mods-dir", type=Path)
        p.add_argument("--godot", type=Path, required=True)
        p.add_argument("--dotnet", default="dotnet")
        if name == "build":
            p.add_argument("--output", type=Path, required=True)
    p = sub.add_parser("verify")
    p.add_argument("--bundle", type=Path, required=True)
    p = sub.add_parser("install")
    p.add_argument("--bundle", type=Path, required=True)
    p.add_argument("--mods-dir", type=Path, required=True)
    p.add_argument("--game-closed", action="store_true")
    args = parser.parse_args()
    try:
        if args.command == "probe":
            facts = preflight(args)
            print(json.dumps({k: v for k, v in facts.items() if k not in ("sdk_path", "data", "mods")}, ensure_ascii=False, indent=2))
            return 1 if facts["errors"] else 0
        if args.command == "build":
            print("Verified bundle:", build(args))
        elif args.command == "verify":
            verify_bundle(args.bundle)
            print("DLL/PCK/manifest hashes verified; this is NOT a gameplay test.")
        elif args.command == "install":
            print("Clean matching pair installed:", install(args.bundle, args.mods_dir, game_closed=args.game_closed))
        return 0
    except (BuildError, OSError, ValueError) as e:
        print(f"ERROR: {e}", file=sys.stderr)
        return 1


if __name__ == "__main__":
    sys.exit(main())
