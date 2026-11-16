#!/usr/bin/env python3
"""Фоны актов мода (M7 п. 2, art spec §3.10): SVG-генератор → art/acts/{act}/*.svg → mod/SlayTheRequiem/images/acts/{act}/.

Для 2а Ледяные Курганы, 2б Заводные Глубины, 4а Кровавый Двор, 4б Звёздная Башня и ★ Безымянный предел:

    combat.jpg     2048×960   боевой фон (стена/небо + пол), как ванильные слои images/rooms/* — растягивается на 2881×1351
                              вокруг центра узла NCombatBackground (Acts/ActLooks.cs); пол начинается с y≈470, враги стоят на ~640.
    combat_fg.png  2048×960   передний план боя (прозрачный): обломки по нижним углам, частицы.
    camp.jpg       3072×1440  лагерь сверху (как ванильный RestSiteBG, прямоугольник −444,−139 … 2320.8,1157): поляна у костра
                              (костёр в точке 1538,966 картинки), рамка из предметов по краям. Брёвна, огонь и свет — ванильные узлы.
    map_top.png / map_middle.png / map_bottom.png  2036×1440  пергамент карты цвета MapBgColor со сгибами, рамкой и водяными знаками
                              акта; края по бокам непрерывны между кусками.

Стиль — как у сцен «двух дверей» и событий: толстый тёмный контур, градиенты, контровой свет цвета акта, зерно.
Случайность — только от фиксированных зёрен, SVG воспроизводимы (CI сверяет art/acts с генератором).

    python3 tools/art/acts/build_acts.py                      # только SVG
    python3 tools/art/acts/build_acts.py --render             # + JPG/PNG (resvg: npm i --prefix /tmp/r @resvg/resvg-js@2)
    python3 tools/art/acts/build_acts.py frozen_barrows       # один акт
    python3 tools/art/acts/build_acts.py --check              # CI: SVG совпадают с генератором, картинки на месте
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
OUT_SVG = os.path.join(ROOT, "art", "acts")
OUT_IMG = os.path.join(ROOT, "mod", "SlayTheRequiem", "images", "acts")

# Бой: холст ванильного слоя; видимое окно 1920×1080 при масштабе 1.407 — x 342…1706, y 96…864.
CW, CH = 2048, 960
CCX = CW // 2
GY = 470          # основание стены / горизонт
VP = (CCX, 250)   # точка схода пола
# Лагерь: картинка RestSiteBG; видно x 493…2627, y 154…1354; костёр (центр брёвен) — FIRE.
KW, KH = 3072, 1440
FIRE = (1538, 966)
VIEW = (493, 154, 2627, 1354)
# Карта: кусок пергамента; бумага по x 60…1976, верх куска «top» с y 48, низ «bottom» до 1392.
MW, MH = 2036, 1440
PX0, PX1 = 60, 1976

# Ключи актов = SuperbossLookRules-подобные snake_case имена (Core/ActLookRules.ArtKey).
ACTS = ["frozen_barrows", "clockwork_depths", "blood_court", "star_tower", "nameless_limit"]
PIECES = {"combat": "jpg", "combat_fg": "png", "camp": "jpg", "map_top": "png", "map_middle": "png", "map_bottom": "png"}
SIZES = {"combat": (CW, CH), "combat_fg": (CW, CH), "camp": (KW, KH), "map_top": (MW, MH), "map_middle": (MW, MH),
         "map_bottom": (MW, MH)}

# Цвет пергамента = MapBgColor акта (Acts/RequiemActs.cs), рамка и водяные знаки.
PAPER = {
    "frozen_barrows": ("#9DB0C4", "#e8fbff", "#2f4c66"),
    "clockwork_depths": ("#B89A63", "#ffe2a0", "#5a3a14"),
    "blood_court": ("#A86A6A", "#ffd0c0", "#5a0c18"),
    "star_tower": ("#8C9AC8", "#f0e8ff", "#2a2f6a"),
    "nameless_limit": ("#6A6278", "#d8c8ff", "#1e1628"),
}


# ═══════════════════════════ общие примитивы ═══════════════════════════

def rect(s: Svg, fill, extra="", x=0, y=0, w=None, h=None):
    w = s.w if w is None else w
    h = s.h if h is None else h
    s.path(f"M{x},{y} H{x + w} V{y + h} H{x} Z", fill, stroke=None, extra=extra)


def glow_at(s: Svg, x, y, r, color, opacity=0.5, sd=None):
    s.circle(x, y, r, color, extra=f'opacity="{opacity}" filter="{s.blur(sd or r * 0.45)}"')


def ridge(rnd, y, amp, step=60, x0=-40, x1=CW + 40):
    pts, x = [], x0
    while x <= x1:
        pts.append((round(x, 1), round(y - rnd.uniform(0, amp), 1)))
        x += step * rnd.uniform(0.6, 1.3)
    pts.append((x1, round(y - rnd.uniform(0, amp), 1)))
    return pts


def below(pts, bottom):
    return poly(pts + [(pts[-1][0], bottom), (pts[0][0], bottom)])


def specks(s: Svg, rnd, n, color, rmin, rmax, y0=0, y1=None, x0=0, x1=None, op=(0.4, 0.95), glow=0.0):
    y1 = s.h if y1 is None else y1
    x1 = s.w if x1 is None else x1
    f = f' filter="{s.glow(glow)}"' if glow else ""
    for _ in range(n):
        s.circle(round(rnd.uniform(x0, x1), 1), round(rnd.uniform(y0, y1), 1), round(rnd.uniform(rmin, rmax), 2), color,
                 extra=f'opacity="{rnd.uniform(*op):.2f}"{f}')


def finish(s: Svg, tint, seed, vignette=0.55, grain=0.4):
    rect(s, s.rad([(0.55, "#000", 0), (1, "#000", vignette)], cx=0.5, cy=0.5, r=0.75))
    rect(s, tint, 'opacity="0.05"')
    rect(s, "#808080", f'opacity="{grain}" filter="{s.grain(0.3, 0.8, seed)}" style="mix-blend-mode:overlay"')


def floor(s: Svg, top, bottom, line, rows=7, cols=13, op=0.55, sw=3):
    """Пол в перспективе: от горизонта GY до низа, лучи к точке схода и ряды с растущим шагом."""
    s.path(f"M-20,{GY} H{CW + 20} V{CH} H-20 Z", s.lin([(0, top), (1, bottom)]), stroke=None)
    for i in range(cols + 1):
        x = -900 + i * (CW + 1800) / cols
        x0 = VP[0] + (x - VP[0]) * (GY - VP[1]) / (CH + 400 - VP[1])
        s.path(f"M{x0:.1f},{GY} L{x:.1f},{CH + 400}", stroke=line, sw=sw, extra=f'opacity="{op}"')
    for k in range(1, rows + 1):
        t = (k / rows) ** 1.8
        y = GY + t * (CH - GY + 60)
        s.path(f"M-20,{y:.1f} H{CW + 20}", stroke=line, sw=sw * (0.6 + t), extra=f'opacity="{op}"')


def drift(s: Svg, x, y, w, h, top="#eef8ff", bottom="#9fb8cc"):
    s.path(smooth([(x - w, y + 10), (x - w * 0.6, y - h * 0.6), (x - w * 0.1, y - h), (x + w * 0.5, y - h * 0.7),
                   (x + w, y + 10)], closed=False) + " Z", s.lin([(0, top), (1, bottom)]), INK, 4)


def rune_stone(s: Svg, x, gy, h, lean=0, glow="#7ff2ff", stone=("#7a8c9a", "#2a3640")):
    s.path(poly([(x - h * 0.15, gy), (x - h * 0.11 + lean, gy - h), (x + h * 0.11 + lean, gy - h - h * 0.05), (x + h * 0.15, gy)]),
           s.lin([(0, stone[0]), (1, stone[1])], 0, 0, 1, 0), INK, 5)
    for k in range(3):
        yy = gy - h * 0.3 - k * h * 0.18
        xx = x + lean * (gy - yy) / h
        s.path(f"M{xx - 7:.1f},{yy:.1f} l7,-12 l7,12 M{xx:.1f},{yy - 12:.1f} v-10", stroke=glow, sw=3, extra=f'filter="{s.glow(4)}"')
    s.path(f"M{x - h * 0.2},{gy - 4} q{h * 0.2},-14 {h * 0.4},0", stroke="#eef8ff", sw=10)


def candle(s: Svg, x, y, h, w=18, wax="#efe4c8", flame="#ffd27a"):
    s.path(f"M{x - w / 2},{y} V{y - h} H{x + w / 2} V{y} Z", s.lin([(0, wax), (1, _tint(wax, -0.35))], 0, 0, 1, 0), INK, 3)
    s.path(f"M{x},{y - h - 4} q-{w * 0.55},-{w * 0.9} 0,-{w * 2.2} q{w * 0.55},{w * 1.3} 0,{w * 2.2} Z", flame, stroke=None,
           extra=f'filter="{s.glow(w * 0.4)}"')
    s.path(f"M{x},{y - h - 6} q-{w * 0.22},-{w * 0.5} 0,-{w * 1.1} q{w * 0.22},{w * 0.6} 0,{w * 1.1} Z", "#ffffff", stroke=None)


def crystal(s: Svg, x, y, r, rot, col="#9ad8ff", op=0.9):
    s.path(poly([(x, y - r), (x + r * 0.45, y - r * 0.1), (x + r * 0.2, y + r * 0.9), (x - r * 0.2, y + r * 0.9), (x - r * 0.45, y - r * 0.1)]),
           s.lin([(0, "#ffffff"), (0.4, col), (1, _tint(col, -0.6))], 0, 0, 1, 1), INK, 3,
           extra=f'opacity="{op}" transform="rotate({rot} {x} {y})"')


def book_stack(s: Svg, rnd, x, y, n, w=120):
    for i in range(n):
        bw = w * rnd.uniform(0.75, 1.0)
        dx = rnd.uniform(-12, 12)
        col = rnd.choice(("#7a3a2a", "#2a4a6a", "#5a2a4a", "#3a5a3a", "#8a6a2a", "#4a3a6a"))
        yy = y - i * 22
        s.path(f"M{x - bw / 2 + dx:.1f},{yy} h{bw:.1f} v-22 h{-bw:.1f} Z", col, INK, 3)
        s.path(f"M{x - bw / 2 + dx + 6:.1f},{yy - 11} h{bw - 12:.1f}", stroke="#d4b45a", sw=2)


def pipe(s: Svg, d, w, col="#8a5a2a", hi="#ffe0a8"):
    s.path(d, stroke=INK, sw=w + 10)
    s.path(d, stroke=col, sw=w)
    s.path(d, stroke=hi, sw=max(2, w * 0.12), extra='opacity="0.45" transform="translate(0,-%s)"' % round(w * 0.25, 1))


def steam(s: Svg, x, y, r, n=4, op=0.35, rnd=None):
    rnd = rnd or random.Random(int(x + y))
    for k in range(n):
        s.circle(round(x + rnd.uniform(-r, r) * 0.6, 1), round(y - k * r * 0.7, 1), round(r * (0.7 + k * 0.25), 1), "#e8e0d8",
                 extra=f'opacity="{op * (1 - k / (n + 1)):.2f}" filter="{s.blur(r * 0.35)}"')


# ═══════════════════════════ 2а — Ледяные Курганы ═══════════════════════════

def combat_frozen_barrows(s: Svg, rnd):
    rect(s, s.lin([(0, "#040c18"), (0.45, "#12304a"), (0.75, "#3d6a86"), (1, "#8fb0c4")]), x=0, y=0, w=CW, h=GY + 40)
    for i, (col, y, a) in enumerate((("#5df2c6", 150, 0.5), ("#62b6ff", 210, 0.38), ("#a98bff", 120, 0.22))):
        pts = [(-60, y + 60), (500, y - 50 + i * 20), (1020, y + 30), (1560, y - 70), (CW + 60, y + 20)]
        s.path(smooth(pts, closed=False), stroke=col, sw=90 - i * 16, extra=f'opacity="{a}" filter="{s.blur(28)}"')
    specks(s, rnd, 220, "#ffffff", 0.6, 1.9, 0, 330)
    glow_at(s, 1500, 120, 60, "#eef8ff", 0.9, 6)
    s.circle(1500, 120, 46, s.rad([(0, "#ffffff"), (1, "#cfe6f5")]), INK, 3)
    s.circle(1486, 112, 9, "#b8d0e0", extra='opacity="0.6"')
    for y, amp, col in ((360, 170, "#2c4a64"), (420, 110, "#1d3348"), (460, 60, "#13243a")):
        pts = ridge(rnd, y, amp, 80)
        s.path(below(pts, GY + 40), col, stroke=None)
        for x, yy in pts:
            if yy < y - amp * 0.55:
                s.path(poly([(x - 34, yy + 40), (x, yy), (x + 30, yy + 36), (x + 6, yy + 24)]), "#cfe6f5", stroke=None,
                       extra='opacity="0.8"')
    # курганы с входами в склепы
    for x, w, h in ((420, 330, 150), (1180, 420, 190), (1760, 300, 130)):
        s.path(f"M{x - w},{GY + 8} Q{x},{GY - h * 2} {x + w},{GY + 8} Z", s.lin([(0, "#e8f4fb"), (0.3, "#9fb8cc"), (1, "#3a5268")]), INK, 6)
        s.path(f"M{x - 58},{GY + 8} V{GY - 62} Q{x},{GY - 130} {x + 58},{GY - 62} V{GY + 8} Z", s.lin([(0, "#8a96a0"), (1, "#3a444e")]), INK, 5)
        s.path(f"M{x - 38},{GY + 8} V{GY - 56} Q{x},{GY - 104} {x + 38},{GY - 56} V{GY + 8} Z", "#05080c", INK, 4)
        glow_at(s, x, GY - 30, 46, "#7ff2ff", 0.35)
    for x, h, lean in ((180, 150, -6), (300, 96, 4), (840, 120, 3), (1540, 140, -3), (1930, 110, 5)):
        rune_stone(s, x, GY + 14, h, lean)
    s.ellipse(CCX, GY + 10, 1300, 50, "#bcd8ea", extra=f'opacity="0.35" filter="{s.blur(24)}"')
    floor(s, "#d7e8f2", "#6f8ea6", "#a8c4d8", rows=6, cols=11, op=0.35, sw=2.5)
    # лёд и трещины
    for x, y, rx, ry in ((520, 640, 260, 46), (1500, 700, 320, 56), (1020, 820, 380, 60)):
        s.ellipse(x, y, rx, ry, s.lin([(0, "#e8fbff", 0.85), (1, "#8fd0f0", 0.6)]), "#5a8aa8", 3)
        for k in range(4):
            a = rnd.uniform(0, math.pi)
            s.path(f"M{x},{y} l{rx * 0.8 * math.cos(a):.1f},{ry * 0.8 * math.sin(a) * rnd.choice((-1, 1)):.1f}", stroke="#4a7a9a", sw=2,
                   extra='opacity="0.7"')
    for x, y, r in ((300, 560, 26), (1750, 600, 30), (760, 900, 34), (1300, 560, 20)):
        s.ellipse(x, y, r * 1.5, r * 0.8, s.lin([(0, "#9aa8b4"), (1, "#3a4652")]), INK, 3)
        s.ellipse(x - r * 0.3, y - r * 0.5, r, r * 0.3, "#eef8ff", extra='opacity="0.9"')


def fg_frozen_barrows(s: Svg, rnd):
    drift(s, 120, CH + 20, 300, 170)
    rune_stone(s, 210, CH - 40, 260, -24, stone=("#6a7c8a", "#1e2a34"))
    drift(s, 1940, CH + 20, 320, 150)
    for x, y, r in ((1760, CH - 70, -20), (1830, CH - 90, 35)):
        s.path(f"M{x - 70},{y} L{x + 70},{y - 10}", stroke=INK, sw=14, extra=f'transform="rotate({r} {x} {y})"')
        s.path(f"M{x - 70},{y} L{x + 70},{y - 10}", stroke="#e8e0cc", sw=8, extra=f'transform="rotate({r} {x} {y})"')
    s.circle(1880, CH - 60, 34, "#e8e0cc", INK, 4)
    s.circle(1870, CH - 64, 8, INK)
    s.circle(1892, CH - 64, 8, INK)
    s.path(f"M1690,{CH} L1730,{CH - 330}", stroke=INK, sw=12)
    s.path(f"M1690,{CH} L1730,{CH - 330}", stroke="#5a4a3a", sw=6)
    s.path(poly([(1722, CH - 330), (1742, CH - 400), (1746, CH - 326)]), "#cfe6f5", INK, 3)
    specks(s, rnd, 260, "#ffffff", 1.0, 3.8, op=(0.45, 0.95))


def camp_frozen_barrows(s: Svg, rnd):
    rect(s, s.lin([(0, "#0a1626"), (0.25, "#24405a"), (1, "#56728a")]))
    for _ in range(120):
        x, y = rnd.uniform(0, KW), rnd.uniform(260, KH)
        s.ellipse(round(x), round(y), round(rnd.uniform(60, 220)), round(rnd.uniform(16, 50)), rnd.choice(("#d8e8f2", "#a8c0d2", "#7e9ab0")),
                  extra=f'opacity="{rnd.uniform(0.25, 0.6):.2f}"')
    s.ellipse(*FIRE, 900, 380, "#e6f0f6", extra=f'opacity="0.45" filter="{s.blur(60)}"')
    s.ellipse(FIRE[0], FIRE[1] + 30, 520, 200, "#5a5040", extra=f'opacity="0.35" filter="{s.blur(40)}"')
    # дальний край: курган со входом, стоячие камни, мёртвые деревья
    s.path(f"M900,420 Q1540,-40 2200,420 Z", s.lin([(0, "#e8f4fb"), (0.4, "#9fb8cc"), (1, "#3a5268")]), INK, 8)
    s.path(f"M1460,420 V320 Q1540,230 1620,320 V420 Z", "#05080c", INK, 6)
    glow_at(s, 1540, 360, 90, "#7ff2ff", 0.35)
    for x, h, lean in ((780, 230, -8), (900, 160, 6), (2240, 210, 6), (2370, 150, -5)):
        rune_stone(s, x, 470, h, lean)
    for x, side in ((620, 1), (2470, -1)):
        s.path(f"M{x},560 L{x + side * 20},200 M{x + side * 10},360 l{side * 90},-80 M{x + side * 16},280 l{-side * 70},-70",
               stroke=INK, sw=16)
        s.path(f"M{x},560 L{x + side * 20},200 M{x + side * 10},360 l{side * 90},-80 M{x + side * 16},280 l{-side * 70},-70",
               stroke="#3a3028", sw=8)
    # боковые сугробы, камни, кости
    for x, y, w, h in ((560, 900, 260, 160), (520, 1250, 300, 200), (2560, 880, 260, 150), (2580, 1260, 320, 190),
                       (1000, 1420, 360, 120), (2100, 1420, 380, 130)):
        drift(s, x, y, w, h)
    for x, y in ((760, 1100), (2340, 1120), (1180, 1300), (1950, 1320)):
        s.ellipse(x, y, 50, 28, s.lin([(0, "#9aa8b4"), (1, "#3a4652")]), INK, 4)
        s.ellipse(x - 10, y - 14, 32, 9, "#eef8ff")
    s.path("M2280,1230 l120,-30", stroke=INK, sw=16)
    s.path("M2280,1230 l120,-30", stroke="#e8e0cc", sw=9)
    specks(s, rnd, 200, "#ffffff", 1.2, 3.6, op=(0.3, 0.8))


# ═══════════════════════════ 2б — Заводные Глубины ═══════════════════════════

def combat_clockwork_depths(s: Svg, rnd):
    rect(s, s.lin([(0, "#0d0704"), (0.55, "#2a170b"), (0.9, "#5a3416"), (1, "#3a2210")]), w=CW, h=GY + 20)
    for cx, cy, r, t, op in ((260, 200, 260, 22, 0.32), (1780, 240, 230, 20, 0.3), (1024, 60, 170, 16, 0.25), (1420, 380, 120, 12, 0.22),
                             (620, 360, 110, 12, 0.22)):
        s.open(f'opacity="{op}"')
        _gear(s, cx, cy, r, t, "#6a4220", stroke="#1a0e05", sw=8, hole="#1a0e05", rot=rnd.uniform(0, 30))
        s.close()
    # топка в глубине
    s.path(f"M{CCX - 170},{GY} V{GY - 170} Q{CCX},{GY - 330} {CCX + 170},{GY - 170} V{GY} Z", s.lin([(0, "#6a4a2a"), (1, "#2a1608")]), INK, 8)
    s.path(f"M{CCX - 120},{GY} V{GY - 150} Q{CCX},{GY - 270} {CCX + 120},{GY - 150} V{GY} Z",
           s.rad([(0, "#ffe7a0"), (0.4, "#ff8a2a"), (1, "#5a1406")], cy=0.9, r=0.8), INK, 5)
    for k in range(-2, 3):
        s.path(f"M{CCX + k * 46},{GY} V{GY - 190 + abs(k) * 20}", stroke="#2a1006", sw=10)
    glow_at(s, CCX, GY - 120, 300, "#ff8a2a", 0.3)
    for y, w, col in ((300, 46, "#8a5a2a"), (380, 28, "#b07a3a")):
        pipe(s, f"M-20,{y} H{CCX - 200} M{CCX + 200},{y} H{CW + 20}", w, col)
        for x in list(range(40, CCX - 200, 120)) + list(range(CCX + 240, CW, 120)):
            s.path(f"M{x},{y - w / 2 - 6} v{w + 12}", stroke=INK, sw=12)
            s.path(f"M{x},{y - w / 2 - 6} v{w + 12}", stroke="#d8a04e", sw=6)
    for x in (360, 1688):
        s.path(f"M{x},0 V{GY}", stroke=INK, sw=56)
        s.path(f"M{x},0 V{GY}", stroke="#7a4a20", sw=44)
        s.path(f"M{x - 10},0 V{GY}", stroke="#c08a4a", sw=5, extra='opacity="0.6"')
        for y in range(40, GY, 70):
            s.circle(x, y, 6, "#e8b860", INK, 2)
    for x in (560, 1490):
        s.path(f"M{x},0 V{140 + (x % 7) * 10}", stroke="#3a2a1a", sw=8)
        s.circle(x, 150 + (x % 7) * 10, 18, "none", "#3a2a1a", 7)
    for x, y, r in ((500, 260, 60), (1580, 330, 80), (820, 200, 46)):
        steam(s, x, y, r, rnd=rnd)
    floor(s, "#4a2e16", "#140a04", "#0a0502", rows=6, cols=12, op=0.7, sw=3)
    s.path(f"M-20,{GY} H{CW + 20}", stroke="#d8a04e", sw=4, extra='opacity="0.6"')
    for k in range(-5, 6):
        x = CCX + k * 190
        s.circle(x, GY + 24, 5, "#d8a04e", INK, 2)
    s.ellipse(CCX, GY + 160, 600, 90, "#ff8a2a", extra=f'opacity="0.18" filter="{s.blur(50)}"')
    for x, y in ((420, 620), (1640, 660), (980, 780)):
        s.ellipse(x, y, 90, 26, "#0a0502", extra='opacity="0.6"')
        s.ellipse(x, y - 3, 80, 20, "none", "#8a5a2a", 3)


def fg_clockwork_depths(s: Svg, rnd):
    pipe(s, f"M-20,{CH - 210} H160 Q240,{CH - 210} 240,{CH - 130} V{CH + 20}", 70, "#7a4a20")
    s.circle(240, CH - 260, 58, "none", INK, 18)
    s.circle(240, CH - 260, 58, "none", "#c03a20", 10)
    for k in range(4):
        a = k * math.pi / 2
        s.path(f"M240,{CH - 260} l{58 * math.cos(a):.1f},{58 * math.sin(a):.1f}", stroke="#c03a20", sw=8)
    s.circle(240, CH - 260, 12, "#d8a04e", INK, 3)
    steam(s, 140, CH - 270, 60, 5, 0.4, rnd)
    s.path(f"M1760,{CH + 20} V{CH - 180} H1980 V{CH + 20} Z", s.lin([(0, "#8a5a2a"), (1, "#3a2010")]), INK, 6)
    s.path(f"M1760,{CH - 180} L1980,{CH + 20} M1980,{CH - 180} L1760,{CH + 20}", stroke="#2a1608", sw=8)
    s.open('opacity="0.95"')
    _gear(s, 1900, CH - 230, 70, 10, "#b07a3a", sw=5, hole="#1a0e05", rot=8)
    _gear(s, 1990, CH - 290, 46, 8, "#8a5a2a", sw=4, hole="#1a0e05", rot=20)
    s.close()
    specks(s, rnd, 90, "#ffb050", 1.0, 3.0, 250, CH, op=(0.5, 1.0), glow=1.6)


def camp_clockwork_depths(s: Svg, rnd):
    rect(s, s.lin([(0, "#120a04"), (0.3, "#3a2410"), (1, "#5a3a1c")]))
    for y in range(300, KH, 130):
        s.path(f"M0,{y} H{KW}", stroke="#140a04", sw=6, extra='opacity="0.8"')
        off = 0 if (y // 130) % 2 else 120
        for x in range(off, KW, 240):
            s.path(f"M{x},{y} v130", stroke="#140a04", sw=6, extra='opacity="0.8"')
            for dx, dy in ((14, 14), (226, 14), (14, 116), (226, 116)):
                s.circle(x + dx, y + dy, 5, "#b07a3a", extra='opacity="0.7"')
    s.ellipse(*FIRE, 820, 340, "#ffb060", extra=f'opacity="0.18" filter="{s.blur(70)}"')
    # дальняя стена: трубы, котёл, шестерни
    rect(s, s.lin([(0, "#0d0704"), (1, "#2a170b")]), x=0, y=0, w=KW, h=380)
    for cx, cy, r in ((1000, 220, 200), (2080, 190, 170), (1540, 120, 120)):
        s.open('opacity="0.45"')
        _gear(s, cx, cy, r, 18, "#6a4220", stroke="#1a0e05", sw=8, hole="#1a0e05", rot=cx % 30)
        s.close()
    for y, w in ((300, 50), (360, 30)):
        pipe(s, f"M0,{y} H{KW}", w)
    s.path("M1400,380 V180 Q1540,60 1680,180 V380 Z", s.rad([(0, "#ffe7a0"), (0.5, "#ff8a2a"), (1, "#5a1406")], cy=0.9), INK, 8)
    glow_at(s, 1540, 300, 200, "#ff8a2a", 0.3)
    steam(s, 900, 260, 70, rnd=rnd)
    steam(s, 2200, 240, 80, rnd=rnd)
    # по бокам — ящики, бочки, шестерни
    for x, y, w, h in ((560, 760, 220, 180), (640, 1180, 260, 200), (2420, 780, 230, 170), (2380, 1200, 260, 210)):
        s.path(f"M{x - w / 2},{y} h{w} v{-h} h{-w} Z", s.lin([(0, "#8a5a2a"), (1, "#3a2010")]), INK, 6)
        s.path(f"M{x - w / 2},{y - h} L{x + w / 2},{y} M{x + w / 2},{y - h} L{x - w / 2},{y}", stroke="#2a1608", sw=8)
    for x, y in ((820, 1000), (2220, 1020), (1040, 1360), (2060, 1370)):
        s.ellipse(x, y, 70, 70, s.rad([(0, "#b07a3a"), (1, "#4a2a10")]), INK, 5)
        s.ellipse(x, y, 70, 20, "none", "#2a1608", 5)
    s.open('opacity="0.85"')
    _gear(s, 760, 1360, 90, 12, "#8a5a2a", sw=5, hole="#1a0e05")
    _gear(s, 2330, 980, 70, 10, "#b07a3a", sw=5, hole="#1a0e05", rot=10)
    s.close()
    specks(s, rnd, 80, "#ffb050", 1.2, 3.2, 300, KH, op=(0.4, 0.9), glow=1.5)


# ═══════════════════════════ 4а — Кровавый Двор ═══════════════════════════

def gothic_window(s: Svg, x, top, w, bottom, moon=False):
    s.path(f"M{x - w / 2 - 20},{bottom} V{top + w * 0.6} Q{x},{top - w * 0.4} {x + w / 2 + 20},{top + w * 0.6} V{bottom} Z",
           s.lin([(0, "#3a0c1a"), (1, "#120208")]), INK, 6)
    s.path(f"M{x - w / 2},{bottom - 16} V{top + w * 0.6} Q{x},{top - w * 0.2} {x + w / 2},{top + w * 0.6} V{bottom - 16} Z",
           s.lin([(0, "#3a1030"), (0.6, "#8a2a2a"), (1, "#2a0610")]), INK, 4)
    if moon:
        s.circle(x + w * 0.12, top + w * 0.7, w * 0.26, s.rad([(0, "#ffd0b0"), (0.6, "#e04030"), (1, "#7a0a10")]),
                 extra=f'filter="{s.glow(10)}"')
    s.path(f"M{x},{bottom - 16} V{top + w * 0.05} M{x - w / 2},{top + w * 1.2} H{x + w / 2} M{x - w / 2},{(top + bottom) / 2 + w * 0.4} H{x + w / 2}",
           stroke=INK, sw=6)


def combat_blood_court(s: Svg, rnd):
    rect(s, s.lin([(0, "#14030a"), (0.6, "#3a0812"), (1, "#1a0408")]), w=CW, h=GY + 20)
    for i, x in enumerate((360, 760, 1288, 1688)):
        gothic_window(s, x, 120, 180, GY - 90, moon=i in (1, 2))
    glow_at(s, 900, 260, 280, "#e04030", 0.18)
    # люстры
    for x in (560, 1488):
        s.path(f"M{x},0 V110", stroke=INK, sw=6)
        s.path(f"M{x - 110},150 Q{x},210 {x + 110},150", stroke=INK, sw=12)
        s.path(f"M{x - 110},150 Q{x},210 {x + 110},150", stroke="#d4a640", sw=6)
        s.path(f"M{x},110 L{x - 110},150 M{x},110 L{x + 110},150 M{x},110 V190", stroke="#d4a640", sw=4)
        for dx in (-110, -55, 0, 55, 110):
            candle(s, x + dx, 158 + (30 if abs(dx) == 55 else 0) * 0.6 + (40 if dx == 0 else 0), 30, 12)
        glow_at(s, x, 160, 160, "#ffcf80", 0.22)
    # шторы
    for side in (-1, 1):
        x = 0 if side < 0 else CW
        pts = [(x, -10), (x + side * 280, 0), (x + side * 200, 200), (x + side * 150, 400), (x + side * 220, GY + 40), (x, GY + 40)]
        s.path(smooth(pts), s.lin([(0, "#a0142a"), (1, "#3a0410")], 0, 0, 1, 0), INK, 5)
        for k in (50, 110, 170):
            s.path(f"M{x + side * k},0 Q{x + side * (k + 30)},300 {x + side * (k - 10)},{GY + 30}", stroke="#5a0614", sw=4)
        s.path(f"M{x + side * 160},{260} q{side * 40},20 {side * 80},0", stroke="#d4a640", sw=8)
    # панели
    s.path(f"M-20,{GY - 80} H{CW + 20} V{GY} H-20 Z", s.lin([(0, "#3a0c14"), (1, "#14030a")]), INK, 4)
    for x in range(0, CW, 128):
        s.path(f"M{x + 12},{GY - 68} h104 v56 h-104 Z", "none", "#6a1a24", 3)
    s.path(f"M-20,{GY - 80} H{CW + 20}", stroke="#d4a640", sw=4)
    # шахматный мрамор и ковёр
    s.path(f"M-20,{GY} H{CW + 20} V{CH} H-20 Z", s.lin([(0, "#2a0a10"), (1, "#0a0204")]), stroke=None)
    rows, cols = 8, 16
    for r in range(rows):
        t0, t1 = (r / rows) ** 1.7, ((r + 1) / rows) ** 1.7
        y0, y1 = GY + t0 * (CH - GY + 80), GY + t1 * (CH - GY + 80)
        for c in range(cols):
            if (r + c) % 2:
                continue
            xs = []
            for yy in (y0, y1):
                k = (yy - VP[1]) / (CH + 80 - VP[1])
                xs.append([VP[0] + (-1500 + cc * (CW + 3000) / cols - VP[0]) * k for cc in (c, c + 1)])
            s.path(poly([(xs[0][0], y0), (xs[0][1], y0), (xs[1][1], y1), (xs[1][0], y1)]), "#5a1a24", stroke=None, extra='opacity="0.75"')
    k0, k1 = (GY - VP[1]) / (CH + 80 - VP[1]), 1.0
    s.path(poly([(VP[0] - 260 * k0, GY), (VP[0] + 260 * k0, GY), (VP[0] + 520, CH + 80), (VP[0] - 520, CH + 80)]),
           s.lin([(0, "#7a0a1a"), (1, "#3a0410")]), INK, 4)
    for d in (30, 46):
        s.path(f"M{VP[0] - (260 - d) * k0:.1f},{GY} L{VP[0] - 520 + d * 1.6:.1f},{CH + 80} M{VP[0] + (260 - d) * k0:.1f},{GY} L{VP[0] + 520 - d * 1.6:.1f},{CH + 80}",
               stroke="#d4a640", sw=4 if d == 30 else 2)
    s.ellipse(900, GY + 140, 500, 80, "#ff5040", extra=f'opacity="0.12" filter="{s.blur(40)}"')


def fg_blood_court(s: Svg, rnd):
    for x in (170, 1880):
        s.path(f"M{x - 70},{CH + 10} L{x - 40},{CH - 40} H{x + 40} L{x + 70},{CH + 10} Z", "#8a6a2a", INK, 5)
        s.path(f"M{x},{CH - 40} V{CH - 330}", stroke=INK, sw=18)
        s.path(f"M{x},{CH - 40} V{CH - 330}", stroke="#d4a640", sw=10)
        s.path(f"M{x - 120},{CH - 330} Q{x},{CH - 250} {x + 120},{CH - 330}", stroke=INK, sw=14)
        s.path(f"M{x - 120},{CH - 330} Q{x},{CH - 250} {x + 120},{CH - 330}", stroke="#d4a640", sw=7)
        for dx in (-120, 0, 120):
            candle(s, x + dx, CH - 330 - (20 if dx == 0 else 0), 70, 20)
        glow_at(s, x, CH - 420, 200, "#ffcf80", 0.3)
    for _ in range(60):
        x, y = rnd.uniform(0, CW), rnd.uniform(300, CH)
        r = rnd.uniform(5, 11)
        s.ellipse(round(x, 1), round(y, 1), round(r, 1), round(r * 0.55, 1), rnd.choice(("#c0142e", "#8a0a1a", "#e0303a")),
                  extra=f'opacity="{rnd.uniform(0.5, 0.9):.2f}" transform="rotate({rnd.randint(0, 180)} {x:.1f} {y:.1f})"')


def camp_blood_court(s: Svg, rnd):
    rect(s, s.lin([(0, "#0e0206"), (0.3, "#2a0810"), (1, "#3a1018")]))
    tile = 150
    for y in range(380, KH, tile):
        for x in range(0, KW, tile):
            if ((x + y) // tile) % 2:
                s.path(f"M{x},{y} h{tile} v{tile} h{-tile} Z", "#5a1a24", stroke=None, extra='opacity="0.55"')
    s.ellipse(*FIRE, 640, 290, s.rad([(0, "#9a1a2a"), (0.8, "#6a0a1a"), (1, "#3a0410")]), INK, 6)
    s.ellipse(*FIRE, 600, 260, "none", "#d4a640", 6)
    s.ellipse(*FIRE, 560, 230, "none", "#d4a640", 2.5)
    for k in range(16):
        a = k * math.pi / 8
        s.path(star(FIRE[0] + 580 * math.cos(a), FIRE[1] + 245 * math.sin(a), 12, 5, 4), "#d4a640", stroke=None)
    rect(s, s.lin([(0, "#14030a"), (1, "#3a0812")]), w=KW, h=400)
    for i, x in enumerate((820, 1240, 1840, 2260)):
        gothic_window(s, x, 30, 200, 380, moon=i == 1)
    s.path(f"M0,380 H{KW} V420 H0 Z", "#3a0c14", INK, 5)
    s.path(f"M0,380 H{KW}", stroke="#d4a640", sw=5)
    for x in (600, 2480):
        s.path(f"M{x - 70},{KH} V420 H{x + 70} V{KH} Z", s.lin([(0, "#5a1a24"), (0.5, "#8a3a40"), (1, "#2a0610")], 0, 0, 1, 0), INK, 7)
        for y in (520, 1000):
            s.path(f"M{x - 80},{y} h160", stroke="#d4a640", sw=8)
    for x, y in ((900, 1250), (2180, 1260)):
        s.path(f"M{x - 60},{y} L{x - 40},{y - 40} H{x + 40} L{x + 60},{y} Z", "#8a6a2a", INK, 4)
        candle(s, x, y - 40, 60, 18)
        glow_at(s, x, y - 120, 120, "#ffcf80", 0.3)
    for _ in range(90):
        x, y = rnd.uniform(500, 2600), rnd.uniform(500, KH)
        s.ellipse(round(x), round(y), 9, 5, rnd.choice(("#c0142e", "#8a0a1a")), extra=f'opacity="{rnd.uniform(0.4, 0.8):.2f}"')


# ═══════════════════════════ 4б — Звёздная Башня ═══════════════════════════

def orrery(s: Svg, x, y, r, rot=0):
    for k, (rr, col) in enumerate(((r, "#d4b45a"), (r * 0.72, "#9aa8ff"), (r * 0.45, "#5ef0ff"))):
        s.ellipse(x, y, rr, rr * 0.32, "none", INK, 10, extra=f'transform="rotate({rot + k * 14} {x} {y})"')
        s.ellipse(x, y, rr, rr * 0.32, "none", col, 4, extra=f'transform="rotate({rot + k * 14} {x} {y})"')
        a = math.radians(40 + k * 110)
        px, py = x + rr * math.cos(a), y + rr * 0.32 * math.sin(a)
        s.circle(px, py, 14 - k * 3, s.rad([(0, "#ffffff"), (1, col)]), INK, 3, extra=f'transform="rotate({rot + k * 14} {x} {y})"')
    s.circle(x, y, r * 0.12, s.rad([(0, "#fff8d0"), (0.6, "#ffd27a"), (1, "#c08a2a")]), INK, 4, extra=f'filter="{s.glow(6)}"')


def combat_star_tower(s: Svg, rnd):
    rect(s, s.lin([(0, "#05061a"), (0.55, "#12164a"), (0.9, "#3a2a78"), (1, "#1a1640")]), w=CW, h=GY + 20)
    for x, y, r, col, op in ((360, 200, 300, "#7a3aff", 0.3), (1700, 240, 280, "#2ab0ff", 0.26), (1024, 100, 200, "#ff6ad8", 0.18)):
        glow_at(s, x, y, r, col, op, 80)
    specks(s, rnd, 320, "#ffffff", 0.5, 1.8, 0, GY - 40)
    for x, y, r in ((250, 140, 9), (1800, 120, 8), (1420, 80, 6), (560, 360, 6), (1600, 400, 7)):
        s.path(star(x, y, r * 2, r * 0.5, 4), "#ffffff", stroke=None, extra=f'filter="{s.glow(4)}"')
    # рёбра купола
    for k in range(-4, 5):
        x = CCX + k * 300
        s.path(f"M{CCX},-200 Q{CCX + k * 120},40 {x},{GY - 80}", stroke=INK, sw=24)
        s.path(f"M{CCX},-200 Q{CCX + k * 120},40 {x},{GY - 80}", stroke="#3a3a6a", sw=14)
    s.path(f"M-20,140 Q{CCX},40 {CW + 20},140", stroke=INK, sw=20)
    s.path(f"M-20,140 Q{CCX},40 {CW + 20},140", stroke="#4a4a8a", sw=10)
    orrery(s, CCX, 230, 330, -8)
    for x, y, r, rot in ((240, 380, 40, 20), (1800, 360, 50, -15), (520, 210, 26, 40), (1560, 180, 30, -30)):
        crystal(s, x, y, r, rot, op=0.8)
    # полки у стены
    for side in (-1, 1):
        for d in range(2):
            x = CCX + side * (720 + d * 200)
            s.path(f"M{x - 90},{GY} V{GY - 220 + d * 20} H{x + 90} V{GY} Z", s.lin([(0, "#3a3060"), (1, "#100c24")]), INK, 5)
            for r in range(4):
                y = GY - 200 + d * 20 + r * 50
                s.path(f"M{x - 90},{y + 48} H{x + 90}", stroke=INK, sw=4)
                xx = x - 84
                while xx < x + 74:
                    bw = rnd.randint(9, 16)
                    col = rnd.choice(("#7a3a2a", "#2a4a6a", "#5a2a4a", "#3a5a3a", "#8a6a2a", "#4a3a6a"))
                    s.path(f"M{xx},{y + 46} V{y + 12 + rnd.randint(0, 12)} H{xx + bw} V{y + 46} Z", col, INK, 1.5)
                    xx += bw + 2
    s.path(f"M-20,{GY - 10} H{CW + 20} V{GY + 10} H-20 Z", "#2a2a5a", INK, 4)
    floor(s, "#2a2a5a", "#08081a", "#4a4a8a", rows=7, cols=14, op=0.55, sw=2.5)
    cy = GY + 210
    for rx, col, sw in ((720, "#9aa8ff", 4), (560, "#d4b45a", 3.5), (380, "#5ef0ff", 3)):
        s.ellipse(CCX, cy, rx, rx * 0.16, "none", col, sw, extra='opacity="0.75"')
    for k in range(16):
        a = k * math.pi / 8
        s.path(star(CCX + 640 * math.cos(a), cy + 102 * math.sin(a), 10, 4, 4), "#bff6ff", stroke=None, extra='opacity="0.85"')
    s.ellipse(CCX, cy, 720, 120, "#5ef0ff", extra=f'opacity="0.1" filter="{s.blur(30)}"')


def fg_star_tower(s: Svg, rnd):
    for x, y, r, rot in ((120, CH - 60, 120, -12), (230, CH - 40, 80, 18), (40, CH - 150, 70, -30)):
        crystal(s, x, y, r, rot, "#c88aff")
    glow_at(s, 140, CH - 120, 160, "#c88aff", 0.25)
    book_stack(s, rnd, 1820, CH + 10, 7, 150)
    s.path(f"M1960,{CH + 10} L1900,{CH - 260}", stroke=INK, sw=12)
    s.path(f"M1960,{CH + 10} L1900,{CH - 260}", stroke="#8a6a2a", sw=6)
    s.path(poly([(1860, CH - 250), (1960, CH - 330), (1990, CH - 300), (1890, CH - 220)]), s.lin([(0, "#d4b45a"), (1, "#6a4a1a")]), INK, 4)
    specks(s, rnd, 70, "#bff6ff", 1.0, 2.8, 250, CH, op=(0.5, 1.0), glow=2)


def camp_star_tower(s: Svg, rnd):
    rect(s, s.lin([(0, "#05061a"), (0.3, "#16184a"), (1, "#22244e")]))
    for y in range(400, KH, 120):
        s.path(f"M0,{y} H{KW}", stroke="#3a3a6a", sw=3, extra='opacity="0.6"')
    for x in range(0, KW, 160):
        s.path(f"M{x},400 V{KH}", stroke="#3a3a6a", sw=3, extra='opacity="0.6"')
    for rx, col in ((700, "#9aa8ff"), (560, "#d4b45a"), (420, "#5ef0ff")):
        s.ellipse(*FIRE, rx, rx * 0.42, "none", col, 5, extra='opacity="0.75"')
    for k in range(12):
        a = k * math.pi / 6
        s.path(star(FIRE[0] + 630 * math.cos(a), FIRE[1] + 265 * math.sin(a), 16, 6, 4), "#bff6ff", stroke=None, extra='opacity="0.85"')
    s.ellipse(*FIRE, 700, 300, "#5ef0ff", extra=f'opacity="0.08" filter="{s.blur(40)}"')
    rect(s, s.lin([(0, "#05061a"), (1, "#12164a")]), w=KW, h=400)
    specks(s, rnd, 160, "#ffffff", 0.6, 2.0, 0, 380, 900, 2200)
    s.path("M900,400 V160 Q1540,-140 2180,160 V400", stroke=INK, sw=22)
    s.path("M900,400 V160 Q1540,-140 2180,160 V400", stroke="#4a4a8a", sw=12)
    for k in range(1, 6):
        x = 900 + k * 213
        s.path(f"M{x},400 V{160 - math.sin(k / 6 * math.pi) * 150:.1f}", stroke="#3a3a6a", sw=8)
    orrery(s, 1540, 210, 260, -6)
    for side, x0 in ((-1, 520), (1, 2560)):
        for d in range(3):
            x = x0 - side * d * 190
            s.path(f"M{x - 85},420 V60 H{x + 85} V420 Z", s.lin([(0, "#3a3060"), (1, "#100c24")]), INK, 5)
            for r in range(6):
                y = 70 + r * 58
                s.path(f"M{x - 85},{y + 56} H{x + 85}", stroke=INK, sw=4)
                xx = x - 80
                while xx < x + 70:
                    bw = rnd.randint(9, 16)
                    col = rnd.choice(("#7a3a2a", "#2a4a6a", "#5a2a4a", "#3a5a3a", "#8a6a2a", "#4a3a6a"))
                    s.path(f"M{xx},{y + 54} V{y + 16 + rnd.randint(0, 12)} H{xx + bw} V{y + 54} Z", col, INK, 1.5)
                    xx += bw + 2
    for x, y, n in ((640, 900, 6), (700, 1280, 8), (2420, 920, 7), (2380, 1300, 5), (1000, 1380, 4), (2080, 1390, 5)):
        book_stack(s, rnd, x, y, n, 140)
    for x, y, r, rot in ((560, 640, 70, -10), (2520, 660, 80, 14)):
        crystal(s, x, y, r, rot, "#c88aff")
        glow_at(s, x, y, 120, "#c88aff", 0.2)
    specks(s, rnd, 80, "#bff6ff", 1.0, 3.0, 400, KH, op=(0.4, 0.9), glow=2)


# ═══════════════════════════ ★ Безымянный предел ═══════════════════════════

def island(s: Svg, rnd, x, y, w, h):
    top = [(x - w, y), (x - w * 0.5, y - h * 0.15), (x, y - h * 0.08), (x + w * 0.5, y - h * 0.18), (x + w, y)]
    bot = [(x + w * 0.7, y + h * 0.4), (x + w * 0.2, y + h), (x - w * 0.3, y + h * 0.6), (x - w * 0.8, y + h * 0.3)]
    s.path(poly(top + bot), s.lin([(0, "#3a3246"), (1, "#0a0810")]), INK, 4)
    s.path(smooth(top, closed=False), stroke="#9a6aff", sw=2, extra='opacity="0.6"')


def combat_nameless_limit(s: Svg, rnd):
    rect(s, s.lin([(0, "#020104"), (0.5, "#0e0818"), (0.85, "#22143a"), (1, "#0a0610")]))
    for x, y, r, col, op in ((1024, 160, 380, "#5a2a9a", 0.35), (300, 300, 260, "#2a1a5a", 0.3), (1760, 280, 260, "#3a1a6a", 0.3)):
        glow_at(s, x, y, r, col, op, 90)
    specks(s, rnd, 240, "#c8b4ff", 0.5, 1.6, 0, GY + 200)
    # чёрное солнце
    s.circle(CCX, 190, 150, "#9a6aff", extra=f'opacity="0.55" filter="{s.blur(30)}"')
    s.circle(CCX, 190, 118, "#000000", "#e8d8ff", 3)
    for k in range(24):
        a = k * math.pi / 12
        s.path(f"M{CCX + 126 * math.cos(a):.1f},{190 + 126 * math.sin(a):.1f} l{30 * math.cos(a):.1f},{30 * math.sin(a):.1f}",
               stroke="#c8a8ff", sw=3, extra='opacity="0.6"')
    for x, y, w, h in ((260, 330, 160, 90), (1820, 300, 190, 110), (600, 180, 90, 50), (1500, 150, 110, 60), (1250, 400, 80, 40)):
        island(s, rnd, x, y, w, h)
    # обломки колонн с короной-трещиной
    for x, h in ((520, 300), (760, 220), (1300, 260), (1560, 320)):
        s.path(poly([(x - 40, GY + 10), (x - 34, GY - h), (x - 10, GY - h - 30), (x + 8, GY - h + 10), (x + 34, GY - h - 18), (x + 40, GY + 10)]),
               s.lin([(0, "#4a4258"), (1, "#14101c")], 0, 0, 1, 0), INK, 5)
        s.path(f"M{x - 4},{GY - h + 10} L{x + 8},{GY - h * 0.6} L{x - 6},{GY - h * 0.3}", stroke="#c8a8ff", sw=3, extra=f'filter="{s.glow(3)}"')
    # платформа над пустотой
    plat = [(-60, GY + 20), (400, GY - 6), (1024, GY + 4), (1640, GY - 8), (CW + 60, GY + 20), (CW + 60, CH + 20), (-60, CH + 20)]
    s.path(smooth(plat[:5], closed=False) + f" L{CW + 60},{CH + 20} L-60,{CH + 20} Z", s.lin([(0, "#2a2436"), (1, "#060408")]), INK, 5)
    for _ in range(10):
        x0, y0 = rnd.uniform(200, CW - 200), rnd.uniform(GY + 60, CH - 40)
        pts = [(x0, y0)]
        for _k in range(4):
            x0 += rnd.uniform(-120, 120)
            y0 += rnd.uniform(-20, 40)
            pts.append((round(x0, 1), round(y0, 1)))
        d = "M" + " L".join(f"{x},{y}" for x, y in pts)
        s.path(d, stroke="#9a6aff", sw=6, extra=f'opacity="0.55" filter="{s.blur(3)}"')
        s.path(d, stroke="#e8d8ff", sw=2)
    s.ellipse(CCX, GY + 120, 700, 90, "#5a2a9a", extra=f'opacity="0.2" filter="{s.blur(40)}"')


def fg_nameless_limit(s: Svg, rnd):
    for x, y, r, rot in ((140, CH - 120, 70, 30), (260, CH - 40, 50, -20), (1880, CH - 100, 80, -25), (1990, CH - 220, 44, 40)):
        s.path(poly([(x, y - r), (x + r * 0.5, y), (x, y + r * 0.6), (x - r * 0.4, y)]), s.lin([(0, "#4a4258"), (1, "#0a0810")]), INK, 4,
               extra=f'transform="rotate({rot} {x} {y})"')
    glow_at(s, 200, CH - 80, 180, "#5a2a9a", 0.3)
    glow_at(s, 1900, CH - 120, 180, "#5a2a9a", 0.3)
    specks(s, rnd, 90, "#c8b4ff", 1.0, 3.0, 200, CH, op=(0.4, 0.9), glow=2)


def camp_nameless_limit(s: Svg, rnd):
    rect(s, s.lin([(0, "#020104"), (0.5, "#120a1e"), (1, "#0a0610")]))
    specks(s, rnd, 260, "#c8b4ff", 0.6, 2.2)
    glow_at(s, 1540, 160, 420, "#5a2a9a", 0.35, 100)
    s.circle(1540, 170, 110, "#000000", "#e8d8ff", 3)
    for x, y, w, h in ((700, 260, 150, 80), (2380, 300, 170, 90), (1060, 120, 90, 50), (2040, 110, 100, 50)):
        island(s, rnd, x, y, w, h)
    plat = [(560, 640), (1000, 470), (1540, 430), (2080, 470), (2520, 640), (2640, 1000), (2500, 1450), (580, 1450), (430, 1000)]
    s.path(smooth(plat), s.rad([(0, "#3a3248"), (0.7, "#1e1828"), (1, "#0e0a14")], cx=0.5, cy=0.6, r=0.6), INK, 8)
    s.path(smooth(plat), stroke="#9a6aff", sw=4, extra=f'opacity="0.55" filter="{s.blur(4)}"')
    for _ in range(14):
        x0, y0 = rnd.uniform(700, 2400), rnd.uniform(560, 1400)
        if abs(x0 - FIRE[0]) < 380 and abs(y0 - FIRE[1]) < 200:
            continue
        pts = [(x0, y0)]
        for _k in range(4):
            x0 += rnd.uniform(-140, 140)
            y0 += rnd.uniform(-60, 60)
            pts.append((round(x0, 1), round(y0, 1)))
        d = "M" + " L".join(f"{x:.1f},{y:.1f}" for x, y in pts)
        s.path(d, stroke="#9a6aff", sw=7, extra=f'opacity="0.5" filter="{s.blur(3)}"')
        s.path(d, stroke="#e8d8ff", sw=2.5)
    for x, y, h in ((760, 760, 260), (2340, 780, 240), (900, 1280, 180), (2180, 1300, 200)):
        s.path(poly([(x - 44, y), (x - 36, y - h), (x - 10, y - h - 30), (x + 10, y - h + 10), (x + 36, y - h - 20), (x + 44, y)]),
               s.lin([(0, "#4a4258"), (1, "#14101c")], 0, 0, 1, 0), INK, 5)


# ═══════════════════════════ карта: пергамент ═══════════════════════════

def side_x(gy, left, act):
    """Край бумаги на глобальной высоте gy (три куска подряд) — непрерывен между кусками."""
    k = ACTS.index(act) + 1
    w = 10 * math.sin(gy / 260 + k) + 6 * math.sin(gy / 97 + 2 * k)
    return (PX0 + w) if left else (PX1 + w * 0.8)


def motif(s: Svg, act, x, y, r, col):
    if act == "frozen_barrows":
        for k in range(6):
            a = k * math.pi / 3
            s.path(f"M{x},{y} l{r * math.cos(a):.1f},{r * math.sin(a):.1f}", stroke=col, sw=4)
            mx, my = x + r * 0.6 * math.cos(a), y + r * 0.6 * math.sin(a)
            for da in (-0.6, 0.6):
                s.path(f"M{mx:.1f},{my:.1f} l{r * 0.3 * math.cos(a + da):.1f},{r * 0.3 * math.sin(a + da):.1f}", stroke=col, sw=3)
    elif act == "clockwork_depths":
        _gear(s, x, y, r, 10, "none", stroke=col, sw=4, hole="none")
    elif act == "blood_court":
        for k in range(5):
            a = k * 2 * math.pi / 5
            s.circle(x + r * 0.4 * math.cos(a), y + r * 0.4 * math.sin(a), r * 0.45, "none", col, 3)
        s.circle(x, y, r * 0.25, "none", col, 3)
        s.path(f"M{x},{y + r * 0.8} q{-r * 0.2},{r * 0.6} {r * 0.1},{r * 1.2}", stroke=col, sw=3)
    elif act == "star_tower":
        rnd = random.Random(int(x * 7 + y))
        pts = [(x + rnd.uniform(-r, r), y + rnd.uniform(-r, r)) for _ in range(5)]
        s.path("M" + " L".join(f"{px:.1f},{py:.1f}" for px, py in pts), stroke=col, sw=2.5)
        for px, py in pts:
            s.path(star(px, py, 10, 3.5, 4), col, stroke=None)
    else:
        rnd = random.Random(int(x * 3 + y))
        x0, y0 = x - r, y
        d = f"M{x0:.1f},{y0:.1f}"
        for _ in range(5):
            x0 += r * 0.4
            y0 += rnd.uniform(-r * 0.4, r * 0.4)
            d += f" L{x0:.1f},{y0:.1f}"
        s.path(d, stroke=col, sw=4)


def map_piece(s: Svg, act, index):
    """index 0 — верх, 1 — середина, 2 — низ."""
    base, light, dark = PAPER[act]
    rnd = random.Random(ACTS.index(act) * 31 + index)
    y0 = 48 if index == 0 else 0
    y1 = 1392 if index == 2 else MH
    ys = list(range(y0, y1 + 1, 40))
    if ys[-1] != y1:
        ys.append(y1)
    # обрезанные (не рваные) концы уводим за холст, чтобы обводка не рисовала шов между кусками
    ys_out = ([] if index == 0 else [-8]) + ys + ([] if index == 2 else [MH + 8])
    left = [(round(side_x(index * MH + y, True, act), 1), y) for y in ys_out]
    right = [(round(side_x(index * MH + y, False, act), 1), y) for y in reversed(ys_out)]

    def torn(y, sign):
        pts, x = [], PX1 + 10
        while x > PX0 - 10:
            pts.append((round(x, 1), round(y + sign * rnd.uniform(0, 14), 1)))
            x -= rnd.uniform(30, 70)
        return pts

    # обход: левый край вниз → низ слева направо → правый край вверх → верх справа налево
    bottom = torn(y1, -1)[::-1] if index == 2 else []
    top = torn(y0, 1) if index == 0 else []
    paper = poly(left + bottom + right + top)
    s.path(paper, s.lin([(0, _tint(base, 0.12)), (0.5, base), (1, _tint(base, -0.08))], 0, 0, 1, 0), stroke=None)
    # пятна и потёртости
    for _ in range(14):
        x, y = rnd.uniform(PX0 + 100, PX1 - 100), rnd.uniform(y0 + 60, y1 - 60)
        s.ellipse(round(x), round(y), round(rnd.uniform(80, 260)), round(rnd.uniform(40, 160)), _tint(base, rnd.choice((-0.12, 0.1))),
                  extra=f'opacity="{rnd.uniform(0.12, 0.28):.2f}" filter="{s.blur(40)}"')
    # водяные знаки акта
    for _ in range(7):
        motif(s, act, round(rnd.uniform(PX0 + 160, PX1 - 160)), round(rnd.uniform(y0 + 140, y1 - 140)), round(rnd.uniform(50, 110)),
              _tint(base, -0.25))
    # сгибы (по два на кусок, как у ванильных карт)
    for fy in (480, 960):
        fl = round(side_x(index * MH + fy, True, act) + 4, 1)
        fr = round(side_x(index * MH + fy, False, act) - 4, 1)
        s.path(f"M{fl},{fy} Q{MW / 2},{fy + rnd.uniform(-12, 12):.1f} {fr},{fy}", stroke=_tint(base, -0.22), sw=5,
               extra='opacity="0.55"')
        s.path(f"M{fl},{fy + 6} Q{MW / 2},{fy + 6} {fr},{fy + 6}", stroke=_tint(base, 0.25), sw=3, extra='opacity="0.5"')
    # рамка вдоль краёв (двойная линия) и уголки
    for off, sw, col in ((28, 4, light), (40, 2, light)):
        for left_side in (True, False):
            pts = [(round(side_x(index * MH + y, left_side, act) + (off if left_side else -off), 1), y) for y in ys]
            if index == 0:
                pts = [p for p in pts if p[1] >= y0 + off]
            if index == 2:
                pts = [p for p in pts if p[1] <= y1 - off]
            s.path("M" + " L".join(f"{x},{y}" for x, y in pts), stroke=col, sw=sw, extra='opacity="0.75"')
    if index in (0, 2):
        yy = y0 + 28 if index == 0 else y1 - 28
        yy2 = y0 + 40 if index == 0 else y1 - 40
        s.path(f"M{PX0 + 30},{yy} H{PX1 - 30}", stroke=light, sw=4, extra='opacity="0.75"')
        s.path(f"M{PX0 + 42},{yy2} H{PX1 - 42}", stroke=light, sw=2, extra='opacity="0.75"')
        for x in (PX0 + 70, PX1 - 70):
            motif(s, act, x, yy2 + (40 if index == 0 else -40), 34, light)
    s.path(paper, stroke=dark, sw=4, extra='opacity="0.7"')


# ═══════════════════════════ запуск ═══════════════════════════

TINT = {"frozen_barrows": "#7ff2ff", "clockwork_depths": "#ffb050", "blood_court": "#ff3040", "star_tower": "#9aa8ff",
        "nameless_limit": "#9a6aff"}


def build(act, piece) -> Svg:
    w, h = SIZES[piece]
    s = Svg(w, h)
    seed = sum(ord(c) for c in act + piece)
    rnd = random.Random(seed)
    if piece.startswith("map_"):
        map_piece(s, act, ("map_top", "map_middle", "map_bottom").index(piece))
        return s
    globals()[f"{piece}_{act}" if piece != "combat_fg" else f"fg_{act}"](s, rnd)
    if piece == "combat":
        finish(s, TINT[act], seed % 97, vignette=0.5)
    elif piece == "camp":
        finish(s, TINT[act], seed % 97, vignette=0.7)
    return s


def outputs(acts):
    for act in acts:
        for piece, ext in PIECES.items():
            yield act, piece, os.path.join(OUT_SVG, act, f"{piece}.svg"), os.path.join(OUT_IMG, act, f"{piece}.{ext}")


def check(acts) -> int:
    bad = []
    for act, piece, svg, img in outputs(acts):
        if not os.path.exists(svg) or open(svg, encoding="utf-8").read() != build(act, piece).render():
            bad.append(f"{os.path.relpath(svg, ROOT)}: не совпадает с генератором (запустите с --render)")
        if not os.path.exists(img):
            bad.append(f"{os.path.relpath(img, ROOT)}: нет картинки")
    for line in bad:
        print("✗", line)
    print(f"фоны актов: {len(acts)} × {len(PIECES)}, ошибок: {len(bad)}")
    return 1 if bad else 0


def main(argv) -> int:
    acts = [a for a in argv[1:] if not a.startswith("--")] or ACTS
    if "--check" in argv:
        return check(acts)
    pairs, jobs = [], []
    tmp = tempfile.mkdtemp()
    for act, piece, svg, img in outputs(acts):
        os.makedirs(os.path.dirname(svg), exist_ok=True)
        with open(svg, "w", encoding="utf-8") as f:
            f.write(build(act, piece).render())
        png = os.path.join(tmp, f"{act}_{piece}.png")
        pairs += [svg, png]
        jobs.append((png, img))
    print(f"SVG: {len(jobs)} → {os.path.relpath(OUT_SVG, ROOT)}")
    if "--render" not in argv:
        return 0
    env = dict(os.environ, NODE_PATH=os.environ.get("NODE_PATH", "/tmp/r/node_modules"))
    subprocess.run(["node", os.path.join(ROOT, "tools", "art", "render_svg.mjs"), *pairs], check=True, env=env)
    from PIL import Image
    for png, img in jobs:
        os.makedirs(os.path.dirname(img), exist_ok=True)
        im = Image.open(png)
        if img.endswith(".jpg"):
            im.convert("RGB").save(img, quality=84, optimize=True)
        else:
            im.save(img, optimize=True)
    print(f"картинки: {len(jobs)} → {os.path.relpath(OUT_IMG, ROOT)}")
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv))
