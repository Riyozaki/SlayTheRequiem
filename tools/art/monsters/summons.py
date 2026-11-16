"""Спрайты призывов героя (M6 п. 2, docs/design/08 §5) → art/monsters/summon_*.svg.

Призывы стоят на стороне героев, поэтому смотрят ВПРАВО (к врагам) — зеркально врагам draw_monsters.py.
Низ фигуры — у нижнего края холста. Духи (волки, звери, рыцарь) — полупрозрачные со свечением, атронахи — стихии,
нежить — кость, стражи — металл. Палитра — art spec §1.1 (толстый тёмный контур, 2–3 тона).
"""
from __future__ import annotations

from svgkit import INK, Svg, poly, smooth

# духи
SPIRIT_CYAN = ("#e9fdff", "#8fe3ff", "#2f7fa8")
BEAST_JADE = ("#f0fff6", "#a8f0c8", "#2f8a64")
# стихии
FIRE = ("#fff4c2", "#ffad3a", "#c2321a")
ICE = ("#f4fdff", "#a9e6ff", "#3c7fae")
STORM_CORE = "#f3e9ff"
STORM_GLOW = "#b48cff"
# нежить и металл
BONE = ("#f6f0dc", "#d2c6a2", "#7d7056")
ROT = ("#d9dcb8", "#9fa77a", "#4f5a3a")
LICH_FIRE = "#7fd7ff"
GRAVE_GREEN = "#a6ff7a"
ABYSS = "#b46cff"
STEEL = ("#eef3f6", "#9aa7b2", "#3f4a55")
CITADEL_BLUE = ("#5d8fd6", "#1f3f78")


def _lin3(s: Svg, pal, x2=0, y2=1, alpha=1.0):
    return s.lin([(0, pal[0], alpha), (0.55, pal[1], alpha), (1, pal[2], alpha)], 0, 0, x2, y2)


def _aura(s: Svg, cx, cy, r, color, alpha=0.45):
    # Ореол не должен упираться в край холста — иначе виден прямоугольный срез.
    r = min(r, cx, cy, s.w - cx, s.h - cy)
    s.circle(cx, cy, r, s.rad([(0, color, alpha), (0.6, color, alpha * 0.35), (1, color, 0)]))


def _wisps(s: Svg, pts, color, sw=5):
    """Тающие струйки духа (хвосты от лап/плаща)."""
    blur = s.blur(1.6)
    for (x, y, dx, dy) in pts:
        s.path(f"M{x},{y} q{dx * 0.5},{dy * 0.2} {dx},{dy}", stroke=color, sw=sw, extra=f'opacity="0.55" filter="{blur}"')


# ═════════════════════════ звери-духи ═════════════════════════

def _wolf(pal):
    s = Svg(300, 220)
    _aura(s, 170, 120, 130, pal[1], 0.35)
    s.shadow(165, 210, 100, 8, 0.3)
    body = _lin3(s, pal, 0.3, 1, 0.88)
    glow = s.glow(3.5)
    s.open(f'filter="{glow}"')
    # хвост (сзади, слева) — пушистый, вздёрнут
    s.path(smooth([(86, 112), (54, 96), (30, 62), (26, 40), (44, 52), (60, 80), (92, 100)]), body, sw=4)
    # дальние лапы (темнее)
    far = _lin3(s, (pal[1], pal[2], "#163a50"), 0, 1, 0.85)
    for x in (110, 210):
        s.path(poly([(x - 8, 138), (x + 8, 138), (x + 10, 196), (x + 18, 206), (x - 4, 206), (x - 6, 150)]), far, sw=3.5)
    # туловище
    s.path(smooth([(74, 116), (110, 90), (170, 86), (214, 96), (232, 118), (222, 146), (180, 154), (120, 152), (82, 140)]), body, sw=4.5)
    # ближние лапы
    for x in (96, 196):
        s.path(poly([(x - 9, 140), (x + 9, 140), (x + 9, 200), (x + 20, 210), (x - 6, 210), (x - 8, 152)]), body, sw=4)
    # загривок и голова
    s.path(smooth([(196, 104), (214, 78), (236, 64), (258, 66), (282, 82), (296, 96), (290, 106), (268, 110), (248, 124), (220, 128)]), body, sw=4.5)
    # уши
    s.path(poly([(236, 70), (240, 40), (254, 66)]), body, sw=3.5)
    s.path(poly([(250, 68), (262, 42), (268, 72)]), body, sw=3.5)
    s.close()
    # шерсть на загривке
    s.path("M176,92 l10,-14 l4,14 l10,-16 l4,16 l10,-14", stroke=pal[0], sw=3, extra='opacity="0.8"')
    # пасть и нос
    s.path("M290,100 L268,104", stroke=INK, sw=3)
    s.circle(294, 96, 4.5, INK)
    s.eye(262, 80, 5, pal[0], "#ffffff")
    _wisps(s, [(96, 210, -26, 4), (196, 210, -28, 2), (40, 46, -18, -10), (84, 140, -30, 6)], pal[1])
    return s


def draw_spirit_wolf(v=""):
    """Волк-дух: голубой полупрозрачный волк."""
    return _wolf(SPIRIT_CYAN)


def draw_beast_wolf(v=""):
    """Дух зверя, волк Инеерождённых: нефритово-белый."""
    return _wolf(BEAST_JADE)


def draw_beast_sabertooth(v=""):
    """Дух зверя, саблезуб: мощный кот с клыками-саблями."""
    pal = BEAST_JADE
    s = Svg(340, 240)
    _aura(s, 190, 130, 150, pal[1], 0.35)
    s.shadow(185, 230, 120, 9, 0.3)
    body = _lin3(s, pal, 0.3, 1, 0.9)
    far = _lin3(s, (pal[1], pal[2], "#12382a"), 0, 1, 0.85)
    s.open(f'filter="{s.glow(3.5)}"')
    # короткий хвост
    s.path(smooth([(70, 118), (44, 112), (30, 94), (40, 88), (58, 102), (78, 108)]), body, sw=4)
    for x in (110, 236):
        s.path(poly([(x - 11, 150), (x + 11, 150), (x + 12, 214), (x + 22, 224), (x - 8, 224), (x - 9, 162)]), far, sw=3.5)
    s.path(smooth([(66, 124), (104, 96), (176, 90), (238, 98), (262, 124), (252, 160), (200, 170), (124, 168), (74, 152)]), body, sw=5)
    for x in (92, 220):
        s.path(poly([(x - 13, 152), (x + 13, 152), (x + 13, 218), (x + 26, 228), (x - 8, 228), (x - 11, 166)]), body, sw=4.5)
    # голова: широкая, низко
    s.path(smooth([(230, 108), (250, 82), (282, 76), (314, 90), (330, 110), (324, 128), (300, 136), (270, 140), (244, 136)]), body, sw=5)
    s.path(poly([(262, 86), (268, 62), (282, 82)]), body, sw=3.5)
    s.close()
    # клыки-сабли
    for dx in (0, 14):
        s.path(f"M{300 + dx},{130} Q{304 + dx},{158} {296 + dx},{176} Q{292 + dx},{156} {294 + dx},{132} Z",
               s.lin([(0, "#ffffff"), (1, "#cfe8da")]), sw=3)
    # полосы
    for x in (140, 170, 200):
        s.path(f"M{x},{94} q8,22 -2,40", stroke=pal[2], sw=5, extra='opacity="0.6"')
    s.circle(330, 108, 4.5, INK)
    s.eye(292, 98, 5.5, pal[0], "#ffffff")
    _wisps(s, [(92, 228, -30, 4), (220, 228, -30, 2), (34, 92, -20, -6)], pal[1])
    return s


def draw_beast_bear(v=""):
    """Дух зверя, медведь: горбатый великан."""
    pal = BEAST_JADE
    s = Svg(360, 280)
    _aura(s, 190, 150, 170, pal[1], 0.35)
    s.shadow(185, 270, 140, 10, 0.3)
    body = _lin3(s, pal, 0.3, 1, 0.9)
    far = _lin3(s, (pal[1], pal[2], "#12382a"), 0, 1, 0.85)
    s.open(f'filter="{s.glow(3.5)}"')
    for x in (110, 246):
        s.path(poly([(x - 16, 180), (x + 16, 180), (x + 18, 254), (x + 28, 264), (x - 12, 264), (x - 14, 196)]), far, sw=3.5)
    s.path(smooth([(54, 170), (70, 120), (130, 74), (200, 70), (256, 96), (284, 140), (276, 196), (220, 208), (120, 206), (64, 196)]), body, sw=5)
    for x in (88, 226):
        s.path(poly([(x - 19, 184), (x + 19, 184), (x + 20, 258), (x + 32, 268), (x - 12, 268), (x - 16, 200)]), body, sw=4.5)
    # голова низко впереди
    s.path(smooth([(250, 128), (276, 108), (310, 110), (338, 128), (352, 146), (344, 162), (316, 168), (284, 170), (258, 160)]), body, sw=5)
    s.circle(282, 108, 13, body, INK, 3.5)
    s.close()
    # шерсть на горбе
    s.path("M120,86 l12,-14 l6,16 l12,-18 l6,18 l14,-16 l4,18", stroke=pal[0], sw=3.5, extra='opacity="0.8"')
    s.path("M352,146 L330,150", stroke=INK, sw=3)
    s.circle(352, 144, 5.5, INK)
    s.eye(312, 128, 5.5, pal[0], "#ffffff")
    # когти
    for x in (226, 88):
        for k in range(3):
            s.path(f"M{x + 8 + k * 7},{268} l5,6", stroke="#ffffff", sw=3)
    _wisps(s, [(88, 268, -34, 4), (226, 268, -34, 2), (56, 180, -30, 8)], pal[1])
    return s


# ═════════════════════════ атронахи ═════════════════════════

def draw_flame_atronach(v=""):
    """Огненный атронах: парящая фигура из пламени с рогами, огненный шар в руке к врагам."""
    s = Svg(300, 360)
    cx = 120
    _aura(s, cx + 20, 180, 140, FIRE[1], 0.5)
    s.shadow(cx, 350, 60, 8, 0.35)
    fire = _lin3(s, FIRE, 0, 1)
    s.open(f'filter="{s.glow(4)}"')
    # огненный хвост вместо ног
    s.path(smooth([(cx - 40, 220), (cx + 38, 220), (cx + 30, 280), (cx + 6, 330), (cx - 8, 300), (cx - 30, 340), (cx - 34, 280)]), fire, sw=4)
    # торс
    s.path(smooth([(cx - 46, 110), (cx + 46, 110), (cx + 40, 170), (cx + 26, 228), (cx - 26, 228), (cx - 40, 170)]), fire, sw=4.5)
    # дальняя рука (за спиной, слева)
    s.path(smooth([(cx - 40, 118), (cx - 72, 160), (cx - 80, 200), (cx - 66, 204), (cx - 56, 166)]), _lin3(s, (FIRE[1], FIRE[2], "#5a1206")), sw=4)
    # ближняя рука вытянута вправо
    s.path(smooth([(cx + 36, 116), (cx + 84, 130), (cx + 116, 140), (cx + 112, 156), (cx + 78, 150), (cx + 34, 146)]), fire, sw=4)
    # голова с рогами
    s.path(smooth([(cx - 24, 70), (cx, 52), (cx + 26, 66), (cx + 28, 98), (cx, 114), (cx - 24, 98)]), fire, sw=4.5)
    s.path(smooth([(cx - 18, 62), (cx - 34, 30), (cx - 30, 12), (cx - 22, 34), (cx - 6, 56)], closed=False), "none", INK, 5)
    s.path(smooth([(cx + 14, 58), (cx + 34, 24), (cx + 40, 8), (cx + 34, 34), (cx + 22, 62)], closed=False), "none", INK, 5)
    s.close()
    # языки пламени по контуру
    for (x, y, h) in [(cx - 46, 120, 30), (cx + 44, 122, 28), (cx - 30, 220, 26), (cx + 24, 222, 24), (cx, 50, 30)]:
        s.path(f"M{x - 8},{y} Q{x - 4},{y - h * 0.6} {x},{y - h} Q{x + 4},{y - h * 0.6} {x + 8},{y} Z",
               s.lin([(0, FIRE[0]), (1, FIRE[1], 0.6)]), stroke=None)
    s.eye(cx + 6, 82, 4.5, "#fff7d6", "#ffffff")
    s.eye(cx + 20, 82, 4, "#fff7d6", "#ffffff")
    # огненный шар
    s.circle(cx + 128, 146, 26, s.rad([(0, "#ffffff"), (0.35, FIRE[0]), (0.7, FIRE[1]), (1, FIRE[2], 0)]))
    s.circle(cx + 128, 146, 12, "#fffbe6", extra=f'filter="{s.glow(4)}"')
    return s


def draw_frost_atronach(v=""):
    """Ледяной атронах: глыба-голем из льда с наростами кристаллов."""
    s = Svg(280, 360)
    cx = 136
    _aura(s, cx, 190, 150, ICE[1], 0.35)
    s.shadow(cx, 350, 96, 10, 0.4)
    ice = _lin3(s, ICE, 0.4, 1)
    dark = _lin3(s, (ICE[1], ICE[2], "#173a5a"), 0.4, 1)
    s.open(f'filter="{s.grain(0.12, 0.8, 23)}"')
    # ноги-глыбы
    for side, fill in ((-1, dark), (1, ice)):
        x = cx + side * 34
        s.path(poly([(x - 26, 236), (x + 24, 236), (x + 30, 300), (x + 36, 344), (x - 32, 344), (x - 30, 300)]), fill, sw=4.5)
    # дальняя рука
    s.path(poly([(cx - 66, 110), (cx - 100, 150), (cx - 108, 222), (cx - 80, 236), (cx - 70, 180), (cx - 48, 150)]), dark, sw=4.5)
    # торс
    s.path(poly([(cx - 70, 96), (cx + 64, 90), (cx + 80, 150), (cx + 56, 240), (cx - 58, 244), (cx - 82, 160)]), ice, sw=5)
    # голова — маленькая, вдавлена в плечи
    s.path(poly([(cx - 22, 52), (cx + 26, 48), (cx + 36, 84), (cx + 12, 104), (cx - 22, 102), (cx - 32, 78)]), ice, sw=4.5)
    # ближняя рука с кулаком к врагам
    s.path(poly([(cx + 54, 104), (cx + 104, 130), (cx + 126, 186), (cx + 104, 200), (cx + 84, 158), (cx + 50, 150)]), ice, sw=4.5)
    s.path(poly([(cx + 96, 186), (cx + 132, 180), (cx + 140, 214), (cx + 104, 222)]), ice, sw=4.5)
    s.close()
    # грани
    for d in [f"M{cx - 70},{96} L{cx - 10},{150} L{cx + 64},{90}", f"M{cx - 10},{150} L{cx - 4},{242}", f"M{cx - 82},{160} L{cx - 10},{150} L{cx + 80},{150}"]:
        s.path(d, stroke=ICE[0], sw=2.5, extra='opacity="0.8"')
    # кристаллы на плечах
    for (x, y, h, a) in [(cx - 60, 98, 52, -18), (cx - 34, 94, 40, -6), (cx + 44, 92, 58, 14), (cx + 66, 102, 40, 26)]:
        s.path(poly([(x - 9, y), (x + 9, y), (x + a * 0.6, y - h)]), s.lin([(0, "#ffffff"), (1, ICE[1])]), sw=3.5)
    s.eye(cx + 14, 76, 5, "#dff8ff", "#ffffff")
    s.eye(cx - 6, 78, 4, "#dff8ff", "#ffffff")
    s.circle(cx - 6, 170, 16, s.rad([(0, "#ffffff"), (0.5, ICE[1]), (1, ICE[1], 0)]))
    return s


def draw_storm_atronach(v=""):
    """Грозовой атронах: камни, парящие вокруг грозового ядра, молнии между ними."""
    s = Svg(280, 380)
    cx = 136
    _aura(s, cx, 180, 150, STORM_GLOW, 0.45)
    s.shadow(cx, 370, 70, 8, 0.35)
    rock = s.lin([(0, "#8e8ba3"), (0.6, "#4e4a66"), (1, "#24213a")], 0, 0, 0.4, 1)
    rock2 = s.lin([(0, "#6e6a86"), (1, "#1c1930")], 0, 0, 0.4, 1)
    # молнии-связки (под камнями)
    bolt = s.glow(3)
    for d in [f"M{cx},{170} L{cx - 18},{120} L{cx - 6},{110} L{cx - 20},{70}",
              f"M{cx},{170} L{cx + 40},{150} L{cx + 52},{160} L{cx + 96},{140}",
              f"M{cx},{170} L{cx - 40},{196} L{cx - 50},{186} L{cx - 92},{220}",
              f"M{cx},{170} L{cx + 10},{240} L{cx - 4},{250} L{cx + 6},{310}"]:
        s.path(d, stroke=STORM_CORE, sw=4, extra=f'filter="{bolt}"')
    # ядро
    s.circle(cx, 170, 36, s.rad([(0, "#ffffff"), (0.4, STORM_CORE), (0.75, STORM_GLOW, 0.8), (1, STORM_GLOW, 0)]))
    s.path(poly([(cx, 140), (cx + 18, 170), (cx, 200), (cx - 18, 170)]), s.lin([(0, "#ffffff"), (1, STORM_GLOW)]), sw=3)
    # камни: голова, плечи, руки, таз, ноги
    stones = [
        ([(cx - 26, 40), (cx + 22, 34), (cx + 34, 64), (cx + 10, 88), (cx - 24, 84), (cx - 34, 60)], rock),
        ([(cx - 84, 102), (cx - 44, 94), (cx - 34, 124), (cx - 70, 138), (cx - 94, 124)], rock2),
        ([(cx + 40, 96), (cx + 86, 100), (cx + 94, 128), (cx + 62, 140), (cx + 36, 124)], rock),
        ([(cx - 112, 160), (cx - 84, 150), (cx - 76, 196), (cx - 102, 208), (cx - 120, 190)], rock2),
        ([(cx + 96, 124), (cx + 132, 126), (cx + 140, 158), (cx + 110, 170), (cx + 92, 150)], rock),
        ([(cx - 40, 220), (cx + 38, 216), (cx + 46, 246), (cx - 4, 262), (cx - 48, 246)], rock),
        ([(cx - 50, 276), (cx - 14, 272), (cx - 10, 330), (cx - 30, 352), (cx - 56, 330)], rock2),
        ([(cx + 12, 276), (cx + 48, 274), (cx + 52, 330), (cx + 30, 356), (cx + 6, 330)], rock),
    ]
    s.open(f'filter="{s.grain(0.18, 0.9, 29)}"')
    for pts, fill in stones:
        s.path(poly(pts), fill, sw=4.5)
    s.close()
    # руны на камнях
    for (x, y) in [(cx - 4, 60), (cx + 64, 118), (cx - 66, 116), (cx, 238)]:
        s.path(f"M{x - 6},{y - 6} L{x + 4},{y} L{x - 4},{y + 6}", stroke=STORM_GLOW, sw=3, extra=f'filter="{s.glow(2)}"')
    s.eye(cx + 12, 62, 4.5, STORM_CORE, "#ffffff")
    return s


# ═════════════════════════ нежить ═════════════════════════

def _skeleton(s: Svg, cx, pal, eye_color, hunched: bool):
    """Скелет-воин в профиль 3/4, смотрит вправо. hunched — сгорбленный мертвец с вытянутыми руками."""
    bone = _lin3(s, pal, 0.3, 1)
    dark = _lin3(s, (pal[1], pal[2], "#2c261a"), 0.3, 1)
    lean = 18 if hunched else 0
    # ноги
    for side, fill in ((-1, dark), (1, bone)):
        x = cx + side * 18
        s.path(f"M{x},{214} L{x + side * 4},{262} L{x - 2},{312}", stroke=INK, sw=13)
        s.path(f"M{x},{214} L{x + side * 4},{262} L{x - 2},{312}", stroke=pal[1] if side > 0 else pal[2], sw=7)
        s.circle(x + side * 4, 262, 7, fill, INK, 3)
        s.path(poly([(x - 14, 308), (x + 16, 308), (x + 22, 322), (x - 12, 322)]), fill, sw=3.5)
    # таз
    s.path(smooth([(cx - 28, 196), (cx + 28, 196), (cx + 24, 220), (cx, 228), (cx - 24, 220)]), bone, sw=4)
    # позвоночник и рёбра
    top = (cx + lean, 104)
    s.path(f"M{cx},{200} Q{cx + lean * 0.3},{150} {top[0]},{top[1]}", stroke=INK, sw=10)
    s.path(f"M{cx},{200} Q{cx + lean * 0.3},{150} {top[0]},{top[1]}", stroke=pal[1], sw=5)
    for i, y in enumerate((120, 138, 156, 174)):
        w = 34 - i * 4
        x = cx + lean * (1 - (y - 104) / 100)
        s.path(f"M{x - w},{y + 6} Q{x},{y - 6} {x + w},{y + 6}", stroke=INK, sw=8)
        s.path(f"M{x - w},{y + 6} Q{x},{y - 6} {x + w},{y + 6}", stroke=pal[0], sw=4)
    # череп
    hx, hy = top[0] + 6, 74
    s.path(smooth([(hx - 26, hy - 4), (hx - 12, hy - 28), (hx + 14, hy - 30), (hx + 30, hy - 10), (hx + 32, hy + 12),
                   (hx + 20, hy + 26), (hx - 2, hy + 30), (hx - 20, hy + 18)]), bone, sw=4.5)
    s.path(poly([(hx + 4, hy + 18), (hx + 26, hy + 16), (hx + 24, hy + 34), (hx + 2, hy + 34)]), bone, sw=3.5)
    s.path(f"M{hx + 8},{hy + 24} L{hx + 8},{hy + 34} M{hx + 14},{hy + 24} L{hx + 14},{hy + 34} M{hx + 20},{hy + 24} L{hx + 20},{hy + 33}", stroke=INK, sw=2)
    s.ellipse(hx + 16, hy + 2, 8, 9, "#140f0a")
    s.ellipse(hx - 4, hy + 2, 6, 8, "#140f0a")
    s.eye(hx + 16, hy + 2, 4, eye_color, "#ffffff")
    s.eye(hx - 4, hy + 2, 3, eye_color, "#ffffff")
    return bone, dark, top


def draw_raised_dead(v=""):
    """Поднятый мертвец: сгорбленный, в лохмотьях, руки тянутся к врагам, могильный зелёный огонь."""
    s = Svg(240, 330)
    cx = 100
    _aura(s, cx + 20, 150, 120, GRAVE_GREEN, 0.25)
    s.shadow(cx, 320, 70, 8, 0.4)
    s.open(f'filter="{s.grain(0.2, 0.9, 31)}"')
    bone, dark, top = _skeleton(s, cx, ROT, GRAVE_GREEN, hunched=True)
    # лохмотья
    s.path(smooth([(cx - 30, 110), (cx + 44, 100), (cx + 50, 160), (cx + 40, 214), (cx + 26, 236), (cx + 12, 216), (cx - 2, 240),
                   (cx - 16, 214), (cx - 32, 232), (cx - 40, 180)]), s.lin([(0, "#5a5a44", 0.95), (1, "#24261a", 0.95)]), sw=4)
    s.path(f"M{cx - 20},{140} l14,10 M{cx + 20},{170} l-10,14", stroke="#1a1a10", sw=3)
    # руки вперёд
    for dy, fill in ((0, dark), (18, bone)):
        sx, sy = top[0] + 20, top[1] + 10 + dy
        s.path(f"M{sx},{sy} L{sx + 50},{sy + 14} L{sx + 96},{sy + 8}", stroke=INK, sw=11)
        s.path(f"M{sx},{sy} L{sx + 50},{sy + 14} L{sx + 96},{sy + 8}", stroke=ROT[1] if dy else ROT[2], sw=6)
        for k in range(3):
            s.path(f"M{sx + 96},{sy + 8} l{12 + k * 2},{-6 + k * 6}", stroke=INK, sw=4)
    s.close()
    _wisps(s, [(cx + 30, top[1] - 34, 10, -28), (cx + 4, top[1] - 30, -6, -26)], GRAVE_GREEN, 4)
    return s


def draw_bone_thrall(v=""):
    """Костяной прислужник Лича: чистая кость, рогатый шлем, щит и ржавый клинок, голубой огонь в глазницах."""
    s = Svg(240, 330)
    cx = 104
    _aura(s, cx + 10, 150, 120, LICH_FIRE, 0.25)
    s.shadow(cx, 320, 70, 8, 0.4)
    s.open(f'filter="{s.grain(0.16, 0.9, 37)}"')
    # клинок за спиной (дальняя рука поднята)
    s.path(f"M{cx - 30},{120} L{cx - 50},{90}", stroke=INK, sw=11)
    s.path(f"M{cx - 30},{120} L{cx - 50},{90}", stroke=BONE[2], sw=6)
    s.path(poly([(cx - 56, 92), (cx - 44, 86), (cx - 10, 10), (cx - 20, 6)]), s.lin([(0, "#c9b8a0"), (1, "#6a4a32")]), sw=3.5)
    s.path(f"M{cx - 64},{94} L{cx - 36},{82}", stroke=INK, sw=6)
    bone, dark, top = _skeleton(s, cx, BONE, LICH_FIRE, hunched=False)
    # рогатый шлем
    hx, hy = top[0] + 6, 74
    s.path(smooth([(hx - 28, hy - 2), (hx - 14, hy - 32), (hx + 16, hy - 34), (hx + 32, hy - 12), (hx + 30, hy - 4)], closed=False) + " Z",
           s.lin([(0, "#8a8f96"), (1, "#2c3036")]), sw=4)
    s.path(smooth([(hx - 18, hy - 26), (hx - 40, hy - 44), (hx - 44, hy - 66), (hx - 30, hy - 48), (hx - 10, hy - 34)]), BONE[1], sw=3.5)
    # щит в ближней руке — к врагам
    sh = [(cx + 34, 120), (cx + 92, 116), (cx + 96, 176), (cx + 64, 214), (cx + 32, 178)]
    s.path(poly(sh), s.lin([(0, "#5a4b3a"), (1, "#241a10")]), sw=4.5)
    s.path(poly(sh), "none", "#8a8f96", 3)
    s.circle(cx + 64, 160, 12, s.rad([(0, "#ffffff"), (0.4, LICH_FIRE), (1, LICH_FIRE, 0.2)]), INK, 3)
    s.close()
    return s


# ═════════════════════════ стражи и рыцари ═════════════════════════

def draw_abyss_guardian(v=""):
    """Дремлющий страж Бездны: тяжёлый тёмный доспех, щель забрала, руны, двуручный меч упёрт в землю перед собой."""
    s = Svg(300, 400)
    cx = 136
    _aura(s, cx, 200, 160, ABYSS, 0.3)
    s.shadow(cx, 390, 100, 10, 0.5)
    armor = s.lin([(0, "#5a5470"), (0.5, "#2c2840"), (1, "#120f1e")], 0, 0, 0.4, 1)
    armor2 = s.lin([(0, "#3e3a54"), (1, "#0c0a16")], 0, 0, 0.4, 1)
    s.open(f'filter="{s.grain(0.16, 0.85, 41)}"')
    # плащ сзади
    s.path(smooth([(cx - 60, 110), (cx - 100, 220), (cx - 110, 370), (cx - 70, 352), (cx - 40, 380), (cx - 20, 250)]),
           s.lin([(0, "#3a1a5a"), (1, "#100618")]), sw=4)
    # ноги
    for side, fill in ((-1, armor2), (1, armor)):
        x = cx + side * 30
        s.path(poly([(x - 22, 250), (x + 22, 250), (x + 20, 340), (x + 30, 380), (x - 26, 380), (x - 20, 340)]), fill, sw=4.5)
        s.path(poly([(x - 18, 300), (x + 18, 300), (x + 14, 320), (x - 14, 320)]), "#4a4460", sw=3)
    # торс
    s.path(smooth([(cx - 66, 108), (cx + 66, 108), (cx + 58, 200), (cx + 40, 262), (cx - 40, 262), (cx - 58, 200)]), armor, sw=5)
    # наплечники
    for side in (-1, 1):
        s.path(smooth([(cx + side * 40, 96), (cx + side * 92, 108), (cx + side * 98, 150), (cx + side * 54, 146)]), armor, sw=4.5)
        s.path(poly([(cx + side * 70, 104), (cx + side * 76, 70), (cx + side * 86, 108)]), armor2, sw=3.5)
    # шлем
    s.path(smooth([(cx - 32, 96), (cx - 30, 52), (cx, 34), (cx + 32, 50), (cx + 36, 96), (cx, 110)]), armor, sw=5)
    s.path(poly([(cx - 4, 34), (cx + 6, 6), (cx + 12, 36)]), armor2, sw=3.5)
    # меч перед собой: гарда у груди, клинок до земли
    s.path(poly([(cx + 72, 186), (cx + 88, 186), (cx + 86, 372), (cx + 80, 388), (cx + 74, 372)]),
           s.lin([(0, "#cfc7ea"), (1, "#4a4466")], 0, 0, 1, 0), sw=4)
    s.path(poly([(cx + 50, 176), (cx + 110, 176), (cx + 110, 188), (cx + 50, 188)]), armor2, sw=4)
    s.path(poly([(cx + 74, 140), (cx + 86, 140), (cx + 86, 176), (cx + 74, 176)]), "#2a2030", sw=3.5)
    s.circle(cx + 80, 136, 9, s.rad([(0, "#ffffff"), (0.5, ABYSS), (1, ABYSS, 0.4)]), INK, 3)
    # руки на навершии
    s.path(smooth([(cx + 50, 120), (cx + 74, 140), (cx + 82, 162), (cx + 66, 168), (cx + 52, 150)]), armor, sw=4)
    s.close()
    # щель забрала и руны (тускло — «дремлет»)
    s.path(f"M{cx - 4},{70} L{cx + 30},{70}", stroke=ABYSS, sw=5, extra=f'filter="{s.glow(3)}"')
    for (x, y) in [(cx - 20, 150), (cx + 10, 176), (cx - 10, 214)]:
        s.path(f"M{x},{y - 8} L{x + 8},{y} L{x},{y + 8} L{x - 8},{y} Z", stroke=ABYSS, sw=2.5, extra=f'opacity="0.85" filter="{s.glow(2)}"')
    return s


def draw_spectral_knight(v=""):
    """Призрачный рыцарь: полупрозрачный рыцарь в плаще, меч поднят, вместо ног — тающий туман."""
    pal = SPIRIT_CYAN
    s = Svg(270, 380)
    cx = 120
    _aura(s, cx, 180, 150, pal[1], 0.4)
    s.shadow(cx, 370, 70, 8, 0.25)
    ghost = _lin3(s, pal, 0.3, 1, 0.82)
    far = _lin3(s, (pal[1], pal[2], "#123048"), 0.3, 1, 0.8)
    s.open(f'filter="{s.glow(3.5)}"')
    # плащ
    s.path(smooth([(cx - 46, 100), (cx - 90, 200), (cx - 96, 320), (cx - 60, 300), (cx - 40, 340), (cx - 10, 250)]), far, sw=4)
    # туман вместо ног
    s.path(smooth([(cx - 40, 240), (cx + 40, 240), (cx + 34, 300), (cx + 14, 360), (cx, 330), (cx - 18, 366), (cx - 30, 300)]), ghost, sw=4)
    # торс в латах
    s.path(smooth([(cx - 50, 104), (cx + 50, 104), (cx + 44, 180), (cx + 34, 250), (cx - 34, 250), (cx - 44, 180)]), ghost, sw=4.5)
    for side in (-1, 1):
        s.path(smooth([(cx + side * 30, 94), (cx + side * 72, 104), (cx + side * 76, 138), (cx + side * 40, 134)]), ghost, sw=4)
    # шлем с плюмажем
    s.path(smooth([(cx - 26, 92), (cx - 24, 50), (cx, 34), (cx + 26, 48), (cx + 30, 92), (cx, 104)]), ghost, sw=4.5)
    s.path(smooth([(cx - 4, 36), (cx - 30, 14), (cx - 62, 18), (cx - 40, 28), (cx - 12, 44)]), far, sw=3.5)
    # рука с мечом вверх, к врагам
    s.path(smooth([(cx + 40, 112), (cx + 72, 92), (cx + 92, 70), (cx + 104, 80), (cx + 84, 108), (cx + 50, 136)]), ghost, sw=4)
    s.close()
    s.path(poly([(cx + 96, 72), (cx + 108, 66), (cx + 170, 14), (cx + 164, 8)]), s.lin([(0, "#ffffff", 0.95), (1, pal[1], 0.7)], 0, 1, 1, 0), sw=3.5)
    s.path(f"M{cx + 86},{60} L{cx + 116},{86}", stroke=INK, sw=7)
    s.path(f"M{cx + 86},{60} L{cx + 116},{86}", stroke=pal[1], sw=3)
    s.path(f"M{cx + 4},{66} L{cx + 28},{66}", stroke=pal[0], sw=5, extra=f'filter="{s.glow(3)}"')
    s.path(f"M{cx},{120} L{cx},{200} M{cx - 22},{150} L{cx + 22},{150}", stroke=pal[0], sw=3, extra='opacity="0.7"')
    _wisps(s, [(cx - 10, 360, -20, 10), (cx + 14, 356, -24, 6), (cx - 90, 316, -16, 12)], pal[1])
    return s


def draw_loyal_guard(v=""):
    """«Верный-до-Конца»: страж Цитадели с ростовым щитом к врагам и копьём."""
    s = Svg(270, 360)
    cx = 110
    s.shadow(cx + 20, 350, 96, 10, 0.45)
    steel = _lin3(s, STEEL, 0.4, 1)
    steel2 = _lin3(s, (STEEL[1], STEEL[2], "#1a2028"), 0.4, 1)
    blue = s.lin([(0, CITADEL_BLUE[0]), (1, CITADEL_BLUE[1])])
    s.open(f'filter="{s.grain(0.16, 0.9, 43)}"')
    # копьё за щитом
    s.path(f"M{cx - 20},{340} L{cx + 70},{20}", stroke=INK, sw=10)
    s.path(f"M{cx - 20},{340} L{cx + 70},{20}", stroke="#6b4a30", sw=5)
    s.path(poly([(cx + 62, 30), (cx + 82, -4), (cx + 80, 36)]), steel, sw=3)
    # ноги
    for side, fill in ((-1, steel2), (1, steel)):
        x = cx + side * 22
        s.path(poly([(x - 16, 226), (x + 16, 226), (x + 14, 300), (x + 24, 338), (x - 18, 338), (x - 14, 300)]), fill, sw=4)
    # сюрко
    s.path(smooth([(cx - 44, 110), (cx + 44, 110), (cx + 50, 200), (cx + 40, 250), (cx - 40, 250), (cx - 50, 200)]), blue, sw=4.5)
    s.path(f"M{cx},{120} L{cx},{240}", stroke="#e8eef6", sw=4)
    s.path(f"M{cx - 30},{160} L{cx + 30},{160}", stroke="#e8eef6", sw=4)
    # шлем-салад
    s.path(smooth([(cx - 26, 96), (cx - 24, 56), (cx, 44), (cx + 26, 56), (cx + 34, 92), (cx + 4, 104)]), steel, sw=4.5)
    s.path(f"M{cx + 2},{76} L{cx + 32},{76}", stroke=INK, sw=5)
    s.path(smooth([(cx - 4, 46), (cx - 16, 28), (cx - 34, 26), (cx - 18, 40)]), blue, sw=3)
    # ростовой щит к врагам
    sh = [(cx + 40, 96), (cx + 124, 92), (cx + 128, 262), (cx + 84, 312), (cx + 42, 264)]
    s.path(poly(sh), s.lin([(0, CITADEL_BLUE[0]), (1, CITADEL_BLUE[1])], 0, 0, 1, 1), sw=5)
    s.path(poly(sh), "none", "#d7dee6", 4)
    s.path(f"M{cx + 84},{110} L{cx + 84},{290} M{cx + 52},{170} L{cx + 118},{170}", stroke="#e8eef6", sw=7)
    s.circle(cx + 84, 170, 16, s.rad([(0, "#ffffff"), (1, "#9aa7b2")]), INK, 3)
    s.close()
    return s


# ключ → функция; draw_monsters.py регистрирует их в общем реестре (вариант один — "").
SUMMONS = {
    "summon_spirit_wolf": draw_spirit_wolf,
    "summon_beast_wolf": draw_beast_wolf,
    "summon_beast_sabertooth": draw_beast_sabertooth,
    "summon_beast_bear": draw_beast_bear,
    "summon_flame_atronach": draw_flame_atronach,
    "summon_frost_atronach": draw_frost_atronach,
    "summon_storm_atronach": draw_storm_atronach,
    "summon_raised_dead": draw_raised_dead,
    "summon_abyss_guardian": draw_abyss_guardian,
    "summon_spectral_knight": draw_spectral_knight,
    "summon_loyal_guard": draw_loyal_guard,
    "summon_bone_thrall": draw_bone_thrall,
}
