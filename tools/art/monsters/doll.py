"""Кукла героя (M6 п. 4, docs/design/07 §8): оружие и щит в руках → art/ui/doll/{key}.svg + {key}_metal.svg.

Каждый предмет — два слоя одного холста: основа (рукоять, древко, кожа, тетива — свои цвета) и металл (клинок, гарда,
обух, обод щита) в светло-сером, который игра красит в цвет материала (Equipment/PaperDoll.cs, `PaperDollRules.MaterialColor`):
железо, сталь, серебро, обсидиан, драконья кость, вечный лёд, Бездонное.

Предмет нарисован вертикально, остриём вверх; точка хвата — `PIVOTS` (её же знает `PaperDollRules.Pivot`), наклон в руке
задаёт код. Холст общий — DOLL_W×DOLL_H.
"""
from __future__ import annotations

from svgkit import INK, Svg, poly, smooth

DOLL_W, DOLL_H = 140, 260
GRIP = (70, 190)
CENTER = (70, 130)
PIVOTS = {"shield": CENTER, "bow": CENTER, "crossbow": CENTER}

WOOD = ("#c08a52", "#7a4e26")
LEATHER = ("#6a4a32", "#2e1e12")
METAL = [(0, "#ffffff"), (0.5, "#e2e6ea"), (1, "#a8b0b8")]
GEM = ("#ffe9a8", "#c8902a")


def _pair():
    return Svg(DOLL_W, DOLL_H), Svg(DOLL_W, DOLL_H)


def _metal(s: Svg, x2=1, y2=0.2):
    return s.lin(METAL, 0, 0, x2, y2)


def _wood(s: Svg):
    return s.lin([(0, WOOD[0]), (1, WOOD[1])], 0, 0, 1, 0)


def _grip(b: Svg, x, y0, y1, w=9):
    b.path(poly([(x - w, y0), (x + w, y0), (x + w, y1), (x - w, y1)]), b.lin([(0, LEATHER[0]), (1, LEATHER[1])], 0, 0, 1, 0), sw=3.5)
    for y in range(int(y0) + 6, int(y1) - 2, 8):
        b.path(f"M{x - w},{y} L{x + w},{y + 5}", stroke="#1a120a", sw=2)


def _pommel(m: Svg, x, y, r=9):
    m.circle(x, y, r, _metal(m), INK, 3)


def sword():
    b, m = _pair()
    x = 70
    _grip(b, x, 172, 214)
    # клинок с долом
    m.path(poly([(x - 11, 160), (x - 10, 34), (x, 10), (x + 10, 34), (x + 11, 160)]), _metal(m), sw=4)
    m.path(f"M{x},{150} L{x},{36}", stroke="#8a929a", sw=3)
    # гарда
    m.path(poly([(x - 38, 158), (x + 38, 158), (x + 34, 172), (x - 34, 172)]), _metal(m, 0, 1), sw=3.5)
    m.circle(x, 165, 5, m.rad([(0, GEM[0]), (1, GEM[1])]), INK, 2)
    _pommel(m, x, 222)
    return b, m


def dagger():
    b, m = _pair()
    x = 70
    _grip(b, x, 176, 210, 8)
    m.path(poly([(x - 10, 168), (x - 8, 104), (x, 82), (x + 9, 104), (x + 10, 168)]), _metal(m), sw=4)
    m.path(f"M{x},{160} L{x},{106}", stroke="#8a929a", sw=2.5)
    m.path(poly([(x - 24, 166), (x + 24, 166), (x + 20, 178), (x - 20, 178)]), _metal(m, 0, 1), sw=3)
    _pommel(m, x, 216, 7)
    return b, m


def axe():
    b, m = _pair()
    x = 70
    b.path(poly([(x - 7, 30), (x + 7, 30), (x + 8, 228), (x - 8, 228)]), _wood(b), sw=3.5)
    _grip(b, x, 176, 214, 9)
    # полукруглое лезвие вперёд (вправо) и обух назад
    m.path(smooth([(x + 4, 46), (x + 40, 30), (x + 62, 56), (x + 66, 90), (x + 56, 118), (x + 30, 112), (x + 4, 96)]), _metal(m), sw=4)
    m.path(smooth([(x + 54, 40), (x + 66, 70), (x + 60, 108)], closed=False), stroke="#ffffff", sw=3)
    m.path(poly([(x - 4, 52), (x - 26, 58), (x - 26, 86), (x - 4, 92)]), _metal(m, 0, 1), sw=3.5)
    return b, m


def mace():
    b, m = _pair()
    x = 70
    b.path(poly([(x - 7, 70), (x + 7, 70), (x + 7, 222), (x - 7, 222)]), _wood(b), sw=3.5)
    _grip(b, x, 176, 214, 9)
    # граненое навершие с перьями
    for dx in (-26, -14, 0, 14, 26):
        m.path(poly([(x + dx * 0.4 - 6, 42), (x + dx, 30 + abs(dx) * 0.5), (x + dx * 1.2, 66), (x + dx * 0.4 + 6, 86)]),
               _metal(m), sw=3)
    m.path(smooth([(x - 20, 40), (x + 20, 40), (x + 24, 70), (x + 14, 92), (x - 14, 92), (x - 24, 70)]), _metal(m), sw=4)
    m.circle(x, 24, 7, _metal(m), INK, 3)
    m.path(poly([(x - 12, 92), (x + 12, 92), (x + 10, 104), (x - 10, 104)]), _metal(m, 0, 1), sw=3)
    _pommel(m, x, 222, 8)
    return b, m


def spear():
    b, m = _pair()
    x = 70
    b.path(poly([(x - 6, 50), (x + 6, 50), (x + 6, 252), (x - 6, 252)]), _wood(b), sw=3.5)
    _grip(b, x, 176, 206, 8)
    # кисть под наконечником
    b.path(smooth([(x - 6, 62), (x - 16, 80), (x - 10, 92), (x, 72), (x + 10, 92), (x + 16, 80), (x + 6, 62)]), "#b02a2a", sw=2.5)
    m.path(poly([(x - 12, 58), (x - 10, 30), (x, 4), (x + 10, 30), (x + 12, 58)]), _metal(m), sw=4)
    m.path(poly([(x - 9, 56), (x + 9, 56), (x + 8, 68), (x - 8, 68)]), _metal(m, 0, 1), sw=3)
    return b, m


def greatsword():
    b, m = _pair()
    x = 70
    _grip(b, x, 168, 226, 10)
    m.path(poly([(x - 16, 158), (x - 15, 24), (x, 2), (x + 15, 24), (x + 16, 158)]), _metal(m), sw=4.5)
    m.path(f"M{x},{150} L{x},{26}", stroke="#8a929a", sw=4)
    m.path(poly([(x - 54, 154), (x + 54, 154), (x + 48, 170), (x - 48, 170)]), _metal(m, 0, 1), sw=4)
    for side in (-1, 1):
        m.circle(x + side * 54, 162, 7, _metal(m), INK, 3)
    _pommel(m, x, 234, 11)
    return b, m


def bow():
    b, m = _pair()
    cx, cy = CENTER
    # дуга лука вперёд (вправо), тетива сзади
    b.path(f"M{cx - 4},{20} Q{cx + 74},{cy} {cx - 4},{240}", stroke=INK, sw=13)
    b.path(f"M{cx - 4},{20} Q{cx + 74},{cy} {cx - 4},{240}", stroke=WOOD[0], sw=7)
    b.path(f"M{cx - 4},{20} L{cx - 4},{240}", stroke="#f2e6c8", sw=2)
    _grip(b, cx + 34, cy - 18, cy + 18, 8)
    # металлические накладки на концах
    for y, sign in ((26, 1), (234, -1)):
        m.path(poly([(cx - 10, y - 6 * sign), (cx + 6, y - 6 * sign), (cx + 10, y + 12 * sign), (cx - 4, y + 12 * sign)]), _metal(m), sw=3)
    return b, m


def crossbow():
    b, m = _pair()
    cx, cy = CENTER
    # ложе вертикально, плечи поперёк
    b.path(poly([(cx - 10, 40), (cx + 10, 40), (cx + 12, 220), (cx - 12, 220)]), _wood(b), sw=3.5)
    b.path(poly([(cx - 12, 170), (cx + 12, 170), (cx + 26, 230), (cx - 2, 236)]), _wood(b), sw=3.5)
    b.path(f"M{cx - 56},{86} L{cx},{118} L{cx + 56},{86}", stroke="#f2e6c8", sw=2)
    m.path(smooth([(cx - 60, 80), (cx - 30, 56), (cx, 50), (cx + 30, 56), (cx + 60, 80), (cx + 56, 90), (cx, 66), (cx - 56, 90)]),
           _metal(m), sw=3.5)
    m.path(poly([(cx - 4, 30), (cx + 4, 30), (cx + 4, 64), (cx - 4, 64)]), _metal(m, 0, 1), sw=2.5)
    m.path(poly([(cx - 7, 140), (cx + 7, 140), (cx + 7, 160), (cx - 7, 160)]), _metal(m), sw=2.5)
    return b, m


def staff():
    b, m = _pair()
    x = 70
    b.path(smooth([(x - 6, 60), (x + 6, 60), (x + 8, 140), (x + 6, 252), (x - 6, 252), (x - 8, 140)]), _wood(b), sw=3.5)
    _grip(b, x, 170, 204, 8)
    # кристалл в оправе
    b.path(poly([(x, 6), (x + 16, 30), (x, 58), (x - 16, 30)]), b.lin([(0, "#e8f4ff"), (1, "#7aa8ff")]), sw=3,
           extra=f'filter="{b.glow(2.5)}"')
    m.path(smooth([(x - 22, 22), (x - 14, 60), (x, 70), (x + 14, 60), (x + 22, 22), (x + 12, 52), (x, 58), (x - 12, 52)]),
           _metal(m), sw=3)
    m.path(poly([(x - 9, 66), (x + 9, 66), (x + 8, 78), (x - 8, 78)]), _metal(m, 0, 1), sw=3)
    return b, m


def shield():
    b, m = _pair()
    cx, cy = CENTER
    pts = [(cx - 52, cy - 70), (cx + 52, cy - 70), (cx + 54, cy + 10), (cx + 30, cy + 62), (cx, cy + 84), (cx - 30, cy + 62), (cx - 54, cy + 10)]
    # щит целиком красится в материал (крест тоже — тёмно-красный под множителем цвета); основа — ремень за щитом
    b.path(f"M{cx - 58},{cy - 20} L{cx + 58},{cy + 20}", stroke=INK, sw=10)
    b.path(f"M{cx - 58},{cy - 20} L{cx + 58},{cy + 20}", stroke=LEATHER[0], sw=6)
    m.path(poly(pts), _metal(m, 0.6, 1), sw=5)
    inner = [(cx - 42, cy - 60), (cx + 42, cy - 60), (cx + 44, cy + 6), (cx + 24, cy + 52), (cx, cy + 70), (cx - 24, cy + 52), (cx - 44, cy + 6)]
    m.path(poly(inner), "none", "#8a929a", 3)
    m.path(poly([(cx - 8, cy - 58), (cx + 8, cy - 58), (cx + 8, cy + 66), (cx - 8, cy + 66)]), "#8a2a30", stroke=None, extra='opacity="0.85"')
    m.path(poly([(cx - 42, cy - 12), (cx + 42, cy - 12), (cx + 42, cy + 4), (cx - 42, cy + 4)]), "#8a2a30", stroke=None, extra='opacity="0.85"')
    m.circle(cx, cy - 4, 14, _metal(m), INK, 3)
    return b, m


DOLL = {
    "sword": sword, "dagger": dagger, "axe": axe, "mace": mace, "spear": spear, "greatsword": greatsword,
    "bow": bow, "crossbow": crossbow, "staff": staff, "shield": shield,
}
