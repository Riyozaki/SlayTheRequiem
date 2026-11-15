"""Спрайты супербоссов Багрового портала (M7 п. 1, docs/design/11 §6) → art/monsters/{key}.svg.

Восемь супербоссов + Рыцарь-эхо (элита Безымянного предела — призрачный отзвук Чёрного Странника). Враги стоят справа
и смотрят ВЛЕВО (к героям), как боссы draw_monsters.py. Низ фигуры — у нижнего края холста. Варианты — на том же
холсте (смена картинки не сдвигает хитбокс), их показывает код боя (Monsters/Superbosses.cs → ShowArt):

    black_wanderer_guard    — Стойка блока (герои не атаковали: Латы +3)
    blood_court_lord_cocoon — кокон (Неуязвим, пока живы слуги-гаргульи)
    world_eater_landed      — сел на землю (без Полёта)
    nameless_shadow_exposed — неуязвимость снята (убита душа)

Свита супербоссов — ванильные модели с фракционной перекраской (Core/SuperbossLookRules.MinionTint), здесь не рисуется.
Палитра — art spec §1.1 и §3.10: толстый тёмный контур, 2–3 тона, контровой цвет фракции, светящиеся глаза и руны.
"""
from __future__ import annotations

import math

from svgkit import INK, Svg, poly, smooth, star

# фракции
VOID = ("#5a4a78", "#2a1f40", "#0c0814")          # Чёрный Странник, Тень
SOUL = "#9fd8ff"                                   # соли душ
FROST = ("#e8fbff", "#8fe3ff", "#2f6f9a")
BONE = ("#f6f0dc", "#cfc29c", "#6f6248")
BRASS = ("#ffe2a0", "#d09a48", "#6a4318")
IRON = ("#c4ccd4", "#6f7a86", "#2a323c")
EMBER = "#ff6a2a"
EMBER_CORE = "#ffd27a"
ABYSS = ("#ff7a5a", "#b0182a", "#3a0610")
ABYSS_GLOW = "#ff3b2e"
HELL_VIOLET = "#c060ff"
BLOOD = ("#ff5a6a", "#9a0c22", "#30040c")
NIGHT = ("#4a3a5c", "#22182e", "#0a060e")
ARCANE = ("#f1f0ff", "#9aa8ff", "#3a3f8a")
STORM = "#c8a8ff"
DRAGON = ("#4a4a58", "#1e1c26", "#08070c")
MAGMA = "#ff8a2a"


def _lin3(s: Svg, pal, x2=0.3, y2=1, alpha=1.0):
    return s.lin([(0, pal[0], alpha), (0.55, pal[1], alpha), (1, pal[2], alpha)], 0, 0, x2, y2)


def _aura(s: Svg, cx, cy, r, color, alpha=0.4):
    r = min(r, cx, cy, s.w - cx, s.h - cy)
    s.circle(cx, cy, r, s.rad([(0, color, alpha), (0.6, color, alpha * 0.35), (1, color, 0)]))


def _wisps(s: Svg, pts, color, sw=5, opacity=0.55):
    blur = s.blur(1.8)
    for (x, y, dx, dy) in pts:
        s.path(f"M{x},{y} q{dx * 0.5},{dy * 0.2} {dx},{dy}", stroke=color, sw=sw, extra=f'opacity="{opacity}" filter="{blur}"')


def _ground(s: Svg, cx, y, rx, top, bottom):
    s.path(smooth([(cx - rx, y + 12), (cx - rx * 0.7, y - 10), (cx, y - 18), (cx + rx * 0.7, y - 10), (cx + rx, y + 12)],
                  closed=False) + " Z", s.lin([(0, top), (1, bottom)]), sw=4)


# ═════════════════════════ Акт 1 — Чёрный Странник ═════════════════════════

def _wanderer(v: str, echo: bool) -> Svg:
    """Странствующий рыцарь в вороной броне и рваном плаще, на поясе — три склянки солей душ, двуручный клинок.
    guard — Стойка блока: клинок поперёк, голубой щит-барьер. echo — Рыцарь-эхо: тот же силуэт, полупрозрачный дух."""
    guard = v == "guard"
    s = Svg(440, 540)
    cx = 222
    alpha = 0.78 if echo else 1.0
    plate = _lin3(s, ("#7a7090", "#3a3350", "#100c18") if not echo else ("#e4f6ff", "#8fc8e8", "#2a5878"), 0.4, 1, alpha)
    cloak = _lin3(s, ("#3a2f4c", "#1a1426", "#06040a") if not echo else ("#b8e4ff", "#4a88b0", "#10304a"), 0.3, 1, alpha * 0.92)
    trim = "#c9a24a" if not echo else "#e8fbff"
    eye = SOUL if not echo else "#ffffff"
    if echo:
        _aura(s, cx, 260, 230, SOUL, 0.35)
    elif guard:
        _aura(s, cx - 40, 250, 220, SOUL, 0.3)
    s.shadow(cx, 528, 140, 13, 0.3 if echo else 0.45)
    group = f'filter="{s.glow(4)}" opacity="0.92"' if echo else f'filter="{s.grain(0.16, 0.85, 71)}"'
    s.open(group)
    # плащ сзади — рваный подол
    s.path(poly([(cx - 52, 128), (cx - 128, 260), (cx - 150, 400), (cx - 140, 520), (cx - 112, 488), (cx - 92, 524),
                 (cx - 62, 486), (cx - 30, 516), (cx + 10, 480), (cx + 48, 518), (cx + 84, 482), (cx + 118, 520),
                 (cx + 142, 470), (cx + 150, 340), (cx + 118, 220), (cx + 60, 128)]), cloak, sw=5)
    for x in (-96, -40, 30, 92):
        s.path(f"M{cx + x * 0.5},{170} Q{cx + x * 0.9},{330} {cx + x},{470}", stroke="#05030a", sw=3)
    # ноги в латах
    for side, shade in ((1, 0.85), (-1, 1.0)):
        lx = cx + side * 38
        s.path(poly([(lx - 24, 340), (lx + 22, 340), (lx + 20, 470), (lx + 30, 500), (lx + 22, 516), (lx - 34, 516),
                     (lx - 26, 490), (lx - 22, 470)]), plate, sw=4.5, extra=f'opacity="{shade}"')
        s.path(smooth([(lx - 22, 410), (lx, 398), (lx + 20, 410), (lx, 426)]), plate, sw=3.5)
    # набедренник
    s.path(poly([(cx - 76, 300), (cx + 76, 300), (cx + 90, 378), (cx + 30, 364), (cx, 384), (cx - 30, 364), (cx - 90, 378)]),
           plate, sw=4.5)
    # пояс со склянками солей душ
    s.path(poly([(cx - 80, 292), (cx + 80, 292), (cx + 80, 312), (cx - 80, 312)]), "#1a1222", sw=4)
    for i, x in enumerate((-50, -14, 22)):
        lit = not echo
        bx = cx + x
        s.path(poly([(bx - 9, 312), (bx + 9, 312), (bx + 12, 344), (bx - 12, 344)]),
               s.lin([(0, "#e8fbff", 0.9), (1, SOUL, 0.7)]) if lit else "none", sw=3)
        if lit:
            s.circle(bx, 330, 7, SOUL, extra=f'filter="{s.glow(3)}" opacity="0.85"')
        s.path(poly([(bx - 6, 304), (bx + 6, 304), (bx + 6, 313), (bx - 6, 313)]), trim, sw=2.5)
    # кираса
    s.path(smooth([(cx - 84, 160), (cx + 84, 160), (cx + 80, 236), (cx + 62, 296), (cx - 62, 296), (cx - 80, 236)]), plate, sw=5)
    s.path(f"M{cx},{168} L{cx},{290}", stroke=INK, sw=3)
    s.path(f"M{cx - 66},{232} Q{cx},{252} {cx + 66},{232}", stroke=INK, sw=3)
    s.path(f"M{cx - 50},{184} Q{cx},{198} {cx + 50},{184}", stroke=trim, sw=3)
    # наплечники
    for side in (-1, 1):
        px = cx + side * 86
        s.path(smooth([(px - 46, 176), (px - 40, 140), (px, 124), (px + 40, 140), (px + 46, 176), (px, 190)]), plate, sw=5)
        s.path(f"M{px - 36},{160} Q{px},{146} {px + 36},{160}", stroke=trim, sw=3)
    # шлем: закрытый, с узкой прорезью и гребнем
    hx, hy = cx - 8, 100
    s.path(smooth([(hx - 40, hy + 30), (hx - 44, hy - 10), (hx - 26, hy - 42), (hx + 8, hy - 52), (hx + 38, hy - 36),
                   (hx + 46, hy), (hx + 40, hy + 34), (hx, hy + 48)]), plate, sw=5)
    s.path(poly([(hx - 6, hy - 52), (hx + 10, hy - 90), (hx + 34, hy - 104), (hx + 24, hy - 72), (hx + 22, hy - 44)]),
           cloak, sw=4)
    s.path(f"M{hx - 38},{hy + 2} L{hx + 22},{hy + 2}", stroke=INK, sw=9)
    s.eye(hx - 22, hy + 2, 3.4, eye)
    s.eye(hx - 2, hy + 2, 3.0, eye)
    s.path(f"M{hx - 34},{hy + 18} L{hx + 30},{hy + 18} M{hx - 30},{hy + 28} L{hx + 26},{hy + 28}", stroke=INK, sw=2.5)
    s.close()
    # руки и двуручный клинок
    blade = s.lin([(0, "#f4f8ff"), (0.5, "#a8b4c8"), (1, "#3a4458")], 0, 0, 1, 0) if not echo else \
        s.lin([(0, "#ffffff", 0.9), (1, SOUL, 0.7)], 0, 0, 1, 0)
    if guard:
        # клинок поперёк груди, барьер перед рыцарем
        s.path(poly([(cx - 150, 196), (cx + 120, 238), (cx + 118, 252), (cx - 154, 210)]), blade, sw=4)
        s.path(poly([(cx + 104, 226), (cx + 112, 262), (cx + 124, 260), (cx + 116, 224)]), trim, sw=3)
        for side in (-1, 1):
            s.path(smooth([(cx + side * 60, 190), (cx + side * 30, 222), (cx + side * 10, 232), (cx + side * 40, 196)]),
                   plate, sw=4)
        bar = s.lin([(0, "#e8fbff", 0.0), (0.4, SOUL, 0.42), (1, "#3a7fb0", 0.1)], 0, 0, 1, 0)
        s.path(smooth([(cx - 120, 90), (cx - 170, 220), (cx - 172, 360), (cx - 130, 470), (cx - 112, 460),
                       (cx - 146, 360), (cx - 146, 220), (cx - 104, 98)]), bar, SOUL, 3, extra=f'filter="{s.glow(5)}"')
        for y in (170, 260, 350):
            s.path(star(cx - 150, y, 9, 3.5, 4), "#ffffff", extra=f'filter="{s.glow(2)}" opacity="0.8"')
    else:
        # клинок вниз-вперёд, обе руки на рукояти
        s.path(smooth([(cx - 70, 180), (cx - 104, 236), (cx - 96, 260), (cx - 62, 214)]), plate, sw=4.5)
        s.path(smooth([(cx + 62, 180), (cx + 10, 236), (cx - 70, 262), (cx - 80, 244), (cx + 4, 214), (cx + 46, 172)]), plate, sw=4.5)
        s.path(poly([(cx - 108, 268), (cx - 96, 262), (cx - 176, 506), (cx - 190, 500)]), blade, sw=4)
        s.path(f"M{cx - 104},{272} L{cx - 182},{496}", stroke="#ffffff", sw=1.5, extra='opacity="0.7"')
        s.path(poly([(cx - 130, 252), (cx - 70, 272), (cx - 74, 284), (cx - 134, 264)]), trim, sw=3)
        s.path(poly([(cx - 96, 240), (cx - 86, 244), (cx - 98, 266), (cx - 108, 262)]), "#2a1a12", sw=3)
        s.circle(cx - 92, 238, 6, trim, INK, 2.5)
    if echo:
        _wisps(s, [(cx - 120, 500, -30, 30), (cx + 40, 510, 20, 24), (cx + 130, 480, 34, 20), (cx - 20, 90, -24, -40)],
               "#e8fbff", 6)
    return s


def draw_black_wanderer(v=""):
    return _wanderer(v, echo=False)


def draw_echo_knight(v=""):
    return _wanderer("", echo=True)


# ═════════════════════════ 2а — Жнец Курганов ═════════════════════════

def draw_barrow_reaper(v=""):
    """Огромный костяной жнец в обледенелом саване, коса из льда, морозное дыхание; у ног — курганный холм с костями."""
    s = Svg(480, 560)
    cx = 250
    _aura(s, cx, 220, 230, FROST[1], 0.28)
    s.shadow(cx, 548, 170, 14)
    _ground(s, cx, 540, 200, "#e3f2fb", "#6d8aa2")
    for (x, y, r) in ((cx - 140, 528, 9), (cx + 120, 530, 8), (cx + 160, 538, 6)):
        s.circle(x, y, r, _lin3(s, BONE), INK, 3)
    shroud = _lin3(s, ("#5a6e86", "#26344a", "#0a1018"), 0.4, 1)
    s.open(f'filter="{s.grain(0.16, 0.8, 81)}"')
    # саван — длинный, без ног, сужается к земле лоскутами
    s.path(poly([(cx - 70, 140), (cx - 140, 260), (cx - 168, 400), (cx - 150, 532), (cx - 120, 500), (cx - 96, 536),
                 (cx - 64, 500), (cx - 30, 534), (cx + 6, 500), (cx + 40, 536), (cx + 76, 500), (cx + 110, 534),
                 (cx + 144, 496), (cx + 168, 400), (cx + 150, 260), (cx + 80, 140)]), shroud, sw=5)
    for x in (-110, -50, 10, 70, 120):
        s.path(f"M{cx + x * 0.45},{190} Q{cx + x * 0.85},{350} {cx + x},{500}", stroke="#04070c", sw=3)
    # иней на подоле
    for x in range(-150, 160, 26):
        s.path(poly([(cx + x, 500), (cx + x + 6, 476), (cx + x + 12, 500)]), FROST[0], INK, 1.5, extra='opacity="0.85"')
    # грудная клетка в прорехе савана
    s.path(smooth([(cx - 54, 190), (cx + 54, 190), (cx + 48, 290), (cx - 48, 290)]), "#0a0e16", sw=4)
    for i, y in enumerate((206, 228, 250, 272)):
        w = 44 - i * 4
        s.path(f"M{cx - w},{y + 8} Q{cx},{y - 6} {cx + w},{y + 8}", stroke=BONE[1], sw=6)
        s.path(f"M{cx - w},{y + 8} Q{cx},{y - 6} {cx + w},{y + 8}", stroke=INK, sw=1.5)
    s.path(f"M{cx},{196} L{cx},{290}", stroke=BONE[0], sw=7)
    s.circle(cx, 248, 13, FROST[1], extra=f'filter="{s.glow(6)}" opacity="0.9"')
    # капюшон и череп
    hx, hy = cx - 12, 110
    s.path(smooth([(hx - 70, hy + 70), (hx - 72, hy), (hx - 44, hy - 56), (hx + 8, hy - 76), (hx + 58, hy - 52),
                   (hx + 78, hy + 4), (hx + 76, hy + 80), (hx + 20, hy + 96)]), shroud, sw=5)
    s.path(smooth([(hx - 40, hy + 50), (hx - 42, hy), (hx - 20, hy - 36), (hx + 18, hy - 44), (hx + 46, hy - 20),
                   (hx + 50, hy + 30), (hx + 40, hy + 66), (hx - 10, hy + 72)]), "#05080e", sw=3)
    skull = _lin3(s, BONE, 0.5, 1)
    s.path(smooth([(hx - 30, hy + 4), (hx - 26, hy - 24), (hx, hy - 34), (hx + 28, hy - 24), (hx + 32, hy + 6),
                   (hx + 24, hy + 30), (hx + 14, hy + 52), (hx - 14, hy + 52), (hx - 24, hy + 30)]), skull, sw=4)
    for ex in (hx - 13, hx + 13):
        s.ellipse(ex, hy + 2, 9, 11, INK)
    s.eye(hx - 13, hy + 2, 3.8, FROST[1], FROST[0])
    s.eye(hx + 13, hy + 2, 3.4, FROST[1], FROST[0])
    s.path(poly([(hx - 4, hy + 16), (hx + 4, hy + 16), (hx, hy + 26)]), INK)
    for tx in range(-12, 14, 6):
        s.path(f"M{hx + tx},{hy + 36} L{hx + tx},{hy + 48}", stroke=INK, sw=2)
    s.close()
    # морозное дыхание влево
    breath = s.blur(5)
    for (dx, dy, r, a) in ((-46, 40, 16, 0.6), (-76, 46, 22, 0.45), (-112, 56, 28, 0.3)):
        s.circle(hx + dx, hy + dy, r, FROST[0], extra=f'opacity="{a}" filter="{breath}"')
    # костяные руки и ледяная коса
    for side in (-1, 1):
        s.path(f"M{cx + side * 70},{180} L{cx + side * 40 - 70},{270}", stroke=BONE[1], sw=10)
        s.path(f"M{cx + side * 70},{180} L{cx + side * 40 - 70},{270}", stroke=INK, sw=2)
    s.path(f"M{cx - 50},{40} L{cx - 150},{520}", stroke=INK, sw=14)
    s.path(f"M{cx - 50},{40} L{cx - 150},{520}", stroke="#5a4a3a", sw=8)
    blade = s.lin([(0, "#ffffff"), (0.5, FROST[1]), (1, FROST[2])], 0, 0, 1, 1)
    s.path(smooth([(cx - 52, 44), (cx - 120, 22), (cx - 210, 46), (cx - 250, 110), (cx - 218, 84), (cx - 140, 62),
                   (cx - 54, 70)]), blade, sw=4.5, extra=f'filter="{s.glow(3)}"')
    s.path(f"M{cx - 70},{50} Q{cx - 160},{40} {cx - 230},{92}", stroke="#ffffff", sw=2, extra='opacity="0.8"')
    s.circle(cx - 96, 266, 10, BONE[1], INK, 3)
    return s


# ═════════════════════════ 2б — Мастер-кузнец Глубин ═════════════════════════

def draw_deep_master_smith(v=""):
    """Великан-кузнец в паровой латной броне: горн в груди, трубы на спине, исполинский молот, клещи; паучки-ремонтники."""
    s = Svg(500, 540)
    cx = 262
    s.shadow(cx, 528, 180, 14)
    brass = _lin3(s, BRASS, 0.5, 1)
    iron = _lin3(s, IRON, 0.5, 1)
    leather = s.lin([(0, "#7a5034"), (1, "#2e1a0e")])
    s.open(f'filter="{s.grain(0.15, 0.85, 91)}"')
    # трубы за спиной с дымом
    for x, h in ((cx + 40, 80), (cx + 90, 56)):
        s.path(poly([(x - 12, 150), (x + 12, 150), (x + 12, h), (x - 12, h)]), iron, sw=4)
        s.path(poly([(x - 16, h), (x + 16, h), (x + 16, h - 14), (x - 16, h - 14)]), brass, sw=3.5)
    # ноги — тяжёлые сапоги-наковальни
    for side in (-1, 1):
        lx = cx + side * 56
        s.path(poly([(lx - 30, 360), (lx + 30, 360), (lx + 32, 470), (lx - 32, 470)]), iron, sw=4.5)
        s.path(poly([(lx - 44, 466), (lx + 40, 466), (lx + 48, 520), (lx - 50, 520)]), brass, sw=4.5)
        for rx in (-30, 0, 30):
            s.circle(lx + rx, 494, 4, BRASS[0], INK, 1.5)
    # фартук кузнеца
    s.path(poly([(cx - 90, 300), (cx + 90, 300), (cx + 100, 430), (cx - 100, 430)]), leather, sw=4.5)
    s.path(f"M{cx - 70},{330} L{cx + 70},{330}", stroke="#1a0e06", sw=2.5)
    # корпус-котёл с горном
    s.path(smooth([(cx - 120, 170), (cx + 120, 170), (cx + 134, 250), (cx + 110, 320), (cx - 110, 320), (cx - 134, 250)]),
           brass, sw=5.5)
    for x in range(-100, 110, 25):
        s.circle(cx + x, 182, 4, BRASS[0], INK, 1.5)
    s.ellipse(cx - 6, 250, 54, 40, "#1a0c06", INK, 4)
    s.ellipse(cx - 6, 252, 44, 30, s.rad([(0, EMBER_CORE), (0.5, EMBER), (1, "#5a1406")]), extra=f'filter="{s.glow(5)}"')
    for x in (-30, -12, 6, 24):
        s.path(f"M{cx + x - 6},{226} L{cx + x - 6},{278}", stroke="#2a1006", sw=4)
    # наплечники-заклёпки
    for side in (-1, 1):
        px = cx + side * 126
        s.circle(px, 186, 40, iron, INK, 5)
        s.circle(px, 186, 14, brass, INK, 3)
    # голова: шлем-маска с бородой из стальных прядей
    hx, hy = cx - 18, 120
    s.path(smooth([(hx - 50, hy + 20), (hx - 48, hy - 24), (hx - 20, hy - 50), (hx + 22, hy - 50), (hx + 50, hy - 22),
                   (hx + 52, hy + 22), (hx + 30, hy + 44), (hx - 30, hy + 44)]), iron, sw=5)
    s.path(f"M{hx - 42},{hy - 4} L{hx + 34},{hy - 4}", stroke=INK, sw=10)
    s.eye(hx - 24, hy - 4, 4, EMBER, EMBER_CORE)
    s.eye(hx + 2, hy - 4, 3.6, EMBER, EMBER_CORE)
    beard = s.lin([(0, "#d8dde2"), (1, "#5a646e")])
    s.path(poly([(hx - 40, hy + 22), (hx + 34, hy + 22), (hx + 26, hy + 70), (hx + 8, hy + 92), (hx - 6, hy + 70),
                 (hx - 20, hy + 96), (hx - 30, hy + 66)]), beard, sw=4)
    for bx in (-24, -8, 10):
        s.path(f"M{hx + bx},{hy + 30} L{hx + bx - 2},{hy + 74}", stroke="#3a424a", sw=2)
    s.close()
    # дым из труб
    puff = s.blur(4)
    for (x, y, r, a) in ((cx + 40, 56, 16, 0.55), (cx + 30, 30, 22, 0.4), (cx + 92, 34, 14, 0.5), (cx + 104, 12, 18, 0.35)):
        s.circle(x, y, r, "#d8d4d0", extra=f'opacity="{a}" filter="{puff}"')
    # молот в правой (ближней к героям) руке — занесён
    s.path(smooth([(cx - 120, 196), (cx - 170, 170), (cx - 190, 120), (cx - 168, 110), (cx - 146, 154), (cx - 104, 176)]),
           iron, sw=5)
    s.path(f"M{cx - 182},{124} L{cx - 236},{20}", stroke=INK, sw=14)
    s.path(f"M{cx - 182},{124} L{cx - 236},{20}", stroke="#6a4a2a", sw=8)
    s.path(poly([(cx - 296, 34), (cx - 210, -6), (cx - 186, 44), (cx - 272, 84)]), iron, sw=5)
    s.path(poly([(cx - 290, 42), (cx - 214, 6), (cx - 206, 22), (cx - 282, 58)]), "#e8eef4", stroke=None, extra='opacity="0.5"')
    s.path(poly([(cx - 300, 30), (cx - 292, 26), (cx - 266, 90), (cx - 274, 94)]), brass, sw=3)
    # левая рука с клещами
    s.path(smooth([(cx + 126, 210), (cx + 150, 270), (cx + 120, 320), (cx + 106, 306), (cx + 128, 270), (cx + 110, 222)]),
           iron, sw=5)
    s.path(f"M{cx + 112},{318} L{cx + 64},{390} M{cx + 122},{322} L{cx + 92},{400}", stroke=INK, sw=6)
    s.circle(cx + 78, 396, 9, s.rad([(0, EMBER_CORE), (1, EMBER)]), INK, 2.5, extra=f'filter="{s.glow(3)}"')
    # паучки-ремонтники
    for (x, y) in ((cx - 140, 500), (cx + 160, 508)):
        for k in range(4):
            a = math.radians(200 + k * 45)
            s.path(f"M{x},{y} L{x + math.cos(a) * 20:.1f},{y + math.sin(a) * 12 + 10:.1f}", stroke=INK, sw=3)
        s.circle(x, y, 10, brass, INK, 3)
        s.circle(x - 4, y - 2, 2.5, EMBER)
    return s


# ═════════════════════════ 3 — Посланник Разрушителя Врат ═════════════════════════

def draw_gatebreaker_envoy(v=""):
    """Рогатый демон-рыцарь Бездны в чёрно-багровой броне, огненный круг у ног, Молот Бездны с рунами."""
    s = Svg(510, 560)
    cx = 278
    _aura(s, cx, 260, 230, ABYSS_GLOW, 0.26)
    s.shadow(cx, 546, 170, 14, 0.5)
    # огненный круг
    ring = s.blur(3)
    s.ellipse(cx, 532, 190, 22, "none", ABYSS_GLOW, 6, extra=f'filter="{ring}" opacity="0.9"')
    for k in range(14):
        x = cx - 180 + k * 27
        s.path(f"M{x - 8},{536} Q{x},{506 - (k % 3) * 10} {x + 8},{536} Z", s.lin([(0, EMBER_CORE), (1, ABYSS_GLOW)]),
               stroke=None, extra='opacity="0.85"')
    plate = _lin3(s, ("#6a4a54", "#2a1418", "#0a0406"), 0.4, 1)
    red = _lin3(s, ABYSS, 0.3, 1)
    s.open(f'filter="{s.grain(0.16, 0.85, 101)}"')
    # крылья-обломки за спиной
    for side in (1,):
        s.path(poly([(cx + 40, 150), (cx + 150, 60), (cx + 200, 40), (cx + 180, 110), (cx + 214, 120), (cx + 170, 170),
                     (cx + 196, 200), (cx + 130, 210), (cx + 70, 230)]), s.lin([(0, "#4a1018"), (1, "#14040a")]), sw=4.5)
    # ноги — копытные латы
    for side in (-1, 1):
        lx = cx + side * 44
        s.path(poly([(lx - 26, 350), (lx + 24, 350), (lx + 30, 430), (lx + 16, 470), (lx + 26, 520), (lx - 30, 520),
                     (lx - 22, 470), (lx - 32, 430)]), plate, sw=4.5)
        s.path(poly([(lx - 18, 400), (lx, 384), (lx + 18, 400), (lx, 414)]), red, sw=3)
    # юбка из пластин
    s.path(poly([(cx - 84, 300), (cx + 84, 300), (cx + 100, 384), (cx + 50, 370), (cx, 392), (cx - 50, 370), (cx - 100, 384)]),
           plate, sw=4.5)
    # кираса с трещиной-раной лавы
    s.path(smooth([(cx - 92, 158), (cx + 92, 158), (cx + 88, 240), (cx + 68, 304), (cx - 68, 304), (cx - 88, 240)]), plate, sw=5)
    s.path(f"M{cx + 10},{170} L{cx - 8},{210} L{cx + 12},{240} L{cx - 4},{290}", stroke=MAGMA, sw=6, extra=f'filter="{s.glow(3)}"')
    s.path(f"M{cx + 10},{170} L{cx - 8},{210} L{cx + 12},{240} L{cx - 4},{290}", stroke=EMBER_CORE, sw=2)
    # шипастые наплечники
    for side in (-1, 1):
        px = cx + side * 94
        s.path(smooth([(px - 48, 182), (px - 40, 142), (px, 126), (px + 40, 142), (px + 48, 182), (px, 196)]), red, sw=5)
        for k in (-1, 0, 1):
            s.path(poly([(px + k * 22 - 8, 140), (px + k * 22 + 8, 140), (px + k * 30, 96 + abs(k) * 12)]), plate, sw=3.5)
    # голова: рогатый шлем, пылающий взгляд
    hx, hy = cx - 10, 104
    s.path(smooth([(hx - 40, hy + 34), (hx - 44, hy - 8), (hx - 24, hy - 40), (hx + 16, hy - 46), (hx + 42, hy - 20),
                   (hx + 44, hy + 22), (hx + 26, hy + 48), (hx - 20, hy + 50)]), plate, sw=5)
    for side in (-1, 1):
        x0 = hx + side * 30
        s.path(smooth([(x0, hy - 30), (x0 + side * 40, hy - 70), (x0 + side * 46, hy - 120), (x0 + side * 30, hy - 84),
                       (x0 + side * 8, hy - 50)]), _lin3(s, BONE, 0.5, 1), sw=4)
    s.path(poly([(hx - 38, hy - 2), (hx + 30, hy - 2), (hx + 24, hy + 10), (hx - 32, hy + 10)]), INK)
    s.eye(hx - 20, hy + 4, 4.2, ABYSS_GLOW, EMBER_CORE)
    s.eye(hx + 6, hy + 4, 3.8, ABYSS_GLOW, EMBER_CORE)
    for tx in range(-24, 26, 10):
        s.path(f"M{hx + tx},{hy + 26} L{hx + tx},{hy + 40}", stroke=INK, sw=2.5)
    s.close()
    # Молот Бездны
    s.path(smooth([(cx - 96, 186), (cx - 140, 236), (cx - 150, 280), (cx - 128, 284), (cx - 116, 244), (cx - 76, 204)]), plate, sw=5)
    s.path(f"M{cx - 140},{290} L{cx - 196},{110}", stroke=INK, sw=14)
    s.path(f"M{cx - 140},{290} L{cx - 196},{110}", stroke="#3a1a1a", sw=8)
    head = s.lin([(0, "#5a3a44"), (1, "#14060a")], 0, 0, 1, 1)
    s.path(poly([(cx - 250, 100), (cx - 160, 62), (cx - 138, 124), (cx - 228, 162)]), head, sw=5)
    for (rx, ry) in ((cx - 214, 104), (cx - 186, 92), (cx - 196, 128)):
        s.path(star(rx, ry, 8, 3, 4, 45), MAGMA, extra=f'filter="{s.glow(2.5)}"')
    s.path(f"M{cx - 140},{290} L{cx - 128},{330}", stroke=INK, sw=10)
    return s


# ═════════════════════════ 4а — Владыка Кровавого Двора ═════════════════════════

def draw_blood_court_lord(v=""):
    """Древний вампир-владыка: корона из шипов, багровая мантия-крылья, когти; cocoon — кровавый кокон-хризалида."""
    cocoon = v == "cocoon"
    s = Svg(480, 560)
    cx = 244
    _aura(s, cx, 250, 230, BLOOD[0], 0.3 if not cocoon else 0.45)
    s.shadow(cx, 548, 150, 13)
    robe = _lin3(s, BLOOD, 0.35, 1)
    night = _lin3(s, NIGHT, 0.4, 1)
    skin = s.rad([(0, "#f2eef4"), (0.7, "#cfc6da"), (1, "#8a809a")], 0.6, 0.35, 0.7)
    s.open(f'filter="{s.grain(0.15, 0.85, 111)}"')
    # крылья-мантия
    wing = s.lin([(0, "#5a0a1c"), (1, "#12030a")])
    for side in (-1, 1):
        ox = cx + side * 30
        pts = [(ox, 150), (ox + side * 90, 60), (ox + side * 200, 30), (ox + side * 190, 130), (ox + side * 214, 200),
               (ox + side * 168, 186), (ox + side * 176, 280), (ox + side * 126, 250), (ox + side * 110, 360), (ox + side * 60, 300)]
        s.path(poly(pts), wing, sw=4.5)
        for k in (2, 4, 6):
            s.path(f"M{ox},{150} L{pts[k][0]},{pts[k][1]}", stroke=BLOOD[0], sw=2.5, extra='opacity="0.5"')
    if cocoon:
        # кокон: вытянутая хризалида из застывшей крови, внутри — тёмный силуэт и глаза
        body = s.lin([(0, "#ff8a96"), (0.35, "#c0142e"), (0.8, "#5a0614"), (1, "#20020a")], 0, 0, 0.6, 1)
        s.path(smooth([(cx, 70), (cx + 70, 140), (cx + 92, 280), (cx + 76, 430), (cx + 30, 530), (cx - 30, 530),
                       (cx - 76, 430), (cx - 92, 280), (cx - 70, 140)]), body, sw=5.5)
        for k in range(7):
            y = 140 + k * 54
            w = 70 + 20 * math.sin(k * 0.9)
            s.path(f"M{cx - w},{y} Q{cx},{y + 26} {cx + w},{y - 6}", stroke="#3a020c", sw=4)
            s.path(f"M{cx - w + 8},{y + 4} Q{cx},{y + 24} {cx + w - 10},{y - 2}", stroke="#ff8a96", sw=1.5, extra='opacity="0.6"')
        s.close()
        s.eye(cx - 18, 170, 4.5, "#ff2a3c", "#fff0f0")
        s.eye(cx + 14, 170, 4.0, "#ff2a3c", "#fff0f0")
        for (x, y) in ((cx - 100, 500), (cx + 110, 470), (cx + 80, 530), (cx - 60, 540)):
            s.ellipse(x, y, 14, 5, BLOOD[1], INK, 2)
        return s
    # мантия
    s.path(smooth([(cx - 60, 150), (cx - 120, 300), (cx - 146, 450), (cx - 130, 534), (cx + 130, 534), (cx + 146, 450),
                   (cx + 120, 300), (cx + 60, 150)]), robe, sw=5)
    s.path(smooth([(cx - 40, 170), (cx - 54, 330), (cx - 30, 530), (cx + 30, 530), (cx + 54, 330), (cx + 40, 170)]), night, sw=4)
    for y in (220, 280, 340, 400):
        s.circle(cx, y, 4, "#c9a24a", INK, 1.5)
    # высокий воротник
    for side in (-1, 1):
        s.path(poly([(cx + side * 56, 160), (cx + side * 96, 70), (cx + side * 80, 120), (cx + side * 30, 150)]), robe, sw=4)
    # голова
    hx, hy = cx - 8, 104
    s.path(smooth([(hx - 26, hy - 4), (hx - 20, hy - 32), (hx + 4, hy - 42), (hx + 26, hy - 30), (hx + 30, hy - 2),
                   (hx + 24, hy + 26), (hx + 8, hy + 42), (hx - 10, hy + 40), (hx - 22, hy + 22)]), skin, sw=4)
    s.path(smooth([(hx - 30, hy + 10), (hx - 34, hy - 26), (hx - 12, hy - 50), (hx + 20, hy - 48), (hx + 34, hy - 24),
                   (hx + 18, hy - 30), (hx + 4, hy - 20), (hx - 10, hy - 30), (hx - 18, hy - 10)]),
           s.lin([(0, "#e8e4ee"), (1, "#8a8098")]), sw=4)
    # корона из шипов
    for k in range(-3, 4):
        x = hx + k * 9
        s.path(poly([(x - 5, hy - 40), (x + 5, hy - 40), (x, hy - 70 + abs(k) * 6)]), s.lin([(0, "#ffd27a"), (1, "#8a5a10")]), INK, 2.5)
    s.path(f"M{hx - 30},{hy - 40} L{hx + 32},{hy - 40}", stroke="#c9a24a", sw=6)
    s.eye(hx - 10, hy - 4, 3.6, "#ff2a3c", "#fff0f0")
    s.eye(hx + 12, hy - 4, 3.2, "#ff2a3c", "#fff0f0")
    s.path(f"M{hx - 8},{hy + 24} Q{hx},{hy + 28} {hx + 10},{hy + 23}", stroke=INK, sw=3)
    for fx in (hx - 4, hx + 6):
        s.path(poly([(fx - 2.5, hy + 25), (fx + 2.5, hy + 25), (fx, hy + 33)]), "#ffffff", INK, 1.2)
    s.close()
    # когтистая рука к героям
    s.path(smooth([(cx - 56, 180), (cx - 110, 230), (cx - 150, 240), (cx - 152, 222), (cx - 112, 210), (cx - 70, 166)]), robe, sw=4.5)
    for k in range(4):
        a = math.radians(160 + k * 22)
        x0, y0 = cx - 150, 232
        s.path(f"M{x0},{y0} L{x0 + math.cos(a) * 40:.1f},{y0 + math.sin(a) * 40:.1f}", stroke=INK, sw=6)
        s.path(f"M{x0},{y0} L{x0 + math.cos(a) * 40:.1f},{y0 + math.sin(a) * 40:.1f}", stroke="#e8e4ee", sw=2.5)
    # туман у подола
    mist = s.blur(8)
    for (x, r) in ((cx - 150, 34), (cx - 60, 40), (cx + 60, 38), (cx + 150, 32)):
        s.circle(x, 530, r, "#c8a8c0", extra=f'opacity="0.35" filter="{mist}"')
    return s


# ═════════════════════════ 4б — Первый Призыватель ═════════════════════════

def draw_first_summoner(v=""):
    """Древний маг в звёздной мантии и драконьей маске, посох-громоотвод с цепью молний, парящие руны подчинения."""
    s = Svg(480, 560)
    cx = 258
    _aura(s, cx, 240, 220, ARCANE[1], 0.32)
    s.shadow(cx, 548, 140, 12)
    robe = _lin3(s, ("#4a4a8a", "#22205a", "#0a0820"), 0.35, 1)
    trim = "#e8c86a"
    s.open(f'filter="{s.grain(0.15, 0.85, 121)}"')
    # мантия, паря чуть над землёй
    s.path(smooth([(cx - 54, 150), (cx - 120, 320), (cx - 150, 470), (cx - 120, 520), (cx - 60, 500), (cx, 524),
                   (cx + 60, 500), (cx + 120, 520), (cx + 150, 470), (cx + 120, 320), (cx + 54, 150)]), robe, sw=5)
    for (x, y) in ((cx - 80, 400), (cx + 40, 330), (cx + 96, 450), (cx - 30, 460), (cx + 10, 250), (cx - 60, 300)):
        s.path(star(x, y, 7, 2.6, 4), "#f1f0ff", extra='opacity="0.8"')
    s.path(smooth([(cx - 54, 150), (cx - 120, 320), (cx - 150, 470), (cx - 120, 520)], closed=False), stroke=trim, sw=3)
    s.path(smooth([(cx + 54, 150), (cx + 120, 320), (cx + 150, 470), (cx + 120, 520)], closed=False), stroke=trim, sw=3)
    # наплечная пелерина
    s.path(smooth([(cx - 90, 196), (cx - 70, 150), (cx, 136), (cx + 70, 150), (cx + 90, 196), (cx, 222)]),
           s.lin([(0, "#6a5aa8"), (1, "#2a2060")]), sw=4.5)
    # капюшон и драконья маска
    hx, hy = cx - 6, 100
    s.path(smooth([(hx - 56, hy + 52), (hx - 58, hy - 4), (hx - 30, hy - 52), (hx + 14, hy - 64), (hx + 50, hy - 40),
                   (hx + 60, hy + 6), (hx + 54, hy + 56), (hx, hy + 70)]), robe, sw=5)
    mask = s.lin([(0, "#fff2c0"), (0.5, trim), (1, "#7a5418")], 0, 0, 0.6, 1)
    s.path(poly([(hx - 30, hy - 14), (hx + 30, hy - 14), (hx + 26, hy + 18), (hx + 6, hy + 46), (hx - 6, hy + 46),
                 (hx - 26, hy + 18)]), mask, sw=4)
    for side in (-1, 1):
        s.path(poly([(hx + side * 24, hy - 14), (hx + side * 50, hy - 50), (hx + side * 34, hy - 10)]), mask, sw=3)
    s.eye(hx - 12, hy + 2, 3.6, STORM, "#ffffff")
    s.eye(hx + 12, hy + 2, 3.6, STORM, "#ffffff")
    s.path(f"M{hx - 8},{hy + 30} L{hx + 8},{hy + 30}", stroke=INK, sw=2.5)
    s.close()
    # посох с навершием-громоотводом и цепь молний
    s.path(f"M{cx - 120},{520} L{cx - 150},{60}", stroke=INK, sw=12)
    s.path(f"M{cx - 120},{520} L{cx - 150},{60}", stroke="#5a3a2a", sw=6)
    s.path(smooth([(cx - 60, 190), (cx - 110, 220), (cx - 136, 210), (cx - 132, 192), (cx - 104, 196), (cx - 70, 170)]), robe, sw=4.5)
    s.path(poly([(cx - 150, 60), (cx - 168, 20), (cx - 150, 34), (cx - 146, -4), (cx - 132, 32), (cx - 118, 18), (cx - 136, 62)]),
           trim, sw=3.5)
    s.circle(cx - 146, 52, 13, s.rad([(0, "#ffffff"), (0.5, STORM), (1, "#5a3aa0")]), INK, 3, extra=f'filter="{s.glow(5)}"')
    bolt = f"M{cx - 150},{50} L{cx - 186},{90} L{cx - 172},{100} L{cx - 214},{150} L{cx - 196},{156} L{cx - 236},{214}"
    s.path(bolt, stroke=STORM, sw=7, extra=f'filter="{s.glow(4)}" opacity="0.85"')
    s.path(bolt, stroke="#ffffff", sw=2.5)
    # руны подчинения на орбите
    for k in range(5):
        a = math.radians(-200 + k * 52)
        x, y = cx + math.cos(a) * 150, 250 + math.sin(a) * 60
        s.circle(x, y, 14, "#1a1440", STORM, 3, extra=f'filter="{s.glow(2)}"')
        s.path(f"M{x - 6},{y - 6} L{x + 6},{y + 6} M{x + 6},{y - 6} L{x},{y + 2}", stroke="#ffffff", sw=2.2)
    return s


# ═════════════════════════ 5 — Пожиратель Миров, Чёрное Крыло ═════════════════════════

def draw_world_eater(v=""):
    """Чёрный дракон с вулканической чешуёй. Основной облик — в полёте: крылья распахнуты, тело высоко, на земле только тень.
    landed — сел: крылья сложены, лапы на земле, пасть к героям, раскалённое горло."""
    landed = v == "landed"
    s = Svg(640, 560)
    scale = _lin3(s, DRAGON, 0.4, 1)
    belly = s.lin([(0, "#8a5a3a"), (1, "#3a2010")])
    wing = s.lin([(0, "#3a3040", 0.95), (1, "#0a0810", 0.95)])
    s.shadow(330, 548, 230 if landed else 180, 14, 0.5 if landed else 0.32)
    oy = 0 if landed else -70
    s.open(f'filter="{s.grain(0.15, 0.8, 131)}"')
    if landed:
        # сложенное крыло за спиной
        s.path(poly([(380, 210), (470, 110), (560, 120), (590, 220), (560, 300), (500, 330), (440, 320)]), wing, sw=5)
        for (x, y) in ((470, 110), (560, 120), (590, 220)):
            s.path(f"M{380},{210} L{x},{y}", stroke="#5a4a5a", sw=4)
    else:
        # крылья распахнуты: дальнее вверх-вправо, ближнее вверх-влево
        for (root, tips, f) in (((390, 230 + oy), [(500, 30), (620, 20), (600, 120), (628, 200), (560, 220), (570, 300), (480, 280)], wing),
                                ((330, 240 + oy), [(260, 40), (150, 10), (170, 110), (110, 150), (190, 190), (160, 260), (250, 260)], wing)):
            pts = [root] + [(x, y) for (x, y) in tips]
            s.path(poly(pts), f, sw=5)
            for (x, y) in tips[1::2]:
                s.path(f"M{root[0]},{root[1]} L{x},{y}", stroke="#6a5a6a", sw=4)
            for (x, y) in tips[::2]:
                s.path(f"M{root[0]},{root[1]} L{x},{y}", stroke="#2a2030", sw=2)
    # хвост
    s.path(smooth([(420, 380 + oy), (500, 420 + oy), (580, 470 + oy * 0.6), (620, 520 + oy * 0.3), (600, 500 + oy * 0.3),
                   (560, 470 + oy * 0.6), (490, 440 + oy), (410, 410 + oy)]), scale, sw=4.5)
    for k in range(5):
        x = 450 + k * 32
        y = 400 + oy + k * 20 - (k * 4 if landed else -k * 4)
        s.path(poly([(x - 8, y + 2), (x + 8, y + 2), (x, y - 16)]), "#ff8a2a", INK, 2)
    # задние лапы
    leg_y = 520 if landed else 430 + oy
    for x in (400, 460):
        s.path(smooth([(x - 30, 380 + oy), (x + 20, 390 + oy), (x + 16, leg_y - 30), (x + 30, leg_y), (x - 24, leg_y),
                       (x - 20, leg_y - 40)]), scale, sw=4.5)
        for c in (-18, -4, 10):
            s.path(poly([(x + c - 4, leg_y), (x + c + 4, leg_y), (x + c - 2, leg_y + 12)]), "#e8dcc0", INK, 1.5)
    # туловище
    s.path(smooth([(250, 300 + oy), (310, 230 + oy), (420, 240 + oy), (480, 320 + oy), (460, 400 + oy), (360, 420 + oy),
                   (270, 390 + oy)]), scale, sw=5.5)
    s.path(smooth([(270, 330 + oy), (330, 300 + oy), (400, 330 + oy), (420, 390 + oy), (340, 404 + oy), (284, 380 + oy)]), belly, sw=3)
    for k in range(5):
        y = 320 + oy + k * 16
        s.path(f"M{284 + k * 4},{y} Q{340},{y + 10} {404 - k * 2},{y + 4}", stroke="#2a1408", sw=2)
    # трещины магмы на чешуе
    s.path(f"M{330},{260 + oy} L{350},{290 + oy} L{340},{320 + oy} M{400},{260 + oy} L{390},{300 + oy}", stroke=MAGMA, sw=4,
           extra=f'filter="{s.glow(2.5)}"')
    # передние лапы
    fy = 520 if landed else 420 + oy
    for x in (270, 320):
        s.path(smooth([(x - 20, 340 + oy), (x + 20, 350 + oy), (x + 10, fy - 30), (x + 22, fy), (x - 26, fy), (x - 20, fy - 40)]),
               scale, sw=4.5)
        for c in (-18, -4, 10):
            s.path(poly([(x + c - 4, fy), (x + c + 4, fy), (x + c - 2, fy + 12)]), "#e8dcc0", INK, 1.5)
    # шея и голова к героям (влево)
    neck = [(300, 260 + oy), (250, 200 + oy), (190, 170 + oy), (150, 190 + oy), (180, 220 + oy), (230, 250 + oy), (270, 300 + oy)]
    if landed:
        neck = [(300, 270), (240, 250), (170, 270), (140, 300), (180, 310), (240, 300), (280, 320)]
    s.path(smooth(neck), scale, sw=5)
    for k in range(4):
        x, y = neck[1][0] - k * 22, neck[1][1] - 18 + k * 4
        s.path(poly([(x - 7, y + 4), (x + 7, y + 4), (x + 2, y - 14)]), "#ff8a2a", INK, 2)
    hx, hy = (130, 300) if landed else (140, 180 + oy)
    head = [(hx + 40, hy - 26), (hx - 10, hy - 30), (hx - 64, hy - 14), (hx - 84, hy + 2), (hx - 62, hy + 10), (hx - 80, hy + 26),
            (hx - 30, hy + 30), (hx + 30, hy + 24), (hx + 50, hy)]
    s.path(smooth(head), scale, sw=5)
    # рога
    for (dx, dy, ex, ey) in ((20, -24, 70, -70), (0, -26, 30, -78)):
        s.path(poly([(hx + dx - 6, hy + dy), (hx + dx + 8, hy + dy), (hx + ex, hy + ey)]), _lin3(s, BONE, 0.5, 1), INK, 3)
    # пасть: раскалённое горло, зубы
    throat = s.rad([(0, EMBER_CORE), (0.5, MAGMA), (1, "#5a1406")])
    s.path(poly([(hx - 76, hy + 12), (hx - 20, hy + 10), (hx + 10, hy + 18), (hx - 30, hy + 26), (hx - 74, hy + 22)]), throat,
           INK, 2.5, extra=f'filter="{s.glow(3)}"')
    for tx in range(-70, -24, 10):
        s.path(poly([(tx + hx, hy + 11), (tx + hx + 6, hy + 11), (tx + hx + 3, hy + 19)]), "#ffffff", INK, 1)
    s.eye(hx - 20, hy - 12, 4.5, MAGMA, EMBER_CORE)
    s.close()
    if not landed:
        # пепел вокруг полёта
        for (x, y, r) in ((90, 360, 4), (200, 420, 3), (520, 380, 4), (600, 330, 3), (120, 90, 3), (420, 60, 4)):
            s.circle(x, y, r, MAGMA, extra=f'filter="{s.glow(2)}" opacity="0.8"')
    else:
        # дым из ноздрей
        puff = s.blur(4)
        for (x, y, r, a) in ((hx - 90, hy - 4, 10, 0.5), (hx - 110, hy - 20, 14, 0.35)):
            s.circle(x, y, r, "#8a8080", extra=f'opacity="{a}" filter="{puff}"')
    return s


# ═════════════════════════ ★ Тень Без Имени ═════════════════════════

def draw_nameless_shadow(v=""):
    """Силуэт из чистой тьмы в треснувшей короне, без лица — только пустота с кольцом звёзд, плащ тает дымом.
    exposed — неуязвимость снята: тьма истончилась, сквозь трещины бьёт бледный свет, корона расколота."""
    exposed = v == "exposed"
    s = Svg(480, 580)
    cx = 244
    if exposed:
        _aura(s, cx, 250, 230, "#f4e8ff", 0.36)
    else:
        _aura(s, cx, 250, 230, "#2a1040", 0.55)
    s.shadow(cx, 568, 150, 13, 0.55)
    body = _lin3(s, VOID if exposed else ("#2a2238", "#0e0a16", "#000000"), 0.4, 1, 0.92 if exposed else 1.0)
    s.open(f'filter="{s.grain(0.12, 0.8, 141)}"')
    # тающий плащ — без ног, растворяется в дым
    s.path(smooth([(cx - 60, 150), (cx - 140, 300), (cx - 170, 450), (cx - 120, 560), (cx - 60, 520), (cx, 566),
                   (cx + 60, 520), (cx + 120, 560), (cx + 170, 450), (cx + 140, 300), (cx + 60, 150)]), body, sw=5)
    # длинные руки-тени
    for side in (-1, 1):
        s.path(smooth([(cx + side * 60, 180), (cx + side * 140, 260), (cx + side * 190, 330), (cx + side * 200, 380),
                       (cx + side * 176, 360), (cx + side * 130, 300), (cx + side * 70, 230)]), body, sw=4.5)
        for k in range(3):
            x0, y0 = cx + side * 196, 376
            s.path(f"M{x0},{y0} q{side * 8},{20 + k * 6} {side * (4 - k * 8)},{40 + k * 8}", stroke=INK, sw=5)
    # голова: пустота в капюшоне с кольцом звёзд
    hx, hy = cx - 6, 112
    s.path(smooth([(hx - 56, hy + 52), (hx - 60, hy - 6), (hx - 32, hy - 56), (hx + 12, hy - 66), (hx + 52, hy - 42),
                   (hx + 62, hy + 6), (hx + 54, hy + 58), (hx, hy + 72)]), body, sw=5)
    void = s.rad([(0, "#000000"), (0.7, "#0a0614"), (1, "#2a1a40")])
    s.ellipse(hx, hy + 8, 36, 44, void, INK, 3)
    for k in range(9):
        a = math.radians(k * 40 + 10)
        x, y = hx + math.cos(a) * 24, hy + 8 + math.sin(a) * 30
        s.circle(x, y, 2.2 if k % 2 else 3.2, "#ffffff" if exposed else "#c8b4ff", extra=f'filter="{s.glow(1.5)}"')
    # корона
    gold = s.lin([(0, "#fff0b0"), (1, "#8a6a20")])
    crown = [(hx - 40, hy - 50), (hx - 34, hy - 86), (hx - 18, hy - 62), (hx - 4, hy - 100), (hx + 10, hy - 62), (hx + 26, hy - 88),
             (hx + 40, hy - 50)]
    s.path(poly(crown), gold if exposed else s.lin([(0, "#a08a50"), (1, "#3a2c14")]), sw=4)
    s.path(f"M{hx - 4},{hy - 98} L{hx + 2},{hy - 74} L{hx - 6},{hy - 60} L{hx + 4},{hy - 50}", stroke=INK, sw=3)
    s.close()
    if exposed:
        # трещины света по телу
        for crack in (f"M{cx - 20},{180} L{cx - 4},{240} L{cx - 30},{300} L{cx - 10},{380}",
                      f"M{cx + 50},{220} L{cx + 30},{280} L{cx + 60},{340}",
                      f"M{cx - 90},{330} L{cx - 60},{380} L{cx - 84},{440}"):
            s.path(crack, stroke="#f4e8ff", sw=6, extra=f'filter="{s.glow(4)}"')
            s.path(crack, stroke="#ffffff", sw=2)
    else:
        # клубы тьмы и сияющий контур неуязвимости
        _wisps(s, [(cx - 120, 560, -30, -40), (cx + 120, 560, 30, -50), (cx - 160, 440, -40, -30), (cx + 170, 420, 40, -30),
                   (cx, 566, 0, -40)], "#3a2050", 9, 0.6)
        s.path(smooth([(cx - 60, 150), (cx - 140, 300), (cx - 170, 450), (cx - 120, 560), (cx - 60, 520), (cx, 566),
                       (cx + 60, 520), (cx + 120, 560), (cx + 170, 450), (cx + 140, 300), (cx + 60, 150)]), "none", "#9a6aff", 3,
               extra=f'filter="{s.glow(5)}" opacity="0.55"')
        s.path(smooth([(hx - 56, hy + 52), (hx - 60, hy - 6), (hx - 32, hy - 56), (hx + 12, hy - 66), (hx + 52, hy - 42),
                       (hx + 62, hy + 6), (hx + 54, hy + 58), (hx, hy + 72)]), "none", "#9a6aff", 3,
               extra=f'filter="{s.glow(5)}" opacity="0.55"')
    return s


SUPERBOSSES = {
    "black_wanderer": (draw_black_wanderer, ("", "guard")),
    "barrow_reaper": (draw_barrow_reaper, ("",)),
    "deep_master_smith": (draw_deep_master_smith, ("",)),
    "gatebreaker_envoy": (draw_gatebreaker_envoy, ("",)),
    "blood_court_lord": (draw_blood_court_lord, ("", "cocoon")),
    "first_summoner": (draw_first_summoner, ("",)),
    "world_eater": (draw_world_eater, ("", "landed")),
    "nameless_shadow": (draw_nameless_shadow, ("", "exposed")),
    "echo_knight": (draw_echo_knight, ("",)),
}
