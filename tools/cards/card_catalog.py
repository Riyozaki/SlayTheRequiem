"""Каталог карт мода из исходников: цена ⚡/◆/✦, тип, редкость, цель, урон, блок, статусы, улучшения и текст (RU).

Нужен для аудита переработки карт (docs/plan/card-rework-spec.md). В коде на него ссылаются документы
docs/design/16-platebearer-cards.md и 17-silent-cards.md — их ещё нет, а каталог служит для них основой.

Запуск:
    python3 tools/cards/card_catalog.py                     # markdown в stdout
    python3 tools/cards/card_catalog.py --out docs/plan/card-catalog.md

Разбор статический (регулярные выражения по конструкторам `RequiemCard`/`SpellCard`), поэтому это ориентир, а не
источник правды. Числа берутся из вызовов `WithDamage(base, upgrade)`, `WithBlock`, `WithStamina`, `WithSpellDamage`,
`WithPower<T>(base, upgrade)`, `WithVar("Name", base, upgrade)`, `WithCostUpgradeBy(n)`, `WithKeyword(...)`.
"""
from __future__ import annotations

import argparse
import collections
import glob
import json
import os
import re
import sys

ROOT = os.path.normpath(os.path.join(os.path.dirname(__file__), "..", ".."))
CODE = os.path.join(ROOT, "mod", "SlayTheRequiemCode")
LOC = os.path.join(ROOT, "mod", "SlayTheRequiem", "localization", "rus", "cards.json")
PREFIX = "SLAYTHEREQUIEM-"

CLASS = re.compile(r"(?P<attrs>(?:\[[^\]]+\]\s*)*)public\s+sealed\s+class\s+(?P<name>\w+)(?:\([^)]*\))?\s*:\s*(?P<base>\w+)")
ANY_TYPE = re.compile(r"^(?:\[[^\]]+\]\s*)*(?:public|internal|file)\s+(?:\w+\s+)*(?:class|record|struct|interface|enum)\s+\w+", re.M)
BASE_CALL = re.compile(r":\s*base\((?P<args>[^)]*)\)")
POOL = re.compile(r"\[Pool\(typeof\((?P<pool>\w+)\)\)\]")
WITH = re.compile(r"\bWith(?P<what>Damage|Block|Heal|Cards|Stamina|SpellDamage|CostUpgradeBy|Keyword|Var|Power<(?P<power>\w+)>)"
                  r"\((?P<args>[^;]*?)\)\s*(?=[;\.\n])")

GROUPS = [
    ("Латоносец", "Cards/Platebearer/"),
    ("Безмолвная", "Cards/Silent/"),
    ("Регент", "Cards/Regent/"),
    ("Кличи", "Cards/Shouts.cs"),
    ("Заклинания", "Spells/"),
    ("Формы и Пути", "Transformations/"),
    ("Зачарование карт", "Progression/CardEnchanting.cs"),
    ("Камень Памяти", "Rollback/"),
    ("Услуги лавки", "Shops/"),
    ("Жетоны выбора", "Progression/ChoiceCards.cs"),
]

SHORT = {"CardType.": "", "CardRarity.": "", "TargetType.": "", "SpellSchool.": "", "SpellTier.": ""}


def snake(name: str) -> str:
    return re.sub(r"(?<=[a-z0-9])(?=[A-Z])", "_", name).upper()


def short(s: str) -> str:
    for k, v in SHORT.items():
        s = s.replace(k, v)
    return s.strip()


def load_constants() -> dict:
    """Целочисленные константы мода (`const int X = 5;`) по имени и по `Класс.Имя` — чтобы в каталоге были числа."""
    consts = {}
    decl = re.compile(r"\bclass\s+(\w+)|const\s+(?:int|decimal)\s+(\w+)\s*=\s*(-?\d+)m?\s*;")
    for path in sorted(glob.glob(os.path.join(CODE, "**", "*.cs"), recursive=True)):
        cls = ""
        for m in decl.finditer(open(path, encoding="utf-8").read()):
            if m.group(1):
                cls = m.group(1)
            else:
                consts[f"{cls}.{m.group(2)}"] = m.group(3)
                consts.setdefault(m.group(2), m.group(3))
    return consts


CONSTS: dict = {}
CONST_REF = re.compile(r"\b(?:[A-Z]\w*\.)?[A-Z]\w+\b")


def resolve(arg: str) -> str:
    """`TransformationRules.SmiteDamage` → `8`; прочее без изменений."""
    if not CONSTS:
        CONSTS.update(load_constants())
    a = arg.strip()
    return CONSTS.get(a, a) if CONST_REF.fullmatch(a) else a


def parse_file(path: str):
    """Карточные классы файла: имя, база, аргументы конструктора, пул, числа With*, ключевые слова, интерфейсы и флаги."""
    src = re.sub(r"//[^\n]*", "", open(path, encoding="utf-8").read())
    matches = list(CLASS.finditer(src))
    # Тело карты кончается на следующем объявлении любого типа (в т. ч. abstract/partial/record), а не только карты.
    bounds = sorted(x.start() for x in ANY_TYPE.finditer(src))
    for i, m in enumerate(matches):
        end = next((b for b in bounds if b > m.end()), len(src))
        body = src[m.end(): end]
        header = src[m.start(): src.find("{", m.end()) if src.find("{", m.end()) >= 0 else m.end()]
        call = BASE_CALL.search(body)
        args = [resolve(short(a)) for a in call.group("args").split(",")] if call else []
        pool = POOL.search(m.group("attrs") or "")
        fx = collections.OrderedDict()
        keywords = []
        cost_up = None
        for w in WITH.finditer(body):
            what, a = w.group("what"), [resolve(x.strip()) for x in w.group("args").split(",")]
            if what == "Keyword":
                keywords.append(short(a[0]).replace("CardKeyword.", ""))
            elif what == "CostUpgradeBy":
                cost_up = a[0]
            elif what == "Var":
                fx[a[0].strip('"')] = a[1:] if len(a) > 1 else []
            elif w.group("power"):
                fx[w.group("power")] = a
            else:
                fx[what] = a
        star = re.search(r"CanonicalStarCost\s*=>\s*(\w+)", body)
        yield {
            "name": m.group("name"), "base": m.group("base"), "args": args,
            "pool": pool.group("pool") if pool else "", "fx": fx, "keywords": keywords, "cost_up": cost_up,
            "star": resolve(star.group(1)) if star else ("X" if re.search(r"HasStarCostX\s*=>\s*true", body) else ""),
            "interfaces": re.findall(r"\bI[A-Z]\w+", header),
            "no_upgrade": bool(re.search(r"MaxUpgradeLevel\s*=>\s*0\b", body)),
            "damage_type": (re.search(r"DamageType\s*=>\s*DamageType\.(\w+)", body) or [None, ""])[1],
            "weight": bool(re.search(r"\bCardWeight\b|\bWeight\s*=>", body)),
            "body": body,
        }


def fmt_val(a):
    if not a:
        return ""
    if len(a) >= 2 and a[1] not in ("0", ""):
        return f"{a[0]}(+{a[1]})" if not a[1].startswith("-") else f"{a[0]}({a[1]})"
    return a[0]


# Базовые классы с особым конструктором: (энергия, тип, цель), редкость фиксирована.
FIXED_RARITY = {"BeastCard": "Token"}


def no_upgrade_bases() -> set:
    """Абстрактные базы с `MaxUpgradeLevel => 0` (ищем в коде, чтобы не держать список руками)."""
    bases = set()
    for path in sorted(glob.glob(os.path.join(CODE, "**", "*.cs"), recursive=True)):
        src = open(path, encoding="utf-8").read()
        for m in re.finditer(r"abstract\s+class\s+(\w+)[^{]*\{", src):
            end = src.find("\n}", m.end())
            if re.search(r"MaxUpgradeLevel\s*=>\s*0\b", src[m.end(): end]):
                bases.add(m.group(1))
    return bases


def collect(loc_path: str = LOC):
    loc = json.load(open(loc_path, encoding="utf-8")) if os.path.exists(loc_path) else {}
    frozen = no_upgrade_bases()
    rows = collections.defaultdict(list)
    for path in sorted(glob.glob(os.path.join(CODE, "**", "*.cs"), recursive=True)):
        rel = os.path.relpath(path, CODE).replace(os.sep, "/")
        group = next((g for g, p in GROUPS if rel.startswith(p)), None)
        if group is None:
            continue
        for c in parse_file(path):
            name, base, args, fx, cost_up = c["name"], c["base"], c["args"], c["fx"], c["cost_up"]
            key = f"{PREFIX}{snake(name)}"
            title = loc.get(f"{key}.title")
            if title is None:
                continue  # не карта (силы, реликвии и т. п. в тех же файлах)
            desc = loc.get(f"{key}.description", "").replace("\n", " ")
            desc = re.sub(r"\[/?\w+\]", "", desc)
            summon = re.fullmatch(r"SummonSpell(?:<\w+>)?", base) is not None or base == "SummonSpell"
            if summon:
                energy, mana, tier, hp, hp_up = (args + [""] * 5)[:5]
                ctype, rarity, target = "Skill", f"Conjuration/{tier}", "Self"
                fx = collections.OrderedDict([("SummonHp", [hp, hp_up]), *fx.items()])
            elif base == "SpellCard" or (args and args[0].isdigit() and len(args) == 6):
                energy, mana, school, tier, ctype, target = (args + [""] * 6)[:6]
                rarity = f"{school}/{tier}"
            elif base in FIXED_RARITY:
                energy, ctype, target = (args + [""] * 3)[:3]
                rarity, mana = FIXED_RARITY[base], ""
            else:
                energy, ctype, rarity, target = (args + [""] * 4)[:4]
                mana = ""
            if c["star"]:
                mana = c["star"]
            if cost_up:
                energy = f"{energy}({cost_up})"
            stamina_raw = fx.pop("Stamina", [])
            damage_raw = fx.pop("Damage", fx.pop("SpellDamage", []))
            block_raw = fx.pop("Block", [])
            rows[group].append({
                "name": name, "title": title, "file": rel, "pool": c["pool"].replace("CardPool", ""),
                "energy": energy, "mana": mana, "stamina": fmt_val(stamina_raw),
                "type": ctype, "rarity": rarity, "target": target,
                "damage": fmt_val(damage_raw), "block": fmt_val(block_raw),
                "other": ", ".join(f"{k} {fmt_val(v)}".strip() for k, v in fx.items()),
                "keywords": ", ".join(c["keywords"]), "desc": desc,
                # для tools/cards/audit_cards.py (в каталог не выводятся)
                "_base": base, "_fx": {"Stamina": stamina_raw, "Damage": damage_raw, "Block": block_raw, **fx},
                "_cost_up": cost_up, "_star": c["star"], "_interfaces": c["interfaces"],
                "_no_upgrade": c["no_upgrade"] or base in frozen, "_damage_type": c["damage_type"],
                "_weight": c["weight"], "_body": c["body"], "_key": key, "_group": group,
            })
    return rows


def render(rows) -> str:
    out = ["# Каталог карт мода (сгенерировано)", "",
           "> Создаётся `python3 tools/cards/card_catalog.py --out docs/plan/card-catalog.md`. Не править руками.",
           "> Значения `N(+M)` — база и прибавка улучшения. ⚡ — энергия, ◆ — выносливость, ✦ — мана (звёзды).",
           "> Текст — русская локализация без разметки, переменные `{…}` оставлены как есть.", ""]
    total = sum(len(v) for v in rows.values())
    out.append(f"Всего карт: **{total}**. " + ", ".join(f"{g} — {len(rows[g])}" for g, _ in GROUPS if rows.get(g)))
    out.append("")
    for group, _ in GROUPS:
        items = rows.get(group)
        if not items:
            continue
        out += [f"## {group} ({len(items)})", ""]
        if group == "Жетоны выбора":
            out.append("| Класс | Название | Текст |")
            out.append("|---|---|---|")
            for r in items:
                out.append(f"| `{r['name']}` | {r['title']} | {r['desc']} |")
            out.append("")
            continue
        out.append("| Класс | Название | Пул | ⚡ | ◆ | ✦ | Тип | Редкость | Цель | Урон | Блок | Прочее | Ключевые | Текст |")
        out.append("|---|---|---|---|---|---|---|---|---|---|---|---|---|---|")
        for r in items:
            out.append("| `{name}` | {title} | {pool} | {energy} | {stamina} | {mana} | {type} | {rarity} | {target} | {damage} | "
                       "{block} | {other} | {keywords} | {desc} |".format(**{k: str(v).replace("|", "\\|") for k, v in r.items()
                                                                              if not k.startswith("_")}))
        out.append("")
    return "\n".join(out) + "\n"


DEFAULT_OUT = "docs/plan/card-catalog.md"


def main() -> int:
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("--out", help="записать markdown в файл")
    ap.add_argument("--check", action="store_true", help=f"CI: код 1, если {DEFAULT_OUT} устарел")
    args = ap.parse_args()
    text = render(collect())
    if args.check:
        path = os.path.join(ROOT, args.out or DEFAULT_OUT)
        if not os.path.exists(path) or open(path, encoding="utf-8").read() != text:
            print(f"✗ {args.out or DEFAULT_OUT} устарел: python3 tools/cards/card_catalog.py --out {args.out or DEFAULT_OUT}")
            return 1
        print(f"{args.out or DEFAULT_OUT}: актуален")
        return 0
    if args.out:
        open(args.out, "w", encoding="utf-8").write(text)
        print(f"{args.out}: {text.count(chr(10))} строк")
    else:
        sys.stdout.write(text)
    return 0


if __name__ == "__main__":
    sys.exit(main())
