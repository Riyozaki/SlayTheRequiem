#!/usr/bin/env python3
"""Где лежит текст: поиск по скриптам локализации tools/loc (ревью F-21, R-30).

Тексты разложены по 25 скриптам этапов (loc_stage3.py, loc_m5shops.py, …), а не по системам, поэтому текст одной
системы бывает в 3–4 файлах. Пока скрипты не сведены по доменам (после первой сборки), этот поиск отвечает на вопрос
«в каком файле править»:

    python3 tools/loc/find_loc.py RogueBackground        # по имени класса (подстрока, без регистра)
    python3 tools/loc/find_loc.py ALLOW_THEFT            # по ключу
    python3 tools/loc/find_loc.py --dups                 # одна модель описана в нескольких местах (последняя побеждает)

Смотрит списки кортежей вида (класс, …) и словари {"eng": {...}, "rus": {...}} в build_loc.py и loc_*.py.
"""
import importlib
import os
import re
import sys
from collections import defaultdict

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)


def snake(name: str) -> str:
    return re.sub(r"(?<=[a-z0-9])(?=[A-Z])", "_", name).upper()


def entries():
    """(файл, имя константы, id модели или ключ) для всех текстов."""
    # Сначала скрипты этапов, потом build_loc; один и тот же объект (импорт, склейка списков) считается один раз.
    names = sorted(f[:-3] for f in os.listdir(HERE) if f.startswith("loc_") and f.endswith(".py")) + ["build_loc"]
    seen = set()
    for mod_name in names:
        module = importlib.import_module(mod_name)
        for attr, value in vars(module).items():
            if not attr.isupper() or attr.startswith("_"):
                continue
            if isinstance(value, (list, tuple)):
                for item in value:
                    if id(item) in seen:
                        continue
                    if isinstance(item, (list, tuple)) and item and isinstance(item[0], str) and re.fullmatch(r"[A-Z]\w*", item[0]):
                        seen.add(id(item))
                        yield mod_name + ".py", attr, snake(item[0])
            elif isinstance(value, dict):
                if id(value) in seen:
                    continue
                seen.add(id(value))
                langs = [v for k, v in value.items() if k in ("eng", "rus") and isinstance(v, dict)]
                for lang in langs[:1]:
                    for key in lang:
                        yield mod_name + ".py", attr, key.split(".")[0]


def main(argv):
    if not argv:
        print(__doc__)
        return 0
    found = list(entries())
    if argv[0] == "--dups":
        where = defaultdict(set)
        for file, attr, key in found:
            where[key].add(f"{file}:{attr}")
        dups = {k: v for k, v in where.items() if len(v) > 1}
        for key in sorted(dups):
            print(f"{key}: {', '.join(sorted(dups[key]))}")
        print(f"Моделей/ключей в нескольких местах: {len(dups)}")
        return 0
    query = argv[0]
    needle = snake(query) if re.search(r"[a-z]", query) else query.upper()
    hits = sorted({(f, a, k) for f, a, k in found if needle in k})
    for file, attr, key in hits:
        print(f"{key:48} {file}:{attr}")
    if not hits:
        print("Не найдено (ключи могут собираться в build_loc.py по шаблону — ищите grep по тексту).")
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
