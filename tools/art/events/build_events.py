#!/usr/bin/env python3
"""Картинки событий актов (M5 п. 3, art spec §3.5): SVG-генератор → art/events/*.svg → JPG в mod/SlayTheRequiem/images/events/.

Кадр 1720×807 (как ванильные портреты событий): фон акта (лёд/склепы, медь/пар, бархат/кровь, звёзды/стекло,
янтарь Улья, руины Славы) + свой мотив события по центру + передний план, виньетка и зерно. Стиль — как у сцен
«двух дверей» и спрайтов боссов: толстый тёмный контур, градиенты, контровой свет цвета акта. Случайность — только от
фиксированных зёрен, поэтому SVG воспроизводимы (CI сверяет art/events с генератором).

    python3 tools/art/events/build_events.py            # только SVG
    python3 tools/art/events/build_events.py --render   # + JPG (нужен resvg: npm i --prefix /tmp/r @resvg/resvg-js@2)
    python3 tools/art/events/build_events.py --check    # CI: SVG совпадают с генератором, JPG на месте
"""
from __future__ import annotations

import math
import os
import random
import subprocess
import sys
import tempfile

HERE = os.path.dirname(__file__)
sys.path.insert(0, os.path.join(HERE, "..", "monsters"))
sys.path.insert(0, os.path.join(HERE, "..", "scenes"))
from svgkit import INK, Svg, poly, smooth, star  # noqa: E402
from build_scenes import _gear, _tint  # noqa: E402

ROOT = os.path.abspath(os.path.join(HERE, "..", "..", ".."))
OUT_SVG = os.path.join(ROOT, "art", "events")
OUT_JPG = os.path.join(ROOT, "mod", "SlayTheRequiem", "images", "events")

W, H = 1720, 807
CX = W // 2
GY = 640  # линия пола/земли

# id события (snake от класса) → (акт, функция мотива)
REGISTRY: dict[str, tuple[str, callable]] = {}


def event(key, act):
    def wrap(fn):
        REGISTRY[key] = (act, fn)
        return fn
    return wrap


# ═══════════════════════════ общие примитивы ═══════════════════════════

def full(s: Svg, fill, extra=""):
    s.path(f"M0,0 H{W} V{H} H0 Z", fill, stroke=None, extra=extra)


def glow_at(s: Svg, x, y, r, color, opacity=0.5, sd=None):
    s.circle(x, y, r, color, extra=f'opacity="{opacity}" filter="{s.blur(sd or r * 0.45)}"')


def ridge(rnd, y, amp, step=60, x0=-40, x1=W + 40):
    pts, x = [], x0
    while x <= x1:
        pts.append((x, y - rnd.uniform(0, amp)))
        x += step * rnd.uniform(0.6, 1.3)
    pts.append((x1, y - rnd.uniform(0, amp)))
    return pts


def below(pts, bottom=H):
    return poly(pts + [(pts[-1][0], bottom), (pts[0][0], bottom)])


def specks(s: Svg, rnd, n, color, rmin, rmax, y0=0, y1=H, x0=0, x1=W, op=(0.4, 0.95), glow=0.0):
    f = f' filter="{s.glow(glow)}"' if glow else ""
    for _ in range(n):
        s.circle(round(rnd.uniform(x0, x1), 1), round(rnd.uniform(y0, y1), 1), round(rnd.uniform(rmin, rmax), 2), color,
                 extra=f'opacity="{rnd.uniform(*op):.2f}"{f}')


def figure(s: Svg, x, gy, h, robe=("#5a5a6a", "#1a1a24"), hood=True, face="#d8b89a", eyes=None, lean=0.0, wide=1.0, rim=None):
    """Фигура в плаще/мантии: силуэт трапецией, капюшон или голова, светящиеся глаза по желанию."""
    w = h * 0.38 * wide
    hr = h * 0.105
    top = gy - h
    hx = x + lean
    sh = top + hr * 2.1
    body = [(x - w * 0.55, gy), (x - w * 0.42, sh + h * 0.35), (hx - w * 0.3, sh + h * 0.06), (hx - w * 0.16, sh),
            (hx + w * 0.16, sh), (hx + w * 0.3, sh + h * 0.06), (x + w * 0.42, sh + h * 0.35), (x + w * 0.55, gy)]
    s.path(smooth(body, k=0.6), s.lin([(0, robe[0]), (1, robe[1])], 0, 0, 0.4, 1), INK, max(3, h * 0.012))
    s.path(f"M{x - w * 0.08:.1f},{sh + h * 0.12:.1f} Q{x:.1f},{gy - h * 0.3:.1f} {x - w * 0.12:.1f},{gy:.1f}", "none",
           stroke=_tint(robe[1], -0.3), sw=max(2, h * 0.008))
    if rim:
        s.path(smooth(body, k=0.6), "none", stroke=rim, sw=max(2, h * 0.01), extra=f'opacity="0.55" filter="{s.blur(3)}"')
    if hood:
        hood_pts = [(hx - hr * 1.35, sh + hr * 0.4), (hx - hr * 1.2, top + hr * 0.7), (hx, top - hr * 0.5),
                    (hx + hr * 1.2, top + hr * 0.7), (hx + hr * 1.35, sh + hr * 0.4)]
        s.path(smooth(hood_pts, k=0.8), s.lin([(0, _tint(robe[0], 0.1)), (1, robe[1])]), INK, max(3, h * 0.012))
        s.ellipse(hx, top + hr * 1.15, hr * 0.72, hr * 0.85, "#07050a")
    else:
        s.circle(hx, top + hr, hr, face, INK, max(3, h * 0.012))
    if eyes:
        for dx in (-hr * 0.32, hr * 0.32):
            s.circle(hx + dx, top + hr * 1.05, hr * 0.13, eyes, extra=f'filter="{s.glow(hr * 0.15)}"')
    return hx, top, hr, w


def finish(s: Svg, tint, seed):
    full(s, s.rad([(0.5, "#000", 0), (1, "#000", 0.62)], cx=0.5, cy=0.5, r=0.72))
    full(s, tint, 'opacity="0.05"')
    full(s, "#808080", f'opacity="0.45" filter="{s.grain(0.3, 0.8, seed)}" style="mix-blend-mode:overlay"')


# ═══════════════════════════ фоны актов ═══════════════════════════

def bg_barrows(s: Svg, rnd):
    full(s, s.lin([(0, "#040c18"), (0.5, "#14304a"), (0.8, "#3d6a86"), (1, "#8fb0c4")]))
    for i, (col, y, a) in enumerate((("#5df2c6", 150, 0.45), ("#62b6ff", 200, 0.35), ("#a98bff", 120, 0.2))):
        pts = [(-60, y + 60), (400, y - 40 + i * 20), (860, y + 30), (1300, y - 60), (W + 60, y + 20)]
        s.path(smooth(pts, closed=False), stroke=col, sw=80 - i * 14, extra=f'opacity="{a}" filter="{s.blur(26)}"')
    specks(s, rnd, 160, "#ffffff", 0.6, 1.8, 0, 380)
    for y, amp, col in ((470, 200, "#2c4a64"), (540, 120, "#1d3348"), (600, 70, "#13243a")):
        pts = ridge(rnd, y, amp, 70)
        s.path(below(pts), col, stroke=None)
        for x, yy in pts:
            if yy < y - amp * 0.6:
                s.path(poly([(x - 30, yy + 36), (x, yy), (x + 26, yy + 32), (x + 6, yy + 22)]), "#cfe6f5", stroke=None,
                       extra='opacity="0.8"')
    s.ellipse(CX, 600, 1100, 60, "#bcd8ea", extra=f'opacity="0.3" filter="{s.blur(22)}"')
    ground = [(-40, GY - 10), (400, GY - 30), (900, GY - 5), (1300, GY - 28), (W + 40, GY - 8)]
    s.path(smooth(ground, closed=False) + f" L{W + 40},{H} L-40,{H} Z", s.lin([(0, "#cfe2ee"), (1, "#7e9cb2")]), stroke=None)
    for x, h, lean in ((140, 150, -6), (260, 100, 4), (1480, 160, 3), (1600, 110, -4)):
        s.path(poly([(x - 22, GY), (x - 16 + lean, GY - h), (x + 16 + lean, GY - h - 8), (x + 22, GY)]),
               s.lin([(0, "#7a8c9a"), (1, "#2a3640")], 0, 0, 1, 0), INK, 5)
        for k in range(3):
            yy = GY - h * 0.3 - k * 28
            s.path(f"M{x - 7 + lean * 0.5},{yy} l7,-12 l7,12 M{x + lean * 0.5},{yy - 12} v-10", "none", stroke="#7ff2ff", sw=3,
                   extra=f'filter="{s.glow(4)}"')


def fg_barrows(s: Svg, rnd):
    specks(s, rnd, 260, "#ffffff", 1.0, 3.6, op=(0.45, 0.95))
    s.ellipse(CX, H - 20, 1200, 70, "#eef8ff", extra=f'opacity="0.35" filter="{s.blur(20)}"')


def bg_depths(s: Svg, rnd):
    full(s, s.lin([(0, "#0d0704"), (0.55, "#2a170b"), (0.85, "#5a3416"), (1, "#3a2210")]))
    for cx, cy, r, t, op in ((220, 200, 230, 20, 0.3), (1500, 260, 200, 18, 0.28), (860, 90, 150, 14, 0.22), (1180, 420, 110, 12, 0.2)):
        s.open(f'opacity="{op}"')
        _gear(s, cx, cy, r, t, "#6a4220", stroke="#1a0e05", sw=8, hole="#1a0e05", rot=rnd.uniform(0, 30))
        s.close()
    s.ellipse(CX, 330, 900, 160, "#ffcf8a", extra=f'opacity="0.14" filter="{s.blur(50)}"')
    for y, sw_, col in ((330, 40, "#8a5a2a"), (390, 24, "#b07a3a")):
        s.path(f"M-20,{y} H{W + 20}", "none", stroke=INK, sw=sw_ + 10)
        s.path(f"M-20,{y} H{W + 20}", "none", stroke=col, sw=sw_)
        s.path(f"M-20,{y - sw_ * 0.25} H{W + 20}", "none", stroke="#ffe0a8", sw=3, extra='opacity="0.5"')
        for x in range(30, W, 110):
            s.path(f"M{x},{y - sw_ / 2 - 5} v{sw_ + 10}", "none", stroke=INK, sw=10)
            s.path(f"M{x},{y - sw_ / 2 - 5} v{sw_ + 10}", "none", stroke="#d8a04e", sw=5)
    for x in (120, 1600):
        s.path(f"M{x},0 V{GY}", "none", stroke=INK, sw=44)
        s.path(f"M{x},0 V{GY}", "none", stroke="#7a4a20", sw=34)
        s.path(f"M{x - 8},0 V{GY}", "none", stroke="#c08a4a", sw=4, extra='opacity="0.6"')
    for x, y, r in ((300, 300, 70), (1420, 360, 90), (700, 250, 50)):
        s.circle(x, y, r, "#f0e0c8", extra=f'opacity="0.22" filter="{s.blur(r * 0.5)}"')
    s.path(f"M0,{GY} H{W} V{H} H0 Z", s.lin([(0, "#4a2e16"), (1, "#140a04")]), stroke=None)
    for i in range(9):
        x0 = CX + (i - 4) * 120
        s.path(f"M{x0},{GY} L{CX + (i - 4) * 300},{H}", "none", stroke="#0a0502", sw=3, extra='opacity="0.6"')
    for y in (GY + 40, GY + 100):
        s.path(f"M0,{y} H{W}", "none", stroke="#0a0502", sw=3, extra='opacity="0.5"')
    s.path(f"M0,{GY} H{W}", "none", stroke="#d8a04e", sw=3, extra='opacity="0.5"')


def fg_depths(s: Svg, rnd):
    for x, y, r in ((200, 700, 120), (1550, 690, 140)):
        s.circle(x, y, r, "#e8d8c0", extra=f'opacity="0.18" filter="{s.blur(r * 0.4)}"')
    specks(s, rnd, 70, "#ffb050", 1.0, 2.8, 200, H, op=(0.5, 1.0), glow=1.5)


def bg_court(s: Svg, rnd):
    full(s, s.lin([(0, "#14030a"), (0.6, "#3a0812"), (1, "#1a0408")]))
    for i, x in enumerate((260, 620, 1100, 1460)):
        s.path(f"M{x - 90},{GY - 60} V230 Q{x},90 {x + 90},230 V{GY - 60} Z", s.lin([(0, "#3a0c1a"), (1, "#120208")]), INK, 6)
        s.path(f"M{x - 70},{GY - 80} V240 Q{x},120 {x + 70},240 V{GY - 80} Z", s.lin([(0, "#5a1626"), (0.6, "#8a2a2a"), (1, "#2a0610")]),
               INK, 4)
        if i in (1, 2):
            s.circle(x + (30 if i == 1 else -30), 260, 46, s.rad([(0, "#ffd0b0"), (0.6, "#e04030"), (1, "#7a0a10")]),
                     extra=f'filter="{s.glow(8)}"')
        s.path(f"M{x},{GY - 80} V150 M{x - 70},360 H{x + 70}", "none", stroke=INK, sw=6)
    for side in (-1, 1):
        x = 0 if side < 0 else W
        pts = [(x, -10), (x + side * 230, 0), (x + side * 170, 200), (x + side * 120, 420), (x + side * 180, GY + 40), (x, GY + 40)]
        s.path(smooth(pts), s.lin([(0, "#a0142a"), (1, "#3a0410")], 0, 0, 1, 0), INK, 5)
        for k in (40, 90, 140):
            s.path(f"M{x + side * k},0 Q{x + side * (k + 30)},300 {x + side * (k - 10)},{GY + 30}", "none", stroke="#5a0614", sw=4)
        s.circle(x + side * 150, 300, 14, "#d4a640", INK, 3)
    for x in (560, 1160):
        glow_at(s, x, 120, 120, "#ffb84a", 0.25)
        for k in range(5):
            s.circle(x - 60 + k * 30, 140, 6, "#ffd88a", extra=f'filter="{s.glow(4)}"')
        s.path(f"M{x - 70},150 Q{x},190 {x + 70},150", "none", stroke="#d4a640", sw=4)
    s.path(f"M0,{GY} H{W} V{H} H0 Z", s.lin([(0, "#2a0a10"), (1, "#0a0204")]), stroke=None)
    for i in range(-8, 9):
        s.path(f"M{CX + i * 90},{GY} L{CX + i * 260},{H}", "none", stroke="#d4a640", sw=2, extra='opacity="0.25"')
    for y in (GY + 30, GY + 75, GY + 130):
        s.path(f"M0,{y} H{W}", "none", stroke="#d4a640", sw=2, extra='opacity="0.2"')


def fg_court(s: Svg, rnd):
    for _ in range(40):
        x, y = rnd.uniform(0, W), rnd.uniform(300, H)
        s.path(f"M{x:.1f},{y:.1f} q6,-10 12,0 q-6,7 -12,0", "#d0182e", stroke=None, extra=f'opacity="{rnd.uniform(0.5, 0.9):.2f}"')


def bg_tower(s: Svg, rnd):
    full(s, s.lin([(0, "#04040f"), (0.55, "#12164a"), (0.9, "#3a2a78"), (1, "#1a1640")]))
    for x, y, r, col, op in ((300, 200, 260, "#7a3aff", 0.3), (1400, 250, 240, "#2ab0ff", 0.26), (860, 120, 180, "#ff6ad8", 0.18)):
        s.circle(x, y, r, col, extra=f'opacity="{op}" filter="{s.blur(70)}"')
    specks(s, rnd, 260, "#ffffff", 0.5, 1.7, 0, GY)
    for x, y, r in ((220, 140, 9), (1500, 120, 8), (1180, 90, 6), (420, 380, 6), (1380, 420, 7)):
        s.path(star(x, y, r * 2, r * 0.5, 4), "#ffffff", stroke=None, extra=f'filter="{s.glow(4)}"')
    for pts, col in (([(-20, 560), (400, 420), (860, 380), (1300, 300), (W + 20, 160)], "#5ef0ff"),
                     ([(-20, 200), (420, 330), (860, 400), (1300, 520), (W + 20, 600)], "#c88aff")):
        s.path(smooth(pts, closed=False), "none", stroke=col, sw=24, extra=f'opacity="0.3" filter="{s.blur(12)}"')
        s.path(smooth(pts, closed=False), "none", stroke=col, sw=3.5, extra='opacity="0.85"')
    for x, y, r, rot in ((180, 480, 40, 20), (1560, 470, 50, -15), (380, 260, 26, 40), (1350, 210, 30, -30)):
        s.path(poly([(x, y - r), (x + r * 0.5, y), (x, y + r * 1.3), (x - r * 0.5, y)]), "#9ad8ff", INK, 3,
               extra=f'opacity="0.75" transform="rotate({rot} {x} {y})"')
    s.path(f"M0,{GY} H{W} V{H} H0 Z", s.lin([(0, "#2a2a5a"), (1, "#08081a")]), stroke=None)
    s.ellipse(CX, GY + 70, 640, 70, "none", "#9aa8ff", 3, extra='opacity="0.5"')
    s.ellipse(CX, GY + 70, 460, 48, "none", "#d4b45a", 2.5, extra='opacity="0.6"')
    for k in range(12):
        a = k * math.pi / 6
        s.path(star(CX + 550 * math.cos(a), GY + 70 + 60 * math.sin(a), 8, 3, 4), "#bff6ff", stroke=None, extra='opacity="0.8"')


def fg_tower(s: Svg, rnd):
    specks(s, rnd, 50, "#bff6ff", 1.0, 2.6, 200, H, op=(0.5, 1.0), glow=2)


def bg_hive(s: Svg, rnd):
    full(s, s.lin([(0, "#140804"), (0.5, "#4a2408"), (0.85, "#8a5212"), (1, "#2a1404")]))
    r = 56
    for row in range(-1, 10):
        for col in range(-1, 20):
            x = col * r * 1.5
            y = row * r * 1.732 + (r * 0.866 if col % 2 else 0)
            pts = [(x + r * 0.92 * math.cos(math.pi / 3 * k), y + r * 0.92 * math.sin(math.pi / 3 * k)) for k in range(6)]
            d = math.hypot((x - CX) / W, (y - 330) / H)
            fill = _tint("#c07a1a", -0.2 - d * 0.9)
            s.path(poly(pts), fill, "#1a0a02", 4, extra='opacity="0.85"')
            if rnd.random() < 0.18:
                s.path(poly(pts), "#ffb030", stroke=None, extra=f'opacity="0.35" filter="{s.blur(6)}"')
    for x in (200, 520, 1250, 1540):
        ln = rnd.randint(60, 160)
        s.path(f"M{x - 10},0 Q{x - 12},{ln * 0.6} {x},{ln} Q{x + 12},{ln * 0.6} {x + 10},0 Z", "#ffb030", INK, 3, extra='opacity="0.85"')
        s.circle(x, ln + 14, 9, "#ffb030", INK, 3)
    for pts in (([(80, 120), (160, 220), (130, 330), (220, 460)]), ([(1640, 90), (1560, 210), (1610, 320), (1520, 470)])):
        s.path(poly(pts, closed=False), "none", stroke="#c06aff", sw=18, extra=f'opacity="0.45" filter="{s.blur(8)}"')
        s.path(poly(pts, closed=False), "none", stroke="#f0d0ff", sw=3)
    s.path(f"M0,{GY} Q{CX},{GY - 40} {W},{GY} V{H} H0 Z", s.lin([(0, "#5a3008"), (1, "#140802")]), stroke=None)


def fg_hive(s: Svg, rnd):
    specks(s, rnd, 80, "#ffcf6a", 1.0, 3.0, 100, H, op=(0.4, 0.9), glow=1.5)
    specks(s, rnd, 20, "#d08aff", 1.5, 3.0, 100, H, op=(0.5, 1.0), glow=2)


def bg_glory(s: Svg, rnd):
    full(s, s.lin([(0, "#0a0614"), (0.45, "#2a1440"), (0.8, "#8a4a3a"), (1, "#d09a5a")]))
    for pts in (([(300, 0), (360, 90), (330, 160), (420, 260)]), ([(1300, 0), (1250, 110), (1330, 190), (1270, 300)]),
                ([(820, 0), (880, 60), (850, 130)])):
        s.path(poly(pts, closed=False), "none", stroke="#9a4aff", sw=30, extra=f'opacity="0.45" filter="{s.blur(14)}"')
        s.path(poly(pts, closed=False), "none", stroke="#f0e0ff", sw=4)
    specks(s, rnd, 80, "#ffe0c0", 0.6, 1.6, 0, 300)
    pts = ridge(rnd, 520, 60, 80)
    s.path(below(pts), "#2a1424", stroke=None)
    for x, h, broken in ((160, 420, 0.0), (420, 300, 0.4), (1300, 360, 0.3), (1560, 440, 0.0)):
        top = GY - h
        s.path(f"M{x - 34},{GY} V{top} H{x + 34} V{GY} Z", s.lin([(0, "#d0b080"), (1, "#5a4030")], 0, 0, 1, 0), INK, 5)
        for k in range(-2, 3):
            s.path(f"M{x + k * 12},{GY} V{top + 10}", "none", stroke="#3a2a20", sw=2, extra='opacity="0.6"')
        if broken:
            s.path(poly([(x - 36, top), (x - 10, top - 30), (x + 6, top - 8), (x + 36, top - 40), (x + 36, top)]), "#c0a070", INK, 4)
        else:
            s.path(f"M{x - 48},{top} H{x + 48} V{top - 22} H{x - 48} Z", "#e0c090", INK, 5)
    for i in range(5):
        y = GY + i * 34
        s.path(f"M{-20},{y} H{W + 20} V{y + 34} H-20 Z", s.lin([(0, _tint("#b09070", -i * 0.12)), (1, _tint("#5a4030", -i * 0.12))]),
               INK, 3)


def fg_glory(s: Svg, rnd):
    specks(s, rnd, 90, "#ffb070", 1.0, 3.0, 150, H, op=(0.4, 1.0), glow=1.5)
    specks(s, rnd, 25, "#c08aff", 1.2, 2.6, 100, H, op=(0.5, 1.0), glow=2)


ACTS = {
    "barrows": (bg_barrows, fg_barrows, "#9DB0C4"),
    "depths": (bg_depths, fg_depths, "#B89A63"),
    "court": (bg_court, fg_court, "#C0505A"),
    "tower": (bg_tower, fg_tower, "#8C9AC8"),
    "hive": (bg_hive, fg_hive, "#E0A040"),
    "glory": (bg_glory, fg_glory, "#B080E0"),
}


# ═══════════════════════════ 2а — Ледяные Курганы ═══════════════════════════

@event("frozen_pilgrim_event", "barrows")
def ev_frozen_pilgrim(s, rnd):
    for i, (dx, w, h) in enumerate(((0, 170, 70), (-10, 140, 60), (8, 110, 54), (-4, 80, 46), (4, 50, 36))):
        y = GY - sum(hh for _, _, hh in ((0, 0, 70), (0, 0, 60), (0, 0, 54), (0, 0, 46), (0, 0, 36))[:i])
        s.path(smooth([(CX + 120 + dx - w / 2, y), (CX + 120 + dx - w / 2 + 8, y - h + 8), (CX + 120 + dx, y - h),
                       (CX + 120 + dx + w / 2 - 6, y - h + 10), (CX + 120 + dx + w / 2, y)], k=0.7),
               s.lin([(0, "#8a9aa8"), (1, "#36444f")]), INK, 5)
        s.path(f"M{CX + 120 + dx - w / 2 + 10},{y - h + 12} Q{CX + 120 + dx},{y - h - 6} {CX + 120 + dx + w / 2 - 10},{y - h + 12}", "none",
               stroke="#eef8ff", sw=7, extra='opacity="0.85"')
    hx, top, hr, w = figure(s, CX - 40, GY, 250, robe=("#b8c8d4", "#4a5a68"), lean=50, wide=1.3, rim="#bfefff")
    s.path(f"M{hx - hr * 1.3},{top + hr * 0.3} Q{hx},{top - hr * 0.9} {hx + hr * 1.3},{top + hr * 0.3}", "none", stroke="#ffffff", sw=10,
           extra='opacity="0.9"')
    s.ellipse(CX - 110, GY - 70, 30, 24, s.lin([(0, "#8a5a2a"), (1, "#3a2410")]), INK, 4)
    s.path(f"M{CX - 128},{GY - 90} Q{CX - 110},{GY - 104} {CX - 92},{GY - 90}", "none", stroke=INK, sw=4)
    for dx, dy in ((-18, 4), (2, 10), (16, -2)):
        s.circle(CX - 110 + dx, GY - 50 + dy, 7, "#ffd25a", INK, 2, extra=f'filter="{s.glow(3)}"')
    s.ellipse(CX, GY + 6, 260, 26, "#ffffff", extra=f'opacity="0.6" filter="{s.blur(8)}"')


@event("barrow_seal_event", "barrows")
def ev_barrow_seal(s, rnd):
    s.ellipse(CX, GY + 10, 470, 260, s.lin([(0, "#4a6a80"), (1, "#14222e")]), INK, 6)
    s.path(f"M{CX - 400},{GY - 120} Q{CX},{GY - 300} {CX + 400},{GY - 120}", "none", stroke="#e8f6ff", sw=26, extra='opacity="0.85"')
    s.circle(CX, GY - 70, 190, s.lin([(0, "#7a8a96"), (1, "#2a343e")]), INK, 8)
    for r, n, col in ((170, 12, "#a8b8c4"), (128, 9, "#98a8b4"), (86, 6, "#889aa6")):
        s.circle(CX, GY - 70, r, "none", INK, 6)
        s.circle(CX, GY - 70, r - 6, "none", "#c8d8e4", 2, extra='opacity="0.5"')
        for k in range(n):
            a = 2 * math.pi * k / n + r
            x, y = CX + (r - 20) * math.cos(a), GY - 70 + (r - 20) * math.sin(a)
            s.path(f"M{x - 9},{y + 6} q9,-18 18,0 q-9,-4 -18,0 Z", col, INK, 2.5)
    s.circle(CX, GY - 70, 44, "#0a1420", INK, 6)
    s.path(f"M{CX - 18},{GY - 46} L{CX - 8},{GY - 100} L{CX},{GY - 50} L{CX + 8},{GY - 100} L{CX + 18},{GY - 46} Z", "#7ff2ff", stroke=None,
           extra=f'filter="{s.glow(8)}"')
    glow_at(s, CX, GY - 70, 70, "#7ff2ff", 0.4)


@event("frost_altar_event", "barrows")
def ev_frost_altar(s, rnd):
    glow_at(s, CX, GY - 160, 260, "#62c8ff", 0.35)
    s.path(f"M{CX - 210},{GY} L{CX - 180},{GY - 170} H{CX + 180} L{CX + 210},{GY} Z", s.lin([(0, "#6a7a88"), (1, "#26323c")]), INK, 6)
    s.path(f"M{CX - 230},{GY - 170} H{CX + 230} V{GY - 210} H{CX - 230} Z", s.lin([(0, "#8a9aa8"), (1, "#3a4652")]), INK, 6)
    for k in range(-2, 3):
        s.path(f"M{CX + k * 60 - 12},{GY - 120} l12,-20 l12,20 M{CX + k * 60},{GY - 140} v-14", "none", stroke="#7ff2ff", sw=4,
               extra=f'filter="{s.glow(5)}"')
    for x, h, w, lean in ((-260, 220, 50, -14), (-190, 300, 64, -6), (-110, 180, 40, 4), (150, 260, 56, 8), (230, 330, 70, 12),
                          (300, 170, 44, 18), (40, 120, 36, -4)):
        bx = CX + x
        s.path(poly([(bx - w / 2, GY - 160), (bx + lean, GY - 160 - h), (bx + w / 2, GY - 160)]),
               s.lin([(0, "#e8fbff"), (0.5, "#8fd8ff"), (1, "#2a7ab0")], 0, 0, 1, 0), INK, 4, extra='opacity="0.92"')
        s.path(f"M{bx - w * 0.15},{GY - 170} L{bx + lean * 0.8},{GY - 150 - h * 0.9}", "none", stroke="#ffffff", sw=3, extra='opacity="0.7"')
    s.path(f"M{CX - 200},{GY - 222} L{CX + 170},{GY - 232} L{CX + 176},{GY - 220} L{CX - 196},{GY - 210} Z", "#c8d8e8", INK, 4)
    s.path(f"M{CX + 170},{GY - 240} H{CX + 230} V{GY - 212} H{CX + 170} Z", "#5a3a20", INK, 4)
    s.path(f"M{CX + 160},{GY - 254} V{GY - 196}", "none", stroke="#d4a640", sw=10)
    s.path(f"M{CX + 160},{GY - 254} V{GY - 196}", "none", stroke=INK, sw=2)


@event("draugr_wake_event", "barrows")
def ev_draugr_wake(s, rnd):
    full(s, "#020810", 'opacity="0.35"')
    seats = [-420, -250, -80, 90, 260, 430]
    for i, dx in enumerate(seats):
        x = CX + dx
        if i == 3:
            s.path(f"M{x - 50},{GY - 140} V{GY - 330} Q{x},{GY - 370} {x + 50},{GY - 330} V{GY - 140}", s.lin([(0, "#4a3422"), (1, "#1e140c")]),
                   INK, 5)
            s.path(f"M{x - 20},{GY - 150} Q{x - 30},{GY - 210} {x + 14},{GY - 230} L{x + 22},{GY - 214} Q{x - 6},{GY - 196} {x + 4},{GY - 150} Z",
                   "#e8d8b0", INK, 4, extra=f'filter="{s.glow(4)}"')
            glow_at(s, x, GY - 190, 60, "#ffd88a", 0.5)
            continue
        hx, top, hr, w = figure(s, x, GY - 120, 210, robe=("#4a5a5a", "#141c1c"), hood=False, face="#7a8a80", eyes="#7ff2ff", lean=rnd.uniform(-6, 6))
        s.path(f"M{hx - hr},{top + hr * 0.4} L{hx - hr * 1.5},{top - hr * 0.5} M{hx + hr},{top + hr * 0.4} L{hx + hr * 1.5},{top - hr * 0.5}",
               "none", stroke="#c8c0a8", sw=5)
        side = -1 if i % 2 else 1
        hx2 = hx + side * w * 0.5
        s.path(f"M{hx2 - 8},{top + 30} Q{hx2 - 14},{top - 20} {hx2 + side * 26},{top - 40} L{hx2 + side * 30},{top - 28} "
               f"Q{hx2 + 2},{top - 12} {hx2 + 8},{top + 30} Z", "#c8b890", INK, 3)
    s.path(f"M{CX - 520},{GY - 140} H{CX + 520} L{CX + 600},{GY - 60} H{CX - 600} Z", s.lin([(0, "#5a4430"), (1, "#2a1e14")]), INK, 6)
    s.path(f"M{CX - 600},{GY - 60} H{CX + 600} V{GY - 36} H{CX - 600} Z", "#1e140c", INK, 5)
    for x in (CX - 560, CX + 560):
        s.path(f"M{x - 12},{GY - 36} V{GY} M{x + 12},{GY - 36} V{GY}", "none", stroke=INK, sw=10)
    for x in (CX - 330, CX + 0, CX + 350):
        s.path(f"M{x - 6},{GY - 140} V{GY - 180} H{x + 6} V{GY - 140} Z", "#e8e0c8", INK, 2)
        s.circle(x, GY - 192, 9, "#7ff2ff", extra=f'filter="{s.glow(6)}"')


@event("burial_urns_event", "barrows")
def ev_burial_urns(s, rnd):
    s.path(f"M{CX - 640},{GY} V120 H{CX + 640} V{GY} Z", s.lin([(0, "#3a4652"), (1, "#141c24")]), INK, 6)
    for row in range(3):
        y = 160 + row * 150
        for col in range(7):
            x = CX - 540 + col * 180
            s.path(f"M{x - 70},{y + 130} V{y + 40} Q{x},{y - 10} {x + 70},{y + 40} V{y + 130} Z", "#0a1016", INK, 4)
            if row == 2 and col == 3:
                continue
            h = 70 + rnd.randint(-8, 12)
            col_ = rnd.choice(("#8a5a3a", "#7a6a5a", "#6a4a3a"))
            s.path(smooth([(x - 18, y + 128), (x - 34, y + 128 - h * 0.45), (x - 16, y + 128 - h * 0.92), (x - 12, y + 128 - h),
                           (x + 12, y + 128 - h), (x + 16, y + 128 - h * 0.92), (x + 34, y + 128 - h * 0.45), (x + 18, y + 128)], k=0.7),
                   s.lin([(0, _tint(col_, 0.2)), (1, _tint(col_, -0.4))], 0, 0, 1, 0), INK, 3.5)
            s.path(f"M{x - 26},{y + 128 - h * 0.5} H{x + 26}", "none", stroke="#d8c8a0", sw=2, extra='opacity="0.5"')
    for dx, dy, r in ((-80, -10, 30), (60, -6, 26), (-20, 8, 22), (110, 4, 18)):
        s.path(poly([(CX + dx - r, GY + dy), (CX + dx - r * 0.3, GY + dy - r * 0.8), (CX + dx + r, GY + dy - r * 0.3), (CX + dx + r * 0.6, GY + dy + 6)]),
               "#8a5a3a", INK, 3)
    for _ in range(26):
        x, y = CX + rnd.uniform(-160, 160), GY + rnd.uniform(-10, 40)
        s.ellipse(round(x, 1), round(y, 1), 9, 5, "#ffd25a", INK, 1.5)
    glow_at(s, CX, GY, 160, "#ffd25a", 0.3)
    s.ellipse(CX + 220, GY + 10, 70, 20, "#d0d0c8", extra=f'opacity="0.5" filter="{s.blur(6)}"')


@event("snowbound_sled_event", "barrows")
def ev_snowbound_sled(s, rnd):
    s.open(f'transform="rotate(-14 {CX} {GY - 60})"')
    s.path(f"M{CX - 300},{GY - 40} H{CX + 260} L{CX + 300},{GY - 140} H{CX - 260} Z", s.lin([(0, "#8a5a32"), (1, "#3a2410")]), INK, 6)
    for k in range(5):
        s.path(f"M{CX - 260 + k * 120},{GY - 138} L{CX - 290 + k * 120},{GY - 42}", "none", stroke="#2a1a0c", sw=3)
    s.path(f"M{CX - 340},{GY - 10} H{CX + 280} Q{CX + 360},{GY - 10} {CX + 360},{GY - 80}", "none", stroke=INK, sw=16)
    s.path(f"M{CX - 340},{GY - 10} H{CX + 280} Q{CX + 360},{GY - 10} {CX + 360},{GY - 80}", "none", stroke="#a0a8b0", sw=8)
    s.close()
    for x, y, w, h in ((CX - 380, GY - 60, 110, 80), (CX + 330, GY - 40, 90, 70), (CX - 250, GY - 20, 70, 50)):
        s.path(f"M{x - w / 2},{y + h / 2} V{y - h / 2} H{x + w / 2} V{y + h / 2} Z", s.lin([(0, "#9a6a3a"), (1, "#4a2e14")]), INK, 5)
        s.path(f"M{x - w / 2},{y - h / 2} L{x + w / 2},{y + h / 2} M{x + w / 2},{y - h / 2} L{x - w / 2},{y + h / 2}", "none", stroke="#2a1a0c", sw=3)
    s.path(f"M{CX + 120},{GY - 210} V{GY - 260}", "none", stroke=INK, sw=5)
    s.path(f"M{CX + 98},{GY - 210} H{CX + 142} V{GY - 160} H{CX + 98} Z", "#ffcf6a", INK, 4, extra=f'filter="{s.glow(6)}"')
    glow_at(s, CX + 120, GY - 185, 110, "#ffb050", 0.35)
    drift = [(CX - 560, GY + 20), (CX - 300, GY - 40), (CX - 60, GY + 10), (CX + 200, GY - 30), (CX + 520, GY + 20)]
    s.path(smooth(drift, closed=False) + f" L{CX + 520},{H} L{CX - 560},{H} Z", s.lin([(0, "#f4fbff"), (1, "#a8c4d8")]), stroke=None)


# ═══════════════════════════ 2б — Заводные Глубины ═══════════════════════════

def brass_body(s: Svg, x, y, rx, ry, k=0.0):
    s.ellipse(x, y, rx, ry, s.lin([(0, _tint("#d8a04e", k)), (0.6, "#8a5a2a"), (1, "#3a2210")], 0, 0, 1, 1), INK, 6)


@event("broken_golem_event", "depths")
def ev_broken_golem(s, rnd):
    s.ellipse(CX, GY + 6, 280, 26, "#000", extra=f'opacity="0.5" filter="{s.blur(8)}"')
    for side in (-1, 1):
        s.path(f"M{CX + side * 70},{GY - 90} Q{CX + side * 190},{GY - 60} {CX + side * 230},{GY - 10}", "none", stroke=INK, sw=64)
        s.path(f"M{CX + side * 70},{GY - 90} Q{CX + side * 190},{GY - 60} {CX + side * 230},{GY - 10}", "none", stroke="#9a6a32", sw=50)
        s.circle(CX + side * 240, GY - 14, 40, "#b07a3a", INK, 6)
    brass_body(s, CX, GY - 210, 170, 190, 0.1)
    for side in (-1, 1):
        s.path(f"M{CX + side * 150},{GY - 300} Q{CX + side * 260},{GY - 200} {CX + side * 250},{GY - 60}", "none", stroke=INK, sw=50)
        s.path(f"M{CX + side * 150},{GY - 300} Q{CX + side * 260},{GY - 200} {CX + side * 250},{GY - 60}", "none", stroke="#a87038", sw=38)
        s.circle(CX + side * 160, GY - 300, 46, "#c08a4a", INK, 6)
    s.circle(CX, GY - 220, 58, "#3a2210", INK, 6)
    s.path(f"M{CX - 12},{GY - 230} a12,12 0 1 1 24,0 l-6,30 h-12 Z", "#ffd88a", stroke=None, extra=f'filter="{s.glow(10)}"')
    glow_at(s, CX, GY - 220, 90, "#ffb040", 0.35)
    for k in range(-3, 4):
        s.circle(CX + k * 40, GY - 360, 6, "#e8c080", INK, 2)
    s.open(f'transform="rotate(-18 {CX} {GY - 440})"')
    s.path(f"M{CX - 90},{GY - 380} V{GY - 480} Q{CX},{GY - 540} {CX + 90},{GY - 480} V{GY - 380} Z", s.lin([(0, "#d8a04e"), (1, "#5a3414")]), INK, 6)
    s.path(f"M{CX - 70},{GY - 452} H{CX + 70}", "none", stroke=INK, sw=24)
    s.eye(CX - 34, GY - 452, 9, "#ffb040")
    s.circle(CX + 34, GY - 452, 8, "#2a1a0c")
    s.close()
    _gear(s, CX + 380, GY - 10, 46, 10, "#9a6a32", rot=12)
    _gear(s, CX - 360, GY - 4, 30, 8, "#b07a3a", rot=5)


@event("steam_valve_event", "depths")
def ev_steam_valve(s, rnd):
    for d, sw_ in ((f"M{CX - 700},{GY - 260} H{CX}", 70), (f"M{CX},{GY + 40} V{GY - 260} H{CX + 700}", 70)):
        s.path(d, "none", stroke=INK, sw=sw_ + 12)
        s.path(d, "none", stroke="#8a5a2a", sw=sw_)
        s.path(d, "none", stroke="#d8a04e", sw=6, extra='opacity="0.45"')
    s.circle(CX, GY - 260, 70, s.rad([(0, "#c08a4a"), (1, "#5a3414")]), INK, 6)
    s.path(f"M{CX},{GY - 330} V{GY - 400}", "none", stroke=INK, sw=26)
    s.path(f"M{CX},{GY - 330} V{GY - 400}", "none", stroke="#9a6a32", sw=16)
    s.circle(CX, GY - 440, 120, "none", INK, 30)
    s.circle(CX, GY - 440, 120, "none", s.lin([(0, "#ffd06a"), (0.5, "#ff6a2a"), (1, "#b02010")]), 20)
    for k in range(6):
        a = k * math.pi / 3 + 0.3
        s.path(f"M{CX},{GY - 440} L{CX + 118 * math.cos(a):.1f},{GY - 440 + 118 * math.sin(a):.1f}", "none", stroke=INK, sw=18)
        s.path(f"M{CX},{GY - 440} L{CX + 118 * math.cos(a):.1f},{GY - 440 + 118 * math.sin(a):.1f}", "none", stroke="#ff7a3a", sw=9)
    s.circle(CX, GY - 440, 26, "#ffb050", INK, 6)
    glow_at(s, CX, GY - 440, 170, "#ff5a2a", 0.4)
    for x, y, r in ((CX + 120, GY - 300, 110), (CX + 260, GY - 380, 150), (CX - 200, GY - 320, 90), (CX + 420, GY - 300, 120), (CX - 380, GY - 360, 130)):
        s.circle(x, y, r, "#f0e8e0", extra=f'opacity="0.38" filter="{s.blur(r * 0.35)}"')
    for k in range(5):
        s.path(f"M{CX + 76},{GY - 240 + k * 12} q{60 + k * 10},{-20 - k * 6} {140 + k * 30},{-30 - k * 4}", "none", stroke="#ffffff", sw=5,
               extra=f'opacity="0.5" filter="{s.blur(3)}"')


@event("brass_archive_event", "depths")
def ev_brass_archive(s, rnd):
    for depth, (scale, op) in enumerate(((0.6, 0.55), (0.8, 0.8), (1.0, 1.0))):
        for side in (-1, 1):
            x0 = CX + side * (180 + depth * 160)
            w = 130 * scale
            top = GY - 470 * scale
            s.path(f"M{x0 - w / 2},{GY} V{top} H{x0 + w / 2} V{GY} Z", s.lin([(0, "#6a4220"), (1, "#2a1608")]), INK, 5, extra=f'opacity="{op}"')
            for r in range(6):
                y = top + 20 + r * (GY - top - 30) / 6
                s.path(f"M{x0 - w / 2},{y + 56 * scale} H{x0 + w / 2}", "none", stroke=INK, sw=5, extra=f'opacity="{op}"')
                for c in range(4):
                    px = x0 - w / 2 + 12 + c * (w - 20) / 4
                    lit = rnd.random() < 0.3
                    s.path(f"M{px},{y + 54 * scale} V{y + 8} H{px + (w - 30) / 4 - 4} V{y + 54 * scale} Z", "#ffd88a" if lit else "#b07a3a",
                           INK, 2, extra=f'opacity="{op}"' + (f' filter="{s.glow(3)}"' if lit else ""))
    s.path(f"M{CX - 120},{GY} L{CX - 100},{GY - 150} H{CX + 100} L{CX + 120},{GY} Z", s.lin([(0, "#9a6a32"), (1, "#3a2210")]), INK, 6)
    s.path(f"M{CX - 140},{GY - 150} L{CX - 80},{GY - 230} H{CX + 80} L{CX + 140},{GY - 150} Z", "#d8a04e", INK, 6)
    for k in range(5):
        s.path(f"M{CX - 100 + k * 8},{GY - 170 - k * 12} H{CX + 100 - k * 8}", "none", stroke="#3a2210", sw=3)
    s.path(f"M{CX - 40},{GY - 200} L{CX},{GY - 225} L{CX + 40},{GY - 200} L{CX},{GY - 175} Z", "none", stroke="#7ff2ff", sw=3,
           extra=f'filter="{s.glow(5)}"')
    glow_at(s, CX, GY - 200, 120, "#ffd88a", 0.35)


@event("coast_raiders_event", "depths")
def ev_coast_raiders(s, rnd):
    for x, y, w, h, rot in ((CX - 380, GY - 60, 160, 120, -4), (CX + 380, GY - 60, 170, 120, 5), (CX - 200, GY - 40, 120, 80, 3),
                            (CX + 220, GY - 40, 130, 80, -3), (CX - 330, GY - 170, 110, 90, 8), (CX + 330, GY - 170, 120, 90, -6)):
        s.path(f"M{x - w / 2},{y + h / 2} V{y - h / 2} H{x + w / 2} V{y + h / 2} Z", s.lin([(0, "#8a5a32"), (1, "#3a2410")]), INK, 5,
               extra=f'transform="rotate({rot} {x} {y})"')
    for x0, x1, y in ((CX - 470, CX + 470, GY - 130), (CX - 420, CX + 440, GY - 210)):
        s.path(f"M{x0},{y} L{x1},{y - 20}", "none", stroke=INK, sw=26)
        s.path(f"M{x0},{y} L{x1},{y - 20}", "none", stroke="#6a4424", sw=18)
    for i, (dx, h) in enumerate(((-150, 330), (10, 380), (170, 320))):
        hx, top, hr, w = figure(s, CX + dx, GY - 120, h, robe=("#4a3a32", "#16100c"), hood=i != 1, face="#c09070",
                                eyes="#ffb040" if i != 1 else None, rim="#ffb050")
        ax = hx + (w * 0.5 if i != 0 else -w * 0.5)
        s.path(f"M{ax},{top + h * 0.45} L{ax + (30 if i != 0 else -30)},{top + 20}", "none", stroke=INK, sw=10)
        s.path(f"M{ax + (30 if i != 0 else -30)},{top + 20} q{24 if i != 0 else -24},10 {10 if i != 0 else -10},44", "#c8c8d0", INK, 4)
        if i == 1:
            s.path(f"M{hx - hr},{top + hr * 0.3} Q{hx},{top - hr * 1.2} {hx + hr},{top + hr * 0.3}", "#7a7a80", INK, 4)
            s.path(f"M{hx - 4},{top + hr * 2} q4,{hr * 1.2} 8,0", "#a08060", INK, 2)
    s.path(f"M{CX + 300},{GY - 120} V{GY - 380}", "none", stroke=INK, sw=12)
    s.path(f"M{CX + 300},{GY - 380} q-30,-40 0,-90 q30,40 0,90 Z", "#ffb040", stroke=None, extra=f'filter="{s.glow(10)}"')
    glow_at(s, CX + 300, GY - 420, 160, "#ff8a30", 0.35)


@event("automaton_forge_event", "depths")
def ev_automaton_forge(s, rnd):
    s.path(f"M{CX + 160},{GY} V{GY - 360} Q{CX + 330},{GY - 470} {CX + 500},{GY - 360} V{GY} Z", s.lin([(0, "#6a4220"), (1, "#1e1006")]), INK, 6)
    s.path(f"M{CX + 230},{GY - 40} V{GY - 260} Q{CX + 330},{GY - 330} {CX + 430},{GY - 260} V{GY - 40} Z",
           s.rad([(0, "#fff0a0"), (0.4, "#ff8a20"), (1, "#7a1a04")], cy=0.7), INK, 5)
    glow_at(s, CX + 330, GY - 160, 220, "#ff7a20", 0.4)
    s.path(f"M{CX - 260},{GY} L{CX - 220},{GY - 100} H{CX - 20} L{CX + 20},{GY} Z", "#2a1a10", INK, 6)
    s.path(f"M{CX - 320},{GY - 100} H{CX + 60} Q{CX + 120},{GY - 110} {CX + 90},{GY - 150} H{CX - 280} Q{CX - 330},{GY - 130} {CX - 320},{GY - 100} Z",
           s.lin([(0, "#7a7a84"), (1, "#2a2a30")]), INK, 6)
    s.path(f"M{CX - 260},{GY - 162} H{CX - 20} L{CX - 30},{GY - 150} H{CX - 250} Z", "#ffb050", INK, 3, extra=f'filter="{s.glow(5)}"')
    s.path(f"M{CX - 600},{GY - 520} L{CX - 360},{GY - 420} L{CX - 180},{GY - 330}", "none", stroke=INK, sw=46)
    s.path(f"M{CX - 600},{GY - 520} L{CX - 360},{GY - 420} L{CX - 180},{GY - 330}", "none", stroke="#a87038", sw=32)
    for x, y in ((CX - 360, GY - 420), (CX - 600, GY - 520)):
        _gear(s, x, y, 38, 9, "#c08a4a", rot=10)
    s.path(f"M{CX - 210},{GY - 260} H{CX - 90} V{GY - 360} H{CX - 210} Z", s.lin([(0, "#9a9aa4"), (1, "#3a3a44")]), INK, 6)
    for _ in range(40):
        a = rnd.uniform(-math.pi, 0)
        r = rnd.uniform(40, 220)
        x, y = CX - 140 + r * math.cos(a), GY - 165 + r * math.sin(a) * 0.8
        s.path(f"M{x:.1f},{y:.1f} l{6 * math.cos(a):.1f},{6 * math.sin(a):.1f}", "none", stroke="#ffd06a", sw=3, extra=f'filter="{s.glow(2)}"')


@event("clockwork_oracle_event", "depths")
def ev_clockwork_oracle(s, rnd):
    s.path(f"M{CX - 130},{GY} L{CX - 100},{GY - 220} H{CX + 100} L{CX + 130},{GY} Z", s.lin([(0, "#7a4a22"), (1, "#2a1608")]), INK, 6)
    s.path(f"M{CX - 30},{GY - 140} H{CX + 30} V{GY - 128} H{CX - 30} Z", "#0a0502", "#ffd88a", 2)
    s.path(f"M{CX - 210},{GY - 220} H{CX + 210} V{GY - 600} H{CX - 210} Z", "#9ad8ff", INK, 8, extra='opacity="0.18"')
    s.path(f"M{CX - 210},{GY - 220} H{CX + 210} V{GY - 600} H{CX - 210} Z", "none", "#d8a04e", 6)
    s.path(f"M{CX - 180},{GY - 580} L{CX - 100},{GY - 240}", "none", stroke="#ffffff", sw=6, extra='opacity="0.35"')
    s.path(f"M{CX - 90},{GY - 230} Q{CX - 110},{GY - 330} {CX - 70},{GY - 360} H{CX + 70} Q{CX + 110},{GY - 330} {CX + 90},{GY - 230} Z", "#8a5a2a", INK, 5)
    s.path(smooth([(CX - 100, GY - 360), (CX - 110, GY - 470), (CX - 60, GY - 540), (CX + 60, GY - 540), (CX + 110, GY - 470), (CX + 100, GY - 360),
                   (CX + 50, GY - 330), (CX - 50, GY - 330)], k=0.8), s.lin([(0, "#f0c070"), (0.5, "#b07a3a"), (1, "#5a3414")], 0, 0, 1, 1), INK, 6)
    s.eye(CX - 42, GY - 450, 13, "#7ff2ff")
    s.eye(CX + 42, GY - 450, 13, "#7ff2ff")
    s.path(f"M{CX - 40},{GY - 380} H{CX + 40} M{CX - 30},{GY - 370} H{CX + 30}", "none", stroke=INK, sw=5)
    _gear(s, CX + 70, GY - 520, 26, 8, "#d8a04e", rot=7)
    s.path(f"M{CX - 120},{GY - 660} H{CX + 120} V{GY - 610} H{CX - 120} Z", "#3a2210", "#d8a04e", 4)
    for k in range(9):
        s.path(f"M{CX - 100 + k * 25},{GY - 646} v20", "none", stroke="#ffd88a", sw=5)
    glow_at(s, CX, GY - 450, 140, "#7ff2ff", 0.22)


# ═══════════════════════════ 4а — Кровавый Двор ═══════════════════════════

def mask(s: Svg, x, y, w, col="#e8e0d0", trim="#d4a640"):
    s.path(f"M{x - w},{y} Q{x - w},{y - w * 0.45} {x - w * 0.4},{y - w * 0.4} Q{x},{y - w * 0.2} {x + w * 0.4},{y - w * 0.4} "
           f"Q{x + w},{y - w * 0.45} {x + w},{y} Q{x + w * 0.5},{y + w * 0.45} {x},{y + w * 0.15} Q{x - w * 0.5},{y + w * 0.45} {x - w},{y} Z",
           col, INK, 3)
    for dx in (-w * 0.45, w * 0.45):
        s.ellipse(x + dx, y - w * 0.08, w * 0.22, w * 0.12, "#0a0204")
    s.path(f"M{x - w},{y} Q{x},{y - w * 0.6} {x + w},{y}", "none", stroke=trim, sw=2.5)


@event("masquerade_event", "court")
def ev_masquerade(s, rnd):
    for dx, h, col in ((-520, 300, "#4a1020"), (-380, 320, "#1a2a4a"), (380, 310, "#3a1a4a"), (520, 290, "#1a3a2a")):
        hx, top, hr, w = figure(s, CX + dx, GY + 20, h, robe=(_tint(col, 0.2), _tint(col, -0.5)), hood=False, face="#e8d0c0", rim="#ff6a6a")
        mask(s, hx, top + hr * 1.05, hr * 1.1, "#e8e0d0" if dx < 0 else "#2a2a2a")
    s.ellipse(CX, GY + 10, 220, 30, "#000", extra=f'opacity="0.5" filter="{s.blur(8)}"')
    body = [(CX - 200, GY + 10), (CX - 120, GY - 140), (CX - 60, GY - 300), (CX - 40, GY - 340), (CX + 40, GY - 340), (CX + 60, GY - 300),
            (CX + 120, GY - 140), (CX + 200, GY + 10)]
    s.path(smooth(body, k=0.6), s.lin([(0, "#f0f4ff"), (0.5, "#a8b0c8"), (1, "#4a5068")], 0, 0, 1, 1), INK, 6)
    for k in range(5):
        s.path(f"M{CX - 150 + k * 70},{GY + 6} Q{CX - 100 + k * 50},{GY - 140} {CX - 40 + k * 20},{GY - 300}", "none", stroke="#7a8098", sw=3)
    s.path(f"M{CX - 40},{GY - 340} Q{CX - 100},{GY - 330} {CX - 150},{GY - 270}", "none", stroke=INK, sw=26)
    s.path(f"M{CX - 40},{GY - 340} Q{CX - 100},{GY - 330} {CX - 150},{GY - 270}", "none", stroke="#e8e0e8", sw=18)
    s.path(f"M{CX + 40},{GY - 340} Q{CX + 130},{GY - 350} {CX + 220},{GY - 380}", "none", stroke=INK, sw=26)
    s.path(f"M{CX + 40},{GY - 340} Q{CX + 130},{GY - 350} {CX + 220},{GY - 380}", "none", stroke="#e8e0e8", sw=18)
    s.circle(CX + 232, GY - 384, 14, "#f0e0e0", INK, 4)
    s.circle(CX, GY - 400, 52, "#f4e8e8", INK, 6)
    s.path(f"M{CX - 60},{GY - 420} Q{CX - 70},{GY - 500} {CX},{GY - 480} Q{CX + 70},{GY - 500} {CX + 60},{GY - 420}", "#1a0a10", INK, 5)
    mask(s, CX, GY - 404, 56, "#d8dce8", "#d4a640")
    for dx, h in ((-34, 60), (-12, 80), (12, 80), (34, 60)):
        s.path(f"M{CX + dx},{GY - 450} q{dx * 0.4},{-h * 0.6} {dx * 0.2},{-h}", "none", stroke="#e8e0f0", sw=5)
    s.path(f"M{CX - 8},{GY - 372} q8,6 16,0", "none", stroke="#b0102a", sw=4)
    glow_at(s, CX, GY - 300, 220, "#e8e8ff", 0.18)


@event("blood_fountain_event", "court")
def ev_blood_fountain(s, rnd):
    s.ellipse(CX, GY - 10, 360, 70, s.lin([(0, "#d8d0c8"), (1, "#6a6058")]), INK, 6)
    s.ellipse(CX, GY - 22, 320, 50, s.rad([(0, "#e0304a"), (1, "#5a0410")]), INK, 4)
    s.ellipse(CX - 80, GY - 30, 90, 10, "#ff8a9a", extra='opacity="0.4"')
    s.path(f"M{CX - 40},{GY - 30} L{CX - 30},{GY - 260} H{CX + 30} L{CX + 40},{GY - 30} Z", s.lin([(0, "#e0d8d0"), (1, "#7a7068")], 0, 0, 1, 0), INK, 5)
    s.ellipse(CX, GY - 260, 160, 34, s.lin([(0, "#e8e0d8"), (1, "#7a7068")]), INK, 5)
    s.ellipse(CX, GY - 268, 140, 22, "#b0102a", INK, 3)
    s.path(f"M{CX - 16},{GY - 270} L{CX - 10},{GY - 380} H{CX + 10} L{CX + 16},{GY - 270} Z", "#d8d0c8", INK, 4)
    s.path(smooth([(CX - 50, GY - 380), (CX - 40, GY - 430), (CX, GY - 460), (CX + 40, GY - 430), (CX + 50, GY - 380)], closed=False) + " Z",
           s.lin([(0, "#e8e0d8"), (1, "#8a8078")]), INK, 5)
    for side in (-1, 1):
        for k, spread in enumerate((150, 120)):
            x0, y0 = CX + side * 36, GY - 440 if k == 0 else GY - 268
            s.path(f"M{x0},{y0} Q{x0 + side * spread * 0.7},{y0 - 60} {x0 + side * spread},{y0 + (200 if k == 0 else 230)}", "none",
                   stroke="#c0102a", sw=10 - k * 2, extra=f'filter="{s.glow(2)}"')
    s.path(f"M{CX},{GY - 460} V{GY - 520}", "none", stroke="#d0182e", sw=10)
    for x, y in ((CX - 300, GY + 20), (CX + 290, GY + 26), (CX - 210, GY + 40), (CX + 200, GY + 44), (CX - 360, GY - 20), (CX + 370, GY - 14)):
        for k in range(5):
            a = k * 2 * math.pi / 5
            s.ellipse(round(x + 12 * math.cos(a), 1), round(y + 10 * math.sin(a), 1), 14, 10, "#c0102a", INK, 2)
        s.circle(x, y, 8, "#7a0010", INK, 2)
        s.path(f"M{x + 12},{y + 8} q30,20 50,6", "none", stroke="#2a5a2a", sw=4)
    glow_at(s, CX, GY - 260, 260, "#ff2a3a", 0.18)


@event("counts_dice_event", "court")
def ev_counts_dice(s, rnd):
    hx, top, hr, w = figure(s, CX, GY - 120, 400, robe=("#3a0a18", "#0a0206"), hood=False, face="#e8dcd4", eyes="#ff3a3a", wide=1.3, rim="#ff4a4a")
    for dx in range(-3, 4):
        s.path(f"M{hx + dx * hr * 0.3},{top + hr * 2} q{dx * 4},{hr * 0.5} {dx * 8},{hr * 0.2}", "none", stroke="#f0e8e0", sw=5)
    s.path(f"M{hx - hr},{top + hr * 0.4} Q{hx},{top - hr * 0.3} {hx + hr},{top + hr * 0.4}", "#1a0a10", INK, 4)
    s.path(f"M{hx - 10},{top + hr * 1.55} l4,12 l4,-12 M{hx + 2},{top + hr * 1.55} l4,12 l4,-12", "none", stroke="#ffffff", sw=2)
    s.path(f"M{CX - 560},{GY - 130} H{CX + 560} L{CX + 640},{GY - 20} H{CX - 640} Z", s.lin([(0, "#5a0a18"), (1, "#1e0408")]), INK, 6)
    s.path(f"M{CX - 640},{GY - 20} H{CX + 640} V{GY + 10} H{CX - 640} Z", "#d4a640", INK, 4)
    for x, y, rot, pips in ((CX - 80, GY - 80, -12, 6), (CX + 40, GY - 70, 18, 6)):
        s.open(f'transform="rotate({rot} {x} {y})"')
        s.path(f"M{x - 34},{y - 34} H{x + 34} V{y + 34} H{x - 34} Z", s.lin([(0, "#fff8ec"), (1, "#c8b8a0")], 0, 0, 1, 1), INK, 5)
        for px, py in ((-16, -16), (16, -16), (-16, 0), (16, 0), (-16, 16), (16, 16))[:pips]:
            s.circle(x + px, y + py, 5.5, "#7a0010")
        s.close()
    s.path(f"M{CX + 260},{GY - 90} L{CX + 236},{GY - 210} H{CX + 304} L{CX + 280},{GY - 90} Z", "#d4a640", INK, 4)
    s.ellipse(CX + 270, GY - 206, 34, 8, "#8a0010", INK, 3)
    s.path(f"M{CX + 250},{GY - 90} H{CX + 290} L{CX + 300},{GY - 76} H{CX + 240} Z", "#d4a640", INK, 3)
    s.path(f"M{CX - 300},{GY - 90} V{GY - 220}", "none", stroke="#f0e8d0", sw=20)
    s.path(f"M{CX - 300},{GY - 220} q-14,-30 0,-56 q14,26 0,56 Z", "#ffcf6a", stroke=None, extra=f'filter="{s.glow(8)}"')
    glow_at(s, CX - 300, GY - 240, 180, "#ffb050", 0.35)
    for x, y in ((CX - 440, GY - 60), (CX - 400, GY - 50), (CX - 420, GY - 72)):
        s.ellipse(x, y, 16, 7, "#ffd25a", INK, 2)


@event("thrall_market_event", "court")
def ev_thrall_market(s, rnd):
    s.path(f"M0,150 H{W} V190 H0 Z", "#2a0a10", INK, 5)
    for x in range(100, W, 260):
        s.path(f"M{x - 20},190 V{GY} H{x + 20} V190 Z", s.lin([(0, "#5a2a2a"), (1, "#1a0808")], 0, 0, 1, 0), INK, 5)
    s.path(f"M{CX - 560},{GY - 70} H{CX + 560} V{GY} H{CX - 560} Z", s.lin([(0, "#4a1a1a"), (1, "#1a0606")]), INK, 6)
    for i, dx in enumerate((-400, -230, -60, 110, 280)):
        x = CX + dx
        hx, top, hr, w = figure(s, x, GY - 70, 270 + (i % 2) * 20, robe=("#6a5a50", "#2a201c"), hood=False, face="#c8b8b0", eyes="#e8e8ff",
                                lean=rnd.uniform(-8, 8))
        for k in range(-3, 4):
            cx_ = hx + k * 22
            s.ellipse(cx_, top + hr * 2.6 + abs(k) * 4, 12, 8, "none", "#a8a8b0", 4)
        s.path(f"M{hx - w * 0.4},{top + 150} H{hx + w * 0.4}", "none", stroke="#a8a8b0", sw=5, extra='stroke-dasharray="14 6"')
    s.path(f"M{CX - 600},{GY - 40} Q{CX},{GY + 20} {CX + 600},{GY - 40}", "none", stroke="#a8a8b0", sw=6, extra='stroke-dasharray="18 8"')
    hx, top, hr, w = figure(s, CX + 470, GY + 30, 420, robe=("#8a1a2a", "#2a0410"), hood=False, face="#f0dcd0", rim="#ff6a6a")
    s.path(f"M{hx - w * 0.6},{top + 160} H{hx - w * 0.6 - 100} V{top + 260} H{hx - w * 0.6} Z", "#f0e4c8", INK, 4)
    for k in range(4):
        s.path(f"M{hx - w * 0.6 - 86},{top + 182 + k * 18} H{hx - w * 0.6 - 14}", "none", stroke="#6a4a2a", sw=3)


@event("blind_mirror_event", "court")
def ev_blind_mirror(s, rnd):
    s.path(f"M{CX - 210},{GY + 10} V{GY - 470} Q{CX},{GY - 620} {CX + 210},{GY - 470} V{GY + 10} Z", s.lin([(0, "#f0d070"), (0.5, "#a07a2a"), (1, "#4a3010")], 0, 0, 1, 1), INK, 8)
    s.path(f"M{CX - 170},{GY - 20} V{GY - 460} Q{CX},{GY - 580} {CX + 170},{GY - 460} V{GY - 20} Z",
           s.lin([(0, "#5a6a80"), (0.5, "#2a3448"), (1, "#14182a")], 0, 0, 1, 1), INK, 5)
    s.path(f"M{CX - 120},{GY - 380} Q{CX - 60},{GY - 500} {CX},{GY - 380} M{CX + 20},{GY - 300} Q{CX + 80},{GY - 420} {CX + 140},{GY - 300}", "none",
           stroke="#8a9ab8", sw=4, extra='opacity="0.5"')
    s.path(f"M{CX - 150},{GY - 520} L{CX + 60},{GY - 60}", "none", stroke="#ffffff", sw=16, extra=f'opacity="0.12" filter="{s.blur(4)}"')
    s.path(f"M{CX - 100},{GY - 160} H{CX + 100} V{GY - 40} H{CX - 100} Z", "#2a0a14", stroke=None, extra='opacity="0.6"')
    for k in range(10):
        a = math.pi + k * math.pi / 9
        s.circle(CX + 196 * math.cos(a), GY - 470 + 140 * math.sin(a), 10, "#f0d070", INK, 3)
    figure(s, CX + 420, GY + 30, 440, robe=("#2a1a2a", "#0a050a"), hood=True, eyes="#ff4a4a", lean=-30, rim="#ff6a6a")
    s.ellipse(CX + 420, GY + 30, 160, 20, "#000", extra=f'opacity="0.55" filter="{s.blur(8)}"')


@event("night_carriage_event", "court")
def ev_night_carriage(s, rnd):
    for x0 in (CX - 420, CX - 220):
        y = GY - 40
        s.path(smooth([(x0 - 110, y - 150), (x0 - 40, y - 170), (x0 + 50, y - 160), (x0 + 80, y - 200), (x0 + 120, y - 230), (x0 + 140, y - 200),
                       (x0 + 110, y - 140), (x0 + 70, y - 110), (x0 - 100, y - 100)], k=0.6), s.lin([(0, "#2a2a30"), (1, "#050507")]), INK, 5)
        for lx in (x0 - 90, x0 - 50, x0 + 30, x0 + 60):
            s.path(f"M{lx},{y - 110} L{lx + 6},{y}", "none", stroke=INK, sw=14)
        s.eye(x0 + 118, y - 205, 5, "#ff3a3a")
        s.path(f"M{x0 + 90},{y - 220} q-20,-30 -60,-30", "none", stroke="#1a1a20", sw=10)
    s.path(f"M{CX - 120},{GY - 150} L{CX + 30},{GY - 150}", "none", stroke=INK, sw=8)
    s.path(f"M{CX + 40},{GY - 80} V{GY - 360} Q{CX + 230},{GY - 420} {CX + 420},{GY - 360} V{GY - 80} Z", s.lin([(0, "#2a1a24"), (1, "#060306")]), INK, 6)
    s.path(f"M{CX + 160},{GY - 300} H{CX + 300} V{GY - 180} H{CX + 160} Z", "#ffb84a", INK, 4, extra=f'filter="{s.glow(5)}"')
    s.path(f"M{CX + 230},{GY - 300} V{GY - 180} M{CX + 160},{GY - 240} H{CX + 300}", "none", stroke=INK, sw=5)
    s.path(f"M{CX + 40},{GY - 360} Q{CX + 230},{GY - 430} {CX + 420},{GY - 360}", "none", stroke="#d4a640", sw=5)
    for wx, r in ((CX + 110, 70), (CX + 360, 80)):
        s.circle(wx, GY - 60, r, "none", INK, 14)
        s.circle(wx, GY - 60, r, "none", "#5a1a24", 7)
        for k in range(8):
            a = k * math.pi / 4
            s.path(f"M{wx},{GY - 60} L{wx + r * math.cos(a):.1f},{GY - 60 + r * math.sin(a):.1f}", "none", stroke=INK, sw=5)
    for lx in (CX + 30, CX + 430):
        s.path(f"M{lx - 12},{GY - 330} H{lx + 12} V{GY - 290} H{lx - 12} Z", "#ffd06a", INK, 3, extra=f'filter="{s.glow(6)}"')
        glow_at(s, lx, GY - 310, 70, "#ffb040", 0.4)
    figure(s, CX - 10, GY - 150, 200, robe=("#1a1a20", "#050507"), hood=True, eyes="#d0e8ff")
    s.path(f"M{CX - 30},{GY - 340} H{CX + 10} V{GY - 380} H{CX - 30} Z", "#0a0a0c", INK, 3)


# ═══════════════════════════ 4б — Звёздная Башня ═══════════════════════════

SCHOOL_COLORS = ("#ff6a3a", "#ffe07a", "#5ef0ff", "#b07aff", "#ff7ad8")


def sigil(s: Svg, x, y, r, col):
    s.circle(x, y, r, "none", col, 3, extra=f'filter="{s.glow(4)}"')
    s.path(star(x, y, r * 0.8, r * 0.35, 6), "none", stroke=col, sw=2.5, extra=f'filter="{s.glow(3)}"')


@event("star_exam_event", "tower")
def ev_star_exam(s, rnd):
    for i in range(5):
        y = GY - i * 28
        w = 560 - i * 70
        s.path(f"M{CX - w},{y} H{CX + w} V{y - 28} H{CX - w} Z", s.lin([(0, "#4a4a90"), (1, "#14143a")]), INK, 4)
        s.path(f"M{CX - w},{y - 28} H{CX + w}", "none", stroke="#9aa8ff", sw=2)
    top = GY - 140
    hx, ftop, hr, w = figure(s, CX, top - 90, 260, robe=("#3a3a8a", "#0a0a24"), hood=False, face="#e0d0c0", rim="#9ad8ff")
    s.path(f"M{hx - hr * 1.2},{ftop + hr * 0.5} L{hx},{ftop - hr * 1.6} L{hx + hr * 1.2},{ftop + hr * 0.5} Z", "#2a2a6a", INK, 4)
    s.path(f"M{hx - hr * 0.6},{ftop + hr * 1.6} Q{hx},{ftop + hr * 4} {hx + hr * 0.6},{ftop + hr * 1.6}", "#d8d8e0", INK, 3)
    s.path(f"M{CX - 90},{top} L{CX - 70},{top - 110} H{CX + 70} L{CX + 90},{top} Z", s.lin([(0, "#5a5ab0"), (1, "#1a1a48")]), INK, 5)
    s.path(f"M{CX - 110},{top - 110} H{CX + 110} L{CX + 90},{top - 136} H{CX - 90} Z", "#d4b45a", INK, 4)
    for k, col in enumerate(SCHOOL_COLORS):
        a = math.pi + (k + 0.5) * math.pi / 5
        sigil(s, CX + 470 * math.cos(a), GY - 250 + 345 * math.sin(a), 44, col)
    figure(s, CX - 560, GY + 30, 300, robe=("#2a2a50", "#08081a"), hood=True, lean=20, rim="#9ad8ff")


@event("ley_well_event", "tower")
def ev_ley_well(s, rnd):
    s.path(f"M{CX - 70},{GY - 120} L{CX - 120},-10 H{CX + 120} L{CX + 70},{GY - 120} Z", s.lin([(0, "#5ef0ff", 0.0), (0.6, "#5ef0ff", 0.35), (1, "#e0ffff", 0.8)]),
           stroke=None, extra=f'filter="{s.blur(10)}"')
    s.path(f"M{CX - 30},{GY - 120} L{CX - 40},0 H{CX + 40} L{CX + 30},{GY - 120} Z", "#e8ffff", stroke=None, extra=f'opacity="0.8" filter="{s.blur(6)}"')
    s.ellipse(CX, GY - 60, 260, 70, s.lin([(0, "#7a7ab0"), (1, "#2a2a5a")]), INK, 6)
    s.path(f"M{CX - 260},{GY - 60} V{GY} Q{CX},{GY + 60} {CX + 260},{GY} V{GY - 60}", s.lin([(0, "#5a5a9a"), (1, "#1a1a40")]), INK, 6)
    s.ellipse(CX, GY - 64, 210, 48, s.rad([(0, "#ffffff"), (0.3, "#9af8ff"), (1, "#2a6aa0")]), INK, 4, extra=f'filter="{s.glow(6)}"')
    for k in range(10):
        a = k * math.pi / 5
        s.path(f"M{CX + 230 * math.cos(a):.1f},{GY - 60 + 58 * math.sin(a):.1f} l0,-14", "none", stroke="#bff6ff", sw=4, extra=f'filter="{s.glow(3)}"')
    for _ in range(50):
        y = rnd.uniform(40, GY - 130)
        x = CX + rnd.uniform(-1, 1) * (40 + (GY - y) * 0.1)
        s.path(star(round(x, 1), round(y, 1), rnd.uniform(4, 9), 2, 4), "#ffffff", stroke=None, extra=f'opacity="{rnd.uniform(0.5, 1):.2f}" filter="{s.glow(2)}"')
    glow_at(s, CX, GY - 70, 300, "#5ef0ff", 0.35)


@event("broken_orrery_event", "tower")
def ev_broken_orrery(s, rnd):
    s.path(f"M{CX - 40},{GY} L{CX - 24},{GY - 300} H{CX + 24} L{CX + 40},{GY} Z", s.lin([(0, "#d4b45a"), (1, "#5a4a1a")], 0, 0, 1, 0), INK, 5)
    s.circle(CX, GY - 320, 46, s.rad([(0, "#fff0a0"), (0.5, "#ffb030"), (1, "#a04a10")]), INK, 5, extra=f'filter="{s.glow(10)}"')
    glow_at(s, CX, GY - 320, 120, "#ffb030", 0.35)
    for rx, ry, rot, broken in ((160, 50, -12, False), (240, 76, 8, True), (320, 100, -4, True)):
        if broken:
            s.path(f"M{CX - rx},{GY - 320} A{rx},{ry} 0 0 1 {CX + rx * 0.4},{GY - 320 - ry * 0.92}", "none", INK, 10, extra=f'transform="rotate({rot} {CX} {GY - 320})"')
            s.path(f"M{CX - rx},{GY - 320} A{rx},{ry} 0 0 1 {CX + rx * 0.4},{GY - 320 - ry * 0.92}", "none", "#d4b45a", 4, extra=f'transform="rotate({rot} {CX} {GY - 320})"')
        else:
            s.ellipse(CX, GY - 320, rx, ry, "none", INK, 10, extra=f'transform="rotate({rot} {CX} {GY - 320})"')
            s.ellipse(CX, GY - 320, rx, ry, "none", "#d4b45a", 4, extra=f'transform="rotate({rot} {CX} {GY - 320})"')
    s.circle(CX + 150, GY - 350, 18, "#5a8aff", INK, 4)
    for x, y, r, col in ((CX - 420, GY + 40, 34, "#c06a3a"), (CX + 380, GY + 60, 44, "#7ab0ff"), (CX - 240, GY + 90, 24, "#e0d0a0"),
                         (CX + 560, GY + 20, 28, "#b07aff"), (CX - 580, GY + 80, 38, "#5ac0a0")):
        s.circle(x, y, r, s.rad([(0, _tint(col, 0.4)), (1, _tint(col, -0.5))], cx=0.35, cy=0.35), INK, 4)
    for x0, x1, y in ((CX - 520, CX - 300, GY + 60), (CX + 200, CX + 480, GY + 100)):
        s.path(f"M{x0},{y} A{(x1 - x0) / 2},{30} 0 0 1 {x1},{y}", "none", INK, 9)
        s.path(f"M{x0},{y} A{(x1 - x0) / 2},{30} 0 0 1 {x1},{y}", "none", "#d4b45a", 4)
    for _ in range(18):
        x, y = CX + rnd.uniform(-500, 500), GY + rnd.uniform(20, 120)
        s.path(poly([(x, y - 8), (x + 10, y), (x, y + 6), (x - 8, y)]), "#bff6ff", INK, 1.5, extra='opacity="0.8"')


@event("renegade_apprentice_event", "tower")
def ev_renegade_apprentice(s, rnd):
    hx, top, hr, w = figure(s, CX, GY + 20, 520, robe=("#5a3a8a", "#140a24"), hood=True, eyes="#bff6ff", lean=-20, rim="#c88aff")
    bx, by = CX - 10, GY - 260
    s.path(f"M{bx - 110},{by} L{bx},{by + 26} L{bx + 110},{by} L{bx + 110},{by - 120} L{bx},{by - 96} L{bx - 110},{by - 120} Z", s.lin([(0, "#7a2a2a"), (1, "#2a0a0a")]), INK, 5)
    s.path(f"M{bx - 100},{by - 112} L{bx},{by - 88} L{bx + 100},{by - 112} L{bx + 100},{by - 10} L{bx},{by + 14} L{bx - 100},{by - 10} Z", "#f0e4c8", INK, 3)
    s.path(f"M{bx},{by - 88} V{by + 14}", "none", stroke=INK, sw=3)
    for k in range(4):
        s.path(f"M{bx - 86},{by - 90 + k * 18} L{bx - 14},{by - 72 + k * 18} M{bx + 14},{by - 72 + k * 18} L{bx + 86},{by - 90 + k * 18}", "none", stroke="#6a4aa0", sw=3)
    for side in (-1, 1):
        s.path(f"M{hx + side * w * 0.3},{top + 180} Q{bx + side * 150},{by - 20} {bx + side * 100},{by - 40}", "none", stroke=INK, sw=34)
        s.path(f"M{hx + side * w * 0.3},{top + 180} Q{bx + side * 150},{by - 20} {bx + side * 100},{by - 40}", "none", stroke="#4a2a7a", sw=24)
    glow_at(s, bx, by - 60, 200, "#c88aff", 0.4)
    for _ in range(30):
        a = rnd.uniform(-math.pi, 0)
        r = rnd.uniform(120, 300)
        s.path(star(round(bx + r * math.cos(a), 1), round(by - 60 + r * math.sin(a), 1), rnd.uniform(5, 11), 2, 4), "#e0c0ff", stroke=None,
               extra=f'filter="{s.glow(3)}"')
    for side in (-1, 1):
        sx = CX + side * 520
        s.path(f"M{sx - 60},{GY} V{GY - 260} H{sx + 60} V{GY} Z", "#14143a", INK, 4)
        s.path(f"M{sx - 60},{GY - 130} H{sx + 60}", "none", stroke=INK, sw=4)


@event("mirror_library_event", "tower")
def ev_mirror_library(s, rnd):
    for side in (-1, 1):
        for d in range(2):
            x = CX + side * (380 + d * 200)
            h = 520 - d * 60
            s.path(f"M{x - 80},{GY} V{GY - h} H{x + 80} V{GY} Z", s.lin([(0, "#3a2a5a"), (1, "#0a0820")]), INK, 5)
            for r in range(6):
                y = GY - h + 30 + r * (h - 40) / 6
                s.path(f"M{x - 80},{y + 60} H{x + 80}", "none", stroke=INK, sw=4)
                xx = x - 72
                while xx < x + 64:
                    bw = rnd.randint(10, 18)
                    col = rnd.choice(("#6a3a8a", "#3a5a8a", "#8a3a4a", "#3a6a5a", "#a08a3a"))
                    s.path(f"M{xx},{y + 58} V{y + 14 + rnd.randint(0, 10)} H{xx + bw} V{y + 58} Z", col, INK, 1.5)
                    xx += bw + 2
    for dx in (-150, 150):
        x = CX + dx
        s.path(f"M{x - 20},{GY} L{x - 10},{GY - 180} H{x + 10} L{x + 20},{GY} Z", "#5a4a8a", INK, 4)
        s.path(f"M{x - 80},{GY - 170} L{x + 80},{GY - 200} L{x + 70},{GY - 220} L{x - 90},{GY - 190} Z", "#7a5a3a", INK, 4)
        s.path(f"M{x - 70},{GY - 196} L{x},{GY - 186} L{x + 70},{GY - 214} L{x + 70},{GY - 250} L{x},{GY - 222} L{x - 70},{GY - 232} Z", "#f0e4c8", INK, 3)
        s.path(f"M{x - 50},{GY - 240} V{GY - 380} Q{x},{GY - 430} {x + 50},{GY - 380} V{GY - 240} Z", s.lin([(0, "#bfd8ff"), (1, "#4a5aa0")], 0, 0, 1, 1), "#d4b45a", 5)
        s.path(f"M{x - 30},{GY - 260} L{x + 20},{GY - 380}", "none", stroke="#ffffff", sw=5, extra='opacity="0.5"')
        glow_at(s, x, GY - 310, 90, "#bfd8ff", 0.3)
    s.path(f"M{CX - 12},{GY - 40} q-20,-50 0,-80 q20,30 0,80 Z", "#5ef0ff", stroke=None, extra=f'filter="{s.glow(8)}"')
    s.path(f"M{CX - 20},{GY} V{GY - 40} H{CX + 20} V{GY} Z", "#e8e0d0", INK, 3)


@event("star_cartographer_event", "tower")
def ev_star_cartographer(s, rnd):
    s.path(f"M0,{GY - 90} H{W} V{GY - 70} H0 Z", "#d4b45a", INK, 4)
    for x in range(30, W, 70):
        s.path(f"M{x - 8},{GY - 70} V{GY} H{x + 8} V{GY - 70} Z", "#9a8a4a", INK, 3)
    hx, top, hr, w = figure(s, CX - 120, GY + 10, 470, robe=("#2a3a7a", "#0a0e24"), hood=False, face="#e8d4c4", lean=30, rim="#9ad8ff")
    s.path(f"M{hx - hr * 1.1},{top + hr * 0.6} L{hx},{top - hr * 1.8} L{hx + hr * 1.1},{top + hr * 0.6} Z", "#3a4a9a", INK, 4)
    s.path(star(hx, top - hr * 1.8, 14, 5, 5), "#ffe07a", stroke=None, extra=f'filter="{s.glow(4)}"')
    s.path(f"M{hx - hr * 0.7},{top + hr * 1.5} Q{hx - hr * 0.2},{top + hr * 5} {hx + hr * 0.3},{top + hr * 1.6}", "#e8e8f0", INK, 3)
    s.open(f'transform="rotate(-28 {hx + 40} {top + 60})"')
    s.path(f"M{hx + 20},{top + 50} H{hx + 330} V{top + 86} H{hx + 20} Z", s.lin([(0, "#e8c870"), (1, "#7a5a1a")]), INK, 5)
    s.path(f"M{hx + 330},{top + 40} H{hx + 360} V{top + 96} H{hx + 330} Z", "#d4b45a", INK, 5)
    s.close()
    s.path(f"M{CX + 160},{GY - 100} L{CX + 560},{GY - 130} L{CX + 580},{GY - 90} L{CX + 180},{GY - 60} Z", "#f0e4c8", INK, 4)
    s.path(f"M{CX + 160},{GY - 100} q-24,20 0,40 M{CX + 560},{GY - 130} q24,20 20,40", "none", stroke=INK, sw=4)
    for _ in range(14):
        x, y = CX + rnd.uniform(200, 540), GY + rnd.uniform(-118, -76)
        s.circle(round(x, 1), round(y, 1), 3, "#2a3a7a")
    s.path(f"M{CX + 220},{GY - 90} L{CX + 300},{GY - 110} L{CX + 380},{GY - 92} L{CX + 470},{GY - 118}", "none", stroke="#2a3a7a", sw=2)
    for x, y in ((CX + 300, 160), (CX + 420, 120), (CX + 520, 200), (CX + 600, 140), (CX + 460, 260)):
        s.path(star(x, y, 12, 4, 4), "#ffffff", stroke=None, extra=f'filter="{s.glow(5)}"')
    s.path(f"M{CX + 300},160 L{CX + 420},120 L{CX + 520},200 L{CX + 600},140 M{CX + 520},200 L{CX + 460},260", "none", stroke="#bff6ff", sw=2, extra='opacity="0.6"')


# ═══════════════════════════ Улей — «Трещины Бездны» ═══════════════════════════

@event("whispering_rift_event", "hive")
def ev_whispering_rift(s, rnd):
    pts_l = [(CX - 20, 40), (CX - 70, 160), (CX - 40, 260), (CX - 110, 380), (CX - 60, 500), (CX - 90, GY)]
    pts_r = [(CX + 30, 40), (CX + 60, 170), (CX + 110, 270), (CX + 60, 390), (CX + 120, 510), (CX + 80, GY)]
    rift = poly(pts_l + list(reversed(pts_r)))
    s.path(rift, "#c06aff", stroke=None, extra=f'opacity="0.6" filter="{s.blur(30)}"')
    s.path(rift, s.lin([(0, "#05010a"), (0.5, "#1a0630"), (1, "#05010a")], 0, 0, 1, 0), INK, 8)
    s.path(rift, "none", stroke="#f0d0ff", sw=4, extra=f'filter="{s.glow(4)}"')
    for x, y, r in ((CX - 10, 200, 14), (CX + 30, 330, 18), (CX - 30, 460, 12), (CX + 50, 540, 10), (CX, 110, 9)):
        s.ellipse(x, y, r * 1.6, r, "#e8c0ff", extra=f'filter="{s.glow(5)}"')
        s.circle(x, y, r * 0.5, "#14001e")
    for k in range(7):
        side = -1 if k % 2 else 1
        y0 = 160 + k * 66
        s.path(smooth([(CX + side * 40, y0), (CX + side * 160, y0 - 40), (CX + side * 260, y0 + 20), (CX + side * 360, y0 - 30)], closed=False),
               "none", stroke=INK, sw=18 - k)
        s.path(smooth([(CX + side * 40, y0), (CX + side * 160, y0 - 40), (CX + side * 260, y0 + 20), (CX + side * 360, y0 - 30)], closed=False),
               "none", stroke="#6a2a9a", sw=10 - k * 0.6)
    for k in range(4):
        s.path(f"M{CX + 160 + k * 40},{200 + k * 70} q30,-10 50,4 M{CX - 200 - k * 40},{240 + k * 60} q-30,-10 -50,4", "none",
               stroke="#f0d0ff", sw=3, extra='opacity="0.7"')


@event("hive_amber_event", "hive")
def ev_hive_amber(s, rnd):
    blob = [(CX - 300, GY), (CX - 340, GY - 180), (CX - 240, GY - 400), (CX - 40, GY - 470), (CX + 200, GY - 430), (CX + 330, GY - 250),
            (CX + 300, GY)]
    s.path(smooth(blob, k=0.8), "#ffb030", stroke=None, extra=f'opacity="0.5" filter="{s.blur(40)}"')
    s.path(smooth(blob, k=0.8), s.rad([(0, "#ffe080"), (0.55, "#e08a18"), (1, "#6a3004")], cx=0.4, cy=0.4), INK, 8, extra='opacity="0.95"')
    s.open(f'transform="rotate(-10 {CX} {GY - 230})" opacity="0.75"')
    s.path(f"M{CX - 80},{GY - 120} H{CX + 80} V{GY - 340} H{CX - 80} Z", "#3a2410", "#1a0a02", 5)
    s.path(f"M{CX - 64},{GY - 136} H{CX + 64} V{GY - 260} H{CX - 64} Z", "#7a4a1a", stroke=None)
    s.circle(CX - 50, GY - 320, 16, "#8a5a1a", "#1a0a02", 3)
    s.path(star(CX, GY - 200, 40, 14, 5), "#ffd25a", stroke=None)
    s.close()
    s.path(f"M{CX - 200},{GY - 360} Q{CX - 120},{GY - 440} {CX},{GY - 450}", "none", stroke="#fff4c0", sw=14, extra=f'opacity="0.7" filter="{s.blur(4)}"')
    for x, y in ((CX + 160, GY - 330), (CX - 220, GY - 120), (CX + 220, GY - 80)):
        s.circle(x, y, 10, "#fff0a0", extra='opacity="0.5"')
    for x in (CX - 360, CX + 330, CX + 400):
        s.path(f"M{x - 18},{GY} Q{x - 10},{GY - 60} {x},{GY - 80} Q{x + 10},{GY - 60} {x + 18},{GY} Z", "#e08a18", INK, 4)


@event("ash_pilgrim_event", "hive")
def ev_ash_pilgrim(s, rnd):
    for k in range(7):
        x = CX - 120 - k * 110
        y = GY + 30 + k * 18
        for dx in (-14, 14):
            s.ellipse(x + dx, y + (8 if dx > 0 else 0), 13, 22, "#9a9088", extra='opacity="0.75"')
    hx, top, hr, w = figure(s, CX + 40, GY + 20, 500, robe=("#8a8580", "#2a2624"), hood=True, eyes="#ff8a30", lean=24, rim="#ff8a30")
    s.path(f"M{hx + w * 0.4},{top + 120} L{hx + w * 0.7},{GY + 20}", "none", stroke=INK, sw=16)
    s.path(f"M{hx + w * 0.4},{top + 120} L{hx + w * 0.7},{GY + 20}", "none", stroke="#5a4030", sw=9)
    s.path(f"M{hx + w * 0.4 - 6},{top + 120} q-14,-40 6,-70 q20,30 6,70 Z", "#ffb040", stroke=None, extra=f'filter="{s.glow(8)}"')
    glow_at(s, hx + w * 0.4, top + 90, 120, "#ff7a20", 0.4)
    for _ in range(60):
        x, y = CX + rnd.uniform(-200, 300), rnd.uniform(top - 100, GY)
        s.circle(round(x, 1), round(y, 1), round(rnd.uniform(2, 6), 1), "#bab0a8", extra=f'opacity="{rnd.uniform(0.3, 0.7):.2f}"')
    for _ in range(25):
        x, y = CX + rnd.uniform(-160, 260), rnd.uniform(top, GY)
        s.circle(round(x, 1), round(y, 1), 2.5, "#ffb050", extra=f'filter="{s.glow(2)}"')


# ═══════════════════════════ Слава — «Вторжение Бездны» ═══════════════════════════

@event("fallen_herald_event", "glory")
def ev_fallen_herald(s, rnd):
    y = GY + 40
    s.path(smooth([(CX - 320, y + 10), (CX - 260, y - 50), (CX - 100, y - 70), (CX + 120, y - 60), (CX + 260, y - 30), (CX + 300, y + 10)], k=0.6),
           s.lin([(0, "#e0c080"), (0.5, "#a08040"), (1, "#4a3418")]), INK, 6)
    s.circle(CX - 300, y - 30, 46, s.lin([(0, "#d0b070"), (1, "#5a4020")]), INK, 6)
    s.path(f"M{CX - 340},{y - 40} Q{CX - 300},{y - 100} {CX - 250},{y - 50}", "#c0a060", INK, 5)
    for k in range(4):
        s.path(f"M{CX - 200 + k * 110},{y - 64} V{y - 6}", "none", stroke="#5a4020", sw=3)
    s.path(f"M{CX + 40},{y - 60} L{CX + 30},{GY - 520}", "none", stroke=INK, sw=18)
    s.path(f"M{CX + 40},{y - 60} L{CX + 30},{GY - 520}", "none", stroke="#c8a050", sw=10)
    s.path(star(CX + 30, GY - 540, 20, 8, 4), "#ffe07a", INK, 3)
    flag = [(CX + 34, GY - 500), (CX + 330, GY - 470), (CX + 300, GY - 400), (CX + 340, GY - 320), (CX + 34, GY - 330)]
    s.path(smooth(flag, k=0.5), s.lin([(0, "#e8e0f8"), (1, "#7a6aa0")], 0, 0, 1, 0), INK, 5)
    s.path(star(CX + 180, GY - 410, 40, 16, 8), "#d4a640", INK, 3)
    s.path(f"M{CX + 260},{GY - 460} L{CX + 300},{GY - 420} M{CX + 220},{GY - 350} l50,10", "none", stroke="#9a4aff", sw=6, extra=f'filter="{s.glow(4)}"')
    glow_at(s, CX + 180, GY - 410, 200, "#ffe0a0", 0.25)


@event("void_merchant_event", "glory")
def ev_void_merchant(s, rnd):
    s.path(f"M{CX - 420},{GY + 60} L{CX - 340},{GY - 10} H{CX + 340} L{CX + 420},{GY + 60} Z", s.lin([(0, "#5a1a6a"), (1, "#1a0620")]), INK, 5)
    for k in range(6):
        s.path(f"M{CX - 380 + k * 150},{GY + 50} L{CX - 320 + k * 130},{GY}", "none", stroke="#d4a640", sw=3)
    body = [(CX - 260, GY + 10), (CX - 220, GY - 200), (CX - 120, GY - 380), (CX - 40, GY - 460), (CX + 40, GY - 460), (CX + 120, GY - 380),
            (CX + 220, GY - 200), (CX + 260, GY + 10)]
    s.path(smooth(body, k=0.7), "#7a3aff", stroke=None, extra=f'opacity="0.45" filter="{s.blur(30)}"')
    s.path(smooth(body, k=0.7), s.lin([(0, "#1a0a2a"), (1, "#020004")]), INK, 6)
    for k in range(5):
        s.path(f"M{CX - 200 + k * 100},{GY} Q{CX - 140 + k * 70},{GY - 200} {CX - 60 + k * 30},{GY - 400}", "none", stroke="#3a1a5a", sw=4)
    s.eye(CX - 36, GY - 380, 12, "#c08aff")
    s.eye(CX + 36, GY - 380, 12, "#c08aff")
    s.eye(CX, GY - 330, 8, "#c08aff")
    for side in (-1, 1):
        s.path(smooth([(CX + side * 120, GY - 300), (CX + side * 240, GY - 250), (CX + side * 320, GY - 180), (CX + side * 360, GY - 120)],
                      closed=False), "none", stroke=INK, sw=22)
        s.path(smooth([(CX + side * 120, GY - 300), (CX + side * 240, GY - 250), (CX + side * 320, GY - 180), (CX + side * 360, GY - 120)],
                      closed=False), "none", stroke="#140820", sw=14)
    for x, kind in ((CX - 300, 0), (CX - 160, 1), (CX + 170, 2), (CX + 310, 0)):
        y = GY + 20
        if kind == 0:
            s.path(f"M{x - 16},{y} L{x - 12},{y - 46} Q{x},{y - 70} {x + 12},{y - 46} L{x + 16},{y} Z", "#5ef0ff", INK, 3, extra=f'filter="{s.glow(4)}"')
        elif kind == 1:
            s.circle(x, y - 26, 26, s.rad([(0, "#ffffff"), (0.5, "#c08aff"), (1, "#3a0a6a")]), INK, 3, extra=f'filter="{s.glow(6)}"')
        else:
            s.path(f"M{x - 34},{y} L{x - 20},{y - 40} H{x + 20} L{x + 34},{y} Z", "#d4a640", INK, 3)
            s.circle(x, y - 22, 9, "#ff3a5a", INK, 2)
    specks(s, rnd, 40, "#d0a0ff", 1, 3, GY - 500, GY, CX - 300, CX + 300, glow=2)


@event("last_campfire_event", "glory")
def ev_last_campfire(s, rnd):
    full(s, "#05020a", 'opacity="0.35"')
    glow_at(s, CX, GY - 60, 420, "#ff8a30", 0.35, sd=120)
    for x, y, w, h in ((CX - 460, GY + 20, 200, 140), (CX + 420, GY + 30, 240, 120), (CX - 300, GY - 20, 90, 70)):
        s.path(poly([(x - w / 2, y), (x - w / 2 + 20, y - h), (x + w / 4, y - h + 20), (x + w / 2, y - h * 0.6), (x + w / 2, y)]),
               s.lin([(0, "#c0a070"), (1, "#3a2a1a")]), INK, 5)
    for rot in (-25, 25, 0):
        s.path(f"M{CX - 120},{GY + 20} H{CX + 120} V{GY} H{CX - 120} Z", s.lin([(0, "#7a4a24"), (1, "#2a1608")]), INK, 5,
               extra=f'transform="rotate({rot} {CX} {GY + 10})"')
    for k in range(9):
        a = math.pi + k * math.pi / 8
        s.ellipse(round(CX + 150 * math.cos(a), 1), round(GY + 30 + 30 * math.sin(a) * -0.3 + 10, 1), 24, 14, "#6a6058", INK, 3)
    s.path(f"M{CX - 90},{GY} Q{CX - 110},{GY - 120} {CX - 40},{GY - 200} Q{CX - 30},{GY - 120} {CX},{GY - 260} Q{CX + 40},{GY - 140} "
           f"{CX + 60},{GY - 190} Q{CX + 110},{GY - 100} {CX + 90},{GY} Z", s.lin([(0, "#ffd06a"), (0.5, "#ff7a20"), (1, "#c02a08")]),
           INK, 4, extra=f'filter="{s.glow(6)}"')
    s.path(f"M{CX - 40},{GY} Q{CX - 50},{GY - 70} {CX},{GY - 140} Q{CX + 50},{GY - 70} {CX + 40},{GY} Z", "#fff0b0", stroke=None,
           extra=f'filter="{s.glow(4)}"')
    for _ in range(40):
        x, y = CX + rnd.uniform(-120, 120), rnd.uniform(GY - 500, GY - 200)
        s.circle(round(x, 1), round(y, 1), round(rnd.uniform(1.5, 3.5), 1), "#ffb050", extra=f'filter="{s.glow(2)}"')
    for x, flip in ((CX - 260, 1), (CX + 280, -1)):
        s.path(f"M{x - 60 * flip},{GY + 40} Q{x - 40 * flip},{GY - 20} {x + 30 * flip},{GY + 10} L{x + 50 * flip},{GY + 50} Z", "#3a2a3a", INK, 4)


# ═══════════════════════════ вьючный зверь (10 §6, M5 п. 5) ═══════════════════════════

def mule(s: Svg, x, gy, k=1.0, flip=False, packs=True, rim=None):
    """Мул в профиль (мордой вправо; flip — влево): туловище, четыре ноги, шея, голова с длинными ушами, поклажа."""
    if flip:
        s.open(f'transform="translate({2 * x} 0) scale(-1 1)"')
    coat = s.lin([(0, "#8a7262"), (1, "#3e3028")], 0, 0, 0, 1)
    by = gy - 150 * k
    for lx, lean in ((-105, -6), (-70, 4), (70, -4), (105, 6)):
        s.path(poly([(x + (lx - 13) * k, by + 20 * k), (x + (lx + 13) * k, by + 20 * k),
                     (x + (lx + 9 + lean) * k, gy - 14 * k), (x + (lx - 9 + lean) * k, gy - 14 * k)]),
               "#4a3a30" if lx in (-70, 105) else "#5e4a3c", INK, max(3, 5 * k))
        s.path(f"M{x + (lx - 11 + lean) * k:.1f},{gy - 14 * k:.1f} h{22 * k:.1f} v{14 * k:.1f} h{-22 * k:.1f} Z", "#1e1612", INK, max(2, 3 * k))
    s.path(f"M{x - 150 * k:.1f},{by - 20 * k:.1f} q{-40 * k:.1f},{30 * k:.1f} {-30 * k:.1f},{110 * k:.1f}", "none", stroke=INK, sw=max(4, 9 * k))
    s.path(f"M{x - 150 * k:.1f},{by - 20 * k:.1f} q{-40 * k:.1f},{30 * k:.1f} {-30 * k:.1f},{110 * k:.1f}", "none", stroke="#3a2c24", sw=max(2, 5 * k))
    s.ellipse(x, by, 160 * k, 72 * k, coat, INK, max(3, 6 * k))
    neck = [(x + 110 * k, by - 30 * k), (x + 150 * k, by - 120 * k), (x + 200 * k, by - 150 * k),
            (x + 215 * k, by - 115 * k), (x + 175 * k, by - 70 * k), (x + 150 * k, by + 20 * k)]
    s.path(smooth(neck, k=0.5), coat, INK, max(3, 6 * k))
    head = [(x + 180 * k, by - 165 * k), (x + 225 * k, by - 175 * k), (x + 290 * k, by - 120 * k),
            (x + 300 * k, by - 95 * k), (x + 270 * k, by - 85 * k), (x + 205 * k, by - 115 * k)]
    s.path(smooth(head, k=0.6), s.lin([(0, "#9a8070"), (1, "#5a463a")]), INK, max(3, 6 * k))
    s.ellipse(x + 288 * k, by - 100 * k, 16 * k, 13 * k, "#c8b0a0", INK, max(2, 3 * k))
    for ex, tilt in ((190, -30), (210, -12)):
        s.path(f"M{x + ex * k:.1f},{by - 165 * k:.1f} q{tilt * k:.1f},{-70 * k:.1f} {(tilt + 22) * k:.1f},{-95 * k:.1f} "
               f"q{8 * k:.1f},{40 * k:.1f} {6 * k:.1f},{92 * k:.1f} Z", "#6a5446", INK, max(3, 4 * k))
    s.circle(x + 238 * k, by - 140 * k, 6 * k, "#140c08")
    s.circle(x + 240 * k, by - 142 * k, 2 * k, "#ffffff")
    s.path(f"M{x + 150 * k:.1f},{by - 120 * k:.1f} q{30 * k:.1f},{-30 * k:.1f} {55 * k:.1f},{-40 * k:.1f}", "none", stroke="#2a1e18", sw=max(3, 8 * k))
    if packs:
        for dx, w, h, col in ((-60, 90, 110, "#8a6a3a"), (40, 80, 100, "#7a5a32")):
            s.path(smooth([(x + (dx - w / 2) * k, by - 50 * k), (x + (dx + w / 2) * k, by - 50 * k),
                           (x + (dx + w / 2 + 8) * k, by + (h - 50) * k), (x + (dx - w / 2 - 8) * k, by + (h - 50) * k)], k=0.4),
                   s.lin([(0, col), (1, _tint(col, -0.45))]), INK, max(3, 5 * k))
            s.path(f"M{x + (dx - w / 2) * k:.1f},{by - 10 * k:.1f} H{x + (dx + w / 2) * k:.1f}", "none", stroke="#3a2412", sw=max(2, 4 * k))
        s.path(f"M{x - 110 * k:.1f},{by - 90 * k:.1f} H{x + 90 * k:.1f} a{22 * k:.1f},{22 * k:.1f} 0 0 1 0,{44 * k:.1f} "
               f"H{x - 110 * k:.1f} a{22 * k:.1f},{22 * k:.1f} 0 0 1 0,{-44 * k:.1f} Z", s.lin([(0, "#9a3a2a"), (1, "#4a1a12")]), INK, max(3, 5 * k))
    if rim:
        s.ellipse(x, by, 160 * k, 72 * k, "none", rim, max(2, 4 * k), extra=f'opacity="0.5" filter="{s.blur(3)}"')
    if flip:
        s.close()


@event("abandoned_mule_event", "barrows")
def ev_abandoned_mule(s, rnd):
    road = [(-40, GY + 30), (CX - 300, GY - 10), (CX + 200, GY + 5), (W + 40, GY - 20)]
    s.path(smooth(road, closed=False) + f" L{W + 40},{GY + 70} L-40,{GY + 90} Z", s.lin([(0, "#9fb4c4"), (1, "#6a8296")]), stroke=None,
           extra='opacity="0.8"')
    for k in range(9):
        x = CX - 520 + k * 130 + rnd.uniform(-20, 20)
        s.path(f"M{x},{GY + 30} q{rnd.uniform(-10, 10):.1f},-30 {rnd.uniform(-20, 20):.1f},-{rnd.uniform(40, 80):.1f}", "none",
               stroke="#5a4a3a", sw=3)
    mule(s, CX - 40, GY + 20, 1.35)
    s.path(f"M{CX + 360},{GY - 120} L{CX + 420},{GY - 300}", "none", stroke=INK, sw=14)
    s.path(f"M{CX + 360},{GY - 120} L{CX + 420},{GY - 300}", "none", stroke="#6a4a2c", sw=8)
    s.path(f"M{CX + 405},{GY - 300} h70 v50 h-70 Z", s.lin([(0, "#8a6a42"), (1, "#4a3420")]), INK, 5,
           extra=f'transform="rotate(-8 {CX + 440} {GY - 275})"')
    s.path(f"M{CX + 240},{GY - 110} Q{CX + 310},{GY - 160} {CX + 370},{GY - 140}", "none", stroke="#c8a070", sw=4)
    glow_at(s, CX - 40, GY - 260, 260, "#bfe8ff", 0.18)


@event("mule_ambush_event", "glory")
def ev_mule_ambush(s, rnd):
    mule(s, CX + 40, GY + 10, 1.1, flip=True)
    for x, h, lean, flip in ((CX - 470, 360, 10, False), (CX + 470, 380, -14, True), (CX + 300, 300, -6, True)):
        hx, top, hr, w = figure(s, x, GY + 20, h, robe=("#3a2a2e", "#120a0e"), eyes="#ff5a40", lean=lean, rim="#ff6a50")
        bow = -1 if flip else 1
        s.path(f"M{hx + bow * w * 0.6},{top + h * 0.18} q{bow * 60},{h * 0.2} 0,{h * 0.4}", "none", stroke=INK, sw=10)
        s.path(f"M{hx + bow * w * 0.6},{top + h * 0.18} q{bow * 60},{h * 0.2} 0,{h * 0.4}", "none", stroke="#7a5030", sw=5)
        s.path(f"M{hx + bow * w * 0.6},{top + h * 0.18} v{h * 0.4}", "none", stroke="#d8d0c0", sw=2)
    for _ in range(9):
        x0, y0 = rnd.uniform(80, W - 80), rnd.uniform(140, GY - 220)
        ang = rnd.uniform(-0.5, 0.5) + (0 if x0 < CX else math.pi)
        dx, dy = math.cos(ang) * 120, math.sin(ang) * 120 + 30
        s.path(f"M{x0:.1f},{y0:.1f} l{dx:.1f},{dy:.1f}", "none", stroke="#e8d8b8", sw=3, extra=f'filter="{s.glow(3)}"')
        s.path(f"M{x0 + dx:.1f},{y0 + dy:.1f} l{-dx * 0.12 - dy * 0.06:.1f},{-dy * 0.12 + dx * 0.06:.1f} "
               f"m{dy * 0.12:.1f},{-dx * 0.12:.1f} l{-dx * 0.12 + dy * 0.06:.1f},{-dy * 0.12 - dx * 0.06:.1f}", "none",
               stroke="#ffb090", sw=3)
    for x, y in ((CX - 160, GY + 40), (CX + 220, GY + 50), (CX - 20, GY + 60)):
        s.path(f"M{x - 40},{y} q20,-30 40,-20 q30,-20 46,6 q-10,24 -46,24 Z", "#8a6a3a", INK, 4)
        specks(s, rnd, 8, "#e8c060", 2, 4, y - 10, y + 20, x - 50, x + 60, glow=2)
    glow_at(s, CX + 40, GY - 200, 300, "#ff5030", 0.2)


# ═══════════════════════════ Камень Памяти и подтипы узлов (M5 п. 6) ═══════════════════════════

def candle(s: Svg, x, y, h, w=22, wax="#efe4c8", flame="#ffd27a"):
    """Свеча с подтёками воска и пламенем; y — низ свечи."""
    s.path(f"M{x - w / 2},{y} V{y - h} H{x + w / 2} V{y} Z", s.lin([(0, wax), (1, _tint(wax, -0.35))], 0, 0, 1, 0), INK, 3)
    for dx, dl in ((-w * 0.3, h * 0.35), (w * 0.2, h * 0.22)):
        s.path(f"M{x + dx - 3},{y - h} v{dl} q3,8 6,0 v{-dl} Z", wax, INK, 2)
    s.path(f"M{x},{y - h} v-8", "none", stroke=INK, sw=2)
    s.path(f"M{x},{y - h - 6} q-{w * 0.55},-{w * 0.9} 0,-{w * 2.2} q{w * 0.55},{w * 1.3} 0,{w * 2.2} Z", flame, stroke=None,
           extra=f'filter="{s.glow(w * 0.4)}"')
    s.path(f"M{x},{y - h - 8} q-{w * 0.22},-{w * 0.5} 0,-{w * 1.1} q{w * 0.22},{w * 0.6} 0,{w * 1.1} Z", "#ffffff", stroke=None)


@event("crypt_candle_event", "barrows")
def ev_crypt_candle(s, rnd):
    # Вход в склеп: каменная арка, в проёме — тьма и надгробная плита со свечой.
    ax, aw, ah = CX, 560, 470
    s.path(f"M{ax - aw / 2 - 70},{GY + 20} V{GY - ah + 120} Q{ax},{GY - ah - 110} {ax + aw / 2 + 70},{GY - ah + 120} V{GY + 20} Z",
           s.lin([(0, "#6a7884"), (1, "#26303a")]), INK, 8)
    s.path(f"M{ax - aw / 2},{GY + 20} V{GY - ah + 150} Q{ax},{GY - ah - 40} {ax + aw / 2},{GY - ah + 150} V{GY + 20} Z",
           s.lin([(0, "#05080c"), (1, "#0e161e")]), INK, 6)
    for k in range(-3, 4):
        s.path(f"M{ax + k * 92},{GY - ah + 100 + abs(k) * abs(k) * 9} v-46", "none", stroke=INK, sw=4)
    glow_at(s, ax, GY - 200, 300, "#ffb860", 0.35)
    # Призрачный лик Камня Памяти в свете пламени.
    s.path(poly([(ax - 70, GY - 150), (ax - 52, GY - 360), (ax, GY - 400), (ax + 54, GY - 352), (ax + 72, GY - 150)]),
           "#9fb4c8", stroke="#cfe6ff", sw=3, extra='opacity="0.22"')
    for k in range(3):
        y = GY - 330 + k * 52
        s.path(f"M{ax - 18},{y} l18,-22 l18,22 M{ax},{y - 22} v-12", "none", stroke="#9ff0ff", sw=4,
               extra=f'opacity="0.5" filter="{s.glow(5)}"')
    # Плита и свеча.
    s.path(f"M{ax - 210},{GY + 20} L{ax - 190},{GY - 90} H{ax + 190} L{ax + 210},{GY + 20} Z", s.lin([(0, "#8a96a0"), (1, "#3a444e")]), INK, 6)
    s.path(f"M{ax - 230},{GY - 90} H{ax + 230} V{GY - 120} H{ax - 230} Z", s.lin([(0, "#a8b4be"), (1, "#56626c")]), INK, 6)
    for k in range(-2, 3):
        s.path(f"M{ax + k * 56 - 10},{GY - 30} l10,-18 l10,18", "none", stroke="#2a343e", sw=4)
    s.ellipse(ax, GY - 122, 46, 10, "#e8dcc0", INK, 3)
    candle(s, ax, GY - 124, 120, 26)
    glow_at(s, ax, GY - 290, 120, "#fff0c0", 0.55)
    # Кости и паутина у входа.
    for x, y, r in ((ax - 320, GY + 30, -20), (ax + 300, GY + 36, 15)):
        s.path(f"M{x - 40},{y} L{x + 40},{y - 8}", "none", stroke=INK, sw=12, extra=f'transform="rotate({r} {x} {y})"')
        s.path(f"M{x - 40},{y} L{x + 40},{y - 8}", "none", stroke="#e8e0cc", sw=7, extra=f'transform="rotate({r} {x} {y})"')
    s.circle(ax + 360, GY + 10, 26, "#e8e0cc", INK, 4)
    s.circle(ax + 352, GY + 6, 6, INK)
    s.circle(ax + 368, GY + 6, 6, INK)


@event("library_event", "tower")
def ev_library(s, rnd):
    # Высокие полки по сторонам.
    for side in (-1, 1):
        for d in range(3):
            x = CX + side * (300 + d * 190)
            h = 580 - d * 50
            s.path(f"M{x - 85},{GY + 10} V{GY - h} H{x + 85} V{GY + 10} Z", s.lin([(0, "#4a3220"), (1, "#140a06")]), INK, 5)
            for r in range(7):
                y = GY - h + 24 + r * (h - 30) / 7
                s.path(f"M{x - 85},{y + 62} H{x + 85}", "none", stroke=INK, sw=5)
                xx = x - 78
                while xx < x + 70:
                    bw = rnd.randint(9, 17)
                    col = rnd.choice(("#7a3a2a", "#2a4a6a", "#5a2a4a", "#3a5a3a", "#8a6a2a", "#4a3a6a"))
                    top = y + 12 + rnd.randint(0, 14)
                    s.path(f"M{xx},{y + 60} V{top} H{xx + bw} V{y + 60} Z", col, INK, 1.5)
                    s.path(f"M{xx + 2},{top + 8} H{xx + bw - 2}", "none", stroke="#d4b45a", sw=1.5)
                    xx += bw + 2
    # Пюпитр с раскрытой книгой и слепой архивариус.
    s.path(f"M{CX - 30},{GY + 10} L{CX - 18},{GY - 170} H{CX + 18} L{CX + 30},{GY + 10} Z", "#5a3a22", INK, 5)
    s.path(f"M{CX - 140},{GY - 160} L{CX + 140},{GY - 200} L{CX + 128},{GY - 226} L{CX - 152},{GY - 186} Z", "#6a4428", INK, 5)
    s.path(f"M{CX - 128},{GY - 192} L{CX},{GY - 178} L{CX + 120},{GY - 214} L{CX + 120},{GY - 262} L{CX},{GY - 228} "
           f"L{CX - 128},{GY - 242} Z", "#f2e6c8", INK, 3)
    for k in range(5):
        s.path(f"M{CX - 112},{GY - 228 + k * 9} L{CX - 18},{GY - 216 + k * 9}", "none", stroke="#8a7a60", sw=2)
        s.path(f"M{CX + 18},{GY - 220 + k * 9} L{CX + 104},{GY - 246 + k * 9}", "none", stroke="#8a7a60", sw=2)
    hx, top, hr, w = figure(s, CX + 250, GY + 20, 400, robe=("#4a4a5a", "#14141e"), lean=-18)
    s.path(f"M{hx - hr * 0.6},{top + hr * 1.05} H{hx + hr * 0.6}", "none", stroke="#d8d0c0", sw=hr * 0.28)
    s.path(f"M{hx - w * 0.5},{top + 190} L{CX + 120},{GY - 236}", "none", stroke=INK, sw=6)
    s.path(f"M{CX + 120},{GY - 236} l-30,-60", "none", stroke="#f0f0f0", sw=4)
    for x in (CX - 210, CX - 160):
        candle(s, x, GY - 186 + (x - CX + 210) * 0.12, 54, 16)
    glow_at(s, CX - 180, GY - 290, 200, "#ffcf80", 0.3)
    specks(s, rnd, 40, "#ffe8b0", 1, 2.4, GY - 520, GY - 120, CX - 360, CX + 360, glow=2)


# ═══════════════════════════ запуск ═══════════════════════════

def build(key) -> Svg:
    act, fn = REGISTRY[key]
    bg, fg, tint = ACTS[act]
    s = Svg(W, H)
    seed = sum(ord(c) for c in key)
    bg(s, random.Random(seed))
    fn(s, random.Random(seed * 7 + 1))
    fg(s, random.Random(seed * 13 + 3))
    finish(s, tint, seed % 97)
    return s


def check(keys) -> int:
    bad = []
    for key in keys:
        svg = os.path.join(OUT_SVG, f"{key}.svg")
        jpg = os.path.join(OUT_JPG, f"{key}.jpg")
        if not os.path.exists(svg) or open(svg, encoding="utf-8").read() != build(key).render():
            bad.append(f"{os.path.relpath(svg, ROOT)}: не совпадает с генератором (запустите с --render)")
        if not os.path.exists(jpg):
            bad.append(f"{os.path.relpath(jpg, ROOT)}: нет картинки")
    for line in bad:
        print("✗", line)
    print(f"события: {len(keys)}, ошибок: {len(bad)}")
    return 1 if bad else 0


def main(argv) -> int:
    render = "--render" in argv
    keys = [a for a in argv[1:] if not a.startswith("--")] or sorted(REGISTRY)
    if "--check" in argv:
        return check(keys)
    os.makedirs(OUT_SVG, exist_ok=True)
    pairs = []
    tmp = tempfile.mkdtemp()
    for key in keys:
        svg = os.path.join(OUT_SVG, f"{key}.svg")
        with open(svg, "w", encoding="utf-8") as f:
            f.write(build(key).render())
        pairs += [svg, os.path.join(tmp, f"{key}.png")]
    print(f"SVG: {len(keys)} → {os.path.relpath(OUT_SVG, ROOT)}")
    if not render:
        return 0
    env = dict(os.environ, NODE_PATH=os.environ.get("NODE_PATH", "/tmp/r/node_modules"))
    subprocess.run(["node", os.path.join(ROOT, "tools", "art", "render_svg.mjs"), *pairs], check=True, env=env)
    from PIL import Image
    os.makedirs(OUT_JPG, exist_ok=True)
    for key in keys:
        Image.open(os.path.join(tmp, f"{key}.png")).convert("RGB").save(os.path.join(OUT_JPG, f"{key}.jpg"), quality=86, optimize=True)
    print(f"JPG: {len(keys)} → {os.path.relpath(OUT_JPG, ROOT)}")
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv))
