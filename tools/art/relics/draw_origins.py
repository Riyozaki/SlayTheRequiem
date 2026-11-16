#!/usr/bin/env python3
"""Значки происхождений и предысторий (M7 п. 3, docs/design/04): SVG → art/relics/{key}.svg → images/relics/.

Раньше у них были ванильные реликвии «по смыслу» (почтовый ящик у Посланника, руль корабля у Легионера) — на экране выбора
происхождения и предыстории (ванильный «выбери реликвию») это сбивало с толку. Теперь:

- **Происхождения (10)** — гербы; форма щита говорит о герое: кланы Латоносца — геральдический щит с металлической каймой,
  племена Безмолвной — круглый кожаный щит с перьями, династии Регента — медальон с короной. В поле — знак народа.
- **Предыстории (31)** — предметы в стиле ванильных реликвий: двуручный топор Берсерка, печать Посланника, узелок Бродяги…

Форматы как у ванильных реликвий (сверено с атласами игры):
    images/relics/{key}.png          85×85   панель реликвий (без тёмной обводки — её рисует игра по контуру)
    images/relics/{key}_outline.png  85×85   белый силуэт, расширенный на 2 px (подсветка)
    images/relics/big/{key}.png      256×256 инспекция и экран выбора — с толстой тёмной обводкой, как ванильные

Подхватываются без правки кода: RequiemRelic ищет свой файл по ArtName раньше ванильного (Extensions/StringExtensions.cs).

    python3 tools/art/relics/draw_origins.py            # только SVG
    python3 tools/art/relics/draw_origins.py --render   # + PNG (resvg: npm i --prefix /tmp/r @resvg/resvg-js@2, Pillow)
    python3 tools/art/relics/draw_origins.py --check    # CI: SVG совпадают с генератором, PNG на месте и нужного размера
"""
from __future__ import annotations

import math
import os
import struct
import subprocess
import sys
import tempfile

HERE = os.path.dirname(__file__)
sys.path.insert(0, os.path.join(HERE, "..", "monsters"))
from svgkit import INK, Svg, poly, smooth, star  # noqa: E402

ROOT = os.path.abspath(os.path.join(HERE, "..", "..", ".."))
OUT_SVG = os.path.join(ROOT, "art", "relics")
OUT_IMG = os.path.join(ROOT, "mod", "SlayTheRequiem", "images", "relics")
S = 256
SMALL = 85
C = 128

METAL = [(0, "#ffffff"), (0.45, "#d6dce2"), (1, "#7e8892")]
GOLD = [(0, "#fff6c8"), (0.45, "#f0c050"), (1, "#9a6a1a")]
WOOD = [(0, "#d09a5e"), (1, "#6e4420")]
LEATHER = [(0, "#9a6a46"), (1, "#4a2e1a")]
BONE = [(0, "#fffaf0"), (1, "#c8b898")]


def g(s: Svg, stops, x2=0.4, y2=1):
    return s.lin(stops, 0, 0, x2, y2)


def gem(s: Svg, x, y, r, light, dark):
    s.circle(x, y, r, s.rad([(0, light), (1, dark)], 0.35, 0.35, 0.7), INK, 3)
    s.circle(x - r * 0.35, y - r * 0.35, r * 0.28, "#ffffff", extra='opacity="0.8"')


def glow(s: Svg, x, y, r, color, alpha=0.55):
    """Мягкий ореол за предметом: слабее порога силуэта, поэтому обводка и контур подсветки его не обнимают."""
    s.circle(x, y, round(r * 0.8, 1), color, extra=f'opacity="{round(alpha * 0.6, 2)}" filter="{s.blur(r * 0.3)}"')


def rot(pts, cx, cy, deg):
    a = math.radians(deg)
    ca, sa = math.cos(a), math.sin(a)
    return [(round(cx + (x - cx) * ca - (y - cy) * sa, 1), round(cy + (x - cx) * sa + (y - cy) * ca, 1)) for x, y in pts]


def bolt(s: Svg, x, y, k=1.0, color="#fff27a"):
    pts = [(x + 6 * k, y - 34 * k), (x - 14 * k, y + 4 * k), (x - 1 * k, y + 4 * k), (x - 8 * k, y + 34 * k), (x + 16 * k, y - 6 * k),
           (x + 3 * k, y - 6 * k)]
    s.path(poly(pts), color, INK, 3, extra=f'filter="{s.glow(2)}"')


# ═══════════════════════════ гербы происхождений ═══════════════════════════

def crest_heater(s: Svg, field, dark):
    """Кланы Латоносца: геральдический щит с металлической каймой."""
    outer = [(40, 34), (216, 34), (216, 120), (200, 172), (128, 228), (56, 172), (40, 120)]
    inner = [(54, 48), (202, 48), (202, 118), (188, 164), (128, 210), (68, 164), (54, 118)]
    s.path(smooth(outer, closed=True, k=0.25), g(s, METAL, 0.6, 1), INK, 7)
    s.path(smooth(inner, closed=True, k=0.25), g(s, [(0, field), (1, dark)], 0.3, 1), INK, 4)
    for x, y in ((48, 42), (208, 42), (128, 220)):
        s.circle(x, y, 5, g(s, METAL), INK, 2)


def crest_round(s: Svg, field, dark):
    """Племена Безмолвной: круглый кожаный щит, шнуровка по краю, перья снизу."""
    for dx, col in ((-30, "#e8e0d0"), (30, "#c8a070")):
        x = C + dx
        s.path(f"M{x},{196} L{x + dx * 0.3:.0f},{238}", stroke=INK, sw=5)
        s.path(smooth([(x, 200), (x - 11, 216), (x + dx * 0.3, 244), (x + 11, 216)], closed=True), col, INK, 3)
    s.circle(C, 116, 92, g(s, LEATHER, 0.5, 1), INK, 7)
    s.circle(C, 116, 74, g(s, [(0, field), (1, dark)], 0.3, 1), INK, 4)
    for k in range(16):
        a = k * math.pi / 8
        s.path(f"M{C + 80 * math.cos(a):.1f},{116 + 80 * math.sin(a):.1f} L{C + 88 * math.cos(a):.1f},{116 + 88 * math.sin(a):.1f}",
               stroke="#e8d0a0", sw=3)


def crest_medallion(s: Svg, field, dark):
    """Династии Регента: золотой медальон с короной."""
    s.path(poly([(84, 66), (96, 26), (112, 52), (128, 18), (144, 52), (160, 26), (172, 66)]), g(s, GOLD), INK, 5)
    for x, y in ((96, 26), (128, 18), (160, 26)):
        gem(s, x, y, 7, "#ffd0e0", "#c0306a")
    s.circle(C, 140, 92, g(s, GOLD, 0.6, 1), INK, 7)
    s.circle(C, 140, 74, g(s, [(0, field), (1, dark)], 0.3, 1), INK, 4)
    for k in range(12):
        a = k * math.pi / 6
        s.circle(round(C + 83 * math.cos(a), 1), round(140 + 83 * math.sin(a), 1), 3.5, "#fff6c8", INK, 1.5)


def o_frostborn(s):
    crest_heater(s, "#bfe6ff", "#4a7aa8")
    s.path(poly([(70, 170), (108, 96), (128, 120), (150, 80), (190, 170)]), g(s, [(0, "#e8f0f8"), (1, "#6a88a8")]), INK, 4)
    s.path(poly([(96, 120), (108, 96), (118, 108), (128, 120), (138, 98), (150, 80), (166, 112), (150, 104), (138, 118), (124, 128),
                 (110, 112)]), "#ffffff", INK, 2.5)
    for k in range(6):
        a = k * math.pi / 3
        s.path(f"M{C},{150} l{20 * math.cos(a):.1f},{20 * math.sin(a):.1f}", stroke="#ffffff", sw=4)


def o_forgeblood(s):
    crest_heater(s, "#7a3a2a", "#2a120c")
    glow(s, C, 150, 40, "#ff7a2a", 0.6)
    s.path(poly([(74, 100), (182, 100), (174, 120), (152, 124), (146, 150), (164, 168), (92, 168), (110, 150), (104, 124), (82, 120)]),
           g(s, [(0, "#8a8e96"), (1, "#2e3036")]), INK, 4)
    for x, d in ((96, -1), (160, 1)):  # клыки
        s.path(f"M{x},{80} Q{x + d * 18},{60} {x + d * 8},{40}", stroke=INK, sw=11)
        s.path(f"M{x},{80} Q{x + d * 18},{60} {x + d * 8},{40}", stroke="#f6eedc", sw=6)
    s.path(f"M{C},{176} q-14,16 0,28 q14,-12 0,-28", "#ffb040", INK, 3)


def o_duneblade(s):
    crest_heater(s, "#f2c878", "#a8702a")
    s.circle(C, 96, 26, g(s, [(0, "#fff4c0"), (1, "#ff9a3a")]), INK, 3)
    s.path("M58,160 Q100,128 140,150 T200,140 L200,176 L58,176 Z", "#c88a40", INK, 3)
    s.path(smooth([(70, 190), (120, 110), (180, 70), (186, 76), (128, 124), (80, 196)], closed=True, k=0.4), g(s, METAL), INK, 4)
    s.path("M66,178 L90,204", stroke=INK, sw=10)
    s.path("M66,178 L90,204", stroke="#7a4a26", sw=6)


def o_citadel(s):
    crest_heater(s, "#b8323a", "#4a0c14")
    s.path(poly([(84, 176), (84, 104), (98, 104), (98, 92), (110, 92), (110, 104), (122, 104), (122, 80), (134, 80), (134, 104),
                 (146, 104), (146, 92), (158, 92), (158, 104), (172, 104), (172, 176)]), g(s, GOLD), INK, 4)
    s.path("M116,176 L116,148 Q128,132 140,148 L140,176 Z", "#3a0c10", INK, 3)
    for x in (100, 156):
        s.path(f"M{x - 5},{126} h10 v14 h-10 z", "#3a0c10", INK, 2)


def o_moonborn(s):
    crest_round(s, "#4a3a8a", "#1a1238")
    s.path("M150,62 A54,54 0 1 0 150,170 A42,42 0 1 1 150,62 Z", g(s, [(0, "#fff6d8"), (1, "#c8b070")]), INK, 4)
    for k in range(3):  # следы когтей
        x = 124 + k * 16
        s.path(f"M{x},{92} Q{x + 10},{118} {x + 4},{146}", stroke="#ff5a6a", sw=5)
    for x, y in ((176, 84), (186, 132), (166, 160)):
        s.path(star(x, y, 7, 3, 4), "#fff6d8", stroke=None)


def o_thicketborn(s):
    crest_round(s, "#3a7a3a", "#14321a")
    s.path("M128,182 L128,110", stroke=INK, sw=9)
    s.path("M128,182 L128,110", stroke="#7a4e26", sw=5)
    for dx, dy, r in ((0, -26, 34), (-34, -4, 26), (34, -4, 26), (-18, 18, 22), (20, 18, 22)):
        s.circle(C + dx, 104 + dy, r, s.rad([(0, "#9adf6a"), (1, "#2e7a2a")], 0.35, 0.3, 0.8), INK, 3.5)
    for x, y in ((112, 70), (146, 92), (100, 110)):
        s.path(f"M{x},{y} q6,-8 12,0 q-6,8 -12,0", "#d8ff9a", stroke=None)


def o_marshborn(s):
    crest_round(s, "#2a7a72", "#0c2e2c")
    s.ellipse(C, 118, 46, 30, g(s, [(0, "#f4e070"), (1, "#a88a20")]), INK, 4)
    s.path("M128,90 Q114,118 128,146 Q142,118 128,90 Z", "#14100a", INK, 2)
    for k, x in enumerate((76, 92, 164, 180)):
        h = 52 if k % 2 else 40
        s.path(f"M{x},{186} Q{x + (6 if k < 2 else -6)},{186 - h / 2} {x},{186 - h}", stroke=INK, sw=7)
        s.path(f"M{x},{186} Q{x + (6 if k < 2 else -6)},{186 - h / 2} {x},{186 - h}", stroke="#7ab84a", sw=3.5)
    s.path("M86,170 Q128,186 170,170", stroke="#9ae0d8", sw=3, extra='opacity="0.7"')


def o_highborn(s):
    crest_medallion(s, "#2a4aa8", "#0e1a48")
    glow(s, C, 140, 40, "#ffe08a", 0.6)
    s.path(star(C, 140, 46, 16, 8, rot=-90), g(s, GOLD), INK, 3.5)
    s.ellipse(C, 140, 18, 11, "#ffffff", INK, 3)
    s.circle(C, 140, 6, "#2a4aa8")


def o_mistborn(s):
    crest_medallion(s, "#8a86b8", "#34305a")
    for k, (y, w) in enumerate(((176, 54), (194, 40))):
        s.path(f"M{C - w},{y} q{w / 2},-12 {w},0 t{w},0", stroke="#e8e6ff", sw=5, extra='opacity="0.85"')
    s.path("M128,180 L128,120", stroke="#3a6a2a", sw=6)
    for k in range(5):
        a = -math.pi / 2 + k * 2 * math.pi / 5
        s.circle(round(C + 18 * math.cos(a), 1), round(118 + 18 * math.sin(a), 1), 16, "#e8607a", INK, 3)
    s.circle(C, 118, 11, "#ffd0a0", INK, 3)


def o_ashborn(s):
    crest_medallion(s, "#4a3a3a", "#160e10")
    glow(s, C, 128, 30, "#ff5a2a", 0.7)
    s.path(poly([(70, 198), (110, 128), (146, 128), (186, 198)]), g(s, [(0, "#5a4a48"), (1, "#1e1616")]), INK, 4)
    s.path("M112,132 Q128,150 144,132 L136,160 Q128,152 120,164 Z", "#ff6a2a", INK, 2.5)
    for k, x in enumerate((104, 128, 152)):  # три звезды Тройственных
        s.path(star(x, 92 - (8 if k == 1 else 0), 9, 4, 5), "#ffd06a", INK, 2)
    for x, y in ((96, 120), (162, 112), (140, 104)):
        s.circle(x, y, 3, "#c8c0c0", extra='opacity="0.7"')


# ═══════════════════════════ предметы предысторий ═══════════════════════════

def b_beast_speaker(s):
    glow(s, C, 128, 80, "#7ad8ff", 0.45)
    head = [(128, 210), (96, 176), (70, 120), (64, 50), (100, 92), (128, 86), (156, 92), (192, 50), (186, 120), (160, 176)]
    s.path(smooth(head, k=0.5), g(s, [(0, "#e8f8ff"), (1, "#5aa0c8")], 0.3, 1), INK, 6, extra='opacity="0.95"')
    s.path("M104,186 Q128,214 152,186 L128,176 Z", "#2a3a4a", INK, 3)
    s.eye(104, 132, 7, "#7ae8ff")
    s.eye(152, 132, 7, "#7ae8ff")
    for k in range(3):
        s.path(f"M{116 + k * 12},{100} l0,18", stroke="#3a6a8a", sw=3)


def b_skald(s):
    pts = [(56, 92), (78, 70), (150, 84), (204, 132), (214, 192), (196, 196), (178, 148), (128, 118), (70, 116)]
    s.path(smooth(pts, k=0.5), g(s, BONE, 0.6, 1), INK, 6)
    for x0, y0, x1, y1 in ((98, 78, 92, 116), (142, 88, 128, 122), (182, 112, 164, 140)):
        s.path(f"M{x0},{y0} L{x1},{y1}", stroke=INK, sw=10)
        s.path(f"M{x0},{y0} L{x1},{y1}", stroke="#c8a050", sw=6)
    s.ellipse(62, 104, 12, 22, "#2a1a10", INK, 4)
    s.path("M92,116 Q130,170 186,150", stroke="#7a4a26", sw=5)


def b_frostbinder(s):
    glow(s, C, 140, 70, "#9ae8ff", 0.5)
    for dx, h, w, a in ((0, 150, 30, 0), (-42, 100, 22, -18), (42, 110, 22, 16), (-20, 80, 16, -34), (24, 76, 16, 32)):
        pts = rot([(C + dx - w, 216), (C + dx - w * 0.8, 216 - h * 0.75), (C + dx, 216 - h), (C + dx + w * 0.8, 216 - h * 0.75), (C + dx + w, 216)],
                  C + dx, 216, a)
        s.path(poly(pts), g(s, [(0, "#ffffff"), (0.5, "#bfeaff"), (1, "#4a8ac0")], 0.7, 1), INK, 4)
    s.path("M128,170 L128,120 M112,134 L144,150 M144,134 L112,150", stroke="#2a6aa0", sw=4)


def b_berserker(s):
    s.path("M70,226 L178,40", stroke=INK, sw=16)
    s.path("M70,226 L178,40", stroke="url(#w)", sw=10)
    s.defs.append('<linearGradient id="w" x1="0" y1="0" x2="1" y2="0"><stop offset="0" stop-color="#d09a5e"/><stop offset="1" stop-color="#6e4420"/></linearGradient>')
    for k in range(4):
        y = 196 - k * 10
        x = 70 + (226 - y) * 108 / 186
        s.path(f"M{x - 8},{y} l16,6", stroke="#4a2a14", sw=3)
    for side in (-1, 1):
        blade = rot([(160, 70), (160 + side * 70, 30), (160 + side * 84, 84), (160 + side * 70, 136), (160, 100)], 160, 85, 30)
        s.path(smooth(blade, k=0.3), g(s, METAL, 0.8, 0.4), INK, 5)
    s.path("M152,64 Q206,46 214,110", stroke="#c0202a", sw=5, extra='opacity="0.8"')


def b_poacher(s):
    s.path(smooth([(96, 70), (160, 70), (176, 220), (82, 220)], k=0.15), g(s, LEATHER, 0.6, 0.2), INK, 6)
    s.path("M92,96 L166,96", stroke="#c8a070", sw=5)
    for k, (x, a) in enumerate(((108, -12), (128, 0), (148, 12), (118, -4), (140, 6))):
        top = rot([(x, 74), (x, 24)], x, 74, a)
        s.path(f"M{top[0][0]},{top[0][1]} L{top[1][0]},{top[1][1]}", stroke=INK, sw=7)
        s.path(f"M{top[0][0]},{top[0][1]} L{top[1][0]},{top[1][1]}", stroke="#d8b080", sw=3)
        tx, ty = top[1]
        s.path(poly([(tx - 9, ty + 4), (tx, ty - 16), (tx + 9, ty + 4)]), "#f0f0f0" if k % 2 else "#c0303a", INK, 2.5)
    s.path("M96,140 L162,140", stroke="#4a2e1a", sw=4)


def b_thunder_shaman(s):
    s.path("M98,228 L150,50", stroke=INK, sw=14)
    s.path("M98,228 L150,50", stroke="#9a6a3a", sw=8)
    s.circle(152, 46, 22, g(s, BONE), INK, 5)
    for x, y in ((144, 42), (160, 42)):
        s.circle(x, y, 5, "#1a1418")
    for k, (dx, col) in enumerate(((-24, "#e8e0d0"), (26, "#3a6ac0"), (-14, "#c8a070"))):
        y = 86 + k * 16
        s.path(smooth([(140, y), (140 + dx, y + 10), (140 + dx * 1.3, y + 40), (140 + dx * 0.6, y + 22)]), col, INK, 2.5)
    bolt(s, 194, 120, 1.3)
    bolt(s, 70, 96, 0.8)


def b_dawn_brother(s):
    s.path("M64,30 Q128,110 192,30", stroke="#c8a050", sw=5)
    glow(s, C, 146, 76, "#ffe08a", 0.6)
    s.path(star(C, 146, 74, 48, 12), g(s, GOLD), INK, 4)
    s.circle(C, 146, 38, s.rad([(0, "#ffffff"), (0.5, "#ffe8a0"), (1, "#f0a030")]), INK, 4)
    s.path("M114,140 Q128,130 142,140 M128,152 l0,10", stroke="#9a6a1a", sw=3)


def b_conspirator(s):
    blade = [(120, 168), (124, 46), (138, 24), (148, 48), (146, 168)]
    s.path(poly(blade), g(s, METAL, 1, 0.2), INK, 5)
    s.path("M134,160 L136,50", stroke="#8a929a", sw=2.5)
    s.path("M124,70 Q118,110 128,130 Q138,150 130,170", stroke="#6aff4a", sw=7, extra=f'opacity="0.9" filter="{s.glow(2)}"')
    s.path(poly([(92, 168), (174, 168), (170, 182), (96, 182)]), g(s, GOLD), INK, 4)
    s.path(poly([(124, 182), (142, 182), (142, 222), (124, 222)]), g(s, LEATHER, 1, 0), INK, 4)
    s.circle(133, 228, 9, g(s, GOLD), INK, 3)
    for x, y in ((112, 196), (100, 214)):
        s.path(f"M{x},{y - 12} q8,10 0,16 q-8,-6 0,-16", "#6aff4a", INK, 2)


def b_ansei(s):
    glow(s, C, 120, 84, "#7af0ff", 0.4)
    blade = [(84, 182), (190, 40), (198, 38), (196, 48), (96, 192)]
    s.path(smooth(blade, k=0.2), g(s, METAL, 1, 0.6), INK, 5)
    s.path("M94,184 L192,48", stroke="#e8ffff", sw=2, extra='opacity="0.8"')
    s.ellipse(86, 190, 18, 9, "#2a2a30", INK, 3.5, extra='transform="rotate(-50 86 190)"')
    s.path("M80,196 L44,236", stroke=INK, sw=14)
    s.path("M80,196 L44,236", stroke="#2a3a6a", sw=8)
    for k in range(3):
        x, y = 72 - k * 10, 206 + k * 11
        s.path(f"M{x - 6},{y - 4} l12,8", stroke="#e8e8f0", sw=2.5)


def b_legionary(s):
    outer = "M62,40 Q128,26 194,40 Q210,128 194,216 Q128,230 62,216 Q46,128 62,40 Z"
    s.path(outer, g(s, METAL, 0.6, 1), INK, 7)
    s.path("M72,52 Q128,40 184,52 Q198,128 184,204 Q128,216 72,204 Q58,128 72,52 Z", g(s, [(0, "#e04a50"), (1, "#6a1018")], 0.5, 1), INK, 4)
    for side in (-1, 1):  # крылья и молнии легиона
        s.path(f"M{C + side * 22},{116} Q{C + side * 58},{96} {C + side * 50},{64} Q{C + side * 44},{100} {C + side * 18},{108} Z",
               "#f0c050", INK, 3)
        s.path(f"M{C + side * 22},{142} l{side * 22},{14} l{-side * 6},{4} l{side * 26},{30}", stroke="#f0c050", sw=5)
    s.circle(C, 128, 26, g(s, METAL), INK, 5)
    s.circle(C, 128, 12, g(s, GOLD), INK, 3)
    for x, y in ((128, 36), (128, 222), (54, 128), (202, 128)):
        s.circle(x, y, 4, "#fff6c8", INK, 1.5)


def b_envoy(s):
    s.path(poly([(48, 70), (208, 70), (208, 186), (48, 186)]), g(s, [(0, "#fbf0d8"), (1, "#c8b088")], 0.3, 1), INK, 6)
    s.path("M48,70 L128,138 L208,70", stroke=INK, sw=4)
    s.path("M48,186 L106,122 M208,186 L150,122", stroke="#a89068", sw=3)
    s.path("M108,150 L94,210 L112,198 L118,216 Z M148,150 L162,210 L144,198 L138,216 Z", "#b02030", INK, 3)
    s.circle(C, 140, 26, s.rad([(0, "#ff5a5a"), (1, "#8a1018")], 0.35, 0.35, 0.7), INK, 4)
    s.path(star(C, 140, 13, 6, 5), "#5a0810", stroke=None)


def b_citadel_mage(s):
    for k, (rx, ry, a) in enumerate(((96, 30, -20), (90, 26, 30))):
        s.ellipse(C, 128, rx, ry, "none", "#9ae0ff", 4, extra=f'transform="rotate({a} 128 128)" opacity="0.85"')
    glow(s, C, 128, 54, "#5ab0ff", 0.6)
    s.circle(C, 128, 40, s.rad([(0, "#ffffff"), (0.4, "#9ad8ff"), (1, "#2a5aa8")], 0.35, 0.35, 0.75), INK, 5)
    for k in range(3):
        s.path(f"M{C - 40 - k * 14},{128} a{40 + k * 14},{40 + k * 14} 0 0 1 {12 + k * 4},{-30 - k * 8}", stroke="#cdf0ff", sw=3,
               extra=f'opacity="{0.8 - k * 0.2}"')
    s.circle(188, 92, 9, "#cdf0ff", INK, 2.5)


def b_fugitive(s):
    s.path("M70,150 m-36,0 a36,36 0 1 0 72,0 a36,36 0 1 0 -72,0 Z M70,150 m-20,0 a20,20 0 1 1 40,0 a20,20 0 1 1 -40,0 Z",
           g(s, METAL, 0.6, 1), INK, 5, extra='fill-rule="evenodd"')
    for k in range(3):
        x, y = 112 + k * 26, 128 - k * 18
        s.ellipse(x, y, 16, 10, "none", INK, 10, extra=f'transform="rotate(-34 {x} {y})"')
        s.ellipse(x, y, 16, 10, "none", "#b8c0c8", 5, extra=f'transform="rotate(-34 {x} {y})"')
    s.path("M190,62 m-30,0 a30,30 0 0 1 56,-6", stroke=INK, sw=16)
    s.path("M190,62 m-30,0 a30,30 0 0 1 56,-6", stroke="#b8c0c8", sw=9)
    for x, y in ((200, 98), (214, 86), (190, 108)):
        s.path(star(x, y, 9, 3, 4), "#fff4a0", stroke=None)


def b_rogue(s):
    s.path(smooth([(96, 84), (160, 84), (204, 168), (178, 214), (78, 214), (52, 168)], k=0.4), g(s, LEATHER, 0.6, 1), INK, 6)
    s.path("M92,90 Q128,104 164,90", stroke="#e8c070", sw=6)
    s.path(smooth([(100, 84), (88, 56), (110, 64), (128, 44), (146, 64), (168, 56), (156, 84)], k=0.3), g(s, LEATHER, 0.6, 1), INK, 5)
    for x, y in ((70, 216), (196, 206), (176, 228)):
        s.ellipse(x, y, 20, 10, g(s, GOLD), INK, 3.5)
    s.circle(C, 158, 24, g(s, GOLD), INK, 4)
    s.path(star(C, 158, 11, 5, 5), "#9a6a1a", stroke=None)


def b_dusk_singer(s):
    s.ellipse(110, 166, 58, 52, g(s, WOOD, 0.6, 1), INK, 6)
    s.circle(110, 160, 16, "#2a1a10", INK, 3)
    s.path("M138,128 L200,48", stroke=INK, sw=20)
    s.path("M138,128 L200,48", stroke="#7a4a22", sw=13)
    s.path(poly([(194, 40), (220, 24), (226, 40), (206, 58)]), "#4a2a14", INK, 4)
    for k in (-4, 0, 4):
        s.path(f"M{90 + k},{200} L{206 + k},{52}", stroke="#f0e6c8", sw=1.5)
    s.path("M76,72 A26,26 0 1 0 100,114 A20,20 0 1 1 76,72 Z", "#fff0b0", INK, 3, extra=f'filter="{s.glow(2)}"')


def b_winddancer(s):
    for k, (r, a) in enumerate(((78, 0), (56, 120), (36, 240))):
        s.path(f"M{C + r * math.cos(math.radians(a)):.1f},{128 + r * math.sin(math.radians(a)):.1f} "
               f"A{r},{r} 0 0 1 {C + r * math.cos(math.radians(a + 200)):.1f},{128 + r * math.sin(math.radians(a + 200)):.1f}",
               stroke="#cdeee0", sw=6 - k, extra='opacity="0.85"')
    for x, y, a, col in ((76, 84, -30, "#9adf6a"), (186, 120, 40, "#e8c050"), (110, 190, 160, "#6ab84a"), (160, 70, 80, "#c8e86a")):
        leaf = rot([(x - 28, y), (x, y - 14), (x + 28, y), (x, y + 14)], x, y, a)
        s.path(smooth(leaf, k=0.6), col, INK, 3.5)
        e = rot([(x - 24, y), (x + 24, y)], x, y, a)
        s.path(f"M{e[0][0]},{e[0][1]} L{e[1][0]},{e[1][1]}", stroke="#2e5a1e", sw=2)


def b_hunter(s):
    s.path("M86,30 Q190,128 86,226", stroke=INK, sw=14)
    s.path("M86,30 Q190,128 86,226", stroke="url(#hw)", sw=8)
    s.defs.append('<linearGradient id="hw" x1="0" y1="0" x2="1" y2="0"><stop offset="0" stop-color="#e0b070"/><stop offset="1" stop-color="#8a5a2a"/></linearGradient>')
    s.path("M86,30 L86,226", stroke="#f6eedc", sw=2.5)
    s.path("M50,128 L208,128", stroke=INK, sw=8)
    s.path("M50,128 L208,128", stroke="#c8a070", sw=4)
    s.path(poly([(204, 116), (232, 128), (204, 140)]), g(s, METAL), INK, 3)
    for k in (-1, 1):
        s.path(poly([(48, 128), (66, 128), (58, 128 + k * 16), (40, 128 + k * 16)]), "#3a8a3a", INK, 2.5)
    s.path("M138,108 L146,148", stroke="#3a8a3a", sw=6)


def b_hermit(s):
    s.ellipse(C, 150, 82, 26, "#3a2414", INK, 5)
    s.path("M46,150 Q50,222 128,222 Q206,222 210,150", g(s, [(0, "#b07a4a"), (1, "#5a3418")], 1, 0), INK, 6)
    s.ellipse(C, 150, 70, 18, "#8a6a3a", INK, 3)
    for x, h, col in ((96, 50, "#c84a3a"), (130, 72, "#e8c890"), (164, 44, "#a87a4a")):
        s.path(f"M{x - 5},{150} L{x - 4},{150 - h + 10} L{x + 4},{150 - h + 10} L{x + 5},{150} Z", "#f6eedc", INK, 3)
        s.path(f"M{x - 26},{150 - h + 14} Q{x},{150 - h - 26} {x + 26},{150 - h + 14} Z", col, INK, 4)
        if col == "#c84a3a":
            for dx in (-10, 6):
                s.circle(x + dx, 150 - h, 4, "#fff4e0")
    for k in range(2):
        s.path(f"M{180 + k * 14},{120} q-8,-14 0,-28 q8,-14 0,-28", stroke="#e8e6f0", sw=3, extra='opacity="0.6"')


def b_gladiator(s):
    s.path("M70,96 Q128,34 186,96 L190,170 Q190,206 128,214 Q66,206 66,170 Z", g(s, [(0, "#f6e0a0"), (1, "#9a6a1a")], 0.5, 1), INK, 6)
    s.path("M58,166 L198,166", stroke=INK, sw=12)
    s.path("M58,166 L198,166", stroke="#c8902a", sw=7)
    for y in (110, 128, 146):
        s.path(f"M92,{y} L164,{y}", stroke="#2a1a10", sw=6)
    for x in (104, 128, 152):
        s.path(f"M{x},{102} L{x},{152}", stroke="#2a1a10", sw=4)
    s.path("M128,60 Q128,20 176,24 Q150,40 152,70", "#c0303a", INK, 4)


def b_shadowscale(s):
    glow(s, C, 128, 70, "#5a2a8a", 0.5)
    s.path(smooth([(128, 30), (196, 80), (206, 150), (128, 226), (50, 150), (60, 80)], k=0.35), g(s, [(0, "#3a4a3a"), (1, "#0e140e")], 0.4, 1),
           INK, 6)
    for y in range(70, 200, 22):
        for x in range(70 + (y // 22 % 2) * 11, 190, 22):
            s.path(f"M{x - 10},{y} q10,12 20,0", stroke="#5a7a5a", sw=2, extra='opacity="0.7"')
    s.ellipse(C, 124, 44, 26, g(s, [(0, "#ffe26a"), (1, "#c87a10")]), INK, 4)
    s.path("M128,100 Q118,124 128,148 Q138,124 128,100 Z", "#0a0806", stroke=None)
    s.circle(116, 114, 5, "#ffffff", extra='opacity="0.8"')


def b_venomweaver(s):
    s.path(poly([(108, 50), (148, 50), (148, 96), (184, 176), (166, 218), (90, 218), (72, 176), (108, 96)]),
           g(s, [(0, "#e0fff0"), (1, "#7ab0a0")], 0.5, 1), INK, 5, extra='opacity="0.95"')
    s.path("M84,170 L172,170 L180,190 L164,212 L92,212 L76,190 Z", "#6aff4a", stroke=None, extra=f'opacity="0.85" filter="{s.blur(1)}"')
    s.path(poly([(102, 34), (154, 34), (154, 52), (102, 52)]), g(s, WOOD, 1, 0), INK, 4)
    s.path(smooth([(70, 130), (102, 104), (160, 120), (186, 150), (160, 186), (100, 172), (80, 200), (110, 226)], closed=False, k=0.5),
           stroke=INK, sw=16)
    s.path(smooth([(70, 130), (102, 104), (160, 120), (186, 150), (160, 186), (100, 172), (80, 200), (110, 226)], closed=False, k=0.5),
           stroke="#4aa83a", sw=10)
    s.ellipse(64, 130, 16, 11, "#4aa83a", INK, 4)
    s.circle(60, 126, 3, "#ffe040")
    s.path("M50,134 l-12,4 M50,134 l-10,-4", stroke="#c0303a", sw=2.5)


def b_justiciar(s):
    s.path("M128,40 L128,210", stroke=INK, sw=10)
    s.path("M128,40 L128,210", stroke="#f0c050", sw=6)
    s.path(poly([(92, 210), (164, 210), (156, 196), (100, 196)]), g(s, GOLD), INK, 4)
    s.path("M56,76 L200,76", stroke=INK, sw=9)
    s.path("M56,76 L200,76", stroke="#f0c050", sw=5)
    for x in (66, 190):
        s.path(f"M{x},{80} L{x - 26},{140} M{x},{80} L{x + 26},{140}", stroke="#c8a050", sw=2.5)
        s.path(f"M{x - 34},{140} Q{x},{170} {x + 34},{140} Z", g(s, GOLD), INK, 4)
    s.circle(C, 40, 12, g(s, GOLD), INK, 3.5)
    bolt(s, C, 128, 1.0, "#9ae8ff")


def b_shadow_sorcerer(s):
    glow(s, C, 128, 80, "#8a3aff", 0.45)
    s.path(smooth([(60, 70), (128, 52), (196, 70), (190, 150), (128, 210), (66, 150)], k=0.35), g(s, [(0, "#f0eaf8"), (1, "#8a80a0")], 0.5, 1),
           INK, 6)
    s.path("M128,54 Q150,130 128,208 Q196,160 196,70 Q160,56 128,54 Z", g(s, [(0, "#3a2a5a"), (1, "#0e0a18")], 0.5, 1), INK, 3)
    s.path("M86,110 Q100,100 112,112 Q100,120 86,110 Z", "#1a1222", INK, 2.5)
    s.eye(160, 110, 7, "#c07aff")
    s.path("M98,160 Q112,170 124,162", stroke=INK, sw=4)
    s.path("M134,164 Q148,156 160,166", stroke="#c07aff", sw=4)


def b_wizard(s):
    s.ellipse(C, 196, 96, 24, g(s, [(0, "#5a4ac8"), (1, "#1e1658")], 0.4, 1), INK, 6)
    s.path(smooth([(64, 192), (110, 100), (128, 40), (190, 26), (150, 56), (166, 120), (192, 192)], k=0.35), g(s, [(0, "#6a5ae0"), (1, "#22186a")], 0.5, 1),
           INK, 6)
    s.path("M78,176 Q128,190 178,176", stroke="#f0c050", sw=8)
    for x, y, r in ((116, 140, 12), (150, 98, 9), (126, 76, 7), (186, 30, 9)):
        s.path(star(x, y, r, r * 0.45, 5), "#fff0a0", INK, 1.5)


def b_knight(s):
    s.path("M74,104 Q74,44 128,40 Q182,44 182,104 L182,200 L74,200 Z", g(s, METAL, 0.6, 1), INK, 6)
    s.path("M86,118 L170,118", stroke="#1a1a20", sw=10)
    s.path("M128,124 L128,192", stroke="#5a6068", sw=4)
    for x in (100, 112, 144, 156):
        s.circle(x, 152, 3.5, "#1a1a20")
    s.path("M128,40 Q118,12 152,10 Q188,14 196,52 Q170,30 150,36", "#3a5ac8", INK, 4)
    s.path("M128,40 Q144,20 176,28", stroke="#9ab0ff", sw=3)


def b_enchanter(s):
    glow(s, C, 128, 72, "#c06aff", 0.55)
    pts = [(128, 30), (182, 90), (166, 196), (128, 226), (90, 196), (74, 90)]
    s.path(poly(pts), g(s, [(0, "#f8e0ff"), (0.45, "#c07aff"), (1, "#4a1a8a")], 0.6, 1), INK, 6)
    for a, b in (((128, 30), (128, 226)), ((74, 90), (166, 196)), ((182, 90), (90, 196))):
        s.path(f"M{a[0]},{a[1]} L{b[0]},{b[1]}", stroke="#ffffff", sw=2, extra='opacity="0.45"')
    s.path("M128,96 L148,128 L128,160 L108,128 Z", "none", "#ffffff", 3)
    for x, y in ((60, 60), (200, 70), (206, 180), (52, 176)):
        s.path(star(x, y, 10, 3.5, 4), "#f0d8ff", stroke=None, extra=f'filter="{s.glow(1.5)}"')


def b_summoner(s):
    glow(s, C, 108, 60, "#7a5aff", 0.6)
    s.path(poly([(28, 176), (128, 196), (228, 176), (228, 206), (128, 228), (28, 206)]), "#4a2a1a", INK, 5)
    for side in (-1, 1):
        s.path(f"M128,192 Q{128 + side * 50},{168} {128 + side * 96},{170} L{128 + side * 96},{196} Q{128 + side * 50},{194} 128,{218} Z",
               g(s, [(0, "#fbf0d8"), (1, "#c8b088")], 0.2, 1), INK, 4)
    s.circle(C, 96, 54, "none", "#b89aff", 4, extra=f'filter="{s.glow(2)}"')
    s.path(star(C, 96, 44, 18, 5), "none", "#e0d0ff", 3)
    for k in range(4):
        x = 66 + k * 14
        s.path(f"M{x},{186 - k * 2} l24,4", stroke="#8a7a5a", sw=2)
        s.path(f"M{166 + k * 14},{182 + k * 2} l24,-4", stroke="#8a7a5a", sw=2)


def b_ordinator(s):
    s.path(smooth([(70, 60), (128, 26), (186, 60), (196, 140), (166, 214), (128, 228), (90, 214), (60, 140)], k=0.35), g(s, GOLD, 0.5, 1),
           INK, 6)
    s.path("M128,30 L128,108", stroke="#9a6a1a", sw=4)
    s.path("M80,118 Q104,104 120,124 L80,130 Z M176,118 Q152,104 136,124 L176,130 Z", "#1a1008", INK, 3)
    s.path("M104,170 Q128,186 152,170 L148,190 L108,190 Z", "#1a1008", INK, 3)
    for x, y in ((92, 82), (164, 82)):
        s.path(f"M{x - 10},{y} q10,-12 20,0", stroke="#9a6a1a", sw=3)
    s.path(poly([(118, 140), (128, 156), (138, 140)]), "#c8902a", INK, 2)


def b_ash_nomad(s):
    s.path("M70,226 L128,40", stroke=INK, sw=12)
    s.path("M70,226 L128,40", stroke="#d8c8a8", sw=6)
    s.path(smooth([(116, 54), (128, 20), (150, 30), (140, 60)], k=0.4), g(s, BONE), INK, 4)
    for k, (dx, col) in enumerate(((-30, "#c0303a"), (-18, "#2a2020"), (-40, "#d8a040"))):
        y = 76 + k * 10
        s.path(f"M{122 - k * 3},{y} Q{122 + dx},{y + 30} {116 + dx},{y + 70}", stroke=col, sw=5)
    s.path(smooth([(150, 120), (198, 112), (214, 170), (186, 214), (146, 204), (138, 160)], k=0.4), g(s, LEATHER, 0.6, 1), INK, 5)
    s.path("M150,140 L204,132", stroke="#c8a070", sw=4)
    for x, y in ((170, 90), (200, 86), (186, 70), (60, 120), (48, 150)):
        s.circle(x, y, 4, "#8a8080", extra='opacity="0.7"')


def b_mushroom_tower(s):
    s.path("M104,226 Q96,150 112,90 L144,90 Q160,150 152,226 Z", g(s, [(0, "#f6e8d0"), (1, "#a8907a")], 1, 0), INK, 5)
    for y, w, col in ((96, 62, "#a86ad8"), (150, 46, "#c88ae8"), (196, 34, "#8a4ac8")):
        s.path(f"M{C - w},{y} Q{C},{y - w * 0.9} {C + w},{y} Q{C},{y - 12} {C - w},{y} Z", col, INK, 4)
        for dx in (-w * 0.5, 0, w * 0.45):
            s.circle(round(C + dx, 1), round(y - w * 0.32, 1), 4, "#f0e0ff")
    for x, y in ((122, 120), (134, 172), (118, 210)):
        s.path(f"M{x - 5},{y} l0,-10 a5,5 0 0 1 10,0 l0,10 z", "#ffd06a", INK, 2)
    s.path("M176,60 q10,-20 -4,-34", stroke="#ffb040", sw=4, extra=f'filter="{s.glow(2)}"')


def b_vagrant(s):
    s.path("M46,226 L196,58", stroke=INK, sw=13)
    s.path("M46,226 L196,58", stroke="url(#vw)", sw=7)
    s.defs.append('<linearGradient id="vw" x1="0" y1="0" x2="1" y2="0"><stop offset="0" stop-color="#d09a5e"/><stop offset="1" stop-color="#7a4e26"/></linearGradient>')
    s.path(smooth([(150, 70), (210, 50), (232, 110), (206, 158), (150, 150), (134, 104)], k=0.45), g(s, [(0, "#c86a5a"), (1, "#6a2a22")], 0.5, 1),
           INK, 6)
    for x, y in ((170, 96), (196, 118), (186, 80), (210, 96)):
        s.circle(x, y, 5, "#f6e0c0", extra='opacity="0.85"')
    s.path("M176,64 L190,52 L198,70 Z", "#6a2a22", INK, 3)
    s.path("M60,212 l20,8 M66,200 l20,8", stroke="#4a2e1a", sw=3)


ORIGINS = {
    "frostborn_origin": o_frostborn, "forgeblood_origin": o_forgeblood, "duneblade_origin": o_duneblade, "citadel_origin": o_citadel,
    "moonborn_origin": o_moonborn, "thicketborn_origin": o_thicketborn, "marshborn_origin": o_marshborn,
    "highborn_origin": o_highborn, "mistborn_origin": o_mistborn, "ashborn_origin": o_ashborn,
}
BACKGROUNDS = {
    "beast_speaker_background": b_beast_speaker, "skald_background": b_skald, "frostbinder_background": b_frostbinder,
    "berserker_background": b_berserker, "poacher_background": b_poacher, "thunder_shaman_background": b_thunder_shaman,
    "dawn_brother_background": b_dawn_brother, "conspirator_background": b_conspirator, "ansei_background": b_ansei,
    "legionary_background": b_legionary, "envoy_background": b_envoy, "citadel_mage_background": b_citadel_mage,
    "fugitive_background": b_fugitive, "rogue_background": b_rogue, "dusk_singer_background": b_dusk_singer,
    "winddancer_background": b_winddancer, "hunter_background": b_hunter, "hermit_background": b_hermit,
    "gladiator_background": b_gladiator, "shadowscale_background": b_shadowscale, "venomweaver_background": b_venomweaver,
    "justiciar_background": b_justiciar, "shadow_sorcerer_background": b_shadow_sorcerer, "wizard_background": b_wizard,
    "knight_background": b_knight, "enchanter_background": b_enchanter, "summoner_background": b_summoner,
    "ordinator_background": b_ordinator, "ash_nomad_background": b_ash_nomad, "mushroom_tower_background": b_mushroom_tower,
    "vagrant_background": b_vagrant,
}
ICONS = {**ORIGINS, **BACKGROUNDS}


def build(key: str) -> Svg:
    s = Svg(S, S)
    s.open(f'filter="{s.grain(0.16, 0.9, sum(map(ord, key)) % 89)}"')
    ICONS[key](s)
    s.close()
    # область фильтра зерна — с запасом, иначе размытые ореолы обрезаются по рамке группы в квадрат
    s.defs = [d.replace('x="0" y="0" width="100%" height="100%"', 'x="-25%" y="-25%" width="150%" height="150%"') for d in s.defs]
    return s


def outputs():
    for key in ICONS:
        yield (key, os.path.join(OUT_SVG, key + ".svg"), os.path.join(OUT_IMG, key + ".png"),
               os.path.join(OUT_IMG, key + "_outline.png"), os.path.join(OUT_IMG, "big", key + ".png"))


def png_size(path):
    with open(path, "rb") as f:
        head = f.read(24)
    return struct.unpack(">II", head[16:24])


def finish(raw: str, small: str, outline: str, big: str):
    from PIL import Image, ImageChops, ImageFilter
    im = Image.open(raw).convert("RGBA")
    a = im.getchannel("A")
    # большая: толстая тёмная обводка под рисунком (как у ванильных больших значков), с полями 8 px
    pad = Image.new("RGBA", (S, S), (0, 0, 0, 0))
    inner = im.resize((S - 20, S - 20), Image.LANCZOS)
    pad.alpha_composite(inner, (10, 10))
    ring = pad.getchannel("A").filter(ImageFilter.MaxFilter(13)).point(lambda v: 255 if v > 150 else 0).filter(ImageFilter.GaussianBlur(1))
    out = Image.new("RGBA", (S, S), (0x12, 0x0d, 0x14, 0))
    out.putalpha(ring)
    out.alpha_composite(pad)
    out.save(big, optimize=True)
    # малая: рисунок без лишней обводки, поля 3 px
    sm = Image.new("RGBA", (SMALL, SMALL), (0, 0, 0, 0))
    sm.alpha_composite(im.resize((SMALL - 6, SMALL - 6), Image.LANCZOS), (3, 3))
    sm.save(small, optimize=True)
    sil = sm.getchannel("A").point(lambda v: 255 if v > 150 else 0).filter(ImageFilter.MaxFilter(5))
    ol = Image.new("RGBA", (SMALL, SMALL), (255, 255, 255, 0))
    ol.putalpha(ImageChops.lighter(sil, sil))
    ol.save(outline, optimize=True)
    del a


def check() -> int:
    bad = []
    for key, svg, small, outline, big in outputs():
        if not os.path.exists(svg) or open(svg, encoding="utf-8").read() != build(key).render():
            bad.append(f"{os.path.relpath(svg, ROOT)}: не совпадает с генератором (запустите с --render)")
        for path, size in ((small, SMALL), (outline, SMALL), (big, S)):
            if not os.path.exists(path):
                bad.append(f"{os.path.relpath(path, ROOT)}: нет картинки")
            elif png_size(path) != (size, size):
                bad.append(f"{os.path.relpath(path, ROOT)}: размер {png_size(path)}, нужно {size}×{size}")
    for line in bad:
        print("✗", line)
    print(f"значки происхождений и предысторий: {len(ORIGINS)} + {len(BACKGROUNDS)}, ошибок: {len(bad)}")
    return 1 if bad else 0


def main(argv) -> int:
    if "--check" in argv:
        return check()
    os.makedirs(OUT_SVG, exist_ok=True)
    pairs, jobs = [], []
    tmp = tempfile.mkdtemp()
    for key, svg, small, outline, big in outputs():
        with open(svg, "w", encoding="utf-8") as f:
            f.write(build(key).render())
        raw = os.path.join(tmp, key + ".png")
        pairs += [svg, raw]
        jobs.append((raw, small, outline, big))
    print(f"SVG: {len(jobs)} → {os.path.relpath(OUT_SVG, ROOT)}")
    if "--render" not in argv:
        return 0
    env = dict(os.environ, NODE_PATH=os.environ.get("NODE_PATH", "/tmp/r/node_modules"))
    subprocess.run(["node", os.path.join(ROOT, "tools", "art", "render_svg.mjs"), *pairs], check=True, env=env)
    os.makedirs(os.path.join(OUT_IMG, "big"), exist_ok=True)
    for job in jobs:
        finish(*job)
    print(f"PNG: {len(jobs)} × 3 → {os.path.relpath(OUT_IMG, ROOT)}")
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv))
