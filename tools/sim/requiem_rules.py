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


# Враги-маги: атаки стихией/магией — заклинания за ✦
def caster_mana_pool(rank: str) -> int:
    return {"elite": 6, "boss": 8}.get(rank, 4)


CASTER_MANA_REGEN = 2


def enemy_spell_cost(hits: int, total: int) -> int:
    return enemy_attack_cost(hits, total) + 1


def caster_turns_until_focus(rank: str, spell_cost: int) -> int:
    """Сколько ходов подряд маг колдует, прежде чем ✦ упадёт ниже 2 и он «Сосредоточится»."""
    mana, turns = caster_mana_pool(rank), 0
    while mana >= max(2, spell_cost) and turns < 50:
        mana = min(caster_mana_pool(rank), mana - spell_cost + CASTER_MANA_REGEN)
        turns += 1
    return turns


def spell_cost_with_discount(base: int, tier: int, mastery: int, discount: int = 0) -> int:
    """Цена с надбавкой предрасположенности (как SpellRules: дельта от базы) и скидкой (Высокая кровь)."""
    _, castable, cost, _ = cast_cost(base, tier, mastery)
    delta = cost - base if castable else 0
    return max(0, base - discount + delta)


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
    """tier — эффективная ступень (база + улучшения); сверх V: +1 урона за уровень, двуручное +2."""
    beyond = max(0, tier - 5) * (2 if kind == "twohanded" else 1) if kind in WEAPON_DAMAGE else 0
    return beyond + WEAPON_DAMAGE.get(kind, [0] * 5)[max(1, min(5, tier)) - 1]


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


# ---------- мана и владение (Core/ManaRules.cs, SpellAffinity.cs) ----------
def mana_start(pool: int) -> int:
    return max(0, pool) // 2


def mana_regen_gain(current: int, pool: int, regen: int) -> int:
    new = max(0, min(current + regen, max(pool, current)))
    return max(0, new - current)


def starting_mastery(hero: str, school: str) -> int:
    if hero == "Silent":
        return 1 if school in {"restoration", "alteration", "illusion"} else 0
    if hero == "Regent":
        return 2 if school == "destruction" else 1
    return 0


def reward_share(hero: str, total_mastery: int) -> float:
    if hero == "Regent":
        return 0.45
    if hero == "Silent":
        return 0.20
    return min(0.45, 0.10 + 0.05 * (max(0, total_mastery) // 2))


# ───────────── Груз (Core/BurdenRules.cs) ─────────────

def overload(total_weight: int, capacity: int) -> tuple[int, int, int, bool]:
    """(перегруз, −прирост ◆, −карт в стартовой руке, можно подбирать карты)."""
    excess = max(0, total_weight - capacity)
    if excess == 0:
        return 0, 0, 0, True
    if excess <= 5:
        return excess, 1, 0, True
    if excess <= 10:
        return excess, 1, 1, True
    return excess, 1, 1, False


def card_weight(rarity: str, is_curse: bool = False, is_status: bool = False) -> int:
    if is_status:
        return 0
    if is_curse:
        return 2
    return 2 if rarity in ("Rare", "Ancient") else 1


# ───────────── Происхождения и предыстории (Core/OriginRules.cs) ─────────────

def marshbark_heal(hp: int, max_hp: int) -> int:
    if max_hp <= 0 or hp <= 0:
        return 0
    if hp * 4 < max_hp:
        return 4
    if hp * 2 < max_hp:
        return 2
    return 0


def knight_plating(hp: int, max_hp: int) -> int:
    if max_hp <= 0:
        return 0
    if hp * 2 < max_hp:
        return 3
    if hp * 10 < max_hp * 7:
        return 2
    if hp * 10 < max_hp * 9:
        return 1
    return 0


def wizard_spell_bonus(mana: int, pool: int) -> int:
    if pool <= 0:
        return 0
    missing = max(0, pool - mana)
    return min(4, missing * 4 // pool)


def fugitive_bonus(zero_cost_cards: int) -> int:
    return max(0, min(3, zero_cost_cards))


def winddancer_bonus(cards_this_turn: int) -> int:
    return max(0, cards_this_turn) // 3


def beast_spirit(elites_defeated: int) -> tuple[int, int]:
    if elites_defeated <= 0:
        return 6, 3
    if elites_defeated == 1:
        return 12, 5
    return 20, 7


# ───────────── Алхимия (Core/AlchemyRules.cs) ─────────────

REAGENTS = ["Bloodmoss", "Bluebloom", "Yellowsorrel", "AshSalt", "FrostSalt", "StormCrystal", "Nightcap", "GraveDust",
            "DragonScale", "FragrantTear"]
ESSENCES = ["Health", "Mana", "Stamina", "Fire", "Frost", "Shock", "Poison", "Blight", "Resistance", "Illusion"]
# реагент i ↔ свойство i
POUCH_CAPACITY = 12
BASE_POISON_CHARGES = 3
RESTORATION_TURNS = 4

RECIPES = [  # (зелье, свойство A, свойство B) — порядок как в C#
    ("Health", "Health", "Health"), ("Stamina", "Stamina", "Stamina"), ("Mana", "Mana", "Mana"),
    ("Rejuvenation", "Health", "Stamina"), ("SecondWind", "Stamina", "Shock"), ("Clarity", "Mana", "Illusion"),
    ("Giant", "Health", "Resistance"), ("Knight", "Stamina", "Resistance"), ("Sorcerer", "Mana", "Shock"),
    ("FireWard", "Fire", "Resistance"), ("FrostWard", "Frost", "Resistance"), ("ShockWard", "Shock", "Resistance"),
    ("Feather", "Stamina", "Illusion"), ("WitchHunter", "Mana", "Resistance"), ("Invisibility", "Illusion", "Illusion"),
    ("Panacea", "Health", "Blight"),
    ("SnakeBite", "Poison", "Fire"), ("Toxin", "Poison", "Poison"), ("Rot", "Poison", "Blight"),
    ("Exhaustion", "Poison", "Frost"), ("Emptybloom", "Poison", "Shock"), ("Weakness", "Poison", "Stamina"),
    ("Corrosion", "Fire", "Frost"), ("Paralytic", "Poison", "Illusion"),
]
POISONS = {"SnakeBite", "Toxin", "Rot", "Exhaustion", "Emptybloom", "Weakness", "Corrosion", "Paralytic"}


def brew_tier(reagents_used: int, herbalism_steps: int = 0) -> int:
    tier = 3 if reagents_used >= 3 else 1
    return min(5, tier + 2 * max(0, herbalism_steps))


def ingredients(recipe: tuple[str, str, str], strong: bool) -> list[int]:
    _, a, b = recipe
    result = [ESSENCES.index(a), ESSENCES.index(b)]
    if strong:
        result.append(ESSENCES.index(a))
    return result


def can_afford(pouch: list[int], needed: list[int]) -> bool:
    return all(i < len(pouch) and pouch[i] >= needed.count(i) for i in set(needed))


def available_brews(pouch: list[int], herbalism_steps: int = 0) -> list[tuple[str, int, list[int]]]:
    """(зелье, ступень, реагенты) — сильнейший вариант каждого рецепта; сортировка по ступени (устойчивая)."""
    result = []
    for recipe in RECIPES:
        strong = ingredients(recipe, True)
        if can_afford(pouch, strong):
            result.append((recipe[0], brew_tier(3, herbalism_steps), strong))
            continue
        weak = ingredients(recipe, False)
        if can_afford(pouch, weak):
            result.append((recipe[0], brew_tier(2, herbalism_steps), weak))
    return sorted(result, key=lambda o: -o[1])


def reagent_drop_count(rank: str) -> int:
    return {"boss": 3, "elite": 2}.get(rank, 1)


def pouch_fit(current: int, incoming: int) -> int:
    return max(0, min(incoming, POUCH_CAPACITY - current))


_POISON_POTENCY = {  # (I, III, V)
    "SnakeBite": (3, 6, 10), "Toxin": (2, 4, 7), "Rot": (1, 2, 3), "Exhaustion": (2, 4, 7), "Emptybloom": (2, 4, 7),
    "Weakness": (1, 2, 3), "Corrosion": (1, 2, 3), "Paralytic": (10, 25, 100),
}


def poison_potency(brew: str, tier: int) -> int:
    table = _POISON_POTENCY.get(brew)
    if table is None:
        return 0
    return table[2] if tier >= 5 else table[1] if tier >= 3 else table[0]


# ───────────── Аффиксы врагов (Core/EnemyRules.cs) ─────────────

NORMAL_AFFIX_CHANCE = 25
ELITE_MINION_HP = 40


def affix_count(rank: str, act_index: int, roll: int, max_hp: int = 10 ** 9) -> int:
    if rank == "elite":
        return 0 if max_hp < ELITE_MINION_HP else 1
    if rank == "normal" and act_index >= 1 and roll < NORMAL_AFFIX_CHANCE:
        return 1
    return 0


# ───────────── Иллюзия (Core/SpellAffinity.cs) ─────────────

def control_hp_cap(tier: str) -> int | None:
    return {"novice": 20, "apprentice": 40, "adept": 70, "expert": 120}.get(tier)


# ───────────── Лавки (Core/ShopRules.cs) ─────────────

import math as _math

SHOP_TYPES = ("Wanderer", "Armorer", "Arcanist", "Herbalist")


def _round_away(x: float) -> int:
    """Math.Round(x, MidpointRounding.AwayFromZero) для x ≥ 0."""
    return int(_math.floor(x + 0.5))


def shop_act_pool(act_index: int) -> list[str]:
    return ["Wanderer", "Armorer", "Herbalist"] if act_index <= 0 else ["Wanderer", "Armorer", "Herbalist", "Arcanist"]


def roll_shop_type(hash_: int, act_index: int, shops_seen: int, previous: str | None) -> str:
    if shops_seen <= 0:
        return "Wanderer"
    pool = [t for t in shop_act_pool(act_index) if t != previous]
    return pool[hash_ % len(pool)]


def fnv1a(text: str, salt: int = 0) -> int:
    h = 2166136261
    for ch in text:
        h ^= ord(ch)
        h = (h * 16777619) & 0xFFFFFFFF
    h ^= salt & 0xFFFFFFFF
    h = (h * 16777619) & 0xFFFFFFFF
    h ^= h >> 15
    return h


def trust(gold_spent: int) -> int:
    return max(0, min(3, gold_spent // 150))


def apply_trust(price: int, trust_level: int) -> int:
    if price <= 0:
        return price
    return max(1, _round_away(price * (1 - 0.05 * max(0, min(3, trust_level)))))


def sell_value(price: int, trust_level: int) -> int:
    if price <= 0:
        return 0
    return max(1, _math.floor(price * (0.50 if trust_level >= 3 else 0.35)))


def _by_tier(tier: int, prices: tuple) -> int:
    return prices[max(1, min(len(prices), tier)) - 1]


EQUIP_PRICES = {
    "Weapon": (45, 80, 130, 190, 260),
    "Shield": (40, 70, 115, 170, 230),
    "Armor": (35, 65, 110, 160, 220),
    "Cloak": (35, 65, 110, 160, 220),
    "Ring": (90, 140, 140, 200, 200),
    "Amulet": (120, 180, 180, 250, 250),
}


def equipment_price(category: str, tier: int) -> int:
    return _by_tier(tier, EQUIP_PRICES[category])


def spell_price(tier: int, single: bool) -> int:
    folio = _by_tier(tier, (45, 70, 110, 160, 230))
    return _round_away(folio * 0.9) if single else folio


def brew_price(tier: int, poison: bool) -> int:
    return _by_tier(tier, (35, 50, 70, 95, 125) if poison else (40, 55, 75, 100, 130))


def treatise_price(bought_before: int) -> int:
    return 120 + 40 * max(0, bought_before)


def equipment_tiers(shop: str, act_index: int) -> tuple[int, int]:
    if shop == "Armorer":
        return max(1, min(4, act_index + 1)), max(2, min(5, act_index + 2))
    return 1, 2


def shop_brew_tier(act_index: int) -> int:
    return 1 if act_index <= 0 else 3 if act_index == 1 else 5


def combat_gold(vanilla: int) -> int:
    return vanilla if vanilla <= 0 else _round_away(vanilla * 1.3)


def spell_allowed_in_shop(tier: int, mastery: int) -> bool:
    return tier <= max(1, mastery) + 1


# доверие: витрина и Заказ
def refills_per_visit(trust_level: int) -> int:
    return max(0, min(3, trust_level))


def can_order(trust_level: int) -> bool:
    return trust_level >= 3


def order_price(item_price: int) -> int:
    return 0 if item_price <= 0 else _math.ceil(item_price * 0.5)


def order_tier(act_index: int) -> int:
    return max(2, equipment_tiers("Armorer", act_index)[1])


def order_item_price(category: str, act_index: int) -> int:
    tier = order_tier(act_index)
    return {
        "Weapon": equipment_price("Weapon", tier), "Equipment": equipment_price("Weapon", tier),
        "Armor": equipment_price("Armor", tier), "Shield": equipment_price("Shield", tier),
        "Spell": spell_price(max(1, min(5, tier)), True),
        "Potion": brew_price(5, False), "Poison": brew_price(5, True),
    }.get(category, 100)


# кража
def theft_chance(price: int) -> int:
    return max(0, min(80, 80 - 10 * (max(0, price) // 50)))


def theft_succeeds(hash_: int, price: int) -> bool:
    return (hash_ % 100) < theft_chance(price)


def theft_fail_damage(max_hp: int) -> int:
    return max(1, _math.ceil(max(0, max_hp) * 0.2))


# услуги
MAX_UPGRADE = 2
REFORGE_PRICE = 50
BREW_SERVICE_PRICE = 25
PANACEA_PRICE = 60
REAGENT_KIT_SIZE = 3


def sharpen_price(tier: int) -> int:
    return 60 if tier <= 2 else 90 if tier == 3 else 130


def reagent_kit_price(act_index: int) -> int:
    return max(20, min(35, 20 + 5 * max(0, act_index)))


def reagent_sell_value(count: int) -> int:
    return max(0, count) * 3


def removal_price(removals_used: int) -> int:
    return 75 + 25 * max(0, removals_used)


def death_echo_surcharge(removal: int, gold_left: int) -> int:
    return max(0, min(removal, max(0, gold_left)))


# ───────────── Камень Памяти (Core/MemoryStoneRules.cs) ─────────────

INFINITE = 2 ** 31 - 1


def candles_for(level: int) -> int:
    if level <= 0:
        return INFINITE
    if level <= 4:
        return 3
    if level <= 7:
        return 2
    if level <= 9:
        return 1
    return 0


def candles_left(level: int, used: int, bonus: int = 0, override: int = -1) -> int:
    total = override if override >= 0 else candles_for(level)
    if total == INFINITE:
        return INFINITE
    return max(0, total + max(0, bonus) - max(0, used))


def flesh_scar_max_hp(max_hp: int) -> int:
    if max_hp <= 1:
        return max_hp
    return max(1, min(max_hp - 1, _math.floor(max_hp * 0.94)))


def purse_scar_gold(gold: int) -> int:
    return 0 if gold <= 0 else _math.floor(gold * 0.6)


def score_multiplier(rollbacks: int) -> float:
    return max(0.0, 1 - 0.15 * max(0, rollbacks))


def scars_to_apply(in_dying_run: int, chosen_now: bool, in_imprint: int) -> int:
    return max(0, in_dying_run + (1 if chosen_now else 0) - in_imprint)


def vote_approved(yes_including_host: int, no: int) -> bool:
    return yes_including_host >= no


def shared_candles_left(per_player: list[int]) -> int:
    return min(per_player) if per_player else 0


def shared_candles_used(per_player: list[int]) -> int:
    return max(per_player) if per_player else 0


DEFAULT_SCAR = "Purse"


def map_rng_name(act_index: int, rollbacks: int) -> str:
    return f"act_{act_index + 1}_map_requiem_{rollbacks}"


# ───────────── этап 4: Пантеон (docs/design/05, Core/PantheonRules.cs) ─────────────

RIGHTEOUS, TAINTED, APOSTATE = 0, 1, 2
BLESSING_NONE, BLESSING_DIMMED, BLESSING_FULL = 0, 1, 2
OMEN_COUNT, OMEN_CHOICES, ALL_OMENS_LEVEL = 10, 3, 8
SHRINE_PERCENT, DARK_SHRINE_PERCENT, EBB_STONES_PERCENT = 14, 8, 8


def piety_sin(p: int) -> int:
    return TAINTED if p == RIGHTEOUS else APOSTATE


def piety_redeem(p: int) -> int:
    return TAINTED if p == APOSTATE else RIGHTEOUS


def piety_effective(recorded: int, holds_abyssal: bool, transformed: bool, permanent_apostate: bool) -> int:
    p = recorded
    if holds_abyssal and p < TAINTED:
        p = TAINTED
    if transformed or permanent_apostate:
        p = APOSTATE
    return p


def blessing_strength(effective: int, forbidden: bool) -> int:
    if forbidden:
        return BLESSING_NONE
    return {RIGHTEOUS: BLESSING_FULL, TAINTED: BLESSING_DIMMED}.get(effective, BLESSING_NONE)


def redemption_cost(act_index: int) -> int:
    return 50 + 25 * max(0, act_index)


def robbery_gold(act_index: int) -> int:
    return 40 + 20 * max(0, act_index)


def can_redeem(recorded: int, last_redeemed_act: int, act_index: int, forbidden: bool) -> bool:
    return not forbidden and recorded != RIGHTEOUS and last_redeemed_act != act_index


def unknown_room_roll(act_index: int, roll: int) -> int:
    """0 — обычное «?», 1 — Святилище, 2 — Тёмное святилище, 3 — Шесть камней (акты 2–3)."""
    if act_index < 1:
        return 0
    if roll < SHRINE_PERCENT:
        return 1
    if roll < SHRINE_PERCENT + DARK_SHRINE_PERCENT:
        return 2
    if act_index <= 2 and roll < SHRINE_PERCENT + DARK_SHRINE_PERCENT + EBB_STONES_PERCENT:
        return 3
    return 0


def omen_offer_count(requiem_level: int) -> int:
    return OMEN_COUNT if requiem_level >= ALL_OMENS_LEVEL else OMEN_CHOICES


def pick_distinct(total: int, count: int, next_int, exclude=None) -> list[int]:
    pool = [i for i in range(max(0, total)) if not exclude or i not in exclude]
    count = max(0, min(count, len(pool)))
    for i in range(count):
        j = i + max(0, min(next_int(len(pool) - i), len(pool) - i - 1))
        pool[i], pool[j] = pool[j], pool[i]
    return pool[:count]


def has_divine_spirit(strength: int, amulet: bool, robe: bool, heavenly_shield: bool) -> bool:
    return strength == BLESSING_FULL and amulet and robe and heavenly_shield


def outcast_after_combat_heal(vow_done: bool) -> int:
    return 2 * (2 if vow_done else 1)


def below_half(hp: int, max_hp: int) -> bool:
    return max_hp > 0 and hp * 2 < max_hp


def star_marks_after_act(marks: int, vow_done: bool) -> int:
    return 0 if vow_done else marks + 1

