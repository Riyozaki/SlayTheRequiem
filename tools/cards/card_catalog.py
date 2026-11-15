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
BASE_CALL = re.compile(r":\s*base\((?P<args>[^)]*)\)")
POOL = re.compile(r"\[Pool\(typeof\((?P<pool>\w+)\)\)\]")
WITH = re.compile(r"\bWith(?P<what>Damage|Block|Heal|Stamina|SpellDamage|CostUpgradeBy|Keyword|Var|Power<(?P<power>\w+)>)"
                  r"\((?P<args>[^;]*?)\)\s*(?=[;\.\n])")

GROUPS = [
    ("Латоносец", "Cards/Platebearer/"),
    ("Безмолвная", "Cards/Silent/"),
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


def parse_file(path: str):
    src = re.sub(r"//[^\n]*", "", open(path, encoding="utf-8").read())
    matches = list(CLASS.finditer(src))
    for i, m in enumerate(matches):
        body = src[m.end(): matches[i + 1].start() if i + 1 < len(matches) else len(src)]
        call = BASE_CALL.search(body)
        args = [short(a) for a in call.group("args").split(",")] if call else []
        pool = POOL.search(m.group("attrs") or "")
        fx = collections.OrderedDict()
        keywords = []
        cost_up = None
        for w in WITH.finditer(body):
            what, a = w.group("what"), [x.strip() for x in w.group("args").split(",")]
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
        yield m.group("name"), m.group("base"), args, (pool.group("pool") if pool else ""), fx, keywords, cost_up


def fmt_val(a):
    if not a:
        return ""
    if len(a) >= 2 and a[1] not in ("0", ""):
        return f"{a[0]}(+{a[1]})" if not a[1].startswith("-") else f"{a[0]}({a[1]})"
    return a[0]


def collect():
    loc = json.load(open(LOC, encoding="utf-8")) if os.path.exists(LOC) else {}
    rows = collections.defaultdict(list)
    for path in sorted(glob.glob(os.path.join(CODE, "**", "*.cs"), recursive=True)):
        rel = os.path.relpath(path, CODE).replace(os.sep, "/")
        group = next((g for g, p in GROUPS if rel.startswith(p)), None)
        if group is None:
            continue
        for name, base, args, pool, fx, keywords, cost_up in parse_file(path):
            key = f"{PREFIX}{snake(name)}"
            title = loc.get(f"{key}.title")
            if title is None:
                continue  # не карта (силы, реликвии и т. п. в тех же файлах)
            desc = loc.get(f"{key}.description", "").replace("\n", " ")
            desc = re.sub(r"\[/?\w+\]", "", desc)
            if base == "SummonSpell":
                energy, mana, tier, hp, hit = (args + [""] * 5)[:5]
                ctype, rarity, target = "Skill", f"Conjuration/{tier}", "Self"
                fx = collections.OrderedDict([("Summon", [f"{hp}♥, удар {hit}"]), *fx.items()])
            elif base == "SpellCard" or (args and args[0].isdigit() and len(args) == 6):
                energy, mana, school, tier, ctype, target = (args + [""] * 6)[:6]
                rarity = f"{school}/{tier}"
            else:
                energy, ctype, rarity, target = (args + [""] * 4)[:4]
                mana = ""
            if cost_up:
                energy = f"{energy}({cost_up})"
            rows[group].append({
                "name": name, "title": title, "file": rel, "pool": pool.replace("CardPool", ""),
                "energy": energy, "mana": mana, "stamina": fmt_val(fx.pop("Stamina", [])),
                "type": ctype, "rarity": rarity, "target": target,
                "damage": fmt_val(fx.pop("Damage", fx.pop("SpellDamage", []))), "block": fmt_val(fx.pop("Block", [])),
                "other": ", ".join(f"{k} {fmt_val(v)}".strip() for k, v in fx.items()),
                "keywords": ", ".join(keywords), "desc": desc,
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
                       "{block} | {other} | {keywords} | {desc} |".format(**{k: str(v).replace("|", "\\|") for k, v in r.items()}))
        out.append("")
    return "\n".join(out) + "\n"


def main() -> int:
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("--out", help="записать markdown в файл")
    args = ap.parse_args()
    text = render(collect())
    if args.out:
        open(args.out, "w", encoding="utf-8").write(text)
        print(f"{args.out}: {text.count(chr(10))} строк")
    else:
        sys.stdout.write(text)
    return 0


if __name__ == "__main__":
    sys.exit(main())
