#!/usr/bin/env python3
"""B3 sprite masters -> reproducible RGBA cutouts on the ORIGINAL runtime canvases.

No image-model calls, replacement masters or anatomy stretching. The source is retained verbatim; magenta
unmatting, documented orientation correction, contain-fit and ground alignment are explicit. Equal canvas
sizes preserve NodeFactory bounds during Swap. An automated alpha check is NOT in-game art acceptance.

python tools/art/monsters/illustrated_sprites.py --render
python tools/art/monsters/illustrated_sprites.py --check
"""
from __future__ import annotations
import argparse
import hashlib
import json
from pathlib import Path

import numpy as np
from PIL import Image, ImageChops, ImageOps

ROOT = Path(__file__).resolve().parents[3]
MANIFEST = ROOT / "art/illustrations/sprite-manifest.json"


def entries():
    return json.loads(MANIFEST.read_text(encoding="utf-8"))["assets"]


def unmatte(raw):
    """Magenta spill is absent from these gray/blue/brown characters. Unmix edges, do not leave pink fringes.

    For an antialiased foreground edge over (255,0,255), min(R,B)-G estimates background contribution.
    Fully keyed RGB is zeroed; opaque non-magenta paint stays intact. Thresholds are versioned source.
    """
    rgb = np.asarray(raw.convert("RGB"), dtype=np.float32)
    spill = np.maximum(0, np.minimum(rgb[..., 0], rgb[..., 2]) - rgb[..., 1])
    alpha = 1 - spill / 255
    alpha[spill <= 16] = 1
    alpha[spill >= 235] = 0
    safe = np.maximum(alpha, 1 / 255)
    color = rgb.copy()
    color[..., 0] -= (1 - alpha) * 255
    color[..., 2] -= (1 - alpha) * 255
    color = np.clip(color / safe[..., None], 0, 255)
    color[alpha == 0] = 0
    return Image.fromarray(np.dstack((np.rint(color), np.rint(alpha * 255))).astype(np.uint8), "RGBA")


def export(row):
    with Image.open(ROOT / row["master"]) as raw:
        # Generated masters have a thin contaminated outer image edge. The explicit safe crop removes only
        # empty matte margin, not anatomy; keep it in provenance rather than silently erasing arbitrary pixels.
        cutout = unmatte(raw.crop(tuple(row["source_safe_crop_xyxy"])))
    box = cutout.getchannel("A").point(lambda a: 255 if a >= 8 else 0).getbbox()
    if not box:
        raise ValueError(f"{row['key']}: empty keyed sprite")
    cutout = cutout.crop(box)
    if row["flip_x"]:
        cutout = ImageOps.mirror(cutout)
    left, top, right, bottom = row["fit_box_xyxy"]
    width, height = right - left, bottom - top
    cutout = ImageOps.contain(cutout, (width, height), Image.Resampling.LANCZOS)
    canvas = Image.new("RGBA", row["canvas"], (0, 0, 0, 0))
    canvas.alpha_composite(cutout, (left + (width - cutout.width) // 2, bottom - cutout.height))
    # Canonical RGB for transparent pixels avoids retaining a chroma rectangle in compressed imports.
    pixels = np.asarray(canvas).copy()
    pixels[pixels[..., 3] == 0, :3] = 0
    return Image.fromarray(pixels)


def target(row):
    return ROOT / "mod" / row["runtime"].removeprefix("res://")


def check(render=False):
    errors = []
    rows = entries()
    if len({r["runtime"] for r in rows}) != len(rows):
        errors.append("duplicate runtime output")
    for row in rows:
        path = ROOT / row["master"]
        if not path.is_file() or hashlib.sha256(path.read_bytes()).hexdigest() != row["master_sha256"]:
            errors.append(f"{row['key']}: master hash mismatch/absent")
            continue
        if row["runtime"] != f"res://SlayTheRequiem/images/monsters/{row['key']}.png":
            errors.append(f"{row['key']}: invalid runtime path")
            continue
        left, top, right, bottom = row["fit_box_xyxy"]
        if not 0 < left < right < row["canvas"][0] or not 0 < top < bottom < row["canvas"][1]:
            errors.append(f"{row['key']}: invalid padding")
            continue
        prompts = json.loads((ROOT / row["prompt_file"]).read_text(encoding="utf-8"))
        if not prompts.get(row["prompt_key"]) or not row.get("provenance") or not row.get("mechanical_intent"):
            errors.append(f"{row['key']}: incomplete provenance")
        if row["reference_master"] and not any(r["master"] == row["reference_master"] for r in rows):
            errors.append(f"{row['key']}: generated reference is not retained")
        with Image.open(path) as raw:
            x1, y1, x2, y2 = row["source_safe_crop_xyxy"]
            if not 0 <= x1 < x2 <= raw.width or not 0 <= y1 < y2 <= raw.height:
                errors.append(f"{row['key']}: unsafe master crop")
                continue
        expected = export(row)
        alpha = np.asarray(expected.getchannel("A"))
        if (alpha > 128).sum() < expected.width * expected.height * .2 or alpha.max() != 255:
            errors.append(f"{row['key']}: empty or unexpectedly translucent foreground")
        if np.any(alpha[0]) or np.any(alpha[-1]) or np.any(alpha[:, 0]) or np.any(alpha[:, -1]):
            errors.append(f"{row['key']}: cutout touches canvas edge")
        rgba = np.asarray(expected)
        pink = (rgba[..., 0].astype(int) - rgba[..., 1] > 70) & (rgba[..., 2].astype(int) - rgba[..., 1] > 70) & (alpha > 32)
        if np.any(pink):
            errors.append(f"{row['key']}: magenta matte spill")
        dest = target(row)
        if render:
            dest.parent.mkdir(parents=True, exist_ok=True); expected.save(dest, optimize=True)
        if not dest.is_file():
            errors.append(f"{row['key']}: runtime export missing")
        else:
            with Image.open(dest) as raw:
                actual = raw.convert("RGBA")
            if actual.size != expected.size or any(channel.getbbox() for channel in ImageChops.difference(actual, expected).split()):
                errors.append(f"{row['key']}: stale RGBA pixels (run --render)")
    for first, second in (("king_under_barrow", "king_under_barrow_p2"), ("hero_werewolf", "hero_werewolf_beast")):
        pair = [r["canvas"] for r in rows if r["key"] in (first, second)]
        if len(pair) != 2 or pair[0] != pair[1]:
            errors.append(f"{first}: phase/form canvas changed bounds")
    for error in errors:
        print("ERROR", error)
    print(f"illustrated sprites: {len(rows)} masters / {len(rows)} RGBA exports; {len(errors)} errors")
    return int(bool(errors))


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--render", action="store_true")
    parser.add_argument("--check", action="store_true")
    return check(parser.parse_args().render)


if __name__ == "__main__":
    raise SystemExit(main())
