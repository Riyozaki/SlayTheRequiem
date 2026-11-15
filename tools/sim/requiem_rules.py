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



# Кланы Латоносца (M4 п. 1)
FORGE_FURY_ATTACKS = 3


def forge_fury_active(attacks_this_turn: int) -> bool:
    return attacks_this_turn >= FORGE_FURY_ATTACKS


def dune_rhythm_regen(stamina: int, pool: int) -> int:
    if pool <= 0:
        return 0
    if stamina * 2 < pool:
        return 2
    if stamina * 4 < pool * 3:
        return 1
    return 0


def training_bonus_points(level: int, max_level: int = 15) -> int:
    return max(1, min(max_level, level)) // 3


def conspirator_potency(potency: int) -> int:
    return potency if potency <= 0 else (potency * 5 + 3) // 4


def blade_spirit_bonus(mana: int) -> int:
    return max(0, mana) // 2


def loyal_guard(level: int, max_level: int = 15) -> tuple[int, int]:
    lv = max(1, min(max_level, level))
    return 8 + 2 * lv, 2 + lv // 4

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


MAX_AFFIXES = 4


def affix_count(rank: str, act_index: int, roll: int, max_hp: int = 10 ** 9, requiem_level: int = 1,
                elite_bonus: int = 0) -> int:
    if rank == "elite":
        if max_hp < ELITE_MINION_HP:
            return 0
        return min(MAX_AFFIXES, elite_affix_base(requiem_level, roll) + max(0, elite_bonus))
    if rank == "normal" and act_index >= 1 and roll < NORMAL_AFFIX_CHANCE:
        return 1
    return 0


ENEMY_AFFIXES = ["Hardened", "Fiery", "Icy", "Stormy", "Draining", "ManaEater", "Vampiric", "PoisonedBlade",
                 "Bannerman", "Absorber", "Frenzied", "Ghostly", "Avenger"]
ELEMENTAL_AFFIXES = ("Fiery", "Icy", "Stormy")
FRENZY_HP_PERCENT = 50


def pick_affixes(count: int, rolls) -> list:
    pool = list(ENEMY_AFFIXES)
    res = []
    for roll in rolls:
        if len(res) >= count or not pool:
            break
        affix = pool[roll % len(pool)]
        res.append(affix)
        pool.remove(affix)
        if affix in ELEMENTAL_AFFIXES:
            pool = [a for a in pool if a not in ELEMENTAL_AFFIXES]
    return res


def is_frenzied(hp: int, max_hp: int) -> bool:
    return max_hp > 0 and hp * 100 < max_hp * FRENZY_HP_PERCENT


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



# ═════════════ Этап 5: Прогрессия (Core/ProgressionRules.cs) ═════════════

MAX_LEVEL = 15


def renown_for(rank: str) -> int:
    return {"boss": 6, "elite": 3}.get(rank, 1)


def renown_to_reach(level: int) -> int:
    level = max(1, min(MAX_LEVEL, level))
    return (level - 1) * (level + 4) // 2


def level_for(renown: int) -> int:
    level = 1
    while level < MAX_LEVEL and renown >= renown_to_reach(level + 1):
        level += 1
    return level


def renown_to_next(renown: int) -> int:
    level = level_for(renown)
    return 0 if level >= MAX_LEVEL else renown_to_reach(level + 1) - renown


def stat_points_for(level: int) -> int:
    return 2 * (max(1, min(MAX_LEVEL, level)) - 1)


def skill_points_for(level: int) -> int:
    return max(1, min(MAX_LEVEL, level)) // 2


def retained_renown(imprint: int, dead: int) -> int:
    return imprint + max(0, dead - imprint) // 2


def stat_tier(points: int) -> int:
    return max(0, min(3, points // 3))


def with_meditation(tier: int, meditated: bool) -> int:
    return min(3, tier + 1) if meditated else tier


def tiers(hp: int, st: int, mana: int) -> dict:
    return dict(
        poison_res=1 if hp >= 1 else 0, burden=6 if hp >= 2 else 0, plating=1 if hp >= 3 else 0,
        stamina_regen=1 if st >= 1 else 0, attack_damage=1 if st >= 2 else 0, free_first_attack=st >= 3,
        mana_regen=1 if mana >= 1 else 0, first_spell_discount=1 if mana >= 2 else 0, ward=3 if mana >= 3 else 0)


def is_clumsy(hero: str, main_hand: str, heavy_shield: bool, trained: bool) -> bool:
    if trained:
        return False
    if hero == "Regent":
        return main_hand in ("TwoHanded", "Crossbow") or heavy_shield
    if hero == "Silent":
        return main_hand == "TwoHanded"
    return False


# Деревья навыков: ветка → узлы по рангу (Выживание — ярусы по 3).
SKILL_LAYOUT = {
    "Craft": ["CraftAlchemy", "CraftEnchanting", "CraftSmithing", "CraftArtisan"],
    "Martial": ["WeaponTraining", "Conditioning", "SecondBreath", "Veteran"],
    "Survival": ["CampKettle", "DeftPacking", "FirstAid", "Wayfarer", "Acclimatization", "TradeName",
                 "WellRested", "CampMaster", "HerbLore"],
    "OneHanded": ["Fighter", "Fencer", "Butcher", "Crusher"],
    "TwoHanded": ["Sweep", "HeavyHand", "Shatter", "Relentless"],
    "Block": ["ShieldBearer", "ShieldWall", "ShieldBash", "Bastion"],
    "HeavyArmor": ["Hardened", "Steadfast", "WeightBearer", "AgainstAllOdds"],
    "Blades": ["Bloodletter", "Ambush", "Precision", "Hemorrhage"],
    "Archery": ["Marksman", "QuickDraw", "AimedShot", "ArmorPiercer"],
    "Evasion": ["Nimble", "Counterstrike", "LightStep", "Elusive"],
    "Poisons": ["Poisoner", "Toxicologist", "VenomWard", "VenomMaster"],
    "Destruction": ["Destruction1", "Destruction2", "Destruction3", "ElementLord"],
    "Restoration": ["Restoration1", "Restoration2", "Restoration3", "Warden"],
    "Alteration": ["Alteration1", "Alteration2", "Alteration3", "Stoneskin"],
    "Conjuration": ["Conjuration1", "Conjuration2", "Conjuration3", "BoundMaster"],
    "Illusion": ["Illusion1", "Illusion2", "Illusion3", "Phantasm"],
}
SCHOOL_BRANCHES = ["Destruction", "Restoration", "Alteration", "Conjuration", "Illusion"]
COMMON_BRANCHES = ["Craft", "Martial", "Survival"]
SKILL_NODES = {}
for _branch, _skills in SKILL_LAYOUT.items():
    for _i, _s in enumerate(_skills):
        SKILL_NODES[_s] = (_branch, _i // 3 + 1 if _branch == "Survival" else _i + 1)


def hero_branches(hero: str) -> list:
    return {"Ironclad": ["OneHanded", "TwoHanded", "Block", "HeavyArmor"],
            "Silent": ["Blades", "Archery", "Evasion", "Poisons"]}.get(hero, [])


def branches_for(hero: str) -> list:
    out = []
    for b in hero_branches(hero) + COMMON_BRANCHES + SCHOOL_BRANCHES:
        if b not in out:
            out.append(b)
    return out


def skill_cost(skill: str, native: bool) -> int:
    return (1 if native else 2) if SKILL_NODES[skill][0] in SCHOOL_BRANCHES else 1


def can_learn(skill: str, learned: set, branches: list) -> bool:
    branch, rank = SKILL_NODES[skill]
    if branch not in branches or skill in learned:
        return False
    if branch == "Survival":
        return sum(1 for s in learned if SKILL_NODES[s][0] == "Survival") >= 2 * (rank - 1)
    if rank == 1:
        return True
    return any(SKILL_NODES[s] == (branch, rank - 1) for s in learned)


def learnable(hero: str, learned: set, points: int, is_native) -> list:
    br = branches_for(hero)
    out = []
    for s, (branch, _) in SKILL_NODES.items():
        if not can_learn(s, learned, br):
            continue
        native = branch in SCHOOL_BRANCHES and is_native(branch)
        if skill_cost(s, native) <= points:
            out.append(s)
    return out


# ═════════════ Этап 5: Лагерь (Core/CampRules.cs) ═════════════

CAMP_COSTS = {"Rest": 2, "ShortRest": 1, "Smith": 2, "Forge": 2, "Brew": 1, "Enchant": 2, "Cook": 1,
              "Pray": 1, "Chest": 0, "Meditate": 1, "Train": 0}


def camp_budget(wayfarer: bool, camp_master: bool) -> int:
    return 2 + (1 if wayfarer else 0) + (1 if camp_master else 0)


def camp_cost(action: str, camp_master: bool) -> int:
    if camp_master and action in ("Forge", "Enchant"):
        return 1
    return CAMP_COSTS.get(action, 2)


def short_rest_percent(first_aid: bool) -> int:
    return 20 if first_aid else 15


def fatigue_stacks(nodes_since_rest: int, enabled: bool) -> int:
    return max(0, min(3, nodes_since_rest // 3)) if enabled else 0


def soul_stone_for(rank: str, act_index: int) -> int:
    if rank == "normal":
        return 0
    power = 1 if act_index <= 1 else 2 if act_index <= 3 else 3
    if rank == "boss":
        power += 1
    return max(1, min(3, power))


DISH_EFFECTS = {
    "BloodmossStew": dict(max_hp=6), "VenisonRoast": dict(stamina_regen=1), "FishSoup": dict(mana_regen=1),
    "SpicedSausage": dict(frost_res=1, start_stamina=2), "MushroomStew": dict(regen=1), "SweetRoll": dict(opening_draw=1),
}
DISH_FIELDS = ["max_hp", "stamina_regen", "mana_regen", "frost_res", "start_stamina", "regen", "opening_draw"]


def dish_effect(dish: str) -> dict:
    if dish == "Feast":
        total = {f: 0 for f in DISH_FIELDS}
        for d in DISH_EFFECTS:
            for k, v in DISH_EFFECTS[d].items():
                total[k] = max(total[k], v) if k == "frost_res" else total[k] + v
        return total
    out = {f: 0 for f in DISH_FIELDS}
    out.update(DISH_EFFECTS[dish])
    return out


def dish_duration(dish: str) -> int:
    return 1 if dish == "Feast" else 3


def feast_ingredients(pouch: list):
    left = list(pouch)
    picked = []
    while len(picked) < 5:
        best = -1
        for i, n in enumerate(left):
            if i == REAGENTS.index("DragonScale") or n <= 0:
                continue
            if best < 0 or n > left[best]:
                best = i
        if best < 0:
            return None
        left[best] -= 1
        picked.append(best)
    return picked


def eat(meals: list, dish: str, slots: int) -> list:
    lst = [m for m in meals if m[1] > 0]
    meal = (dish, dish_duration(dish))
    for i, m in enumerate(lst):
        if m[0] == dish:
            lst[i] = meal
            return lst
    while len(lst) >= max(1, slots):
        lst.pop(0)
    lst.append(meal)
    return lst


def meals_after_combat(meals: list) -> list:
    return [(d, f - 1) for d, f in meals if f - 1 > 0]


ENCHANT_SLOTS = {
    "Health": ("Head", "Body"), "Stamina": ("Feet", "Body"), "Magic": ("Head", "Ring"),
    "FireResistance": ("OffHand", "Ring", "Feet"), "FrostResistance": ("OffHand", "Ring", "Feet"),
    "ShockResistance": ("OffHand", "Ring", "Feet"), "WeaponMight": ("Hands", "Ring"), "Burden": ("Hands", "Feet"),
    "FireBrand": ("MainHand",), "FrostBrand": ("MainHand",), "ShockBrand": ("MainHand",),
}


def enchant_allowed(e: str, slot: str) -> bool:
    return slot in ENCHANT_SLOTS[e]


def enchant_value(e: str, power: int) -> int:
    p = max(1, min(3, power))
    return {"Health": 4 * p, "Stamina": p, "Magic": 2 * p, "WeaponMight": p, "Burden": 4 * p,
            "FireBrand": 2 * p, "FrostBrand": p, "ShockBrand": p}.get(e, 2 if p >= 3 else 1)


def enchant_charges(power: int) -> int:
    return 4 + 4 * max(1, min(3, power))


def apply_enchant(current: list, e: str, power: int, slots: int) -> list:
    lst = list(current)
    brand = e in ("FireBrand", "FrostBrand", "ShockBrand")
    slot = (e, max(1, min(3, power)), enchant_charges(power) if brand else 0)
    for i, s in enumerate(lst):
        if s[0] == e:
            lst[i] = slot
            return lst
    while len(lst) >= max(1, slots):
        lst.pop(0)
    lst.append(slot)
    return lst


# ───── Кличи (04 «Глас») ─────

def shout_cooldown(base: int, reduction: int) -> int:
    return max(1, base - max(0, reduction))


def shout_shelf_capacity(skald: bool) -> int:
    return 2 if skald else 1


def soul_tear_executes(hp: int, max_hp: int, boss: bool) -> bool:
    return not boss and hp > 0 and hp * 4 < max_hp


# ───── Хмельной Князь: вторые свойства напитков ─────

def revel_second_property(brew: str, tier: int, is_poison: bool) -> tuple:
    step = max(1, (tier + 1) // 2)
    if brew in ("Health", "Rejuvenation"):
        return ("Stamina", step)
    if brew in ("Stamina", "SecondWind"):
        return ("Mana", step)
    if brew in ("Mana", "Clarity"):
        return ("Heal", 2 * step)
    if is_poison:
        return ("None", 0)
    return ("Block", 3 * step)


# ═════════════ Этап 6: трансформации и Пути (docs/design/06; Core/TransformationRules.cs) ═════════════

FORMS = ["None", "Righteous", "Vampire", "Werewolf", "DarkPact", "Herald", "Lich", "Clockwork"]
PATHS = ["None", "Paladin", "BlackKnight", "Champion", "Barbarian", "Monk", "Assassin", "Blightbringer",
         "MysticArcher", "ElementLord", "Battlemage", "Reflector", "Medium", "Reborn"]
CARD_ENCHANTS = ["Flaming", "Frost", "Storm", "Lightened", "Breathing", "Double"]


def is_worldview(kind: str) -> bool:
    return kind not in ("None", "Clockwork")


def is_apostate_form(kind: str) -> bool:
    return kind in ("Vampire", "Werewolf", "DarkPact", "Herald", "Lich")


def is_chain_step(frm: str, to: str) -> bool:
    return (frm, to) in (("DarkPact", "Herald"), ("Herald", "Lich"))


def can_take_worldview(current: str, nxt: str, righteous_piety: bool, ordinator: bool, clockwork: bool) -> bool:
    if not is_worldview(nxt):
        return False
    if (ordinator or clockwork) and nxt != "Righteous":
        return False
    if nxt in ("Herald", "Lich"):
        return is_chain_step(current, nxt)
    if current != "None":
        return False
    return nxt != "Righteous" or righteous_piety


def can_take_clockwork(worldview: str, has_clockwork: bool) -> bool:
    return not has_clockwork and worldview in ("None", "Righteous")


# Праведник
def halo_triggers(hp: int, max_hp: int, used: bool) -> bool:
    return not used and max_hp > 0 and hp > 0 and hp * 100 < max_hp * 50


def righteous_step_after_act(step: int, vow_kept: bool) -> int:
    return min(2, step + 1) if vow_kept else step


# Дитя Ночи: (Regen, FireRes, AttackBonus, FireLightMult, HealPercent, Lifesteal, OnlyFeeding)
def thirst_row(thirst: int) -> tuple:
    t = max(0, min(4, thirst))
    if t == 0:
        return (1, -1, 0, 1.0, 0, 0, False)
    if t == 1:
        return (0, -1, 0, 1.0, 0, 0, False)
    if t in (2, 3):
        return (0, 0, 2, 1.5, -50, 0, False)
    return (0, 0, 4, 2.0, -100, 25, True)


def thirst_after_combat(thirst: int, fed: bool) -> int:
    return 0 if fed else min(4, max(0, thirst) + 1)


def lifesteal(dealt: int, percent: int) -> int:
    return max(0, dealt) * max(0, percent) // 100


# Носитель Зверя
def moon_call_duration(stalker: int) -> int:
    return 3 + (1 if stalker >= 1 else 0) + (1 if stalker >= 3 else 0)


def hits_per_extension(stalker: int) -> int:
    return 2 if stalker >= 2 else 3


def extension_turns(before: int, after: int, per: int) -> int:
    return 0 if per <= 0 else max(0, after // per - before // per)


def beast_attack_bonus(hunter: int) -> int:
    return 4 + (2 if hunter >= 1 else 0) + (2 if hunter >= 3 else 0)


def alpha_wolf_hp(alpha: int) -> int:
    return 0 if alpha <= 0 else {1: 8, 2: 12}.get(alpha, 16)


def beast_nodes_earned(transformations: int) -> int:
    return min(9, max(0, transformations) // 2)


# Договор → Вестник → Лич
def next_stance(stance: int) -> int:
    return (stance + 1) % 3


def lich_max_hp_loss(max_hp: int) -> int:
    return max(0, min(max_hp * 30 // 100, max(0, max_hp - 1)))


def can_become_lich(current: str, herald_wins: int) -> bool:
    return current == "Herald" and herald_wins >= 10


def phylactery_revive_hp(max_hp: int) -> int:
    return max(1, max_hp * 30 // 100)


# Лечение
def heal_percent(mods) -> int:
    return max(-100, sum(mods))


def apply_heal(amount: float, percent: int, blocked: bool) -> int:
    if blocked or amount <= 0:
        return 0
    return max(0, math.floor(amount * (100 + max(-100, percent)) / 100))


# События трансформаций
def is_late_act(act: int, total: int) -> bool:
    return act >= (3 if total >= 5 else max(1, total - 1))


def event_forms_for(act: int, total: int) -> list:
    out = []
    if act <= 1:
        out += ["Werewolf", "DarkPact"]
    if act in (1, 2):
        out += ["Righteous", "Clockwork"]
    if 1 <= act <= 3:
        out.append("Vampire")
    if is_late_act(act, total):
        out.append("Lich")
    return out


def unknown_room_event(act: int, total: int, roll: int, pick: int, seen_mask: int) -> str:
    if roll < 0 or roll >= 12:
        return "None"
    opts = [k for k in event_forms_for(act, total) if not seen_mask & (1 << FORMS.index(k))]
    return opts[abs(pick) % len(opts)] if opts else "None"


# Путь Героя
def path_offer_act_index(total: int) -> int:
    return 2 if total >= 5 else max(0, total - 2)


def path_offer_due(level: int, boss_act: int, total: int, has_path: bool) -> bool:
    return not has_path and level >= 8 and boss_act >= path_offer_act_index(total)


def path_allowed(path: str, worldview: str, apostate: bool, unarmed: bool) -> bool:
    if path == "None":
        return False
    if path == "Paladin":
        return worldview == "Righteous"
    if path == "BlackKnight":
        return apostate and worldview != "Righteous"
    if path == "Monk":
        return unarmed
    return True


def native_paths(hero, worldview: str, apostate: bool, unarmed: bool) -> list:
    out = []
    if worldview == "Righteous":
        out.append("Paladin")
    if apostate and worldview != "Righteous":
        out.append("BlackKnight")
    out += {"Ironclad": ["Champion", "Barbarian", "Battlemage"],
            "Silent": ["Assassin", "Blightbringer", "MysticArcher"],
            "Regent": ["ElementLord", "Battlemage", "Reflector", "Medium", "MysticArcher"]}.get(hero, [])
    if unarmed:
        out.append("Monk")
    return list(dict.fromkeys(out))


def offer_paths(hero, worldview, apostate, unarmed, nxt) -> list:
    native = native_paths(hero, worldview, apostate, unarmed)
    foreign = [p for p in PATHS[1:] if p not in native and path_allowed(p, worldview, apostate, unarmed)]
    res = []
    for _ in range(2):
        if not native:
            break
        res.append(native.pop(nxt(len(native))))
    while len(res) < 3 and (foreign or native):
        pool = foreign if foreign else native
        res.append(pool.pop(nxt(len(pool))))
    return res


def monk_unarmed_damage(level: int) -> int:
    return 4 + max(1, level) // 2


def barbarian_rage(hp: int, max_hp: int) -> bool:
    return max_hp > 0 and hp * 100 < max_hp * 50


def fear_turn(rnd: int) -> bool:
    return rnd > 0 and rnd % 2 == 0


def reflected(absorbed: int) -> int:
    return max(0, absorbed) * 50 // 100


def champion_unshakeable_stagger(stagger: int) -> int:
    return (max(0, stagger) + 1) // 2


# Чары карт (07 §6.2)
def can_enchant_card(enchant: str, is_attack: bool, double_perk: bool) -> bool:
    if enchant == "Lightened":
        return True
    if enchant == "Double":
        return double_perk and is_attack
    return is_attack


def encode_double(a: str, b: str) -> int:
    return CARD_ENCHANTS.index(a) * 10 + CARD_ENCHANTS.index(b)


def decode_double(code: int) -> tuple:
    return CARD_ENCHANTS[max(0, min(4, code // 10))], CARD_ENCHANTS[max(0, min(4, code % 10))]


def valid_double(a: str, b: str) -> bool:
    return a != b and "Double" not in (a, b)


def card_weight_after(weight: int, lightened: bool) -> int:
    return max(0, weight - 1) if lightened else weight


def stamina_after(cost: int, breathing: bool) -> int:
    return max(0, cost - 1) if breathing else cost


def run_score(score: int, rollbacks: int) -> int:
    return score if rollbacks <= 0 else math.floor(score * score_multiplier(rollbacks))


# ───────────── Этап 7: уровни «Реквием» (Core/RequiemLevelRules.cs) ─────────────

REQUIEM_MIN, REQUIEM_MAX = 0, 10
VETERAN_LEVEL, LEGEND_LEVEL, DAMNED_LEVEL = 5, 7, 10


def level_from_ascension(ascension: int) -> int:
    return max(1, min(REQUIEM_MAX, ascension + 1))


def resolve_level(config_override: int, ascension: int) -> int:
    return max(REQUIEM_MIN, min(REQUIEM_MAX, config_override)) if config_override >= 0 else level_from_ascension(ascension)


def level_enemy_hp(level: int) -> float:
    return 0.75 if level <= 0 else 1.0


def level_damage_taken(level: int) -> float:
    return 0.75 if level <= 0 else 1.25 if level >= LEGEND_LEVEL else 1.0


def level_damage_dealt(level: int) -> float:
    return 0.8 if level >= DAMNED_LEVEL else 1.0


def elite_affix_base(level: int, roll: int) -> int:
    return (3 if roll < 50 else 2) if level >= LEGEND_LEVEL else 1


def superbosses_mandatory(level: int) -> bool:
    return level >= DAMNED_LEVEL


def candles_allowed(level: int) -> bool:
    return level < DAMNED_LEVEL


# ───────────── Этап 7: пять актов (Core/ActRules.cs) ─────────────

FULL_RUN_ACTS, SHORT_RUN_ACTS = 5, 3
NEW_ACTS = ("FrozenBarrows", "ClockworkDepths", "BloodCourt", "StarTower")


def act_slots(short_run: bool) -> list:
    return [0, 2, 4] if short_run else [0, 1, 2, 3, 4]


def vanilla_tier(act_index: int, act_count: int) -> int:
    if act_count >= FULL_RUN_ACTS:
        return 0 if act_index <= 0 else 1 if act_index <= 2 else 2
    return max(0, min(2, act_index))


def slot_of(act_index: int, act_count: int) -> int:
    if act_count == SHORT_RUN_ACTS:
        return max(0, min(2, act_index)) * 2
    return max(0, min(FULL_RUN_ACTS, act_index))


def map_width(kind: str) -> int:
    return 9 if kind in NEW_ACTS else 7


def act_rooms(kind: str) -> int:
    return {"Foothills": 15, "FrozenBarrows": 14, "ClockworkDepths": 14, "Hive": 14, "BloodCourt": 13,
            "StarTower": 13, "Glory": 13, "NamelessLimit": 4}.get(kind, 15)


def enemy_hp_bonus_percent(kind: str, full_run: bool) -> int:
    if not full_run and kind in ("Hive", "Glory"):
        return 0
    return {"FrozenBarrows": 35, "ClockworkDepths": 35, "Hive": 5, "BloodCourt": 20, "StarTower": 20,
            "Glory": 15, "NamelessLimit": 25}.get(kind, 0)


def elite_affix_bonus(kind: str, full_run: bool) -> int:
    return 1 if full_run and kind == "Glory" else 0


ACT_RULE = {"Foothills": "Apprenticeship", "FrozenBarrows": "Frost", "ClockworkDepths": "Steam", "Hive": "AbyssRifts",
            "BloodCourt": "DayNight", "StarTower": "LeyLines", "Glory": "AbyssInvasion"}

FROST_CHILL, WARMTH_FIGHTS = 1, 2
MAX_CLAW_KEYS_PER_ACT, VAULT_GOLD = 2, 40
OVERHEAT_PERIOD, OVERHEAT_BURNING, VALVE_RELIEF_FIGHTS = 4, 3, 3
DAY_NIGHT_BLOCK, NIGHT_HP_BONUS, NIGHT_GOLD_BONUS = 3, 25, 15
LEY_LINE_MANA = 3
RARE_ELITE_CHANCE = 15


def darkness_visible_rows(night_sight: bool) -> int:
    return 4 if night_sight else 2


def hidden_by_darkness(node_row: int, current_row: int, night_sight: bool) -> bool:
    return node_row - current_row > darkness_visible_rows(night_sight)


def locked_vault_count(roll: int) -> int:
    return 2 + abs(roll % 2)


def valve_count(roll: int) -> int:
    return 3 + abs(roll % 2)


def ley_line_count(roll: int) -> int:
    return 2 + abs(roll % 2)


def is_overheat_round(rnd: int) -> bool:
    return rnd > 0 and rnd % OVERHEAT_PERIOD == 0


def is_overheat_warning_round(rnd: int) -> bool:
    return rnd > 0 and rnd % OVERHEAT_PERIOD == OVERHEAT_PERIOD - 1


def is_night(row: int) -> bool:
    return row >= 1 and (row - 1) // DAY_NIGHT_BLOCK % 2 == 1


def is_rift_trigger(rule: str, row: int) -> bool:
    if rule == "AbyssRifts":
        return row > 0 and row % 3 == 0
    if rule == "AbyssInvasion":
        return row > 0
    return False


def can_become_gate(rule: str, node_row: int, current_row: int, is_combat: bool, is_shop_or_rest: bool,
                    is_special: bool) -> bool:
    if node_row <= current_row or is_shop_or_rest or is_special:
        return False
    if rule == "AbyssRifts":
        return is_combat
    return rule == "AbyssInvasion"


def pick_index(roll: int, count: int) -> int:
    return -1 if count <= 0 else roll % count


def act_pick_distinct(n: int, count: int, rolls) -> list:
    pool = list(range(max(0, n)))
    res = []
    for roll in rolls:
        if len(res) >= count or not pool:
            break
        res.append(pool.pop(pick_index(roll, len(pool))))
    return sorted(res)


def roll_rare_elite(roll: int, already_met: bool) -> bool:
    return not already_met and roll < RARE_ELITE_CHANCE


SUPERBOSSES = ["BlackWanderer", "BarrowReaper", "DeepMasterSmith", "GatebreakerEnvoy", "BloodCourtLord",
               "FirstSummoner", "WorldEater", "NamelessShadow"]
SUPERBOSS_HP = {"BlackWanderer": 220, "BarrowReaper": 300, "DeepMasterSmith": 320, "GatebreakerEnvoy": 420,
                "BloodCourtLord": 450, "FirstSummoner": 430, "WorldEater": 600, "NamelessShadow": 700}
SUPERBOSS_FOR = {"Foothills": "BlackWanderer", "FrozenBarrows": "BarrowReaper", "ClockworkDepths": "DeepMasterSmith",
                 "Hive": "GatebreakerEnvoy", "BloodCourt": "BloodCourtLord", "StarTower": "FirstSummoner",
                 "Glory": "WorldEater"}
SUPERBOSSES_FOR_NAMELESS = 5
MAX_SUPERBOSS_CANDLES = 1


def count_superbosses(mask: int) -> int:
    return sum(1 for b in range(SUPERBOSSES.index("NamelessShadow")) if mask & (1 << b))


def nameless_unlocked(mask: int, full_run: bool) -> bool:
    return full_run and count_superbosses(mask) >= SUPERBOSSES_FOR_NAMELESS


def portal_mandatory(always_setting: bool, level: int) -> bool:
    return always_setting or superbosses_mandatory(level)


def encode_mark(act_index: int, col: int, row: int) -> int:
    return act_index * 10000 + col * 100 + row


def decode_mark(code: int) -> tuple:
    return code // 10000, code // 100 % 100, code % 100


# Силы актов (Powers/ActPowers.cs: ActPowerRules)
GHOSTLY_REDUCTION = 75
MIRROR_REFLECT_PERCENT = 50
MIRROR_PHYSICAL_MULTIPLIER = 1.5
FLIGHT_MULTIPLIER = 0.5
BINDING_PERCENT_PER_STACK, BINDING_MAX_STACKS = 15, 3


def binding_multiplier(stacks: int) -> float:
    return 1 + max(0, min(BINDING_MAX_STACKS, stacks)) * BINDING_PERCENT_PER_STACK / 100


def bypasses_ghostly(is_spell: bool, damage_type: str, silver: bool) -> bool:
    return is_spell or damage_type != "physical" or silver
