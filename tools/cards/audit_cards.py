"""Аудит карт мода по чек-листу card spec §7 (веха M2): всё, что проверяется без игры.

Запуск:
    python3 tools/cards/audit_cards.py                         # отчёт в stdout
    python3 tools/cards/audit_cards.py --out docs/plan/card-audit.md
    python3 tools/cards/audit_cards.py --check                 # для CI: код 1, если есть ошибки (✗)

Ошибки (✗) — нарушения правил, которые записаны в дизайне и спецификации:
  1. id уникален среди карт мода;
  2. у атаки объявлена цена ◆ — кроме атак, которые ◆ не платят по правилу (`StaminaCosts.IsStaminaExempt`:
     заклинания, магические атаки с ✦ без ◆, Кличи) и решений из таблицы STAMINA_DECISIONS ниже; гибриды (◆ и ✦) — в сводке;
  3. у играбельной карты есть улучшение (число, цена или ключевое слово) — кроме классов с `MaxUpgradeLevel => 0`
     (жетоны-«меню», облики форм; решение записано в 06/18);
  4. каждое улучшаемое число видно в тексте как `{Var:diff()}` — на обоих языках;
  5. у карты есть название и описание RU и EN;
  6. карта героя лежит в пуле своего героя, служебные — вне пулов героев;
  7. у карты есть строка в дизайн-документе: 16 (Латоносец), 17 (Безмолвная), 18 (Кличи и служебные), 08 (заклинания),
     06 (Пути и формы) — по имени класса или по русскому названию.
Предупреждения (⚠) — ориентиры, а не правила: доля редкостей у героя (40/40/20 %), урон на энергию у обычных атак.
"""
from __future__ import annotations

import argparse
import collections
import glob
import json
import os
import re
import sys

sys.path.insert(0, os.path.dirname(__file__))
import card_catalog as cc  # noqa: E402

ROOT = cc.ROOT
LOC_DIR = os.path.join(ROOT, "mod", "SlayTheRequiem", "localization")
DESIGN = os.path.join(ROOT, "docs", "design")

# Решения по атакам без ◆ (card spec §3.4 п. 2), которых не покрывает общее правило IsStaminaExempt.
STAMINA_DECISIONS = {
    "Bite": "Кормление вампира: 0⚡ лёгкий укус (02: лёгкие атаки 0–1 ◆), цена — Жажда; 06, 18",
}

# Где искать строку карты в дизайне, по группе каталога.
DOCS = {
    "Латоносец": ["16-platebearer-cards.md"],
    "Безмолвная": ["17-silent-cards.md"],
    "Регент": ["18-regent-and-shared-cards.md"],
    "Кличи": ["18-regent-and-shared-cards.md", "04-heroes-origins.md"],
    "Контрмеры боёв": ["18-regent-and-shared-cards.md", "11-enemies.md"],
    "Заклинания": ["08-spells.md"],
    "Формы и Пути": ["06-paths-transformations.md", "18-regent-and-shared-cards.md"],
    "Зачарование карт": ["18-regent-and-shared-cards.md", "07-equipment-relics.md"],
    "Камень Памяти": ["18-regent-and-shared-cards.md", "15-death-rollback.md"],
    "Услуги лавки": ["18-regent-and-shared-cards.md", "14-shops.md"],
    "Жетоны выбора": ["18-regent-and-shared-cards.md"],
}

HERO_POOLS = {"Латоносец": "Ironclad", "Безмолвная": "Silent", "Регент": "Regent"}
SERVICE_GROUPS = {"Зачарование карт", "Камень Памяти", "Услуги лавки", "Жетоны выбора"}
VANILLA_HERO_POOLS = {"Ironclad", "Silent", "Regent", "Necrobinder", "Defect"}

# Имя переменной в тексте для чисел With*: WithDamage → {Damage}, WithPower<T> → {T}, WithVar("X") → {X}.
FIXED_VAR = {"Damage": "Damage", "Block": "Block", "Heal": "Heal", "SpellDamage": "Damage"}


def string_consts() -> dict:
    """`const string SneakVar = "Sneak";` → {"SneakVar": "Sneak", "SilentCard.SneakVar": "Sneak"}."""
    out = {}
    decl = re.compile(r"\bclass\s+(\w+)|const\s+string\s+(\w+)\s*=\s*\"([^\"]*)\"\s*;")
    for path in sorted(glob.glob(os.path.join(cc.CODE, "**", "*.cs"), recursive=True)):
        cls = ""
        for m in decl.finditer(open(path, encoding="utf-8").read()):
            if m.group(1):
                cls = m.group(1)
            else:
                out[f"{cls}.{m.group(2)}"] = m.group(3)
                out.setdefault(m.group(2), m.group(3))
    return out


def using_aliases() -> dict:
    """`using VanillaPoison = MegaCrit.Sts2.Core.Models.Powers.PoisonPower;` → {"VanillaPoison": "PoisonPower"}."""
    out = {}
    for path in sorted(glob.glob(os.path.join(cc.CODE, "**", "*.cs"), recursive=True)):
        for m in re.finditer(r"^using\s+(\w+)\s*=\s*[\w.]+\.(\w+)\s*;", open(path, encoding="utf-8").read(), re.M):
            out[m.group(1)] = m.group(2)
    return out


def fmt(values) -> str:
    return cc.fmt_val(values)


def upgrade_of(values) -> str:
    return values[1] if values and len(values) >= 2 and values[1] not in ("", "0") else ""


def text_var(key: str, consts: dict, aliases: dict) -> str:
    if key in FIXED_VAR:
        return FIXED_VAR[key]
    key = key.strip('"')
    key = re.sub(r"^(?:\w+\.)?(\w+)\.GainVar$", "StaminaGain", key) if key.endswith(".GainVar") else key
    return consts.get(key, aliases.get(key, key))


def design_text(names) -> str:
    return "\n".join(open(os.path.join(DESIGN, n), encoding="utf-8").read()
                     for n in names if os.path.exists(os.path.join(DESIGN, n)))


def audit():
    rows = cc.collect()
    eng_rows = cc.collect(os.path.join(LOC_DIR, "eng", "cards.json"))
    eng = {r["name"]: r for g in eng_rows.values() for r in g}
    eng_loc = json.load(open(os.path.join(LOC_DIR, "eng", "cards.json"), encoding="utf-8"))
    rus_loc = json.load(open(os.path.join(LOC_DIR, "rus", "cards.json"), encoding="utf-8"))
    consts, aliases = string_consts(), using_aliases()
    errors, warnings, notes = [], [], []
    seen = collections.Counter(r["name"] for g in rows.values() for r in g)

    for group, items in rows.items():
        docs = design_text(DOCS.get(group, []))
        for r in items:
            name, fx, ctype = r["name"], r["_fx"], r["type"]
            where = f"`{name}` ({r['title']})"
            playable = group not in SERVICE_GROUPS and not name.startswith("Pick")
            # 1. уникальность
            if seen[name] > 1:
                errors.append(f"{where}: имя класса повторяется ×{seen[name]}")
            # 2. ◆ у атак
            magic = r["_star"] not in ("", "0", "-1")
            exempt = (r["_base"] in ("SpellCard", "SummonSpell") or "IShoutCard" in r["_interfaces"]
                      or (magic and not fx["Stamina"]))
            if ctype == "Attack" and not fx["Stamina"] and not exempt:
                if name in STAMINA_DECISIONS:
                    notes.append(f"{where}: без ◆ — {STAMINA_DECISIONS[name]}")
                else:
                    errors.append(f"{where}: атака без цены ◆ (WithStamina) и без правила/решения")
            if ctype == "Attack" and fx["Stamina"] and exempt:
                errors.append(f"{where}: объявлена ◆, но карта по правилу её не платит (заклинание/Клич)")
            if ctype == "Attack" and fx["Stamina"] and magic:
                notes.append(f"{where}: гибрид — платит и ◆ ({fmt(fx['Stamina'])}), и ✦ ({r['_star']}): удар оружием + чары")
            # 3. улучшение
            upgrades = {k: upgrade_of(v) for k, v in fx.items() if upgrade_of(v)}
            has_upgrade = bool(upgrades) or bool(r["_cost_up"]) or "UpgradeKeyword" in r["_body"] \
                or re.search(r"Upgrade\w*\(", r["_body"]) is not None
            if playable and not r["_no_upgrade"] and not has_upgrade:
                errors.append(f"{where}: улучшение ничего не меняет — нужен «+» или MaxUpgradeLevel => 0 с решением")
            # 4. :diff() в тексте
            for lang, loc in (("rus", rus_loc), ("eng", eng_loc)):
                desc = loc.get(f"{r['_key']}.description", "")
                for k in upgrades:
                    if k == "Stamina":
                        continue  # цена ◆ видна на значке карты (M1), в тексте не обязательна
                    var = text_var(k, consts, aliases)
                    if not re.search(r"\{" + re.escape(var) + r":diff\(\)\}", desc) and f"{{{var}" not in desc:
                        warnings.append(f"{where} [{lang}]: улучшаемое «{var}» не найдено в тексте")
                    elif not re.search(r"\{" + re.escape(var) + r":(?:diff|inverseDiff)\(\)\}", desc):
                        errors.append(f"{where} [{lang}]: «{{{var}}}» улучшается, но выводится без :diff()")
            # 5. RU и EN
            for lang, loc in (("rus", rus_loc), ("eng", eng_loc)):
                for field in ("title", "description"):
                    if not loc.get(f"{r['_key']}.{field}"):
                        errors.append(f"{where}: нет {lang} {field}")
            # 6. пул
            pool = r["pool"]
            if group in HERO_POOLS and pool != HERO_POOLS[group]:
                errors.append(f"{where}: карта героя в пуле {pool or '—'} вместо {HERO_POOLS[group]}")
            if group in SERVICE_GROUPS and pool in VANILLA_HERO_POOLS:
                errors.append(f"{where}: служебная карта в пуле героя {pool}")
            # 7. дизайн-документ: строка таблицы (design_tables.py) или упоминание; у карт боя — заполненное «Почему так»
            row = re.search(r"^\| `" + re.escape(name) + r"` \|.*$", docs, re.M)
            if docs and not row and r["title"] not in docs:
                errors.append(f"{where}: нет строки в {', '.join(DOCS[group])}")
            elif not docs:
                errors.append(f"{where}: нет дизайн-документа {', '.join(DOCS.get(group, ['?']))}")
            elif row and group not in SERVICE_GROUPS and not name.startswith("Pick") \
                    and row.group(0).rstrip().endswith("| — |"):
                errors.append(f"{where}: в дизайн-документе не заполнено «Почему так»")

    # · тёзки ванильных карт: id мода разные (префикс), но правила, которые ищут разметку по имени класса, обязаны
    #   сначала проверять IRequiemCard (StaminaCosts, EquipmentCombatRules.IsWeaponAttack).
    vanilla_path = os.path.join(ROOT, "tools", "art", "vanilla_models.txt")
    if os.path.exists(vanilla_path):
        vanilla = {ln.split()[-1] for ln in open(vanilla_path, encoding="utf-8") if ln.startswith("card ")}
        twins = sorted(r["name"] for g in rows.values() for r in g if r["name"] in vanilla)
        if twins:
            notes.append("тёзки ванильных карт (разметка CSV к ним не применяется): " + ", ".join(f"`{t}`" for t in twins))

    # ⚠ редкость у героев (card spec §3.5: 40/40/20 %)
    for group in HERO_POOLS:
        items = rows.get(group, [])
        count = collections.Counter(r["rarity"] for r in items)
        total = sum(count[k] for k in ("Common", "Uncommon", "Rare")) or 1
        share = {k: round(100 * count[k] / total) for k in ("Common", "Uncommon", "Rare")}
        line = f"{group}: Common {share['Common']} %, Uncommon {share['Uncommon']} %, Rare {share['Rare']} % ({total} карт)"
        (warnings if abs(share["Common"] - 40) > 15 or abs(share["Rare"] - 20) > 10 else notes).append(f"редкость — {line}")

    # ⚠ кривая силы: обычные одиночные атаки героев, урон на энергию (1⚡ ≈ 6–7)
    for group in HERO_POOLS:
        for r in rows.get(group, []):
            if r["type"] != "Attack" or r["rarity"] != "Common" or r["target"] != "AnyEnemy":
                continue
            try:
                dmg, energy = int(r["_fx"]["Damage"][0]), int(str(r["energy"]).split("(")[0])
            except (ValueError, IndexError, TypeError):
                continue
            per = dmg / energy if energy else 0
            extra = r["other"]
            if energy > 0 and not 4 <= per <= 14:
                warnings.append(f"кривая — `{r['name']}`: {dmg} урона за {energy}⚡ (+ {extra or 'ничего'})")
    return rows, errors, warnings, notes


def render(rows, errors, warnings, notes) -> str:
    total = sum(len(v) for v in rows.values())
    out = ["# Аудит карт (сгенерировано)", "",
           "> `python3 tools/cards/audit_cards.py --out docs/plan/card-audit.md`. Не править руками.",
           "> Правила — card spec §7 и шапка `tools/cards/audit_cards.py`. ✗ ломает CI, ⚠ — повод посмотреть.", "",
           f"Карт: **{total}**. Ошибок: **{len(errors)}**. Предупреждений: **{len(warnings)}**.", ""]
    for title, items, mark in (("Ошибки", errors, "✗"), ("Предупреждения", warnings, "⚠"), ("Решения и сводка", notes, "·")):
        out += [f"## {title} ({len(items)})", ""]
        out += [f"- {mark} {line}" for line in items] or ["- нет"]
        out.append("")
    return "\n".join(out)


def main() -> int:
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("--out", help="записать отчёт в файл")
    ap.add_argument("--check", action="store_true", help="код 1 при ошибках; отчёт в --out должен совпадать")
    args = ap.parse_args()
    rows, errors, warnings, notes = audit()
    text = render(rows, errors, warnings, notes)
    if args.out and args.check:
        current = open(args.out, encoding="utf-8").read() if os.path.exists(args.out) else ""
        if current != text:
            print(f"✗ {args.out} устарел: python3 tools/cards/audit_cards.py --out {args.out}")
            return 1
    elif args.out:
        open(args.out, "w", encoding="utf-8").write(text)
    if not args.out or args.check:
        for line in errors:
            print("✗", line)
    print(f"Карт: {sum(len(v) for v in rows.values())}; ошибок: {len(errors)}; предупреждений: {len(warnings)}")
    return 1 if args.check and errors else 0


if __name__ == "__main__":
    sys.exit(main())
