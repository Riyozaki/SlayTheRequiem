#!/usr/bin/env python3
"""Offline integrity/link check for this historical evidence snapshot (not a mod test).

Run from any directory. --workspace additionally requires the checked code inputs to
still match b82c909; omit it when examining this historical snapshot after later work.
Both pinned Git source objects (8000886, 03d6360) must be available for port hashes.
"""
from __future__ import annotations

import argparse
import hashlib
import json
import re
import subprocess
from pathlib import Path
from urllib.parse import unquote, urlsplit

EVIDENCE = Path(__file__).resolve().parent
ROOT = EVIDENCE.parents[3]


def git(*args: str) -> bytes:
    return subprocess.check_output(["git", *args], cwd=ROOT, stderr=subprocess.PIPE)


def load(name: str) -> dict:
    return json.loads((EVIDENCE / name).read_text(encoding="utf-8"))


def digest(data: bytes) -> str:
    return hashlib.sha256(data).hexdigest()


def require(condition: bool, detail: str) -> None:
    if not condition:
        raise ValueError(detail)


def heading_slug(text: str) -> str:
    text = re.sub(r"<[^>]*>", "", text).replace("`", "").lower().strip()
    return re.sub(r"[^\w\- ]", "", text).replace(" ", "-")


def check_links(base: str) -> None:
    changed = git("diff", "--name-only", base).decode().splitlines()
    changed += git("ls-files", "--others", "--exclude-standard").decode().splitlines()
    files = sorted({Path(p) for p in changed if p.endswith(".md")})
    local_count = pinned_count = external_count = 0
    for file in files:
        text = (ROOT / file).read_text(encoding="utf-8")
        text = re.sub(r"^```[^\n]*\n.*?^```\s*$", "", text, flags=re.M | re.S)
        links = re.findall(r"!?\[[^\]\n]*\]\(\s*([^\n)]*)\)", text)
        links += re.findall(r"^\s{0,3}\[[^\]\n]+\]:\s*(\S+)", text, re.M)
        links += re.findall(r'<(?:img|a)\b[^>]*\b(?:src|href)=[\"\x27]([^\"\x27]+)', text)
        for raw in links:
            target = raw.split(' "', 1)[0].strip().strip("<>")
            url = urlsplit(target)
            pinned = re.fullmatch(
                r"/Riyozaki/SlayTheRequiem/blob/([0-9a-f]{7,40})/(.+)", unquote(url.path)
            ) if url.netloc == "github.com" else None
            if pinned:
                data = git("show", f"{pinned[1]}:{pinned[2]}")
                suffix = Path(pinned[2]).suffix
                pinned_count += 1
            elif url.scheme or url.netloc:
                external_count += 1
                continue  # External pages were NOT fetched by this offline check.
            else:
                path = (ROOT / file.parent / unquote(url.path)).resolve() if url.path else (ROOT / file).resolve()
                require(path.is_relative_to(ROOT) and path.exists(), f"Missing/outside local link: {file}: {target}")
                data = path.read_bytes() if path.is_file() else b""
                suffix = path.suffix
                local_count += 1
            fragment = unquote(url.fragment)
            if re.fullmatch(r"L\d+(?:-L?\d+)?", fragment):
                require(max(map(int, re.findall(r"\d+", fragment))) <= len(data.splitlines()),
                        f"Line range outside source: {file}: {target}")
            elif fragment and suffix == ".md":
                source = data.decode("utf-8")
                headings = {heading_slug(h) for h in re.findall(r"^#{1,6}\s+(.+)", source, re.M)}
                headings.update(re.findall(r'(?:id|name)=[\"\x27]([^\"\x27]+)', source))
                require(fragment in headings, f"Missing heading: {file}: {target}")
    print(f"Markdown: {len(files)} changed/new documents; {local_count} local links and {pinned_count} pinned Git source links verified")
    print(f"Other external URLs not fetched: {external_count}")


def verify(workspace: bool) -> None:
    inputs, checks, ci, adapters = (load(name) for name in (
        "runtime-inputs.json", "checks.json", "ci.json", "native-adapters.json"
    ))
    code = inputs["code_source_commit"]
    require(all(item["code_source_commit"] == code for item in (checks, ci, adapters)), "Mixed code commit identities")
    for item in inputs["files"]:
        require(digest(git("show", f"{code}:{item['path']}")) == item["sha256"], f"Git source hash: {item['path']}")
        if workspace:
            require(digest((ROOT / item["path"]).read_bytes()) == item["sha256"], f"Changed workspace input: {item['path']}")
    for check in checks["checks"]:
        require(check["exit_code"] == 0, f"Failed recorded check: {check['name']}")
        require(digest((EVIDENCE / check["log"]).read_bytes()) == check["log_sha256"], f"Changed log: {check['log']}")
    counters = ci["core_counters"]
    require(counters["total"] == counters["executed"] == counters["passed"] == 560, "Core PASS counts differ")
    require(all(value == 0 for key, value in counters.items() if key not in ("total", "executed", "passed")), "Non-pass Core counter")
    require(ci["conclusion"] == "success" and len(ci["jobs"]) == 4
            and all(job["conclusion"] == "success" for job in ci["jobs"]), "Recorded CI jobs are not successful")
    require(not ci["trx_downloaded_locally"], "This snapshot did not inspect a local TRX")
    source = git("show", f"{code}:{adapters['source_file']}")
    require(digest(source) == adapters["source_sha256"], "Native adapter source hash differs")
    table = source.decode().split("var cases =", 1)[1].split("var failures =", 1)[0]
    cases = re.findall(r'\("([^\"]+)",\s*(\w+)\)', table)
    require(len(cases) == adapters["source_cases"] == len({name for name, _ in cases}) == 67, "Native source count differs")
    require(cases == [(case["name"], case["method"]) for case in adapters["cases"]], "Native source names differ")
    require(not adapters["compiled"] and not adapters["executed"]
            and all(not case["executed"] for case in adapters["cases"]), "Native adapters were not executed")
    manifest = load("port-manifest.json")
    for item in manifest["files"]:
        for revision, field in ((manifest["prior_commit"], "prior_sha256"),
                                (manifest["from_commit"], "import_sha256"),
                                (manifest["reconstruction"]["first_implementation_commit"], "first_implementation_sha256")):
            if item[field] is not None:
                require(digest(git("show", f"{revision}:{item['path']}")) == item[field], f"Port source hash: {item['path']}")
    require(len(manifest["files"]) == len({item["path"] for item in manifest["files"]}) == 63, "Reconstructed scope count differs")
    print(f"Snapshot metadata and hashes: {len(inputs['files'])} changed code inputs, {len(checks['checks'])} recorded check logs")
    print("Core: 560/560 via stored public CI annotation; native adapters: 67 SOURCE-ONLY / NOT EXECUTED")
    print("Port: 63-file reconstructed scope verified against Git trees; original copy-list NOT independently recoverable")
    check_links(inputs["base_commit"])
    print("PASS (offline evidence/link integrity only; no build, engine, network or game test)")


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--workspace", action="store_true", help="also verify current executable input bytes")
    args = parser.parse_args()
    try:
        verify(args.workspace)
    except (ValueError, KeyError, OSError, subprocess.CalledProcessError) as error:
        raise SystemExit(f"FAIL: {error}")
