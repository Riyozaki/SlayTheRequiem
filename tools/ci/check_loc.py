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
from pathlib import Path

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
    """Как StringHelper.Slugify игры (и tools/loc/build_loc.py): "([A-Za-z0-9]|\\G(?!^))([A-Z])" → "$1_$2".
    Серия заглавных продолжается через \\G: NodeDestructionII → NODE_DESTRUCTION_I_I (старый вариант давал
    NODE_DESTRUCTION_II, и проверка не видела, что игра ищет другой ключ)."""
    out: list[str] = []
    i, last_end = 0, -1
    while i < len(name):
        if i + 1 < len(name) and name[i].isalnum() and name[i + 1].isupper():
            out.append(name[i] + "_" + name[i + 1])
            i += 2
            last_end = i
        elif i == last_end and name[i].isupper():
            out.append("_" + name[i])
            i += 1
            last_end = i
        else:
            out.append(name[i])
            i += 1
    return "".join(out).upper()


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
            tables[file] = json.loads(Path(p).read_text(encoding="utf-8")) if os.path.exists(p) else {}
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
            tables[(lang, file)] = json.loads(Path(p).read_text(encoding="utf-8")) if os.path.exists(p) else {}
    for path in glob.glob(os.path.join(CODE, "**", "*.cs"), recursive=True):
        src = Path(path).read_text(encoding="utf-8-sig")
        wanted = [("card_keywords.json", name.upper()) for name in KEYWORD.findall(src)]
        # Fully specified dotted keys are not just their first uppercase segment (UI.journal.foo != UI).
        complete = re.compile(r'(?:TipLoc|TipText)\(\$?"([^"{\n]+\.(?:title|description))"')
        for literal in complete.findall(src):
            checked += 1
            for lang in ("eng", "rus"):
                if PREFIX + literal not in tables[(lang, "static_hover_tips.json")]:
                    errors.append(f"{os.path.relpath(path, ROOT)}: нет {lang} → {PREFIX}{literal}")
        src = complete.sub("", src)
        src = src.replace('TipText("UI.journal."', "")
        # Finite UI enum families are checked exhaustively below, not as a wildcard 'UI.title'.
        src = re.sub(r'(?:TipLoc|TipText)\(\$"UI\.[^"]+"', "", src)
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


def creation_keys(source: str, ui_source: str, stages: list[str], schools: list[str]) -> set[str]:
    """Finite dynamically assembled CREATION keys, not a C# compiler or a visual-layout test."""
    keys = set(re.findall(r'\bText\("([A-Za-z][A-Za-z0-9.]*)"', source))
    for a, b in re.findall(r'\bText\([^;\n]*?\?\s*"([A-Za-z][A-Za-z0-9.]*)"\s*:\s*"([A-Za-z][A-Za-z0-9.]*)"', source):
        keys.update((a, b))
    for body in re.findall(r'\bText\(\w+\s+switch\s*\{([^}]+)\}', source):
        keys.update(re.findall(r'=>\s*"([A-Za-z][A-Za-z0-9.]*)"', body))
    keys.update(re.findall(r'"CREATION\.([A-Za-z][A-Za-z0-9.]*)\.title"', ui_source))
    if "stage." in keys:
        keys.remove("stage.")
        keys.update("stage." + name for name in stages)
    if "school." in keys:
        keys.remove("school.")
        keys.update("school." + name for name in schools)
    return keys


def enum_names(path: str, name: str) -> list[str]:
    source = re.sub(r"//[^\n]*", "", Path(path).read_text(encoding="utf-8"))
    body = re.search(r"\benum\s+" + re.escape(name) + r"\s*\{([^}]+)\}", source)
    if not body:
        raise ValueError(f"Missing enum {name} in {path}")
    return [item.split("=")[0].strip() for item in body[1].split(",") if item.strip()]


def check_creation_keys(errors: list) -> int:
    source = Path(CODE, "Progression", "HeroCreation.cs").read_text(encoding="utf-8")
    ui = Path(CODE, "UI", "NHeroCreationScreen.cs").read_text(encoding="utf-8")
    keys = creation_keys(source, ui, enum_names(os.path.join(CODE, "Core", "HeroCreationPlan.cs"), "CreationStage"),
                         enum_names(os.path.join(CODE, "Core", "SpellAffinity.cs"), "SpellSchool"))
    tables = {lang: json.loads(Path(LOC, lang, "static_hover_tips.json").read_text(encoding="utf-8")) for lang in ("eng", "rus")}
    for key in sorted(keys):
        name = f"{PREFIX}CREATION.{key}.title"
        for lang, table in tables.items():
            if name not in table:
                errors.append(f"HeroCreation: нет {lang}/static_hover_tips.json → {name}")
        placeholders = [set(re.findall(r"\{([A-Za-z]\w*)\}", tables[lang].get(name, ""))) for lang in ("eng", "rus")]
        if placeholders[0] != placeholders[1]:
            errors.append(f"HeroCreation: несовместимые параметры RU/EN → {name}")
    return len(keys)


def check_journal_keys(errors: list) -> int:
    source = Path(CODE, "UI", "HeroJournal.cs").read_text(encoding="utf-8")
    keys = {"UI.journal." + key for key in re.findall(r'\bT\("([A-Za-z]+)"', source)}
    for a, b in re.findall(r'\bT\([^;\n]*?\?\s*"([A-Za-z]+)"\s*:\s*"([A-Za-z]+)"', source):
        keys.update(("UI.journal." + a, "UI.journal." + b))
    for family, path, enum in (("branch", "ProgressionRules.cs", "SkillBranch"), ("slot", "Equipment.cs", "EquipSlot"),
                               ("cast", "SpellAffinity.cs", "CastState")):
        keys.update("UI." + family + "." + name for name in enum_names(str(Path(CODE, "Core", path)), enum))
    tables = {lang: json.loads(Path(LOC, lang, "static_hover_tips.json").read_text()) for lang in ("eng", "rus")}
    for key in keys:
        name = PREFIX + key + ".title"
        for lang, table in tables.items():
            if name not in table: errors.append(f"Journal: нет {lang} → {name}")
        placeholders = [set(re.findall(r"\{([A-Za-z]\w*)\}", table.get(name, ""))) for table in tables.values()]
        if placeholders[0] != placeholders[1]: errors.append(f"Journal: RU/EN parameter mismatch → {name}")
    return len(keys)


def main() -> int:
    errors: list = []
    check_parity(errors)
    checked = check_models(errors)
    checked += check_moves(errors)
    checked += check_ui_keys(errors)
    checked += check_creation_keys(errors)
    checked += check_journal_keys(errors)
    for e in errors:
        print("✗", e)
    print(f"Моделей проверено: {checked}; ошибок: {len(errors)}")
    return 1 if errors else 0


if __name__ == "__main__":
    sys.exit(main())
