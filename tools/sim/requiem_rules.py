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


IMMUNE_TIER = 4


def resistance_multiplier(tier: int, immune: bool = False) -> float:
    if immune or tier >= IMMUNE_TIER:
        return 0.0
    if tier < 0:
        return 1.5
    return 1.0 - 0.25 * min(tier, 3)


def resist_stacks(stacks: int, tier: int, immune: bool = False) -> int:
    if immune or stacks <= 0 or tier >= IMMUNE_TIER:
        return 0
    if tier < 0:
        return math.ceil(stacks * 1.5)
    return math.floor(stacks * resistance_multiplier(tier))


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


# Враги (docs/design/11 §1, §4)
def enemy_stamina_pool(rank: str) -> int:
    return {"elite": 4, "boss": 5}.get(rank, 3)


def enemy_attack_cost(hits: int, total: int) -> int:
    return 2 if hits >= 3 or total >= 20 else 1


def _round_half_up(x: float) -> int:
    return int(math.floor(x + 0.5))


def enemy_poise(rank: str, max_hp: int) -> int:
    if rank == "boss":
        return 0
    base = max(3, min(20, _round_half_up(max_hp / 8)))
    return base + 3 if rank == "elite" else base


def raise_threshold(t: int) -> int:
    return math.ceil(t * 1.5)


def stagger(stagger_amount: int, threshold: int) -> tuple[bool, int, int]:
    if threshold <= 0:
        return (False, stagger_amount, threshold)
    if stagger_amount >= threshold:
        return (True, 0, raise_threshold(threshold))
    return (False, stagger_amount, threshold)


# ---------- снаряжение (Core/Equipment.cs) ----------
WEAPON_DAMAGE = {
    "dagger": [0, 1, 2, 2, 3], "sword": [1, 2, 3, 3, 4], "axe": [1, 2, 3, 3, 4], "mace": [1, 2, 3, 3, 4],
    "spear": [1, 2, 3, 3, 4], "twohanded": [3, 4, 5, 7, 8], "bow": [2, 3, 4, 5, 6], "crossbow": [4, 5, 6, 7, 8],
}
ARMOR_SLOTS = {"head", "body", "hands", "feet"}


def weapon_damage(kind: str, tier: int) -> int:
    return WEAPON_DAMAGE.get(kind, [0] * 5)[max(1, min(5, tier)) - 1]


def loadout(items: list[dict]) -> dict:
    """items: {slot, armor, tier, weapon}."""
    armor = [i for i in items if i["slot"] in ARMOR_SLOTS]
    heavy = [i for i in armor if i.get("armor") == "heavy"]
    light = sum(1 for i in armor if i.get("armor") == "light")
    main = next((i for i in items if i["slot"] == "main"), None)
    off = next((i for i in items if i["slot"] == "off"), None)
    two = main is not None and main.get("weapon") in {"twohanded", "bow", "crossbow"}
    duel = main is not None and not two and off is None
    monk = main is None and off is None
    return {
        "plating": sum(1 for i in heavy if i["tier"] >= 2) // 2 + sum(1 for i in heavy if i["tier"] >= 4),
        "poise": len(heavy),
        "mana_penalty": 1 if len(heavy) >= 3 else 0,
        "dodge": 0 if len(heavy) >= 3 else 2 if light >= 4 else 1 if light >= 2 else 0,
        "stamina_pool": light // 2,
        "stamina_regen": 1 if duel or monk else 0,
        "weapon_damage": weapon_damage(main["weapon"], main["tier"]) if main else 0,
    }
