#!/usr/bin/env python3
"""Promoted raster card scenes: preserve masters, explicit crop, deterministic two-size export.

This deliberately does not call an image model or overwrite masters. The old SVG
portrait generator skips these keys so running it cannot silently undo a promotion.

  python tools/art/cards/illustrations.py --render
  python tools/art/cards/illustrations.py --check

Checks prove provenance/output consistency, not artistic or in-game acceptance.
"""
from __future__ import annotations

import argparse
import hashlib
import json
from pathlib import Path

from PIL import Image, ImageChops

ROOT = Path(__file__).resolve().parents[3]
MANIFEST = ROOT / "art/illustrations/manifest.json"
DESTINATION = ROOT / "mod/SlayTheRequiem/images/card_portraits"


def entries():
    return json.loads(MANIFEST.read_text(encoding="utf-8"))["assets"]


def promoted_keys():
    return {row["key"] for row in entries()}


def exports(row):
    with Image.open(ROOT / row["master"]) as raw:
        crop = raw.convert("RGB").crop(tuple(row["crop_xyxy"]))
    big = crop.resize((1000, 760), Image.Resampling.LANCZOS)
    yield DESTINATION / "big" / (row["key"] + ".png"), big
    yield DESTINATION / (row["key"] + ".png"), big.resize((250, 190), Image.Resampling.LANCZOS)


def check(render=False, quiet=False):
    errors = []
    rows = entries()
    if len(promoted_keys()) != len(rows):
        errors.append("duplicate promoted key")
    for row in rows:
        path = ROOT / row["master"]
        if not path.exists() or hashlib.sha256(path.read_bytes()).hexdigest() != row["master_sha256"]:
            errors.append(f"{row['key']}: master absent or does not match provenance")
            continue
        with Image.open(path) as master:
            x1, y1, x2, y2 = row["crop_xyxy"]
            if not (0 <= x1 < x2 <= master.width and 0 <= y1 < y2 <= master.height):
                errors.append(f"{row['key']}: crop out of bounds")
                continue
            # Never distort anatomy to fit the frame. The authored crop is already 1000:760 (+/- 1 pixel).
            if abs((x2 - x1) / (y2 - y1) - 1000 / 760) > .002:
                errors.append(f"{row['key']}: crop aspect would stretch the art")
                continue
        for target, expected in exports(row):
            if render:
                target.parent.mkdir(parents=True, exist_ok=True)
                expected.save(target, optimize=True)
            if not target.exists():
                errors.append(f"{target.relative_to(ROOT)}: missing export")
            else:
                with Image.open(target) as raw:
                    actual = raw.convert("RGB")
                if actual.size != expected.size or ImageChops.difference(actual, expected).getbbox():
                    errors.append(f"{target.relative_to(ROOT)}: stale pixels (run --render)")
        for field in ("prompt", "provenance", "rights", "mechanical_intent", "acceptance"):
            if not row.get(field):
                errors.append(f"{row['key']}: missing {field}")
        if not row.get("runtime_small") == f"res://SlayTheRequiem/images/card_portraits/{row['key']}.png":
            errors.append(f"{row['key']}: incorrect small runtime path")
        if not row.get("runtime_big") == f"res://SlayTheRequiem/images/card_portraits/big/{row['key']}.png":
            errors.append(f"{row['key']}: incorrect big runtime path")
    if not quiet:
        for error in errors:
            print("ERROR", error)
        print(f"promoted card scenes: {len(rows)} masters / {len(rows) * 2} exports; {len(errors)} errors")
    return int(bool(errors))


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--render", action="store_true")
    parser.add_argument("--check", action="store_true")
    args = parser.parse_args()
    return check(args.render)


if __name__ == "__main__":
    raise SystemExit(main())
