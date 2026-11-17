"""Перерисовка значков карты в «чернильный» стиль ванильной карты StS2 (плейтест 2026-11: «элементы карты
низкого качества, как клипарт»).

Плоские цветные значки (images/map/icons/*.png) переводятся в монохром тушью по пергаменту:
  * тёмные контуры → густая тушь;
  * средние тона → диагональная штриховка (чем темнее заливка, тем гуще штрих);
  * светлые заливки → светлый пергамент с лёгкой «подложкой»;
  * края слегка «дрожат» (детерминированный шум), чтобы линия выглядела рисованной.
Исходники лежат в images/map/icons_src (сюда скрипт копирует их при первом запуске); повторный запуск
пересобирает результат из исходников, поэтому идемпотентен.

    python tools/assets/ink_map_icons.py            # пересобрать
    python tools/assets/ink_map_icons.py --sheet out.png   # плюс лист для просмотра
"""
from __future__ import annotations

import argparse
import shutil
from pathlib import Path

from PIL import Image, ImageFilter

ROOT = Path(__file__).resolve().parents[2]
ICONS = ROOT / "mod/SlayTheRequiem/images/map/icons"
SRC = ROOT / "mod/SlayTheRequiem/images/map/icons_src"
SCALE = 4  # рисуем в 4× и уменьшаем — штрих получается мягким

INK = (43, 30, 22)
PAPER = (236, 224, 196)


def noise(x: int, y: int, seed: int) -> float:
    h = (x * 374761393 + y * 668265263 + seed * 2246822519) & 0xFFFFFFFF
    h = ((h ^ (h >> 13)) * 1274126177) & 0xFFFFFFFF
    return ((h ^ (h >> 16)) & 0xFFFF) / 0xFFFF


def ink(src: Image.Image, seed: int) -> Image.Image:
    src = src.convert("RGBA")
    big = src.resize((src.width * SCALE, src.height * SCALE), Image.LANCZOS)
    # «дрожь» края: немного размытая альфа + шум как порог
    alpha = big.getchannel("A").filter(ImageFilter.GaussianBlur(SCALE * 0.6))
    out = Image.new("RGBA", big.size, (0, 0, 0, 0))
    px, al, op = big.load(), alpha.load(), out.load()
    w, h = big.size
    for y in range(h):
        for x in range(w):
            a = al[x, y] / 255
            if a < 0.35 + 0.3 * (noise(x // 3, y // 3, seed) - 0.5):
                continue
            r, g, b, _ = px[x, y]
            lum = (0.299 * r + 0.587 * g + 0.114 * b) / 255
            sat = max(r, g, b) - min(r, g, b)
            if lum < 0.28:
                op[x, y] = (*INK, 255)
                continue
            # насыщенные цвета (красный, синий) — штриховкой, светлые — бумагой
            tone = 1 - lum + (0.15 if sat > 90 else 0)
            period = 7 * SCALE // 2
            if tone > 0.55:
                width = period * 0.62
            elif tone > 0.35:
                width = period * 0.38
            elif tone > 0.18:
                width = period * 0.16
            else:
                width = 0
            d = (x + y) % period
            if tone > 0.6:  # густая тень — перекрёстная штриховка
                d = min(d, (x - y) % period)
            op[x, y] = (*INK, 235) if d < width else (*PAPER, 255)
    # тонкая тень-«растекание» туши вокруг значка
    shadow = out.getchannel("A").filter(ImageFilter.MaxFilter(SCALE * 2 + 1)).filter(ImageFilter.GaussianBlur(SCALE))
    base = Image.new("RGBA", big.size, (*INK, 0))
    base.putalpha(shadow.point(lambda v: int(v * 0.35)))
    base.alpha_composite(out)
    return base.resize(src.size, Image.LANCZOS)


def main() -> None:
    ap = argparse.ArgumentParser()
    ap.add_argument("--sheet")
    args = ap.parse_args()
    SRC.mkdir(exist_ok=True)
    for f in sorted(ICONS.glob("*.png")):
        if not (SRC / f.name).exists():
            shutil.copy2(f, SRC / f.name)
    done = []
    for i, f in enumerate(sorted(SRC.glob("*.png"))):
        img = ink(Image.open(f), seed=i + 1)
        img.save(ICONS / f.name)
        done.append(img)
    print(f"значков перерисовано: {len(done)}")
    if args.sheet:
        cols = 8
        sheet = Image.new("RGBA", (cols * 140, ((len(done) + cols - 1) // cols) * 140), (214, 196, 160, 255))
        for i, img in enumerate(done):
            sheet.alpha_composite(img.resize((128, 128), Image.LANCZOS), ((i % cols) * 140 + 6, (i // cols) * 140 + 6))
        sheet.save(args.sheet)


if __name__ == "__main__":
    main()
