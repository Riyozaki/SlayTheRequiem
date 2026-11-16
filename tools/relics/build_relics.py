#!/usr/bin/env python3
"""Переработка ванильных реликвий (docs/design/07 §7): таблица mod/SlayTheRequiem/data/vanilla_relics.csv.

Таблица — источник правды, её правят руками: одна строка на каждую реликвию игры
(снимок tools/relics/vanilla_relics_snapshot.txt, его пишет tools/art/snapshot_vanilla.py). Колонки:

  key          класс ванильной реликвии
  pool, rarity пул(ы) и редкость из снимка — для сверки
  bucket       keep — остаётся реликвией (рюкзак); jewelry — уходит в бижутерию (не выпадает героям Реквиема,
               вместо неё в пуле кольцо/амулет мода из replacement); rework — перерабатывается (change)
  weight       вес в Грузе 0–3 (02 §3)
  change       правка переработки: regen-1 — +1 энергия остаётся, прирост ◆ (у Регента ✦) −1
  replacement  класс бижутерии мода (только для jewelry)
  note         почему так (обязательно, если решение не по правилу по умолчанию)

Правила по умолчанию (подсказка для новых строк): стартовые и разовые («только при получении») — вес 0; обычные,
необычные, лавочные и событийные — 1; редкие и древние — 2; меняющие макс. энергию — rework/regen-1 (07 §7).

    python3 tools/relics/build_relics.py              # упорядочить таблицу и обновить сводку в 07 §7
    python3 tools/relics/build_relics.py --add-missing # дописать новые реликвии игры с весом по умолчанию
    python3 tools/relics/build_relics.py --check      # CI: код 1 — нет строки, ошибка в строке или сводка устарела
"""
from __future__ import annotations

import argparse
import csv
import glob
import io
import os
import re
import sys

ROOT = os.path.normpath(os.path.join(os.path.dirname(__file__), "..", ".."))
SNAPSHOT = os.path.join(ROOT, "tools", "relics", "vanilla_relics_snapshot.txt")
CSV_PATH = os.path.join(ROOT, "mod", "SlayTheRequiem", "data", "vanilla_relics.csv")
DOC = glob.glob(os.path.join(ROOT, "docs", "design", "07-*.md"))[0]
CODE = os.path.join(ROOT, "mod", "SlayTheRequiemCode")
COLUMNS = ["key", "pool", "rarity", "bucket", "weight", "change", "replacement", "note"]
BUCKETS = {"keep", "jewelry", "rework"}
CHANGES = {"", "regen-1"}
NEW_MARK = "НОВАЯ — проверить"
BEGIN, END = "<!-- relics:summary -->", "<!-- /relics:summary -->"
HEADER = ("# Переработка ванильных реликвий (docs/design/07 §7). Правится руками; проверка и сводка — "
          "tools/relics/build_relics.py.\n")


def read_snapshot() -> dict[str, dict]:
    out = {}
    for line in open(SNAPSHOT, encoding="utf-8"):
        if line.startswith("#") or not line.strip():
            continue
        name, pool, rarity, flags = line.split()
        out[name] = {"pool": pool, "rarity": rarity, "flags": set() if flags == "-" else set(flags.split(","))}
    return out


def default_row(name: str, snap: dict) -> dict:
    flags, rarity = snap["flags"], snap["rarity"]
    row = {"key": name, "pool": snap["pool"], "rarity": rarity, "bucket": "keep", "change": "", "replacement": "", "note": ""}
    if "energy" in flags:
        row.update(bucket="rework", change="regen-1", weight="2", note="энергия: +1 энергия, прирост ◆/✦ −1 (07 §7)")
    elif rarity == "Starter":
        row.update(weight="0", note="стартовая")
    elif "once" in flags:
        row.update(weight="0", note="разовая: действует при получении")
    elif rarity in ("Rare", "Ancient"):
        row["weight"] = "2"
    else:
        row["weight"] = "1"
    return row


def read_csv() -> list[dict]:
    if not os.path.exists(CSV_PATH):
        return []
    lines = [l for l in open(CSV_PATH, encoding="utf-8") if not l.startswith("#")]
    return list(csv.DictReader(lines))


def write_csv(rows: list[dict]) -> str:
    buf = io.StringIO()
    buf.write(HEADER)
    w = csv.DictWriter(buf, fieldnames=COLUMNS, lineterminator="\n")
    w.writeheader()
    for r in sorted(rows, key=lambda r: r["key"]):
        w.writerow({c: r.get(c, "") for c in COLUMNS})
    return buf.getvalue()


def mod_classes() -> set[str]:
    names = set()
    for path in glob.glob(os.path.join(CODE, "**", "*.cs"), recursive=True):
        names |= set(re.findall(r"\bclass (\w+)", open(path, encoding="utf-8-sig").read()))
    return names


def validate(rows: list[dict], snap: dict[str, dict]) -> list[str]:
    errors = []
    seen = set()
    classes = mod_classes()
    for r in rows:
        k = r["key"]
        if k in seen:
            errors.append(f"{k}: строка повторяется")
        seen.add(k)
        if k not in snap:
            errors.append(f"{k}: такой реликвии нет в снимке игры")
            continue
        s = snap[k]
        if r["pool"] != s["pool"] or r["rarity"] != s["rarity"]:
            errors.append(f"{k}: пул/редкость {r['pool']}/{r['rarity']} ≠ снимок {s['pool']}/{s['rarity']}")
        if r["bucket"] not in BUCKETS:
            errors.append(f"{k}: неизвестная корзина «{r['bucket']}»")
        if r["change"] not in CHANGES:
            errors.append(f"{k}: неизвестная правка «{r['change']}»")
        if not r["weight"].isdigit() or not 0 <= int(r["weight"]) <= 3:
            errors.append(f"{k}: вес «{r['weight']}» вне 0–3")
        if (r["bucket"] == "rework") != bool(r["change"]):
            errors.append(f"{k}: правка нужна ровно у корзины rework")
        if r["bucket"] == "jewelry":
            if not r["replacement"]:
                errors.append(f"{k}: у jewelry нет replacement")
            elif r["replacement"] not in classes:
                errors.append(f"{k}: класса бижутерии {r['replacement']} нет в коде мода")
        elif r["replacement"]:
            errors.append(f"{k}: replacement только у jewelry")
        if "energy" in s["flags"] and r["change"] != "regen-1":
            errors.append(f"{k}: меняет макс. энергию — по 07 §7 должна быть rework/regen-1")
        if NEW_MARK in r["note"]:
            errors.append(f"{k}: новая реликвия не разобрана ({NEW_MARK})")
        if r["bucket"] != "keep" and not r["note"]:
            errors.append(f"{k}: решение {r['bucket']} без причины (note)")
    for k in sorted(set(snap) - seen):
        errors.append(f"{k}: реликвии игры нет в таблице (python3 tools/relics/build_relics.py --add-missing)")
    return errors


def summary(rows: list[dict]) -> str:
    by = {b: [r for r in rows if r["bucket"] == b] for b in ("keep", "jewelry", "rework")}
    weights = {w: sum(1 for r in by["keep"] if r["weight"] == str(w)) for w in range(4)}
    out = [BEGIN, "",
           f"Таблица `data/vanilla_relics.csv` покрывает **{len(rows)}** ванильных реликвий игры (сводка генерируется "
           "`tools/relics/build_relics.py`, CI сверяет):", "",
           "| Корзина | Реликвий |", "|---|---|",
           f"| Остаются (рюкзак) | {len(by['keep'])} |",
           f"| В бижутерию | {len(by['jewelry'])} |",
           f"| Перерабатываются | {len(by['rework'])} |", "",
           "Вес оставшихся: " + ", ".join(f"{w} — {n}" for w, n in weights.items()) + ".", "",
           "| В бижутерию | Чем заменена | Почему |", "|---|---|---|"]
    out += [f"| {r['key']} | `{r['replacement']}` | {r['note']} |" for r in sorted(by["jewelry"], key=lambda r: r["key"])]
    out += ["", "| Перерабатываются | Правка | Почему |", "|---|---|---|"]
    out += [f"| {r['key']} | {r['change']} | {r['note']} |" for r in sorted(by["rework"], key=lambda r: r["key"])]
    notes = [r for r in by["keep"] if r["note"] and r["note"] not in ("стартовая", "разовая: действует при получении")]
    if notes:
        out += ["", "| Остаются, особые решения | Вес | Почему |", "|---|---|---|"]
        out += [f"| {r['key']} | {r['weight']} | {r['note']} |" for r in sorted(notes, key=lambda r: r["key"])]
    out += ["", END]
    return "\n".join(out)


def doc_with(text: str, block: str) -> str:
    if BEGIN in text:
        return re.sub(re.escape(BEGIN) + r".*?" + re.escape(END), lambda _: block, text, flags=re.S)
    anchor = text.index("## 8.")
    return text[:anchor] + block + "\n\n" + text[anchor:]


def main() -> int:
    ap = argparse.ArgumentParser()
    ap.add_argument("--check", action="store_true")
    ap.add_argument("--add-missing", action="store_true")
    args = ap.parse_args()
    snap = read_snapshot()
    rows = read_csv()
    if args.add_missing:
        have = {r["key"] for r in rows}
        for k in sorted(set(snap) - have):
            row = default_row(k, snap[k])
            row["note"] = (row["note"] + "; " if row["note"] else "") + NEW_MARK
            rows.append(row)
    errors = validate(rows, snap)
    doc = open(DOC, encoding="utf-8").read()
    new_csv, new_doc = write_csv(rows), doc_with(doc, summary(rows))
    if args.check:
        if open(CSV_PATH, encoding="utf-8").read() != new_csv:
            errors.append("vanilla_relics.csv не упорядочена (python3 tools/relics/build_relics.py)")
        if doc != new_doc:
            errors.append("сводка в docs/design/07 §7 устарела (python3 tools/relics/build_relics.py)")
    else:
        open(CSV_PATH, "w", encoding="utf-8", newline="\n").write(new_csv)
        open(DOC, "w", encoding="utf-8", newline="\n").write(new_doc)
    for e in errors:
        print("ОШИБКА:", e)
    by = {b: sum(1 for r in rows if r["bucket"] == b) for b in BUCKETS}
    print(f"Реликвий: {len(rows)} (остаются {by['keep']}, в бижутерию {by['jewelry']}, перерабатываются {by['rework']}); "
          f"ошибок: {len(errors)}")
    return 1 if errors else 0


if __name__ == "__main__":
    sys.exit(main())
