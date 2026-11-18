#!/usr/bin/env python3
"""Verify/reassemble actual Core CI annotation output; never reconstruct missing scenario data.

Input: gh api repos/OWNER/REPO/check-runs/ID/annotations?per_page=100
Output: only the three C#-generated files and a bounded verification receipt.
Not a native trace authenticator, full-mod build, TRX downloader or balance simulator.
"""
from __future__ import annotations

import argparse
import base64
import binascii
import gzip
import hashlib
import io
import json
import re
from pathlib import Path

TITLE = "B2.5 Core economic evidence "
CHUNK_CHARS = 2800
MAX_PARTS = 9  # ten notices per step, including the separate scenario summary
MAX_BYTES = 2 * 1024 * 1024
NAMES = {"core-scenarios.json", "core-scenarios.summary.json", "core-scenarios.md"}
FIELDS = {"schema", "producer", "encoding", "sha256", "part", "total", "data"}


def unique_object(pairs):
    result = {}
    for key, value in pairs:
        if key in result:
            raise ValueError(f"Duplicate JSON key: {key}")
        result[key] = value
    return result


def load_json(value):
    return json.loads(value, object_pairs_hook=unique_object)


def verify(annotations: list, source_commit: str) -> tuple[dict[str, str], dict]:
    if not re.fullmatch(r"[0-9a-f]{40}", source_commit):
        raise ValueError("Expected the full source commit from the successful CI run")
    if not isinstance(annotations, list) or len(annotations) > 100:
        raise ValueError("Expected a bounded annotation array")
    parts = {}
    identity = None
    for annotation in annotations:
        title = annotation.get("title", "")
        if not title.startswith(TITLE):
            continue
        message = annotation.get("message", "")
        try:
            fields = unique_object(item.split("=", 1) for item in message.split(";"))
        except (TypeError, ValueError) as error:
            raise ValueError("Invalid/truncated evidence packet") from error
        if set(fields) != FIELDS or (fields["schema"], fields["producer"], fields["encoding"]) != ("1", "core-scenario", "gzip-base64url"):
            raise ValueError("Unknown evidence packet schema/producer/encoding")
        if not re.fullmatch(r"[0-9a-f]{64}", fields["sha256"]):
            raise ValueError("Invalid bundle SHA256")
        try:
            part, total = int(fields["part"]), int(fields["total"])
        except ValueError as error:
            raise ValueError("Invalid part numbers") from error
        if not 1 <= part <= total <= MAX_PARTS or title != f"{TITLE}{part}/{total}":
            raise ValueError("Invalid part bounds/title or notice budget exceeded")
        current = fields["sha256"], total
        if identity is not None and identity != current:
            raise ValueError("Mixed evidence bundles")
        identity = current
        if part in parts:
            raise ValueError("Duplicate evidence part")
        data = fields["data"]
        if not 0 < len(data) <= CHUNK_CHARS or not re.fullmatch(r"[A-Za-z0-9_-]+={0,2}", data):
            raise ValueError("Invalid or oversized base64url part")
        parts[part] = data
    if identity is None or set(parts) != set(range(1, identity[1] + 1)):
        raise ValueError("Incomplete evidence: require every part, never repair missing data")
    encoded = "".join(parts[i] for i in range(1, identity[1] + 1))
    try:
        compressed = base64.b64decode(encoded, altchars=b"-_", validate=True)
        with gzip.GzipFile(fileobj=io.BytesIO(compressed)) as stream:
            raw = stream.read(MAX_BYTES + 1)
    except (binascii.Error, EOFError, OSError) as error:
        raise ValueError("Invalid base64url/gzip evidence") from error
    if len(raw) > MAX_BYTES:
        raise ValueError("Decompressed evidence exceeds the bounded budget")
    if hashlib.sha256(raw).hexdigest() != identity[0]:
        raise ValueError("Bundle SHA256 mismatch")
    files = load_json(raw)
    if not isinstance(files, dict) or set(files) != NAMES or not all(isinstance(v, str) for v in files.values()):
        raise ValueError("Unknown output file set; no paths outside the three Core outputs allowed")
    batch = load_json(files["core-scenarios.json"])
    summary = load_json(files["core-scenarios.summary.json"])
    if batch.get("producer") != "core-scenario" or type(batch.get("schema")) is not int or batch["schema"] != 1:
        raise ValueError("Not a Core scenario batch; never native evidence")
    runs = batch.get("runs", [])
    if len(runs) != 60 or sum(len(r.get("acts", [])) for r in runs) != 300:
        raise ValueError("Unexpected scenario/act counts")
    expected = dict(schema=1, producer="core-scenario", source_commit=source_commit,
                    actual_native_runs=0, scenarios=60, act_rows=300, paired_comparisons=30, paired_seeds_per_hero=10)
    if any(type(summary.get(k)) is not type(v) or summary[k] != v for k, v in expected.items()):
        raise ValueError("Summary source/scope/counts mismatch")
    hashes = {name: hashlib.sha256(text.encode("utf-8")).hexdigest() for name, text in files.items()}
    if hashes["core-scenarios.json"] != summary.get("json_sha256"):
        raise ValueError("Scenario JSON SHA256 mismatch")
    receipt = dict(schema=1, scope="Actual C# Core scenario output; NOT full mod build or native gameplay",
                   source_commit=source_commit, annotation_parts=len(parts), annotation_bundle_sha256=identity[0],
                   decompressed_bundle_bytes=len(raw), file_sha256=hashes, actual_native_runs=0)
    return files, receipt


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("annotations", type=Path)
    parser.add_argument("--commit", required=True)
    parser.add_argument("--out", type=Path, required=True)
    args = parser.parse_args()
    # Validate the entire bundle BEFORE creating/writing anything in the output directory.
    files, receipt = verify(load_json(args.annotations.read_bytes()), args.commit)
    args.out.mkdir(parents=True, exist_ok=True)
    for name, text in files.items():
        (args.out / name).write_bytes(text.encode("utf-8"))
    (args.out / "verification.json").write_text(json.dumps(receipt, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    print(f"Verified {receipt['annotation_parts']} parts / {receipt['decompressed_bundle_bytes']} bytes; actual native executions: 0")


if __name__ == "__main__":
    main()
