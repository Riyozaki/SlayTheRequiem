#!/usr/bin/env python3
"""Проверка локализации мода (CI).

1. eng и rus содержат одинаковые ключи, пустых строк нет, JSON читается.
2. У каждой конкретной (sealed) модели — карты, силы, реликвии, зелья, монстра, встречи, чары — есть заголовок.
   Наследование строится по объявлениям классов в mod/SlayTheRequiemCode, корни — игровые/BaseLib-базы.

Запуск: python3 tools/ci/check_loc.py  (код выхода 1 при ошибках)
"""
import glob
import json
import os
import re
import sys

ROOT = os.path.join(os.path.dirname(__file__), "..", "..")
LOC = os.path.join(ROOT, "mod", "SlayTheRequiem", "localization")
CODE = os.path.join(ROOT, "mod", "SlayTheRequiemCode")
PREFIX = "SLAYTHEREQUIEM-"

# корневой тип движка/BaseLib → (файл локализации, поле)
ROOTS = {
    "CardModel": ("cards.json", "title"), "CustomCardModel": ("cards.json", "title"),
    "PowerModel": ("powers.json", "title"), "CustomPowerModel": ("powers.json", "title"),
    "RelicModel": ("relics.json", "title"), "CustomRelicModel": ("relics.json", "title"),
    "PotionModel": ("potions.json", "title"), "CustomPotionModel": ("potions.json", "title"),
    "MonsterModel": ("monsters.json", "name"), "CustomMonsterModel": ("monsters.json", "name"),
    "EncounterModel": ("encounters.json", "title"), "CustomEncounterModel": ("encounters.json", "title"),
    "EnchantmentModel": ("enchantments.json", "title"), "CustomEnchantmentModel": ("enchantments.json", "title"),
}

DECL = re.compile(r"\b(?P<mods>(?:(?:public|internal|sealed|abstract|static|partial)\s+)*)class\s+(?P<name>\w+)"
                  r"(?:<[^>]*>)?(?:\([^)]*\))?\s*:\s*(?P<base>[\w.]+)")


def snake(name: str) -> str:
    return re.sub(r"(?<=[a-z0-9])(?=[A-Z])", "_", name).upper()


def check_parity(errors: list) -> None:
    for eng_path in sorted(glob.glob(os.path.join(LOC, "eng", "*.json"))):
        name = os.path.basename(eng_path)
        rus_path = os.path.join(LOC, "rus", name)
        try:
            eng = json.load(open(eng_path, encoding="utf-8"))
            rus = json.load(open(rus_path, encoding="utf-8")) if os.path.exists(rus_path) else {}
        except json.JSONDecodeError as e:
            errors.append(f"{name}: битый JSON — {e}")
            continue
        for key in sorted(set(eng) - set(rus)):
            errors.append(f"rus/{name}: нет ключа {key}")
        for key in sorted(set(rus) - set(eng)):
            errors.append(f"eng/{name}: нет ключа {key}")
        for lang, data in (("eng", eng), ("rus", rus)):
            for key, value in data.items():
                if isinstance(value, str) and not value.strip():
                    errors.append(f"{lang}/{name}: пустая строка {key}")


def check_models(errors: list) -> int:
    classes = {}
    for path in glob.glob(os.path.join(CODE, "**", "*.cs"), recursive=True):
        src = re.sub(r"//[^\n]*", "", open(path, encoding="utf-8").read())
        for m in DECL.finditer(src):
            classes[m.group("name")] = (m.group("base").split(".")[-1], "abstract" in m.group("mods"),
                                        "sealed" in m.group("mods"), os.path.relpath(path, ROOT))

    def root_of(name: str, depth: int = 0):
        if name in ROOTS:
            return ROOTS[name]
        if name not in classes or depth > 20:
            return None
        return root_of(classes[name][0], depth + 1)

    tables = {}
    checked = 0
    for name, (base, abstract, sealed, path) in sorted(classes.items()):
        if abstract or not sealed:
            continue
        target = root_of(base)
        if target is None:
            continue
        file, field = target
        if file not in tables:
            p = os.path.join(LOC, "eng", file)
            tables[file] = json.load(open(p, encoding="utf-8")) if os.path.exists(p) else {}
        checked += 1
        if f"{PREFIX}{snake(name)}.{field}" not in tables[file]:
            errors.append(f"{path}: {name} — нет {file} → {PREFIX}{snake(name)}.{field}")
    return checked


def main() -> int:
    errors: list = []
    check_parity(errors)
    checked = check_models(errors)
    for e in errors:
        print("✗", e)
    print(f"Моделей проверено: {checked}; ошибок: {len(errors)}")
    return 1 if errors else 0


if __name__ == "__main__":
    sys.exit(main())
