#!/usr/bin/env python3
"""Картины-сцены мода: SVG-генератор → art/scenes/*.svg → JPG в mod/SlayTheRequiem/images/ui/doors/.

M5 п. 2 — экран «две двери» (docs/design/13 §2, art spec §4.10): за каменной аркой виден мир варианта акта.
Стиль — как у спрайтов боссов (tools/art/monsters): толстый тёмный контур, градиенты, контровой свет цвета акта, зерно.
Случайность только от фиксированных зёрен, поэтому SVG воспроизводимы (CI сверяет art/scenes с генератором).

    python3 tools/art/scenes/build_scenes.py            # только SVG
    python3 tools/art/scenes/build_scenes.py --render   # + JPG (нужен resvg: npm i --prefix /tmp/r @resvg/resvg-js@2)
    python3 tools/art/scenes/build_scenes.py --check    # CI: SVG совпадают с генератором, JPG на месте
"""
from __future__ import annotations

import math
import os
import random
import subprocess
import sys
import tempfile

sys.path.insert(0, os.path.join(os.path.dirname(__file__), "..", "monsters"))
from svgkit import INK, Svg, poly, smooth, star  # noqa: E402

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", "..", ".."))
OUT_SVG = os.path.join(ROOT, "art", "scenes")
OUT_JPG = os.path.join(ROOT, "mod", "SlayTheRequiem", "images", "ui", "doors")

W, H = 600, 860
# Проём арки: полуциркуль над прямыми косяками.
AX0, AX1, SPRING = 96, 504, 330
R = (AX1 - AX0) / 2
CX = (AX0 + AX1) / 2
FRAME = 58

REGISTRY: dict[str, callable] = {}


def scene(key):
    def wrap(fn):
        REGISTRY[key] = fn
        return fn
    return wrap


def arch_path(x0, x1, spring, bottom=H, pad=0.0) -> str:
    r = (x1 - x0) / 2 + pad
    cx = (x0 + x1) / 2
    return (f"M{cx - r:.1f},{bottom} L{cx - r:.1f},{spring} A{r:.1f},{r:.1f} 0 0 1 {cx + r:.1f},{spring} "
            f"L{cx + r:.1f},{bottom} Z")


def begin(s: Svg, sky_stops):
    """Клип проёма и небо; возвращает id клипа."""
    cid = s.uid("clip")
    s.defs.append(f'<clipPath id="{cid}"><path d="{arch_path(AX0, AX1, SPRING)}"/></clipPath>')
    s.open(f'clip-path="url(#{cid})"')
    s.path(f"M0,0 H{W} V{H} H0 Z", s.lin(sky_stops), stroke=None)
    return cid


def ridge(rnd: random.Random, y, amp, step=30, x0=AX0 - 20, x1=AX1 + 20, jag=1.0):
    """Ломаная линия горизонта (горы, руины)."""
    pts = []
    x = x0
    while x <= x1:
        pts.append((x, y - rnd.uniform(0, amp) * jag))
        x += step * rnd.uniform(0.6, 1.3)
    pts.append((x1, y - rnd.uniform(0, amp) * jag))
    return pts


def fill_below(pts, bottom=H) -> str:
    return poly(pts + [(pts[-1][0], bottom), (pts[0][0], bottom)])


def fog(s: Svg, y, color, opacity=0.45, h=40, sd=14):
    s.ellipse(CX, y, R * 1.6, h, color, extra=f'opacity="{opacity}" filter="{s.blur(sd)}"')


def particles(s: Svg, rnd: random.Random, n, color, rmin=1.0, rmax=2.6, y0=0, y1=H, opacity=(0.4, 0.95), glow=0.0):
    f = f' filter="{s.glow(glow)}"' if glow else ""
    for _ in range(n):
        s.circle(round(rnd.uniform(AX0, AX1), 1), round(rnd.uniform(y0, y1), 1), round(rnd.uniform(rmin, rmax), 2), color,
                 extra=f'opacity="{rnd.uniform(*opacity):.2f}"{f}')


def frame(s: Svg, stone, mortar, rim, seed):
    """Каменная арка: стена вокруг, клинчатые камни свода, косяки блоками, контровой свет цвета акта по кромке."""
    rnd = random.Random(seed)
    s.close()  # конец клипа сцены
    outer = arch_path(AX0, AX1, SPRING, pad=FRAME)
    inner = arch_path(AX0, AX1, SPRING)
    # стена (всё вне арки)
    s.path(f"M0,0 H{W} V{H} H0 Z {outer}", s.lin([(0, "#1a161c"), (1, "#0b090c")]), stroke=None, extra='fill-rule="evenodd"')
    wid = s.uid("wall")
    s.defs.append(f'<clipPath id="{wid}"><path d="M0,0 H{W} V{H} H0 Z {outer}" clip-rule="evenodd"/></clipPath>')
    s.open(f'clip-path="url(#{wid})"')
    for y in range(0, H, 46):
        off = 0 if (y // 46) % 2 == 0 else 40
        for x in range(-40 + off, W, 80):
            s.path(f"M{x},{y} h78 v44 h-78 Z", "none", stroke="#000", sw=2, extra='opacity="0.35"')
    s.close()
    # свод: клинчатые камни
    stones = 13
    r_in, r_out = R, R + FRAME
    for i in range(stones):
        a0 = math.pi + math.pi * i / stones
        a1 = math.pi + math.pi * (i + 1) / stones
        pts = [(CX + r_in * math.cos(a0), SPRING + r_in * math.sin(a0)),
               (CX + r_out * math.cos(a0), SPRING + r_out * math.sin(a0)),
               (CX + r_out * math.cos(a1), SPRING + r_out * math.sin(a1)),
               (CX + r_in * math.cos(a1), SPRING + r_in * math.sin(a1))]
        shade = rnd.uniform(-0.08, 0.08)
        key = i == stones // 2
        s.path(poly(pts), s.lin([(0, _tint(stone[0], shade + (0.08 if key else 0))), (1, _tint(stone[1], shade))], 0, 0, 1, 1),
               stroke=mortar, sw=4)
    # косяки
    for side in (-1, 1):
        x_in = AX0 if side < 0 else AX1
        x_out = x_in + side * FRAME
        y = SPRING
        k = 0
        while y < H:
            h = rnd.randint(58, 84)
            ext = 10 if k % 2 == 0 else 0
            xa, xb = sorted((x_in, x_out + side * ext))
            shade = rnd.uniform(-0.08, 0.08)
            s.path(f"M{xa},{y} H{xb} V{y + h} H{xa} Z", s.lin([(0, _tint(stone[0], shade)), (1, _tint(stone[1], shade))], 0, 0, 1, 1),
                   stroke=mortar, sw=4)
            y += h
            k += 1
    # контровой свет по кромке проёма + общий контур
    s.path(inner, "none", stroke=rim, sw=5, extra=f'opacity="0.75" filter="{s.blur(3)}"')
    s.path(inner, "none", stroke=INK, sw=6)
    s.path(outer, "none", stroke=INK, sw=6)


def finish(s: Svg, tint, seed):
    """Виньетка, тон и зерно по всему кадру."""
    s.path(f"M0,0 H{W} V{H} H0 Z", s.rad([(0.55, "#000", 0), (1, "#000", 0.6)], cx=0.5, cy=0.45, r=0.75), stroke=None)
    s.path(f"M0,0 H{W} V{H} H0 Z", tint, stroke=None, extra='opacity="0.06"')
    s.path(f"M0,0 H{W} V{H} H0 Z", "#808080", stroke=None, extra=f'opacity="0.5" filter="{s.grain(0.35, 0.85, seed)}" '
                                                                 'style="mix-blend-mode:overlay"')


def _tint(hex_color: str, k: float) -> str:
    c = hex_color.lstrip("#")
    rgb = [int(c[i:i + 2], 16) for i in (0, 2, 4)]
    if k >= 0:
        rgb = [round(v + (255 - v) * k) for v in rgb]
    else:
        rgb = [round(v * (1 + k)) for v in rgb]
    return "#" + "".join(f"{max(0, min(255, v)):02x}" for v in rgb)


# ═══════════════════════════ 2а — Ледяные Курганы ═══════════════════════════

@scene("frozen_barrows")
def draw_frozen_barrows():
    s = Svg(W, H)
    rnd = random.Random(21)
    begin(s, [(0, "#05101c"), (0.45, "#14304a"), (0.75, "#3d6a86"), (1, "#9db8cc")])
    # сияние
    for i, (col, y, a) in enumerate((("#5df2c6", 210, 0.55), ("#62b6ff", 250, 0.45), ("#a98bff", 180, 0.25))):
        pts = [(AX0 - 40, y + 40), (CX - 120, y - 30 + i * 10), (CX, y + 20), (CX + 110, y - 50), (AX1 + 40, y + 10)]
        s.path(smooth(pts, closed=False), stroke=col, sw=46 - i * 8, extra=f'opacity="{a}" filter="{s.blur(16)}"')
        s.path(smooth(pts, closed=False), stroke="#e8fff8", sw=3, extra=f'opacity="{a * 0.6}" filter="{s.blur(2)}"')
    # звёзды и луна
    particles(s, rnd, 70, "#ffffff", 0.6, 1.6, 120, 330)
    s.circle(400, 190, 42, s.rad([(0, "#ffffff"), (0.7, "#dff2ff"), (1, "#9cc8e8")]), extra=f'filter="{s.glow(10)}"')
    s.circle(386, 180, 8, "#c8e0f0", extra='opacity="0.6"')
    s.circle(410, 204, 5, "#c8e0f0", extra='opacity="0.5"')
    # горы: три слоя
    for y, amp, col, j in ((470, 170, "#2c4a64", 1.0), (530, 110, "#1d3348", 0.9), (590, 70, "#13243a", 0.8)):
        pts = ridge(rnd, y, amp, 34, jag=j)
        s.path(fill_below(pts), col, stroke=None)
        caps = [(x, yy) for x, yy in pts if yy < y - amp * 0.55]
        for x, yy in caps:  # снежные шапки
            s.path(poly([(x - 18, yy + 22), (x, yy), (x + 16, yy + 20), (x + 4, yy + 14)]), "#cfe6f5", stroke=None, extra='opacity="0.85"')
    fog(s, 600, "#bcd8ea", 0.35, 50)
    # курганы со стоячими камнями
    for x, y, rx, ry in ((150, 690, 150, 70), (470, 700, 170, 80)):
        s.ellipse(x, y, rx, ry, s.lin([(0, "#3c5a70"), (1, "#0f1c28")]), INK, 4)
        s.path(f"M{x - rx * 0.8},{y - ry * 0.35} Q{x},{y - ry * 1.15} {x + rx * 0.8},{y - ry * 0.35}", "none", stroke="#e3f2fb", sw=10,
               extra='opacity="0.8"')
    for x, h, lean in ((112, 110, -4), (190, 80, 3), (430, 120, 2), (520, 90, -3)):
        base = 660
        s.path(poly([(x - 16, base), (x - 12 + lean, base - h), (x + 12 + lean, base - h - 6), (x + 16, base)]),
               s.lin([(0, "#7a8c9a"), (1, "#2a3640")], 0, 0, 1, 0), INK, 4)
        for k in range(3):  # руны-насечки
            yy = base - h * 0.3 - k * 22
            s.path(f"M{x - 5 + lean * 0.5},{yy} l5,-9 l5,9 M{x + lean * 0.5},{yy - 9} v-8", "none", stroke="#7ff2ff", sw=2.5,
                   extra=f'filter="{s.glow(3)}"')
    # вход в курган
    s.path(f"M{CX - 60},700 L{CX - 60},600 Q{CX},540 {CX + 60},600 L{CX + 60},700 Z", s.lin([(0, "#5a6e7e"), (1, "#1a2632")]), INK, 5)
    s.path(f"M{CX - 38},700 L{CX - 38},612 Q{CX},572 {CX + 38},612 L{CX + 38},700 Z", s.rad([(0, "#bff6ff"), (0.4, "#3fa9d8"), (1, "#081a2a")],
                                                                                           cy=0.7), INK, 4)
    s.ellipse(CX, 660, 70, 60, "#7fe0ff", extra=f'opacity="0.35" filter="{s.blur(18)}"')
    # тропа
    s.path(poly([(CX - 30, 700), (CX + 30, 700), (CX + 150, H), (CX - 150, H)]), s.lin([(0, "#b7d0e0"), (1, "#eaf6ff")]), stroke=None)
    for i in range(6):
        y = 720 + i * 24
        s.path(f"M{CX - 30 - i * 18},{y} Q{CX},{y + 6} {CX + 30 + i * 18},{y}", "none", stroke="#8fb0c4", sw=2, extra='opacity="0.6"')
    fog(s, 720, "#e6f6ff", 0.4, 30, 10)
    particles(s, rnd, 120, "#ffffff", 1.0, 3.2, 120, H, (0.5, 0.95))
    frame(s, ("#7d8a96", "#3a4552"), "#1b2229", "#9be4ff", 210)
    # сосульки на своде
    for i in range(15):
        a = math.pi + math.pi * (i + 0.5) / 15
        x = CX + R * math.cos(a)
        y = SPRING + R * math.sin(a)
        ln = rnd.randint(14, 40)
        s.path(poly([(x - 6, y - 2), (x, y + ln), (x + 6, y - 2)]), s.lin([(0, "#ffffff"), (1, "#8fd8ff")]), stroke=INK, sw=2)
    for side in (-1, 1):  # иней на косяках
        x = AX0 if side < 0 else AX1
        s.path(f"M{x},{SPRING} v{H - SPRING}", "none", stroke="#e8f8ff", sw=7, extra=f'opacity="0.35" filter="{s.blur(4)}"')
    finish(s, "#7fc8ff", 11)
    return s


# ═══════════════════════════ 2б — Заводные Глубины ═══════════════════════════

def _gear(s: Svg, cx, cy, r, teeth, fill, stroke=INK, sw=4, hole="#120a04", rot=0.0):
    pts = []
    for i in range(teeth * 2):
        a = math.radians(rot) + i * math.pi / teeth
        rr = r if i % 2 == 0 else r * 0.84
        pts += [(cx + rr * math.cos(a - 0.11), cy + rr * math.sin(a - 0.11)), (cx + rr * math.cos(a + 0.11), cy + rr * math.sin(a + 0.11))]
    s.path(poly(pts), fill, stroke=stroke, sw=sw)
    s.circle(cx, cy, r * 0.62, "none", stroke=stroke, sw=sw * 0.6)
    s.circle(cx, cy, r * 0.22, hole, stroke=stroke, sw=sw * 0.6)


@scene("clockwork_depths")
def draw_clockwork_depths():
    s = Svg(W, H)
    rnd = random.Random(33)
    begin(s, [(0, "#0d0704"), (0.5, "#2a170b"), (0.8, "#5a3416"), (1, "#8a5a2a")])
    # огромные шестерни в дымке
    for cx, cy, r, t, op in ((170, 250, 150, 18, 0.35), (450, 330, 120, 14, 0.3), (300, 120, 90, 12, 0.25)):
        s.open(f'opacity="{op}"')
        _gear(s, cx, cy, r, t, "#6a4220", stroke="#1a0e05", sw=6, hole="#1a0e05", rot=rnd.uniform(0, 30))
        s.close()
    fog(s, 300, "#ffcf8a", 0.18, 120, 30)
    # трубы
    for y, sw_, col in ((380, 30, "#8a5a2a"), (420, 18, "#b07a3a")):
        s.path(f"M{AX0 - 20},{y} H{AX1 + 20}", "none", stroke=INK, sw=sw_ + 8)
        s.path(f"M{AX0 - 20},{y} H{AX1 + 20}", "none", stroke=col, sw=sw_)
        s.path(f"M{AX0 - 20},{y - sw_ * 0.25} H{AX1 + 20}", "none", stroke="#ffe0a8", sw=3, extra='opacity="0.55"')
        for x in range(AX0, AX1 + 1, 70):
            s.path(f"M{x},{y - sw_ / 2 - 4} v{sw_ + 8}", "none", stroke=INK, sw=8)
            s.path(f"M{x},{y - sw_ / 2 - 4} v{sw_ + 8}", "none", stroke="#d8a04e", sw=4)
    s.path(f"M{AX1 - 60},140 V380", "none", stroke=INK, sw=30)
    s.path(f"M{AX1 - 60},140 V380", "none", stroke="#7a4a20", sw=22)
    # дальняя стена с топкой-воротами
    s.path(f"M{AX0 - 20},450 H{AX1 + 20} V{H} H{AX0 - 20} Z", s.lin([(0, "#3a2412"), (1, "#140a04")]), stroke=None)
    for y in range(460, 700, 34):
        s.path(f"M{AX0 - 20},{y} H{AX1 + 20}", "none", stroke="#0a0502", sw=2, extra='opacity="0.6"')
    s.path(f"M{CX - 90},700 V560 Q{CX},470 {CX + 90},560 V700 Z", s.lin([(0, "#c88a3e"), (1, "#5a3414")], 0, 0, 1, 1), INK, 6)
    s.path(f"M{CX - 64},700 V570 Q{CX},506 {CX + 64},570 V700 Z", s.rad([(0, "#fff4c0"), (0.35, "#ff9a2e"), (0.8, "#8a2a08"), (1, "#2a0a02")],
                                                                         cy=0.75), INK, 5)
    for x in range(-48, 56, 16):
        s.path(f"M{CX + x},{560 if abs(x) < 40 else 590} V700", "none", stroke="#1a0a02", sw=6)
    s.ellipse(CX, 640, 130, 90, "#ff9a3a", extra=f'opacity="0.35" filter="{s.blur(26)}"')
    _gear(s, CX, 500, 34, 10, s.lin([(0, "#ffe2a0"), (1, "#8a5a24")]), rot=8)
    # фонари
    for x in (AX0 + 40, AX1 - 40):
        s.path(f"M{x},470 v40", "none", stroke=INK, sw=4)
        s.circle(x, 524, 14, "#ffd27a", INK, 3, extra=f'filter="{s.glow(8)}"')
    # мостки к воротам
    s.path(poly([(CX - 70, 700), (CX + 70, 700), (CX + 190, H), (CX - 190, H)]), s.lin([(0, "#6a4a2a"), (1, "#2a1a0c")]), INK, 4)
    for i in range(7):
        y = 712 + i * 22
        w = 70 + (y - 700) * 0.75
        s.path(f"M{CX - w},{y} H{CX + w}", "none", stroke="#1a0e05", sw=3)
    for side in (-1, 1):
        s.path(f"M{CX + side * 70},700 L{CX + side * 190},{H}", "none", stroke="#c8903e", sw=6)
        for k in range(4):
            t = 0.15 + k * 0.25
            x = CX + side * (70 + 120 * t)
            y = 700 + (H - 700) * t
            s.path(f"M{x},{y} v-40", "none", stroke=INK, sw=6)
            s.path(f"M{x},{y} v-40", "none", stroke="#b07a3a", sw=3)
        s.path(f"M{CX + side * 70},660 L{CX + side * 190},{H - 40}", "none", stroke="#b07a3a", sw=4)
    # пар
    for x, y, k in ((AX0 + 50, 420, 1.2), (AX1 - 60, 360, 1.0), (CX + 40, 470, 0.9), (AX0 + 120, 700, 1.4)):
        for i, (dx, dy, r) in enumerate(((0, 0, 26), (20, -30, 32), (-8, -64, 38), (22, -100, 44))):
            s.circle(x + dx * k, y + dy * k, r * k, "#f6ece0", extra=f'opacity="{0.5 - i * 0.1:.2f}" filter="{s.blur(10 * k)}"')
    particles(s, rnd, 40, "#ffb44a", 1.0, 2.4, 420, H, (0.5, 1.0), glow=1.5)
    frame(s, ("#8a7a66", "#3e3428"), "#1c160f", "#ffb04a", 330)
    # латунная окантовка и шестерня-замковый камень
    s.path(arch_path(AX0, AX1, SPRING, pad=10), "none", stroke=INK, sw=14)
    s.path(arch_path(AX0, AX1, SPRING, pad=10), "none", stroke="#c8903e", sw=8)
    for i in range(1, 12):
        a = math.pi + math.pi * i / 12
        s.circle(CX + (R + 10) * math.cos(a), SPRING + (R + 10) * math.sin(a), 4, "#ffe0a0", INK, 2)
    _gear(s, CX, SPRING - R - 28, 34, 12, s.lin([(0, "#ffe2a0"), (0.6, "#c8903e"), (1, "#6a4218")]))
    finish(s, "#ff9a3a", 13)
    return s


# ═══════════════════════════ 4а — Кровавый Двор ═══════════════════════════

@scene("blood_court")
def draw_blood_court():
    s = Svg(W, H)
    rnd = random.Random(44)
    begin(s, [(0, "#0c0208"), (0.45, "#3a0812"), (0.75, "#8a1e22"), (1, "#e0703a")])
    particles(s, rnd, 40, "#ffd8d0", 0.6, 1.4, 120, 300)
    # кровавая луна
    s.circle(CX + 70, 230, 120, "#ff3a3a", extra=f'opacity="0.25" filter="{s.blur(30)}"')
    s.circle(CX + 70, 230, 70, s.rad([(0, "#ffd0b0"), (0.6, "#e04030"), (1, "#7a0a10")]), extra=f'filter="{s.glow(6)}"')
    s.circle(CX + 50, 215, 12, "#b02020", extra='opacity="0.5"')
    s.circle(CX + 92, 250, 8, "#b02020", extra='opacity="0.45"')
    # летучие мыши
    for x, y, k in ((200, 200, 1.0), (240, 170, 0.7), (420, 320, 0.8), (170, 280, 0.6)):
        s.path(f"M{x},{y} q{-10 * k},{-12 * k} {-24 * k},{-4 * k} q{6 * k},{4 * k} {4 * k},{10 * k} q{-6 * k},{-2 * k} {-10 * k},{4 * k} "
               f"q{14 * k},{-2 * k} {30 * k},{-10 * k} q{16 * k},{8 * k} {30 * k},{10 * k} q{-4 * k},{-6 * k} {-10 * k},{-4 * k} "
               f"q{-2 * k},{-6 * k} {4 * k},{-10 * k} q{-14 * k},{-8 * k} {-24 * k},{4 * k} Z", "#0a0204", stroke=None)
    # дальние холмы
    s.path(fill_below(ridge(rnd, 560, 50, 40)), "#2a0810", stroke=None)
    fog(s, 560, "#ff8a6a", 0.25, 40)
    # замок
    castle = "#14040a"
    s.path(f"M{CX - 170},620 V470 H{CX - 140} V440 H{CX - 110} V470 H{CX - 70} V400 H{CX + 70} V470 H{CX + 110} V430 H{CX + 150} V470 H{CX + 180} V620 Z",
           castle, INK, 4)
    for x, top, w in ((CX - 150, 300, 30), (CX - 30, 240, 60), (CX + 130, 320, 34)):
        s.path(f"M{x - w / 2},470 V{top + 40} H{x + w / 2} V470 Z", castle, INK, 4)
        s.path(poly([(x - w / 2 - 8, top + 42), (x, top - 20), (x + w / 2 + 8, top + 42)]), "#1e0610", INK, 4)
    # окна и витраж-роза
    for x, y in ((CX - 150, 360), (CX - 150, 400), (CX + 130, 380), (CX - 110, 510), (CX + 120, 520), (CX - 60, 540), (CX + 60, 540)):
        s.path(f"M{x - 5},{y + 12} V{y} Q{x},{y - 8} {x + 5},{y} V{y + 12} Z", "#ffb84a", stroke=None, extra=f'filter="{s.glow(3)}"')
    rx, ry = CX - 30 + 30, 340
    s.circle(rx, ry, 26, s.rad([(0, "#ffe080"), (0.5, "#e0402a"), (1, "#5a0a20")]), INK, 3, extra=f'filter="{s.glow(4)}"')
    for k in range(8):
        a = k * math.pi / 4
        s.path(f"M{rx},{ry} L{rx + 26 * math.cos(a):.1f},{ry + 26 * math.sin(a):.1f}", "none", stroke=INK, sw=2.5)
    s.circle(rx, ry, 8, "#ffe8a0", INK, 2)
    s.path(f"M{CX - 26},620 V580 Q{CX},556 {CX + 26},580 V620 Z", "#ffcf6a", INK, 3, extra=f'filter="{s.glow(5)}"')
    fog(s, 630, "#ff9a7a", 0.3, 36, 12)
    # дорога с фонарями
    s.path(poly([(CX - 26, 620), (CX + 26, 620), (CX + 170, H), (CX - 170, H)]), s.lin([(0, "#4a1a1c"), (1, "#1a0608")]), stroke=None)
    for i in range(8):
        y = 640 + i * 28
        w = 26 + (y - 620) * 0.6
        s.path(f"M{CX - w},{y} Q{CX},{y + 4} {CX + w},{y}", "none", stroke="#0a0204", sw=2, extra='opacity="0.7"')
    for side in (-1, 1):
        for t in (0.25, 0.65):
            x = CX + side * (26 + 150 * t) + side * 30
            y = 620 + (H - 620) * t
            h = 70 + 80 * t
            s.path(f"M{x},{y} V{y - h}", "none", stroke=INK, sw=6 + 2 * t)
            s.path(f"M{x},{y} V{y - h}", "none", stroke="#2a1416", sw=3)
            s.circle(x, y - h - 8, 8 + 6 * t, "#ffb84a", INK, 2, extra=f'filter="{s.glow(6)}"')
    # лепестки роз
    for _ in range(26):
        x, y = rnd.uniform(AX0, AX1), rnd.uniform(380, H)
        s.path(f"M{x:.1f},{y:.1f} q4,-7 8,0 q-4,5 -8,0", "#d0182e", stroke=None, extra=f'opacity="{rnd.uniform(0.5, 0.9):.2f}"')
    frame(s, ("#6a5a5e", "#2a1e22"), "#140c0e", "#ff4a4a", 440)
    # бархатные портьеры и золотая кайма
    for side in (-1, 1):
        x = AX0 if side < 0 else AX1
        pts = [(x - side * 6, SPRING - R * 0.55), (x + side * 70, SPRING - R * 0.2), (x + side * 48, SPRING + 80), (x + side * 22, SPRING + 260),
               (x + side * 36, H), (x - side * 6, H)]
        s.path(smooth(pts), s.lin([(0, "#a0142a"), (1, "#3a0410")], 0, 0, 1, 0), INK, 4)
        for k in (12, 28):
            s.path(f"M{x + side * k},{SPRING - R * 0.3} Q{x + side * (k + 14)},{SPRING + 200} {x + side * (k + 6)},{H}", "none",
                   stroke="#5a0614", sw=3)
        s.circle(x + side * 30, SPRING + 120, 9, "#d4a640", INK, 3)
    s.path(arch_path(AX0, AX1, SPRING, pad=6), "none", stroke="#d4a640", sw=4, extra='opacity="0.9"')
    finish(s, "#ff3a4a", 17)
    return s


# ═══════════════════════════ 4б — Звёздная Башня ═══════════════════════════

@scene("star_tower")
def draw_star_tower():
    s = Svg(W, H)
    rnd = random.Random(55)
    begin(s, [(0, "#04040f"), (0.5, "#12164a"), (0.85, "#3a2a78"), (1, "#6a5aa8")])
    # туманность
    for x, y, r, col, op in ((200, 230, 150, "#7a3aff", 0.35), (420, 300, 130, "#2ab0ff", 0.3), (300, 180, 90, "#ff6ad8", 0.2)):
        s.circle(x, y, r, col, extra=f'opacity="{op}" filter="{s.blur(40)}"')
    particles(s, rnd, 140, "#ffffff", 0.5, 1.5, 110, 700)
    for x, y, r in ((150, 170, 7), (460, 200, 6), (380, 140, 5), (200, 420, 5), (480, 460, 6)):
        s.path(star(x, y, r * 1.8, r * 0.5, 4), "#ffffff", stroke=None, extra=f'filter="{s.glow(3)}"')
    # лей-линии
    for pts, col in (([(AX0 - 20, 640), (200, 520), (CX, 470), (420, 380), (AX1 + 20, 250)], "#5ef0ff"),
                     ([(AX0 - 20, 330), (220, 420), (CX, 480), (420, 600), (AX1 + 20, 700)], "#c88aff")):
        s.path(smooth(pts, closed=False), "none", stroke=col, sw=16, extra=f'opacity="0.35" filter="{s.blur(8)}"')
        s.path(smooth(pts, closed=False), "none", stroke=col, sw=3, extra='opacity="0.9"')
    # парящие острова
    def island(x, y, w, h, top="#5a5aa0"):
        s.path(f"M{x - w},{y} Q{x - w * 0.6},{y + h} {x},{y + h * 1.6} Q{x + w * 0.6},{y + h} {x + w},{y} Z",
               s.lin([(0, "#3a3a6a"), (1, "#0a0a20")]), INK, 4)
        s.path(f"M{x - w},{y} Q{x},{y - 10} {x + w},{y}", "none", stroke=top, sw=8)
    island(150, 610, 50, 30)
    island(470, 560, 60, 34)
    island(CX, 600, 110, 60, "#7a7ad0")
    for x, y in ((150, 590), (470, 538)):
        s.path(f"M{x - 10},{y} V{y - 50} L{x},{y - 66} L{x + 10},{y - 50} V{y} Z", "#24245a", INK, 3)
        s.circle(x, y - 40, 3, "#bff6ff", extra=f'filter="{s.glow(3)}"')
    # башня
    tw = "#1e1e50"
    s.path(f"M{CX - 44},592 L{CX - 30},260 H{CX + 30} L{CX + 44},592 Z", s.lin([(0, "#3a3a8a"), (0.5, tw), (1, "#0a0a24")], 0, 0, 1, 0), INK, 5)
    for y in (500, 420, 340):
        w = 44 - (592 - y) * 0.045
        s.path(f"M{CX - w - 10},{y} H{CX + w + 10} V{y + 12} H{CX - w - 10} Z", "#2a2a6a", INK, 3)
    for y in (540, 460, 380, 300):
        s.path(f"M{CX - 6},{y + 14} V{y} Q{CX},{y - 6} {CX + 6},{y} V{y + 14} Z", "#bff6ff", stroke=None, extra=f'filter="{s.glow(3)}"')
    s.path(poly([(CX - 40, 262), (CX, 150), (CX + 40, 262)]), s.lin([(0, "#5a5ab0"), (1, "#1a1a48")], 0, 0, 1, 0), INK, 5)
    # кольца астролябии вокруг шпиля
    for rx, ry, rot in ((110, 26, -14), (90, 22, 22), (130, 30, 4)):
        s.ellipse(CX, 230, rx, ry, "none", INK, 8, extra=f'transform="rotate({rot} {CX} 230)"')
        s.ellipse(CX, 230, rx, ry, "none", "#d4b45a", 3.5, extra=f'transform="rotate({rot} {CX} 230)"')
    s.circle(CX, 150, 14, s.rad([(0, "#ffffff"), (0.5, "#9ad8ff"), (1, "#3a3aa0")]), INK, 3, extra=f'filter="{s.glow(8)}"')
    # парящая лестница к зрителю
    for i in range(8):
        t = i / 7
        y = 650 + i * 28
        w = 30 + 70 * t
        x = CX + math.sin(i * 0.7) * 30 * (1 - t)
        s.path(f"M{x - w},{y} H{x + w} L{x + w - 8},{y + 14} H{x - w + 8} Z", s.lin([(0, "#4a4a90"), (1, "#14143a")]), INK, 3)
        s.path(f"M{x - w},{y} H{x + w}", "none", stroke="#9aa8ff", sw=2)
    particles(s, rnd, 30, "#bff6ff", 1.0, 2.2, 400, H, (0.5, 1.0), glow=1.5)
    frame(s, ("#5a5a7a", "#22223a"), "#10101e", "#7ad8ff", 550)
    # светящиеся звёздные знаки по своду (свои, не из TES)
    for i in range(1, 13, 2):
        a = math.pi + math.pi * (i + 0.5) / 13
        x = CX + (R + FRAME / 2) * math.cos(a)
        y = SPRING + (R + FRAME / 2) * math.sin(a)
        s.path(star(x, y, 9, 3.5, 4 + i % 3), "#bff6ff", stroke=None, extra=f'opacity="0.9" filter="{s.glow(3)}"')
    finish(s, "#7a8aff", 19)
    return s


# ═══════════════════════════ запуск ═══════════════════════════

def check(keys) -> int:
    bad = []
    for key in keys:
        svg = os.path.join(OUT_SVG, f"door_{key}.svg")
        jpg = os.path.join(OUT_JPG, f"{key}.jpg")
        if not os.path.exists(svg) or open(svg, encoding="utf-8").read() != REGISTRY[key]().render():
            bad.append(f"{os.path.relpath(svg, ROOT)}: не совпадает с генератором (запустите с --render)")
        if not os.path.exists(jpg):
            bad.append(f"{os.path.relpath(jpg, ROOT)}: нет картины")
    for line in bad:
        print("✗", line)
    print(f"сцены: {len(keys)}, ошибок: {len(bad)}")
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
        svg = os.path.join(OUT_SVG, f"door_{key}.svg")
        with open(svg, "w", encoding="utf-8") as f:
            f.write(REGISTRY[key]().render())
        pairs += [svg, os.path.join(tmp, f"{key}.png")]
    print(f"SVG: {len(keys)} → {os.path.relpath(OUT_SVG, ROOT)}")
    if not render:
        return 0
    env = dict(os.environ, NODE_PATH=os.environ.get("NODE_PATH", "/tmp/r/node_modules"))
    subprocess.run(["node", os.path.join(ROOT, "tools", "art", "render_svg.mjs"), *pairs], check=True, env=env)
    from PIL import Image
    os.makedirs(OUT_JPG, exist_ok=True)
    for key in keys:
        Image.open(os.path.join(tmp, f"{key}.png")).convert("RGB").save(os.path.join(OUT_JPG, f"{key}.jpg"), quality=88, optimize=True)
    print(f"JPG: {len(keys)} → {os.path.relpath(OUT_JPG, ROOT)}")
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv))
