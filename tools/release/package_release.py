#!/usr/bin/env python3
"""Build/verify a deterministic UNACCEPTED candidate archive from a real G0 bundle and its frozen source.

No downloads, game DLLs, private saves/logs/binlogs, automatic installation or publication.
Candidate packaging is not game acceptance. Final acceptance seals the immutable archive separately.
"""
from __future__ import annotations

import argparse
import hashlib
import io
import json
from pathlib import Path, PurePosixPath
import posixpath
import re
import subprocess
import sys
import tarfile
import tempfile
import zipfile

ROOT = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(ROOT / "tools/g0"))
import mod_build as build

MANIFEST = "candidate.json"
MOD_DIR = "mods/" + build.MOD_ID + "/"
MAX_BYTES = 256 * 1024 * 1024
MAX_FILES = 10000
FORBIDDEN_SUFFIXES = {".dll", ".pck", ".exe", ".so", ".dylib", ".save", ".binlog", ".log", ".key", ".pem", ".pfx", ".zip"}
FORBIDDEN_DIRS = {".git", ".arena", ".arena-ci", ".godot", "bin", "obj", "node_modules", "__pycache__", "dist"}
PUBLIC_DOCS = {"INSTALL.md": "docs/release/INSTALL.md", "COMPATIBILITY.md": "docs/release/COMPATIBILITY.md",
               "RELEASE_NOTES.md": "docs/release/RELEASE_NOTES.md", "ART_PROVENANCE.md": "docs/release/ART_PROVENANCE.md",
               "FINAL_ACCEPTANCE.md": "docs/release/FINAL_ACCEPTANCE.md", "DIAGNOSTICS.md": "docs/release/DIAGNOSTICS.md",
               "asset-coverage.csv": "docs/release/asset-coverage.csv", "final-matrix.plan.json": "docs/release/final-matrix.plan.json",
               "source/docs/GUIDE.md": "docs/GUIDE.md"}


def require(condition, message):
    if not condition:
        raise build.BuildError(message)


def encoded(value):
    return (json.dumps(value, ensure_ascii=False, sort_keys=True, indent=2, allow_nan=False) + "\n").encode("utf-8")


def digest(data):
    return hashlib.sha256(data).hexdigest()


def read_json(data):
    def unique(pairs):
        result = {}
        for key, value in pairs:
            require(key not in result, "Duplicate JSON field")
            result[key] = value
        return result
    return json.loads(data, object_pairs_hook=unique,
                      parse_constant=lambda _: (_ for _ in ()).throw(build.BuildError("Non-finite JSON")))


def safe_name(name):
    require(isinstance(name, str) and name and "\\" not in name and not any(ord(char) < 32 for char in name), "Unsafe archive path")
    path = PurePosixPath(name)
    require(not path.is_absolute() and all(part not in ("", ".", "..") and ":" not in part
            and part.rstrip(" .") == part for part in name.split("/")), "Unsafe archive path")
    return path


def source_name(name):
    path = safe_name(name)
    require(not set(path.parts) & FORBIDDEN_DIRS and path.suffix.lower() not in FORBIDDEN_SUFFIXES,
            "Forbidden/private/binary source file: " + name)
    # A tracked source tree is not an excuse to ship arbitrary credential/home directories.
    require(path.parts[0] in {"mod", "tools", "art", "docs", ".github"}
            or len(path.parts) == 1 and (path.suffix.lower() in {".md", ".json"} or path.name in {".gitignore", ".gitattributes", ".editorconfig", "LICENSE", "COPYING"}),
            "Source path outside release allowlist: " + name)


def is_build_input(name):
    return any(name == root or name.startswith(root + "/") for root in build.SOURCE_ROOTS)


def source_digest(files):
    value = hashlib.sha256()
    for name, data in sorted(files.items(), key=lambda item: item[0].encode("utf-8")):
        if is_build_input(name):
            value.update(name.encode("utf-8") + b"\0" + digest(data).encode("ascii") + b"\0")
    return value.hexdigest()


def frozen_source(commit):
    require(isinstance(commit, str) and re.fullmatch(r"[0-9a-f]{40}", commit), "Full source SHA required")
    raw = subprocess.check_output(["git", "archive", "--format=tar", commit], cwd=ROOT)
    require(len(raw) <= MAX_BYTES, "Source snapshot too large")
    result = {}
    with tarfile.open(fileobj=io.BytesIO(raw), mode="r:") as archive:
        for member in archive:
            if member.isdir() or member.name.startswith(("docs/review/evidence/", "docs/review/assets/")):
                continue  # historical logs/contact sheets are public in Git, but not part of the shipped source/build inputs
            source_name(member.name)
            require(member.isfile() and len(result) < MAX_FILES, "Source symlink/special file or too many inputs")
            require(0 <= member.size <= MAX_BYTES, "Invalid source file size")
            result[member.name] = archive.extractfile(member).read()
    require(result, "No frozen source inputs")
    return result


def public_document(target, original, source):
    """Lift install docs without breaking relative links. The guide stays in its original source/docs tree."""
    data = source[original]
    if not original.endswith(".md") or target == "source/" + original:
        return data
    targets = {value: key for key, value in PUBLIC_DOCS.items()}

    def link(match):
        href = match[2]
        if href.startswith(("#", "//")) or re.match(r"[a-zA-Z][a-zA-Z0-9+.-]*:", href):
            return match[0]
        path, separator, fragment = href.partition("#")
        resolved = posixpath.normpath(posixpath.join(posixpath.dirname(original), path))
        safe_name(resolved)
        require(resolved in source or any(name.startswith(resolved.rstrip("/") + "/") for name in source),
                "Missing packaged documentation link: " + resolved)
        destination = targets.get(resolved, "source/" + resolved)
        return match[1] + destination + (separator + fragment if separator else "") + ")"

    return re.sub(r"(\[[^\]\n]+\]\()([^\s)]+)\)", link, data.decode("utf-8")).encode("utf-8")


def public_passport(passport):
    # Runtime/installer-compatible subset. Do NOT copy private installed-mod IDs or unbounded engine strings.
    keys = ("schema", "git_sha", "dirty", "source_tree_sha256", "built_at_utc", "exported_at_utc", "game_version",
            "baselib_version", "dotnet_sdk", "save_schema", "shop_protocol", "session_protocol", "creation_schema", "economy_revision",
            "godot_executable_sha256")
    result = {key: passport[key] for key in keys if key in passport}
    result["artifacts"] = {name: {key: passport["artifacts"][name][key] for key in ("sha256", "bytes")} for name in build.ARTIFACTS}
    dependencies = passport.get("dependencies")
    required = ("sts2.dll", "0Harmony.dll", "installed_baselib_sha256")
    require(isinstance(dependencies, dict) and all(isinstance(dependencies.get(key), str)
            and re.fullmatch(r"[0-9a-f]{64}", dependencies[key]) for key in required), "Missing legal-build dependency hashes")
    result["dependencies"] = {key: dependencies[key] for key in required}
    result["godot"] = build.GODOT_VERSION + " mono (version string hashed; private build logs excluded)"
    version_hash = passport.get("godot_version_sha256", digest(str(passport.get("godot", "")).encode("utf-8")))
    require(isinstance(version_hash, str) and re.fullmatch(r"[0-9a-f]{64}", version_hash), "Invalid engine version hash")
    result["godot_version_sha256"] = version_hash
    proof = passport["proof"]
    report = proof["pack_resource_loader"]
    result["proof"] = {key: proof[key] for key in ("full_mod_build", "core_tests", "pack_export")}
    counters = ("total", "executed", "passed", "failed", "error", "aborted", "timeout", "notExecuted", "notRunnable", "disconnected")
    result["proof"]["core_test_results"] = {key: proof["core_test_results"].get(key, 0) for key in counters}
    result["proof"].update(pack_resource_loader={key: report[key] for key in ("checked", "failed", "isolated_project")}, game_runtime=False, co_op=False)
    result["note"] = "Build/hash consistency only; runtime acceptance is a separate record for this exact DLL/PCK/source/archive. Not signed provenance."
    return result


def write_archive(files, output):
    require(not output.exists(), "Use a new archive path; accepted archives are immutable")
    require(len(files) <= MAX_FILES and sum(map(len, files.values())) <= MAX_BYTES, "Archive exceeds bounded budget")
    for name in files:
        safe_name(name)
    output.parent.mkdir(parents=True, exist_ok=True)
    # Bytes/timestamps/permissions/order are fixed for the same inputs and Python/zlib toolchain.
    with output.open("xb") as stream, zipfile.ZipFile(stream, "w", compression=zipfile.ZIP_DEFLATED, compresslevel=9) as archive:
        for name, data in sorted(files.items()):
            info = zipfile.ZipInfo(name, (1980, 1, 1, 0, 0, 0))
            info.compress_type = zipfile.ZIP_DEFLATED
            info.external_attr = 0o100644 << 16
            archive.writestr(info, data, compress_type=zipfile.ZIP_DEFLATED, compresslevel=9)


def create(bundle, output):
    passport = build.verify_bundle(bundle)
    current = build.source_identity()
    require(passport["dirty"] is False and current["dirty"] is False, "Release candidates require a clean identified build and checkout")
    require(current["source_tree_sha256"] == passport["source_tree_sha256"], "Build inputs changed; rebuild before packaging")
    source = frozen_source(passport["git_sha"])
    require(source_digest(source) == passport["source_tree_sha256"], "Frozen source is not the built input hash")
    files = {"source/" + name: data for name, data in source.items()}
    for target, original in PUBLIC_DOCS.items():
        require(original in source, "Missing release document: " + original)
        files[target] = public_document(target, original, source)
    for name in build.ARTIFACTS:
        files[MOD_DIR + name] = (bundle / name).read_bytes()
    files[MOD_DIR + build.PASSPORT] = encoded(public_passport(passport))
    files[MANIFEST] = encoded(dict(schema=1, kind="unaccepted-candidate", source_commit=passport["git_sha"],
        source_tree_sha256=passport["source_tree_sha256"],
        warning="NOT a released/accepted mod. Complete final acceptance; publication requires owner authorization.",
        entries={name: dict(sha256=digest(data), bytes=len(data)) for name, data in sorted(files.items())}))
    write_archive(files, output)
    verify(output)
    return dict(archive_sha256=build.sha(output), source_commit=passport["git_sha"], entries=len(files), accepted=False)


def verify(path):
    require(path.is_file() and not path.is_symlink() and path.stat().st_size <= MAX_BYTES, "Archive must be a bounded regular file")
    with zipfile.ZipFile(path) as archive:
        entries = archive.infolist()
        require(0 < len(entries) <= MAX_FILES and sum(item.file_size for item in entries) <= MAX_BYTES, "Oversized archive")
        require(len(entries) == len({item.filename.casefold() for item in entries}), "Duplicate/case-colliding archive entry")
        for entry in entries:
            name = entry.filename
            safe_name(name)
            require(not entry.is_dir() and entry.external_attr >> 16 & 0o170000 != 0o120000, "Directory/symlink archive entry")
            if name.startswith("source/"):
                source_name(name.removeprefix("source/"))
            else:
                require(name in {MANIFEST, *PUBLIC_DOCS} or name in {MOD_DIR + item for item in (*build.ARTIFACTS, build.PASSPORT)}, "Unexpected public file")
        require(MANIFEST in archive.namelist(), "Missing candidate manifest")
        manifest = read_json(archive.read(MANIFEST))
        require(isinstance(manifest, dict) and type(manifest.get("schema")) is int and manifest["schema"] == 1
                and manifest.get("kind") == "unaccepted-candidate", "Wrong candidate manifest")
        metadata = manifest.get("entries")
        require(isinstance(metadata, dict) and set(metadata) == set(archive.namelist()) - {MANIFEST}, "Missing/extra manifest entries")
        files = {}
        for name, expected in metadata.items():
            require(isinstance(expected, dict) and type(expected.get("bytes")) is int and expected["bytes"] >= 0, "Malformed entry metadata")
            data = archive.read(name)
            require(len(data) == expected["bytes"] and digest(data) == expected.get("sha256"), "Archive content hash mismatch: " + name)
            files[name] = data
    require(all(name in files for name in PUBLIC_DOCS), "Missing install/compatibility/provenance/guide documents")
    with tempfile.TemporaryDirectory(prefix="requiem-package-verify-") as directory:
        bundle = Path(directory)
        for name in (*build.ARTIFACTS, build.PASSPORT):
            require(MOD_DIR + name in files, "Missing runtime file")
            (bundle / name).write_bytes(files[MOD_DIR + name])
        passport = build.verify_bundle(bundle)
    require(passport == public_passport(passport), "Public build passport contains non-allowlisted/private fields")
    require(passport["dirty"] is False and manifest.get("source_commit") == passport["git_sha"]
            and manifest.get("source_tree_sha256") == passport["source_tree_sha256"], "Candidate/build identity mismatch")
    sources = {name.removeprefix("source/"): data for name, data in files.items() if name.startswith("source/")}
    require(source_digest(sources) == passport["source_tree_sha256"], "Archive source is not the compiled input hash")
    for target, original in PUBLIC_DOCS.items():
        require(original in sources and files[target] == public_document(target, original, sources), "Public document disagrees with source snapshot")
    return dict(passport=passport, manifest=manifest, archive_sha256=build.sha(path),
                plan=read_json(files["final-matrix.plan.json"]),
                passport_sha256=digest(files[MOD_DIR + build.PASSPORT]))


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    sub = parser.add_subparsers(dest="command", required=True)
    create_parser = sub.add_parser("candidate")
    create_parser.add_argument("--bundle", required=True, type=Path)
    create_parser.add_argument("--out", required=True, type=Path)
    check = sub.add_parser("verify")
    check.add_argument("archive", type=Path)
    args = parser.parse_args()
    try:
        if args.command == "candidate":
            print(json.dumps(create(args.bundle, args.out), indent=2))
        else:
            result = verify(args.archive)
            print(json.dumps(dict(archive_sha256=result["archive_sha256"], source_commit=result["passport"]["git_sha"], accepted=False), indent=2))
        return 0
    except (build.BuildError, OSError, ValueError, KeyError, zipfile.BadZipFile, subprocess.CalledProcessError) as error:
        print("ERROR:", error, file=sys.stderr)
        return 1


if __name__ == "__main__":
    raise SystemExit(main())
