"""Утолщение спрайтов куклы снаряжения (плейтест 2026-11: «оружие на герое — почти невидимые тонкие штрихи»).

Для каждого оружия ui/doll/<kind>.png (основа) и <kind>_metal.png (металл, его красит код по материалу):
  * тонкие детали утолщаются на ~1 px (MaxFilter по каналам — цвета плоские, так что не «плывут»);
  * основа получает тёмный контур толщиной ~4 px по объединению основы и металла — силуэт читается
    на любом фоне боя, а металл ложится поверх и тонируется как прежде.
Исходники хранятся в images/ui/doll_src (копируются при первом запуске), повтор идемпотентен.

    python tools/assets/bold_doll.py [--sheet out.png]
"""
from __future__ import annotations

import argparse
import shutil
from pathlib import Path

from PIL import Image, ImageChops, ImageFilter

ROOT = Path(__file__).resolve().parents[2]
DOLL = ROOT / "mod/SlayTheRequiem/images/ui/doll"
SRC = ROOT / "mod/SlayTheRequiem/images/ui/doll_src"
OUTLINE = (24, 17, 12)


def fatten(img: Image.Image) -> Image.Image:
    r, g, b, a = img.split()
    a2 = a.filter(ImageFilter.MaxFilter(3))
    # цвет для новых пикселей — из соседей (MaxFilter по RGB, взвешенный по альфе исходника)
    rgb = Image.merge("RGB", (r, g, b))
    premult = Image.composite(rgb, Image.new("RGB", img.size, (0, 0, 0)), a.point(lambda v: 255 if v > 40 else 0))
    spread = premult.filter(ImageFilter.MaxFilter(3))
    rgb2 = Image.composite(rgb, spread, a.point(lambda v: 255 if v > 40 else 0))
    return Image.merge("RGBA", (*rgb2.split(), a2))


def outline(alpha: Image.Image, px: int) -> Image.Image:
    grown = alpha.point(lambda v: 255 if v > 30 else 0).filter(ImageFilter.MaxFilter(px * 2 + 1))
    grown = grown.filter(ImageFilter.GaussianBlur(0.8))
    layer = Image.new("RGBA", alpha.size, (*OUTLINE, 0))
    layer.putalpha(grown.point(lambda v: int(v * 0.92)))
    return layer


def main() -> None:
    ap = argparse.ArgumentParser()
    ap.add_argument("--sheet")
    args = ap.parse_args()
    SRC.mkdir(exist_ok=True)
    for f in DOLL.glob("*.png"):
        if not (SRC / f.name).exists():
            shutil.copy2(f, SRC / f.name)
    bases = sorted(f for f in SRC.glob("*.png") if not f.stem.endswith("_metal"))
    previews = []
    for base_path in bases:
        base = fatten(Image.open(base_path).convert("RGBA"))
        metal_path = SRC / f"{base_path.stem}_metal.png"
        metal = fatten(Image.open(metal_path).convert("RGBA")) if metal_path.exists() else None
        union = base.getchannel("A") if metal is None else ImageChops.lighter(base.getchannel("A"), metal.getchannel("A"))
        out = outline(union, 4)
        out.alpha_composite(base)
        out.save(DOLL / base_path.name)
        if metal is not None:
            metal.save(DOLL / metal_path.name)
            prev = out.copy()
            prev.alpha_composite(metal)
        else:
            prev = out
        previews.append(prev)
    print(f"спрайтов куклы: {len(bases)}")
    if args.sheet:
        sheet = Image.new("RGBA", (len(previews) * 150, 280), (70, 60, 55, 255))
        for i, p in enumerate(previews):
            sheet.alpha_composite(p, (i * 150 + 5, 10))
        sheet.save(args.sheet)


if __name__ == "__main__":
    main()
