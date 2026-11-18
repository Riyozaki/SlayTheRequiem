#!/usr/bin/env python3
"""B2.4: source-checked build/access matrix, not a reward or battle simulator.

Recipes are authored; costs/rarities/pools are resolved from the same catalog as CI.
Fail on inaccessible/cut cards, wrong-hero pools, unknown skills/gear/identity, or lost roles.
Run with --check in CI; without it regenerate the review matrix.
"""
from __future__ import annotations

import argparse
import collections
import csv
import json
from pathlib import Path
import re
import sys

ROOT = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(ROOT / "tools/cards"))
import card_catalog as cc  # noqa: E402

CODE = ROOT / "mod/SlayTheRequiemCode"
RECIPES = Path(__file__).with_name("build_directions.json")
OUT = ROOT / "docs/review/stage-two-build-matrix-2026-11-18.md"
ROLES = ("enablers", "payoffs", "defense", "draw", "resources", "answers", "optional_rares")
LABELS = ("Связки", "Отдача", "Защита", "Добор", "Ресурсы", "Контрмеры", "Необязательные редкие")


def inputs():
    catalog = {r["name"]: r for rows in cc.collect().values() for r in rows}
    vanilla = {line.split()[2] for line in (ROOT / "tools/art/vanilla_models.txt").read_text().splitlines()
               if line.startswith("card ")}
    with (ROOT / "mod/SlayTheRequiem/data/requiem_pools.csv").open() as f:
        pool = {(r["pool"], r["key"]): r for r in csv.DictReader(line for line in f if not line.startswith("#"))}
    sources = "\n".join(p.read_text() for p in sorted(CODE.rglob("*.cs")))
    backgrounds = (CODE / "Relics/Backgrounds.cs").read_text()
    identity = (CODE / "Core/HeroIdentityRules.cs").read_text()
    return catalog, vanilla, pool, sources, backgrounds, identity


def validate(data, deps=None):
    catalog, vanilla, pool, sources, backgrounds, identity = deps or inputs()
    errors = []
    builds = data.get("directions", [])
    counts = collections.Counter(b["hero"] for b in builds)
    if counts != {"Ironclad": 3, "Silent": 3, "Regent": 3}:
        errors.append(f"Need exactly three distinct recipes per release hero, got {dict(counts)}")
    if len({b["id"] for b in builds}) != len(builds):
        errors.append("Duplicate direction id")
    for b in builds:
        prefix = b["id"] + ": "
        origins = re.search(r'"' + re.escape(b["hero"]) + r'"\s*=>\s*\[([^]]+)\]', identity)
        if not origins or '"' + b["origin"] + '"' not in origins[1]:
            errors.append(prefix + "origin is not offered to this hero")
        options = re.search(r'\b' + re.escape(b["origin"]) + r'\s*=>\s*\[([^]]+)\]', backgrounds)
        if not options or f'typeof({b["background"]})' not in options[1]:
            errors.append(prefix + "background is not offered by this origin")
        if not re.search(r'\b' + re.escape(b["path"]) + r'\b', (CODE / "Core/TransformationRules.cs").read_text()):
            errors.append(prefix + "unknown Path")
        for role in ROLES:
            refs = b.get(role, [])
            if role != "optional_rares" and not refs:
                errors.append(prefix + "missing role " + role)
            for ref in refs:
                if ref.startswith("mod:"):
                    card = catalog.get(ref[4:])
                    if card is None:
                        errors.append(prefix + "unknown card " + ref)
                    elif card["pool"] not in (b["hero"], "Spell", "Path"):
                        errors.append(prefix + "inaccessible hero pool " + ref + " (" + card["pool"] + ")")
                    elif card["rarity"] in ("Event", "Token", "Ancient", "Basic"):
                        errors.append(prefix + "recipe relies on a non-reward card " + ref)
                elif ref.startswith("vanilla:") and "/" in ref:
                    hero, name = ref[8:].split("/", 1)
                    if name not in vanilla or hero not in (b["hero"], "Colorless"):
                        errors.append(prefix + "unknown or wrong-hero native card " + ref)
                    if pool.get((hero, name), {}).get("decision") in ("cut", "gated"):
                        errors.append(prefix + "cut/gated native card " + ref)
                else:
                    errors.append(prefix + "invalid card reference " + ref)
        enablers = [catalog[r[4:]] for r in b["enablers"] if r.startswith("mod:") and r[4:] in catalog]
        if not any(c["rarity"] in ("Common", "Uncommon") or c["rarity"].endswith(("/Novice", "/Apprentice")) for c in enablers):
            errors.append(prefix + "no accessible common/uncommon enabler")
        for gear in b["gear"]:
            if not re.search(r'\bclass\s+' + re.escape(gear) + r'\b', sources):
                errors.append(prefix + "unknown gear " + gear)
        skills = (CODE / "Core/ProgressionRules.cs").read_text().split("public enum Skill\n", 1)[-1].split("public sealed record SkillNode", 1)[0]
        for skill in b["skills"]:
            if not re.search(r'\b' + re.escape(skill) + r'\b', skills):
                errors.append(prefix + "unknown skill " + skill)
    return errors


def describe(ref, catalog):
    if ref.startswith("vanilla:"):
        return "`" + ref[8:] + "` (сохранённый ванильный пул)"
    c = catalog[ref[4:]]
    cost = "/".join(str(c[k] or 0) for k in ("energy", "stamina", "mana"))
    gate = ""
    if c["pool"] == "Spell" and "/" in c["rarity"]:
        school, tier = c["rarity"].split("/", 1)
        rank = {"Novice": 1, "Apprentice": 2, "Adept": 3, "Expert": 4, "Master": 5}.get(tier, 5)
        gate = f"; награды: {school} ≥{max(1, rank - 1)}"
    return f"`{c['name']}` — {c['pool']}, {c['rarity']}, ⚡/◆/✦ {cost}{gate}"


def render(data, catalog):
    out = ["# Б2.4 — матрица проверяемых направлений, 2026-11-18", "",
           "> Автоматическая **проверка исходного доступа**, не пройденные забеги, не точные шансы наград и не winrate.",
           "> Источник рецептов: `tools/balance/build_directions.json`; генератор: `python3 tools/balance/build_directions.py`.", "",
           "По три кандидата на героя. Это роли и взаимозаменяемые ориентиры, не обязательная колода и не гарантия выпадения всех карт.",
           "Названия карт/пулы/редкости/цены берутся из текущего каталога. Проверяются стартовые пары, наличие снаряжения/навыков,",
           "удалённые ванильные карты и роли. Условия текста, порядок хуков и победы требуют нативной финальной проверки.", "",
           "## Общие ограничения доступа", "",
           "- Обычные награды: сначала доля своего пула по редкости, затем правила заклинаний и Пути могут заменить её. 30% — не финальная доля.",
           "- Spell/Path-подмена сохраняет редкость исходной опции; если кандидата этой редкости нет, исходная карта остаётся.",
           "- Школа в наградах открыта при владении ≥1, ступень ≤ владение+1. В лавке можно увидеть низкие ступени и без владения; это не отменяет цену/состояние каста.",
           "- Путь — сохранённые 2 своих + 1 чужой, а не гарантированный конкретный Путь. Базовая связка должна работать до него.",
           "- Полный маршрут: выбор после босса 3 при уровне ≥8. Короткий 1→3→5: после босса 1 без порога уровня 8.",
           "- Формы: необязательный обряд за 1 очко лагеря; отмена бесплатна. Совместимость, праведность и заработанные ступени обязательны.",
           "- Договор → Вестник: Хранитель Гробницы либо две элиты после Договора. Лич: поздний акт и 10 побед Вестником (6 в явном short run).",
           "- Только выбранная подпись Повелителя стихии получает компетентность Мастера от Сущности. Другие заклинания/школы/возврат очков её не получают.", ""]
    for b in data["directions"]:
        out += [f"## {b['hero']} · {b['title']} (`{b['id']}`)", "",
                f"Старт: `{b['origin']}` → `{b['background']}`. Поддерживающий, но не обязательный Путь: `{b['path']}`.", "",
                "| Роль | Доступные кандидаты (⚡/◆/✦ до скидок/предрасположенности) |", "|---|---|"]
        for role, label in zip(ROLES, LABELS):
            out.append("| " + label + " | " + "; ".join(describe(r, catalog) for r in b[role]) + " |")
        out += ["", "Снаряжение: " + (", ".join(f"`{g}`" for g in b["gear"]) or "не является обязательным стартовым условием") + ".",
                "Узлы: " + ", ".join(f"`{s}`" for s in b["skills"]) + ".", "", b["access"], ""]
    out += ["## Объём реализации и оставшаяся проверка", "",
            "Две новые карты охоты закрывают общедоступный Раскол и защиту/добор стрелка; оружие и карта охоты гарантированы предысторией.",
            "У Регента в NEW-колоде три звена (заклинание/призыв/восстановление маны) вместо трёх базовых карт, без роста числа карт.",
            "Старые сохранения и Перерождение не прогоняют новую стартовую колоду. Обязательные карты формы/Пути используют существующие saved claims/defer, не бесконтрольную выдачу.",
            "Статическая матрица не доказывает игровые комбо, частоты появления или проходимость. Для финала цикла подготовлены нативные integrity cases и opt-in трассы; их исполнение не подменяется этим отчётом.", ""]
    return "\n".join(out)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--check", action="store_true")
    args = parser.parse_args()
    data = json.loads(RECIPES.read_text())
    deps = inputs()
    errors = validate(data, deps)
    if errors:
        for error in errors:
            print("FAIL", error)
        return 1
    text = render(data, deps[0])
    if args.check:
        if not OUT.exists() or OUT.read_text() != text:
            print("FAIL build matrix stale: python3 tools/balance/build_directions.py")
            return 1
    else:
        OUT.write_text(text)
    print("Build access: 9 directions / 3 heroes; role, identity, pool and rarity source checks PASS (not native combat)")
    return 0


if __name__ == "__main__":
    sys.exit(main())
