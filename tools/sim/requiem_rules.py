"""Зеркало mod/SlayTheRequiemCode/Core на Python.

Нужно, чтобы проверять и балансировать правила без .NET и без игры.
При изменении Core/*.cs обновляйте этот файл и tools/sim/test_requiem_rules.py
(те же случаи, что в mod/SlayTheRequiem.Tests/CoreTests.cs).
"""
from __future__ import annotations

import csv
import math
from dataclasses import dataclass

RESTED, TIRED, EXHAUSTED = "rested", "tired", "exhausted"
OVERSTRAIN_HP_PER_POINT = 2


def stamina_state(current: int, pool: int, cost: int) -> str:
    if cost > current:
        return EXHAUSTED
    if pool > 0 and current * 2 < pool:
        return TIRED
    return RESTED


def damage_multiplier(state: str) -> float:
    return {RESTED: 1.0, TIRED: 0.75}.get(state, 0.5)


@dataclass(frozen=True)
class Payment:
    spent: int
    shortfall: int
    hp_loss: int
    state: str
    multiplier: float


def pay(current: int, pool: int, cost: int) -> Payment:
    cost = max(0, cost)
    state = stamina_state(current, pool, cost)
    spent = min(current, cost)
    short = cost - spent
    return Payment(spent, short, short * OVERSTRAIN_HP_PER_POINT, state, damage_multiplier(state))


def regenerate(current: int, pool: int, regen: int) -> int:
    return max(0, min(current + regen, max(pool, current)))


def apply_plating(damage: int, plating: int, pierce: int = 0, sunder: int = 0) -> int:
    if damage <= 0:
        return 0
    eff = plating - sunder
    if eff > 0:
        eff = max(0, eff - max(0, pierce))
        return damage if eff == 0 else max(1, damage - eff)
    return damage - eff


def resistance_multiplier(tier: int, immune: bool = False) -> float:
    if immune:
        return 0.0
    if tier < 0:
        return 1.5
    return 1.0 - 0.25 * min(tier, 3)


def resist_stacks(stacks: int, tier: int, immune: bool = False) -> int:
    if immune or stacks <= 0:
        return 0
    return math.floor(stacks * resistance_multiplier(max(0, tier)))


def burning_total(stacks: int) -> int:
    total = 0
    while stacks > 0:
        total += stacks
        stacks //= 2
    return total


def burning_vs_block(damage: int, block: int) -> tuple[int, int]:
    if damage <= 0:
        return (0, 0)
    block = max(0, block)
    absorbed = min(damage, block // 2)
    return (min(block, damage * 2), damage - absorbed)


def poison_total(stacks: int) -> int:
    return 0 if stacks <= 0 else stacks * (stacks + 1) // 2


def static_tick(stacks: int, mana: int) -> tuple[int, int, int]:
    stacks = max(0, stacks)
    loss = min(stacks, max(0, mana))
    return (loss, stacks - loss, max(0, stacks - 1))


# Предрасположенность к школам (docs/design/08 §8)
def cast_cost(base_mana: int, tier: int, mastery: int, from_scroll: bool = False) -> tuple[str, bool, int, float]:
    base_mana = max(0, base_mana)
    if from_scroll:
        return ("free", True, 0, 1.0)
    gap = tier - max(0, min(mastery, 5))
    if gap <= -2:
        return ("ease", True, max(0, base_mana - 1), 1.0)
    if gap <= 0:
        return ("free", True, base_mana, 1.0)
    if gap == 1:
        return ("clumsy", True, math.ceil(base_mana * 1.5), 1.0)
    if gap == 2:
        return ("strained", True, base_mana * 2, 0.75)
    return ("impossible", False, base_mana, 0.0)


def load_stamina_table(path: str) -> dict[str, dict[str, str]]:
    with open(path, encoding="utf-8") as f:
        rows = csv.DictReader(line for line in f if not line.startswith("#"))
        return {r["key"]: r for r in rows}
