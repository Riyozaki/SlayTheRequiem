#!/usr/bin/env python3
"""Свои значки реликвий там, где ванильная замена сбивает с толку (M7 п. 6, completion plan M7 п. 6, M3 п. 3).

Сбивает с толку (правило tools/art/art_fit.py, CI):
- **дубль внутри мода** — две реликвии мода с одним ванильным значком (13 Сущностей были одним призматическим камнем,
  10 амулетов богов — одним амулетом, 10 облачений, 10 благословений, 5 трактатов, ~20 пар и троек);
- **дубль с ванильной реликвией, которая выпадает героям Реквиема** (data/vanilla_relics.csv: keep/rework вне пулов
  Дефекта и Некробиндера) — игрок видит две разные реликвии с одной картинкой.

Значок собирается из основы и знака (relic_kit.py): у групп — общая основа (медальон богов, облачение, сияние, кристалл
Сущности, трактат, осколок, диск Знамения, кольцо, подвеска) и свой знак и цвет; снаряжение, посохи, походные вещи и
формы — свои предметы. Форматы и подхват — как у значков происхождений (draw_origins.py): images/relics/{id}.png 85×85,
{id}_outline.png, big/{id}.png 256×256; RequiemRelic берёт свой файл раньше ванильного, код не меняется.

    python3 tools/art/relics/draw_relics.py            # только SVG
    python3 tools/art/relics/draw_relics.py --render   # + PNG (resvg: npm i --prefix /tmp/r @resvg/resvg-js@2, Pillow)
    python3 tools/art/relics/draw_relics.py --check    # CI: SVG совпадают с генератором, PNG на месте и нужного размера
"""
from __future__ import annotations

import math
import os
import subprocess
import sys
import tempfile

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, os.path.join(HERE, "..", "monsters"))
sys.path.insert(0, HERE)
from svgkit import INK, Svg, arc_pts, poly, smooth, star  # noqa: E402
from relic_kit import (BONE, BRONZE, C, CLOTH, DARKLEATHER, DARKWOOD, GOLD, GREENLEATHER, ICE, IRON, LEATHER, METAL,  # noqa: E402
                       OBSIDIAN, RUST, SILVER, STEEL, STONE, WOOD, U, blessing, bolt, crystal, e_flame, emblem, g, gem, glow,
                       line, medallion, omen, pendant, ring, robe, rot, shard, sparkle, staff, tome)
import draw_origins as origins  # noqa: E402

ROOT = os.path.abspath(os.path.join(HERE, "..", "..", ".."))
OUT_SVG = os.path.join(ROOT, "art", "relics")
OUT_IMG = os.path.join(ROOT, "mod", "SlayTheRequiem", "images", "relics")
S = 256


def shade(hex_color: str, k: float) -> str:
    """Темнее (k < 1) или светлее (k > 1, к белому)."""
    r, gg, b = (int(hex_color[i:i + 2], 16) for i in (1, 3, 5))
    if k <= 1:
        r, gg, b = (round(v * k) for v in (r, gg, b))
    else:
        t = min(1.0, k - 1)
        r, gg, b = (round(v + (255 - v) * t) for v in (r, gg, b))
    return f"#{r:02x}{gg:02x}{b:02x}"


# ═══════════════════════════ группы ═══════════════════════════

# 10 богов (docs/design/05): знак, цвет, ткань облачения
GODS = {
    "anvil_father": ("anvil", "#e08a3a", "orange"),
    "hearth_mother": ("flame", "#ff6a4a", "red"),
    "mercy_shield": ("shield_heart", "#8fd0ff", "white"),
    "mirror_maiden": ("mirror", "#ff9ad8", "pink"),
    "scroll_keeper": ("scroll", "#e8d090", "brown"),
    "sun_archer": ("sun_arrow", "#ffd34a", "gold"),
    "threshold_warden": ("gate", "#5fc0a8", "teal"),
    "thunder_crowned": ("crown_bolt", "#8ab0ff", "blue"),
    "wind_mistress": ("wind", "#a0f0e0", "green"),
    "winged_hour": ("hourglass", "#c8a8ff", "purple"),
}

# 13 Сущностей пути: знак и цвет кристалла
ESSENCES = {
    "assassin": ("dagger", "#9a5ad8"), "barbarian": ("axe", "#e04a3a"), "battlemage": ("bolt", "#4a8aff"),
    "black_knight": ("sword", "#4a4458"), "blightbringer": ("skull", "#7ab03a"), "champion": ("laurel", "#f0c050"),
    "element_lord": ("elements", "#e8e8ff"), "medium": ("eye", "#4ad0c0"), "monk": ("fist", "#c08a50"),
    "mystic_archer": ("arrow_star", "#6ae0ff"), "paladin": ("sun_cross", "#fff0b0"), "reborn": ("feather", "#ff6a3a"),
    "reflector": ("reflect", "#c8d0e0"),
}

# 5 школ магии (трактаты): знак и обложка
SCHOOLS = {
    "alteration": ("spiral", "green", "#b8ff8a"), "conjuration": ("pentacle", "purple", "#e0b8ff"),
    "destruction": ("flame", "red", "#ffb070"), "illusion": ("mask", "blue", "#d8e8ff"), "restoration": ("life", "gold", "#ffffff"),
}

# 6 Осколков Отлива: знак и цвет
SHARDS = {
    "beast": ("claw", "#ff8a4a"), "earth": ("mountain", "#c8a070"), "root": ("roots", "#8ad04a"), "sun": ("sun", "#ffd34a"),
    "water": ("wave", "#4ab0ff"), "wind": ("wind", "#a0f0e0"),
}

# 10 Знамений: знак
OMENS = {
    "bastion": "tower", "blade": "sword", "burden": "weight", "eye": "eye", "rite": "candle", "serpent": "serpent", "shade": "wisp",
    "sponge": "sponge", "trickster": "die", "voice": "voice",
}


def tri(hex_color):
    return shade(hex_color, 1.6), hex_color, shade(hex_color, 0.38)


def god_amulet(key):
    sign, color, _ = GODS[key]
    return lambda s: medallion(s, GOLD, [(0, shade(color, 0.75)), (1, shade(color, 0.28))], sign, shade(color, 1.35))


def god_robe(key):
    sign, color, cloth = GODS[key]
    return lambda s: robe(s, CLOTH[cloth], "#e8c060", sign, shade(color, 1.2))


def god_blessing(key):
    sign, color, _ = GODS[key]
    return lambda s: blessing(s, color, sign, shade(color, 1.45))


def essence(key):
    sign, color = ESSENCES[key]
    fill = "#e02a3a" if key == "black_knight" else shade(color, 1.75)
    return lambda s: crystal(s, tri(color), sign, fill)


def treatise(key):
    sign, cover, fill = SCHOOLS[key]
    return lambda s: tome(s, CLOTH[cover], sign, fill)


def ebb_shard(key, seed):
    sign, color = SHARDS[key]
    return lambda s: shard(s, tri(color), sign, shade(color, 1.4), seed)


def omen_icon(key):
    return lambda s: omen(s, OMENS[key])


# ═══════════════════════════ предметы ═══════════════════════════

def boots(s, mat, kind="leather", cuff=None, accent=None):
    if kind == "greaves":
        shin = [(92, 30), (156, 30), (162, 150), (150, 176), (98, 176), (86, 150)]
        s.path(smooth(shin, k=0.25), g(s, mat, 0.8, 0.4), INK, 6)
        s.ellipse(124, 64, 40, 26, g(s, mat, 0.6, 1), INK, 5)
        s.path(smooth([(98, 170), (156, 170), (214, 206), (210, 230), (100, 230)], k=0.2), g(s, LEATHER, 0.6, 1), INK, 6)
        for y in (104, 136):
            s.path(f"M92,{y} L160,{y}", stroke=INK, sw=4)
        for x, y in ((100, 90), (148, 90), (100, 150), (148, 150)):
            s.circle(x, y, 4, "#fff6c8", INK, 2)
        return
    shaft = [(92, 40), (156, 40), (158, 160), (210, 190), (220, 222), (100, 226), (94, 160)]
    s.path(smooth(shaft, k=0.18), g(s, mat, 0.7, 1), INK, 6)
    s.path("M100,226 L220,222", stroke="#2a1a10", sw=10)
    if cuff:
        s.path(smooth([(84, 30), (164, 30), (166, 62), (82, 62)], k=0.2), g(s, cuff, 0.6, 1), INK, 5)
        for x in range(92, 160, 12):
            s.path(f"M{x},34 l4,24", stroke="#ffffff", sw=2, extra='opacity="0.5"')
    if accent == "buckle":
        s.path("M94,120 L158,124", stroke="#3a2416", sw=10)
        s.path(poly([(118, 112), (136, 113), (136, 132), (118, 131)]), "none", "#e8c060", 4)
    elif accent == "feather":
        s.path(smooth([(150, 70), (196, 22), (186, 64), (160, 92)], k=0.6), "#e8e0ff", INK, 4)
        s.path("M156,84 L192,30", stroke="#8a7aa8", sw=2.5)
    elif accent == "laces":
        for y in range(70, 160, 18):
            s.path(f"M140,{y} L156,{y + 10} M156,{y} L140,{y + 10}", stroke="#d8c8a0", sw=3)


def hammer(s, head, kind):
    line(s, [(70, 234), (150, 92)], "#7a4a26", 10)
    s.path("M70,234 L150,92", stroke=g(s, WOOD, 1, 0), sw=10)
    for k in range(3):
        px, py = 82 + k * 9, 212 - k * 16
        s.path(f"M{px - 8},{py - 3} l16,8", stroke="#3a2416", sw=4)
    hd = rot([(104, 40), (204, 40), (204, 104), (104, 104)], 154, 72, 30)
    if kind == "mace":
        s.circle(160, 78, 40, g(s, head, 0.6, 1), INK, 6)
        for k in range(8):
            a = math.radians(k * 45 + 10)
            fl = [(160 + math.cos(a - 0.25) * 34, 78 + math.sin(a - 0.25) * 34), (160 + math.cos(a) * 64, 78 + math.sin(a) * 64),
                  (160 + math.cos(a + 0.25) * 34, 78 + math.sin(a + 0.25) * 34)]
            s.path(poly(fl), g(s, head, 0.6, 1), INK, 4)
        s.circle(160, 78, 14, "#fff6c8", INK, 3)
        return
    s.path(poly(hd), g(s, head, 0.6, 1), INK, 6)
    if kind == "ice":
        for px, py, a in ((110, 30, -40), (150, 14, 0), (196, 40, 40), (214, 92, 80)):
            sp = rot([(px - 8, py + 14), (px, py - 18), (px + 8, py + 14)], px, py, a)
            s.path(poly(sp), "#e8f8ff", INK, 3)
        glow(s, 154, 72, 70, "#9ae8ff", 0.5)
        s.path(f"M120,64 L188,80 M140,48 L170,100", stroke="#ffffff", sw=3, extra='opacity="0.7"')
    elif kind == "war":
        sp = rot([(96, 62), (56, 74), (96, 86)], 154, 72, 30)
        s.path(poly(sp), g(s, head), INK, 4)
        s.path(poly(rot([(204, 52), (214, 52), (214, 92), (204, 92)], 154, 72, 30)), "#e8eef8", INK, 3)


def axe(s, head, kind):
    line(s, [(84, 236), (146, 34)], "#7a4a26", 10)
    s.path("M84,236 L146,34", stroke=g(s, DARKWOOD if kind == "battle" else WOOD, 1, 0), sw=10)
    if kind == "battle":
        for side in (-1, 1):
            blade = [(140, 56), (140 + side * 70, 22), (140 + side * 84, 84), (140 + side * 70, 140), (130, 106)]
            s.path(smooth(blade, k=0.3), g(s, head, 0.8, 0.4), INK, 6)
        s.circle(136, 80, 12, "#fff6c8", INK, 3)
    else:
        blade = [(136, 60), (206, 40), (214, 112), (130, 104)]
        s.path(smooth(blade, k=0.25), g(s, head, 0.8, 0.4), INK, 6)
        s.path("M204,48 L212,106", stroke="#ffffff", sw=4, extra='opacity="0.8"')
        s.path("M100,190 l18,6 M104,176 l18,6", stroke="#c03030", sw=4)


def blade(s, mat, kind, grip="#5a3a2a", guard=GOLD):
    if kind == "cleaver":
        s.open('transform="rotate(-38 128 128)"')
        s.path(poly([(124, 150), (148, 150), (144, 246), (128, 246)]), g(s, LEATHER), INK, 5)
        for y in (180, 206, 230):
            s.path(f"M126,{y} L146,{y + 4}", stroke="#2a1a10", sw=3)
        s.path(poly([(70, 20), (196, 20), (200, 140), (84, 154), (70, 130)]), g(s, mat, 0.7, 1), INK, 6)
        s.path("M74,128 L86,150 L200,138", stroke="#f0d8c8", sw=4, extra='opacity="0.8"')
        s.circle(176, 44, 9, "#2a1a10", INK, 3)
        for x, y, r in ((100, 96, 12), (134, 116, 8), (160, 80, 14), (110, 52, 7)):
            s.circle(x, y, r, "#6a2e16", extra='opacity="0.75"')
        s.close()
        return
    if kind == "sickle":
        d = "M80,232 L120,150 C150,150 210,120 214,52 C190,96 150,112 112,122 Z"
        s.path(f"M70,236 L118,146", stroke=INK, sw=16)
        s.path(f"M70,236 L118,146", stroke="#3a2a34", sw=10)
        s.path("M118,150 C156,154 224,116 216,32 C196,96 150,108 104,120 Z", g(s, mat, 0.7, 1), INK, 6)
        for k in range(4):
            s.path(f"M{136 + k * 18},{132 - k * 18} q10,-8 18,-2", stroke="#e8e0ff", sw=1.5, extra='opacity="0.7"')
        s.circle(118, 146, 9, "#e02030", INK, 3)
        del d
        return
    if kind == "saber":
        s.path("M112,158 C106,96 140,52 206,22 C166,66 146,104 148,158 Z", g(s, mat, 1, 0.2), INK, 6)
        s.path("M118,150 C116,104 146,66 196,34", stroke="#ffffff", sw=3, extra='opacity="0.7"')
    elif kind == "curved":
        s.path("M112,158 C108,116 128,82 182,52 C160,92 148,124 148,158 Z", g(s, mat, 1, 0.2), INK, 6)
        s.path("M120,150 C120,118 136,92 170,64", stroke="#c8b0ff", sw=3, extra='opacity="0.6"')
    else:
        s.path(poly([(112, 158), (110, 92), (130, 26), (150, 92), (148, 158)]), g(s, mat, 1, 0.2), INK, 6)
        s.path("M130,40 L130,150", stroke="#5e6a7a", sw=3)
    s.path(poly([(84, 156), (174, 156), (170, 172), (88, 172)]), g(s, guard), INK, 5)
    s.path(poly([(120, 172), (140, 172), (140, 222), (120, 222)]), grip, INK, 5)
    if kind == "dagger" and grip == "scales":
        pass
    s.circle(130, 230, 10, g(s, guard), INK, 4)


def scaled_grip(s):
    s.path(poly([(120, 172), (140, 172), (140, 222), (120, 222)]), g(s, [(0, "#8ad07a"), (1, "#2a5a2a")]), INK, 5)
    for y in range(178, 220, 8):
        s.path(f"M120,{y} q10,6 20,0", stroke="#1a3a1a", sw=2)


def helm(s, kind):
    if kind == "kettle":
        s.path(smooth([(76, 150), (80, 80), (128, 48), (176, 80), (180, 150)], k=0.5), g(s, IRON, 0.6, 1), INK, 6)
        s.ellipse(128, 156, 96, 22, g(s, IRON, 0.6, 1), INK, 6)
        s.path("M128,52 L128,150", stroke="#ffffff", sw=3, extra='opacity="0.4"')
        for x in (90, 128, 166):
            s.circle(x, 132, 4, "#fff6c8", INK, 2)
        return
    if kind in ("hood", "coif"):
        cloth = CLOTH["green"] if kind == "hood" else CLOTH["black"]
        s.path(smooth([(60, 226), (72, 110), (128, 28), (184, 110), (196, 226)], k=0.5), g(s, cloth, 0.6, 1), INK, 6)
        s.path(smooth([(94, 200), (92, 120), (128, 84), (164, 120), (162, 200)], k=0.5), "#140e16", INK, 4)
        if kind == "coif":
            s.path(smooth([(92, 160), (164, 160), (160, 206), (96, 206)], k=0.3), g(s, CLOTH["grey"]), INK, 4)
            s.eye(112, 138, 5, "#ffd34a")
            s.eye(144, 138, 5, "#ffd34a")
        else:
            s.path(poly([(96, 130), (160, 130), (156, 150), (100, 150)]), g(s, GREENLEATHER), INK, 3)
            s.eye(112, 140, 4, "#c8ff8a")
            s.eye(144, 140, 4, "#c8ff8a")
            for k in range(4):
                s.path(f"M{78 + k * 8},{120 + k * 22} l14,-6", stroke="#2a3a1a", sw=3)
        return
    base = [(78, 200), (74, 110), (128, 56), (182, 110), (178, 200), (150, 210), (150, 168), (106, 168), (106, 210)]
    s.path(smooth(base, k=0.35), g(s, STEEL, 0.6, 1), INK, 6)
    s.path("M86,132 L170,132", stroke=INK, sw=8)
    s.path("M128,60 L128,128", stroke="#ffffff", sw=3, extra='opacity="0.5"')
    if kind == "horned":
        for side in (-1, 1):
            horn = [(128 + side * 50, 108), (128 + side * 100, 78), (128 + side * 112, 20), (128 + side * 90, 60), (128 + side * 46, 84)]
            s.path(smooth(horn, k=0.5), g(s, BONE, 0.6, 1), INK, 5)
    elif kind == "spiked":
        for k, px in enumerate((90, 110, 128, 146, 166)):
            top = 40 if px == 128 else 62 + abs(px - 128) * 0.3
            s.path(poly([(px - 9, 96 + abs(px - 128) * 0.25), (px, top - 20), (px + 9, 96 + abs(px - 128) * 0.25)]), g(s, METAL), INK, 4)


def gauntlet(s, kind):
    if kind == "plate":
        s.path(smooth([(84, 230), (80, 150), (96, 132), (164, 132), (178, 150), (170, 230)], k=0.25), g(s, STEEL, 0.6, 1), INK, 6)
        for k in range(4):
            x = 92 + k * 20
            s.path(smooth([(x, 136), (x - 2, 70), (x + 8, 52), (x + 18, 70), (x + 16, 136)], k=0.5), g(s, STEEL, 0.6, 1), INK, 5)
            s.path(f"M{x},{100} L{x + 16},{100}", stroke=INK, sw=3)
        s.path(smooth([(166, 170), (200, 130), (214, 142), (184, 188)], k=0.5), g(s, STEEL), INK, 5)
        s.path("M84,196 L172,196", stroke=INK, sw=5)
        for x in (100, 128, 156):
            s.path(poly([(x - 6, 196), (x, 178), (x + 6, 196)]), "#e8eef8", INK, 2)
        return
    if kind == "glove":
        s.path(smooth([(86, 230), (84, 150), (100, 132), (164, 132), (176, 150), (168, 230)], k=0.25), g(s, GREENLEATHER, 0.6, 1), INK, 6)
        for k in range(4):
            x = 94 + k * 19
            s.path(smooth([(x, 136), (x - 1, 74), (x + 8, 60), (x + 17, 74), (x + 16, 136)], k=0.5), g(s, GREENLEATHER, 0.6, 1), INK, 5)
        s.path(smooth([(164, 168), (196, 134), (208, 146), (180, 186)], k=0.5), g(s, GREENLEATHER), INK, 5)
        s.path(smooth([(80, 200), (176, 200), (180, 232), (78, 232)], k=0.2), g(s, CLOTH["brown"]), INK, 5)
        for x in range(86, 176, 10):
            s.path(f"M{x},204 l3,24", stroke="#ffffff", sw=2, extra='opacity="0.4"')
        return
    mat = LEATHER if kind == "bracer" else STEEL
    s.path(smooth([(76, 70), (180, 54), (190, 190), (84, 206)], k=0.15), g(s, mat, 0.6, 1), INK, 6)
    if kind == "bracer":
        for y in (90, 130, 170):
            s.path(f"M78,{y + 8} L186,{y - 8}", stroke="#2a1a10", sw=9)
            s.path(poly([(150, y - 12), (166, y - 14), (166, y + 2), (150, y + 4)]), "none", "#e8c060", 4)
    else:
        for k, (x, y) in enumerate(((104, 100), (132, 128), (150, 96), (116, 156), (156, 160))):
            s.path(f"M{x - 8},{y - 10} L{x + 8},{y + 10} M{x + 8},{y - 10} L{x},{y}", stroke="#7ad8ff", sw=4, extra=f'filter="{s.glow(2)}"')
        s.path("M80,84 L182,68 M86,192 L188,176", stroke="#e8c060", sw=6)


def torso(s, kind):
    body = [(70, 40), (104, 32), (128, 46), (152, 32), (186, 40), (206, 100), (186, 108), (184, 226), (72, 226), (70, 108), (50, 100)]
    if kind == "cuirass":
        s.path(smooth(body, k=0.2), g(s, STEEL, 0.6, 1), INK, 6)
        s.path("M128,50 L128,220", stroke="#ffffff", sw=4, extra='opacity="0.5"')
        s.path(smooth([(84, 150), (128, 168), (172, 150)], closed=False, k=0.8), stroke=INK, sw=5)
        s.path(smooth([(84, 190), (128, 208), (172, 190)], closed=False, k=0.8), stroke=INK, sw=5)
        for x, y in ((84, 70), (172, 70)):
            s.circle(x, y, 6, "#fff6c8", INK, 2)
    elif kind == "chain":
        s.path(smooth(body, k=0.2), g(s, IRON, 0.6, 1), INK, 6)
        for row in range(10):
            y = 60 + row * 16
            for col in range(9):
                x = 76 + col * 13 + (6 if row % 2 else 0)
                if 70 < x < 186:
                    s.circle(x, y, 5, "none", "#3a4048", 2)
        s.path(smooth([(104, 32), (128, 70), (152, 32)], closed=False, k=0.8), stroke="#c8a050", sw=6)
    else:
        s.path(smooth(body, k=0.2), g(s, GREENLEATHER, 0.6, 1), INK, 6)
        s.path("M128,50 L128,224", stroke="#1a2a10", sw=5)
        for y in range(70, 210, 20):
            s.path(f"M118,{y} L138,{y + 10} M138,{y} L118,{y + 10}", stroke="#d8c8a0", sw=3)
        s.path(smooth([(70, 40), (104, 32), (90, 90), (60, 96)], k=0.4), g(s, CLOTH["brown"]), INK, 4)
        s.path(smooth([(186, 40), (152, 32), (166, 90), (196, 96)], k=0.4), g(s, CLOTH["brown"]), INK, 4)


def shield(s, kind):
    if kind == "oak":
        s.circle(C, C, 98, g(s, WOOD, 0.6, 1), INK, 7)
        for x in (82, 112, 144, 174):
            s.path(f"M{x},{40 + abs(x - 128) * 0.4} L{x},{216 - abs(x - 128) * 0.4}", stroke="#5a3418", sw=3)
        s.circle(C, C, 98, "none", "#7a8088", 8)
        s.circle(C, C, 30, g(s, IRON), INK, 5)
        s.circle(C - 8, C - 8, 8, "#ffffff", extra='opacity="0.6"')
        for k in range(8):
            a = math.radians(k * 45)
            s.circle(round(C + math.cos(a) * 92, 1), round(C + math.sin(a) * 92, 1), 4, "#e0e4ea", INK, 2)
        return
    pts = [(60, 40), (196, 40), (200, 120), (128, 236), (56, 120)]
    s.path(smooth(pts, k=0.3), g(s, GOLD, 0.6, 1), INK, 7)
    inner = [(74, 54), (182, 54), (184, 118), (128, 214), (72, 118)]
    s.path(smooth(inner, k=0.3), s.rad([(0, "#ffffff"), (0.5, "#d8e8ff"), (1, "#8aa0c8")], 0.35, 0.3, 0.9), INK, 4)
    s.path("M90,70 L150,150 M110,64 L166,138", stroke="#ffffff", sw=6, extra='opacity="0.8"')
    sparkle(s, 160, 90, 18)


def cloak(s):
    body = [(C, 40), (C + 70, 60), (C + 96, 228), (C - 96, 228), (C - 70, 60)]
    s.path(smooth(body, k=0.2), g(s, [(0, "#ffffff"), (0.5, "#cfe6ff"), (1, "#5a86b8")], 0.6, 1), INK, 6)
    s.path(smooth([(C - 80, 72), (C - 40, 36), (C, 52), (C + 40, 36), (C + 80, 72), (C + 40, 92), (C, 80), (C - 40, 92)], k=0.6), "#ffffff", INK, 5)
    for x in range(C - 76, C + 80, 14):
        s.path(f"M{x},{66} l4,16", stroke="#a8c0d8", sw=2)
    s.path(star(C, 98, 18, 6, 6), "#bfeaff", INK, 3, extra=f'filter="{s.glow(2)}"')
    for x, y in ((90, 170), (160, 150), (120, 210), (176, 206)):
        s.path(star(x, y, 8, 2.5, 6), "#ffffff", extra='opacity="0.8"')


def bow(s):
    d = "M150,18 C70,60 70,196 150,238"
    s.path(d, stroke=INK, sw=20)
    s.path(d, stroke=g(s, DARKWOOD, 1, 0), sw=13)
    s.path("M150,18 L150,238", stroke="#e8e0d0", sw=3)
    for y in (100, 156):
        s.path(f"M{92},{y} l14,0", stroke="#c8a050", sw=6)
    line(s, [(56, 128), (214, 128)], "#c8a070", 5)
    s.path(poly([(226, 128), (204, 116), (204, 140)]), g(s, METAL), INK, 3)
    s.path(poly([(56, 128), (40, 116), (48, 128), (40, 140)]), "#e04040", INK, 3)


def thorn_necklace(s):
    d = f"M{C - 80},20 Q{C},300 {C + 80},20"
    s.path(d, stroke=INK, sw=16)
    s.path(d, stroke="#5a7a2a", sw=9)
    for k in range(1, 12):
        t = k / 12
        x = (1 - t) ** 2 * (C - 80) + 2 * (1 - t) * t * C + t * t * (C + 80)
        y = (1 - t) ** 2 * 20 + 2 * (1 - t) * t * 300 + t * t * 20
        side = 1 if k % 2 else -1
        a = math.atan2(y - 60, x - C) + side * 0.9
        s.path(poly([(x - 6, y - 3), (x + math.cos(a) * 26, y + math.sin(a) * 26), (x + 6, y + 3)]), "#d8dce4", INK, 3)
    s.path(star(C, 176, 32, 12, 6), g(s, BRONZE), INK, 5)
    s.circle(C, 176, 11, "#e02a3a", INK, 3)


def roadmap(s, barrow=False):
    s.path(poly([(40, 60), (200, 44), (216, 196), (56, 212)]), g(s, [(0, "#fff4d8"), (1, "#c8a870")]), INK, 6)
    for x0, y0, x1, y1 in ((40, 60, 56, 212), (200, 44, 216, 196)):
        s.path(f"M{x0},{y0} L{x1},{y1}", stroke="#8a6a3a", sw=12)
        s.path(f"M{x0},{y0} L{x1},{y1}", stroke=INK, sw=2)
    if barrow:
        for x, y, w in ((96, 160, 34), (150, 130, 26), (110, 104, 20)):
            s.path(f"M{x - w},{y} Q{x},{y - w * 1.1} {x + w},{y} Z", "#7a8a5a", INK, 3)
        emblem(s, "skull", 168, 176, 16, "#f0e8d8")
        s.path("M70,190 Q100,150 150,130 T180,80", stroke="#a02020", sw=4, extra='stroke-dasharray="8 6"')
    else:
        s.path("M70,186 Q110,140 120,150 T170,90 L184,70", stroke="#a02020", sw=5, extra='stroke-dasharray="9 6"')
        s.path("M176,62 L192,78 M192,62 L176,78", stroke="#a02020", sw=5)
        for x, y in ((80, 96), (150, 176)):
            s.path(f"M{x - 12},{y + 8} L{x},{y - 12} L{x + 12},{y + 8} Z", "#6a7a8a", INK, 2)
        s.path(star(96, 150, 18, 4, 4), "#c8a050", INK, 2)


def mead_bottle(s):
    s.path(smooth([(104, 56), (152, 56), (150, 96), (186, 132), (186, 224), (70, 224), (70, 132), (106, 96)], k=0.25),
           g(s, [(0, "#ffe8a0"), (0.5, "#e0a030"), (1, "#7a4a10")], 0.6, 1), INK, 6)
    s.path(poly([(108, 28), (148, 28), (148, 58), (108, 58)]), g(s, WOOD), INK, 5)
    s.path(poly([(84, 150), (172, 150), (172, 196), (84, 196)]), "#f4e8c8", INK, 4)
    s.path(smooth([(110, 180), (128, 156), (146, 180), (128, 172)], k=0.5), "#c89a3a", INK, 2)
    s.ellipse(92, 140, 7, 22, "#ffffff", extra='opacity="0.5"')


def kettle(s):
    for k, x in enumerate((88, 110, 136, 160)):
        e_flame(s, x, 210, 26 - (k % 2) * 6, "#ff7a2e")
    s.path(smooth([(60, 100), (196, 100), (190, 170), (150, 196), (106, 196), (66, 170)], k=0.4), g(s, IRON, 0.6, 1), INK, 6)
    s.ellipse(C, 100, 70, 14, "#3a4048", INK, 5)
    s.path("M66,98 C60,30 196,30 190,98", stroke=INK, sw=6)
    for x, y in ((104, 80), (140, 66), (122, 50)):
        s.circle(x, y, 9, "#e8f0f8", extra=f'opacity="0.7" filter="{s.blur(3)}"')


def sack(s, cloth, feather=False, herbs=False, bedroll=False):
    body = [(84, 90), (172, 90), (196, 160), (184, 222), (72, 222), (60, 160)]
    s.path(smooth(body, k=0.4), g(s, cloth, 0.6, 1), INK, 6)
    s.path(smooth([(92, 92), (100, 64), (156, 64), (164, 92)], k=0.4), g(s, cloth, 0.6, 1), INK, 5)
    s.path("M88,96 Q128,110 168,96", stroke="#c8a070", sw=7)
    if feather:
        s.path(smooth([(150, 70), (200, 12), (212, 30), (164, 90)], k=0.6), "#ffffff", INK, 4)
        s.path("M156,84 L206,22", stroke="#a0a8c0", sw=2.5)
        for x, y in ((96, 150), (150, 180), (120, 196)):
            s.path(f"M{x},{y} q10,-12 20,0", stroke="#ffffff", sw=3, extra='opacity="0.6"')
    if herbs:
        for x, a, col in ((106, -20, "#6ab03a"), (128, 0, "#8ad04a"), (148, 18, "#4a9a3a")):
            stem = rot([(x, 72), (x, 20)], x, 72, a)
            s.path(f"M{stem[0][0]},{stem[0][1]} L{stem[1][0]},{stem[1][1]}", stroke="#2a4a1a", sw=4)
            s.ellipse(stem[1][0], stem[1][1], 10, 18, col, INK, 3, extra=f'transform="rotate({a} {stem[1][0]} {stem[1][1]})"')
        s.circle(146, 160, 14, "#c8e8a0", INK, 3)
        s.path("M140,160 l12,0 M146,154 l0,12", stroke="#3a7a2a", sw=3)
    if bedroll:
        s.path(smooth([(54, 62), (202, 62), (204, 92), (52, 92)], k=0.2), g(s, CLOTH["red"]), INK, 5)
        s.ellipse(56, 77, 10, 16, "#d86a5a", INK, 4)
        for x in (90, 166):
            s.path(f"M{x},58 L{x},96", stroke="#4a2e1a", sw=6)
        s.path("M84,120 L84,200 M172,120 L172,200", stroke="#4a2e1a", sw=7)


def whetstone(s):
    s.path(poly([(48, 150), (180, 104), (212, 130), (80, 180)]), g(s, STONE, 0.6, 1), INK, 6)
    s.path(poly([(80, 180), (212, 130), (212, 150), (82, 202)]), "#3a3640", INK, 5)
    s.path("M70,154 L190,114", stroke="#ffffff", sw=3, extra='opacity="0.35"')
    line(s, [(90, 120), (176, 40)], "#c8d0dc", 8)
    for x, y in ((150, 92), (168, 104), (140, 80), (186, 86)):
        sparkle(s, x, y, 12, "#ffe08a")


def torch(s):
    s.path(poly([(116, 110), (140, 110), (134, 236), (122, 236)]), g(s, WOOD, 1, 0), INK, 5)
    s.path(poly([(108, 100), (148, 100), (144, 128), (112, 128)]), "#5a4030", INK, 5)
    for y in (106, 118):
        s.path(f"M110,{y} L146,{y + 4}", stroke="#c8a070", sw=3)
    glow(s, C, 60, 70, "#ffb040", 0.6)
    e_flame(s, C, 58, 48, "#ff8a2e")


def mule(s):
    head = [(92, 220), (86, 150), (96, 100), (128, 86), (160, 100), (170, 150), (164, 220)]
    s.path(smooth(head, k=0.4), g(s, [(0, "#b8a08a"), (1, "#5a4636")], 0.6, 1), INK, 6)
    for side in (-1, 1):
        ear = [(C + side * 20, 96), (C + side * 56, 16), (C + side * 46, 100)]
        s.path(smooth(ear, k=0.4), g(s, [(0, "#b8a08a"), (1, "#5a4636")]), INK, 5)
    s.ellipse(C, 200, 34, 26, "#d8c8b8", INK, 4)
    for x in (116, 140):
        s.ellipse(x, 204, 5, 7, INK)
    for x in (104, 152):
        s.circle(x, 140, 7, "#1a1418")
    s.path("M86,120 L170,120 M90,170 L166,170 M128,120 L128,170", stroke="#8a2a20", sw=6)
    s.circle(128, 120, 7, "#e8c060", INK, 2)
    for side in (-1, 1):
        s.path(smooth([(C + side * 62, 150), (C + side * 110, 150), (C + side * 112, 214), (C + side * 60, 214)], k=0.3), g(s, LEATHER), INK, 5)
        s.path(f"M{C + side * 64},170 L{C + side * 110},170", stroke="#c8a070", sw=4)


def candles(s):
    for x, h, flame in ((84, 96, "#ffb040"), (172, 110, "#ffb040"), (128, 150, "#e0203a")):
        top = 230 - h
        s.path(poly([(x - 18, top), (x + 18, top), (x + 18, 228), (x - 18, 228)]), g(s, [(0, "#fffaf0"), (1, "#c8b898")], 1, 0), INK, 5)
        s.path(smooth([(x - 18, top), (x - 4, top + 14), (x + 6, top + 4), (x + 18, top)], closed=False, k=0.6), stroke=INK, sw=3)
        glow(s, x, top - 26, 40 if flame == "#e0203a" else 28, flame, 0.6)
        e_flame(s, x, top - 22, 22 if flame != "#e0203a" else 28, flame, "#ffe0e0" if flame == "#e0203a" else "#fff2a0")
    s.path("M50,230 L206,230", stroke=INK, sw=8)


def contract(s):
    s.path(poly([(60, 40), (180, 30), (196, 210), (76, 222)]), g(s, [(0, "#f4e4c4"), (1, "#a88a5a")]), INK, 6)
    for k in range(5):
        y = 70 + k * 22
        s.path(f"M{82},{y + 2 - k} L{170},{y - 6 - k}", stroke="#5a3a2a", sw=4, extra='stroke-dasharray="14 5"')
    s.circle(150, 184, 24, s.rad([(0, "#ff6a6a"), (1, "#7a0a14")]), INK, 5)
    s.path(star(150, 184, 12, 5, 5), "#3a0408")
    for x, y in ((140, 210), (160, 214)):
        s.path(f"M{x},{y} q4,14 0,20", stroke="#a01020", sw=6)
    s.path(smooth([(196, 20), (230, 4), (214, 40), (120, 140)], k=0.6), "#2a2232", INK, 4)


def lantern(s):
    s.path("M128,22 L128,46", stroke=INK, sw=6)
    s.circle(C, 18, 10, "none", INK, 5)
    s.path(poly([(92, 56), (164, 56), (156, 46), (100, 46)]), g(s, OBSIDIAN), INK, 5)
    glow(s, C, 130, 70, "#6aff9a", 0.6)
    s.path(poly([(96, 56), (160, 56), (168, 196), (88, 196)]), s.rad([(0, "#d8ffe0", 0.9), (1, "#2a6a4a", 0.7)]), INK, 5)
    e_flame(s, C, 134, 36, "#7aff9a", "#e8ffe8")
    for x in (96, 128, 160):
        s.path(f"M{x},56 L{x + (x - 128) * 0.12},196", stroke=INK, sw=6)
    s.path(poly([(84, 196), (172, 196), (164, 222), (92, 222)]), g(s, OBSIDIAN), INK, 5)


def lich(s):
    glow(s, C, 140, 90, "#a06aff", 0.5)
    emblem(s, "skull", C, 150, 76, "#f0e8d8")
    for x in (104, 152):
        s.eye(x, 146, 7, "#c08aff")
    emblem(s, "crown", C, 70, 52, g(s, GOLD))


def phylactery(s):
    glow(s, C, 140, 90, "#c08aff", 0.55)
    s.path(smooth([(100, 60), (156, 60), (154, 90), (196, 140), (186, 206), (128, 228), (70, 206), (60, 140), (102, 90)], k=0.45),
           s.rad([(0, "#f0e8ff", 0.85), (1, "#4a2a7a", 0.85)]), INK, 6)
    emblem(s, "spiral", C, 158, 40, "#e8d8ff")
    s.path(poly([(96, 30), (160, 30), (164, 62), (92, 62)]), g(s, GOLD), INK, 5)
    s.circle(C, 26, 10, "#a06aff", INK, 3)
    s.ellipse(90, 150, 8, 28, "#ffffff", extra='opacity="0.45"')


def bat_form(s):
    for side in (-1, 1):
        wing = [(C + side * 20, 110), (C + side * 70, 50), (C + side * 118, 70), (C + side * 104, 110), (C + side * 112, 150),
                (C + side * 86, 140), (C + side * 70, 170), (C + side * 54, 150), (C + side * 30, 160)]
        s.path(poly(wing), g(s, [(0, "#5a3a4a"), (1, "#1a0e14")], 0.6, 1), INK, 5)
    s.path(smooth([(C, 70), (C + 44, 130), (C + 38, 182), (C, 222), (C - 38, 182), (C - 44, 130)], k=0.6),
           s.rad([(0, "#ff8a8a"), (0.5, "#c0102a"), (1, "#5a0410")], 0.35, 0.3, 0.8), INK, 6)
    s.ellipse(C - 14, 140, 7, 18, "#ffffff", extra='opacity="0.55"')
    for x in (112, 144):
        s.path(poly([(x - 8, 70), (x + 8, 70), (x, 100)]), "#ffffff", INK, 3)


def righteous(s):
    for side in (-1, 1):
        wing = [(C + side * 30, 140), (C + side * 80, 70), (C + side * 124, 60), (C + side * 112, 100), (C + side * 120, 130),
                (C + side * 96, 140), (C + side * 100, 170), (C + side * 60, 170)]
        s.path(smooth(wing, k=0.35), g(s, [(0, "#ffffff"), (1, "#c8c0a0")], 0.6, 1), INK, 5)
        for k in range(3):
            s.path(f"M{C + side * (52 + k * 18)},{150 - k * 10} L{C + side * (84 + k * 14)},{100 - k * 8}", stroke="#b8a878", sw=2.5)
    glow(s, C, 120, 80, "#ffe08a", 0.6)
    s.ellipse(C, 66, 50, 14, "none", INK, 12)
    s.ellipse(C, 66, 50, 14, "none", g(s, GOLD), 6)
    emblem(s, "sun", C, 146, 44, g(s, GOLD))


def werewolf(s):
    emblem(s, "moon", 118, 120, 84, s.rad([(0, "#fffaf0"), (1, "#b8b8d0")], 0.4, 0.4, 0.7))
    for k in range(3):
        x = 92 + k * 30
        d = f"M{x},{40 + k * 6} Q{x + 22},{130} {x - 4},{226 - k * 6}"
        s.path(d, stroke=INK, sw=16)
        s.path(d, stroke="#b01a2a", sw=8)


def deadspeaker(s):
    pendant(s, "#5a4a3a", [(0, "#e8e0d0"), (1, "#6a6a5a")], None, shape="none")
    emblem(s, "skull", C, 170, 52, g(s, BONE))
    for x in (110, 146):
        s.eye(x, 168, 5, "#7affd8")
    s.circle(C, 112, 10, g(s, IRON), INK, 4)


def hunter_tooth(s):
    pendant(s, "#7a5a3a", None, shape="none")
    pts = [(C - 26, 116), (C + 26, 116), (C + 18, 170), (C, 234), (C - 18, 170)]
    s.path(smooth(pts, k=0.4), g(s, BONE, 0.6, 1), INK, 6)
    s.path(poly([(C - 28, 104), (C + 28, 104), (C + 26, 122), (C - 26, 122)]), "#7a5a3a", INK, 4)
    for side in (-1, 1):
        s.path(smooth([(C + side * 44, 90), (C + side * 60, 122), (C + side * 52, 148), (C + side * 40, 110)], k=0.5), "#c84a3a", INK, 3)


def spindle(s):
    pendant(s, "#c8c0e0", None, shape="none")
    s.path("M128,108 L128,234", stroke=INK, sw=10)
    s.path("M128,108 L128,234", stroke="#c8a070", sw=5)
    s.ellipse(C, 170, 46, 36, g(s, [(0, "#ffffff"), (1, "#9a8ac8")]), INK, 5)
    for k in range(6):
        s.path(f"M{86 + k * 4},{150 + k * 8} Q128,{140 + k * 8} {170 - k * 4},{150 + k * 8}", stroke="#6a5aa0", sw=2.5)
    s.ellipse(C, 222, 22, 9, g(s, WOOD), INK, 4)
    emblem(s, "web", 196, 70, 34, "#e8e0ff")


def starstone(s):
    d = f"M{C - 84},18 Q{C},300 {C + 84},18"
    s.path(d, stroke=INK, sw=9)
    s.path(d, stroke="#c8d0e8", sw=4)
    for k, t in enumerate((0.18, 0.34, 0.5, 0.66, 0.82)):
        x = (1 - t) ** 2 * (C - 84) + 2 * (1 - t) * t * C + t * t * (C + 84)
        y = (1 - t) ** 2 * 18 + 2 * (1 - t) * t * 300 + t * t * 18
        r = 40 if k == 2 else 24
        glow(s, x, y, r * 1.6, "#9ac8ff", 0.5)
        s.path(star(round(x, 1), round(y, 1), r, r * 0.45, 5), s.rad([(0, "#ffffff"), (1, "#6a8ad8")]), INK, 4)


def void_pendant(s):
    pendant(s, "#3a2a4a", None, shape="none")
    glow(s, C, 172, 80, "#a06aff", 0.6)
    s.circle(C, 172, 54, s.rad([(0, "#2a1a3a"), (0.7, "#0e0814"), (1, "#3a1a5a")]), INK, 6)
    emblem(s, "spiral", C, 172, 40, "#c08aff")
    s.circle(C, 112, 11, g(s, SILVER), INK, 4)


def ember_pendant(s):
    pendant(s, "#a0603a", [(0, "#ffe08a"), (0.5, "#ff6a2a"), (1, "#7a1a0a")], None)
    e_flame(s, C, 178, 30, "#fff2a0", "#ffffff")


def vial_pendant(s):
    pendant(s, "#c8a050", None, shape="none")
    s.path(poly([(116, 104), (140, 104), (140, 128), (116, 128)]), g(s, GOLD), INK, 4)
    s.path(smooth([(114, 128), (142, 128), (160, 178), (150, 228), (106, 228), (96, 178)], k=0.45),
           s.rad([(0, "#ffd0e0", 0.95), (0.5, "#e03a6a", 0.9), (1, "#5a0a2a", 0.9)]), INK, 6)
    s.path("M100,180 Q128,170 156,180", stroke="#ffffff", sw=3, extra='opacity="0.5"')
    s.ellipse(112, 190, 5, 16, "#ffffff", extra='opacity="0.55"')


def faceless_mask(s):
    pts = [(70, 40), (186, 40), (196, 120), (172, 192), (128, 228), (84, 192), (60, 120)]
    s.path(smooth(pts, k=0.45), g(s, [(0, "#ffffff"), (1, "#c8c8d0")], 0.6, 1), INK, 6)
    for side in (-1, 1):
        s.path(smooth([(C + side * 18, 108), (C + side * 40, 98), (C + side * 60, 110), (C + side * 40, 118)], k=0.6), INK)
    s.path("M70,40 Q128,20 186,40", stroke="#c8c8d0", sw=3)
    s.path("M196,120 L232,96 M60,120 L24,96", stroke="#7a2a2a", sw=5)


def wraps(s):
    for k in range(5):
        y = 60 + k * 32
        s.path(smooth([(60, y + 6), (128, y - 10), (196, y + 6), (196, y + 34), (128, y + 18), (60, y + 34)], k=0.3),
               g(s, [(0, "#fffaf0"), (1, "#c8b898")], 0.6, 1), INK, 5)
    s.path("M196,80 Q232,110 214,160 Q206,190 226,226", stroke=INK, sw=14)
    s.path("M196,80 Q232,110 214,160 Q206,190 226,226", stroke="#fff4e0", sw=8)
    s.path("M100,100 l20,-6 M140,170 l22,-6", stroke="#c03030", sw=4, extra='opacity="0.7"')


def banner(s):
    line(s, [(64, 238), (64, 20)], "#7a4a26", 8)
    s.path("M64,238 L64,20", stroke=g(s, WOOD, 1, 0), sw=8)
    s.path(poly([(54, 36), (190, 36), (190, 44), (54, 44)]), g(s, GOLD), INK, 4)
    s.path(poly([(70, 44), (186, 44), (186, 200), (128, 172), (70, 200)]), g(s, CLOTH["red"], 0.6, 1), INK, 6)
    s.path(poly([(70, 60), (186, 60)], closed=False), stroke="#e8c060", sw=5)
    emblem(s, "sword", 128, 114, 40, g(s, METAL))
    s.circle(64, 16, 10, g(s, GOLD), INK, 4)


def quiver(s):
    s.path(smooth([(86, 70), (170, 54), (188, 218), (104, 234)], k=0.15), g(s, DARKLEATHER, 0.6, 1), INK, 6)
    for k, x in enumerate((100, 122, 144, 162)):
        top = rot([(x, 66), (x, 16)], x, 66, -10 + k * 6)
        s.path(f"M{top[0][0]},{top[0][1]} L{top[1][0]},{top[1][1]}", stroke=INK, sw=7)
        s.path(f"M{top[0][0]},{top[0][1]} L{top[1][0]},{top[1][1]}", stroke="#d8b080", sw=3)
        tx, ty = top[1]
        s.path(poly([(tx - 9, ty + 6), (tx, ty - 12), (tx + 9, ty + 6)]), "#7ad8ff", INK, 2.5, extra=f'filter="{s.glow(2)}"')
    for k, (x, y) in enumerate(((120, 120), (136, 150), (124, 182), (146, 204))):
        s.path(f"M{x - 9},{y - 10} L{x + 9},{y + 10} M{x + 9},{y - 10} L{x},{y}", stroke="#7ad8ff", sw=4, extra=f'filter="{s.glow(2)}"')


def bell(s):
    s.path("M128,24 L128,52", stroke=INK, sw=6)
    s.circle(C, 26, 10, "none", INK, 5)
    s.path(smooth([(96, 70), (128, 50), (160, 70), (168, 160), (196, 190), (60, 190), (88, 160)], k=0.35), g(s, BRONZE, 0.6, 1), INK, 6)
    s.path("M70,186 L186,186", stroke="#fff0c0", sw=3, extra='opacity="0.6"')
    s.circle(C, 204, 14, g(s, BRONZE), INK, 4)
    for x, y, flip in ((52, 100, -1), (204, 110, 1)):
        emblem(s, "wisp", x, y, 26, "#bff8ff")
        del flip
    for k in range(3):
        rr = 96 + k * 14
        pts = arc_pts(C, 186, rr, rr * 0.4, 200, 340, 10)
        s.path(poly(pts, closed=False), stroke="#bff8ff", sw=2.5, extra='opacity="0.6"')


def staff_head(kind):
    def head(s, x, y):
        if kind == "void":
            glow(s, x, y, 70, "#8a4aff", 0.6)
            for a in (-60, 0, 60):
                cl = rot([(x - 6, y + 30), (x, y - 44), (x + 6, y + 30)], x, y + 24, a)
                s.path(poly(cl), g(s, OBSIDIAN), INK, 3)
            s.circle(x, y, 26, s.rad([(0, "#4a2a6a"), (0.7, "#0a0610"), (1, "#8a4aff")]), INK, 5)
            emblem(s, "spiral", x, y, 18, "#c08aff")
        elif kind == "ice":
            glow(s, x, y, 70, "#9ae8ff", 0.6)
            for a, h in ((-30, 40), (0, 56), (30, 40)):
                cr = rot([(x - 12, y + 24), (x - 8, y - h * 0.5), (x, y - h), (x + 8, y - h * 0.5), (x + 12, y + 24)], x, y + 24, a)
                s.path(poly(cr), g(s, ICE, 0.7, 1), INK, 4)
        elif kind == "oak":
            for a, col in ((-50, "#6ab03a"), (20, "#8ad04a"), (70, "#4a9a3a"), (-10, "#9ae05a")):
                lx, ly = x + math.cos(math.radians(a - 90)) * 30, y + math.sin(math.radians(a - 90)) * 30
                s.ellipse(round(lx, 1), round(ly, 1), 12, 24, col, INK, 3, extra=f'transform="rotate({a} {round(lx, 1)} {round(ly, 1)})"')
            s.path(smooth([(x - 20, y + 30), (x - 26, y - 6), (x - 6, y - 20), (x + 18, y - 6), (x + 16, y + 26)], closed=False, k=0.6),
                   stroke=INK, sw=14)
            s.path(smooth([(x - 20, y + 30), (x - 26, y - 6), (x - 6, y - 20), (x + 18, y - 6), (x + 16, y + 26)], closed=False, k=0.6),
                   stroke="#8a5a30", sw=8)
            s.circle(x, y, 13, "#c8ff8a", INK, 3, extra=f'filter="{s.glow(4)}"')
        elif kind == "obsidian":
            glow(s, x, y, 70, "#ff6a2a", 0.55)
            for a, h in ((-40, 44), (0, 60), (40, 44)):
                cr = rot([(x - 12, y + 24), (x - 4, y - h * 0.6), (x + 3, y - h), (x + 12, y + 24)], x, y + 24, a)
                s.path(poly(cr), g(s, OBSIDIAN, 0.7, 1), INK, 4)
                s.path(poly(cr[1:3], closed=False), stroke="#ff8a3a", sw=3, extra=f'filter="{s.glow(2)}"')
            s.circle(x, y + 18, 12, "#ffb040", INK, 3, extra=f'filter="{s.glow(3)}"')
        elif kind == "silver":
            glow(s, x, y, 60, "#e8f0ff", 0.55)
            emblem(s, "moon", x, y - 6, 34, g(s, SILVER))
            sparkle(s, x + 22, y - 20, 12)
        elif kind == "element":
            for k, col in enumerate(("#ff7a2e", "#7ad0ff", "#c9a8ff", "#8ad04a")):
                a = math.radians(-90 + k * 90 + 45)
                cx, cy = x + math.cos(a) * 24, y + math.sin(a) * 24
                s.circle(round(cx, 1), round(cy, 1), 16, s.rad([(0, "#ffffff"), (1, col)]), INK, 4, extra=f'filter="{s.glow(2)}"')
            s.path(f"M{x - 34},{y + 20} Q{x},{y - 60} {x + 34},{y + 20}", stroke="#c8a050", sw=6)
    return head


def gift_aura(s):
    """Дары Князей: общий фиолетово-багровый ореол за предметом."""
    s.circle(C, C, 104, s.rad([(0, "#c0306a", 0.55), (0.6, "#6a1a8a", 0.3), (1, "#2a0a3a", 0)]))
    for k in range(6):
        a = math.radians(k * 60 + 15)
        s.path(f"M{C + math.cos(a) * 70:.1f},{C + math.sin(a) * 70:.1f} L{C + math.cos(a) * 110:.1f},{C + math.sin(a) * 110:.1f}",
               stroke="#e05a9a", sw=3, extra='opacity="0.6"')


def gift(kind):
    def draw(s):
        gift_aura(s)
        if kind == "dusk_star":
            s.path(star(C, C, 92, 36, 8), s.rad([(0, "#5a3a6a"), (1, "#0e0814")]), INK, 6)
            s.path(star(C, C, 92, 36, 8), "none", "#c08aff", 2.5, extra=f'filter="{s.glow(2)}"')
            s.circle(C, C, 22, "#e8c8ff", INK, 4, extra=f'filter="{s.glow(4)}"')
        elif kind == "gate_breaker":
            for x in (78, 104, 152, 178):
                s.path(f"M{x},40 L{x},220", stroke=INK, sw=16)
                s.path(f"M{x},40 L{x},220", stroke=g(s, IRON, 1, 0), sw=9)
            s.path("M60,70 L196,70 M60,190 L196,190", stroke=INK, sw=14)
            s.path("M60,70 L196,70 M60,190 L196,190", stroke="#8a9098", sw=7)
            s.path(poly([(112, 30), (146, 30), (160, 120), (136, 140), (150, 230), (108, 118), (126, 104)]), "#ffb040", INK, 4,
                   extra=f'filter="{s.glow(3)}"')
        elif kind == "outcast":
            emblem(s, "crown", C, 132, 86, g(s, [(0, "#d8d0c0"), (1, "#5a5048")]))
            s.path("M128,60 L118,110 L138,130 L124,200", stroke=INK, sw=8)
            s.path("M128,60 L118,110 L138,130 L124,200", stroke="#e05a9a", sw=3)
        elif kind == "plague_lord":
            s.path(smooth([(C, 40), (C + 62, 120), (C + 54, 196), (C, 226), (C - 54, 196), (C - 62, 120)], k=0.6),
                   s.rad([(0, "#c8e870"), (0.6, "#5a7a1a"), (1, "#2a3a0a")], 0.35, 0.3, 0.8), INK, 6)
            for x, y, r in ((110, 130, 12), (150, 160, 16), (124, 186, 9), (148, 110, 7)):
                s.circle(x, y, r, "#3a4a10", INK, 2)
            for x, y in ((80, 60), (180, 80), (190, 170)):
                s.ellipse(x, y, 8, 6, "#1a1a1a", INK, 2)
                s.path(f"M{x - 4},{y - 4} q-8,-10 -2,-14 M{x + 4},{y - 4} q8,-10 2,-14", stroke="#e8f0f8", sw=2)
        elif kind == "revel":
            s.path(smooth([(70, 60), (186, 60), (176, 120), (140, 150), (116, 150), (80, 120)], k=0.35), g(s, GOLD, 0.6, 1), INK, 6)
            s.ellipse(C, 62, 58, 12, "#8a0a3a", INK, 4)
            s.path(poly([(118, 150), (138, 150), (138, 196), (118, 196)]), g(s, GOLD), INK, 5)
            s.ellipse(C, 208, 46, 14, g(s, GOLD), INK, 5)
            for x, y in ((92, 70), (166, 74)):
                s.path(f"M{x},{y} q-6,24 2,40", stroke="#b0104a", sw=7)
            gem(s, C, 100, 13, "#ff8ab0", "#8a0a3a")
        elif kind == "treachery":
            blade(s, STEEL, "dagger", "#2a1a2a")
            emblem(s, "serpent", 128, 100, 52, "#6ad04a")
        elif kind == "twilight_widow":
            emblem(s, "spider", C, C, 92, s.rad([(0, "#4a3a5a"), (1, "#0e0a12")]))
        elif kind == "web_weaver":
            emblem(s, "web", C, C, 98, "#f0e8ff")
            for x, y in ((100, 90), (162, 150), (120, 178)):
                s.circle(x, y, 6, "#bff8ff", INK, 1.5)
            emblem(s, "spider", 156, 82, 30, "#e8e0ff", )
    return draw


def grimoire(s):
    glow(s, C, C, 100, "#a06aff", 0.45)
    tome(s, CLOTH["black"], "eye", "#c08aff", clasp=SILVER, thick=1.6)


def ledger(s):
    tome(s, CLOTH["green"], "coin", "#ffffff", clasp=BRONZE)


def chronicle(s):
    s.path(smooth([(24, 80), (128, 64), (232, 80), (226, 208), (128, 196), (30, 208)], k=0.15), "#3a1a14", INK, 6)
    for side in (-1, 1):
        page = [(C, 70), (C + side * 96, 58), (C + side * 100, 192), (C, 202)]
        s.path(smooth(page, k=0.12), g(s, [(0, "#fffaf0"), (1, "#e0d0a8")], 0.6, 1), INK, 5)
        for k in range(6):
            y = 88 + k * 16
            s.path(f"M{C + side * 18},{y} L{C + side * 80},{y - 6}", stroke="#8a6a4a", sw=2.5)
    emblem(s, "laurel", 70, 150, 26, "#8ad04a")
    s.path(smooth([(150, 200), (226, 30), (240, 40), (160, 206)], k=0.6), "#ffffff", INK, 4)
    s.path("M156,200 L232,36", stroke="#a0a8c0", sw=2)


# ═══════════════════════════ таблица значков ═══════════════════════════

ICONS: dict = {}
for _god in GODS:
    _camel = "".join(p.capitalize() for p in _god.split("_"))
    ICONS[f"amulet_of_{_god}"] = god_amulet(_god)
    ICONS[f"robe_of_{_god}"] = god_robe(_god)
    ICONS[f"blessing_of_{_god}"] = god_blessing(_god)
for _cls in ESSENCES:
    ICONS[f"{_cls}_essence"] = essence(_cls)
for _sch in SCHOOLS:
    ICONS[f"treatise_of_{_sch}"] = treatise(_sch)
for _i, _sh in enumerate(SHARDS):
    ICONS[f"{_sh}_shard"] = ebb_shard(_sh, _i)
for _om in OMENS:
    ICONS[f"omen_of_the_{_om}"] = omen_icon(_om)
for _gf in ("dusk_star", "gate_breaker", "outcast", "plague_lord", "revel", "treachery", "twilight_widow", "web_weaver"):
    ICONS[f"gift_of_{_gf}"] = gift(_gf)

ICONS.update({
    # снаряжение (Items)
    "boots_of_silence": lambda s: boots(s, CLOTH["purple"], accent="feather", cuff=CLOTH["black"]),
    "smuggler_boots": lambda s: boots(s, LEATHER, accent="buckle"),
    "stalker_boots": lambda s: boots(s, GREENLEATHER, cuff=[(0, "#e8dcc8"), (1, "#8a7a64")], accent="laces"),
    "militia_greaves": lambda s: boots(s, STEEL, kind="greaves"),
    "everfrost_hammer": lambda s: hammer(s, ICE, "ice"),
    "steel_warhammer": lambda s: hammer(s, STEEL, "war"),
    "iron_mace": lambda s: hammer(s, IRON, "mace"),
    "first_blizzard_cloak": cloak,
    "heavy_bow": bow,
    "iron_battleaxe": lambda s: axe(s, IRON, "battle"),
    "woodcutter_axe": lambda s: axe(s, STEEL, "wood"),
    "militia_cuirass": lambda s: torso(s, "cuirass"),
    "smuggler_hauberk": lambda s: torso(s, "chain"),
    "stalker_jerkin": lambda s: torso(s, "jerkin"),
    "militia_gauntlets": lambda s: gauntlet(s, "plate"),
    "stalker_gloves": lambda s: gauntlet(s, "glove"),
    "smuggler_bracers": lambda s: gauntlet(s, "bracer"),
    "runic_bracers": lambda s: gauntlet(s, "runic"),
    "militia_helm": lambda s: helm(s, "kettle"),
    "smuggler_coif": lambda s: helm(s, "coif"),
    "stalker_hood": lambda s: helm(s, "hood"),
    "horned_helm": lambda s: helm(s, "horned"),
    "spiked_helm": lambda s: helm(s, "spiked"),
    "necklace_of_thorns": thorn_necklace,
    "oak_shield": lambda s: shield(s, "oak"),
    "mirror_shield": lambda s: shield(s, "mirror"),
    "rusty_cleaver": lambda s: blade(s, RUST, "cleaver"),
    "scaled_dagger": lambda s: (blade(s, STEEL, "dagger"), scaled_grip(s)),
    "smuggler_dagger": lambda s: blade(s, OBSIDIAN, "curved", "#2a1a10", BRONZE),
    "silver_saber": lambda s: blade(s, SILVER, "saber", "#2a3a6a"),
    "twilight_widow_blade": lambda s: blade(s, [(0, "#e8e0ff"), (0.5, "#8a7aa8"), (1, "#2a2232")], "sickle"),
    "star_academy_robe": lambda s: robe(s, CLOTH["blue"], "#c8d8ff", "star", "#fff6c8", hood=True),
    # кольца
    "ring_of_iron_skin": lambda s: ring(s, IRON, ("#c8ccd2", "#4a5058"), "shield_heart", "#d8dce4"),
    "ring_of_magicka": lambda s: ring(s, SILVER, ("#a8d0ff", "#1a3a9a"), "star", "#e8f4ff"),
    "ring_of_stamina": lambda s: ring(s, GOLD, ("#c8ff8a", "#2a7a1a"), "bolt", "#fff27a"),
    "ring_of_clear_mind": lambda s: ring(s, SILVER, ("#ffffff", "#a8b0c8"), None),
    "ring_of_hoarfrost": lambda s: ring(s, ICE, ("#e8f8ff", "#4a8ac0"), "star", "#ffffff", band_motif="thorns"),
    "ring_of_quick_breath": lambda s: ring(s, SILVER, ("#d8fff4", "#3a9a8a"), "wind", "#ffffff"),
    "ring_of_the_drunkard": lambda s: ring(s, BRONZE, ("#ff8ab0", "#7a0a2a"), "voice", "#ffd0e0"),
    "ring_of_the_faceless_thief": lambda s: ring(s, OBSIDIAN, None, "mask", "#e8e8f0"),
    "ring_of_the_pyromancer": lambda s: ring(s, GOLD, ("#ffd080", "#a02a0a"), "flame", "#ff8a2e"),
    "ring_of_the_serpent_smile": lambda s: ring(s, [(0, "#c8f0a0"), (1, "#2a5a1a")], ("#e8ff8a", "#4a7a0a"), "serpent", "#3a6a1a"),
    "ring_of_the_summoner": lambda s: ring(s, OBSIDIAN, ("#d8b0ff", "#4a1a8a"), "skull", "#f0e8d8", band_motif="runes"),
    "ring_of_the_viceroy": lambda s: ring(s, GOLD, None, "crown", "#c02a3a"),
    "ring_of_the_witch_hunter": lambda s: ring(s, SILVER, ("#ffffff", "#8a90a0"), "sun_cross", "#c8a050"),
    # подвески и ожерелья
    "amulet_of_the_deadspeaker": deadspeaker,
    "amulet_of_the_hearth": ember_pendant,
    "amulet_of_the_hunter": hunter_tooth,
    "amulet_of_the_weaver": spindle,
    "necklace_of_starstone": starstone,
    "necklace_of_the_void": void_pendant,
    "vial_pendant": vial_pendant,
    # посохи
    "abyssal_staff": lambda s: staff(s, OBSIDIAN, staff_head("void")),
    "everfrost_staff": lambda s: staff(s, ICE, staff_head("ice")),
    "oak_staff": lambda s: staff(s, WOOD, staff_head("oak")),
    "obsidian_staff": lambda s: staff(s, OBSIDIAN, staff_head("obsidian")),
    "silver_staff": lambda s: staff(s, SILVER, staff_head("silver")),
    "element_staff": lambda s: staff(s, DARKWOOD, staff_head("element")),
    # знаки пути
    "faceless_mask": faceless_mask,
    "monk_wraps": wraps,
    "regiment_banner": banner,
    "runic_quiver": quiver,
    "spirit_bell": bell,
    # походное
    "act_ledger": lambda s: roadmap(s),
    "barrow_map": lambda s: roadmap(s, barrow=True),
    "bottle_of_mead": mead_bottle,
    "camp_kettle": kettle,
    "feather_sack": lambda s: sack(s, CLOTH["white"], feather=True),
    "herbalist_pouch": lambda s: sack(s, LEATHER, herbs=True),
    "travel_pack": lambda s: sack(s, CLOTH["brown"], bedroll=True),
    "sharpening_stone": whetstone,
    "torch": torch,
    "pack_mule": mule,
    "memory_stone_relic": candles,
    "merchant_ledger": ledger,
    "hero_record": chronicle,
    "grimoire": grimoire,
    # формы и договоры
    "dark_pact_form": contract,
    "herald_form": lantern,
    "lich_form": lich,
    "phylactery": phylactery,
    "vampire_form": bat_form,
    "righteous_form": righteous,
    "werewolf_form": werewolf,
})


def build(key: str) -> Svg:
    s = Svg(S, S)
    s.open(f'filter="{s.grain(0.16, 0.9, sum(map(ord, key)) % 89)}"')
    ICONS[key](s)
    s.close()
    s.defs = [d.replace('x="0" y="0" width="100%" height="100%"', 'x="-25%" y="-25%" width="150%" height="150%"') for d in s.defs]
    return s


def outputs():
    for key in sorted(ICONS):
        yield (key, os.path.join(OUT_SVG, key + ".svg"), os.path.join(OUT_IMG, key + ".png"),
               os.path.join(OUT_IMG, key + "_outline.png"), os.path.join(OUT_IMG, "big", key + ".png"))


def check() -> int:
    bad = []
    clash = set(ICONS) & set(origins.ICONS)
    if clash:
        bad.append(f"ключи совпадают со значками происхождений: {sorted(clash)}")
    for key, svg, small, outline, big in outputs():
        if not os.path.exists(svg) or open(svg, encoding="utf-8").read() != build(key).render():
            bad.append(f"{os.path.relpath(svg, ROOT)}: не совпадает с генератором (запустите с --render)")
        for path, size in ((small, origins.SMALL), (outline, origins.SMALL), (big, S)):
            if not os.path.exists(path):
                bad.append(f"{os.path.relpath(path, ROOT)}: нет картинки")
            elif origins.png_size(path) != (size, size):
                bad.append(f"{os.path.relpath(path, ROOT)}: размер {origins.png_size(path)}, нужно {size}×{size}")
    for line_ in bad:
        print("✗", line_)
    print(f"свои значки реликвий (M7 п. 6): {len(ICONS)}, ошибок: {len(bad)}")
    return 1 if bad else 0


def main(argv) -> int:
    if "--check" in argv:
        return check()
    only = [a for a in argv[1:] if not a.startswith("--")]
    os.makedirs(OUT_SVG, exist_ok=True)
    pairs, jobs = [], []
    tmp = tempfile.mkdtemp()
    for key, svg, small, outline, big in outputs():
        if only and key not in only:
            continue
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
        origins.finish(*job)
    print(f"PNG: {len(jobs)} × 3 → {os.path.relpath(OUT_IMG, ROOT)}")
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv))
