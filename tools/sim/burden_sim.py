"""Симуляция Груза по этажам (docs/design/02 §3; плейтест 2026-11: «карты не берутся»).

Грубая модель забега: на каждом этаже — тип комнаты по долям ванильной карты, после боя — награда картой
(берётся с вероятностью take_rate), у элит и сундуков — реликвия, в лавке — покупка. Веса — как в коде мода
(`BurdenRules.DefaultCardWeight`, `data/vanilla_relics.csv`, снаряжение — по типам в Equipment/*.cs).

Вывод: на каком этаже перегруз впервые выходит за +5 и за +10 (медиана и доля забегов), с удалением карт
в лавке и без «Выбросить». Цель дизайна 02 §3: «запас ~10 единиц на первый акт».

    python3 tools/sim/burden_sim.py [--runs 4000]
"""
from __future__ import annotations

import argparse
import csv
import os
import random
import statistics

from requiem_rules import card_weight, overload

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))

# Вместимость (02 §3) и стартовая колода (ванильные стартовые колоды: Ironclad 10, Silent 12, Regent 10).
HEROES = {
    "Platebearer": {"capacity": 32, "deck": 10},
    "Silent": {"capacity": 26, "deck": 12},
    "Regent": {"capacity": 24, "deck": 10},
}

FLOORS_PER_ACT = 15
# Доли комнат на этаже (как в ванильной генерации, без первого и последнего этажа).
ROOMS = [("monster", 0.45), ("elite", 0.12), ("event", 0.22), ("rest", 0.12), ("shop", 0.06), ("treasure", 0.03)]
CARD_RARITY = [("Common", 0.60), ("Uncommon", 0.33), ("Rare", 0.07)]
# Реликвия: доля снаряжения и его средний вес (оружие 1–5, броня 1–4 за часть, украшения 0–1).
EQUIPMENT_SHARE = 0.35
EQUIPMENT_WEIGHTS = [0, 0, 1, 1, 2, 2, 2, 3, 3, 4]


def vanilla_relic_weights() -> list[int]:
    path = os.path.join(ROOT, "mod", "SlayTheRequiem", "data", "vanilla_relics.csv")
    lines = [line for line in open(path, encoding="utf-8") if not line.startswith("#")]
    rows = [r for r in csv.DictReader(lines) if r["bucket"] != "cut" and r["rarity"] in ("Common", "Uncommon", "Rare")]
    return [int(r["weight"] or 1) for r in rows]


def pick(rng: random.Random, table):
    x, acc = rng.random(), 0.0
    for item, p in table:
        acc += p
        if x < acc:
            return item
    return table[-1][0]


def relic_weight(rng: random.Random, vanilla: list[int]) -> int:
    if rng.random() < EQUIPMENT_SHARE:
        return rng.choice(EQUIPMENT_WEIGHTS)
    return rng.choice(vanilla)


def level_for(renown: int) -> int:
    """ProgressionRules.LevelFor: порог уровня L → L+1 равен L+2 Славы."""
    level = 1
    while level < 15 and renown >= (level) * (level + 5) // 2:
        level += 1
    return level


def run_once(rng: random.Random, hero: str, vanilla: list[int], take_rate: float, removals: bool,
             burden_points: bool = False, acts: int = 3):
    """Возвращает (этаж первого перегруза > 5, этаж > 10, карты, не взятые из-за запрета).

    burden_points — игрок вкладывает одно из двух очков уровня в Груз (+3 вместимости, ProgressionRules.BurdenPerPoint),
    пока перегружен.
    """
    h = HEROES[hero]
    weight, capacity = h["deck"], h["capacity"]
    renown, level = 0, 1
    first5 = first10 = None
    blocked = 0
    floor = 0
    for act in range(acts):
        for f in range(FLOORS_PER_ACT):
            floor += 1
            room = "monster" if f == 0 else ("rest" if f == FLOORS_PER_ACT - 1 else pick(rng, ROOMS))
            _, _, _, can_pick = overload(weight, capacity)
            if room in ("monster", "elite"):
                renown += 3 if room == "elite" else 1
                new_level = level_for(renown)
                if burden_points and new_level > level and weight + 4 > capacity:
                    capacity += 3 * (new_level - level)
                level = new_level
            if room in ("monster", "elite") and rng.random() < take_rate:
                w = card_weight(pick(rng, CARD_RARITY))
                if can_pick:
                    weight += w
                else:
                    blocked += 1
            if room in ("elite", "treasure") and can_pick:
                weight += relic_weight(rng, vanilla)
            if room == "event" and rng.random() < 0.3 and can_pick:
                weight += 1
            if room == "shop":
                if removals and weight > capacity:
                    weight -= 1  # удаление Удара / Защиты
                if can_pick and rng.random() < 0.6:
                    weight += relic_weight(rng, vanilla)
            excess = weight - capacity
            if first5 is None and excess > 5:
                first5 = floor
            if first10 is None and excess > 10:
                first10 = floor
        weight += relic_weight(rng, vanilla)  # реликвия босса
    return first5, first10, blocked


def summarize(values: list[int | None], total_floors: int) -> str:
    hit = [v for v in values if v is not None]
    if not hit:
        return "никогда"
    share = len(hit) / len(values) * 100
    return f"медиана этаж {int(statistics.median(hit))} (в {share:.0f} % забегов, из {total_floors})"


def main() -> None:
    ap = argparse.ArgumentParser()
    ap.add_argument("--runs", type=int, default=4000)
    ap.add_argument("--seed", type=int, default=7)
    args = ap.parse_args()
    vanilla = vanilla_relic_weights()
    rng = random.Random(args.seed)
    total = FLOORS_PER_ACT * 3
    print(f"Груз: {args.runs} забегов на героя, {total} этажей, ванильных реликвий с весом: {len(vanilla)}\n")
    for take_rate, removals, points, label in [
        (0.9, False, False, "берёт 90 % карт, без удалений, очки уровня не в Груз"),
        (0.7, True, False, "берёт 70 % карт, удаляет в лавке"),
        (0.7, True, True, "берёт 70 % карт, удаляет в лавке, при перегрузе 1 очко уровня в Груз (+3)"),
    ]:
        print(f"## {label}")
        for hero in HEROES:
            res = [run_once(rng, hero, vanilla, take_rate, removals, points) for _ in range(args.runs)]
            blocked = statistics.mean(r[2] for r in res)
            print(f"- {hero:12s} перегруз > 5: {summarize([r[0] for r in res], total)}; "
                  f"> 10: {summarize([r[1] for r in res], total)}; не взято карт за забег: {blocked:.1f}")
        print()


if __name__ == "__main__":
    main()
