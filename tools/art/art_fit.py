#!/usr/bin/env python3
"""Проверка «сбивающего» ванильного арта у карт мода (completion plan M3 п. 3, art spec §0) — то, что видно без игры.

Правила для карт боя (служебные жетоны-таблички — вне проверки, у них один портрет на категорию):

1. **Тип.** Портрет берётся у ванильной карты того же типа: атака — с атаки, навык — с навыка, сила — с силы. Рамка
   у типов разная, а картинка рисовалась под свою рамку и своё действие (удар, приём, постоянный эффект).
   Исключение — с причиной в столбце note, начинающейся с «тип:» (например, имя ванильной карты буквально то же).
2. **Дубль.** Портрет не берётся у ванильной карты, которую герой Реквиема сам видит в забеге: пулы Ironclad, Silent,
   Regent и бесцветных, кроме изъятых (docs/design/19 §2), и карты, открытые трансформациями (19 §4). Иначе игрок видит
   две разные карты с одной картинкой. Исключение — note с «дубль:».
3. ⚠ Одинаковый портрет у двух карт боя мода — предупреждение (лучше разные, но не ошибка).

    python3 tools/art/art_fit.py          # отчёт
    python3 tools/art/art_fit.py --check  # CI: код 1 при нарушениях 1–2
"""
from __future__ import annotations

import argparse
import collections
import fnmatch
import os
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.normpath(os.path.join(HERE, "..", ".."))
sys.path.insert(0, os.path.join(ROOT, "tools", "cards"))
import card_catalog as cc  # noqa: E402

RULES = os.path.join(HERE, "vanilla_art.csv")
TYPES = os.path.join(HERE, "vanilla_card_types.txt")
POOLS = os.path.join(ROOT, "mod", "SlayTheRequiem", "data", "requiem_pools.csv")

VISIBLE_POOLS = {"ironclad", "silent", "regent", "colorless"}
SERVICE_GROUPS = {"Зачарование карт", "Камень Памяти", "Услуги лавки", "Жетоны выбора"}
CHECKED_TYPES = {"Attack", "Skill", "Power"}


def read_rules():
    exact, patterns = {}, []
    for line in open(RULES, encoding="utf-8"):
        if line.startswith("#") or not line.startswith("card,"):
            continue
        parts = line.rstrip("\n").split(",", 3)
        mod, vanilla, note = parts[1], parts[2], parts[3] if len(parts) > 3 else ""
        (patterns.append((mod, vanilla, note)) if any(ch in mod for ch in "*?") else exact.__setitem__(mod, (vanilla, note)))
    return exact, patterns


def rule_for(name, exact, patterns):
    if name in exact:
        return exact[name]
    return next(((v, n) for p, v, n in patterns if fnmatch.fnmatchcase(name, p)), (None, ""))


def vanilla_types() -> dict:
    out = {}
    for line in open(TYPES, encoding="utf-8"):
        if not line.startswith("#") and line.strip():
            name, ctype = line.split()
            out[name] = ctype
    return out


def requiem_pools():
    cut, gated = set(), set()
    for line in open(POOLS, encoding="utf-8"):
        parts = line.strip().split(",")
        if len(parts) >= 3 and parts[2] == "cut":
            cut.add((parts[0].lower(), parts[1]))
        elif len(parts) >= 3 and parts[2] == "gated":
            gated.add((parts[0].lower(), parts[1]))
    return cut, gated


def audit():
    exact, patterns = read_rules()
    vtypes = vanilla_types()
    cut, gated = requiem_pools()
    errors, warnings, allowed = [], [], []
    used = collections.defaultdict(list)
    for group, rows in cc.collect().items():
        if group in SERVICE_GROUPS:
            continue
        for r in rows:
            name, ctype = r["name"], r["type"]
            if name.startswith(("Pick", "Node", "Counter", "Order")) or ctype not in CHECKED_TYPES:
                continue
            vanilla, note = rule_for(name, exact, patterns)
            if not vanilla:
                continue
            pool, key = vanilla.split("/", 1)
            used[vanilla].append(name)
            vt = vtypes.get(key)
            if vt and vt != ctype:
                if note.startswith("тип:"):
                    allowed.append(f"`{name}` ({ctype}) ← `{vanilla}` ({vt}): {note[4:].strip()}")
                else:
                    errors.append(f"`{name}` ({ctype}) ← `{vanilla}` ({vt}): тип портрета не совпадает")
            visible = (pool in VISIBLE_POOLS and (pool, key) not in cut) or (pool, key) in gated
            if visible:
                if note.startswith("дубль:"):
                    allowed.append(f"`{name}` ← `{vanilla}`: {note[6:].strip()}")
                else:
                    errors.append(f"`{name}` ← `{vanilla}`: ванильная карта есть в забеге Реквиема — две карты с одной картинкой")
    for vanilla, names in sorted(used.items()):
        if len(names) > 1:
            warnings.append(f"`{vanilla}` у {len(names)} карт мода: {', '.join(names)}")
    return errors, warnings, allowed


def main() -> int:
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("--check", action="store_true")
    args = ap.parse_args()
    errors, warnings, allowed = audit()
    for e in errors:
        print("✗", e)
    if not args.check:
        for w in warnings:
            print("⚠", w)
        for a in allowed:
            print("· исключение:", a)
    print(f"Арт карт: нарушений {len(errors)}, повторов внутри мода {len(warnings)}, исключений с причиной {len(allowed)}")
    return 1 if errors and args.check else 0


if __name__ == "__main__":
    sys.exit(main())
