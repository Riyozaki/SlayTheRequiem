#!/usr/bin/env python3
"""Значки боссов на карте из альфы спрайта: images/monsters/{key}.png → images/map/bosses/{key}.png + _outline.png.

Игра (NBossMapPoint без Spine) красит значок цветом пути акта, а контур — цветом фона карты, поэтому оба файла —
белые силуэты на прозрачном фоне одного размера: значок = силуэт тела, контур = тот же силуэт, расширенный на OUTLINE px.
Детали (глаза, рёбра, руны) вырезаются из силуэта по тёмным пикселям спрайта, чтобы значок читался.

    python3 tools/art/monsters/build_boss_icons.py [--check]
"""
import sys
from pathlib import Path

import numpy as np
from PIL import Image, ImageFilter

ROOT = Path(__file__).resolve().parents[3]
SRC = ROOT / "mod/SlayTheRequiem/images/monsters"
DST = ROOT / "mod/SlayTheRequiem/images/map/bosses"
BOSSES = ["king_under_barrow", "bone_frost_wyrm", "barrow_wailer",
          "clockwork_colossus", "automaton_architect", "boiler_wyrm",
          "crimson_countess", "eclipse_warden", "chalice_hierophant",
          "astrolabe_keeper", "ley_wyrm", "archmage_simulacrum"]
SIZE, PAD, OUTLINE = 256, 22, 9


def _main_parts(mask, keep=0.02):
    """Связные области маски площадью ≥ keep от крупнейшей (4-связность, без scipy)."""
    h, w = mask.shape
    label = np.zeros((h, w), np.int32)
    areas = [0]
    flat = mask.ravel()
    lab = label.ravel()
    for start in np.flatnonzero(flat):
        if lab[start]:
            continue
        n = len(areas)
        lab[start] = n
        stack, area = [start], 0
        while stack:
            i = stack.pop()
            area += 1
            y, x = divmod(i, w)
            for j, ok in ((i - 1, x > 0), (i + 1, x < w - 1), (i - w, y > 0), (i + w, y < h - 1)):
                if ok and flat[j] and not lab[j]:
                    lab[j] = n
                    stack.append(j)
        areas.append(area)
    big = max(areas)
    good = np.array([a >= keep * big and i > 0 for i, a in enumerate(areas)])
    return good[label]


def build(key):
    im = Image.open(SRC / f"{key}.png").convert("RGBA")
    a = np.asarray(im)[..., 3].astype(np.float32) / 255
    rgb = np.asarray(im)[..., :3].astype(np.float32)
    lum = (0.3 * rgb[..., 0] + 0.59 * rgb[..., 1] + 0.11 * rgb[..., 2]) / 255
    body = _main_parts(a > 0.3)                         # тени, туман, свечение и звёзды не входят
    ys, xs = np.nonzero(body)
    box = (xs.min(), ys.min(), xs.max() + 1, ys.max() + 1)
    sil = Image.fromarray((body * 255).astype(np.uint8)).crop(box)
    dark = Image.fromarray(((body & (lum < 0.13)) * 255).astype(np.uint8))
    broad = dark.filter(ImageFilter.MinFilter(13)).filter(ImageFilter.MaxFilter(17))  # тёмные заливки (мантии)
    ink = Image.fromarray(np.where(np.asarray(broad) > 0, 0, np.asarray(dark)).astype(np.uint8)).crop(box)
    w, h = sil.size
    k = (SIZE - 2 * PAD) / max(w, h)
    nw, nh = max(1, round(w * k)), max(1, round(h * k))
    sil, ink = sil.resize((nw, nh), Image.LANCZOS), ink.resize((nw, nh), Image.LANCZOS)
    ink = ink.filter(ImageFilter.MinFilter(3))         # тонкие контуры исчезают, крупные тёмные детали остаются
    canvas = lambda: Image.new("L", (SIZE, SIZE), 0)
    off = ((SIZE - nw) // 2, SIZE - PAD - nh)           # прижат к низу, как фигура на постаменте
    s, d = canvas(), canvas()
    s.paste(sil, off)
    d.paste(ink, off)
    icon = np.clip(np.asarray(s, np.float32) - np.asarray(d, np.float32) * 0.85, 0, 255).astype(np.uint8)
    outline = s.filter(ImageFilter.MaxFilter(2 * OUTLINE + 1)).filter(ImageFilter.GaussianBlur(1.2))
    white = lambda alpha: Image.merge("RGBA", (*(Image.new("L", (SIZE, SIZE), 255),) * 3, alpha))
    return white(Image.fromarray(icon)), white(outline)


def main(argv):
    check = "--check" in argv
    bad = []
    DST.mkdir(parents=True, exist_ok=True)
    for key in BOSSES:
        icon, outline = build(key)
        for img, name in ((icon, f"{key}.png"), (outline, f"{key}_outline.png")):
            path = DST / name
            if check:
                # средняя разница, а не точное совпадение: ресэмплинг/размытие слегка плавают между версиями Pillow
                if not path.exists() or Image.open(path).size != img.size or np.abs(
                        np.asarray(Image.open(path).convert("RGBA"), np.float32) - np.asarray(img, np.float32)).mean() > 1.0:
                    bad.append(name)
            else:
                img.save(path, optimize=True)
    if bad:
        print("устарели значки боссов:", ", ".join(bad), "— python3 tools/art/monsters/build_boss_icons.py")
        return 1
    print(f"значки боссов: {len(BOSSES)} × 2 {'OK' if check else '→ ' + str(DST.relative_to(ROOT))}")
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
