"""Облики героя (M6 п. 3, docs/design/06 §5) и Знаки Путей → art/monsters/hero_*.svg, art/ui/path_signs/*.svg.

Облики вампира и оборотня — полная замена модели героя (Transformations/HeroLooks.cs): статичный спрайт + код-анимации
OwnArtAnimator. Герой стоит слева и смотрит ВПРАВО (к врагам), как призывы summons.py. Варианты — на том же холсте
(смена картинки не сдвигает хитбокс): вампир «lord» (Стойка Кровавого лорда), оборотень «beast» (Звериная форма).
Лич и Заводное сердце берут ванильные модели Некробиндер и Дефекта — здесь не рисуются.

Знак Пути — знамя за спиной героя (HeroOverlays): вымпел цвета Пути с эмблемой, древко слева, ласточкин хвост снизу.
"""
from __future__ import annotations

from svgkit import INK, Svg, poly, smooth, star

HERO_W, HERO_H = 300, 400

# вампир
CLOAK = ("#7a1a2c", "#3e0a18", "#16040a")
LINING = ("#ff5a6a", "#b0122a")
DOUBLET = ("#4a3a5c", "#2a1f38", "#120c1a")
SKIN_PALE = ("#f6f2f4", "#d9d2e0", "#9a90a8")
VAMP_EYE = "#ff2a3c"
# оборотень
LEATHER = ("#a8784a", "#6e4a2a", "#2e1d10")
PELT = ("#b8aa98", "#7c6c5c", "#3a3028")
FUR = ("#9c8a78", "#5e4e40", "#241c16")
SKIN_TAN = ("#f0c8a0", "#c89470", "#7a5038")
WOLF_EYE = "#ffd23a"


def _lin3(s: Svg, pal, x2=0.3, y2=1, alpha=1.0):
    return s.lin([(0, pal[0], alpha), (0.55, pal[1], alpha), (1, pal[2], alpha)], 0, 0, x2, y2)


def _aura(s: Svg, cx, cy, r, color, alpha=0.4):
    r = min(r, cx, cy, s.w - cx, s.h - cy)
    s.circle(cx, cy, r, s.rad([(0, color, alpha), (0.6, color, alpha * 0.35), (1, color, 0)]))


# ═════════════════════════ вампир ═════════════════════════

def draw_hero_vampire(v=""):
    """Вампир: бледный аристократ в плаще со стоячим воротником, алые глаза, клыки, рапира к врагам.
    lord — Кровавый лорд: плащ раскрыт крыльями нетопыря, багровый ореол и капли крови."""
    lord = v == "lord"
    s = Svg(HERO_W, HERO_H)
    cx = 128
    if lord:
        _aura(s, cx + 10, 200, 190, "#ff2a3c", 0.38)
    s.shadow(cx + 10, 390, 90, 9, 0.45)
    cloak = _lin3(s, CLOAK, 0.4, 1)
    lining = s.lin([(0, LINING[0]), (1, LINING[1])], 0, 0, 0.3, 1)
    doublet = _lin3(s, DOUBLET, 0.4, 1)
    skin = s.rad([(0, SKIN_PALE[0]), (0.7, SKIN_PALE[1]), (1, SKIN_PALE[2])], 0.6, 0.35, 0.7)
    boot = s.lin([(0, "#3a3040"), (1, "#0e0a12")])
    s.open(f'filter="{s.grain(0.14, 0.9, 61)}"')
    if lord:
        # крылья нетопыря из плаща — за спиной; дальнее крыло меньше
        for side, scale, fill in ((1, 0.55, s.lin([(0, "#4a0c1c"), (1, "#14040a")])), (-1, 0.92, cloak)):
            ox, oy = cx + side * 10, 120
            pts = [(ox, oy), (ox + side * 60 * scale, oy - 70 * scale), (ox + side * 118 * scale, oy - 96 * scale),
                   (ox + side * 112 * scale, oy - 30 * scale), (ox + side * 124 * scale, oy + 20 * scale),
                   (ox + side * 96 * scale, oy + 6 * scale), (ox + side * 100 * scale, oy + 70 * scale),
                   (ox + side * 70 * scale, oy + 44 * scale), (ox + side * 56 * scale, oy + 120 * scale),
                   (ox + side * 34 * scale, oy + 80 * scale), (ox + side * 16, oy + 160 * scale)]
            s.path(poly(pts), fill, sw=4.5)
            for k in (1, 2, 4):
                s.path(f"M{ox},{oy} L{pts[k * 2][0]:.1f},{pts[k * 2][1]:.1f}", stroke="#ff5a6a", sw=2.5, extra='opacity="0.55"')
    # плащ сзади
    s.path(smooth([(cx - 40, 108), (cx - 86, 220), (cx - 98, 330), (cx - 92, 386), (cx - 44, 372), (cx - 6, 386),
                   (cx + 34, 372), (cx + 62, 300), (cx + 48, 112)]), cloak, sw=5)
    s.path(smooth([(cx - 30, 120), (cx - 66, 230), (cx - 74, 360), (cx - 44, 352), (cx - 20, 250)]), lining, sw=3,
           extra='opacity="0.9"')
    # ноги и сапоги
    for side, shade in ((-1, "#1e1824"), (1, "#2c2434")):
        x = cx + side * 18
        s.path(poly([(x - 14, 256), (x + 14, 256), (x + 12, 330), (x - 12, 330)]), shade, sw=4)
        s.path(poly([(x - 15, 322), (x + 14, 322), (x + 16, 372), (x + 32, 384), (x - 16, 384)]), boot, sw=4)
    # колет
    s.path(smooth([(cx - 38, 106), (cx + 38, 106), (cx + 36, 190), (cx + 32, 268), (cx - 32, 268), (cx - 36, 190)]), doublet, sw=4.5)
    for y in (150, 180, 210, 240):
        s.circle(cx + 4, y, 3.4, "#c9a24a", INK, 1.5)
    # жабо и брошь
    s.path(poly([(cx - 12, 104), (cx + 18, 104), (cx + 10, 146), (cx + 2, 138), (cx - 6, 148)]), "#f4eef0", sw=3)
    s.circle(cx + 3, 112, 7, s.rad([(0, "#ffb0b8"), (1, "#a0102a")]), INK, 2.5)
    # пояс
    s.path(poly([(cx - 36, 226), (cx + 36, 226), (cx + 35, 238), (cx - 35, 238)]), "#1a1020", sw=3)
    s.path(poly([(cx - 4, 224), (cx + 12, 224), (cx + 12, 240), (cx - 4, 240)]), "#c9a24a", sw=2.5)
    # стоячий воротник
    for side in (-1, 1):
        s.path(poly([(cx + side * 40, 116), (cx + side * 62, 44), (cx + side * 52, 78), (cx + side * 18, 104)]),
               cloak, sw=4)
        s.path(poly([(cx + side * 36, 108), (cx + side * 54, 60), (cx + side * 46, 86), (cx + side * 20, 102)]),
               lining, stroke=None)
    # голова в 3/4 вправо
    hx, hy = cx + 6, 70
    s.path(smooth([(hx - 22, hy - 6), (hx - 16, hy - 30), (hx + 6, hy - 38), (hx + 24, hy - 26), (hx + 30, hy - 4),
                   (hx + 28, hy + 16), (hx + 18, hy + 32), (hx + 4, hy + 36), (hx - 12, hy + 26), (hx - 20, hy + 10)]), skin, sw=4)
    # волосы назад, мыс вдовы
    s.path(smooth([(hx - 26, hy + 6), (hx - 30, hy - 22), (hx - 12, hy - 44), (hx + 14, hy - 46), (hx + 30, hy - 30),
                   (hx + 18, hy - 26), (hx + 8, hy - 16), (hx - 2, hy - 28), (hx - 12, hy - 14), (hx - 16, hy + 8)]),
           s.lin([(0, "#3a3048"), (1, "#0c0810")]), sw=4)
    # ухо острое
    s.path(poly([(hx - 10, hy - 2), (hx - 26, hy - 18), (hx - 14, hy + 10)]), skin, sw=3)
    # брови, глаза, рот с клыками
    s.path(f"M{hx + 2},{hy - 12} L{hx + 14},{hy - 8} M{hx + 20},{hy - 9} L{hx + 28},{hy - 12}", stroke=INK, sw=3)
    eye_r = 3.6 if lord else 3.0
    s.eye(hx + 10, hy - 2, eye_r, VAMP_EYE, "#fff0f0")
    s.eye(hx + 24, hy - 2, eye_r * 0.85, VAMP_EYE, "#fff0f0")
    s.path(f"M{hx + 26},{hy + 4} L{hx + 32},{hy + 12} L{hx + 26},{hy + 14}", stroke=INK, sw=2.5)
    s.path(f"M{hx + 10},{hy + 22} Q{hx + 18},{hy + 26} {hx + 26},{hy + 21}", stroke=INK, sw=3)
    for fx in (hx + 13, hx + 22):
        s.path(poly([(fx - 2.5, hy + 23), (fx + 2.5, hy + 23), (fx, hy + 30)]), "#ffffff", INK, 1.2)
    # рука с рапирой к врагам
    arm = [(cx + 30, 112), (cx + 56, 140), (cx + 82, 166), (cx + 92, 158), (cx + 64, 126), (cx + 42, 104)]
    s.path(smooth(arm), doublet, sw=4)
    s.path(smooth([(cx + 78, 160), (cx + 90, 150), (cx + 100, 160), (cx + 90, 172)]), "#f4eef0", sw=3)
    s.close()
    # рапира
    s.path(f"M{cx + 96},{162} L{cx + 166},{110}", stroke=INK, sw=6)
    s.path(f"M{cx + 96},{162} L{cx + 166},{110}", stroke="#e8eef4", sw=2.5)
    s.path(smooth([(cx + 84, 168), (cx + 92, 150), (cx + 108, 152), (cx + 104, 170)]), "none", "#c9a24a", 3.5)
    s.circle(cx + 90, 168, 4.5, "#c9a24a", INK, 2)
    if lord:
        # капли крови вокруг
        for (x, y, r) in ((cx - 78, 170, 9), (cx + 118, 70, 8), (cx + 146, 214, 10), (cx - 30, 40, 7), (cx + 78, 258, 8)):
            s.path(f"M{x},{y - r * 2} Q{x + r},{y - r * 0.2} {x},{y + r} Q{x - r},{y - r * 0.2} {x},{y - r * 2} Z",
                   s.rad([(0, "#ff8a96"), (1, "#a0102a")]), INK, 2, extra=f'filter="{s.glow(2)}"')
    return s


# ═════════════════════════ оборотень ═════════════════════════

def _werewolf_human(s: Svg, cx):
    leather = _lin3(s, LEATHER, 0.4, 1)
    pelt = _lin3(s, PELT, 0.3, 1)
    skin = s.rad([(0, SKIN_TAN[0]), (0.7, SKIN_TAN[1]), (1, SKIN_TAN[2])], 0.6, 0.35, 0.7)
    trousers = s.lin([(0, "#4e4438"), (1, "#1c1812")])
    s.open(f'filter="{s.grain(0.18, 0.9, 67)}"')
    # шкура-плащ сзади
    s.path(smooth([(cx - 40, 100), (cx - 80, 180), (cx - 84, 290), (cx - 66, 276), (cx - 58, 310), (cx - 40, 290),
                   (cx - 26, 316), (cx - 10, 260), (cx + 40, 104)]), pelt, sw=4.5)
    for k in range(5):
        s.path(f"M{cx - 76 + k * 12},{286 + (k % 2) * 10} l-4,14", stroke=PELT[2], sw=3)
    # ноги: рваные штаны, босые ступни с когтями
    for side, shade in ((-1, "#2a241c"), (1, trousers)):
        x = cx + side * 20
        s.path(poly([(x - 16, 246), (x + 16, 246), (x + 15, 344), (x + 8, 336), (x + 2, 348), (x - 6, 338), (x - 14, 346)]),
               shade, sw=4)
        s.path(smooth([(x - 13, 344), (x + 14, 342), (x + 22, 372), (x + 34, 384), (x - 14, 384), (x - 16, 362)]), skin, sw=4)
        for k in range(3):
            s.path(f"M{x + 18 + k * 6},{384} l5,-2", stroke="#f4ece0", sw=3)
    # торс: кожаный колет нараспашку, грубая рубаха
    s.path(smooth([(cx - 40, 102), (cx + 40, 102), (cx + 40, 180), (cx + 34, 256), (cx - 34, 256), (cx - 40, 180)]), leather, sw=4.5)
    s.path(poly([(cx - 8, 104), (cx + 16, 104), (cx + 12, 230), (cx - 4, 230)]), "#8a7a62", sw=3)
    s.path(f"M{cx - 4},{130} l14,8 M{cx - 2},{160} l12,8 M{cx},{190} l10,8", stroke="#3a2a1a", sw=2.5)
    # перевязь с ножнами
    s.path(f"M{cx - 38},{110} L{cx + 36},{226}", stroke=INK, sw=11)
    s.path(f"M{cx - 38},{110} L{cx + 36},{226}", stroke="#5a3a22", sw=7)
    s.path(poly([(cx - 36, 214), (cx + 36, 214), (cx + 35, 228), (cx - 35, 228)]), "#2a1c10", sw=3)
    # дальняя рука — когтистая, вниз
    s.path(smooth([(cx - 36, 112), (cx - 52, 170), (cx - 50, 220), (cx - 38, 224), (cx - 34, 168)]), leather, sw=4)
    s.path(smooth([(cx - 54, 218), (cx - 34, 216), (cx - 32, 236), (cx - 52, 240)]), skin, sw=3)
    for k in range(3):
        s.path(f"M{cx - 50 + k * 7},{240} l-1,10", stroke="#f4ece0", sw=3)
    # голова: небритый, растрёпанный, жёлтые глаза
    hx, hy = cx + 6, 72
    s.path(smooth([(hx - 20, hy - 6), (hx - 14, hy - 28), (hx + 6, hy - 34), (hx + 24, hy - 24), (hx + 30, hy - 2),
                   (hx + 28, hy + 18), (hx + 16, hy + 32), (hx, hy + 34), (hx - 14, hy + 24), (hx - 20, hy + 8)]), skin, sw=4)
    # щетина и бакенбарды
    s.path(smooth([(hx - 14, hy + 4), (hx - 4, hy + 30), (hx + 16, hy + 34), (hx + 28, hy + 18), (hx + 22, hy + 30),
                   (hx + 4, hy + 38), (hx - 12, hy + 26)]), s.lin([(0, "#6a5040", 0.85), (1, "#3a2a1e", 0.85)]), sw=2.5)
    s.path(f"M{hx + 2},{hy - 12} L{hx + 16},{hy - 6} M{hx + 20},{hy - 7} L{hx + 28},{hy - 12}", stroke=INK, sw=3.5)
    s.eye(hx + 11, hy - 1, 3.2, WOLF_EYE, "#fffbe0")
    s.eye(hx + 24, hy - 1, 2.8, WOLF_EYE, "#fffbe0")
    s.path(f"M{hx + 26},{hy + 4} L{hx + 33},{hy + 12} L{hx + 26},{hy + 14}", stroke=INK, sw=2.5)
    s.path(f"M{hx + 10},{hy + 22} L{hx + 26},{hy + 20}", stroke=INK, sw=3)
    # капюшон — волчья голова на макушке, морда вперёд
    s.path(smooth([(hx - 32, hy + 10), (hx - 34, hy - 26), (hx - 14, hy - 48), (hx + 16, hy - 52), (hx + 44, hy - 40),
                   (hx + 62, hy - 30), (hx + 58, hy - 20), (hx + 34, hy - 20), (hx + 22, hy - 30), (hx - 2, hy - 30),
                   (hx - 18, hy - 12), (hx - 20, hy + 14)]), pelt, sw=4)
    s.path(poly([(hx - 6, hy - 46), (hx - 2, hy - 70), (hx + 12, hy - 50)]), pelt, sw=3.5)
    s.circle(hx + 60, hy - 27, 3.5, INK)
    s.circle(hx + 26, hy - 38, 3, "#1a1410")
    # ближняя рука с охотничьим ножом к врагам
    s.path(smooth([(cx + 30, 110), (cx + 58, 140), (cx + 80, 158), (cx + 90, 148), (cx + 64, 120), (cx + 42, 100)]), leather, sw=4)
    s.path(smooth([(cx + 76, 152), (cx + 92, 140), (cx + 102, 154), (cx + 88, 166)]), skin, sw=3)
    s.close()
    s.path(poly([(cx + 96, 150), (cx + 150, 116), (cx + 154, 122), (cx + 102, 158)]), s.lin([(0, "#f4f8fa"), (1, "#8a96a0")]), sw=3)
    s.path(f"M{cx + 88},{140} L{cx + 104},{166}", stroke=INK, sw=6)
    s.path(f"M{cx + 88},{140} L{cx + 104},{166}", stroke="#8a6a3a", sw=3)


def _werewolf_beast(s: Svg, cx):
    fur = _lin3(s, FUR, 0.4, 1)
    far = _lin3(s, (FUR[1], FUR[2], "#120e0a"), 0.3, 1)
    belly = s.lin([(0, "#c8b8a4"), (1, "#7a6a58")])
    _aura(s, cx + 20, 200, 180, "#7a8cff", 0.18)
    s.open(f'filter="{s.grain(0.2, 0.9, 71)}"')
    # хвост
    s.path(smooth([(cx - 60, 250), (cx - 92, 240), (cx - 100, 204), (cx - 90, 196), (cx - 82, 216), (cx - 58, 228)]), far, sw=4)
    # дальняя нога (пальцеходящая) и дальняя лапа
    s.path(smooth([(cx - 40, 240), (cx - 64, 300), (cx - 42, 340), (cx - 56, 384), (cx - 14, 384), (cx - 18, 340),
                   (cx - 2, 290), (cx - 6, 246)]), far, sw=4)
    s.path(smooth([(cx + 24, 120), (cx + 74, 150), (cx + 112, 196), (cx + 124, 224), (cx + 106, 228), (cx + 70, 184),
                   (cx + 20, 160)]), far, sw=4)
    for k in range(4):
        s.path(f"M{cx + 106 + k * 5},{224} l{6 + k},{14}", stroke="#f4ece0", sw=3.5)
    # туловище — сгорбленное, загривок высоко
    s.path(smooth([(cx - 66, 236), (cx - 74, 170), (cx - 50, 100), (cx - 6, 62), (cx + 44, 60), (cx + 76, 86),
                   (cx + 80, 140), (cx + 54, 210), (cx + 20, 262), (cx - 34, 270)]), fur, sw=5)
    s.path(smooth([(cx + 6, 150), (cx + 56, 140), (cx + 50, 200), (cx + 18, 246), (cx - 10, 236)]), belly, sw=3)
    # шерсть на загривке
    s.path(f"M{cx - 60},{150} l-12,-10 l10,-4 l-10,-16 l14,2 l-6,-18 l16,6 l-2,-20 l16,10 l4,-18 l12,14 l10,-16 l8,16",
           fur, sw=3.5)
    # ближняя нога
    s.path(smooth([(cx - 30, 230), (cx + 2, 296), (cx - 10, 340), (cx + 6, 384), (cx + 52, 384), (cx + 30, 350),
                   (cx + 38, 292), (cx + 20, 240)]), fur, sw=4.5)
    for k in range(3):
        s.path(f"M{cx + 40 + k * 6},{384} l6,-1", stroke="#f4ece0", sw=3.5)
    # ближняя рука — когти к врагам
    s.path(smooth([(cx + 40, 104), (cx + 92, 126), (cx + 140, 170), (cx + 158, 196), (cx + 138, 202), (cx + 96, 168),
                   (cx + 44, 146)]), fur, sw=4.5)
    for k in range(4):
        s.path(f"M{cx + 140 + k * 5},{198 - k * 3} q8,8 10,20", stroke=INK, sw=6)
        s.path(f"M{cx + 140 + k * 5},{198 - k * 3} q8,8 10,20", stroke="#f4ece0", sw=3)
    # голова — вытянутая морда вправо
    hx, hy = cx + 70, 84
    s.path(smooth([(hx - 26, hy - 4), (hx - 14, hy - 30), (hx + 16, hy - 34), (hx + 42, hy - 22), (hx + 76, hy - 10),
                   (hx + 92, hy + 2), (hx + 88, hy + 14), (hx + 60, hy + 18), (hx + 72, hy + 30), (hx + 40, hy + 34),
                   (hx + 6, hy + 30), (hx - 20, hy + 18)]), fur, sw=4.5)
    # уши
    s.path(poly([(hx - 10, hy - 26), (hx - 14, hy - 62), (hx + 8, hy - 32)]), fur, sw=3.5)
    s.path(poly([(hx + 4, hy - 30), (hx + 6, hy - 58), (hx + 20, hy - 30)]), far, sw=3)
    # пасть с клыками
    s.path(f"M{hx + 30},{hy + 18} L{hx + 86},{hy + 10}", stroke=INK, sw=3.5)
    for k in range(4):
        x = hx + 40 + k * 11
        s.path(poly([(x, hy + 15 - k * 1.2), (x + 5, hy + 15 - k * 1.2), (x + 2, hy + 25 - k)]), "#ffffff", INK, 1.2)
    s.path(poly([(hx + 52, hy + 19), (hx + 57, hy + 19), (hx + 54, hy + 10)]), "#ffffff", INK, 1.2)
    s.circle(hx + 92, hy + 2, 5, INK)
    s.path(f"M{hx + 12},{hy - 16} L{hx + 34},{hy - 12}", stroke=INK, sw=3.5)
    s.eye(hx + 26, hy - 6, 4.2, WOLF_EYE, "#fffbe0")
    s.close()


def draw_hero_werewolf(v=""):
    """Оборотень: проклятый охотник в волчьей шкуре (жёлтые глаза, когти, нож). beast — Звериная форма: сгорбленный
    волколак, морда и когти к врагам, холодный лунный ореол."""
    s = Svg(HERO_W, HERO_H)
    if v == "beast":
        cx = 110
        s.shadow(cx + 20, 390, 116, 10, 0.5)
        _werewolf_beast(s, cx)
    else:
        cx = 126
        s.shadow(cx + 6, 390, 84, 9, 0.45)
        _werewolf_human(s, cx)
    return s


HEROES = {
    "hero_vampire": (draw_hero_vampire, ("", "lord")),
    "hero_werewolf": (draw_hero_werewolf, ("", "beast")),
}


# ═════════════════════════ Знаки Путей — знамёна за спиной ═════════════════════════

SIGN_W, SIGN_H = 96, 150


def _banner(color: str, dark: str, emblem) -> Svg:
    s = Svg(SIGN_W, SIGN_H)
    # древко
    s.path(f"M14,{SIGN_H - 4} L14,8", stroke=INK, sw=8)
    s.path(f"M14,{SIGN_H - 4} L14,8", stroke="#7a5432", sw=4)
    s.circle(14, 8, 6, s.rad([(0, "#fff0b0"), (1, "#a0782a")]), INK, 2.5)
    # перекладина и полотнище с ласточкиным хвостом
    s.path("M10,18 L90,18", stroke=INK, sw=6)
    s.path("M10,18 L90,18", stroke="#7a5432", sw=3)
    cloth = [(18, 20), (86, 20), (88, 124), (68, 108), (52, 136), (36, 108), (18, 124)]
    s.path(poly(cloth), s.lin([(0, color), (1, dark)], 0, 0, 0.3, 1), sw=4)
    s.path(poly([(24, 26), (80, 26), (82, 112), (68, 98), (52, 124), (36, 98), (24, 112)]), "none", "#f2e6c8", 2.2,
           extra='opacity="0.75"')
    emblem(s, 52, 66)
    return s


def _e_sun(s, x, y):
    s.path(star(x, y, 20, 11, 8), "#fff4c2", INK, 2.5)
    s.circle(x, y, 9, "#ffd35a", INK, 2.5)


def _e_spiked_helm(s, x, y):
    s.path(smooth([(x - 16, y + 18), (x - 16, y - 6), (x, y - 18), (x + 16, y - 6), (x + 16, y + 18)], closed=False) + " Z",
           "#2a2a30", INK, 2.5)
    for dx in (-12, 0, 12):
        s.path(poly([(x + dx - 4, y - 10 - abs(dx) * 0.3), (x + dx, y - 28 + abs(dx) * 0.5), (x + dx + 4, y - 10 - abs(dx) * 0.3)]),
               "#c8ccd4", INK, 2)
    s.path(f"M{x - 10},{y + 2} L{x + 10},{y + 2}", stroke="#ff3b4e", sw=3.5)


def _e_crown(s, x, y):
    s.path(poly([(x - 20, y + 12), (x - 22, y - 12), (x - 10, y), (x, y - 18), (x + 10, y), (x + 22, y - 12), (x + 20, y + 12)]),
           "#ffd35a", INK, 2.5)
    for dx in (-12, 0, 12):
        s.circle(x + dx, y + 6, 2.6, "#ff3b4e")


def _e_horns(s, x, y):
    for side in (-1, 1):
        s.path(f"M{x + side * 6},{y + 8} Q{x + side * 26},{y + 6} {x + side * 22},{y - 22} Q{x + side * 16},{y - 2} {x + side * 4},{y - 2} Z",
               "#f2e6c8", INK, 2.5)
    s.path(smooth([(x - 10, y - 4), (x + 10, y - 4), (x + 12, y + 14), (x, y + 22), (x - 12, y + 14)]), "#8a5a32", INK, 2.5)


def _e_palm(s, x, y):
    s.circle(x, y, 20, "none", "#fff4c2", 4)
    s.circle(x, y, 20, "none", INK, 1.5)
    s.path(smooth([(x - 8, y + 14), (x - 10, y - 4), (x - 6, y - 14), (x + 6, y - 14), (x + 10, y - 2), (x + 8, y + 14)]),
           "#f0c8a0", INK, 2.5)
    s.path(f"M{x - 3},{y - 12} L{x - 3},{y} M{x + 3},{y - 12} L{x + 3},{y}", stroke=INK, sw=1.6)


def _e_mask(s, x, y):
    s.path(smooth([(x - 18, y - 16), (x + 18, y - 16), (x + 18, y + 4), (x + 8, y + 20), (x, y + 22), (x - 8, y + 20), (x - 18, y + 4)]),
           "#e8e4ea", INK, 2.5)
    for side in (-1, 1):
        s.path(f"M{x + side * 4},{y - 4} L{x + side * 14},{y - 8}", stroke=INK, sw=4)


def _e_vial(s, x, y):
    s.path(f"M{x - 5},{y - 22} L{x + 5},{y - 22} L{x + 5},{y - 10} Q{x + 18},{y - 2} {x + 16},{y + 10} Q{x + 12},{y + 22} {x},{y + 22} "
           f"Q{x - 12},{y + 22} {x - 16},{y + 10} Q{x - 18},{y - 2} {x - 5},{y - 10} Z", "#e8f4ff", INK, 2.5)
    s.path(f"M{x - 15},{y + 6} Q{x},{y + 2} {x + 15},{y + 6} Q{x + 12},{y + 20} {x},{y + 20} Q{x - 12},{y + 20} {x - 15},{y + 6} Z",
           "#9cff5a", stroke=None)
    s.circle(x - 4, y + 12, 2.5, "#e8ffd0")


def _e_arrow(s, x, y):
    s.path(f"M{x - 18},{y + 18} L{x + 14},{y - 14}", stroke=INK, sw=6)
    s.path(f"M{x - 18},{y + 18} L{x + 14},{y - 14}", stroke="#e8eef4", sw=2.5)
    s.path(poly([(x + 20, y - 20), (x + 6, y - 16), (x + 16, y - 6)]), "#8fe3ff", INK, 2)
    for k in (0, 6):
        s.path(f"M{x - 16 + k},{y + 16 - k} l-8,-2 M{x - 16 + k},{y + 16 - k} l2,8", stroke="#8fe3ff", sw=2.5)


def _e_flame(s, x, y):
    s.path(f"M{x},{y - 24} Q{x + 22},{y} {x + 12},{y + 18} Q{x},{y + 26} {x - 12},{y + 18} Q{x - 22},{y} {x - 6},{y - 8} "
           f"Q{x - 4},{y + 2} {x + 2},{y + 2} Q{x + 6},{y - 10} {x},{y - 24} Z", "#ffad3a", INK, 2.5)
    s.path(f"M{x},{y - 2} Q{x + 9},{y + 8} {x + 4},{y + 16} Q{x},{y + 20} {x - 4},{y + 16} Q{x - 8},{y + 8} {x},{y - 2} Z",
           "#fff4c2", stroke=None)


def _e_rune(s, x, y):
    s.path(poly([(x, y - 22), (x + 18, y), (x, y + 22), (x - 18, y)]), "#d8b8ff", INK, 2.5)
    s.path(f"M{x},{y - 14} L{x},{y + 14} M{x - 8},{y - 4} L{x + 8},{y + 4} M{x + 8},{y - 4} L{x - 8},{y + 4}", stroke=INK, sw=2.5)


def _e_mirror(s, x, y):
    s.circle(x, y, 19, s.rad([(0, "#ffffff"), (0.6, "#cfeeff"), (1, "#6aa0c8")], 0.35, 0.3), INK, 3)
    s.path(f"M{x - 10},{y - 4} L{x - 2},{y - 12} M{x - 8},{y + 4} L{x + 6},{y - 10}", stroke="#ffffff", sw=3)


def _e_bell(s, x, y):
    s.path(f"M{x - 4},{y - 22} L{x + 4},{y - 22} L{x + 4},{y - 16} Q{x + 14},{y - 12} {x + 14},{y + 4} L{x + 20},{y + 14} "
           f"L{x - 20},{y + 14} L{x - 14},{y + 4} Q{x - 14},{y - 12} {x - 4},{y - 16} Z", "#d8f4ee", INK, 2.5)
    s.circle(x, y + 19, 4.5, "#8ad8c8", INK, 2)
    for side in (-1, 1):
        s.path(f"M{x + side * 22},{y - 6} q{side * 6},6 0,12", stroke="#d8f4ee", sw=2.5)


# Ключ файла = snake_case пути (TransformationRules.PathSignKey).
PATH_SIGNS = {
    "paladin": ("#ffe066", "#a07a1a", _e_sun),
    "black_knight": ("#5a0e1c", "#1a0408", _e_spiked_helm),
    "champion": ("#e6332a", "#7a1410", _e_crown),
    "barbarian": ("#d8731f", "#6e3208", _e_horns),
    "monk": ("#f2d79a", "#9a7a3a", _e_palm),
    "assassin": ("#3a3a44", "#101014", _e_mask),
    "blightbringer": ("#6ad82a", "#24600a", _e_vial),
    "mystic_archer": ("#7aa8ff", "#24448a", _e_arrow),
    "element_lord": ("#ff8a33", "#8a2a0a", _e_flame),
    "battlemage": ("#a866ff", "#44187a", _e_rune),
    "reflector": ("#cdeeff", "#5a86a8", _e_mirror),
    "medium": ("#8ae0d2", "#2a7a6e", _e_bell),
}


def draw_path_sign(key: str) -> Svg:
    color, dark, emblem = PATH_SIGNS[key]
    return _banner(color, dark, emblem)
