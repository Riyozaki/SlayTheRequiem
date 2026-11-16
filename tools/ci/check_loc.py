#!/usr/bin/env python3
"""Проверка локализации мода (CI).

1. eng и rus содержат одинаковые ключи, пустых строк нет, JSON читается.
2. У каждой конкретной (sealed) модели — карты, силы, реликвии, зелья, монстра, встречи, чары — есть заголовок.
   Наследование строится по объявлениям классов в mod/SlayTheRequiemCode, корни — игровые/BaseLib-базы.
4. У каждого ключевого слова мода ([CustomEnum] CardKeyword) есть title/description в card_keywords.json, а у подсказок
   интерфейса (MakeTip/TipLoc/TipText и ключи состояний в mod/SlayTheRequiemCode/UI) — в static_hover_tips.json.
5. У каждого хода монстра (литерал MoveState("ID", …) в теле sealed-класса) есть monsters.json → <id>.moves.<ID>.title:
   без него намерение в бою показывает сырой ключ.
3. Имена классов моделей уникальны: id модели = префикс мода + имя класса (без пространства имён), и второй
   одноимённый класс молча затирает первый в ModelDb (ревью R-07).

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
    "ConstructedCardModel": ("cards.json", "title"),  # BaseLib: база RequiemCard — без неё карты не проверялись
    "EventModel": ("events.json", "title"), "CustomEventModel": ("events.json", "title"),
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
    decls = {}
    for path in sorted(glob.glob(os.path.join(CODE, "**", "*.cs"), recursive=True)):
        src = re.sub(r"//[^\n]*", "", open(path, encoding="utf-8").read())
        for m in DECL.finditer(src):
            classes[m.group("name")] = (m.group("base").split(".")[-1], "abstract" in m.group("mods"),
                                        "sealed" in m.group("mods"), os.path.relpath(path, ROOT))
            if "partial" not in m.group("mods"):
                decls.setdefault(m.group("name"), []).append(os.path.relpath(path, ROOT))

    def root_of(name: str, depth: int = 0):
        if name in ROOTS:
            return ROOTS[name]
        if name not in classes or depth > 20:
            return None
        return root_of(classes[name][0], depth + 1)

    for name, paths in sorted(decls.items()):
        if len(paths) > 1 and root_of(classes[name][0]) is not None:
            errors.append(f"{name}: одно имя у {len(paths)} моделей — один id, вторая затрёт первую ({', '.join(paths)})")

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


KEYWORD = re.compile(r"\[CustomEnum[^\]]*\](?:\s*\[[^\]]*\])*\s*public\s+static\s+CardKeyword\s+(\w+)\s*;")
FLAT_TIP = re.compile(r'(?:TipLoc|TipText)\(\$?"((?:[A-Z][A-Z_]*|\{[^}"]*\})(?:\.(?:[A-Za-z_]+|\{[^}"]*\}))*\.(?!title"|description")[a-z][A-Za-z]*)"')
UI_TIP = re.compile(r'(?:MakeTip|TipLoc|TipText)\(\$?"([A-Z][A-Z_]*)[.{"]|(?:=>|return)\s+"([A-Z][A-Z_]+)"')


def check_ui_keys(errors: list) -> int:
    """Ключевые слова BaseLib и подсказки UI: ключ без текста в игре показывается как сам ключ."""
    checked = 0
    tables = {}
    for lang in ("eng", "rus"):
        for file in ("card_keywords.json", "static_hover_tips.json"):
            p = os.path.join(LOC, lang, file)
            tables[(lang, file)] = json.load(open(p, encoding="utf-8")) if os.path.exists(p) else {}
    for path in glob.glob(os.path.join(CODE, "**", "*.cs"), recursive=True):
        src = open(path, encoding="utf-8-sig").read()
        wanted = [("card_keywords.json", name.upper()) for name in KEYWORD.findall(src)]
        if os.sep + "UI" + os.sep in path:
            # Плоские ключи экранов («DOORS.title», «DOORS.{Key(kind)}.foes»): строка целиком, без полей title/description.
            for literal in FLAT_TIP.findall(src):
                checked += 1
                pattern = re.compile("^" + re.escape(PREFIX) + re.sub(r"\\\{[^}]*\\\}", "[A-Z_]+", re.escape(literal)) + "$")
                for lang in ("eng", "rus"):
                    if not any(pattern.match(k) for k in tables[(lang, "static_hover_tips.json")]):
                        errors.append(f"{os.path.relpath(path, ROOT)}: нет {lang}/static_hover_tips.json → {PREFIX}{literal}")
            src = FLAT_TIP.sub("", src)
            wanted += [("static_hover_tips.json", a or b) for a, b in UI_TIP.findall(src)]
        for file, key in wanted:
            checked += 1
            for lang in ("eng", "rus"):
                for field in ("title", "description"):
                    if f"{PREFIX}{key}.{field}" not in tables[(lang, file)]:
                        errors.append(f"{os.path.relpath(path, ROOT)}: нет {lang}/{file} → {PREFIX}{key}.{field}")
    return checked


MOVE = re.compile(r'new\s+MoveState\(\s*"([A-Z0-9_]+)"')


def check_moves(errors: list) -> int:
    """Ходы монстров: id хода — ключ заголовка намерения."""
    table = json.load(open(os.path.join(LOC, "eng", "monsters.json"), encoding="utf-8"))
    checked = 0
    for path in sorted(glob.glob(os.path.join(CODE, "**", "*.cs"), recursive=True)):
        src = re.sub(r"//[^\n]*", "", open(path, encoding="utf-8").read())
        decls = list(DECL.finditer(src))
        for i, m in enumerate(decls):
            if "sealed" not in m.group("mods"):
                continue
            body = src[m.end():decls[i + 1].start() if i + 1 < len(decls) else len(src)]
            for move in sorted(set(MOVE.findall(body))):
                checked += 1
                key = f"{PREFIX}{snake(m.group('name'))}.moves.{move}.title"
                if key not in table:
                    errors.append(f"{os.path.relpath(path, ROOT)}: {m.group('name')} — нет monsters.json → {key}")
    return checked


def main() -> int:
    errors: list = []
    check_parity(errors)
    checked = check_models(errors)
    checked += check_moves(errors)
    checked += check_ui_keys(errors)
    for e in errors:
        print("✗", e)
    print(f"Моделей проверено: {checked}; ошибок: {len(errors)}")
    return 1 if errors else 0


if __name__ == "__main__":
    sys.exit(main())
