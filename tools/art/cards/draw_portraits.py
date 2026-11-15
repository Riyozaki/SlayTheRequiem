#!/usr/bin/env python3
"""Свои портреты карт там, где ванильная замена сбивает с толку (M7 п. 6, completion plan M3 п. 3, art spec §3.1).

Сбивает с толку (tools/art/art_fit.py):
- **дубль внутри мода** — две карты мода с одним ванильным портретом (Эйдолон, Страж могил, Нежить — по 2 карты);
- **исключения «тип:»** — портрет взят у ванильной карты другого типа (атака с силы, навык с атаки): рамка и картинка
  спорят. Своя картинка снимает исключение.

Портрет — сцена: фон (небо, дальние гряды, туман), крупный предмет-действие в центре, свет и частицы, виньетка, зерно.
Предметы — из того же набора знаков, что и значки реликвий (tools/art/relics/relic_kit.py), — один язык рисунка.
Форматы как у ванильных портретов: images/card_portraits/big/{id}.png 1000×760 и {id}.png 250×190 (RequiemCard берёт
свой большой → свой малый → ванильный, код не меняется). PNG квантуется в 256 цветов — бюджет размера пака.

    python3 tools/art/cards/draw_portraits.py            # только SVG (art/cards/)
    python3 tools/art/cards/draw_portraits.py --render   # + PNG (resvg: npm i --prefix /tmp/r @resvg/resvg-js@2, Pillow)
    python3 tools/art/cards/draw_portraits.py --check    # CI: SVG совпадают с генератором, PNG на месте и нужного размера
"""
from __future__ import annotations

import math
import os
import random
import struct
import subprocess
import sys
import tempfile

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.normpath(os.path.join(HERE, "..", "..", ".."))
sys.path.insert(0, os.path.join(HERE, "..", "monsters"))
sys.path.insert(0, os.path.join(HERE, "..", "relics"))
from svgkit import INK, Svg, poly, smooth, star  # noqa: E402
from relic_kit import BONE, ICE, LEATHER, METAL, STEEL, STONE, WOOD, emblem, line  # noqa: E402

W, H = 1000, 760
SW_, SH_ = 250, 190
CX, CY = 500, 380
OUT_SVG = os.path.join(ROOT, "art", "cards")
OUT_IMG = os.path.join(ROOT, "mod", "SlayTheRequiem", "images", "card_portraits")


# ───────────────────────── фон и свет ─────────────────────────

def lin(s, stops, x1=0, y1=0, x2=0, y2=1):
    return s.lin(stops, x1, y1, x2, y2)


def scene(s, sky, ridge, ground, seed, horizon=520, ridges=2):
    """Небо, дальние гряды (светлее и дальше — выше), земля."""
    s.path(f"M0,0 H{W} V{H} H0 Z", lin(s, sky), stroke=None)
    rnd = random.Random(seed)
    for k in range(ridges):
        y0 = horizon - 120 + k * 70
        pts = [(-20, H)]
        x = -20
        while x < W + 60:
            pts.append((x, y0 + rnd.uniform(-60, 40)))
            x += rnd.uniform(80, 160)
        pts += [(W + 40, y0), (W + 40, H)]
        s.path(smooth(pts, k=0.6), ridge[k % len(ridge)], stroke=None, extra=f'opacity="{0.55 + 0.25 * k}"')
    s.path(f"M0,{horizon + 60} Q{W / 2},{horizon + 20} {W},{horizon + 60} V{H} H0 Z", lin(s, ground), stroke=None)


def bloom(s, x, y, r, color, alpha=0.6):
    s.circle(x, y, r, color, extra=f'opacity="{alpha}" filter="{s.blur(r * 0.45)}"')


def rays(s, x, y, color, n=9, seed=1, length=900, alpha=0.18, spread=360, start=0):
    rnd = random.Random(seed)
    s.open(f'opacity="{alpha}" filter="{s.blur(8)}"')
    for i in range(n):
        a = math.radians(start + spread * (i + rnd.uniform(-0.3, 0.3)) / n)
        w = rnd.uniform(0.04, 0.09)
        p1 = (x + math.cos(a - w) * length, y + math.sin(a - w) * length)
        p2 = (x + math.cos(a + w) * length, y + math.sin(a + w) * length)
        s.path(poly([(x, y), p1, p2]), color, stroke=None)
    s.close()


def motes(s, color, n, seed, box=(0, 0, W, H), rmax=7, alpha=0.8):
    rnd = random.Random(seed)
    f = s.glow(2.5)
    s.open(f'filter="{f}"')
    for _ in range(n):
        x, y = rnd.uniform(box[0], box[2]), rnd.uniform(box[1], box[3])
        s.circle(round(x, 1), round(y, 1), round(rnd.uniform(1.5, rmax), 1), color, extra=f'opacity="{round(rnd.uniform(0.3, alpha), 2)}"')
    s.close()


def mist(s, y, color, alpha=0.35, h=90, seed=0):
    rnd = random.Random(seed)
    f = s.blur(30)
    for k in range(4):
        s.ellipse(round(rnd.uniform(0, W), 1), y + rnd.uniform(-20, 20), rnd.uniform(240, 420), h, color,
                  extra=f'opacity="{alpha}" filter="{f}"')


def vignette(s, strength=0.8):
    s.path(f"M0,0 H{W} V{H} H0 Z", s.rad([(0.55, "#000000", 0), (1, "#05030a", strength)], 0.5, 0.5, 0.75), stroke=None)


def shadow(s, x, y, rx, ry=None, alpha=0.5):
    s.ellipse(x, y, rx, ry or rx * 0.18, "#000000", extra=f'opacity="{alpha}" filter="{s.blur(rx * 0.12)}"')


def ghost(s, alpha=0.75):
    """Группа «призрак»: полупрозрачная, с мягким свечением."""
    s.open(f'opacity="{alpha}" filter="{s.glow(6)}"')


# ───────────────────────── предметы ─────────────────────────

def sword_big(s, x, y, length, angle, blade, hilt="#c8a050", grip="#4a2e1a", width=1.0):
    """Длинный меч от рукояти (x, y) под углом angle (0 — остриём вверх)."""
    a = math.radians(angle)
    ux, uy = math.sin(a), -math.cos(a)
    px, py = -uy, ux
    w = 26 * width

    def P(t, o):
        return (x + ux * t + px * o, y + uy * t + py * o)
    s.path(poly([P(0, -w), P(length * 0.86, -w * 0.9), P(length, 0), P(length * 0.86, w * 0.9), P(0, w)]), blade, INK, 9)
    s.path(poly([P(10, 0), P(length * 0.9, 0)], closed=False), stroke="#ffffff", sw=4, extra='opacity="0.6"')
    s.path(poly([P(0, -w * 3), P(0, w * 3), P(-22, w * 3), P(-22, -w * 3)]), hilt, INK, 8)
    s.path(poly([P(-22, -w * 0.55), P(-22, w * 0.55), P(-120, w * 0.5), P(-120, -w * 0.5)]), grip, INK, 8)
    cx_, cy_ = P(-138, 0)
    s.circle(round(cx_, 1), round(cy_, 1), round(w * 0.9, 1), hilt, INK, 7)


def rune_circle(s, x, y, rx, ry, color, n=12, seed=0, alpha=0.9):
    rnd = random.Random(seed)
    s.open(f'opacity="{alpha}" filter="{s.glow(5)}"')
    for k in (1.0, 0.82):
        s.ellipse(x, y, round(rx * k, 1), round(ry * k, 1), "none", color, 5)
    for i in range(n):
        a = 2 * math.pi * i / n
        mx, my = x + math.cos(a) * rx * 0.91, y + math.sin(a) * ry * 0.91
        h = 10
        kind = rnd.randrange(3)
        if kind == 0:
            d = f"M{mx - h:.1f},{my:.1f} L{mx + h:.1f},{my:.1f} M{mx:.1f},{my - h * ry / rx:.1f} L{mx:.1f},{my + h * ry / rx:.1f}"
        elif kind == 1:
            d = poly([(mx - h, my + h * 0.4), (mx, my - h * 0.5), (mx + h, my + h * 0.4)], closed=False)
        else:
            d = f"M{mx - h * 0.7:.1f},{my - h * 0.3:.1f} L{mx + h * 0.7:.1f},{my + h * 0.3:.1f}"
        s.path(d, stroke=color, sw=4)
    s.close()


def jag(x1, y1, x2, y2, seed, n=7, amp=40):
    rnd = random.Random(seed)
    pts = [(x1, y1)]
    for i in range(1, n):
        t = i / n
        pts.append((x1 + (x2 - x1) * t + rnd.uniform(-amp, amp), y1 + (y2 - y1) * t + rnd.uniform(-amp * 0.3, amp * 0.3)))
    pts.append((x2, y2))
    return pts


def lightning(s, pts, color="#fff6b0", width=10):
    d = poly(pts, closed=False)
    s.path(d, stroke=color, sw=width * 3, extra=f'opacity="0.35" filter="{s.blur(10)}"')
    s.path(d, stroke=INK, sw=width + 6)
    s.path(d, stroke=color, sw=width)
    s.path(d, stroke="#ffffff", sw=width * 0.35)


def cloud(s, x, y, r, fill, seed=0):
    rnd = random.Random(seed)
    pts = []
    for i in range(14):
        a = 2 * math.pi * i / 14
        k = rnd.uniform(0.75, 1.15)
        pts.append((x + math.cos(a) * r * 1.8 * k, y + math.sin(a) * r * 0.7 * k))
    s.path(smooth(pts, k=0.8), fill, INK, 7)


def drops(s, color, pts, r=12):
    for x, y in pts:
        s.path(f"M{x},{y - r * 1.6} Q{x + r},{y - r * 0.2} {x},{y + r} Q{x - r},{y - r * 0.2} {x},{y - r * 1.6} Z", color, INK, 4)


def helm(s, x, y, r, fill, eye="#9ff6ff"):
    """Шлем рыцаря анфас: купол, гребень, забрало со щелью."""
    s.path(smooth([(x - r, y + r * 0.9), (x - r * 1.02, y - r * 0.2), (x - r * 0.6, y - r * 0.95), (x, y - r * 1.15),
                   (x + r * 0.6, y - r * 0.95), (x + r * 1.02, y - r * 0.2), (x + r, y + r * 0.9), (x, y + r * 1.15)], k=0.5),
           fill, INK, 10)
    s.path(f"M{x},{y - r * 1.14} L{x},{y + r * 1.1}", stroke=INK, sw=7, extra='opacity="0.5"')
    s.path(poly([(x - r * 0.78, y - r * 0.12), (x + r * 0.78, y - r * 0.12), (x + r * 0.7, y + r * 0.06), (x - r * 0.7, y + r * 0.06)]), "#0b0a14", INK, 6)
    for dx in (-0.36, 0.36):
        s.eye(x + r * dx, y - r * 0.03, r * 0.07, eye)
    for k in range(5):
        yy = y + r * (0.32 + 0.12 * k)
        s.path(f"M{x - r * 0.42},{yy:.1f} L{x - r * 0.16},{yy:.1f} M{x + r * 0.16},{yy:.1f} L{x + r * 0.42},{yy:.1f}", stroke=INK, sw=5)
    s.path(smooth([(x - r * 0.2, y - r * 1.12), (x - r * 0.1, y - r * 1.6), (x + r * 0.5, y - r * 1.85), (x + r * 0.3, y - r * 1.3),
                   (x + r * 0.15, y - r * 1.1)], k=0.6), "#3a8a9a", INK, 6)


def hooded(s, x, y, r, cloak, face="#07060c", eyes=None):
    s.path(smooth([(x - r * 1.5, y + r * 2.6), (x - r * 1.05, y + r * 0.6), (x - r * 0.9, y - r * 0.5), (x - r * 0.35, y - r * 1.2),
                   (x + r * 0.1, y - r * 1.35), (x + r * 0.75, y - r * 0.85), (x + r * 0.95, y + r * 0.2), (x + r * 1.05, y + r * 0.6),
                   (x + r * 1.5, y + r * 2.6)], k=0.5), cloak, INK, 10)
    s.path(smooth([(x - r * 0.55, y + r * 0.45), (x - r * 0.6, y - r * 0.3), (x, y - r * 0.85), (x + r * 0.6, y - r * 0.3),
                   (x + r * 0.55, y + r * 0.45), (x, y + r * 0.7)], k=0.6), face, INK, 7)
    if eyes:
        for dx in (-0.22, 0.22):
            s.eye(x + r * dx, y - r * 0.05, r * 0.07, eyes)


def bones_hand(s, x, y, k, fill):
    """Кисть скелета, растущая из земли (запястье в (x, y))."""
    s.path(poly([(x - 26 * k, y), (x - 20 * k, y - 120 * k), (x + 20 * k, y - 120 * k), (x + 26 * k, y)]), fill, INK, 8)
    s.path(smooth([(x - 52 * k, y - 120 * k), (x - 40 * k, y - 170 * k), (x + 40 * k, y - 175 * k), (x + 58 * k, y - 125 * k),
                   (x, y - 105 * k)], k=0.5), fill, INK, 8)
    for i, (dx, ln, a) in enumerate(((-48, 120, -28), (-20, 150, -10), (8, 160, 4), (34, 140, 18), (58, 70, 60))):
        bx, by = x + dx * k, y - 165 * k
        if i == 4:
            bx, by = x + 52 * k, y - 125 * k
        ex = bx + math.sin(math.radians(a)) * ln * k
        ey = by - math.cos(math.radians(a)) * ln * k
        mx, my = (bx + ex) / 2, (by + ey) / 2
        for p, q in (((bx, by), (mx, my)), ((mx, my), (ex, ey))):
            line(s, [p, q], fill, 16 * k)
            s.circle(round(q[0], 1), round(q[1], 1), round(10 * k, 1), fill, INK, 4)


# ───────────────────────── портреты ─────────────────────────

def blade_spirit(s):
    """Дух клинка: призрачный меч парит над рукой латника, вокруг — вихрь огоньков-духов."""
    scene(s, [(0, "#0c1426"), (0.6, "#1c2c48"), (1, "#24304a")], ["#121c30", "#0e1626"], [(0, "#141a28"), (1, "#07090f")], 11)
    rays(s, CX, 300, "#9fe8ff", 11, 3, alpha=0.12)
    bloom(s, CX, 320, 260, "#5ac8ff", 0.45)
    ghost(s, 0.8)
    sword_big(s, CX + 40, 600, 470, 12, s.lin([(0, "#e8fbff"), (1, "#5ab0e8")], 0, 0, 1, 0), hilt="#8ad8f0", grip="#2a5a7a")
    s.close()
    for i in range(7):
        a = i / 7 * 2 * math.pi
        x, y = CX + math.cos(a) * 270, 340 + math.sin(a) * 120
        emblem(s, "wisp", round(x, 1), round(y, 1), 34, "#bff4ff")
    motes(s, "#cff6ff", 60, 5, (100, 80, 900, 600))
    vignette(s)


def call_spirit_knight(s):
    """Зов Рыцаря-Духа: из тумана встаёт призрачный рыцарь — шлем с гребнем, щит, светящиеся глаза."""
    scene(s, [(0, "#0a1418"), (0.6, "#183034"), (1, "#20383a")], ["#0e2226", "#0a1a1c"], [(0, "#10201e"), (1, "#050a0a")], 21)
    bloom(s, CX, 300, 300, "#5af0d0", 0.35)
    ghost(s, 0.82)
    s.path(smooth([(CX - 330, H + 20), (CX - 290, 520), (CX - 160, 440), (CX + 160, 440), (CX + 290, 520), (CX + 330, H + 20)], k=0.5),
           s.lin([(0, "#bff8ec"), (1, "#3a9a8a", 0.2)]), INK, 10)
    s.path(smooth([(CX - 330, 560), (CX - 340, 470), (CX - 250, 450), (CX - 190, 520), (CX - 210, 640), (CX - 280, 700)], k=0.6),
           s.lin([(0, "#e0fff8"), (1, "#5ab8a8")]), INK, 9)
    emblem(s, "sun_cross", CX - 268, 560, 50, "#3a8a7a")
    helm(s, CX, 300, 150, s.lin([(0, "#f0fffa"), (1, "#6ac0b0")], 0, 0, 0.6, 1), eye="#d8fff0")
    s.close()
    sword_big(s, CX + 300, 720, 420, 8, s.lin([(0, "#e8fff8"), (1, "#7ac8b8")], 0, 0, 1, 0), hilt="#8ad8c0", grip="#2a5a52", width=0.8)
    mist(s, 640, "#9ff0dc", 0.3, seed=2)
    motes(s, "#d0fff4", 40, 7)
    vignette(s)


def conjure_abyss_guardian(s):
    """Страж Бездны: из багрово-фиолетового провала встаёт каменный страж — глаз ещё закрыт (первый ход дремлет)."""
    s.path(f"M0,0 H{W} V{H} H0 Z", s.rad([(0, "#4a2068"), (0.6, "#1a0c2a"), (1, "#07040c")], 0.5, 0.6, 0.7), stroke=None)
    for k in range(6, 0, -1):
        r = k * 70
        s.ellipse(CX, 520, r * 1.4, r * 0.42, "none", "#b06aff", 4, extra=f'opacity="{0.08 + 0.05 * (6 - k)}"')
    bloom(s, CX, 520, 240, "#a040ff", 0.55)
    for side in (-1, 1):
        s.path(smooth([(CX + side * 200, 190), (CX + side * 330, 120), (CX + side * 380, 10), (CX + side * 300, 90),
                       (CX + side * 170, 130)], k=0.5), lin(s, BONE), INK, 9)
    s.path(smooth([(CX - 280, 560), (CX - 300, 330), (CX - 220, 160), (CX - 60, 90), (CX + 60, 90), (CX + 220, 160), (CX + 300, 330),
                   (CX + 280, 560), (CX, 600)], k=0.55), lin(s, STONE + [(1, "#1a1620")]), INK, 12)
    for pts in (((CX - 220, 200), (CX - 150, 260), (CX - 180, 340)), ((CX + 200, 180), (CX + 150, 280), (CX + 220, 380)),
                ((CX - 40, 120), (CX + 10, 170), (CX - 20, 220))):
        s.path(poly(pts, closed=False), stroke="#1a1620", sw=6)
    s.path(smooth([(CX - 150, 300), (CX, 250), (CX + 150, 300), (CX, 345)], k=0.5), "#2a2232", INK, 9)
    s.path(f"M{CX - 140},302 Q{CX},355 {CX + 140},302", stroke="#d08aff", sw=7, extra=f'filter="{s.glow(5)}"')
    for dx in (-80, 0, 80):
        s.path(f"M{CX + dx},{322 + abs(dx) * -0.15:.1f} l{dx * 0.12:.1f},26", stroke="#d08aff", sw=5, extra='opacity="0.8"')
    for side in (-1, 1):
        s.path(smooth([(CX + side * 270, 420), (CX + side * 400, 380), (CX + side * 460, 470), (CX + side * 440, 600),
                       (CX + side * 300, 620)], k=0.4), lin(s, STONE), INK, 10)
        for k in range(3):
            fx = CX + side * (330 + k * 45)
            s.path(poly([(fx - 16, 600), (fx + 16, 600), (fx, 650)]), lin(s, BONE), INK, 5)
    s.path(f"M0,{H} L0,600 Q{CX},520 {W},600 L{W},{H} Z", "#07040c", stroke=None, extra='opacity="0.85"')
    emblem(s, "spiral", CX, 470, 60, "#c08aff")
    motes(s, "#e0b8ff", 50, 9, (0, 300, W, H))
    vignette(s, 0.85)


def lesser_ward(s):
    """Малый оберег: ладонь поднимает светящийся купол-печать с пентаклем."""
    scene(s, [(0, "#101026"), (0.7, "#262a4a"), (1, "#2e2a44")], ["#1a1c34", "#14162a"], [(0, "#1a1a2a"), (1, "#08080e")], 31)
    bloom(s, CX, 330, 280, "#8ab0ff", 0.4)
    s.path(f"M{CX - 270},560 A270,270 0 0 1 {CX + 270},560 Z", s.rad([(0.6, "#8ab4ff", 0.05), (1, "#cfe0ff", 0.55)], 0.5, 1, 1),
           "#cfe0ff", 6, extra=f'filter="{s.glow(6)}"')
    rune_circle(s, CX, 560, 300, 70, "#bcd4ff", 16, 4)
    emblem(s, "pentacle", CX, 330, 110, "#e8f0ff")
    for side in (-1, 1):
        emblem(s, "star", CX + side * 190, 250, 22, "#e8f0ff")
    s.path(smooth([(CX - 120, H + 20), (CX - 110, 690), (CX - 150, 620), (CX - 120, 600), (CX - 60, 640), (CX - 40, 590),
                   (CX + 30, 580), (CX + 90, 600), (CX + 120, 650), (CX + 110, H + 20)], k=0.5), lin(s, LEATHER), INK, 9)
    motes(s, "#e0ecff", 50, 11)
    vignette(s)


def raise_dead(s):
    """Поднять мертвеца: из разрытой могилы под зелёным светом тянется кость-рука, рядом кренится надгробие."""
    scene(s, [(0, "#0a140c"), (0.6, "#1a2c1c"), (1, "#22301e")], ["#14201a", "#0e1812"], [(0, "#1e1a14"), (1, "#080604")], 41)
    s.circle(780, 150, 70, "#d8f0c0", extra=f'filter="{s.glow(12)}" opacity="0.8"')
    s.path(poly([(150, 600), (160, 380), (200, 330), (260, 330), (300, 380), (300, 610)]), lin(s, STONE), INK, 10)
    s.path("M200,420 h60 M230,390 v80", stroke="#2a2630", sw=10)
    bloom(s, CX, 560, 240, "#6aff6a", 0.45)
    s.ellipse(CX, 610, 260, 60, "#2a1e14", INK, 8)
    s.ellipse(CX, 600, 200, 36, "#120c08")
    rays(s, CX, 600, "#8aff8a", 9, 4, length=500, alpha=0.2, spread=140, start=200)
    bones_hand(s, CX, 600, 1.35, lin(s, BONE))
    for x, y in ((CX - 200, 640), (CX + 180, 650), (CX + 240, 600)):
        s.path(smooth([(x - 30, y), (x - 10, y - 30), (x + 25, y - 18), (x + 30, y + 8)], k=0.6), "#3a2a1a", INK, 5)
    motes(s, "#a0ff90", 60, 13, (150, 200, 850, 650))
    mist(s, 650, "#5ac85a", 0.25, seed=3)
    vignette(s)


def blood_lord_stance(s):
    """Кровавый лорд: под алой луной — силуэт в высоком воротнике и плаще-крыльях, с клыков капает кровь."""
    s.path(f"M0,0 H{W} V{H} H0 Z", lin(s, [(0, "#1a0408"), (0.6, "#3a0a14"), (1, "#14040a")]), stroke=None)
    s.circle(CX, 280, 190, s.rad([(0, "#ff8070"), (0.7, "#c01a2a"), (1, "#6a0a14")], 0.4, 0.4, 0.7), extra=f'filter="{s.glow(18)}"')
    bloom(s, CX, 280, 300, "#ff2a3a", 0.35)
    for side in (-1, 1):
        wing = [(CX, 330), (CX + side * 120, 260), (CX + side * 300, 220), (CX + side * 460, 280), (CX + side * 400, 340),
                (CX + side * 420, 420), (CX + side * 330, 400), (CX + side * 310, 500), (CX + side * 220, 460), (CX + side * 160, 600)]
        s.path(poly(wing), "#0c0408", INK, 8)
    s.path(smooth([(CX - 200, H + 20), (CX - 150, 460), (CX - 90, 390), (CX - 140, 300), (CX - 80, 330), (CX, 360), (CX + 80, 330),
                   (CX + 140, 300), (CX + 90, 390), (CX + 150, 460), (CX + 200, H + 20)], k=0.4), "#14060c", INK, 9)
    s.path(smooth([(CX - 62, 300), (CX - 70, 220), (CX, 170), (CX + 70, 220), (CX + 62, 300), (CX, 345)], k=0.6), "#1e0c14", INK, 8)
    for dx in (-26, 26):
        s.eye(CX + dx, 255, 8, "#ff3a4a")
    for dx in (-14, 14):
        s.path(poly([(CX + dx - 6, 300), (CX + dx + 6, 300), (CX + dx, 324)]), "#ffffff", INK, 2)
    drops(s, "#d01a2a", [(CX - 14, 350), (CX + 16, 372), (CX - 4, 420)], 9)
    motes(s, "#ff6070", 40, 17)
    vignette(s, 0.85)


def storm_call(s):
    """Зов грозы: из клубящихся туч бьют несколько молний сразу — по всему полю."""
    scene(s, [(0, "#0a0e1c"), (0.5, "#1c2440"), (1, "#2a3050")], ["#141a30", "#0e1222"], [(0, "#141826"), (1, "#06070c")], 51, horizon=560)
    for i, (x, y, r) in enumerate(((180, 110, 90), (420, 80, 110), (700, 110, 100), (900, 90, 80), (300, 170, 80), (600, 180, 90))):
        cloud(s, x, y, r, lin(s, [(0, "#4a5070"), (1, "#1e2236")]), seed=i)
    for i, (x1, x2) in enumerate(((260, 200), (500, 520), (760, 820))):
        pts = jag(x1, 190, x2, 640, seed=7 + i, amp=60)
        lightning(s, pts, "#bfe4ff" if i != 1 else "#fff6b0", 12 if i == 1 else 9)
        bloom(s, x2, 640, 90, "#bfe4ff", 0.5)
        rays(s, x2, 640, "#dff0ff", 7, i, length=160, alpha=0.35, spread=180, start=180)
    motes(s, "#dff0ff", 30, 19, (0, 500, W, H))
    vignette(s)


def toxic_coating(s):
    """Ядовитая смазка: клинок кинжала покрыт зелёным ядом, капли срываются с острия, рядом — флакон."""
    s.path(f"M0,0 H{W} V{H} H0 Z", s.rad([(0, "#24341a"), (0.7, "#0e160a"), (1, "#050804")], 0.5, 0.45, 0.75), stroke=None)
    bloom(s, CX, 330, 260, "#8aff3a", 0.3)
    sword_big(s, 360, 660, 520, 38, lin(s, STEEL, 0, 0, 1, 0), hilt="#5a6a3a", grip="#2a2014", width=1.2)
    a = math.radians(38)
    ux, uy = math.sin(a), -math.cos(a)
    pts = [(360 + ux * t + uy * 22 * o, 660 + uy * t - ux * 22 * o) for t, o in ((120, 1.4), (240, 1.6), (380, 1.3), (470, 0.6))]
    s.path(smooth([(360 + ux * 80, 660 + uy * 80)] + pts + [(360 + ux * 470 - uy * 10, 660 + uy * 470 + ux * 10),
                                                         (360 + ux * 120 - uy * 20, 660 + uy * 120 + ux * 20)], k=0.5),
           "#7aff3a", INK, 6, extra=f'opacity="0.85" filter="{s.glow(4)}"')
    tip = (360 + ux * 520, 660 + uy * 520)
    drops(s, "#8aff3a", [(round(tip[0] + 6, 1), round(tip[1] + 60, 1)), (round(tip[0] - 20, 1), round(tip[1] + 130, 1)),
                         (round(tip[0] - 70, 1), round(tip[1] + 90, 1))], 13)
    s.path(smooth([(170, 700), (130, 600), (150, 520), (180, 500), (180, 450), (230, 450), (230, 500), (260, 520), (280, 600), (240, 700)],
                  k=0.4), s.lin([(0, "#bfff8a"), (1, "#2a6a14")]), INK, 8)
    s.path("M168,440 h74 v-26 h-74 Z", lin(s, WOOD), INK, 6)
    emblem(s, "skull", 205, 610, 42, "#e8f8d0")
    motes(s, "#b0ff70", 40, 23)
    vignette(s)


def bound_blade(s):
    """Призванный клинок: из печати на земле, остриём вниз, нисходит сияющий фиолетовый клинок."""
    scene(s, [(0, "#120a24"), (0.6, "#26164a"), (1, "#2c1c44")], ["#1c1234", "#140c26"], [(0, "#1c1428"), (1, "#08060c")], 61)
    rays(s, CX, 120, "#d8b8ff", 9, 6, alpha=0.14, spread=120, start=30)
    bloom(s, CX, 600, 220, "#b070ff", 0.5)
    rune_circle(s, CX, 610, 300, 80, "#d8b0ff", 14, 6)
    sword_big(s, CX, 200, 400, 180, lin(s, [(0, "#ffffff"), (0.5, "#d8c0ff"), (1, "#7a4ad0")], 0, 0, 1, 0), hilt="#f0c050", grip="#3a1a5a")
    s.path(f"M{CX - 30},600 L{CX + 30},600", stroke="#ffffff", sw=8, extra=f'filter="{s.glow(6)}"')
    for i in range(8):
        a = math.radians(-90 + (i - 3.5) * 22)
        x, y = CX + math.cos(a) * 160, 620 + math.sin(a) * 160
        emblem(s, "star", round(x, 1), round(y, 1), 12, "#f0e0ff")
    motes(s, "#e8d8ff", 50, 29)
    vignette(s)


def sparks(s):
    """Искры: на кончиках пальцев трещат фиолетовые искры, ветвистые разряды скачут во все стороны."""
    s.path(f"M0,0 H{W} V{H} H0 Z", s.rad([(0, "#2a1a4a"), (0.7, "#100a1e"), (1, "#05030a")], 0.5, 0.5, 0.7), stroke=None)
    bloom(s, CX, 330, 200, "#c070ff", 0.6)
    s.path(smooth([(CX - 260, H + 20), (CX - 230, 640), (CX - 200, 560), (CX - 150, 470), (CX - 110, 440), (CX - 80, 470), (CX - 90, 520),
                   (CX - 40, 430), (CX - 10, 400), (CX + 20, 420), (CX + 10, 470), (CX + 60, 420), (CX + 95, 420), (CX + 90, 480),
                   (CX + 60, 560), (CX + 30, 650), (CX + 20, H + 20)], k=0.5), lin(s, [(0, "#d8b8a0"), (1, "#6a4a40")]), INK, 9)
    for i in range(9):
        a = math.radians(-160 + i * 18 + (i % 2) * 7)
        r = 220 + (i % 3) * 60
        pts = jag(CX, 340, CX + math.cos(a) * r, 340 + math.sin(a) * r, seed=31 + i, n=5, amp=24)
        lightning(s, pts, "#e0b0ff", 6)
    s.circle(CX, 340, 46, s.rad([(0, "#ffffff"), (0.6, "#e8c8ff"), (1, "#a050ff")], 0.5, 0.5, 0.5), extra=f'filter="{s.glow(10)}"')
    for i in range(14):
        a = i * 2.4
        x, y = CX + math.cos(a) * (120 + i * 16), 340 + math.sin(a) * (80 + i * 10)
        s.path(star(round(x, 1), round(y, 1), 14, 3, 4), "#f4e0ff", stroke=None, extra=f'filter="{s.glow(3)}"')
    vignette(s)


def firebolt(s):
    """Огненная стрела: пылающий снаряд летит наискосок, за ним — хвост огня и искр."""
    scene(s, [(0, "#1a0a08"), (0.6, "#3a1610"), (1, "#3a1c12")], ["#2a120c", "#1e0c08"], [(0, "#22140e"), (1, "#0a0604")], 71)
    tail = [(120, 640), (300, 520), (520, 380), (720, 250)]
    s.path(poly(tail, closed=False), stroke="#ff7a1a", sw=120, extra=f'opacity="0.35" filter="{s.blur(30)}"')
    for k, (col, w) in enumerate((("#ff4a10", 80), ("#ffa020", 48), ("#fff0a0", 18))):
        s.path(smooth(tail, closed=False, k=0.5), stroke=col, sw=w, extra=f'filter="{s.blur(6 - k * 2)}"')
    for i in range(18):
        rnd = random.Random(i)
        t = rnd.uniform(0.05, 0.9)
        x = 120 + (720 - 120) * t + rnd.uniform(-60, 60)
        y = 640 + (250 - 640) * t + rnd.uniform(-60, 60)
        s.path(star(round(x, 1), round(y, 1), rnd.uniform(6, 16), 3, 4), "#ffd070", stroke=None, extra=f'filter="{s.glow(2)}"')
    bloom(s, 740, 240, 240, "#ff8a20", 0.7)
    s.open('transform="rotate(234 740 240)"')
    emblem(s, "flame", 740, 200, 170, s.rad([(0, "#fff6c0"), (0.5, "#ffa020"), (1, "#e03a10")], 0.5, 0.7, 0.7))
    s.close()
    vignette(s)


def blizzard(s):
    """Буран: метель хлещет по ледяным пикам, снежинки и вихри ветра по всему полю."""
    scene(s, [(0, "#a8c8e0"), (0.5, "#5a7aa0"), (1, "#3a5070")], ["#7a9ac0", "#506a90"], [(0, "#dceaf6"), (1, "#7a98b8")], 81, horizon=540)
    for x, h in ((120, 260), (300, 340), (520, 300), (760, 380), (930, 260)):
        s.path(poly([(x - 150, 600), (x, 600 - h), (x + 150, 600)]), lin(s, ICE), INK, 8)
        s.path(poly([(x - 50, 600 - h * 0.66), (x, 600 - h), (x + 50, 600 - h * 0.66), (x + 15, 600 - h * 0.58), (x, 600 - h * 0.7),
                     (x - 15, 600 - h * 0.58)]), "#ffffff", stroke=None)
    for i in range(7):
        y = 140 + i * 80
        s.path(f"M{-50 + i * 30},{y} C{250},{y - 80} {500},{y + 70} {1050},{y - 40}", stroke="#ffffff", sw=10 - i % 3 * 2,
               extra=f'opacity="0.45" filter="{s.blur(3)}"')
    rnd = random.Random(5)
    for _ in range(26):
        x, y, r = rnd.uniform(40, 960), rnd.uniform(40, 700), rnd.uniform(8, 26)
        d = " ".join(f"M{x:.1f},{y:.1f} L{x + math.cos(a) * r:.1f},{y + math.sin(a) * r:.1f}" for a in [k * math.pi / 3 for k in range(6)])
        s.path(d, stroke="#ffffff", sw=max(2.5, r / 6), extra='opacity="0.9"')
    vignette(s, 0.6)


def terror(s):
    """Страх: огромная тень с горящими глазами и когтями нависает над крошечной фигурой."""
    scene(s, [(0, "#0a060e"), (0.6, "#1c1224"), (1, "#24162a")], ["#140c1a", "#0e0812"], [(0, "#140e18"), (1, "#050306")], 91, horizon=560)
    s.path(smooth([(80, H), (60, 420), (160, 180), (330, 60), (500, 30), (670, 60), (840, 180), (940, 420), (920, H)], k=0.5),
           "#050308", stroke=None, extra=f'filter="{s.blur(6)}"')
    for dx in (-140, 140):
        s.ellipse(CX + dx, 250, 60, 28, "#ffcf3a", extra=f'filter="{s.glow(14)}"')
        s.ellipse(CX + dx, 250, 12, 26, "#1a0a04")
    s.path(smooth([(CX - 170, 400), (CX, 470), (CX + 170, 400), (CX, 430)], k=0.5), "#2a0a10", stroke=None)
    for side in (-1, 1):
        for k in range(4):
            x0 = CX + side * (260 + k * 50)
            s.path(f"M{x0},{420 + k * 10} Q{x0 + side * 40},{520 + k * 20} {x0 - side * 30},{620 + k * 10}", stroke="#050308", sw=30 - k * 4)
    bloom(s, CX, 650, 80, "#ffd88a", 0.35)
    s.path(smooth([(CX - 20, 690), (CX - 16, 640), (CX - 10, 615), (CX, 605), (CX + 10, 615), (CX + 16, 640), (CX + 20, 690)], k=0.5),
           "#b8a8c8", INK, 4)
    s.circle(CX, 596, 12, "#b8a8c8", INK, 4)
    shadow(s, CX, 694, 40)
    motes(s, "#ffcf3a", 14, 37, (200, 150, 800, 400), rmax=4)
    vignette(s, 0.9)


def poison_spray(s):
    """Ядовитый спрей: из горлышка склянки бьёт веер ядовитых брызг и облачко."""
    s.path(f"M0,0 H{W} V{H} H0 Z", s.rad([(0, "#203018"), (0.7, "#0e160a"), (1, "#050804")], 0.35, 0.6, 0.8), stroke=None)
    s.open('transform="rotate(35 250 520)"')
    s.path(smooth([(170, 680), (130, 580), (150, 480), (210, 450), (210, 380), (290, 380), (290, 450), (350, 480), (370, 580), (330, 680)],
                  k=0.4), s.lin([(0, "#d8ffb0"), (1, "#3a8a1a")]), INK, 9)
    s.path("M200,380 h100 v-40 h-100 Z", lin(s, WOOD), INK, 7)
    emblem(s, "skull", 250, 580, 50, "#f0ffe0")
    s.close()
    rnd = random.Random(7)
    for i in range(40):
        a = math.radians(rnd.uniform(-58, -10))
        r = rnd.uniform(140, 620)
        x, y = 350 + math.cos(a) * r, 380 + math.sin(a) * r * 0.9
        s.circle(round(x, 1), round(y, 1), round(rnd.uniform(5, 18), 1), "#8aff3a", INK, 3, extra=f'opacity="{round(rnd.uniform(0.6, 1), 2)}"')
    for x, y, r in ((560, 300, 110), (720, 220, 130), (820, 360, 100)):
        s.circle(x, y, r, "#7aff4a", extra=f'opacity="0.22" filter="{s.blur(30)}"')
    motes(s, "#b0ff70", 30, 41, (400, 80, 950, 500))
    vignette(s)


def herald_stance_card(s):
    """Стойка Вестника: фигура в капюшоне, вокруг неё по кругу три знака стоек — Чума, Раздор, Распад."""
    scene(s, [(0, "#0c0a10"), (0.6, "#1e1a22"), (1, "#24202a")], ["#16121a", "#100d14"], [(0, "#16141a"), (1, "#060508")], 101)
    s.ellipse(CX, 420, 330, 250, "none", "#8a8070", 5, extra='opacity="0.5" stroke-dasharray="18 14"')
    for (x, y, sign, col, glowc) in ((CX, 150, "skull", "#c8f0a0", "#6aff4a"), (CX - 290, 560, "sword", "#ff8a7a", "#ff3a2a"),
                                     (CX + 290, 560, "spiral", "#d0c8b8", "#b0a080")):
        bloom(s, x, y, 110, glowc, 0.45)
        s.circle(x, y, 78, s.rad([(0, "#3a3240"), (1, "#141018")], 0.4, 0.4, 0.7), "#c8a050", 8)
        emblem(s, sign, x, y, 52, col)
    hooded(s, CX, 400, 120, lin(s, [(0, "#4a4238"), (1, "#16120e")]), eyes="#ffd070")
    s.path(f"M{CX + 120},470 l40,90", stroke="#5a4a30", sw=10)
    s.path(poly([(CX + 140, 560), (CX + 190, 560), (CX + 200, 640), (CX + 130, 640)]), "#2a2418", INK, 6)
    s.path(poly([(CX + 148, 572), (CX + 182, 572), (CX + 190, 628), (CX + 140, 628)]), "#ffd070", stroke=None, extra=f'filter="{s.glow(8)}"')
    motes(s, "#e8d8b0", 30, 43)
    vignette(s)


def whirlwind(s):
    """Размах: секира описывает полный круг — свистящие дуги и пыль по всему полю."""
    scene(s, [(0, "#2a1c14"), (0.6, "#5a3a24"), (1, "#4a3020")], ["#3a2618", "#2a1c12"], [(0, "#3a2a1a"), (1, "#100a06")], 111, horizon=540)
    for k, (r, a) in enumerate(((330, 0.5), (280, 0.35), (380, 0.25))):
        s.path(f"M{CX - r},{380} A{r},{r * 0.5} 0 1 1 {CX + r * 0.7},{380 + r * 0.36}", stroke="#fff0d0", sw=26 - k * 6,
               extra=f'opacity="{a}" filter="{s.blur(4)}"')
    s.path(f"M{CX + 300},{380 - 120} A330,165 0 0 1 {CX + 200},{380 + 140}", stroke="#ffffff", sw=12, extra=f'opacity="0.7" filter="{s.blur(2)}"')
    s.open(f'transform="rotate(-30 {CX} 380)"')
    line(s, [(CX, 380), (CX + 330, 380)], "#7a4a2a", 22)
    s.path(smooth([(CX + 300, 300), (CX + 400, 260), (CX + 430, 380), (CX + 400, 500), (CX + 300, 460), (CX + 320, 380)], k=0.4),
           lin(s, METAL), INK, 10)
    s.close()
    emblem(s, "wind", CX - 260, 240, 60, "#f0e0c0")
    emblem(s, "wind", CX + 300, 600, 50, "#f0e0c0")
    rnd = random.Random(3)
    for _ in range(40):
        a = rnd.uniform(0, 2 * math.pi)
        r = rnd.uniform(200, 420)
        s.circle(round(CX + math.cos(a) * r, 1), round(380 + math.sin(a) * r * 0.5, 1), round(rnd.uniform(3, 9), 1), "#c8a070",
                 extra='opacity="0.6"')
    vignette(s)


def miasma(s):
    """Миазмы: над топью с мёртвыми деревьями ползёт ядовито-зелёный туман и лопаются пузыри."""
    scene(s, [(0, "#0e140a"), (0.6, "#24301a"), (1, "#2a3018")], ["#1a2212", "#12180c"], [(0, "#1e2614"), (1, "#080a04")], 121, horizon=500)
    for x, h, lean in ((140, 380, -1), (840, 420, 1), (650, 260, -1)):
        s.path(f"M{x - 18},{620} L{x - 8},{620 - h} L{x + 8},{620 - h} L{x + 18},{620} Z", "#0c0a06", INK, 5)
        for k in range(3):
            y = 620 - h * (0.5 + k * 0.18)
            s.path(f"M{x},{y:.1f} q{lean * 60},-30 {lean * 110},-80", stroke="#0c0a06", sw=12 - k * 3)
    s.ellipse(CX, 640, 520, 90, s.lin([(0, "#3a5a1a"), (1, "#1a2a0a")]), INK, 6)
    for x, y, r in ((360, 630, 22), (520, 650, 30), (640, 620, 16), (430, 660, 12)):
        s.circle(x, y, r, "#8aff4a", INK, 4, extra='opacity="0.75"')
        s.circle(x - r * 0.3, y - r * 0.3, r * 0.25, "#ffffff", extra='opacity="0.7"')
    for i, (y, a) in enumerate(((560, 0.4), (470, 0.32), (380, 0.25))):
        mist(s, y, "#8aff4a", a, h=80, seed=i + 5)
    emblem(s, "skull", CX, 380, 70, "#9aff6a")
    s.circle(CX, 380, 120, "#0e140a", extra='opacity="0.0"')
    motes(s, "#c0ff8a", 40, 47, (0, 200, W, 680))
    vignette(s)


def focused_aim(s):
    """Прицел: натянутый лук, кольца прицела сходятся на точке, вокруг — тишина и собранность."""
    scene(s, [(0, "#14181e"), (0.6, "#2c3440"), (1, "#323a44")], ["#20262e", "#181c22"], [(0, "#1e2228"), (1, "#08090c")], 131)
    for k, r in enumerate((260, 180, 100)):
        s.circle(CX, 330, r, "none", "#ffd89a", 6 - k, extra=f'opacity="{0.35 + k * 0.15}" filter="{s.glow(3)}"')
    for a in (0, 90, 180, 270):
        x, y = CX + math.cos(math.radians(a)) * 300, 330 + math.sin(math.radians(a)) * 300
        x2, y2 = CX + math.cos(math.radians(a)) * 130, 330 + math.sin(math.radians(a)) * 130
        s.path(f"M{x:.1f},{y:.1f} L{x2:.1f},{y2:.1f}", stroke="#ffd89a", sw=6, extra='opacity="0.7"')
    bloom(s, CX, 330, 60, "#fff0c0", 0.8)
    emblem(s, "eye", CX, 330, 50, "#fff4d8")
    s.path(f"M120,{H - 60} Q60,380 220,90", stroke=INK, sw=30)
    s.path(f"M120,{H - 60} Q60,380 220,90", stroke=lin(s, WOOD), sw=20)
    s.path(f"M220,90 L380,400 L120,{H - 60}", stroke="#e8e0d0", sw=4)
    line(s, [(380, 400), (CX - 60, 345)], "#b08a5a", 10)
    s.path(poly([(CX - 60, 330), (CX - 10, 343), (CX - 60, 360)]), lin(s, STEEL), INK, 5)
    for dy in (-14, 14):
        s.path(poly([(380, 400), (340, 400 + dy * 2.4), (360, 400)]), "#e04a3a", INK, 4)
    vignette(s, 0.85)


def rebound(s):
    """Отдача: щит принимает удар магического снаряда — тот рикошетом разлетается искрами."""
    scene(s, [(0, "#101420"), (0.6, "#22283c"), (1, "#283044")], ["#181e2e", "#121624"], [(0, "#181c28"), (1, "#06070c")], 141)
    s.path("M80,180 Q300,260 470,350", stroke="#7ad0ff", sw=46, extra=f'opacity="0.4" filter="{s.blur(14)}"')
    s.path("M80,180 Q300,260 470,350", stroke="#e8f8ff", sw=14, extra=f'filter="{s.glow(5)}"')
    s.path("M560,330 Q720,180 940,140", stroke="#7ad0ff", sw=30, extra=f'opacity="0.35" filter="{s.blur(10)}" stroke-dasharray="40 26"')
    bloom(s, 520, 360, 180, "#a0e0ff", 0.6)
    rays(s, 520, 360, "#e8f8ff", 12, 2, length=320, alpha=0.4)
    s.path(smooth([(640, 160), (780, 200), (780, 420), (680, 600), (580, 420), (580, 200)], k=0.3), lin(s, METAL, 0, 0, 0.5, 1), INK, 12)
    s.path(smooth([(640, 200), (745, 230), (745, 410), (680, 550), (615, 410), (615, 230)], k=0.3), lin(s, [(0, "#4a6aa0"), (1, "#1a2a50")]),
           INK, 6)
    emblem(s, "reflect", 680, 370, 70, "#bfe4ff")
    for i in range(10):
        a = math.radians(140 + i * 16)
        x, y = 520 + math.cos(a) * 140, 360 + math.sin(a) * 140
        s.path(star(round(x, 1), round(y, 1), 16, 4, 4), "#e8f8ff", stroke=None, extra=f'filter="{s.glow(3)}"')
    vignette(s)


def devour(s):
    """Пожрать: распахнутая волчья пасть с клыками заполняет кадр, в глубине — алый отсвет."""
    s.path(f"M0,0 H{W} V{H} H0 Z", s.rad([(0, "#5a0a10"), (0.5, "#2a0408"), (1, "#080204")], 0.5, 0.5, 0.7), stroke=None)
    upper = [(40, 80), (CX, 40), (960, 80), (940, 280), (CX + 280, 330), (CX, 350), (CX - 280, 330), (60, 280)]
    lower = [(60, 500), (CX - 280, 430), (CX, 410), (CX + 280, 430), (940, 500), (960, 720), (CX, 760), (40, 720)]
    s.path(smooth(upper, k=0.4), lin(s, [(0, "#4a3a34"), (1, "#24181a")]), INK, 12)
    s.path(smooth(lower, k=0.4), lin(s, [(0, "#3a2a26"), (1, "#140c0c")]), INK, 12)
    s.ellipse(CX, 390, 340, 70, "#1a0204")
    bloom(s, CX, 390, 160, "#ff2a2a", 0.45)
    s.path(smooth([(CX - 330, 300), (CX, 330), (CX + 330, 300), (CX + 300, 340), (CX, 360), (CX - 300, 340)], k=0.5), "#a02a3a", stroke=None)
    s.path(smooth([(CX - 330, 470), (CX, 440), (CX + 330, 470), (CX + 300, 430), (CX, 420), (CX - 300, 430)], k=0.5), "#a02a3a", stroke=None)
    for i in range(9):
        t = (i - 4) / 4
        x = CX + t * 330
        yu = 325 - abs(t) ** 2 * 20
        h = 70 if abs(i - 4) in (3, 2) and i in (1, 7) else 44
        if i in (1, 7):
            h = 110
        s.path(poly([(x - 18, yu), (x + 18, yu), (x, yu + h)]), lin(s, BONE), INK, 5)
        yl = 445 + abs(t) ** 2 * 20
        s.path(poly([(x - 15 + 20, yl), (x + 15 + 20, yl), (x + 20, yl - h * 0.8)]), lin(s, BONE), INK, 5)
    for dx in (-260, 260):
        s.eye(CX + dx, 160, 18, "#ffb02a")
    drops(s, "#c01a2a", [(CX - 120, 520), (CX + 150, 540), (CX + 30, 600)], 12)
    vignette(s, 0.8)


PORTRAITS = {
    "blade_spirit": blade_spirit, "call_spirit_knight": call_spirit_knight, "conjure_abyss_guardian": conjure_abyss_guardian,
    "lesser_ward": lesser_ward, "raise_dead": raise_dead, "blood_lord_stance": blood_lord_stance, "storm_call": storm_call,
    "toxic_coating": toxic_coating, "bound_blade": bound_blade, "sparks": sparks, "firebolt": firebolt, "blizzard": blizzard,
    "terror": terror, "poison_spray": poison_spray, "herald_stance_card": herald_stance_card, "whirlwind": whirlwind,
    "miasma": miasma, "focused_aim": focused_aim, "rebound": rebound, "devour": devour,
}


# ───────────────────────── сборка ─────────────────────────

def build(key: str) -> Svg:
    s = Svg(W, H)
    s.open(f'filter="{s.grain(0.12, 0.8, sum(map(ord, key)) % 97)}"')
    PORTRAITS[key](s)
    s.close()
    return s


def outputs():
    for key in sorted(PORTRAITS):
        yield key, os.path.join(OUT_SVG, key + ".svg"), os.path.join(OUT_IMG, key + ".png"), os.path.join(OUT_IMG, "big", key + ".png")


def png_size(path):
    with open(path, "rb") as f:
        head = f.read(24)
    return struct.unpack(">II", head[16:24])


def finish(raw: str, small: str, big: str):
    """RGB без альфы (портрет — прямоугольник), 256 цветов с дизерингом: ~4× меньше полноцветного PNG."""
    from PIL import Image
    im = Image.open(raw).convert("RGB")
    im.quantize(256, method=Image.Quantize.MEDIANCUT, dither=Image.Dither.FLOYDSTEINBERG).save(big, optimize=True)
    sm = im.resize((SW_, SH_), Image.LANCZOS)
    sm.quantize(256, method=Image.Quantize.MEDIANCUT, dither=Image.Dither.FLOYDSTEINBERG).save(small, optimize=True)


def check() -> int:
    bad = []
    for key, svg, small, big in outputs():
        if not os.path.exists(svg) or open(svg, encoding="utf-8").read() != build(key).render():
            bad.append(f"{os.path.relpath(svg, ROOT)}: не совпадает с генератором (запустите с --render)")
        for path, size in ((small, (SW_, SH_)), (big, (W, H))):
            if not os.path.exists(path):
                bad.append(f"{os.path.relpath(path, ROOT)}: нет картинки")
            elif png_size(path) != size:
                bad.append(f"{os.path.relpath(path, ROOT)}: размер {png_size(path)}, нужно {size[0]}×{size[1]}")
    for line_ in bad:
        print("✗", line_)
    print(f"свои портреты карт (M7 п. 6): {len(PORTRAITS)}, ошибок: {len(bad)}")
    return 1 if bad else 0


def main(argv) -> int:
    if "--check" in argv:
        return check()
    only = [a for a in argv[1:] if not a.startswith("--")]
    os.makedirs(OUT_SVG, exist_ok=True)
    pairs, jobs = [], []
    tmp = tempfile.mkdtemp()
    for key, svg, small, big in outputs():
        if only and key not in only:
            continue
        with open(svg, "w", encoding="utf-8") as f:
            f.write(build(key).render())
        raw = os.path.join(tmp, key + ".png")
        pairs += [svg, raw]
        jobs.append((raw, small, big))
    print(f"SVG: {len(jobs)} → {os.path.relpath(OUT_SVG, ROOT)}")
    if "--render" not in argv:
        return 0
    env = dict(os.environ, NODE_PATH=os.environ.get("NODE_PATH", "/tmp/r/node_modules"))
    subprocess.run(["node", os.path.join(ROOT, "tools", "art", "render_svg.mjs"), *pairs], check=True, env=env)
    os.makedirs(os.path.join(OUT_IMG, "big"), exist_ok=True)
    for job in jobs:
        finish(*job)
    print(f"PNG: {len(jobs)} × 2 → {os.path.relpath(OUT_IMG, ROOT)}")
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv))
