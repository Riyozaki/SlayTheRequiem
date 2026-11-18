#!/usr/bin/env python3
"""Source-image contact sheets for B3.1 review. NOT game screenshots, UI/input or visual acceptance.

python tools/art/quality_previews.py --out docs/review/assets/stage-three-2026-11-18
"""
from __future__ import annotations
import argparse
from pathlib import Path
from PIL import Image, ImageDraw, ImageFont, ImageOps
import readable_icons as icons

ROOT = Path(__file__).resolve().parents[2]
RES = ROOT / "mod/SlayTheRequiem/images"
FONT = ImageFont.truetype("DejaVuSans.ttf", 14)
HEAD = ImageFont.truetype("DejaVuSans.ttf", 19)
BG, TEXT = "#1a2028", "#eee3c8"


def label(draw, xy, text, font=FONT):
    draw.text(xy, text, fill=TEXT, font=font)


def tile(sheet, x, y, size, key, background, gray=False, outline=False):
    cell = Image.new("RGBA", (92, 72), background)
    with Image.open(RES / f"map/icons/{key}{'_outline' if outline else ''}.png") as raw:
        icon = raw.convert("RGBA").resize((size, size), Image.Resampling.LANCZOS)
    if gray:
        alpha = icon.getchannel("A")
        icon = ImageOps.grayscale(icon).convert("RGBA"); icon.putalpha(alpha)
    cell.alpha_composite(icon, ((92 - size) // 2, (72 - size) // 2))
    sheet.paste(cell.convert("RGB"), (x, y))


def maps(out):
    sheet = Image.new("RGB", (1010, 84 + 78 * len(icons.MAP)), BG); draw = ImageDraw.Draw(sheet)
    label(draw, (18, 12), "B3.1 / Source previews only - not game screenshots", HEAD)
    for x, title in ((205, "64 / paper"), (355, "32 / paper"), (505, "32 / dark"), (655, "32 / gray"), (805, "32 / mask")):
        label(draw, (x, 50), title)
    for i, key in enumerate(sorted(icons.MAP)):
        y = 80 + i * 78; label(draw, (16, y + 25), key)
        tile(sheet, 205, y, 64, key, "#c9b68d")
        tile(sheet, 355, y, 32, key, "#c9b68d")
        tile(sheet, 505, y, 32, key, "#101720")
        tile(sheet, 655, y, 32, key, "#525252", gray=True)
        tile(sheet, 805, y, 32, key, "#101720", outline=True)
    sheet.save(out / "map-native-size.png", optimize=True)


def camp(out):
    keys = sorted(icons.CAMP)
    sheet = Image.new("RGB", (1080, 380), BG); draw = ImageDraw.Draw(sheet)
    label(draw, (18, 12), "Camp symbols / 64 px preview of 192 px runtime files / not a camp screenshot", HEAD)
    for i, key in enumerate(keys):
        x = 14 + (i % 7) * 152; y = 64 + (i // 7) * 150
        im = Image.open(RES / f"ui/rest_site/option_slaytherequiem_{key}.png").convert("RGBA").resize((64, 64), Image.Resampling.LANCZOS)
        sheet.paste(im, (x + 36, y), im)
        label(draw, (x + 4, y + 78), key)
    sheet.save(out / "camp-symbols.png", optimize=True)


def cards(out):
    keys = (("shield_bash", "Platebearer common Attack"), ("backstab", "Silent common Attack"),
            ("arcane_focus", "Regent starter/common Skill"), ("whirlwind", "Barbarian Path Attack"),
            ("focused_aim", "Mystic Archer Path Skill"), ("firebolt", "Early Destruction spell"))
    sheet = Image.new("RGB", (840, 600), BG); draw = ImageDraw.Draw(sheet)
    label(draw, (18, 12), "Card scene candidates / actual 250 x 190 exports / not in-game acceptance", HEAD)
    for i, (key, intent) in enumerate(keys):
        x = 20 + (i % 3) * 275; y = 50 + (i // 3) * 280
        with Image.open(RES / f"card_portraits/{key}.png") as raw:
            sheet.paste(raw.convert("RGB"), (x, y + 26))
        label(draw, (x, y), key)
        label(draw, (x, y + 228), intent)
    sheet.save(out / "card-scenes-250.png", optimize=True)


def sprites(out):
    keys = ("king_under_barrow", "king_under_barrow_p2", "hero_werewolf", "hero_werewolf_beast")
    sheet = Image.new("RGB", (1100, 660), BG); draw = ImageDraw.Draw(sheet)
    label(draw, (18, 12), "Phase / form source candidates - alpha cutouts, not a game screenshot", HEAD)
    for i, key in enumerate(keys):
        x = 18 + i * 270
        label(draw, (x, 58), key)
        with Image.open(RES / f"monsters/{key}.png") as raw:
            sprite = ImageOps.contain(raw.convert("RGBA"), (250, 260), Image.Resampling.LANCZOS)
        for y, bg in ((90, "#bfae8b"), (370, "#101720")):
            tile = Image.new("RGBA", (252, 266), bg)
            tile.alpha_composite(sprite, ((252 - sprite.width) // 2, 264 - sprite.height))
            sheet.paste(tile.convert("RGB"), (x, y))
    sheet.save(out / "phase-form-cutouts.png", optimize=True)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--out", type=Path, default=ROOT / ".arena/stage3/previews")
    out = parser.parse_args().out; out.mkdir(parents=True, exist_ok=True)
    maps(out); camp(out); cards(out); sprites(out)
    print(f"Source review previews -> {out}")


if __name__ == "__main__":
    main()
