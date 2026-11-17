"""Отчёт об эффективности карт мода: «ценность за цену» по героям и редкостям (ночной план 2026-11-17, этап 2).

Ценность — грубая сумма: урон (умноженный на число целей), Блок и статусы по весам ниже. Цена — ⚡ + 0,5·◆ + 0,5·✦
(◆ и ✦ — дешевле энергии: восполняются каждый ход, но ограничены пулом). Сравнение — с медианой своей группы
(герой × редкость). Это не симулятор боя: эффекты из кода (условия, «если Устали», добор) не учитываются, поэтому
карты с текстовыми условиями помечаются «?» и в выбросы не попадают. Отчёт подсказывает, на что посмотреть глазами.

    python3 tools/cards/balance_report.py [--out docs/plan/card-balance.md]
"""
from __future__ import annotations

import argparse
import os
import re
import statistics
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from card_catalog import collect  # noqa: E402

# Вес статуса в единицах «урона» (за 1 стак). Опора — ванильные ориентиры: Уязвимость 1 ≈ 3 урона, Слабость 1 ≈ 2,5 Блока.
STATUS_VALUE = {
    "VulnerablePower": 3, "WeakPower": 2.5, "FrailPower": 2, "StrengthPower": 5, "DexterityPower": 4,
    "StaggerPower": 1, "SunderPower": 1.5, "BleedPower": 1.5, "ChillPower": 1.2, "BurningPower": 1.5,
    "PoisonPower": 1.3, "StaticPower": 1.2, "PlatingPower": 4, "WardPower": 0.8, "DrainVar": 1,
    "Cards": 3.5, "Draw": 3.5, "StaminaGain": 1.5, "ManaGain": 1.5,
}
HEROES = ("Ironclad", "Silent", "Regent")
HERO_TITLE = {"Ironclad": "Латоносец", "Silent": "Безмолвная", "Regent": "Регент"}
CONDITIONAL = re.compile(r"Если|если|С луком|Со щитом|Вдвое|за каждую|за каждый|Каждый|Каждая|в этом ходу", re.U)


def num(value: str) -> float:
    m = re.match(r"\s*(-?\d+)", value or "")
    return float(m.group(1)) if m else 0.0


def first(raw) -> float:
    return num(raw[0]) if raw else 0.0


def evaluate(row: dict) -> tuple[float, float, bool]:
    fx = row["_fx"]
    targets = 2.2 if row["target"] == "AllEnemies" else 1.0
    hits = first(fx.get("Hits", [])) or 1.0
    value = first(fx.get("Damage", [])) * targets * hits + first(fx.get("Block", []))
    for key, raw in fx.items():
        base = key.split(".")[-1]
        if base in STATUS_VALUE:
            value += STATUS_VALUE[base] * first(raw) * (targets if base.endswith("Power") and row["target"] == "AllEnemies" else 1)
    energy = num(str(row["energy"]))
    stamina = num(row["stamina"])
    mana = num(str(row["mana"]))
    cost = max(0.5, energy + 0.5 * stamina + 0.5 * mana)
    conditional = bool(CONDITIONAL.search(row["desc"])) or row["type"] == "Power"
    return value, cost, conditional


def build() -> str:
    rows = collect()
    groups: dict[tuple[str, str], list[tuple[dict, float]]] = {}
    for group_rows in rows.values():
        for row in group_rows:
            if row["pool"] not in HEROES or row["type"] not in ("Attack", "Skill"):
                continue
            value, cost, cond = evaluate(row)
            if value <= 0:
                continue
            row["_ratio"], row["_cond"], row["_value"], row["_cost"] = value / cost, cond, value, cost
            groups.setdefault((row["pool"], row["rarity"]), []).append((row, value / cost))

    out = ["# Баланс карт: ценность за цену (сгенерировано)", "",
           "> `python3 tools/cards/balance_report.py --out docs/plan/card-balance.md`. Грубая оценка, не симулятор боя:",
           "> ценность = урон × цели + Блок + статусы по весам; цена = ⚡ + 0,5·◆ + 0,5·✦. «?» — у карты условие в тексте,",
           "> оценка неполная. Выброс — отклонение от медианы своей группы больше 35 %.", ""]
    flagged = []
    for hero in HEROES:
        out.append(f"## {HERO_TITLE[hero]}")
        out.append("")
        out.append("| Редкость | Карт | Медиана | Мин | Макс |")
        out.append("|---|---|---|---|---|")
        for rarity in ("Common", "Uncommon", "Rare"):
            items = groups.get((hero, rarity), [])
            if not items:
                continue
            ratios = [r for _, r in items]
            med = statistics.median(ratios)
            out.append(f"| {rarity} | {len(items)} | {med:.1f} | {min(ratios):.1f} | {max(ratios):.1f} |")
            for row, ratio in items:
                dev = (ratio - med) / med if med else 0
                if abs(dev) > 0.35 and not row["_cond"]:
                    flagged.append((hero, rarity, row, ratio, med, dev))
        out.append("")
    out.append("## Выбросы (без условий в тексте)")
    out.append("")
    out.append("| Герой | Редкость | Карта | ⚡/◆/✦ | Ценность | За цену | Медиана | Отклонение |")
    out.append("|---|---|---|---|---|---|---|---|")
    for hero, rarity, row, ratio, med, dev in sorted(flagged, key=lambda x: x[5]):
        cost = f"{row['energy']}/{row['stamina'] or 0}/{row['mana'] or 0}"
        out.append(f"| {HERO_TITLE[hero]} | {rarity} | `{row['name']}` {row['title']} | {cost} | {row['_value']:.1f} | "
                   f"{ratio:.1f} | {med:.1f} | {dev * 100:+.0f} % |")
    if not flagged:
        out.append("| — | | | | | | | |")
    out.append("")
    return "\n".join(out)


def main() -> int:
    ap = argparse.ArgumentParser()
    ap.add_argument("--out")
    args = ap.parse_args()
    text = build()
    if args.out:
        with open(args.out, "w", encoding="utf-8") as f:
            f.write(text)
    else:
        print(text)
    return 0


if __name__ == "__main__":
    sys.exit(main())
