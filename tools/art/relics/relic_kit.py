"""Набор для своих значков реликвий (M7 п. 6): материалы, знаки (эмблемы) и основы предметов.

Стиль — как у значков происхождений (M7 п. 3, draw_origins.py) и ванильных реликвий: толстый тёмный контур, градиент
света сверху-слева, свечение у магического. Холст 256×256, центр 128. Значок собирается из основы (амулет, кольцо,
облачение, кристалл, книга…) и знака: у групп реликвий (10 богов, 13 сущностей, 5 школ…) — общая основа и свой знак и
цвет, чтобы группа читалась как группа, а реликвии в ней — различались.
Знаки рисуются в единичных координатах (−1…1) и масштабируются: e_*(s, x, y, r, fill).
"""
from __future__ import annotations

import math

from svgkit import INK, Svg, arc_pts, poly, smooth, star

C = 128

METAL = [(0, "#ffffff"), (0.45, "#d6dce2"), (1, "#7e8892")]
IRON = [(0, "#c8ccd2"), (0.5, "#8a9098"), (1, "#4a5058")]
STEEL = [(0, "#f4f8ff"), (0.4, "#b8c4d4"), (1, "#5e6a7a")]
SILVER = [(0, "#ffffff"), (0.5, "#e2e6f0"), (1, "#9aa2b8")]
GOLD = [(0, "#fff6c8"), (0.45, "#f0c050"), (1, "#9a6a1a"),]
BRONZE = [(0, "#ffd8a0"), (0.5, "#c88a48"), (1, "#6e4420")]
RUST = [(0, "#e0a070"), (0.5, "#a0583a"), (1, "#5a2e1e")]
WOOD = [(0, "#d09a5e"), (1, "#6e4420")]
DARKWOOD = [(0, "#8a5a3a"), (1, "#3a2216")]
LEATHER = [(0, "#9a6a46"), (1, "#4a2e1a")]
DARKLEATHER = [(0, "#5a4a44"), (1, "#221a18")]
GREENLEATHER = [(0, "#7a8a4a"), (1, "#2e3a1a")]
BONE = [(0, "#fffaf0"), (1, "#c8b898")]
ICE = [(0, "#ffffff"), (0.5, "#bfeaff"), (1, "#4a8ac0")]
OBSIDIAN = [(0, "#6a5a7a"), (0.5, "#2a2232"), (1, "#0e0a12")]
STONE = [(0, "#9a96a0"), (0.5, "#5e5a66"), (1, "#2e2a34")]
CLOTH = {
    "red": [(0, "#ff8a7a"), (1, "#8a1a22")], "blue": [(0, "#8ac0ff"), (1, "#1a3a8a")], "white": [(0, "#ffffff"), (1, "#a8b8d0")],
    "purple": [(0, "#c8a0ff"), (1, "#3a1a6a")], "green": [(0, "#a8d878"), (1, "#2a4a1a")], "black": [(0, "#5a5266"), (1, "#141018")],
    "brown": [(0, "#c8a07a"), (1, "#4a2e1a")], "gold": [(0, "#fff0a0"), (1, "#9a6a1a")], "teal": [(0, "#9af0e0"), (1, "#1a5a5a")],
    "orange": [(0, "#ffc080"), (1, "#8a3a10")], "pink": [(0, "#ffc8e8"), (1, "#8a2a6a")], "grey": [(0, "#d8dce4"), (1, "#4a505a")],
}


# ───────────────────────── общие помощники ─────────────────────────

def g(s: Svg, stops, x2=0.4, y2=1):
    return s.lin(stops, 0, 0, x2, y2)


def U(pts, x, y, r):
    return [(round(x + px * r, 1), round(y + py * r, 1)) for px, py in pts]


def rot(pts, cx, cy, deg):
    a = math.radians(deg)
    ca, sa = math.cos(a), math.sin(a)
    return [(round(cx + (px - cx) * ca - (py - cy) * sa, 1), round(cy + (px - cx) * sa + (py - cy) * ca, 1)) for px, py in pts]


def sw(r, k=1.0):
    return round(max(2.0, r / 13) * k, 1)


def glow(s: Svg, x, y, r, color, alpha=0.55):
    """Мягкий ореол за предметом (слабее порога силуэта — обводка и контур подсветки его не обнимают)."""
    s.circle(x, y, round(r * 0.8, 1), color, extra=f'opacity="{round(alpha * 0.6, 2)}" filter="{s.blur(r * 0.3)}"')


def gem(s: Svg, x, y, r, light, dark, facet=True):
    s.circle(x, y, r, s.rad([(0, light), (1, dark)], 0.35, 0.35, 0.7), INK, sw(r * 2.2))
    if facet:
        s.path(poly(U([(-0.55, -0.1), (0, -0.62), (0.55, -0.1), (0, 0.55)], x, y, r)), stroke="#ffffff", sw=1.5, extra='opacity="0.45"')
    s.circle(round(x - r * 0.35, 1), round(y - r * 0.35, 1), round(r * 0.26, 1), "#ffffff", extra='opacity="0.85"')


def line(s: Svg, pts, color, width, under=True):
    d = poly(pts, closed=False)
    if under:
        s.path(d, stroke=INK, sw=width + 6)
    s.path(d, stroke=color, sw=width)


def bolt_pts(x, y, k=1.0):
    return [(x + 6 * k, y - 34 * k), (x - 14 * k, y + 4 * k), (x - 1 * k, y + 4 * k), (x - 8 * k, y + 34 * k), (x + 16 * k, y - 6 * k),
            (x + 3 * k, y - 6 * k)]


def bolt(s: Svg, x, y, k=1.0, color="#fff27a"):
    s.path(poly(bolt_pts(x, y, k)), color, INK, 3, extra=f'filter="{s.glow(2)}"')


def sparkle(s: Svg, x, y, r, color="#ffffff"):
    s.path(star(x, y, r, r * 0.22, 4), color, extra=f'filter="{s.glow(r * 0.15)}"')


# ═══════════════════════════ знаки (эмблемы) ═══════════════════════════
# Каждый: e(s, x, y, r, fill) — фигура в круге радиуса r, заливка fill, контур INK.

def e_anvil(s, x, y, r, fill):
    pts = [(-1, -0.55), (0.62, -0.55), (1, -0.72), (0.9, -0.32), (0.42, -0.12), (0.28, 0.28), (0.62, 0.62), (-0.62, 0.62),
           (-0.28, 0.28), (-0.42, -0.12), (-1, -0.28)]
    s.path(poly(U(pts, x, y, r)), fill, INK, sw(r))
    s.path(poly(U([(-0.9, -0.42), (0.6, -0.42)], x, y, r), closed=False), stroke="#ffffff", sw=sw(r, 0.6), extra='opacity="0.6"')


def e_flame(s, x, y, r, fill, inner="#fff2a0"):
    pts = [(0, -1), (0.32, -0.5), (0.62, -0.05), (0.6, 0.45), (0.28, 0.85), (-0.28, 0.85), (-0.6, 0.45), (-0.58, 0.0), (-0.3, -0.22),
           (-0.12, -0.55)]
    s.path(smooth(U(pts, x, y, r), k=0.7), fill, INK, sw(r), extra=f'filter="{s.glow(r * 0.06)}"')
    pts2 = [(0, -0.3), (0.28, 0.1), (0.3, 0.5), (0, 0.72), (-0.3, 0.5), (-0.26, 0.15)]
    s.path(smooth(U(pts2, x, y, r), k=0.7), inner)


def e_heart(s, x, y, r, fill):
    d = (f"M{x},{y + r * 0.85} C{x - r * 1.25},{y - r * 0.05} {x - r * 0.75},{y - r * 1.05} {x},{y - r * 0.4} "
         f"C{x + r * 0.75},{y - r * 1.05} {x + r * 1.25},{y - r * 0.05} {x},{y + r * 0.85} Z")
    s.path(d, fill, INK, sw(r))


def e_shield_heart(s, x, y, r, fill):
    pts = [(-0.82, -0.85), (0.82, -0.85), (0.82, 0.05), (0, 0.98), (-0.82, 0.05)]
    s.path(smooth(U(pts, x, y, r), k=0.25), fill, INK, sw(r))
    e_heart(s, x, y - r * 0.08, r * 0.42, "#ff5a6a")


def e_mirror(s, x, y, r, fill):
    s.path(poly(U([(-0.14, 0.25), (0.14, 0.25), (0.12, 1), (-0.12, 1)], x, y, r)), "#c8a050", INK, sw(r))
    s.ellipse(x, round(y - r * 0.3, 1), round(r * 0.62, 1), round(r * 0.68, 1), "#c8a050", INK, sw(r))
    s.ellipse(x, round(y - r * 0.3, 1), round(r * 0.46, 1), round(r * 0.52, 1), fill, INK, sw(r, 0.6))
    s.path(poly(U([(-0.25, -0.55), (0.05, -0.75)], x, y, r), closed=False), stroke="#ffffff", sw=sw(r, 0.8), extra='opacity="0.9"')


def e_scroll(s, x, y, r, fill):
    s.path(poly(U([(-0.6, -0.68), (0.6, -0.68), (0.6, 0.68), (-0.6, 0.68)], x, y, r)), fill, INK, sw(r))
    for yy in (-0.72, 0.72):
        s.path(poly(U([(-0.82, yy - 0.14), (0.82, yy - 0.14), (0.82, yy + 0.14), (-0.82, yy + 0.14)], x, y, r)), "#c8a070", INK, sw(r))
    for yy in (-0.3, -0.05, 0.2):
        s.path(poly(U([(-0.38, yy), (0.38, yy)], x, y, r), closed=False), stroke="#6a4a2a", sw=sw(r, 0.6))


def e_sun(s, x, y, r, fill):
    s.path(star(x, y, r, r * 0.6, 12), fill, INK, sw(r, 0.8))
    s.circle(x, y, round(r * 0.48, 1), "#fff6c8", INK, sw(r, 0.8))


def e_sun_arrow(s, x, y, r, fill):
    e_sun(s, x, y, r * 0.78, fill)
    a, b = U([(-0.95, 0.95)], x, y, r)[0], U([(0.8, -0.8)], x, y, r)[0]
    line(s, [a, b], "#7a4a26", sw(r, 0.9))
    s.path(poly(U([(0.98, -0.98), (0.5, -0.82), (0.82, -0.5)], x, y, r)), "#e8e8f0", INK, sw(r, 0.6))
    s.path(poly(U([(-0.95, 0.95), (-0.95, 0.62), (-0.75, 0.75), (-0.62, 0.95)], x, y, r)), "#e04040", INK, sw(r, 0.5))


def e_gate(s, x, y, r, fill):
    pts = [(-0.75, 0.92), (-0.75, -0.2)] + [(px, py) for px, py in
                                             ((math.cos(math.radians(a)) * 0.75, -0.2 - math.sin(math.radians(a)) * 0.75)
                                              for a in range(180, -1, -15))] + [(0.75, 0.92)]
    s.path(poly(U(pts, x, y, r)), fill, INK, sw(r))
    s.circle(x, round(y + r * 0.05, 1), round(r * 0.17, 1), INK)
    s.path(poly(U([(-0.08, 0.1), (0.08, 0.1), (0.14, 0.5), (-0.14, 0.5)], x, y, r)), INK)


def e_crown(s, x, y, r, fill):
    pts = [(-0.9, 0.55), (-0.95, -0.45), (-0.45, 0.05), (0, -0.7), (0.45, 0.05), (0.95, -0.45), (0.9, 0.55)]
    s.path(poly(U(pts, x, y, r)), fill, INK, sw(r))
    for px in (-0.95, 0, 0.95):
        s.circle(*U([(px, -0.6 if px else -0.82)], x, y, r)[0], round(r * 0.11, 1), "#ffffff", INK, sw(r, 0.4))


def e_crown_bolt(s, x, y, r, fill):
    e_crown(s, x, y + r * 0.25, r * 0.8, fill)
    s.path(poly(bolt_pts(x, y + r * 0.12, r / 70)), "#fff27a", INK, sw(r, 0.6), extra=f'filter="{s.glow(r * 0.05)}"')


def e_wind(s, x, y, r, fill):
    for k, (oy, rr) in enumerate(((-0.45, 0.42), (0.05, 0.55), (0.55, 0.36))):
        pts = U([(-0.95, oy), (0.2, oy)], x, y, r)
        cx, cy = x + 0.2 * r, y + (oy - rr * 0.5) * r
        d = f"M{pts[0][0]},{pts[0][1]} L{pts[1][0]},{pts[1][1]} A{rr * r * 0.5:.1f},{rr * r * 0.5:.1f} 0 1 0 {cx:.1f},{cy:.1f}"
        s.path(d, stroke=INK, sw=sw(r, 2.2))
        s.path(d, stroke=fill, sw=sw(r, 1.2))


def e_hourglass(s, x, y, r, fill, wings=True):
    if wings:
        for side in (-1, 1):
            pts = [(side * 0.3, -0.1), (side * 1.0, -0.75), (side * 0.95, -0.35), (side * 1.02, -0.1), (side * 0.8, 0.15), (side * 0.3, 0.2)]
            s.path(smooth(U(pts, x, y, r), k=0.5), "#ffffff", INK, sw(r, 0.8))
            for k in range(3):
                s.path(poly(U([(side * (0.45 + k * 0.16), -0.05 - k * 0.12), (side * (0.55 + k * 0.17), 0.1)], x, y, r), closed=False),
                       stroke="#a0a8c0", sw=sw(r, 0.4))
    s.path(poly(U([(-0.42, -0.85), (0.42, -0.85), (0.08, 0), (0.42, 0.85), (-0.42, 0.85), (-0.08, 0)], x, y, r)), fill, INK, sw(r))
    s.path(poly(U([(-0.22, 0.75), (0.22, 0.75), (0, 0.4)], x, y, r)), "#f0d090")
    for yy in (-0.9, 0.9):
        s.path(poly(U([(-0.55, yy - 0.08), (0.55, yy - 0.08), (0.55, yy + 0.08), (-0.55, yy + 0.08)], x, y, r)), "#a0703a", INK, sw(r, 0.6))


def e_dagger(s, x, y, r, fill):
    pts = rot(U([(-0.13, 0.25), (-0.1, -0.7), (0, -1), (0.1, -0.7), (0.13, 0.25)], x, y, r), x, y, 30)
    s.path(poly(pts), fill, INK, sw(r))
    g1 = rot(U([(-0.45, 0.25), (0.45, 0.25), (0.45, 0.38), (-0.45, 0.38)], x, y, r), x, y, 30)
    s.path(poly(g1), "#c8a050", INK, sw(r, 0.7))
    h = rot(U([(-0.09, 0.38), (0.09, 0.38), (0.09, 0.85), (-0.09, 0.85)], x, y, r), x, y, 30)
    s.path(poly(h), "#6a3a2a", INK, sw(r, 0.7))


def e_sword(s, x, y, r, fill):
    s.path(poly(U([(-0.12, 0.35), (-0.12, -0.75), (0, -1), (0.12, -0.75), (0.12, 0.35)], x, y, r)), fill, INK, sw(r))
    s.path(poly(U([(-0.55, 0.35), (0.55, 0.35), (0.55, 0.5), (-0.55, 0.5)], x, y, r)), "#c8a050", INK, sw(r, 0.7))
    s.path(poly(U([(-0.09, 0.5), (0.09, 0.5), (0.09, 0.85), (-0.09, 0.85)], x, y, r)), "#6a3a2a", INK, sw(r, 0.7))
    s.circle(x, round(y + r * 0.92, 1), round(r * 0.12, 1), "#c8a050", INK, sw(r, 0.5))


def e_axe(s, x, y, r, fill):
    line(s, U([(-0.15, 1), (0.15, -0.95)], x, y, r), "#9a6a3a", sw(r, 0.9))
    s.path(smooth(U([(0.05, -0.75), (0.8, -0.95), (0.95, -0.3), (0.75, 0.2), (0.05, -0.05)], x, y, r), k=0.35), fill, INK, sw(r))


def e_hammer(s, x, y, r, fill):
    line(s, U([(0, 1), (0, -0.35)], x, y, r), "#9a6a3a", sw(r, 0.9))
    s.path(poly(U([(-0.75, -0.95), (0.75, -0.95), (0.75, -0.35), (-0.75, -0.35)], x, y, r)), fill, INK, sw(r))


def e_skull(s, x, y, r, fill):
    pts = [(-0.75, -0.1), (-0.7, -0.65), (0, -0.95), (0.7, -0.65), (0.75, -0.1), (0.45, 0.3), (0.42, 0.7), (-0.42, 0.7), (-0.45, 0.3)]
    s.path(smooth(U(pts, x, y, r), k=0.5), fill, INK, sw(r))
    for px in (-0.32, 0.32):
        s.ellipse(*U([(px, -0.12)], x, y, r)[0], round(r * 0.22, 1), round(r * 0.26, 1), INK)
    s.path(poly(U([(0, 0.15), (-0.1, 0.35), (0.1, 0.35)], x, y, r)), INK)
    for px in (-0.2, 0, 0.2):
        s.path(poly(U([(px, 0.5), (px, 0.7)], x, y, r), closed=False), stroke=INK, sw=sw(r, 0.5))


def e_laurel(s, x, y, r, fill):
    for side in (-1, 1):
        pts = arc_pts(x, y, r * 0.8, r * 0.85, 90 + side * 20, 90 + side * 160, 8)
        s.path(poly(pts, closed=False), stroke=INK, sw=sw(r, 0.9))
        for k, (px, py) in enumerate(pts[1:]):
            a = math.atan2(py - y, px - x)
            lx, ly = px + math.cos(a) * r * 0.16, py + math.sin(a) * r * 0.16
            s.ellipse(round(lx, 1), round(ly, 1), round(r * 0.2, 1), round(r * 0.09, 1), fill, INK, sw(r, 0.45),
                      extra=f'transform="rotate({round(math.degrees(a) + side * 50)} {round(lx, 1)} {round(ly, 1)})"')
    s.path(star(x, y, r * 0.38, r * 0.16, 5), "#fff6c8", INK, sw(r, 0.6))


def e_elements(s, x, y, r, fill):
    for k, col in enumerate(("#ff7a2e", "#7ad0ff", "#c9a8ff", "#8ad04a")):
        a = math.radians(-90 + k * 90)
        cx, cy = x + math.cos(a) * r * 0.55, y + math.sin(a) * r * 0.55
        s.circle(round(cx, 1), round(cy, 1), round(r * 0.36, 1), col, INK, sw(r, 0.7), extra=f'filter="{s.glow(r * 0.04)}"')
    s.circle(x, y, round(r * 0.22, 1), "#ffffff", INK, sw(r, 0.6))


def e_eye(s, x, y, r, fill):
    d = f"M{x - r},{y} Q{x},{y - r * 0.95} {x + r},{y} Q{x},{y + r * 0.95} {x - r},{y} Z"
    s.path(d, "#fffaf0", INK, sw(r))
    s.circle(x, y, round(r * 0.42, 1), fill, INK, sw(r, 0.6))
    s.circle(x, y, round(r * 0.18, 1), INK)
    s.circle(round(x - r * 0.12, 1), round(y - r * 0.14, 1), round(r * 0.08, 1), "#ffffff")


def e_fist(s, x, y, r, fill):
    s.path(smooth(U([(-0.7, -0.35), (0.7, -0.35), (0.78, 0.25), (0.45, 0.62), (-0.45, 0.62), (-0.78, 0.25)], x, y, r), k=0.35), fill, INK, sw(r))
    for k in range(4):
        px = -0.52 + k * 0.35
        s.path(smooth(U([(px - 0.17, -0.3), (px - 0.15, -0.72), (px + 0.15, -0.72), (px + 0.17, -0.3)], x, y, r), closed=False, k=0.6),
               fill, INK, sw(r, 0.8))
    s.path(smooth(U([(-0.78, 0.0), (-0.95, -0.25), (-0.55, -0.15), (-0.2, 0.1)], x, y, r), closed=False, k=0.6), fill, INK, sw(r, 0.8))
    s.path(poly(U([(-0.5, 0.95), (0.5, 0.95), (0.45, 0.62), (-0.45, 0.62)], x, y, r)), "#e8d8b0", INK, sw(r, 0.7))


def e_arrow_star(s, x, y, r, fill):
    line(s, U([(-0.85, 0.85), (0.55, -0.55)], x, y, r), "#c8a070", sw(r, 0.8))
    s.path(poly(U([(0.85, -0.85), (0.3, -0.62), (0.62, -0.3)], x, y, r)), fill, INK, sw(r, 0.6))
    for px, py in ((-0.85, 0.85), (-0.7, 0.7)):
        s.path(poly(U([(px, py), (px - 0.25, py - 0.05), (px + 0.05, py - 0.25)], x, y, r)), "#ffffff", INK, sw(r, 0.4))
    s.path(star(*U([(0.45, 0.4)], x, y, r)[0], r * 0.42, r * 0.18, 4), "#ffffff", extra=f'filter="{s.glow(r * 0.06)}"')


def e_sun_cross(s, x, y, r, fill):
    s.circle(x, y, round(r * 0.62, 1), "none", INK, sw(r, 2.2))
    s.circle(x, y, round(r * 0.62, 1), "none", fill, sw(r, 1.1))
    s.path(poly(U([(-0.16, -1), (0.16, -1), (0.16, -0.16), (1, -0.16), (1, 0.16), (0.16, 0.16), (0.16, 1), (-0.16, 1), (-0.16, 0.16),
                   (-1, 0.16), (-1, -0.16), (-0.16, -0.16)], x, y, r)), fill, INK, sw(r, 0.8))


def e_feather(s, x, y, r, fill):
    pts = rot(U([(0, -1), (0.42, -0.45), (0.38, 0.25), (0, 0.75), (-0.38, 0.25), (-0.42, -0.45)], x, y, r), x, y, 25)
    s.path(smooth(pts, k=0.7), fill, INK, sw(r))
    spine = rot(U([(0, -0.9), (0, 1)], x, y, r), x, y, 25)
    s.path(poly(spine, closed=False), stroke=INK, sw=sw(r, 0.7))
    for k in range(4):
        a, b = rot(U([(0, -0.5 + k * 0.3), (0.32, -0.7 + k * 0.3)], x, y, r), x, y, 25)
        c, d = rot(U([(0, -0.5 + k * 0.3), (-0.32, -0.7 + k * 0.3)], x, y, r), x, y, 25)
        s.path(f"M{a[0]},{a[1]} L{b[0]},{b[1]} M{c[0]},{c[1]} L{d[0]},{d[1]}", stroke="#fff0c0", sw=sw(r, 0.4), extra='opacity="0.8"')


def e_reflect(s, x, y, r, fill):
    s.path(poly(U([(-0.9, 0.5), (0.9, 0.5), (0.9, 0.72), (-0.9, 0.72)], x, y, r)), "#e8eef8", INK, sw(r, 0.8))
    for pts in (((-0.85, -0.85), (-0.05, 0.4)), ((0.05, 0.4), (0.85, -0.85))):
        line(s, U(list(pts), x, y, r), fill, sw(r, 0.8))
    s.path(poly(U([(0.95, -0.98), (0.55, -0.85), (0.85, -0.55)], x, y, r)), fill, INK, sw(r, 0.5))


def e_spiral(s, x, y, r, fill):
    pts = []
    for i in range(48):
        t = i / 47
        a = t * math.tau * 1.6
        rr = r * (0.12 + 0.85 * t)
        pts.append((round(x + math.cos(a) * rr, 1), round(y + math.sin(a) * rr, 1)))
    s.path(poly(pts, closed=False), stroke=INK, sw=sw(r, 2.3))
    s.path(poly(pts, closed=False), stroke=fill, sw=sw(r, 1.2), extra=f'filter="{s.glow(r * 0.04)}"')
    ex, ey = pts[-1]
    s.path(poly([(ex + r * 0.2, ey - r * 0.25), (ex + r * 0.12, ey + r * 0.28), (ex - r * 0.3, ey + r * 0.02)]), fill, INK, sw(r, 0.5))


def e_pentacle(s, x, y, r, fill):
    s.circle(x, y, round(r * 0.92, 1), "none", INK, sw(r, 2))
    s.circle(x, y, round(r * 0.92, 1), "none", fill, sw(r, 0.9), extra=f'filter="{s.glow(r * 0.05)}"')
    pts = [(x + math.cos(math.radians(-90 + i * 144)) * r * 0.82, y + math.sin(math.radians(-90 + i * 144)) * r * 0.82) for i in range(5)]
    s.path(poly(pts), stroke=INK, sw=sw(r, 2))
    s.path(poly(pts), stroke=fill, sw=sw(r, 0.9), extra=f'filter="{s.glow(r * 0.05)}"')


def e_mask(s, x, y, r, fill):
    pts = [(-0.8, -0.75), (0.8, -0.75), (0.75, 0.05), (0.4, 0.7), (0, 0.9), (-0.4, 0.7), (-0.75, 0.05)]
    s.path(smooth(U(pts, x, y, r), k=0.45), fill, INK, sw(r))
    for side in (-1, 1):
        s.path(smooth(U([(side * 0.18, -0.3), (side * 0.38, -0.42), (side * 0.6, -0.3), (side * 0.38, -0.18)], x, y, r), k=0.6), INK)
    s.path(smooth(U([(-0.3, 0.32), (0, 0.48), (0.3, 0.32)], x, y, r), closed=False, k=0.6), stroke=INK, sw=sw(r, 0.8))


def e_life(s, x, y, r, fill):
    for side in (-1, 1):
        s.path(smooth(U([(side * 0.1, 0.9), (side * 0.75, 0.35), (side * 0.92, -0.25), (side * 0.55, -0.1), (side * 0.15, 0.4)], x, y, r), k=0.5),
               "#8ad04a", INK, sw(r, 0.8))
    s.path(poly(U([(-0.15, -0.85), (0.15, -0.85), (0.15, -0.35), (0.6, -0.35), (0.6, -0.05), (0.15, -0.05), (0.15, 0.55), (-0.15, 0.55),
                   (-0.15, -0.05), (-0.6, -0.05), (-0.6, -0.35), (-0.15, -0.35)], x, y, r)), fill, INK, sw(r, 0.8))


def e_tower(s, x, y, r, fill):
    pts = [(-0.55, 0.95), (-0.45, -0.5), (-0.62, -0.5), (-0.62, -0.95), (-0.36, -0.95), (-0.36, -0.75), (-0.12, -0.75), (-0.12, -0.95),
           (0.12, -0.95), (0.12, -0.75), (0.36, -0.75), (0.36, -0.95), (0.62, -0.95), (0.62, -0.5), (0.45, -0.5), (0.55, 0.95)]
    s.path(poly(U(pts, x, y, r)), fill, INK, sw(r))
    s.path(poly(U([(-0.18, 0.95), (-0.18, 0.55), (0, 0.4), (0.18, 0.55), (0.18, 0.95)], x, y, r)), INK)
    s.path(poly(U([(-0.06, -0.2), (0.06, -0.2), (0.06, 0.1), (-0.06, 0.1)], x, y, r)), INK)


def e_weight(s, x, y, r, fill):
    s.path(smooth(U([(-0.3, -0.62), (-0.3, -0.95), (0.3, -0.95), (0.3, -0.62)], x, y, r), closed=False, k=0.6), stroke=INK, sw=sw(r, 1.8))
    s.path(poly(U([(-0.5, -0.6), (0.5, -0.6), (0.9, 0.9), (-0.9, 0.9)], x, y, r)), fill, INK, sw(r))
    s.path(poly(U([(-0.25, 0.25), (0.25, 0.25)], x, y, r), closed=False), stroke="#ffffff", sw=sw(r, 0.7), extra='opacity="0.6"')


def e_candle(s, x, y, r, fill):
    s.path(poly(U([(-0.32, -0.15), (0.32, -0.15), (0.32, 0.85), (-0.32, 0.85)], x, y, r)), "#fff4dc", INK, sw(r))
    s.path(smooth(U([(-0.32, -0.15), (0.0, 0.05), (0.32, -0.15), (0.2, 0.3), (0.32, 0.35)], x, y, r), closed=False, k=0.6), stroke=INK, sw=sw(r, 0.5))
    s.path(smooth(U([(0, -1), (0.22, -0.55), (0.12, -0.28), (-0.12, -0.28), (-0.22, -0.55)], x, y, r), k=0.7), fill, INK, sw(r, 0.6),
           extra=f'filter="{s.glow(r * 0.08)}"')


def e_serpent(s, x, y, r, fill):
    pts = U([(0.55, -0.8), (-0.4, -0.75), (-0.5, -0.15), (0.45, 0.0), (0.45, 0.6), (-0.6, 0.8)], x, y, r)
    d = smooth(pts, closed=False, k=1)
    s.path(d, stroke=INK, sw=sw(r, 3.2))
    s.path(d, stroke=fill, sw=sw(r, 2.0))
    hx, hy = pts[0]
    s.ellipse(hx, hy, round(r * 0.24, 1), round(r * 0.17, 1), fill, INK, sw(r, 0.7))
    s.circle(round(hx + r * 0.05, 1), round(hy - r * 0.04, 1), round(r * 0.05, 1), INK)
    s.path(poly([(hx + r * 0.22, hy), (hx + r * 0.42, hy - r * 0.06), (hx + r * 0.36, hy + r * 0.02), (hx + r * 0.42, hy + r * 0.1)], closed=False),
           stroke="#e04050", sw=sw(r, 0.4))


def e_wisp(s, x, y, r, fill):
    pts = [(0, -0.9), (0.55, -0.45), (0.5, 0.2), (0.3, 0.55), (0.4, 0.95), (0.05, 0.7), (-0.15, 0.95), (-0.3, 0.6), (-0.55, 0.8), (-0.5, 0.2),
           (-0.55, -0.45)]
    s.path(smooth(U(pts, x, y, r), k=0.6), fill, INK, sw(r), extra=f'filter="{s.glow(r * 0.06)}" opacity="0.95"')
    for px in (-0.2, 0.2):
        s.ellipse(*U([(px, -0.3)], x, y, r)[0], round(r * 0.1, 1), round(r * 0.16, 1), INK)


def e_sponge(s, x, y, r, fill):
    pts = [(-0.8, -0.5), (-0.3, -0.85), (0.35, -0.75), (0.85, -0.35), (0.8, 0.4), (0.3, 0.85), (-0.4, 0.8), (-0.85, 0.3)]
    s.path(smooth(U(pts, x, y, r), k=0.6), fill, INK, sw(r))
    for px, py, rr in ((-0.35, -0.3, 0.16), (0.25, -0.4, 0.12), (0.35, 0.2, 0.18), (-0.25, 0.35, 0.13), (0.0, -0.02, 0.09), (-0.6, 0.0, 0.08)):
        s.circle(*U([(px, py)], x, y, r)[0], round(r * rr, 1), "#2a3a2a", INK, sw(r, 0.3))


def e_die(s, x, y, r, fill):
    s.path(poly(U([(0, -0.95), (0.85, -0.5), (0, -0.05), (-0.85, -0.5)], x, y, r)), "#ffffff", INK, sw(r))
    s.path(poly(U([(-0.85, -0.5), (0, -0.05), (0, 0.95), (-0.85, 0.5)], x, y, r)), "#d8dce8", INK, sw(r))
    s.path(poly(U([(0.85, -0.5), (0, -0.05), (0, 0.95), (0.85, 0.5)], x, y, r)), "#a8b0c4", INK, sw(r))
    for px, py in ((0, -0.5), (-0.55, -0.05), (-0.3, 0.45), (0.3, 0.1), (0.55, 0.55), (0.42, 0.32)):
        s.circle(*U([(px, py)], x, y, r)[0], round(r * 0.09, 1), fill)


def e_voice(s, x, y, r, fill):
    s.path(smooth(U([(-0.85, -0.2), (-0.35, -0.55), (0.0, -0.2), (0.0, 0.2), (-0.35, 0.55), (-0.85, 0.2)], x, y, r), k=0.5), fill, INK, sw(r))
    s.path(smooth(U([(-0.7, 0.0), (-0.35, -0.2), (-0.1, 0.0), (-0.35, 0.2)], x, y, r), k=0.5), INK)
    for k in range(3):
        rr = 0.35 + k * 0.25
        pts = arc_pts(x + r * 0.05, y, r * rr, r * rr, -40, 40, 8)
        s.path(poly(pts, closed=False), stroke=INK, sw=sw(r, 1.6))
        s.path(poly(pts, closed=False), stroke=fill, sw=sw(r, 0.8))


def e_claw(s, x, y, r, fill):
    for k in (-1, 0, 1):
        pts = U([(-0.55 + k * 0.42, -0.85), (-0.25 + k * 0.42, -0.1), (0.15 + k * 0.42, 0.9), (-0.05 + k * 0.42, -0.05)], x, y, r)
        s.path(smooth(pts, k=0.5), fill, INK, sw(r, 0.8))


def e_mountain(s, x, y, r, fill):
    s.path(poly(U([(-1, 0.75), (-0.35, -0.55), (-0.05, -0.05), (0.3, -0.8), (1, 0.75)], x, y, r)), fill, INK, sw(r))
    s.path(poly(U([(0.3, -0.8), (0.12, -0.42), (0.28, -0.48), (0.42, -0.36), (0.48, -0.52)], x, y, r)), "#ffffff")


def e_roots(s, x, y, r, fill):
    for pts in (((0, -0.95), (0, -0.2), (-0.45, 0.35), (-0.85, 0.9)), ((0, -0.2), (0.4, 0.3), (0.7, 0.95)), ((0, -0.2), (0.05, 0.4), (-0.1, 0.95)),
                ((-0.45, 0.35), (-0.25, 0.85))):
        d = smooth(U(list(pts), x, y, r), closed=False, k=0.8)
        s.path(d, stroke=INK, sw=sw(r, 2.2))
        s.path(d, stroke=fill, sw=sw(r, 1.1))
    for side in (-1, 1):
        s.ellipse(*U([(side * 0.28, -0.75)], x, y, r)[0], round(r * 0.26, 1), round(r * 0.13, 1), "#8ad04a", INK, sw(r, 0.5),
                  extra=f'transform="rotate({side * -30} {U([(side * 0.28, -0.75)], x, y, r)[0][0]} {U([(side * 0.28, -0.75)], x, y, r)[0][1]})"')


def e_wave(s, x, y, r, fill):
    for k, oy in enumerate((-0.4, 0.15, 0.7)):
        pts = U([(-0.95, oy), (-0.5, oy - 0.3), (0, oy), (0.5, oy - 0.3), (0.95, oy)], x, y, r)
        d = smooth(pts, closed=False, k=1)
        s.path(d, stroke=INK, sw=sw(r, 2.4))
        s.path(d, stroke=fill if k != 1 else "#ffffff", sw=sw(r, 1.2))


def e_star(s, x, y, r, fill):
    s.path(star(x, y, r, r * 0.42, 5), fill, INK, sw(r), extra=f'filter="{s.glow(r * 0.05)}"')


def e_moon(s, x, y, r, fill):
    """Полумесяц: внешняя дуга слева (большая), внутренняя — дугой большего радиуса обратно."""
    px, py = round(x + r * 0.342, 1), round(r * 0.94, 1)
    d = f"M{px},{round(y - py, 1)} A{r},{r} 0 1 0 {px},{round(y + py, 1)} A{round(r * 1.2, 1)},{round(r * 1.2, 1)} 0 0 1 {px},{round(y - py, 1)} Z"
    s.path(d, fill, INK, sw(r))


def e_coin(s, x, y, r, fill):
    s.circle(x, y, r, s.rad([(0, "#fff6c8"), (1, "#c8901a")], 0.35, 0.35, 0.8), INK, sw(r))
    s.circle(x, y, round(r * 0.7, 1), "none", "#9a6a1a", sw(r, 0.6))
    s.path(star(x, y, r * 0.45, r * 0.2, 4), "#9a6a1a")


def e_web(s, x, y, r, fill):
    for k in range(8):
        a = math.radians(k * 45)
        s.path(f"M{x},{y} L{x + math.cos(a) * r:.1f},{y + math.sin(a) * r:.1f}", stroke=fill, sw=sw(r, 0.5))
    for rr in (0.3, 0.58, 0.88):
        pts = [(x + math.cos(math.radians(k * 45)) * r * rr, y + math.sin(math.radians(k * 45)) * r * rr) for k in range(8)]
        s.path(smooth(pts, k=-0.4), stroke=fill, sw=sw(r, 0.45))


def e_spider(s, x, y, r, fill, mark="#e02030"):
    for side in (-1, 1):
        for k in range(4):
            a = -50 + k * 30
            p1 = (x + side * r * 0.2, y + r * (k * 0.12 - 0.15))
            p2 = (x + side * r * 0.7, y + r * (-0.6 + k * 0.4))
            p3 = (x + side * r * 0.95, y + r * (-0.2 + k * 0.4))
            d = poly([p1, p2, p3], closed=False)
            s.path(d, stroke=INK, sw=sw(r, 1.2))
            del a
    s.ellipse(x, round(y + r * 0.3, 1), round(r * 0.42, 1), round(r * 0.52, 1), fill, INK, sw(r))
    s.circle(x, round(y - r * 0.32, 1), round(r * 0.26, 1), fill, INK, sw(r))
    s.path(poly(U([(-0.16, 0.1), (0.16, 0.1), (0, 0.3), (0.16, 0.5), (-0.16, 0.5), (0, 0.3)], x, y, r)), mark)


EMBLEMS = {
    "anvil": e_anvil, "flame": e_flame, "heart": e_heart, "shield_heart": e_shield_heart, "mirror": e_mirror, "scroll": e_scroll,
    "sun": e_sun, "sun_arrow": e_sun_arrow, "gate": e_gate, "crown": e_crown, "crown_bolt": e_crown_bolt, "wind": e_wind,
    "hourglass": e_hourglass, "dagger": e_dagger, "sword": e_sword, "axe": e_axe, "hammer": e_hammer, "skull": e_skull,
    "laurel": e_laurel, "elements": e_elements, "eye": e_eye, "fist": e_fist, "arrow_star": e_arrow_star, "sun_cross": e_sun_cross,
    "feather": e_feather, "reflect": e_reflect, "spiral": e_spiral, "pentacle": e_pentacle, "mask": e_mask, "life": e_life,
    "tower": e_tower, "weight": e_weight, "candle": e_candle, "serpent": e_serpent, "wisp": e_wisp, "sponge": e_sponge, "die": e_die,
    "voice": e_voice, "claw": e_claw, "mountain": e_mountain, "roots": e_roots, "wave": e_wave, "star": e_star, "moon": e_moon,
    "coin": e_coin, "web": e_web, "spider": e_spider, "bolt": lambda s, x, y, r, f: s.path(poly(bolt_pts(x, y, r / 36)), f, INK, sw(r),
                                                                                       extra=f'filter="{s.glow(r * 0.05)}"'),
}


def emblem(s, name, x, y, r, fill):
    EMBLEMS[name](s, x, y, r, fill)


# ═══════════════════════════ основы групп ═══════════════════════════

def medallion(s, metal, field, sign, sign_fill, chain="#c8a050"):
    """Амулет богов: цепь и круглый медальон с каймой, в поле — знак."""
    for side in (-1, 1):
        pts = [(C + side * 70, 20), (C + side * 62, 60), (C + side * 40, 92)]
        d = smooth(pts, closed=False, k=1)
        s.path(d, stroke=INK, sw=10)
        s.path(d, stroke=chain, sw=5, extra='stroke-dasharray="7 4"')
    s.path(poly([(C - 14, 82), (C + 14, 82), (C + 10, 100), (C - 10, 100)]), g(s, metal), INK, 4)
    s.circle(C, 156, 74, g(s, metal, 0.6, 1), INK, 6)
    s.circle(C, 156, 58, s.rad(field, 0.4, 0.35, 0.75), INK, 4)
    for k in range(12):
        a = math.radians(k * 30)
        s.circle(round(C + math.cos(a) * 66, 1), round(156 + math.sin(a) * 66, 1), 3, "#ffffff", extra='opacity="0.7"')
    emblem(s, sign, C, 156, 38, sign_fill)


def pendant(s, cord, stone, sign=None, sign_fill="#ffffff", shape="drop"):
    """Подвеска бижутерии: шнурок и камень-капля (или клык, флакон) — не путать с медальонами богов."""
    d = f"M{C - 74},24 Q{C - 40},110 {C},116 Q{C + 40},110 {C + 74},24"
    s.path(d, stroke=INK, sw=9)
    s.path(d, stroke=cord, sw=4)
    if shape == "drop":
        pts = [(C, 104), (C + 46, 160), (C + 40, 206), (C, 230), (C - 40, 206), (C - 46, 160)]
        s.path(smooth(pts, k=0.6), s.rad(stone, 0.4, 0.35, 0.75), INK, 6)
        s.circle(C, 112, 12, g(s, GOLD), INK, 4)
        s.ellipse(C - 18, 160, 8, 16, "#ffffff", extra='opacity="0.55"')
        if sign:
            emblem(s, sign, C, 180, 26, sign_fill)
    return d


def robe(s, cloth, trim, sign, sign_fill, hood=False):
    """Облачение: силуэт мантии с рукавами, кайма и знак на груди."""
    body = [(C, 44), (C + 40, 46), (C + 74, 70), (C + 108, 150), (C + 86, 162), (C + 64, 120), (C + 70, 228), (C - 70, 228), (C - 64, 120),
            (C - 86, 162), (C - 108, 150), (C - 74, 70), (C - 40, 46)]
    s.path(smooth(body, k=0.25), g(s, cloth, 0.5, 1), INK, 6)
    s.path(f"M{C - 70},214 L{C + 70},214", stroke=trim, sw=8)
    s.path(f"M{C},60 L{C},226", stroke=trim, sw=6)
    s.path(f"M{C - 40},46 Q{C},92 {C + 40},46", "none", trim, 7)
    if hood:
        s.path(smooth([(C - 44, 48), (C - 36, 18), (C, 8), (C + 36, 18), (C + 44, 48), (C, 60)], k=0.6), g(s, cloth, 0.5, 1), INK, 5)
    s.circle(C, 128, 32, g(s, [(0, "#ffffff"), (1, trim)]), INK, 4)
    emblem(s, sign, C, 128, 22, sign_fill)
    for side in (-1, 1):
        s.path(f"M{C + side * 100},150 L{C + side * 80},160", stroke=trim, sw=6)


def blessing(s, color, sign, sign_fill):
    """Благословение: свет без предмета — лучи, нимб и знак бога."""
    s.circle(C, C, 108, s.rad([(0, color, 0.85), (0.6, color, 0.35), (1, color, 0)]))
    rays = []
    for k in range(16):
        a = math.radians(k * 22.5)
        rl = 118 if k % 2 == 0 else 92
        rays.append(f"M{C + math.cos(a) * 52:.1f},{C + math.sin(a) * 52:.1f} L{C + math.cos(a) * rl:.1f},{C + math.sin(a) * rl:.1f}")
    s.path(" ".join(rays), stroke=INK, sw=11)
    s.path(" ".join(rays), stroke="#fff6c8", sw=5, extra=f'filter="{s.glow(2)}"')
    s.circle(C, C, 70, "none", INK, 12)
    s.circle(C, C, 70, "none", g(s, GOLD), 6)
    s.circle(C, C, 60, s.rad([(0, "#ffffff"), (0.6, color), (1, color)], 0.45, 0.4, 0.7), INK, 4)
    emblem(s, sign, C, C, 40, sign_fill)


def crystal(s, colors, sign, sign_fill):
    """Сущность пути: вытянутый гранёный кристалл, знак класса светится внутри."""
    light, mid, dark = colors
    glow(s, C, C, 96, mid, 0.5)
    outer = [(C, 18), (C + 52, 72), (C + 46, 184), (C, 238), (C - 46, 184), (C - 52, 72)]
    s.path(poly(outer), g(s, [(0, light), (0.5, mid), (1, dark)], 0.6, 1), INK, 6)
    s.path(poly([(C, 18), (C + 52, 72), (C, 92), (C - 52, 72)]), light, INK, 3, extra='opacity="0.85"')
    s.path(poly([(C, 92), (C, 238)], closed=False), stroke=INK, sw=2.5, extra='opacity="0.6"')
    s.path(poly([(C - 52, 72), (C - 20, 184), (C, 238)], closed=False), stroke="#ffffff", sw=2, extra='opacity="0.4"')
    s.circle(C, 146, 40, s.rad([(0, "#ffffff", 0.9), (1, mid, 0)]))
    emblem(s, sign, C, 146, 30, sign_fill)
    sparkle(s, C - 26, 50, 12)


def tome(s, cover, sign, sign_fill, clasp=GOLD, thick=1.0, corners=True):
    """Книга в три четверти: обложка, корешок, обрез страниц, знак на обложке."""
    t = int(18 * thick)
    s.path(poly([(70, 52), (196, 40), (196 + t, 52), (196 + t, 220), (70 + t, 232), (70, 220)]), "#f2e6c8", INK, 5)
    for k in range(4):
        s.path(f"M{198 + k * t / 5:.1f},{48 + k * 2} L{198 + k * t / 5:.1f},{218 - k}", stroke="#c8b890", sw=1.5)
    s.path(poly([(56, 54), (190, 40), (190, 210), (56, 226)]), g(s, cover, 0.6, 1), INK, 6)
    s.path(poly([(56, 54), (76, 52), (76, 224), (56, 226)]), g(s, [(0, "#000000", 0.0), (1, "#000000", 0.45)]), INK, 3)
    if corners:
        for px, py, dx, dy in ((190, 40, -1, 1), (190, 210, -1, -1)):
            s.path(poly([(px, py), (px + dx * 26, py + dy * 3), (px, py + dy * 26)]), g(s, clasp), INK, 3)
    s.path(poly([(186, 116), (214, 114), (214, 136), (186, 138)]), g(s, clasp), INK, 4)
    s.circle(126, 132, 46, s.rad([(0, "#ffffff", 0.35), (1, "#ffffff", 0)]))
    emblem(s, sign, 126, 132, 40, sign_fill)


def shard(s, colors, sign, sign_fill, seed=0):
    """Осколок Отлива: неровный каменный осколок с жилами света и знаком стихии."""
    light, mid, dark = colors
    glow(s, C, C, 90, mid, 0.45)
    base = [(C - 18, 18), (C + 30, 34), (C + 66, 84), (C + 58, 150), (C + 74, 196), (C + 20, 236), (C - 40, 222), (C - 70, 160), (C - 56, 100),
            (C - 62, 58)]
    pts = [(px + ((i * 37 + seed * 11) % 9) - 4, py) for i, (px, py) in enumerate(base)]
    s.path(poly(pts), g(s, STONE, 0.6, 1), INK, 6)
    s.path(poly([(C - 18, 18), (C + 30, 34), (C + 8, 90), (C - 56, 100), (C - 62, 58)]), "#ffffff", extra='opacity="0.12"')
    for d in (f"M{C - 50},{90} L{C - 10},{128} L{C + 40},{110}", f"M{C - 10},{128} L{C - 20},{196} L{C + 30},{220}",
              f"M{C + 40},{110} L{C + 60},{170}"):
        s.path(d, stroke=mid, sw=5, extra=f'filter="{s.glow(3)}"')
    s.circle(C, 136, 42, s.rad([(0, light, 0.9), (1, mid, 0)]))
    emblem(s, sign, C, 136, 32, sign_fill)


def omen(s, sign, glow_color="#c08aff"):
    """Знамение: тёмный каменный диск со сколами, знак светится фиолетовым."""
    pts = []
    for k in range(20):
        a = math.radians(k * 18)
        rr = 92 - (9 if k in (3, 11, 16) else 0)
        pts.append((round(C + math.cos(a) * rr, 1), round(C + math.sin(a) * rr, 1)))
    s.path(poly(pts), g(s, STONE, 0.5, 1), INK, 6)
    s.circle(C, C, 72, "none", "#2a2232", 4)
    for k in range(8):
        a = math.radians(k * 45 + 22)
        s.path(f"M{C + math.cos(a) * 76:.1f},{C + math.sin(a) * 76:.1f} L{C + math.cos(a) * 84:.1f},{C + math.sin(a) * 84:.1f}", stroke=glow_color, sw=3,
               extra='opacity="0.8"')
    s.circle(C, C, 62, s.rad([(0, glow_color, 0.55), (1, glow_color, 0)]))
    emblem(s, sign, C, C, 50, glow_color)


def ring(s, band, stone, sign=None, sign_fill="#ffffff", band_motif=None):
    """Кольцо: ободок в перспективе, оправа с камнем сверху; знак — на камне или вместо него."""
    cx, cy = C, 164
    s.ellipse(cx, cy, 76, 50, "none", INK, 30)
    s.ellipse(cx, cy, 76, 50, "none", g(s, band, 0.6, 1), 20)
    s.path(f"M{cx - 70},{cy + 6} A70,44 0 0 0 {cx + 70},{cy + 6}", stroke="#ffffff", sw=3, extra='opacity="0.55"')
    if band_motif == "thorns":
        for k in range(9):
            a = math.radians(200 + k * 17)
            px, py = cx + math.cos(a) * 76, cy + math.sin(a) * 50
            s.path(poly([(px - 5, py), (px + math.cos(a) * 14, py + math.sin(a) * 14), (px + 5, py)]), "#c8ccd2", INK, 2)
    elif band_motif == "runes":
        for k in range(5):
            a = math.radians(25 + k * 32)
            px, py = cx + math.cos(a) * 76, cy + math.sin(a) * 50
            s.path(f"M{px - 4:.1f},{py - 5:.1f} L{px + 4:.1f},{py + 5:.1f} M{px + 4:.1f},{py - 5:.1f} L{px:.1f},{py:.1f}", stroke="#7ad8ff", sw=2.5,
                   extra=f'filter="{s.glow(1.5)}"')
    s.path(poly([(cx - 40, 128), (cx + 40, 128), (cx + 28, 104), (cx - 28, 104)]), g(s, band), INK, 5)
    for px in (-30, -10, 10, 30):
        s.path(f"M{cx + px},{110} L{cx + px * 1.2},{70}", stroke=INK, sw=7)
        s.path(f"M{cx + px},{110} L{cx + px * 1.2},{70}", stroke="#d8c070", sw=3)
    if stone:
        light, dark = stone
        glow(s, cx, 78, 54, light, 0.5)
        gem(s, cx, 78, 40, light, dark)
        if sign:
            emblem(s, sign, cx, 80, 24, sign_fill)
    elif sign:
        s.circle(cx, 78, 42, g(s, band), INK, 5)
        emblem(s, sign, cx, 80, 28, sign_fill)


def staff(s, shaft, head):
    """Посох: древко по диагонали, навершие — функция head(s, x, y)."""
    line(s, [(78, 238), (156, 82)], "#000000", 8)
    s.path("M78,238 L156,82", stroke=g(s, shaft, 1, 0), sw=12)
    for k in range(3):
        t = 0.55 + k * 0.08
        px, py = 78 + (156 - 78) * t, 238 + (82 - 238) * t
        s.path(f"M{px - 9:.1f},{py - 2:.1f} L{px + 9:.1f},{py + 4:.1f}", stroke="#c8a050", sw=4)
    head(s, 166, 62)
