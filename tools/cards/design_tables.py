"""Таблицы карт в дизайн-документах 16/17/18/06 (card spec §3.1, §6): числа — из кода, смысл — руками.

В документе таблица стоит между маркерами `<!-- cards:Группа -->` и `<!-- /cards -->` (группа — как в каталоге:
«Латоносец», «Кличи», «Формы и Пути»…). Столбцы с числами (⚡ ◆ ✦, редкость, урон, блок, прочее) генерируются из
исходников через tools/cards/card_catalog.py. Столбцы «Архетип», «Роль» и «Почему так» пишет дизайнер — генератор их
сохраняет по имени класса. Новая карта появляется строкой с «—» (её надо заполнить: audit_cards.py ругается).

    python3 tools/cards/design_tables.py           # обновить таблицы в документах
    python3 tools/cards/design_tables.py --check   # CI: код 1, если числа в документе разошлись с кодом
"""
from __future__ import annotations

import argparse
import os
import re
import sys

sys.path.insert(0, os.path.dirname(__file__))
import card_catalog as cc  # noqa: E402

DESIGN = os.path.join(cc.ROOT, "docs", "design")
DOCS = ["16-platebearer-cards.md", "17-silent-cards.md", "18-regent-and-shared-cards.md", "06-paths-transformations.md"]
BLOCK = re.compile(r"(<!-- cards:(?P<group>[^>]+?) -->\n)(?P<body>.*?)(<!-- /cards -->)", re.S)

FULL = ["Класс", "Название", "⚡", "◆", "✦", "Тип", "Редкость", "Урон", "Блок", "Прочее", "Архетип", "Роль", "Почему так"]
SHORT = ["Класс", "Название", "Категория", "Текст"]
SHORT_GROUPS = {"Жетоны выбора", "Услуги лавки", "Камень Памяти", "Зачарование карт"}
MANUAL = {"Архетип", "Роль", "Почему так"}
# Служебные жетоны: категория по префиксу класса (обоснование — прозой по категориям в документе 18).
CATEGORY = [
    ("PickCardEnchant", "чары карты (07 §6.2)"), ("PickStat", "очко характеристики (02 §5)"),
    ("PickSchool", "школа магии (04, 08)"), ("PickDish", "блюдо (10)"), ("PickEnchant", "чары снаряжения (07 §6)"),
    ("PickStone", "камень душ (07 §6)"), ("PickMeditate", "медитация (02 §5)"), ("PickPray", "молитва (05)"), ("PickMentor", "таверна (14 §2.9)"), ("PickLesson", "таверна (14 §2.9)"),
    ("Node", "узел навыка (04, 02 §5)"), ("Counter", "услуга лавки (14)"), ("Order", "заказ (14)"),
    ("Scar", "Шрам (15)"), ("Death", "Камень Памяти (15)"),
]


def category(name: str) -> str:
    return next((c for prefix, c in CATEGORY if name.startswith(prefix)), EMPTY)
EMPTY = "—"


def cell(text) -> str:
    return str(text).replace("|", "\\|").replace("\n", " ").strip()


def parse_rows(body: str) -> dict:
    """Ручные столбцы существующей таблицы: {класс: {столбец: текст}}."""
    lines = [ln for ln in body.splitlines() if ln.startswith("|")]
    if len(lines) < 2:
        return {}
    header = [h.strip() for h in lines[0].strip("|").split("|")]
    out = {}
    for ln in lines[2:]:
        cells = [c.strip() for c in re.split(r"(?<!\\)\|", ln.strip().strip("|"))]
        row = dict(zip(header, cells))
        name = row.get("Класс", "").strip("`")
        if name:
            out[name] = {k: v for k, v in row.items() if k in MANUAL}
    return out


def render(group: str, cards: list, manual: dict) -> str:
    cols = SHORT if group in SHORT_GROUPS else FULL
    lines = ["| " + " | ".join(cols) + " |", "|" + "---|" * len(cols)]
    for r in cards:
        keep = manual.get(r["name"], {})
        values = {
            "Класс": f"`{r['name']}`", "Название": r["title"], "⚡": r["energy"], "◆": r["stamina"], "✦": r["mana"],
            "Тип": r["type"], "Редкость": r["rarity"], "Урон": r["damage"], "Блок": r["block"], "Прочее": r["other"],
            "Текст": r["desc"], "Категория": category(r["name"]),
        }
        for k in MANUAL:
            values[k] = keep.get(k) or EMPTY
        lines.append("| " + " | ".join(cell(values[c]) for c in cols) + " |")
    return "\n".join(lines) + "\n"


def process(check: bool) -> int:
    rows = cc.collect()
    stale = []
    for name in DOCS:
        path = os.path.join(DESIGN, name)
        if not os.path.exists(path):
            continue
        text = open(path, encoding="utf-8").read()

        def repl(m):
            group = m.group("group").strip()
            if group not in rows:
                stale.append(f"{name}: неизвестная группа «{group}»")
                return m.group(0)
            body = render(group, rows[group], parse_rows(m.group("body")))
            return m.group(1) + body + m.group(4)

        new = BLOCK.sub(repl, text)
        if new != text:
            stale.append(name)
            if not check:
                open(path, "w", encoding="utf-8").write(new)
    if check and stale:
        for s in stale:
            print(f"✗ {s}: таблица карт разошлась с кодом — python3 tools/cards/design_tables.py")
        return 1
    print("Таблицы карт: " + (", ".join(stale) + " — обновлены" if stale and not check else "актуальны"))
    return 0


def main() -> int:
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("--check", action="store_true")
    return process(ap.parse_args().check)


if __name__ == "__main__":
    sys.exit(main())
