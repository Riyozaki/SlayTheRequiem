#!/usr/bin/env python3
"""B3.1: native-size-first map/camp symbols, not another hatch/noise pass.

Editable SVGs are kept outside the Godot project. The 64-unit drawing has >=4-unit
strokes (2 px at 32 px); there is no shared shop counter. Map outlines are white
alpha dilations for NNormalMapPoint's native background/hover tint, NOT a square.

  npm i --prefix .arena/stage3/resvg @resvg/resvg-js@2.6.2
  NODE_PATH=$PWD/.arena/stage3/resvg/node_modules python tools/art/readable_icons.py --render
  python tools/art/readable_icons.py --check

--check verifies authored geometry, source/output SHA256 receipt, size, alpha and
pixel-exact outlines without Node. --verify-render ALSO rerenders SVGs with resvg.
Neither a hash nor a size check is artistic/in-game acceptance.
"""
from __future__ import annotations

import argparse
import hashlib
import io
import json
import os
from pathlib import Path
import subprocess
import tempfile

from PIL import Image, ImageChops, ImageFilter

ROOT = Path(__file__).resolve().parents[2]
RES = ROOT / "mod/SlayTheRequiem/images"
RECEIPT = ROOT / "art/readable-icons.json"
INK, PAPER, SHADE = "#302922", "#eee3c8", "#b8a886"


def path(d, fill=PAPER, stroke=INK, width=4):
    return f'<path d="{d}" fill="{fill}" stroke="{stroke}" stroke-width="{width}"/>'


def circle(x, y, r, fill=PAPER, stroke=INK, width=4):
    return f'<circle cx="{x}" cy="{y}" r="{r}" fill="{fill}" stroke="{stroke}" stroke-width="{width}"/>'


# Each entry is an individual silhouette. No grain, filter, hatching or sub-pixel detail.
MAP = {
    "armorer": (
        path("M6 34 H43 L57 28 V39 L43 44 H35 V50 H45 V57 H12 V50 H23 V44 H14 Z")
        + path("M21 28 L37 12", "none", INK, 7)
        + path("M29 9 L37 3 L53 18 L45 26 Z", SHADE)
    ),
    "enchanter": (
        path("M18 58 L23 27 M18 27 L14 15 L24 5 L34 15 L29 27 Z", SHADE, INK, 5)
        + path("M46 24 L50 34 L59 38 L50 42 L46 53 L42 42 L33 38 L42 34 Z")
    ),
    "herbalist": (
        path("M22 7 H41 V15 H37 V25 Q57 40 52 51 Q48 60 31 59 Q12 59 10 48 Q8 38 26 25 V15 H22 Z")
        + path("M17 43 Q31 36 48 43 L48 50 Q31 58 17 50 Z", SHADE, INK, 4)
    ),
    "jeweler": (
        path("M17 10 H47 L59 28 L32 57 L5 28 Z")
        + path("M5 28 H59 M17 10 L22 28 L32 57 L42 28 L47 10", "none")
    ),
    "caravan": (
        path("M7 39 V24 Q7 9 26 9 H37 Q48 9 48 24 V39 Z")
        + path("M8 25 H48 M26 10 V39", "none")
        + path("M5 39 H50 L56 29 H60 M10 39 V48 H48 V39", "none", INK, 5)
        + circle(17, 51, 7, SHADE) + circle(43, 51, 7, SHADE)
    ),
    "fence": (
        circle(19, 20, 13, PAPER, INK, 5) + circle(19, 20, 5, INK, "none", 0)
        + path("M29 29 L56 54 L50 60 L43 53 L38 58 L31 51 L36 46 L23 34 Z", SHADE)
    ),
    "tavern": (
        path("M45 24 H52 Q62 24 58 39 Q55 47 45 44", "none", INK, 6)
        + path("M10 21 H44 V54 Q29 62 12 54 Z")
        + path("M9 22 Q2 13 12 10 Q15 2 24 8 Q35 1 40 10 Q51 10 46 22 Z")
        + path("M23 33 V48 M33 33 V48", "none", SHADE, 4)
    ),
    "library": (
        path("M5 14 Q17 8 32 18 Q46 8 59 14 V53 Q45 48 32 57 Q18 48 5 53 Z")
        + path("M32 18 V57 M12 23 Q20 21 25 26 M39 26 Q48 20 53 23", "none")
    ),
    "shrine": (
        circle(32, 17, 9) + path("M32 3 V7 M32 27 V33 M13 17 H18 M46 17 H51 M18 4 L22 8 M42 26 L46 30 M46 4 L42 8 M18 30 L22 26", "none")
        + path("M25 35 H39 V47 H47 V57 H17 V47 H25 Z", SHADE)
    ),
    "dark_altar": (
        path("M11 7 Q15 26 29 25 L32 15 L35 25 Q49 26 53 7 Q64 38 39 40 L44 48 H52 V58 H12 V48 H20 L25 40 Q1 38 11 7 Z", SHADE)
        + path("M26 47 H38", "none")
    ),
    "lair": (
        path("M13 51 Q9 44 17 37 L24 30 Q32 24 39 31 L48 38 Q57 48 49 54 Q43 59 33 54 Q21 61 13 51 Z")
        + path("M8 16 Q13 6 19 15 Q24 29 16 30 Q7 30 8 16 Z")
        + path("M27 9 Q33 1 38 11 Q43 25 34 26 Q26 25 27 9 Z")
        + path("M47 18 Q56 8 58 20 Q60 33 51 33 Q43 30 47 18 Z")
    ),
    "trial": (
        path("M9 5 L24 12 L48 43 L42 49 L17 18 Z")
        + path("M55 5 L40 12 L16 43 L22 49 L47 18 Z")
        + path("M8 39 L26 55 M56 39 L38 55 M17 47 L9 57 M47 47 L55 57", "none", INK, 5)
    ),
    "cache": (
        path("M8 30 Q8 12 23 12 H41 Q56 12 56 30 V55 H8 Z", SHADE)
        + path("M8 31 H56 M20 16 V55 M44 16 V55", "none")
        + path("M27 28 H37 V41 H27 Z")
    ),
    "locked_gate": (
        path("M7 57 V26 Q7 4 32 4 Q57 4 57 26 V57 H48 V27 Q48 14 32 14 Q16 14 16 27 V57 Z", SHADE)
        + path("M25 23 V56 M39 23 V56 M17 43 H47", "none")
        + path("M25 34 Q25 25 32 25 Q39 25 39 34 V40 H25 Z")
    ),
    "valve": (
        path("M27 40 H37 V56 H27 Z", SHADE)
        + circle(32, 26, 21)
        + path("M32 6 V46 M12 26 H52", "none", INK, 6)
        + circle(32, 26, 7, SHADE)
    ),
    "abyss_gate": (
        path("M9 56 L5 33 L17 8 L25 18 L33 4 L41 18 L51 10 L59 34 L54 56 Z", SHADE)
        + path("M19 50 L17 34 L24 23 L32 17 L41 27 L46 37 L43 50 Z", INK, INK)
    ),
    "crimson_portal": (
        path("M22 7 L18 16 L9 18 L14 26 L6 33 L15 38 L12 49 L23 48 L28 59 L35 51 L46 56 L47 45 L58 39 L51 31 L57 20 L45 20 L42 8 L33 15 Z", SHADE)
        + path("M32 21 Q47 31 32 45 Q18 31 32 21 Z", INK, INK)
    ),
    "memory_stone": (
        path("M19 58 L14 22 L25 5 L43 10 L50 58 Z", SHADE)
        + path("M25 44 Q42 46 40 32 Q38 22 28 26 Q21 32 30 36", "none", PAPER, 5)
    ),
    "ebb_stones": (
        path("M7 38 L9 14 L21 9 L25 38 Z", SHADE)
        + path("M27 34 L30 5 L42 7 L46 34 Z")
        + path("M47 38 L49 18 L57 21 L59 38 Z", SHADE)
        + path("M5 46 Q14 39 23 46 Q32 53 41 46 Q50 39 59 46 M5 57 Q14 50 23 57 Q32 64 41 57 Q50 50 59 57", "none", INK, 5)
    ),
    "ambush": (
        path("M7 54 L14 37 L23 44 L25 26 L37 40 L48 30 L47 46 L59 44 L55 57 Z", SHADE)
        + path("M25 35 L40 8 L55 4 L51 19 L33 41 Z")
        + path("M19 32 L36 43 M26 40 L18 51", "none", INK, 5)
    ),
}

CAMP = {
    "short_rest": (
        path("M8 26 Q8 19 16 19 H49 Q58 19 58 27 V51 H7 Z", "#aea6ce")
        + path("M8 32 H57 M22 20 V50", "none") + path("M10 9 Q18 5 24 10 V20 H10 Z")
    ),
    "brew": MAP["herbalist"],
    "cook": (
        path("M13 28 H51 L47 52 Q32 60 17 52 Z", "#c09866")
        + path("M6 30 H13 M51 30 H58 M11 24 H53", "none", INK, 5)
        + path("M24 17 Q17 11 25 5 M40 18 Q33 12 41 6", "none", SHADE, 4)
    ),
    "forge": MAP["armorer"],
    "enchant": (
        path("M14 56 L44 10 L55 5 L53 20 L22 61 Z", "#a9c9df")
        + path("M8 43 L28 56", "none", INK, 5)
        + path("M14 5 L17 13 L25 17 L17 21 L14 29 L11 21 L3 17 L11 13 Z", "#b3a0da")
    ),
    "card_enchant": (
        path("M10 10 L45 5 L54 53 L18 59 Z", "#b3a0da")
        + path("M30 19 L35 27 L44 30 L36 35 L34 45 L29 37 L20 34 L27 28 Z")
    ),
    "meditate": (
        circle(32, 12, 7, "#a9c9df")
        + path("M23 25 Q32 20 41 25 L45 40 L57 48 L48 57 L32 49 L16 57 L7 48 L19 40 Z", "#a9c9df")
        + path("M23 32 L26 43 H38 L41 32", "none")
    ),
    "pray": (
        path("M9 50 L22 28 L27 26 L32 12 L37 26 L42 28 L55 50 L43 58 L32 45 L21 58 Z")
        + path("M32 14 V45 M20 34 L27 39 M44 34 L37 39", "none")
        + circle(51, 11, 5, "#d9b46a")
    ),
    "train": (
        path("M31 9 H39 V57 H31 Z", "#b58a58")
        + path("M15 22 L28 16 H42 L55 22 L50 36 L41 32 L44 47 H25 L28 32 L19 36 Z")
        + path("M5 5 L10 9 L22 26 M9 27 L19 18", "none", INK, 5)
    ),
    "chest": MAP["cache"],
    "tavern": MAP["tavern"],
    "form_rites": (
        path("M12 44 Q3 22 23 8 Q38 1 51 16 Q61 34 45 48 L46 57 H18 V50 Z", "#b3a0da")
        + path("M32 12 Q18 29 32 45 Q46 28 32 12 Z", INK)
        + path("M32 16 V41", "none", PAPER, 4)
    ),
    "beast_rites": (
        path("M8 57 L12 35 L21 10 L25 32 L33 5 L37 31 L49 9 L49 37 L57 28 L54 50 L42 59 Z", "#b58a58")
        + path("M21 39 L19 51 M33 36 L32 52 M45 40 L43 52", "none")
    ),
    "warm": (
        path("M31 5 Q47 18 39 28 Q49 25 50 20 Q66 47 44 56 Q14 65 10 42 Q8 31 19 20 Q17 34 26 32 Q18 20 31 5 Z", "#db9b60")
        + path("M29 53 Q21 44 33 34 Q44 45 37 53 Z")
    ),
}


def svg(body, size, title):
    return (f'<svg xmlns="http://www.w3.org/2000/svg" width="{size}" height="{size}" viewBox="0 0 64 64">\n'
            f'  <title>{title}</title>\n'
            '  <g stroke-linejoin="round" stroke-linecap="round">\n'
            f'    {body}\n  </g>\n</svg>\n')


def sources():
    for key, body in sorted(MAP.items()):
        relative = Path(f"map/icons/{key}")
        yield relative, svg(body, 64, key), 64
    for key, body in sorted(CAMP.items()):
        relative = Path(f"ui/rest_site/option_slaytherequiem_{key}")
        yield relative, svg(body, 192, key), 192


def outline(image):
    alpha = image.convert("RGBA").getchannel("A").filter(ImageFilter.MaxFilter(7))
    result = Image.new("RGBA", image.size, "white")
    result.putalpha(alpha)
    return result


def digest(data):
    return hashlib.sha256(data).hexdigest()


def render_to(directory):
    pairs = []
    for rel, _, _ in sources():
        pairs += [str(ROOT / "art" / rel.with_suffix(".svg")), str(directory / rel.with_suffix(".png"))]
    subprocess.run(["node", str(ROOT / "tools/art/render_svg.mjs"), *pairs], check=True, env=os.environ.copy())


def write():
    for rel, text, _ in sources():
        target = ROOT / "art" / rel.with_suffix(".svg")
        target.parent.mkdir(parents=True, exist_ok=True)
        target.write_text(text, encoding="utf-8")
    with tempfile.TemporaryDirectory() as tmp:
        render_to(Path(tmp))
        rows = []
        for rel, text, size in sources():
            target = RES / rel.with_suffix(".png")
            target.parent.mkdir(parents=True, exist_ok=True)
            target.write_bytes((Path(tmp) / rel.with_suffix(".png")).read_bytes())
            row = {"source": str(Path("art") / rel.with_suffix(".svg")), "source_sha256": digest(text.encode()),
                   "runtime": str(target.relative_to(ROOT)), "runtime_sha256": digest(target.read_bytes()), "size": size}
            if str(rel).startswith("map/"):
                mask = target.with_name(target.stem + "_outline.png")
                outline(Image.open(target)).save(mask, optimize=True)
                row.update(outline=str(mask.relative_to(ROOT)), outline_sha256=digest(mask.read_bytes()))
            rows.append(row)
    RECEIPT.write_text(json.dumps({
        "schema": 1, "producer": "tools/art/readable_icons.py", "renderer": "@resvg/resvg-js 2.6.2",
        "provenance": "Original project-specific SVG geometry authored in this change; no downloaded or game artwork.",
        "rights": "Not copied from third-party assets. Repository artwork licensing must be settled before public release; this receipt is not a new license grant.",
        "acceptance": "source-and-native-size-preview only; final in-game visual/input acceptance pending",
        "assets": rows,
    }, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    print(f"readable icons: wrote {len(MAP)} map + {len(CAMP)} camp + {len(MAP)} outline")


def check(verify_render=False):
    errors = []
    receipt = json.loads(RECEIPT.read_text(encoding="utf-8")) if RECEIPT.exists() else {"assets": []}
    rows = {r["source"]: r for r in receipt["assets"]}
    expected = {str(Path("art") / rel.with_suffix(".svg")) for rel, _, _ in sources()}
    if set(rows) != expected:
        errors.append("receipt coverage does not match the authored icons")
    for rel, text, size in sources():
        src = ROOT / "art" / rel.with_suffix(".svg")
        target = RES / rel.with_suffix(".png")
        if not src.exists() or src.read_text(encoding="utf-8") != text:
            errors.append(f"{src.relative_to(ROOT)}: stale SVG")
        if not target.exists():
            errors.append(f"{target.relative_to(ROOT)}: missing runtime")
            continue
        row = rows.get(str(src.relative_to(ROOT)), {})
        if row.get("source_sha256") != digest(text.encode()) or row.get("runtime_sha256") != digest(target.read_bytes()):
            errors.append(f"{target.relative_to(ROOT)}: receipt/source mismatch")
        with Image.open(target) as raw:
            image = raw.convert("RGBA")
        if image.size != (size, size) or image.getchannel("A").getextrema() != (0, 255):
            errors.append(f"{target.relative_to(ROOT)}: size/alpha")
        if str(rel).startswith("map/"):
            mask = target.with_name(target.stem + "_outline.png")
            if not mask.exists() or row.get("outline_sha256") != digest(mask.read_bytes()):
                errors.append(f"{mask.relative_to(ROOT)}: absent/stale outline")
            elif ImageChops.difference(Image.open(mask).convert("RGBA"), outline(image)).getbbox(alpha_only=False):
                errors.append(f"{mask.relative_to(ROOT)}: outline is not an alpha dilation")
    if verify_render and not errors:
        with tempfile.TemporaryDirectory() as tmp:
            render_to(Path(tmp))
            for rel, _, _ in sources():
                original = Image.open(RES / rel.with_suffix(".png")).convert("RGBA")
                actual = Image.open(Path(tmp) / rel.with_suffix(".png")).convert("RGBA")
                if ImageChops.difference(original, actual).getbbox(alpha_only=False):
                    errors.append(f"{rel}: resvg pixel mismatch")
    for error in errors:
        print("ERROR", error)
    print(f"readable icons: {len(MAP)} map / {len(CAMP)} camp / {len(MAP)} alpha outlines; {len(errors)} errors")
    return int(bool(errors))


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--render", action="store_true")
    parser.add_argument("--check", action="store_true")
    parser.add_argument("--verify-render", action="store_true")
    args = parser.parse_args()
    if args.render:
        write()
    return check(args.verify_render)


if __name__ == "__main__":
    raise SystemExit(main())
