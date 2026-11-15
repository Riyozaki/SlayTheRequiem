#!/usr/bin/env python3
"""Спрайты боссов актов 2/4 и их свиты → art/monsters/*.svg (рендер: tools/art/render_ui_icons.mjs, группа monsters).

    python3 tools/art/monsters/draw_monsters.py            # все SVG
    python3 tools/art/monsters/draw_monsters.py king_under_barrow   # один (для отладки)

Каждый враг — функция draw_<key>(variant) → Svg. Вариант "p2" (фаза 2) / "night" — на том же холсте, чтобы смена
картинки не сдвигала хитбокс. Низ фигуры — у нижнего края холста (OwnArtMonster ставит спрайт ногами на землю).
"""
from __future__ import annotations

import os
import sys

sys.path.insert(0, os.path.dirname(__file__))
from svgkit import INK, Svg, mirror, poly, smooth, star, arc_pts  # noqa: E402

ROOT = os.path.normpath(os.path.join(os.path.dirname(__file__), "..", "..", ".."))
OUT = os.path.join(ROOT, "art", "monsters")

# Контровой свет и акценты актов (art spec §3.10 палитры).
FROST = "#8fe3ff"
FROST_CORE = "#e8fbff"
STEAM = "#ffb35c"
EMBER = "#ff6a2a"
BLOOD = "#ff3b4e"
ARCANE = "#9aa8ff"
ARCANE_CORE = "#f1f0ff"

REGISTRY: dict[str, tuple] = {}


def sprite(key, variants=("",)):
    def wrap(fn):
        REGISTRY[key] = (fn, variants)
        return fn
    return wrap


# ═════════════════════════ 2а — Ледяные Курганы ═════════════════════════

@sprite("king_under_barrow", ("", "p2"))
def draw_king_under_barrow(v=""):
    p2 = v == "p2"
    s = Svg(440, 520)
    cx = 220
    eye = FROST_CORE if p2 else FROST
    grain = s.grain(0.18, 0.85, 7)
    if p2:
        s.circle(cx, 180, 215, s.rad([(0, FROST_CORE, 0.85), (0.35, FROST, 0.45), (0.75, "#3a6f8f", 0.12), (1, "#0b1a26", 0)]))
    s.shadow(cx, 500, 150, 16)
    # снежный холм
    s.path(smooth([(40, 512), (90, 482), (170, 470), (270, 472), (360, 484), (410, 512)], closed=False) + " Z",
           s.lin([(0, "#e3f2fb"), (1, "#7d9bb3")]), sw=4)
    s.open(f'filter="{grain}"')
    # плащ
    cape = [(cx - 70, 120), (cx - 150, 220), (cx - 175, 330), (cx - 160, 470), (cx - 128, 446), (cx - 108, 482),
            (cx - 80, 452), (cx - 52, 488), (cx, 470), (cx + 52, 488), (cx + 80, 452), (cx + 108, 482), (cx + 128, 446),
            (cx + 160, 470), (cx + 175, 330), (cx + 150, 220), (cx + 70, 120)]
    s.path(poly(cape), s.lin([(0, "#2b3d55"), (0.6, "#1a2638"), (1, "#0f1622")]), sw=5)
    for x in (-120, -70, 70, 120):
        s.path(f"M{cx + x * 0.6},{170} Q{cx + x},{320} {cx + x * 1.05},{450}", stroke="#0b111b", sw=3)
    # ноги
    for side in (-1, 1):
        lx = cx + side * 42
        s.path(poly([(lx - 24, 350), (lx + 24, 350), (lx + 20, 470), (lx + 32, 492), (lx - 30, 492), (lx - 22, 470)]),
               s.lin([(0, "#8aa0b2"), (1, "#3d4d5e")], 0, 0, 1, 1), sw=4)
        s.path(f"M{lx - 22},{410} L{lx + 22},{410}", stroke=INK, sw=3)
    # юбка кольчуги
    s.path(poly([(cx - 78, 300), (cx + 78, 300), (cx + 92, 392), (cx + 40, 380), (cx, 398), (cx - 40, 380), (cx - 92, 392)]),
           s.lin([(0, "#5b6c7d"), (1, "#2c3846")]), sw=4)
    for y in (322, 344, 366):
        s.path(f"M{cx - 80},{y} Q{cx},{y + 10} {cx + 80},{y}", stroke="#1b2430", sw=2)
    # торс-кираса
    s.path(smooth([(cx - 80, 175), (cx + 80, 175), (cx + 74, 250), (cx + 60, 305), (cx - 60, 305), (cx - 74, 250)]),
           s.lin([(0, "#a9bccb"), (0.5, "#64788b"), (1, "#2f3c4b")], 0, 0, 1, 1), sw=5)
    s.path(f"M{cx},{182} L{cx},{300}", stroke="#2a3644", sw=3)
    s.path(f"M{cx - 60},{250} Q{cx},{270} {cx + 60},{250}", stroke="#2a3644", sw=3)
    # руна на груди
    s.path(f"M{cx - 14},{208} L{cx},{232} L{cx + 14},{208} M{cx},{232} L{cx},{252}", stroke=eye, sw=4,
           extra=f'filter="{s.glow(3)}"')
    # пояс
    s.path(poly([(cx - 72, 292), (cx + 72, 292), (cx + 74, 310), (cx - 74, 310)]), "#3b2a1e", sw=4)
    s.path(poly([(cx - 12, 290), (cx + 12, 290), (cx + 12, 312), (cx - 12, 312)]), "#b9c9d6", sw=3)
    # меч, воткнутый в землю
    blade = s.lin([(0, "#f2fbff"), (0.5, "#9fd9f2"), (1, "#4c88a8")], 0, 0, 1, 0)
    s.path(poly([(cx - 13, 250), (cx + 13, 250), (cx + 10, 470), (cx, 495), (cx - 10, 470)]), blade, sw=4)
    s.path(f"M{cx},{258} L{cx},{470}", stroke="#5c9ab8", sw=2)
    if p2:
        for y in range(280, 460, 34):
            s.path(f"M{cx - 5},{y} L{cx + 5},{y + 10}", stroke=FROST_CORE, sw=3, extra=f'filter="{s.glow(3)}"')
    s.path(poly([(cx - 52, 238), (cx + 52, 238), (cx + 46, 254), (cx - 46, 254)]), "#7b8fa1", sw=4)
    s.path(poly([(cx - 9, 196), (cx + 9, 196), (cx + 9, 240), (cx - 9, 240)]), "#3a2a20", sw=3)
    s.circle(cx, 190, 11, "#9cb2c4", INK, 3)
    # кисти на навершии
    for side in (-1, 1):
        s.path(smooth([(cx + side * 6, 200), (cx + side * 34, 196), (cx + side * 38, 222), (cx + side * 8, 228)]),
               s.lin([(0, "#cfd9e0"), (1, "#6f7f8e")]), sw=4)
    # руки (от наплечников к кистям)
    for side in (-1, 1):
        s.path(smooth([(cx + side * 88, 182), (cx + side * 106, 214), (cx + side * 70, 220), (cx + side * 36, 214),
                       (cx + side * 52, 192)]), s.lin([(0, "#8197a9"), (1, "#3c4b5b")]), sw=4)
    # наплечники
    for side in (-1, 1):
        px = cx + side * 92
        s.path(smooth([(px - 46 * side, 150), (px + 10 * side, 128), (px + 46 * side, 150), (px + 40 * side, 196),
                       (px - 10 * side, 200), (px - 40 * side, 184)]),
               s.lin([(0, "#c4d3de"), (1, "#56697b")], 0, 0, 1, 1), sw=5)
        spikes = 4 if p2 else 3
        for i in range(spikes):
            bx = px + side * (-24 + i * 18)
            h = 46 + (i % 2) * 14 + (18 if p2 else 0)
            s.path(poly([(bx - 9, 140), (bx + side * 6, 140 - h), (bx + 9, 138)]),
                   s.lin([(0, "#f4fdff"), (1, "#7fc6e4")]), sw=3)
    # голова: череп в шлеме
    s.path(smooth([(cx - 46, 112), (cx - 42, 64), (cx, 44), (cx + 42, 64), (cx + 46, 112), (cx + 26, 150), (cx - 26, 150)]),
           s.lin([(0, "#7d8f9e"), (1, "#2c3946")]), sw=5)
    s.path(smooth([(cx - 30, 96), (cx, 84), (cx + 30, 96), (cx + 26, 132), (cx, 146), (cx - 26, 132)]),
           s.lin([(0, "#e9e2cf"), (1, "#a59c86")]), sw=4)
    s.path(poly([(cx - 46, 86), (cx + 46, 86), (cx + 40, 100), (cx - 40, 100)]), "#5c6e7e", sw=4)
    s.path(f"M{cx - 7},{122} L{cx},{112} L{cx + 7},{122} Z", "#1b1416", sw=2)
    for i in range(-3, 4):
        s.path(f"M{cx + i * 6},{134} L{cx + i * 6},{142}", stroke="#3d3330", sw=2)
    s.ellipse(cx - 15, 108, 9, 7, "#0a0608")
    s.ellipse(cx + 15, 108, 9, 7, "#0a0608")
    # ледяная борода
    s.path(poly([(cx - 26, 140), (cx - 18, 178), (cx - 8, 160), (cx, 192), (cx + 8, 160), (cx + 18, 178), (cx + 26, 140)]),
           s.lin([(0, "#e8f7ff"), (1, "#86c8e4")]), sw=3)
    # корона инея
    n = 7
    for i in range(n):
        x = cx - 42 + i * 14
        h = (30 if i % 2 == 0 else 18) + (16 if p2 and i % 2 == 0 else 0)
        s.path(poly([(x - 7, 66 - abs(i - 3) * 3), (x, 66 - abs(i - 3) * 3 - h), (x + 7, 66 - abs(i - 3) * 3)]),
               s.lin([(0, "#ffffff"), (1, "#8fd6f1")]), sw=3)
    s.path(smooth([(cx - 46, 70), (cx, 56), (cx + 46, 70)], closed=False), stroke="#c8e6f3", sw=5)
    s.close()
    s.eye(cx - 15, 108, 4.5 if p2 else 3.6, eye)
    s.eye(cx + 15, 108, 4.5 if p2 else 3.6, eye)
    # морозный туман
    mist = s.blur(10)
    for x, y, rx in ((90, 492, 70), (220, 500, 110), (350, 492, 70)):
        s.ellipse(x, y, rx, 14, "#dff4ff", extra=f'opacity="{0.55 if p2 else 0.4}" filter="{mist}"')
    return s


def _bone(s, d, sw=14, fill="#e6dfcc"):
    """Кость: толстая тёмная линия + светлая сердцевина."""
    s.path(d, stroke=INK, sw=sw + 7)
    s.path(d, stroke=fill, sw=sw)


@sprite("bone_frost_wyrm", ("", "p2"))
def draw_bone_frost_wyrm(v=""):
    p2 = v == "p2"
    s = Svg(520, 500)
    grain = s.grain(0.16, 0.8, 11)
    s.shadow(280, 486, 210, 16)
    bone = "#e9e2cf"
    # крылья (позади тела)
    memb = s.lin([(0, "#bfefff", 0.75), (1, "#3f7ea3", 0.35)], 0, 0, 0, 1)
    if p2:
        fingers = [(300, 170), (360, 150), (410, 210), (430, 290)]
        sh = (250, 260)
        s.path(poly([sh, (300, 170), (330, 205), (360, 150), (372, 230), (410, 210), (396, 270), (430, 290), (360, 300)]),
               memb, sw=3)
    else:
        fingers = [(280, 40), (370, 30), (450, 80), (500, 170)]
        sh = (250, 260)
        s.path(poly([sh, (280, 40), (300, 120), (370, 30), (360, 140), (450, 80), (410, 170), (500, 170), (440, 210),
                     (470, 250), (380, 260)]), memb, sw=3)
    for f in fingers:
        _bone(s, f"M{sh[0]},{sh[1]} Q{(sh[0] + f[0]) / 2 + 10},{(sh[1] + f[1]) / 2 - 20} {f[0]},{f[1]}", 7, bone)
    s.open(f'filter="{grain}"')
    # хвост и позвоночник
    spine = [(510, 440), (470, 420), (430, 390), (390, 345), (340, 312), (280, 300), (220, 296), (180, 268),
             (150, 220), (132, 170), (122, 128)]
    s.path(smooth(spine, closed=False), stroke=INK, sw=22)
    s.path(smooth(spine, closed=False), stroke="#cfc6b0", sw=13)
    for i, (x, y) in enumerate(spine[:-1]):
        r = 7 + i * 0.9
        s.circle(x, y, r, bone, INK, 3)
        s.path(f"M{x},{y - r} L{x + 4},{y - r - 10 - i}", stroke=INK, sw=5)
        s.path(f"M{x},{y - r} L{x + 4},{y - r - 10 - i}", stroke=bone, sw=2.5)
    # рёбра
    for i, x in enumerate(range(220, 360, 22)):
        y0 = 300 + (x - 220) * 0.12
        depth = 70 - abs(i - 3) * 6
        _bone(s, f"M{x},{y0} Q{x - 30},{y0 + depth * 0.6} {x - 6},{y0 + depth}", 5, bone)
    # ноги
    for (hx, hy), (kx, ky), (fx, fy) in (((215, 300), (178, 380), (168, 470)), ((340, 315), (392, 392), (372, 470))):
        _bone(s, f"M{hx},{hy} L{kx},{ky} L{fx},{fy}", 12, bone)
        for dx in (-22, -8, 8):
            _bone(s, f"M{fx},{fy} Q{fx + dx - 6},{fy + 8} {fx + dx * 1.4 - 10},{fy + 16}", 4, bone)
    # череп (смотрит влево)
    skull = s.lin([(0, "#f6f1e3"), (1, "#a49a82")], 0, 0, 0, 1)
    s.path(smooth([(150, 120), (120, 92), (70, 92), (24, 112), (20, 128), (60, 132), (100, 144), (140, 146)]), skull, sw=5)
    jaw_open = 30 if p2 else 18
    s.path(poly([(132, 146), (96, 150), (40, 140 + jaw_open), (30, 146 + jaw_open), (90, 168), (130, 158)]), skull, sw=4)
    for x in range(36, 108, 12):
        s.path(f"M{x},{126 + (x - 36) * 0.08} l3,10 l3,-10", "#fffaf0", sw=2)
    s.ellipse(96, 112, 13, 9, "#0b0709")
    s.path(f"M40,112 l10,4", stroke=INK, sw=3)
    # рога
    for i, (ex, ey) in enumerate(((190, 60), (200, 92))):
        s.path(smooth([(132, 100 + i * 10), (160, 80 + i * 8), (ex, ey)], closed=False), stroke=INK, sw=12 - i * 3)
        s.path(smooth([(132, 100 + i * 10), (160, 80 + i * 8), (ex, ey)], closed=False), stroke="#d9e8ef", sw=6 - i * 2)
    # наросты льда на позвоночнике
    for x, y in spine[3:8]:
        s.path(poly([(x - 8, y - 12), (x - 2, y - 40 - (14 if p2 else 0)), (x + 8, y - 12)]), s.lin([(0, "#ffffff"), (1, FROST)]), sw=2.5)
    s.close()
    s.eye(96, 112, 5 if p2 else 4, FROST_CORE if p2 else FROST)
    # дыхание
    if p2:
        s.ellipse(20, 150, 34, 22, FROST, extra=f'opacity="0.8" filter="{s.blur(9)}"')
        s.ellipse(14, 150, 14, 10, FROST_CORE, extra=f'filter="{s.blur(3)}"')
    else:
        s.ellipse(34, 140, 18, 10, FROST, extra=f'opacity="0.6" filter="{s.blur(7)}"')
    mist = s.blur(12)
    for x, rx in ((120, 90), (300, 140), (450, 80)):
        s.ellipse(x, 486, rx, 14, "#dff4ff", extra=f'opacity="0.38" filter="{mist}"')
    return s


@sprite("barrow_wailer", ("", "p2"))
def draw_barrow_wailer(v=""):
    p2 = v == "p2"
    s = Svg(400, 520)
    cx = 200
    if p2:
        robe = s.lin([(0, "#5e6b78"), (0.7, "#2a323d"), (1, "#151a22")])
        skin = s.lin([(0, "#c9c4b8"), (1, "#7e7a72")])
    else:
        robe = s.lin([(0, "#cdefff", 0.85), (0.55, "#5fa5c8", 0.6), (1, "#1d4a66", 0.0)])
        skin = s.lin([(0, "#f2fbff", 0.95), (1, "#9fd2e8", 0.85)])
        s.circle(cx, 200, 170, s.rad([(0, FROST, 0.35), (1, FROST, 0)]))
    s.shadow(cx, 505, 90, 10, 0.3)
    grain = s.grain(0.14, 0.9, 5)
    s.open(f'filter="{grain}"')
    # волосы, развевающиеся назад
    hair = s.lin([(0, "#1f2f44" if not p2 else "#2a2a30"), (1, "#0b1520", 0.6 if not p2 else 1)])
    s.path(smooth([(cx - 40, 70), (cx + 10, 40), (cx + 70, 70), (cx + 130, 150), (cx + 170, 230), (cx + 120, 210),
                   (cx + 140, 280), (cx + 80, 230), (cx + 60, 300), (cx + 20, 160), (cx - 50, 160), (cx - 70, 260),
                   (cx - 90, 200), (cx - 120, 250), (cx - 80, 130)]), hair, sw=4)
    # платье-саван
    tail = 470 if p2 else 500
    s.path(smooth([(cx - 46, 150), (cx + 46, 150), (cx + 80, 260), (cx + 120, 380), (cx + 90, tail - 20), (cx + 50, tail),
                   (cx + 10, tail - 30), (cx - 30, tail + 4), (cx - 70, tail - 26), (cx - 110, tail - 40), (cx - 120, 380),
                   (cx - 80, 260)]), robe, sw=4)
    for x in (-50, -15, 25, 60):
        s.path(f"M{cx + x * 0.4},{170} Q{cx + x},{300} {cx + x * 1.4},{tail - 40}", stroke="#0e2333" if not p2 else "#10151c", sw=2.5,
               extra='opacity="0.6"')
    if p2:  # проступающие рёбра
        for i in range(4):
            y = 190 + i * 18
            s.path(f"M{cx - 32},{y} Q{cx},{y + 10} {cx + 32},{y}", stroke="#d8d2c4", sw=4)
            s.path(f"M{cx - 32},{y} Q{cx},{y + 10} {cx + 32},{y}", stroke=INK, sw=1.5)
    # руки, поднятые в плаче
    for side in (-1, 1):
        sx = cx + side * 40
        s.path(smooth([(sx, 165), (sx + side * 60, 140), (sx + side * 110, 90), (sx + side * 120, 100),
                       (sx + side * 70, 160), (sx + side * 10, 200)]), robe, sw=4)
        hx, hy = sx + side * 118, 92
        s.path(smooth([(hx - 10, hy + 10), (hx, hy - 6), (hx + side * 12, hy - 4), (hx + side * 6, hy + 12)]), skin, sw=3)
        for k in range(4):
            fx = hx + side * (k * 6 - 4)
            s.path(f"M{fx},{hy - 2} l{side * (4 + k)},-{26 - k * 3}", stroke=INK, sw=5)
            s.path(f"M{fx},{hy - 2} l{side * (4 + k)},-{26 - k * 3}", stroke="#e8f6fb" if not p2 else "#cfc8b8", sw=2.5)
    # лицо
    s.path(smooth([(cx - 30, 80), (cx, 56), (cx + 30, 80), (cx + 26, 128), (cx, 150), (cx - 26, 128)]), skin, sw=4)
    s.ellipse(cx - 12, 100, 8, 11, "#05080c")
    s.ellipse(cx + 12, 100, 8, 11, "#05080c")
    s.path(smooth([(cx - 10, 126), (cx, 120), (cx + 10, 126), (cx + 7, 142), (cx, 146), (cx - 7, 142)]), "#05080c", sw=2)
    # капюшон-вуаль
    s.path(smooth([(cx - 44, 120), (cx - 40, 60), (cx, 34), (cx + 40, 60), (cx + 44, 120), (cx + 30, 70), (cx, 52), (cx - 30, 70)]),
           robe, sw=3)
    s.close()
    col = FROST_CORE if p2 else FROST
    s.eye(cx - 12, 102, 3.6, col)
    s.eye(cx + 12, 102, 3.6, col)
    if not p2:
        for x, y in ((90, 300), (310, 260), (120, 430), (300, 420)):
            s.circle(x, y, 5, FROST_CORE, extra=f'opacity="0.7" filter="{s.glow(4)}"')
    return s


@sprite("barrow_huskarl")
def draw_barrow_huskarl(v=""):
    s = Svg(260, 320)
    cx = 130
    s.shadow(cx, 308, 80, 9)
    s.open(f'filter="{s.grain(0.18, 0.9, 13)}"')
    # секира за спиной справа
    s.path("M182,70 L196,290", stroke=INK, sw=11)
    s.path("M182,70 L196,290", stroke="#6b4a30", sw=6)
    s.path(smooth([(176, 64), (226, 40), (236, 96), (214, 120), (186, 104)]), s.lin([(0, "#d9e3ea"), (1, "#6c7d8b")]), sw=4)
    # ноги
    for side in (-1, 1):
        lx = cx + side * 22
        s.path(poly([(lx - 14, 210), (lx + 14, 210), (lx + 12, 290), (lx + 20, 304), (lx - 18, 304), (lx - 12, 290)]),
               s.lin([(0, "#6d6455"), (1, "#2f2a24")]), sw=4)
    # тело в рваной кольчуге
    s.path(smooth([(cx - 46, 108), (cx + 46, 108), (cx + 54, 200), (cx + 40, 236), (cx - 40, 236), (cx - 54, 200)]),
           s.lin([(0, "#7d8a94"), (1, "#323a42")]), sw=4)
    for y in range(126, 230, 16):
        s.path(f"M{cx - 48},{y} Q{cx},{y + 6} {cx + 48},{y}", stroke="#20262c", sw=2)
    s.path(poly([(cx - 48, 196), (cx + 48, 196), (cx + 48, 208), (cx - 48, 208)]), "#3b2a1e", sw=3)
    # голова: череп в шлеме-шишаке
    s.path(smooth([(cx - 26, 84), (cx, 70), (cx + 26, 84), (cx + 22, 112), (cx, 122), (cx - 22, 112)]),
           s.lin([(0, "#e3dccb"), (1, "#9b927d")]), sw=4)
    s.path(smooth([(cx - 30, 86), (cx - 24, 50), (cx, 34), (cx + 24, 50), (cx + 30, 86)], closed=False) + " Z",
           s.lin([(0, "#a7b4bd"), (1, "#4a5661")]), sw=4)
    s.path(f"M{cx},{84} L{cx},{106}", stroke="#4a5661", sw=6)
    s.ellipse(cx - 11, 96, 6, 5, "#08060a")
    s.ellipse(cx + 11, 96, 6, 5, "#08060a")
    # круглый щит (слева, к героям)
    s.circle(cx - 46, 176, 54, s.rad([(0, "#6f5137"), (1, "#3a2817")]), INK, 5)
    s.circle(cx - 46, 176, 54, "none", "#8a98a3", 5)
    for a in range(0, 360, 60):
        import math
        x2 = cx - 46 + 48 * math.cos(math.radians(a))
        y2 = 176 + 48 * math.sin(math.radians(a))
        s.path(f"M{cx - 46},176 L{x2:.1f},{y2:.1f}", stroke="#2b1d10", sw=2)
    s.circle(cx - 46, 176, 13, "#a9b6bf", INK, 3)
    # иней
    for x, y in ((cx - 80, 140), (cx + 30, 110), (cx - 20, 230)):
        s.path(poly([(x - 6, y), (x, y - 16), (x + 6, y)]), "#e8f8ff", sw=2)
    s.close()
    s.eye(cx - 11, 96, 2.8, FROST)
    s.eye(cx + 11, 96, 2.8, FROST)
    return s


# ═════════════════════════ 2б — Заводные Глубины ═════════════════════════

BRASS = [(0, "#ffe2a0"), (0.35, "#d9a24e"), (0.75, "#8a5a24"), (1, "#4a2e12")]
COPPER = [(0, "#ffc19a"), (0.4, "#c8703e"), (1, "#5a2a14")]
IRON = [(0, "#b9b3a8"), (1, "#3d3a36")]


def _rivets(s, pts, r=3.2):
    for x, y in pts:
        s.circle(x, y, r, "#fff0c4", INK, 1.5)


def _gear(s, cx, cy, r, teeth=10, fill="#b98a3e"):
    import math
    pts = []
    for i in range(teeth * 2):
        a = math.radians(i * 180 / teeth)
        rr = r if i % 2 == 0 else r * 0.8
        pts += [(cx + rr * math.cos(a - 0.12), cy + rr * math.sin(a - 0.12)), (cx + rr * math.cos(a + 0.12), cy + rr * math.sin(a + 0.12))]
    s.path(poly(pts), fill, sw=3)
    s.circle(cx, cy, r * 0.35, "#3a2610", INK, 2)


def _steam(s, x, y, scale=1.0, opacity=0.7):
    f = s.blur(6 * scale)
    for i, (dx, dy, r) in enumerate(((0, 0, 18), (14, -22, 22), (-6, -48, 26), (16, -76, 30))):
        s.circle(x + dx * scale, y + dy * scale, r * scale, "#f3ece4", extra=f'opacity="{opacity - i * 0.12:.2f}" filter="{f}"')


@sprite("clockwork_colossus", ("", "p2"))
def draw_clockwork_colossus(v=""):
    p2 = v == "p2"
    s = Svg(500, 520)
    cx = 250
    hot = BLOOD if p2 else EMBER
    s.shadow(cx, 505, 200, 16)
    # трубы за спиной + дым
    for x in (cx - 70, cx + 70):
        s.path(poly([(x - 18, 40), (x + 18, 40), (x + 14, 150), (x - 14, 150)]), s.lin(IRON, 0, 0, 1, 0), sw=4)
        s.path(poly([(x - 24, 30), (x + 24, 30), (x + 24, 44), (x - 24, 44)]), "#5a534a", sw=4)
    s.open(f'filter="{s.grain(0.16, 0.85, 17)}"')
    # ноги-колонны
    for side in (-1, 1):
        lx = cx + side * 70
        s.path(poly([(lx - 36, 360), (lx + 36, 360), (lx + 44, 470), (lx + 56, 500), (lx - 56, 500), (lx - 44, 470)]),
               s.lin(BRASS, 0, 0, 1, 1), sw=5)
        s.path(f"M{lx - 40},{420} L{lx + 40},{420}", stroke=INK, sw=4)
        _rivets(s, [(lx - 30, 436), (lx, 436), (lx + 30, 436)])
    # бочка-торс
    s.path(smooth([(cx - 120, 150), (cx + 120, 150), (cx + 140, 260), (cx + 110, 380), (cx - 110, 380), (cx - 140, 260)]),
           s.lin(BRASS, 0, 0, 1, 1), sw=6)
    if p2:
        # сорванные пластины: открытые шестерни
        s.path(smooth([(cx - 100, 190), (cx - 10, 180), (cx - 20, 300), (cx - 110, 320)]), "#2a1a0e", sw=4)
        _gear(s, cx - 66, 230, 30, 10, "#c79a48")
        _gear(s, cx - 40, 290, 22, 8, "#a77a34")
        s.path(poly([(cx - 104, 186), (cx - 140, 150), (cx - 150, 196)]), s.lin(BRASS), sw=4)
    for y in (200, 330):
        s.path(f"M{cx - 128},{y} Q{cx},{y + 18} {cx + 128},{y}", stroke=INK, sw=4)
    _rivets(s, [(cx - 120 + i * 30, 206 + (abs(i - 4) * -2)) for i in range(9)])
    # топка
    s.path(smooth([(cx - 48, 230), (cx + 48, 230), (cx + 52, 300), (cx - 52, 300)]), "#24170c", sw=5)
    s.ellipse(cx, 266, 44, 30, s.rad([(0, "#fff3c0"), (0.4, hot), (1, "#3a0e04")]), extra=f'filter="{s.glow(6)}"')
    for x in range(-36, 40, 14):
        s.path(f"M{cx + x},{236} L{cx + x},{296}", stroke="#1a0f06", sw=5)
    # голова-котелок
    s.path(smooth([(cx - 44, 150), (cx - 40, 104), (cx, 88), (cx + 40, 104), (cx + 44, 150)]), s.lin(IRON, 0, 0, 1, 1), sw=5)
    s.path(poly([(cx - 30, 118), (cx + 30, 118), (cx + 26, 130), (cx - 26, 130)]), "#120a06", sw=3)
    # руки-поршни
    for side in (-1, 1):
        sx = cx + side * 130
        s.circle(sx, 180, 44, s.rad([(0, "#ffdc90"), (0.5, "#b07a34"), (1, "#4a2e12")], fx=0.35, fy=0.3), INK, 5)
        _rivets(s, [(sx + side * 20, 160), (sx - side * 12, 150), (sx + side * 30, 192)])
        s.path(poly([(sx + side * 4 - 18, 216), (sx + side * 4 + 18, 216), (sx + side * 30 + 14, 330), (sx + side * 30 - 14, 330)]),
               s.lin(IRON, 0, 0, 1, 0), sw=4)
        s.path(f"M{sx + side * 10},{230} L{sx + side * 32},{320}", stroke="#e9e3d6", sw=4)
        # кулак-молот
        fx = sx + side * 34
        s.path(smooth([(fx - 40, 330), (fx + 40, 330), (fx + 46, 400), (fx + 30, 430), (fx - 30, 430), (fx - 46, 400)]),
               s.lin(BRASS, 0, 0, 1, 1), sw=5)
        for k in range(3):
            s.path(f"M{fx - 30 + k * 30},{340} L{fx - 30 + k * 30},{420}", stroke=INK, sw=3)
    s.close()
    s.eye(cx - 14, 124, 4.5, hot, "#fff4d0")
    s.eye(cx + 14, 124, 4.5, hot, "#fff4d0")
    _steam(s, cx - 70, 20, 0.9, 0.55)
    _steam(s, cx + 70, 20, 0.9, 0.55)
    if p2:
        _steam(s, cx - 120, 250, 0.8, 0.75)
        _steam(s, cx + 130, 300, 0.7, 0.7)
        for x, y in ((cx - 60, 340), (cx + 90, 230)):
            s.circle(x, y, 6, "#ffd27a", extra=f'filter="{s.glow(5)}"')
    return s


def _engineer(s, cx, base, scale=1.0, eye=STEAM):
    """Маленький инженер в плаще и очках: используется в обеих фазах Архитектора."""
    k = scale
    coat = s.lin([(0, "#6b4b33"), (1, "#2e1d12")])
    s.path(smooth([(cx - 34 * k, base - 160 * k), (cx + 34 * k, base - 160 * k), (cx + 56 * k, base - 20 * k),
                   (cx + 30 * k, base), (cx, base - 14 * k), (cx - 30 * k, base), (cx - 56 * k, base - 20 * k)]), coat, sw=4)
    s.path(f"M{cx},{base - 150 * k} L{cx},{base - 10 * k}", stroke="#1d120a", sw=3)
    # ремни с инструментами
    s.path(f"M{cx - 34 * k},{base - 140 * k} L{cx + 40 * k},{base - 70 * k}", stroke="#2a1a0e", sw=7 * k)
    for i in range(3):
        s.path(f"M{cx - 20 * k + i * 18 * k},{base - 124 * k + i * 16 * k} l0,{14 * k}", stroke="#d8c08a", sw=4 * k)
    # голова
    s.circle(cx, base - 186 * k, 28 * k, s.lin([(0, "#d9b48e"), (1, "#8f6a4a")]), INK, 4)
    s.path(smooth([(cx - 34 * k, base - 192 * k), (cx - 26 * k, base - 226 * k), (cx + 26 * k, base - 226 * k),
                   (cx + 34 * k, base - 192 * k)]), "#3b2a1c", sw=4)
    # борода
    s.path(smooth([(cx - 24 * k, base - 176 * k), (cx + 24 * k, base - 176 * k), (cx + 14 * k, base - 140 * k),
                   (cx, base - 128 * k), (cx - 14 * k, base - 140 * k)]), "#b8b0a2", sw=3)
    # очки
    for side in (-1, 1):
        s.circle(cx + side * 12 * k, base - 192 * k, 10 * k, "#2b1a0c", INK, 3)
        s.eye(cx + side * 12 * k, base - 192 * k, 5 * k, eye, "#fff4d0")
    s.path(f"M{cx - 2 * k},{base - 192 * k} L{cx + 2 * k},{base - 192 * k}", stroke=INK, sw=3)


@sprite("automaton_architect", ("", "p2"))
def draw_automaton_architect(v=""):
    p2 = v == "p2"
    s = Svg(440, 520)
    cx = 220
    s.shadow(cx, 506, 170 if p2 else 110, 14)
    s.open(f'filter="{s.grain(0.15, 0.9, 19)}"')
    if not p2:
        # верстак-тележка и чертёж
        s.path(poly([(cx + 40, 400), (cx + 170, 400), (cx + 160, 470), (cx + 50, 470)]), s.lin(BRASS, 0, 0, 1, 1), sw=4)
        for x in (cx + 70, cx + 140):
            s.circle(x, 482, 20, "#3a2a1a", INK, 4)
            s.circle(x, 482, 6, "#d9a24e", INK, 2)
        _gear(s, cx + 110, 376, 26, 9)
        _gear(s, cx + 150, 388, 16, 7, "#9c6c2c")
        _engineer(s, cx - 20, 500, 1.35)
        # механическая рука с циркулем
        s.path(f"M{cx - 60},{300} L{cx - 120},{250} L{cx - 150},{170}", stroke=INK, sw=16)
        s.path(f"M{cx - 60},{300} L{cx - 120},{250} L{cx - 150},{170}", stroke="#c9944a", sw=9)
        s.circle(cx - 120, 250, 10, "#e8c27a", INK, 3)
        s.path(f"M{cx - 150},{170} L{cx - 176},{110} M{cx - 150},{170} L{cx - 128},{110}", stroke=INK, sw=8)
        s.path(f"M{cx - 150},{170} L{cx - 176},{110} M{cx - 150},{170} L{cx - 128},{110}", stroke="#e9e3d6", sw=4)
        # свиток чертежа
        s.path(poly([(cx + 20, 300), (cx + 90, 284), (cx + 100, 340), (cx + 30, 356)]), "#3c6a9a", sw=3)
        for i in range(3):
            s.path(f"M{cx + 32},{312 + i * 12} L{cx + 86},{300 + i * 12}", stroke="#cfe4ff", sw=1.5)
    else:
        # экзокаркас: ноги
        for side in (-1, 1):
            lx = cx + side * 96
            s.path(poly([(lx - 26, 300), (lx + 26, 300), (lx + side * 20 + 22, 400), (lx + 30, 490), (lx - 30, 490),
                         (lx + side * 20 - 22, 400)]), s.lin(BRASS, 0, 0, 1, 1), sw=5)
            s.circle(lx + side * 20, 400, 16, "#e8c27a", INK, 4)
            s.path(f"M{lx},{310} L{lx + side * 20},{400} L{lx},{480}", stroke="#d8d2c4", sw=4)
        # рама и кабина
        s.path(smooth([(cx - 130, 180), (cx + 130, 180), (cx + 120, 320), (cx - 120, 320)]), s.lin(IRON, 0, 0, 1, 1), sw=6)
        s.path(smooth([(cx - 80, 170), (cx + 80, 170), (cx + 76, 300), (cx - 76, 300)]), "#1a120c", sw=4)
        _engineer(s, cx, 300, 0.75)
        s.path(smooth([(cx - 84, 170), (cx - 60, 100), (cx + 60, 100), (cx + 84, 170)], closed=False), stroke="#e9d9a8", sw=4)
        s.path(smooth([(cx - 84, 170), (cx - 60, 100), (cx + 60, 100), (cx + 84, 170)], closed=False) + " Z",
               s.lin([(0, "#cfe9ff", 0.45), (1, "#7fa8c8", 0.15)]), stroke=None)
        _rivets(s, [(cx - 120 + i * 30, 312) for i in range(9)])
        # руки экзокаркаса
        for side in (-1, 1):
            sx = cx + side * 150
            s.circle(sx, 200, 34, s.rad([(0, "#ffdc90"), (0.5, "#b07a34"), (1, "#4a2e12")], fx=0.35, fy=0.3), INK, 5)
            s.path(f"M{sx},{220} L{sx + side * 22},{330}", stroke=INK, sw=26)
            s.path(f"M{sx},{220} L{sx + side * 22},{330}", stroke="#8a8378", sw=17)
        # молот справа, клешня слева
        s.path(poly([(cx + 140, 320), (cx + 220, 320), (cx + 220, 390), (cx + 140, 390)]), s.lin(IRON, 0, 0, 1, 1), sw=5)
        s.path(poly([(cx - 190, 330), (cx - 150, 320), (cx - 140, 400), (cx - 170, 380), (cx - 200, 400)]), s.lin(BRASS), sw=5)
        _steam(s, cx - 120, 150, 0.7, 0.6)
        _steam(s, cx + 120, 150, 0.7, 0.6)
    s.close()
    return s


@sprite("brass_sentry")
def draw_brass_sentry(v=""):
    s = Svg(240, 300)
    cx = 120
    s.shadow(cx, 290, 80, 8)
    s.open(f'filter="{s.grain(0.15, 0.9, 23)}"')
    for dx in (-60, 0, 60):
        s.path(f"M{cx},{190} L{cx + dx},{284}", stroke=INK, sw=12)
        s.path(f"M{cx},{190} L{cx + dx},{284}", stroke="#b98a3e", sw=6)
    s.path(smooth([(cx - 64, 200), (cx - 66, 130), (cx, 90), (cx + 66, 130), (cx + 64, 200)]), s.lin(BRASS, 0, 0, 1, 1), sw=5)
    s.path(f"M{cx - 64},{176} Q{cx},{190} {cx + 64},{176}", stroke=INK, sw=3)
    _rivets(s, [(cx - 48, 184), (cx - 16, 188), (cx + 16, 188), (cx + 48, 184)])
    s.circle(cx - 10, 146, 26, "#1a0f06", INK, 4)
    s.eye(cx - 10, 146, 11, "#7fd6ff", "#effcff")
    # антенна-катушка
    s.path(f"M{cx + 30},{104} L{cx + 40},{40}", stroke=INK, sw=8)
    s.path(f"M{cx + 30},{104} L{cx + 40},{40}", stroke="#d8c08a", sw=4)
    for i in range(4):
        s.ellipse(cx + 34 + i * 1.5, 92 - i * 14, 9, 3, "#c8703e", INK, 2)
    s.close()
    s.path("M160,40 l-12,-14 l10,2 l-6,-16", stroke="#bff0ff", sw=3, extra=f'filter="{s.glow(3)}"')
    s.circle(cx + 40, 38, 6, "#e8fbff", extra=f'filter="{s.glow(5)}"')
    return s


@sprite("boiler_wyrm", ("", "p2"))
def draw_boiler_wyrm(v=""):
    p2 = v == "p2"
    s = Svg(520, 480)
    s.shadow(280, 466, 210, 14)
    s.open(f'filter="{s.grain(0.15, 0.85, 29)}"')
    body = [(500, 430), (430, 448), (350, 440), (300, 400), (330, 350), (400, 330), (430, 280), (390, 230), (310, 220),
            (240, 240), (190, 200), (160, 150), (150, 110)]
    import math
    s.path(smooth(body, closed=False), stroke=INK, sw=34)
    s.path(smooth(body, closed=False), stroke="#6a3418", sw=26)
    s.path(smooth(body, closed=False), stroke="#a8582c", sw=10, extra='opacity="0.7"')
    for i, (x, y) in enumerate(body):
        r = 20 + min(i, len(body) - 1 - i) * 3.2 + (6 if 3 < i < 9 else 0)
        heat = p2 and i % 2 == 0
        fill = s.rad([(0, "#ffe1b0"), (0.4, "#ff6a2a" if heat else "#c8703e"), (1, "#5a1a08" if heat else "#5a2a14")], fx=0.35, fy=0.3)
        s.circle(x, y, r, fill, INK, 4)
        s.circle(x, y, r * 0.62, "none", "#3a1a0a", 2.5)
        _rivets(s, [(x + r * 0.78 * math.cos(a), y + r * 0.78 * math.sin(a)) for a in (0.3, 2.4, 4.4)], 2.4)
    # вентили на спине
    for i in (5, 8):
        x, y = body[i]
        s.path(f"M{x},{y - 30} L{x},{y - 52}", stroke=INK, sw=8)
        s.path(f"M{x},{y - 30} L{x},{y - 52}", stroke="#8a8378", sw=4)
        s.circle(x, y - 56, 12, "none", "#c8312a", 5)
        s.circle(x, y - 56, 12, "none", INK, 1.5)
    # голова-топка
    hx, hy = 100, 104
    s.path(smooth([(hx + 70, hy - 30), (hx + 20, hy - 48), (hx - 50, hy - 30), (hx - 72, hy - 6), (hx - 40, hy + 6),
                   (hx + 40, hy + 10), (hx + 76, hy + 6)]), s.lin(COPPER, 0, 0, 0, 1), sw=5)
    jaw = 30 if p2 else 20
    s.path(poly([(hx + 70, hy + 8), (hx - 40, hy + 14), (hx - 70, hy + 10 + jaw), (hx - 30, hy + 34 + jaw * 0.5), (hx + 66, hy + 30)]),
           s.lin(COPPER, 0, 0, 0, 1), sw=5)
    s.ellipse(hx - 28, hy + 14 + jaw * 0.4, 30, 8 + jaw * 0.3, s.rad([(0, "#fff3c0"), (0.5, EMBER), (1, "#3a0e04")]),
              extra=f'filter="{s.glow(5)}"')
    for x in range(int(hx - 60), int(hx + 30), 14):
        s.path(f"M{x},{hy + 6} l5,10 l5,-10", "#e9e3d6", sw=2)
    s.path(f"M{hx + 30},{hy - 44} L{hx + 60},{hy - 80} L{hx + 64},{hy - 36}", s.lin(IRON), sw=4)
    s.circle(hx - 10, hy - 18, 12, "#1a0f06", INK, 3)
    s.close()
    s.eye(hx - 10, hy - 18, 6, BLOOD if p2 else STEAM, "#fff4d0")
    _steam(s, body[5][0], body[5][1] - 80, 0.7 if not p2 else 1.0, 0.6)
    _steam(s, body[8][0], body[8][1] - 80, 0.6 if not p2 else 0.9, 0.55)
    if p2:
        _steam(s, hx - 70, hy + 40, 0.8, 0.7)
    return s


# ═════════════════════════ 4а — Кровавый Двор ═════════════════════════

CRIMSON = [(0, "#e2475a"), (0.5, "#9a1528"), (1, "#3a0510")]
PALE = [(0, "#f5e9ec"), (1, "#bfa6ad")]
GOLD = [(0, "#fff0b0"), (0.5, "#d4a640"), (1, "#6a4a10")]


@sprite("crimson_countess", ("", "p2"))
def draw_crimson_countess(v=""):
    p2 = v == "p2"
    s = Svg(440, 540)
    cx = 220
    if p2:
        s.circle(cx, 200, 200, s.rad([(0, BLOOD, 0.45), (1, BLOOD, 0)]))
        # крылья летучей мыши
        for side in (-1, 1):
            pts = [(cx + side * 30, 170), (cx + side * 120, 60), (cx + side * 210, 40), (cx + side * 190, 120),
                   (cx + side * 214, 200), (cx + side * 170, 190), (cx + side * 180, 270), (cx + side * 130, 240),
                   (cx + side * 120, 310), (cx + side * 50, 250)]
            s.path(poly(pts), s.lin([(0, "#4a0c18"), (1, "#1a0308")], 0, 0, side * 0.5 + 0.5, 1), sw=4)
            for k in (1, 3, 5, 7):
                s.path(f"M{cx + side * 30},170 L{pts[k][0]},{pts[k][1]}", stroke="#2a050c", sw=3)
    s.shadow(cx, 526, 150, 14)
    s.open(f'filter="{s.grain(0.14, 0.9, 31)}"')
    gown = s.lin(CRIMSON, 0, 0, 1, 1)
    hem = 528
    pts = [(cx - 40, 200), (cx + 40, 200), (cx + 70, 300), (cx + 130, 420), (cx + 170, hem), (cx - 170, hem), (cx - 130, 420), (cx - 70, 300)]
    if p2:
        pts = pts[:4] + [(cx + 170, hem), (cx + 130, hem - 26), (cx + 96, hem), (cx + 50, hem - 30), (cx, hem),
                         (cx - 50, hem - 30), (cx - 96, hem), (cx - 130, hem - 26), (cx - 170, hem)] + pts[6:]
    s.path(poly(pts), gown, sw=5)
    for x in (-110, -60, 0, 60, 110):
        s.path(f"M{cx + x * 0.25},{230} Q{cx + x * 0.7},{380} {cx + x},{hem - 6}", stroke="#4a0712", sw=3)
    # корсет с золотом
    s.path(smooth([(cx - 42, 170), (cx + 42, 170), (cx + 36, 250), (cx, 270), (cx - 36, 250)]), s.lin([(0, "#2a0a12"), (1, "#0e0206")]), sw=4)
    s.path(f"M{cx - 30},{190} L{cx},{258} L{cx + 30},{190}", stroke="#d4a640", sw=3)
    # высокий воротник
    s.path(poly([(cx - 30, 150), (cx - 90, 70), (cx - 60, 150), (cx - 40, 172)]), s.lin(CRIMSON), sw=4)
    s.path(poly([(cx + 30, 150), (cx + 90, 70), (cx + 60, 150), (cx + 40, 172)]), s.lin(CRIMSON), sw=4)
    # руки
    skin = s.lin(PALE)
    s.path(smooth([(cx - 40, 178), (cx - 76, 250), (cx - 60, 300), (cx - 46, 296), (cx - 54, 250), (cx - 30, 200)]), gown, sw=4)
    s.path(smooth([(cx + 40, 178), (cx + 90, 210), (cx + 120, 170), (cx + 128, 180), (cx + 100, 230), (cx + 36, 210)]), gown, sw=4)
    s.circle(cx - 54, 304, 10, skin, INK, 3)
    s.circle(cx + 124, 170, 10, skin, INK, 3)
    if p2:
        for k in range(4):
            s.path(f"M{cx + 120 + k * 4},{162} l{6 + k * 2},-{18 - k * 2}", stroke=INK, sw=5)
            s.path(f"M{cx + 120 + k * 4},{162} l{6 + k * 2},-{18 - k * 2}", stroke="#efe0e4", sw=2.5)
    else:
        # роза
        s.path(f"M{cx + 124},{168} L{cx + 138},{110}", stroke="#2a4a1a", sw=4)
        s.circle(cx + 140, 104, 12, s.rad([(0, "#ff6a7a"), (1, "#7a0a1a")]), INK, 3)
        s.path(f"M{cx + 134},{102} q6,-8 12,0 q-6,8 -12,0", stroke="#4a0510", sw=2)
    # шея и лицо
    s.path(f"M{cx - 10},{150} L{cx - 10},{172} L{cx + 10},{172} L{cx + 10},{150}", skin, sw=3)
    s.path(smooth([(cx - 26, 108), (cx, 88), (cx + 26, 108), (cx + 22, 140), (cx, 158), (cx - 22, 140)]), skin, sw=4)
    s.path(f"M{cx - 8},{144} Q{cx},{150} {cx + 8},{144}", stroke="#8a0a1e", sw=3)
    if p2:
        s.path(f"M{cx - 6},{146} l2,7 l2,-6 M{cx + 2},{146} l2,7 l2,-6", "#ffffff", sw=1.5)
    # причёска
    hair = s.lin([(0, "#2a1418"), (1, "#0a0406")])
    s.path(smooth([(cx - 30, 120), (cx - 34, 80), (cx - 10, 50), (cx + 10, 50), (cx + 34, 80), (cx + 30, 120), (cx + 20, 96),
                   (cx - 20, 96)]), hair, sw=4)
    s.circle(cx, 40, 22, hair, INK, 4)
    s.path(star(cx, 34, 10, 4, 5), "#d4a640", sw=2)
    s.close()
    s.eye(cx - 10, 118, 3.4 if not p2 else 4.2, BLOOD, "#ffe0e4")
    s.eye(cx + 10, 118, 3.4 if not p2 else 4.2, BLOOD, "#ffe0e4")
    for x, y in ((cx - 100, 470), (cx + 90, 500), (cx + 140, 440)):
        s.path(f"M{x},{y} q4,-10 8,0 q-4,6 -8,0", "#ff5a6a", stroke=None, extra='opacity="0.7"')
    return s


@sprite("eclipse_warden", ("", "night"))
def draw_eclipse_warden(v=""):
    night = v == "night"
    s = Svg(460, 520)
    cx = 230
    stone = s.lin([(0, "#3e4660"), (1, "#141826")] if night else [(0, "#d8d0c0"), (1, "#7a6f60")], 0, 0, 1, 1)
    # диск за головой
    if night:
        s.circle(cx, 120, 140, s.rad([(0, "#c8d8ff", 0.5), (1, "#5a6aa8", 0)]))
        s.circle(cx, 120, 92, s.lin([(0, "#f2f6ff"), (1, "#8a9ac8")]), INK, 5)
        s.circle(cx + 34, 104, 82, "#141826", stroke=None)
    else:
        s.circle(cx, 120, 150, s.rad([(0, "#fff2b0", 0.7), (1, "#ffb84a", 0)]))
        for i in range(16):
            import math
            a = math.radians(i * 22.5)
            s.path(poly([(cx + 92 * math.cos(a - 0.08), 120 + 92 * math.sin(a - 0.08)), (cx + 130 * math.cos(a), 120 + 130 * math.sin(a)),
                         (cx + 92 * math.cos(a + 0.08), 120 + 92 * math.sin(a + 0.08))]), "#ffd36a", sw=3)
        s.circle(cx, 120, 92, s.rad([(0, "#fff6c8"), (0.7, "#f0b440"), (1, "#a06a10")]), INK, 5)
    s.shadow(cx, 506, 170, 14)
    s.open(f'filter="{s.grain(0.2, 0.75, 37)}"')
    # сложенные крылья
    for side in (-1, 1):
        s.path(poly([(cx + side * 60, 170), (cx + side * 190, 120), (cx + side * 200, 200), (cx + side * 180, 330),
                     (cx + side * 150, 280), (cx + side * 130, 360), (cx + side * 80, 300)]), stone, sw=5)
        s.path(f"M{cx + side * 70},{180} L{cx + side * 186},{130}", stroke=INK, sw=3)
    # постамент
    s.path(poly([(cx - 150, 460), (cx + 150, 460), (cx + 170, 506), (cx - 170, 506)]), stone, sw=5)
    s.path(f"M{cx - 140},{476} L{cx + 140},{476}", stroke=INK, sw=2.5)
    # присевшая фигура: ноги
    for side in (-1, 1):
        s.path(smooth([(cx + side * 30, 330), (cx + side * 100, 350), (cx + side * 110, 430), (cx + side * 90, 462),
                       (cx + side * 40, 462), (cx + side * 60, 420), (cx + side * 40, 380)]), stone, sw=5)
        for k in range(3):
            x = cx + side * (50 + k * 16)
            s.path(f"M{x},{460} l{side * 4},10", stroke="#e8e0d0" if not night else "#b8c8ff", sw=4)
    # торс
    s.path(smooth([(cx - 70, 170), (cx + 70, 170), (cx + 60, 300), (cx + 30, 350), (cx - 30, 350), (cx - 60, 300)]), stone, sw=5)
    for y in (220, 260, 300):
        s.path(f"M{cx - 50},{y} Q{cx},{y + 10} {cx + 50},{y}", stroke=INK, sw=2.5)
    # руки на коленях
    for side in (-1, 1):
        s.path(smooth([(cx + side * 70, 180), (cx + side * 96, 260), (cx + side * 84, 360), (cx + side * 66, 360),
                       (cx + side * 72, 260), (cx + side * 56, 200)]), stone, sw=5)
        for k in range(3):
            s.path(f"M{cx + side * (64 + k * 9)},{360} l{side * 2},{14}", stroke=INK, sw=4)
    # голова-маска
    s.path(smooth([(cx - 40, 150), (cx - 44, 100), (cx, 70), (cx + 44, 100), (cx + 40, 150), (cx, 180)]), stone, sw=5)
    for side in (-1, 1):
        s.path(poly([(cx + side * 30, 88), (cx + side * 54, 40), (cx + side * 44, 96)]), stone, sw=4)
    s.path(f"M{cx - 20},{150} L{cx + 20},{150}", stroke=INK, sw=3)
    if not night:
        s.path(f"M{cx - 28},{118} L{cx - 8},{118} M{cx + 8},{118} L{cx + 28},{118}", stroke=INK, sw=4)
    # трещины
    s.path(f"M{cx - 40},{230} l14,20 l-6,18 l12,16", stroke=INK, sw=2)
    s.path(f"M{cx + 30},{180} l-10,24 l14,14", stroke=INK, sw=2)
    s.close()
    if night:
        s.eye(cx - 18, 118, 5, "#a8c0ff", "#ffffff")
        s.eye(cx + 18, 118, 5, "#a8c0ff", "#ffffff")
        for x, y in ((60, 80), (400, 60), (380, 260), (70, 300), (420, 180)):
            s.path(star(x, y, 6, 2.2, 4), "#e8eeff", stroke=None, extra=f'filter="{s.glow(2)}"')
    return s


@sprite("chalice_hierophant", ("", "p2"))
def draw_chalice_hierophant(v=""):
    p2 = v == "p2"
    s = Svg(420, 540)
    cx = 210
    if p2:
        s.circle(cx, 150, 190, s.rad([(0, BLOOD, 0.55), (1, BLOOD, 0)]))
    s.shadow(cx, 528, 140, 13)
    s.open(f'filter="{s.grain(0.15, 0.9, 41)}"')
    robe = s.lin([(0, "#2a0a12"), (1, "#0a0205")])
    # мантия
    s.path(poly([(cx - 60, 196), (cx + 60, 196), (cx + 110, 370), (cx + 150, 528), (cx - 150, 528), (cx - 110, 370)]), robe, sw=5)
    s.path(poly([(cx - 40, 206), (cx + 40, 206), (cx + 60, 528), (cx - 60, 528)]), s.lin(CRIMSON), sw=4)
    s.path(f"M{cx - 40},{206} L{cx - 60},{528} M{cx + 40},{206} L{cx + 60},{528}", stroke="#d4a640", sw=4)
    for y in range(230, 520, 60):
        s.path(star(cx, y, 9, 4, 4), "#d4a640", sw=1.5)
    # руки вверх, к чаше
    for side in (-1, 1):
        s.path(smooth([(cx + side * 56, 196), (cx + side * 100, 160), (cx + side * 62, 70), (cx + side * 42, 76),
                       (cx + side * 70, 160), (cx + side * 40, 220)]), robe, sw=4)
        s.circle(cx + side * 46, 70, 10, s.lin(PALE), INK, 3)
    # чаша над головой
    cup = s.lin(GOLD, 0, 0, 1, 0)
    s.path(smooth([(cx - 50, 22), (cx + 50, 22), (cx + 38, 62), (cx, 72), (cx - 38, 62)]), cup, sw=4)
    s.path(poly([(cx - 7, 72), (cx + 7, 72), (cx + 5, 88), (cx - 5, 88)]), cup, sw=3)
    s.path(smooth([(cx - 22, 98), (cx, 86), (cx + 22, 98)]), cup, sw=3)
    s.ellipse(cx, 24, 48, 8, "#7a0614", INK, 3)
    for x in (-34, -10, 20, 40):
        s.circle(cx + x * 0.9, 44 + abs(x) * 0.2, 3, "#ff4a5a", INK, 1)
    # голова и митра
    s.path(smooth([(cx - 24, 186), (cx - 22, 162), (cx, 154), (cx + 22, 162), (cx + 24, 186), (cx, 206)]), s.lin(PALE), sw=4)
    s.path(smooth([(cx - 28, 166), (cx - 24, 128), (cx, 104), (cx + 24, 128), (cx + 28, 166)]), s.lin(CRIMSON), sw=4)
    s.path(f"M{cx},{108} L{cx},{164} M{cx - 12},{130} L{cx + 12},{130}", stroke="#d4a640", sw=3)
    s.path(f"M{cx - 28},{166} L{cx + 28},{166}", stroke="#d4a640", sw=4)
    s.close()
    s.eye(cx - 9, 182, 3, BLOOD, "#ffe0e4")
    s.eye(cx + 9, 182, 3, BLOOD, "#ffe0e4")
    # кровь из чаши
    drips = [(-40, 150), (40, 170), (-22, 120), (30, 200)] if p2 else [(-44, 90), (46, 110)]
    for x, l in drips:
        s.path(f"M{cx + x},{30} Q{cx + x * 1.1},{30 + l * 0.6} {cx + x * 1.05},{30 + l}", stroke="#b0081e", sw=5)
        s.circle(cx + x * 1.05, 30 + l + 4, 5, "#b0081e")
    s.ellipse(cx, 18, 40, 10, "#ff3b4e", extra=f'opacity="{0.9 if p2 else 0.5}" filter="{s.glow(6)}"')
    return s


@sprite("chalice_acolyte")
def draw_chalice_acolyte(v=""):
    s = Svg(240, 300)
    cx = 120
    s.shadow(cx, 290, 80, 8)
    s.open(f'filter="{s.grain(0.15, 0.9, 43)}"')
    s.path(smooth([(cx - 40, 90), (cx + 40, 90), (cx + 70, 230), (cx + 84, 288), (cx - 84, 288), (cx - 70, 230)]),
           s.lin(CRIMSON, 0, 0, 1, 1), sw=4)
    s.path(smooth([(cx - 44, 110), (cx - 40, 50), (cx, 26), (cx + 40, 50), (cx + 44, 110), (cx, 96)]), s.lin([(0, "#7a1020"), (1, "#2a0408")]), sw=4)
    s.ellipse(cx, 80, 24, 26, "#0a0204")
    s.path(smooth([(cx - 34, 150), (cx + 34, 150), (cx + 20, 180), (cx - 20, 180)]), s.lin(GOLD, 0, 0, 1, 0), sw=3)
    s.ellipse(cx, 152, 32, 6, "#7a0614", INK, 2)
    for side in (-1, 1):
        s.circle(cx + side * 36, 160, 8, s.lin(PALE), INK, 3)
    s.close()
    s.eye(cx - 8, 78, 2.6, BLOOD, "#ffe0e4")
    s.eye(cx + 8, 78, 2.6, BLOOD, "#ffe0e4")
    return s


# ═════════════════════════ 4б — Звёздная Башня ═════════════════════════

NIGHTSKY = [(0, "#3a4a9a"), (0.6, "#1a2050"), (1, "#0a0c22")]


def _stars(s, pts, r=3.0, color="#ffffff"):
    for x, y in pts:
        s.path(star(x, y, r * 1.8, r * 0.6, 4), color, stroke=None, extra=f'filter="{s.glow(r * 0.6)}"')


@sprite("astrolabe_keeper", ("", "p2"))
def draw_astrolabe_keeper(v=""):
    p2 = v == "p2"
    s = Svg(440, 540)
    cx = 220
    import random
    rnd = random.Random(5)
    # кольца астролябии позади
    s.circle(cx, 170, 200, s.rad([(0, ARCANE, 0.45 if p2 else 0.3), (1, ARCANE, 0)]))
    rings = [(170, 52, -18), (140, 140, 0), (186, 40, 24)] if not p2 else [(190, 70, -34), (150, 150, 0), (200, 56, 40), (120, 30, 80)]
    for rx, ry, rot in rings:
        s.ellipse(cx, 170, rx, ry, "none", INK, 9, extra=f'transform="rotate({rot} {cx} 170)"')
        s.ellipse(cx, 170, rx, ry, "none", "#d4b45a", 4, extra=f'transform="rotate({rot} {cx} 170)"')
    s.shadow(cx, 528, 130, 13)
    s.open(f'filter="{s.grain(0.12, 0.9, 47)}"')
    cloak = s.lin(NIGHTSKY, 0, 0, 1, 1)
    s.path(poly([(cx - 50, 170), (cx + 50, 170), (cx + 100, 340), (cx + 140, 528), (cx - 140, 528), (cx - 100, 340)]), cloak, sw=5)
    _stars(s, [(cx + rnd.randint(-110, 110), rnd.randint(260, 510)) for _ in range(14)], 2.2, "#e8ecff")
    s.path(f"M{cx - 50},{170} L{cx - 90},{528} M{cx + 50},{170} L{cx + 90},{528}", stroke="#d4b45a", sw=3)
    # посох с шаром
    s.path(f"M{cx + 110},{110} L{cx + 130},{520}", stroke=INK, sw=11)
    s.path(f"M{cx + 110},{110} L{cx + 130},{520}", stroke="#6a4a2a", sw=6)
    orb = "#fff1a8" if not p2 else ARCANE_CORE
    s.circle(cx + 108, 96, 18, s.rad([(0, "#ffffff"), (0.5, ARCANE), (1, "#2a2a7a")]), INK, 4)
    s.circle(cx + 108, 96, 26, orb, extra=f'opacity="0.35" filter="{s.blur(8)}"')
    # рукава
    for side in (-1, 1):
        s.path(smooth([(cx + side * 48, 180), (cx + side * 110, 230), (cx + side * 120, 280), (cx + side * 80, 290),
                       (cx + side * 60, 240), (cx + side * 34, 220)]), cloak, sw=4)
    s.circle(cx + 110, 270, 10, "#c8b8d8", INK, 3)
    s.circle(cx - 110, 270, 10, "#c8b8d8", INK, 3)
    # капюшон со звёздной пустотой
    s.path(smooth([(cx - 60, 180), (cx - 56, 110), (cx, 70), (cx + 56, 110), (cx + 60, 180), (cx, 166)]), cloak, sw=5)
    s.ellipse(cx, 136, 32, 38, s.rad([(0, "#2a2a6a"), (1, "#05060f")]), INK, 3)
    s.close()
    _stars(s, [(cx - 12, 130), (cx + 12, 130), (cx, 150), (cx - 6, 116)], 2.4)
    s.eye(cx - 12, 130, 3.5, ARCANE, ARCANE_CORE)
    s.eye(cx + 12, 130, 3.5, ARCANE, ARCANE_CORE)
    if p2:
        for (x, y), col in (((cx - 160, 120), EMBER), ((cx + 170, 240), FROST), ((cx - 130, 300), "#ffe76a")):
            s.circle(x, y, 14, col, extra=f'filter="{s.glow(8)}"')
            s.circle(x, y, 6, "#ffffff")
        s.path(poly([(cx - 160, 120), (cx - 130, 300), (cx + 170, 240)]), stroke=ARCANE_CORE, sw=1.5, extra='opacity="0.6"')
    return s


@sprite("ley_wyrm", ("", "p2"))
def draw_ley_wyrm(v=""):
    p2 = v == "p2"
    s = Svg(520, 500)
    core = "#e6c8ff" if p2 else ARCANE_CORE
    hue = "#c06aff" if p2 else ARCANE
    s.shadow(280, 488, 200, 12, 0.3)
    body = [(500, 450), (420, 470), (330, 450), (290, 390), (330, 330), (410, 300), (430, 230), (370, 180), (290, 190),
            (220, 230), (160, 200), (130, 140), (120, 100)]
    path = smooth(body, closed=False)
    s.path(path, stroke=hue, sw=70, extra=f'opacity="0.35" filter="{s.blur(14)}"')
    s.path(path, stroke=INK, sw=46)
    s.path(path, stroke=s.lin([(0, "#2a2a7a"), (1, "#6a4ac8")], 0, 0, 1, 0), sw=38)
    s.path(path, stroke=hue, sw=14, extra=f'opacity="0.9" filter="{s.glow(4)}"')
    s.path(path, stroke=core, sw=4)
    # руны-кольца вдоль тела
    for i, (x, y) in enumerate(body[1:-2]):
        if p2 and i % 3 == 1:
            s.path(f"M{x - 18},{y - 18} L{x + 18},{y + 18}", stroke=INK, sw=8)
            s.path(f"M{x - 18},{y - 18} L{x + 18},{y + 18}", stroke="#ff9af0", sw=3, extra=f'filter="{s.glow(4)}"')
            continue
        s.circle(x, y, 22, "none", "#d4b45a", 3)
        s.path(f"M{x - 6},{y - 8} L{x + 6},{y} L{x - 6},{y + 8}", stroke=core, sw=2.5)
    # голова
    hx, hy = 112, 96
    head = s.lin([(0, "#9a8af0"), (0.6, "#3a3a9a"), (1, "#14143a")], 0, 0, 0, 1)
    for k, (dx, dy) in enumerate(((40, -70), (70, -56), (92, -30))):  # светящийся гребень
        s.path(poly([(hx + 10 + k * 18, hy - 24), (hx + dx + 30, hy + dy - 20), (hx + 34 + k * 18, hy - 18)]),
               s.lin([(0, core), (1, hue, 0.5)]), sw=3, extra=f'filter="{s.glow(3)}"')
    s.path(smooth([(hx + 56, hy - 28), (hx + 10, hy - 48), (hx - 50, hy - 34), (hx - 96, hy - 6), (hx - 60, hy + 10),
                   (hx + 10, hy + 6), (hx + 60, hy + 4)]), head, sw=5)
    jaw = 26 if p2 else 16
    s.path(poly([(hx + 56, hy + 6), (hx - 40, hy + 12), (hx - 90, hy + 8 + jaw), (hx - 30, hy + 30 + jaw * 0.4), (hx + 50, hy + 30)]), head, sw=5)
    s.ellipse(hx - 50, hy + 16 + jaw * 0.3, 34, 6 + jaw * 0.3, core, extra=f'filter="{s.glow(6)}"')
    for x in range(int(hx - 84), int(hx + 20), 13):
        s.path(f"M{x},{hy + 8} l4,9 l4,-9", "#f4f0ff", sw=1.5)
    s.eye(hx - 22, hy - 18, 6, hue, core)
    _stars(s, [(60, 260), (470, 120), (250, 330), (200, 440), (480, 330)], 2.6, core)
    return s


def _crystal_mage(s, cx, base, opacity=1.0, cracked=False, tint=ARCANE):
    glass = s.lin([(0, "#f4f6ff", 0.95 * opacity), (0.5, "#a8b8ff", 0.75 * opacity), (1, "#3a3a8a", 0.85 * opacity)], 0, 0, 1, 1)
    facet = s.lin([(0, "#ffffff", 0.7 * opacity), (1, "#c8d0ff", 0.1 * opacity)])
    op = f'opacity="{opacity}"'
    s.open(op)
    robe = [(cx - 50, base - 360), (cx + 50, base - 360), (cx + 90, base - 200), (cx + 130, base), (cx - 130, base), (cx - 90, base - 200)]
    s.path(poly(robe), glass, sw=5)
    for a, b in (((cx - 50, base - 360), (cx - 20, base)), ((cx + 50, base - 360), (cx + 40, base)), ((cx - 90, base - 200), (cx + 90, base - 200)),
                 ((cx, base - 360), (cx - 70, base - 100))):
        s.path(f"M{a[0]},{a[1]} L{b[0]},{b[1]}", stroke="#5a5aa8", sw=2)
    s.path(poly([(cx - 50, base - 360), (cx, base - 300), (cx - 90, base - 200)]), facet, stroke=None)
    for side in (-1, 1):
        s.path(poly([(cx + side * 46, base - 350), (cx + side * 120, base - 290), (cx + side * 130, base - 250), (cx + side * 60, base - 280)]),
               glass, sw=4)
    # капюшон-кристалл
    s.path(poly([(cx - 50, base - 360), (cx - 44, base - 430), (cx, base - 480), (cx + 44, base - 430), (cx + 50, base - 360), (cx, base - 380)]),
           glass, sw=5)
    s.path(poly([(cx - 28, base - 400), (cx, base - 440), (cx + 28, base - 400), (cx, base - 372)]), "#0a0c2a", sw=3)
    if cracked:
        for d in ("M{0},{1} l20,40 l-12,30 l26,40", "M{2},{3} l-24,30 l10,26", "M{4},{5} l18,-30 l-8,-24"):
            dd = d.format(cx - 30, base - 330, cx + 40, base - 250, cx - 60, base - 80)
            s.path(dd, stroke=INK, sw=4)
            s.path(dd, stroke="#ffffff", sw=1.5)
    s.close()


@sprite("archmage_simulacrum", ("", "p2"))
def draw_archmage_simulacrum(v=""):
    p2 = v == "p2"
    s = Svg(440, 540)
    cx = 220
    s.circle(cx, 200, 190, s.rad([(0, ARCANE, 0.35), (1, ARCANE, 0)]))
    s.shadow(cx, 528, 130, 12, 0.35)
    if p2:
        _crystal_mage(s, cx - 60, 524, 0.35, tint="#ff9af0")
        _crystal_mage(s, cx + 60, 524, 0.35, tint=FROST)
    _crystal_mage(s, cx, 528, 1.0, cracked=p2)
    s.eye(cx - 10, 124, 3.5, ARCANE, ARCANE_CORE)
    s.eye(cx + 10, 124, 3.5, ARCANE, ARCANE_CORE)
    # парящие кристаллы у рук
    for x, y in ((cx - 140, 270), (cx + 140, 270)):
        s.path(poly([(x, y - 30), (x + 14, y), (x, y + 30), (x - 14, y)]), s.lin([(0, "#ffffff"), (1, ARCANE)]), sw=3,
               extra=f'filter="{s.glow(4)}"')
    if p2:
        import random
        rnd = random.Random(9)
        for _ in range(9):
            x, y = rnd.randint(40, 400), rnd.randint(60, 480)
            sz = rnd.randint(6, 14)
            s.path(poly([(x, y - sz), (x + sz * 0.6, y), (x, y + sz), (x - sz * 0.6, y)]), "#e8ecff", sw=2)
    return s


@sprite("mirror_shard")
def draw_mirror_shard(v=""):
    s = Svg(220, 280)
    cx = 110
    s.shadow(cx, 270, 50, 7, 0.3)
    s.circle(cx, 130, 90, s.rad([(0, ARCANE, 0.4), (1, ARCANE, 0)]))
    s.path(poly([(cx, 20), (cx + 50, 110), (cx + 30, 220), (cx, 250), (cx - 34, 210), (cx - 54, 100)]),
           s.lin([(0, "#ffffff", 0.95), (0.5, "#b8c4ff", 0.8), (1, "#3a3a8a", 0.9)], 0, 0, 1, 1), sw=5)
    s.path(f"M{cx},{20} L{cx - 6},{250} M{cx - 54},{100} L{cx + 30},{220}", stroke="#5a5aa8", sw=2)
    s.path(poly([(cx, 20), (cx - 54, 100), (cx - 6, 140)]), "#ffffff", stroke=None, extra='opacity="0.5"')
    s.ellipse(cx - 2, 130, 22, 14, "#0a0c2a", INK, 3)
    s.eye(cx - 2, 130, 7, ARCANE, ARCANE_CORE)
    return s


# ═════════════════════════ Лавки — Стража (M5 п. 4, 14 §1) ═════════════════════════

SHOP_GOLD = "#ffd36a"


@sprite("shop_guard")
def draw_shop_guard(v=""):
    """Стража лавки: городской стражник в бригантине и шапеле, алебарда справа, геральдический щит к героям."""
    s = Svg(280, 340)
    cx = 140
    s.shadow(cx, 328, 86, 10)
    s.open(f'filter="{s.grain(0.18, 0.9, 17)}"')
    # алебарда: древко за спиной справа
    s.path("M200,30 L212,316", stroke=INK, sw=12)
    s.path("M200,30 L212,316", stroke="#6b4a30", sw=7)
    s.path(poly([(196, 10), (204, 2), (210, 30), (200, 34)]), s.lin([(0, "#eef3f6"), (1, "#7d8b96")]), sw=3)
    s.path(smooth([(200, 40), (250, 30), (262, 70), (248, 104), (206, 94)]), s.lin([(0, "#e6edf2"), (1, "#66737e")]), sw=4)
    s.path(poly([(198, 52), (178, 62), (198, 74)]), s.lin([(0, "#d0d9df"), (1, "#5a6670")]), sw=3)
    # ноги в шоссах и сапогах
    for side in (-1, 1):
        lx = cx + side * 24
        s.path(poly([(lx - 15, 220), (lx + 15, 220), (lx + 13, 290), (lx - 13, 290)]), s.lin([(0, "#5d3a3a"), (1, "#2a1818")]), sw=4)
        s.path(poly([(lx - 15, 286), (lx + 15, 286), (lx + 22, 324), (lx - 18, 324)]), s.lin([(0, "#4a3626"), (1, "#1e140c")]), sw=4)
    # табард лавки (бордо с золотой каймой) поверх бригантины
    s.path(smooth([(cx - 52, 112), (cx + 52, 112), (cx + 60, 206), (cx + 46, 246), (cx - 46, 246), (cx - 60, 206)]),
           s.lin([(0, "#8b929a"), (1, "#353b42")]), sw=4)
    s.path(poly([(cx - 34, 116), (cx + 34, 116), (cx + 30, 252), (cx, 264), (cx - 30, 252)]),
           s.lin([(0, "#9c2a35"), (1, "#4a0f16")]), sw=4)
    s.path(poly([(cx - 34, 116), (cx + 34, 116), (cx + 30, 252), (cx, 264), (cx - 30, 252)]), "none", SHOP_GOLD, 2.5)
    # знак лавки — монета с весами
    s.circle(cx, 168, 17, s.rad([(0, "#fff0b0"), (1, "#b07a1e")]), INK, 3)
    s.path(f"M{cx},{156} L{cx},{180} M{cx - 10},{162} L{cx + 10},{162}", stroke="#5a3a10", sw=2.5)
    s.path(f"M{cx - 12},{170} Q{cx - 10},{176} {cx - 6},{170} M{cx + 6},{170} Q{cx + 10},{176} {cx + 12},{170}", stroke="#5a3a10", sw=2)
    # пояс
    s.path(poly([(cx - 56, 206), (cx + 56, 206), (cx + 56, 218), (cx - 56, 218)]), "#2e2016", sw=3)
    s.path(poly([(cx - 8, 204), (cx + 8, 204), (cx + 8, 220), (cx - 8, 220)]), SHOP_GOLD, sw=2)
    # голова: лицо под шапелем, усы
    s.path(smooth([(cx - 22, 82), (cx, 72), (cx + 22, 82), (cx + 20, 108), (cx, 118), (cx - 20, 108)]),
           s.lin([(0, "#f0c9a2"), (1, "#a6714c")]), sw=4)
    s.path(f"M{cx - 14},{104} Q{cx},{98} {cx + 14},{104}", stroke="#3a2414", sw=5)
    s.ellipse(cx - 9, 92, 3.5, 3, "#1a0e08")
    s.ellipse(cx + 9, 92, 3.5, 3, "#1a0e08")
    s.path(f"M{cx - 15},{86} L{cx - 4},{88} M{cx + 4},{88} L{cx + 15},{86}", stroke="#3a2414", sw=3)
    # шапель с широкими полями
    s.path(smooth([(cx - 26, 80), (cx - 20, 50), (cx, 40), (cx + 20, 50), (cx + 26, 80)], closed=False) + " Z",
           s.lin([(0, "#c3ccd3"), (1, "#56626c")]), sw=4)
    s.path(smooth([(cx - 46, 82), (cx, 74), (cx + 46, 82), (cx, 90)]), s.lin([(0, "#aeb8c0"), (1, "#46515a")]), sw=4)
    s.path(f"M{cx},{42} L{cx},{76}", stroke="#56626c", sw=3)
    # рука с алебардой
    s.path(smooth([(cx + 46, 120), (cx + 70, 150), (cx + 66, 178), (cx + 52, 176), (cx + 50, 150)]), s.lin([(0, "#8b929a"), (1, "#353b42")]), sw=4)
    s.circle(cx + 66, 172, 11, "#f0c9a2", INK, 3)
    # геральдический щит к героям (слева)
    shield = [(cx - 104, 128), (cx - 34, 128), (cx - 36, 196), (cx - 69, 238), (cx - 102, 196)]
    s.path(poly(shield), s.lin([(0, "#b23240"), (1, "#56121b")]), sw=5)
    s.path(poly([(cx - 69, 128), (cx - 34, 128), (cx - 36, 196), (cx - 69, 238)]), s.lin([(0, "#f2e6c8"), (1, "#a8946a")]), stroke=None)
    s.path(poly(shield), "none", SHOP_GOLD, 3.5)
    s.circle(cx - 69, 172, 12, s.rad([(0, "#fff0b0"), (1, "#b07a1e")]), INK, 3)
    s.close()
    return s


def main(argv) -> int:
    os.makedirs(OUT, exist_ok=True)
    keys = argv[1:] or sorted(REGISTRY)
    n = 0
    for key in keys:
        fn, variants = REGISTRY[key]
        for v in variants:
            name = key + (f"_{v}" if v else "")
            with open(os.path.join(OUT, name + ".svg"), "w", encoding="utf-8") as f:
                f.write(fn(v).render())
            n += 1
    print(f"SVG: {n} → {os.path.relpath(OUT, ROOT)}")
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv))
