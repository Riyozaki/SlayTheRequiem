"""Манифест ассетов мода: какие картинки ждёт код, что из них своё, что берётся из игры, чего нет вовсе.

Политика «сначала ванильное» (docs/plan/art-ui-animation-spec.md §0): модель без своего файла показывает ванильный
портрет/значок — карты, силы, реликвии, события, чары по таблице tools/art/vanilla_art.csv (→ Art/VanillaArtTable.g.cs),
зелья — ванильную бутылку (VanillaArt), враги — облик ванильного врага (LookOf), действия лагеря — FallbackIcon.
Колонка present: own (свой файл), vanilla (ванильный арт, id в колонке vanilla), no (заглушка шаблона).

Источник правды — сами классы моделей (как в tools/ci/check_loc.py): конкретные sealed-наследники карт, сил, реликвий,
зелий, событий, чар, врагов и действий лагеря. Пути — те же, что строят `Extensions/StringExtensions.cs`,
`Cards/RequiemCard.cs`, `Powers/RequiemPower.cs`, `Relics/RequiemRelic.cs`, `Potions/Potions.cs`,
`Pantheon/PantheonEvents.cs`, `Alchemy/CampOptions.cs`.

Запуск:
    python3 tools/assets/asset_manifest.py             # сводка в stdout
    python3 tools/assets/asset_manifest.py --csv out.csv  # полный список (для художников / трекера)
    python3 tools/assets/asset_manifest.py --check     # CI: код 1, если у модели нет ни своей, ни ванильной картинки

Колонки CSV: kind, class, id, group (файл-источник), title_rus, title_eng, asset, path, present, vanilla, note.
"""
from __future__ import annotations

import argparse
import collections
import csv
import glob
import json
import os
import re
import sys

ROOT = os.path.normpath(os.path.join(os.path.dirname(__file__), "..", ".."))
CODE = os.path.join(ROOT, "mod", "SlayTheRequiemCode")
RES = os.path.join(ROOT, "mod", "SlayTheRequiem")
LOC = os.path.join(RES, "localization")
PREFIX = "SLAYTHEREQUIEM-"

DECL = re.compile(r"\b(?P<mods>(?:(?:public|internal|sealed|abstract|static|partial)\s+)*)class\s+(?P<name>\w+)"
                  r"(?:<[^>]*>)?(?:\([^)]*\))?\s*:\s*(?P<base>[\w.]+)")
LOOK = re.compile(r"LookOf\s*=>\s*ModelDb\.Monster<(?:[\w]+\.)*(?P<look>\w+)>|GetScenePath\(\"creature_visuals/(?P<scene>\w+)\"\)")

# База мода → (вид, файл локализации, поле названия)
BASES = {
    "RequiemCard": ("card", "cards.json", "title"),
    "RequiemPower": ("power", "powers.json", "title"),
    "RequiemRelic": ("relic", "relics.json", "title"),
    "RequiemPotion": ("potion", "potions.json", "title"),
    "PantheonEvent": ("event", "events.json", "title"),
    "RequiemCardEnchantment": ("enchantment", "enchantments.json", "title"),
    "CustomMonsterModel": ("monster", "monsters.json", "name"),
    "RequiemRestSiteOption": ("rest_option", None, None),
}

# Что нужно каждому виду: (имя ассета, шаблон пути относительно mod/SlayTheRequiem, примечание)
ASSETS = {
    "card": [
        ("portrait", "images/card_portraits/{id}.png", "250×190, малый портрет; иначе — ванильный по vanilla_art.csv"),
        ("portrait_big", "images/card_portraits/big/{id}.png", "1000×760, основной арт карты (CustomPortraitPath)"),
    ],
    "power": [
        ("icon", "images/powers/{id}.png", "64×64 RGBA, иконка над полосой ♥"),
        ("icon_big", "images/powers/big/{id}.png", "256×256 RGBA, иконка в подсказке"),
    ],
    "relic": [
        ("icon", "images/relics/{id}.png", "94×94 RGBA, панель реликвий"),
        ("outline", "images/relics/{id}_outline.png", "94×94 RGBA, белый контур для подсветки"),
        ("icon_big", "images/relics/big/{id}.png", "256×256 RGBA, инспекция и награда"),
    ],
    "potion": [
        ("image", "images/potions/{id}.png", "размер как у ванильных зелий; иначе — ванильная бутылка"),
        ("outline", "images/potions/outline/{id}.png", "контур"),
    ],
    "event": [
        ("image", "images/events/{id}.png", "размер как у ванильных событий; иначе — ванильный портрет по vanilla_art.csv"),
    ],
    "enchantment": [
        ("icon", "images/enchantments/{id}.png", "свой значок или ванильный по vanilla_art.csv (CustomIconPath)"),
    ],
    "monster": [
        ("visual", "scenes/creature_visuals/{id}.tscn", "пока облик ванильного врага (LookOf)"),
    ],
    "rest_option": [
        ("icon", "images/ui/rest_site/option_{option}.png", "размер как у ванильных иконок лагеря; иначе — ванильная (FallbackIcon)"),
    ],
}

PLACEHOLDERS = {
    "images/card_portraits/card.png", "images/card_portraits/big/card.png", "images/powers/power.png",
    "images/powers/big/power.png", "images/relics/relic.png", "images/relics/relic_outline.png",
    "images/relics/big/relic.png",
}

# Общие ассеты интерфейса, которые нужны вне моделей (см. docs/plan/art-ui-animation-spec.md §4).
UI_ASSETS = [
    ("images/ui/stamina.png", "◆ иконка ресурса (есть заглушка шаблона)"),
    ("images/ui/mana.png", "✦ иконка ресурса (есть заглушка шаблона)"),
    ("images/ui/burden.png", "⚖ иконка груза"),
    ("images/ui/stamina_cost_orb.png", "кружок цены ◆ на карте"),
    ("images/ui/weight_badge.png", "плашка веса карты"),
    ("images/ui/damage_types/frost.png", "тип урона: холод"),
    ("images/ui/damage_types/fire.png", "тип урона: огонь"),
    ("images/ui/damage_types/shock.png", "тип урона: разряд"),
    ("images/ui/damage_types/poison.png", "тип урона: яд"),
    ("images/ui/damage_types/blight.png", "тип урона: мор"),
    ("images/ui/damage_types/light.png", "тип урона: свет"),
    ("images/ui/damage_types/magic.png", "тип урона: магия"),
    ("images/ui/damage_types/true.png", "тип урона: чистый"),
    ("images/ui/intents/breather.png", "намерение «Отдышаться»"),
    ("images/ui/intents/spell.png", "намерение-заклинание (цена ✦)"),
    ("images/ui/slots/paper_doll.png", "фон куклы снаряжения"),
    ("images/map/icons/shrine.png", "значок карты ☀"),
    ("images/map/icons/dark_altar.png", "значок карты ⛧"),
    ("images/map/icons/armorer.png", "значок карты ⚒"),
    ("images/map/icons/enchanter.png", "значок карты ✧"),
    ("images/map/icons/herbalist.png", "значок карты ⚗"),
    ("images/map/icons/jeweler.png", "значок карты ◈"),
    ("images/map/icons/caravan.png", "значок карты 🐪"),
    ("images/map/icons/fence.png", "значок карты 🗝"),
    ("images/map/icons/tavern.png", "значок карты 🍺"),
    ("images/map/icons/library.png", "значок карты 📖"),
    ("images/map/icons/lair.png", "значок карты 🐾"),
    ("images/map/icons/trial.png", "значок карты ⚔"),
    ("images/map/icons/cache.png", "значок карты ⚿"),
    ("images/map/icons/locked_gate.png", "значок карты ⛓"),
    ("images/map/icons/valve.png", "значок карты ⚙"),
    ("images/map/icons/abyss_gate.png", "значок карты ◉"),
    ("images/map/icons/crimson_portal.png", "значок карты ✹"),
    ("images/map/icons/memory_stone.png", "значок карты ◇"),
    ("images/map/icons/ebb_stones.png", "значок карты ≈"),
    ("images/map/icons/ambush.png", "значок карты !"),
    ("images/ui/doors/frozen_barrows.jpg", "«две двери»: вид на Ледяные Курганы (tools/art/scenes)"),
    ("images/ui/doors/clockwork_depths.jpg", "«две двери»: вид на Заводные Глубины (tools/art/scenes)"),
    ("images/ui/doors/blood_court.jpg", "«две двери»: вид на Кровавый Двор (tools/art/scenes)"),
    ("images/ui/doors/star_tower.jpg", "«две двери»: вид на Звёздную Башню (tools/art/scenes)"),
]


def snake(name: str) -> str:
    return re.sub(r"(?<=[a-z0-9])(?=[A-Z])", "_", name).lower()


def load_classes():
    classes = {}
    for path in glob.glob(os.path.join(CODE, "**", "*.cs"), recursive=True):
        src = re.sub(r"//[^\n]*", "", open(path, encoding="utf-8").read())
        matches = list(DECL.finditer(src))
        for i, m in enumerate(matches):
            body = src[m.end(): matches[i + 1].start() if i + 1 < len(matches) else len(src)]
            look = LOOK.search(body)
            classes[m.group("name")] = {
                "base": m.group("base").split(".")[-1],
                "abstract": "abstract" in m.group("mods"),
                "sealed": "sealed" in m.group("mods"),
                "file": os.path.relpath(path, CODE),
                "look": (look.group("look") or look.group("scene")) if look else None,
                "option": (re.search(r'OptionId\s*=>\s*"(\w+)"', body) or [None, None])[1],
                "fallback_icon": (re.search(r'FallbackIcon\s*=>\s*"(\w+)"', body) or [None, None])[1],
                "potion_art": (re.search(r'VanillaArt\s*=>\s*"(\w+)"', body) or [None, None])[1],
                "art_key": (re.search(r'ArtKey\s*=>\s*"(\w+)"', body) or [None, None])[1],
                "icon_key": (re.search(r'IconKey\s*=>\s*"(\w+)"', body) or [None, None])[1],
            }
    return classes


def kind_of(name, classes, depth=0):
    if name in BASES:
        return name
    info = classes.get(name)
    if info is None or depth > 20:
        return None
    return kind_of(info["base"], classes, depth + 1)


def inherited_look(name, classes):
    while name in classes:
        if classes[name]["look"]:
            return classes[name]["look"]
        name = classes[name]["base"]
    return None


VANILLA_TABLE = os.path.join(CODE, "Art", "VanillaArtTable.g.cs")
TABLE_KIND = {"CardTable": "card", "PowerTable": "power", "RelicTable": "relic", "EventTable": "event",
              "EnchantmentTable": "enchantment"}


def load_vanilla_table():
    """kind → {класс мода: ванильный id} из сгенерированной таблицы (tools/art/build_vanilla_art.py)."""
    table = collections.defaultdict(dict)
    if not os.path.exists(VANILLA_TABLE):
        return table
    kind = None
    for line in open(VANILLA_TABLE, encoding="utf-8"):
        m = re.search(r"Dictionary<string, string> (\w+)", line)
        if m:
            kind = TABLE_KIND.get(m.group(1))
            continue
        m = re.search(r'\["(\w+)"\] = "([\w/]+)"', line)
        if m and kind:
            table[kind][m.group(1)] = m.group(2)
    return table


def inherited(name, classes, key):
    while name in classes:
        if classes[name].get(key):
            return classes[name][key]
        name = classes[name]["base"]
    return None


def load_loc():
    tables = {}
    for lang in ("rus", "eng"):
        for path in glob.glob(os.path.join(LOC, lang, "*.json")):
            tables[(lang, os.path.basename(path))] = json.load(open(path, encoding="utf-8"))
    return tables


def build_rows():
    classes = load_classes()
    loc = load_loc()
    vanilla = load_vanilla_table()
    rows = []
    for name, info in sorted(classes.items()):
        if info["abstract"] or not info["sealed"]:
            continue
        base = kind_of(info["base"], classes)
        if base is None:
            continue
        kind, loc_file, field = BASES[base]
        ident = snake(name)
        titles = {}
        for lang in ("rus", "eng"):
            table = loc.get((lang, loc_file), {}) if loc_file else {}
            titles[lang] = table.get(f"{PREFIX}{ident.upper()}.{field}", "")
        option = (info["option"] or ident).lower().removeprefix("slaytherequiem_")
        if kind == "monster":
            borrowed = inherited_look(name, classes)
        elif kind == "potion":
            borrowed = inherited(name, classes, "potion_art") or "blood_potion"
        elif kind == "rest_option":
            borrowed = inherited(name, classes, "fallback_icon")
        else:
            borrowed = vanilla.get(kind, {}).get(name)
        assets = ASSETS[kind]
        art_key = info.get("art_key") if kind == "monster" else None
        if art_key:
            # Свой спрайт (Monsters/OwnArtMonster.cs): PNG вместо сцены облика, варианты — {key}_{variant}.png.
            assets = [("sprite", f"images/monsters/{art_key}.png", "свой спрайт + код-анимации (OwnArtMonster)")]
        for asset, template, note in assets:
            rel = template.format(id=ident, option=option)
            own = os.path.exists(os.path.join(RES, rel)) and rel not in PLACEHOLDERS
            rows.append({
                "kind": kind, "class": name, "id": ident, "group": info["file"],
                "title_rus": titles["rus"], "title_eng": titles["eng"],
                "asset": asset, "path": rel, "present": "own" if own else "vanilla" if borrowed else "no",
                "vanilla": borrowed or "", "note": note,
            })
    # Значки боссов актов на карте (Monsters/ActBosses.cs → ActBossEncounter.IconKey): значок + контур, как placeholder/*_icon.
    for name, info in sorted(classes.items()):
        if not info.get("icon_key") or info["abstract"]:
            continue
        for suffix in ("", "_outline"):
            rel = f"images/map/bosses/{info['icon_key']}{suffix}.png"
            present = os.path.exists(os.path.join(RES, rel))
            rows.append({"kind": "boss_icon", "class": name, "id": info["icon_key"] + suffix, "group": info["file"],
                         "title_rus": "", "title_eng": "", "asset": "icon" + suffix, "path": rel,
                         "present": "own" if present else "vanilla", "vanilla": "VantomBoss",
                         "note": "без файла — значок Вантома (BossNodePath)"})
    for rel, note in UI_ASSETS:
        present = os.path.exists(os.path.join(RES, rel))
        rows.append({"kind": "ui", "class": "", "id": os.path.splitext(os.path.basename(rel))[0], "group": "UI",
                     "title_rus": note, "title_eng": "", "asset": "ui", "path": rel,
                     "present": "own" if present else "no", "vanilla": "",
                     "note": "заглушка шаблона — заменить" if rel in ("images/ui/stamina.png", "images/ui/mana.png") else ""})
    order = {k: i for i, k in enumerate(list(ASSETS) + ["boss_icon", "ui"])}
    rows.sort(key=lambda r: (order[r["kind"]], r["group"], r["class"]))
    return rows


def main() -> int:
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("--csv", help="записать полный манифест в CSV")
    ap.add_argument("--check", action="store_true", help="код возврата 1, если чего-то не хватает")
    args = ap.parse_args()

    rows = build_rows()
    models = collections.Counter()
    count = collections.defaultdict(collections.Counter)
    seen = set()
    for r in rows:
        if (r["kind"], r["class"]) not in seen and r["kind"] != "ui":
            seen.add((r["kind"], r["class"]))
            models[r["kind"]] += 1
        count[r["kind"]]["all"] += 1
        count[r["kind"]][r["present"]] += 1

    print(f"{'вид':<12}{'моделей':>9}{'файлов':>9}{'свои':>7}{'ваниль':>8}{'нет':>7}")
    for kind in list(ASSETS) + ["ui"]:
        c = count[kind]
        print(f"{kind:<12}{models.get(kind, 0):>9}{c['all']:>9}{c['own']:>7}{c['vanilla']:>8}{c['no']:>7}")
    total = collections.Counter()
    for c in count.values():
        total.update(c)
    print(f"{'итого':<12}{sum(models.values()):>9}{total['all']:>9}{total['own']:>7}{total['vanilla']:>8}{total['no']:>7}")
    uncovered = [r for r in rows if r["kind"] != "ui" and r["present"] == "no"]
    for r in uncovered[:20]:
        print(f"✗ нет ни своей, ни ванильной картинки: {r['kind']} {r['class']} ({r['asset']})")

    if args.csv:
        with open(args.csv, "w", encoding="utf-8", newline="") as f:
            w = csv.DictWriter(f, fieldnames=list(rows[0]), lineterminator="\n")
            w.writeheader()
            w.writerows(rows)
        print(f"CSV: {args.csv} ({len(rows)} строк)")
    return 1 if args.check and uncovered else 0


if __name__ == "__main__":
    sys.exit(main())
