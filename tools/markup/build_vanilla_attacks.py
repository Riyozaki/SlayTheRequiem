#!/usr/bin/env python3
"""
Ручная разметка ванильных атак StS2 для мода Slay the Requiem.

Источник фактов о картах (ID, энергия, урон, редкость): открытая база
https://github.com/vitechliu/sts2_database (MIT, game_version 0.107.1).
Решения мода (◆, класс удара, тип урона, тег Оружейная) — в таблице MARKUP ниже,
каждая строка просмотрена вручную. Скрипт только сводит их с базой и пишет CSV,
который мод читает при загрузке.

Использование:
    git clone https://github.com/vitechliu/sts2_database /tmp/stsdb     # полный клон: --depth 1 отдаёт неполную базу
    git -C /tmp/stsdb checkout 2a26a96fa4d4808c2532291f70163af2b0361beb  # закреплённая версия (как в CI)
    python3 tools/markup/build_vanilla_attacks.py /tmp/stsdb mod/SlayTheRequiem/data/vanilla_attacks_stamina.csv

Скрипт падает, если в базе есть атака из нашего пула без разметки
(или разметка ссылается на несуществующую карту) — так новые карты
после патчей EA не проскочат незамеченными.
"""
from __future__ import annotations

import csv
import glob
import json
import os
import sys

# Пулы, которые размечаем. Некробиндер и Дефект в моде — только облики трансформаций,
# их карты в режиме «Реквием» не генерируются (docs/design/06 §4).
POOLS = {"Ironclad", "Silent", "Regent", "Colorless", "Event", "Token"}
EXCLUDED_KEYS = {"SweepingGaze"}  # токен Ости (Некробиндер)

# Класс удара: L — лёгкий, N — обычный, H — тяжёлый.
# Тип урона: phys, fire, frost, shock, magic, light.
# ◆: число или "X" (= X энергии, потраченной на карту).
# weapon: Y — карта получает бонус оружия (тег «Оружейная», docs/design/07 §3.1).
#
# Правила, от которых отталкивались (docs/design/02 §1):
#   0⚡ лёгкий тычок ≤ 8 урона → 0–1; 1⚡ → 1; 2⚡ → 2; 3⚡ → 3;
#   «тяжёлые» (32+ урона одним ударом, финишеры) → +1;
#   многоударные с 3+ ударами → +1, если удары не мелкие;
#   карты, которые и так платят ♥ (Hemokinesis, Breakthrough), не дорожают;
#   ядро архетипов (Шивы, Неутрализация, Клинок Суверена) не душим.
#                key                 ◆    класс тип     weapon  заметка
MARKUP: dict[str, tuple] = {
    # ---------------- Ironclad → Латоносец ----------------
    "StrikeIronclad":   (1,   "N", "phys",  "Y", "базовый удар"),
    "Bash":             (2,   "H", "phys",  "Y", "базовый тяжёлый удар"),
    "Anger":            (1,   "L", "phys",  "Y", "копирует себя: ◆ ограничивает бесконечные Anger-комбо"),
    "AshenStrike":      (1,   "N", "fire",  "Y", "огненная тематика"),
    "Bludgeon":         (4,   "H", "phys",  "Y", "32 урона одним ударом — половина пула ◆ Латоносца"),
    "BodySlam":         (1,   "N", "phys",  "N", "синергия с Блоком/Латами, не душим; урон не от оружия"),
    "Break":            (2,   "H", "phys",  "Y", "Древний: 20 урона за 1⚡"),
    "Breakthrough":     (1,   "N", "phys",  "Y", "уже стоит ♥"),
    "Bully":            (1,   "L", "phys",  "Y", ""),
    "Cinder":           (2,   "N", "fire",  "Y", ""),
    "Conflagration":    (2,   "N", "fire",  "N", "4 удара по всем"),
    "Dismantle":        (1,   "N", "phys",  "Y", ""),
    "Feed":             (1,   "N", "phys",  "Y", ""),
    "FiendFire":        (3,   "H", "fire",  "N", "финишер, сжигает руку"),
    "FightMe":          (2,   "N", "phys",  "Y", ""),
    "Grapple":          (1,   "N", "phys",  "N", "захват — без оружия"),
    "Headbutt":         (1,   "N", "phys",  "N", "удар головой — без оружия"),
    "Hemokinesis":      (1,   "N", "magic", "N", "магия крови, уже стоит ♥"),
    "HowlFromBeyond":   (3,   "H", "magic", "N", "может разыгрываться автоматически: при нехватке ◆ — надрыв, не блок"),
    "IronWave":         (1,   "N", "phys",  "Y", ""),
    "Mangle":           (3,   "H", "phys",  "Y", ""),
    "MoltenFist":       (1,   "N", "fire",  "N", "кулак — без оружия"),
    "PactsEnd":         (1,   "N", "magic", "N", "условная карта, не дорожает"),
    "PerfectedStrike":  (2,   "N", "phys",  "Y", ""),
    "Pillage":          (1,   "N", "phys",  "Y", ""),
    "PommelStrike":     (1,   "N", "phys",  "Y", ""),
    "Rampage":          (1,   "N", "phys",  "Y", ""),
    "SetupStrike":      (1,   "N", "phys",  "Y", ""),
    "Spite":            (1,   "L", "phys",  "Y", ""),
    "Stomp":            (3,   "H", "phys",  "N", "топот по всем"),
    "SwordBoomerang":   (1,   "L", "phys",  "Y", "3 мелких удара"),
    "TearAsunder":      (2,   "N", "phys",  "Y", ""),
    "Thrash":           (1,   "N", "phys",  "Y", ""),
    "Thunderclap":      (1,   "N", "shock", "N", "громовой хлопок"),
    "TwinStrike":       (1,   "N", "phys",  "Y", ""),
    "Unrelenting":      (2,   "N", "phys",  "Y", ""),
    "Uppercut":         (2,   "N", "phys",  "N", "апперкот — без оружия"),
    "Whirlwind":        ("X", "N", "phys",  "Y", "◆ = X"),

    # ---------------- Silent → Безмолвная ----------------
    "StrikeSilent":     (1,   "N", "phys",  "Y", "базовый удар"),
    "Neutralize":       (0,   "L", "phys",  "Y", "базовый тычок, ядро класса"),
    "Assassinate":      (1,   "L", "phys",  "Y", ""),
    "Backstab":         (1,   "L", "phys",  "Y", ""),
    "DaggerSpray":      (1,   "L", "phys",  "Y", ""),
    "DaggerThrow":      (1,   "L", "phys",  "Y", ""),
    "Dash":             (2,   "N", "phys",  "Y", ""),
    "EchoingSlash":     (2,   "N", "phys",  "Y", "повторяется за каждое убийство"),
    "Finisher":         (2,   "N", "phys",  "Y", "много ударов"),
    "Flechettes":       (1,   "L", "phys",  "N", "метательные"),
    "FlickFlack":       (1,   "N", "phys",  "Y", "Ловкость (Sly): при авто-розыгрыше ◆ не блокирует"),
    "FollowThrough":    (1,   "N", "phys",  "Y", ""),
    "GrandFinale":      (3,   "H", "phys",  "Y", "60 урона"),
    "LeadingStrike":    (1,   "N", "phys",  "Y", ""),
    "MementoMori":      (1,   "N", "phys",  "Y", ""),
    "Murder":           (3,   "H", "phys",  "Y", ""),
    "Pinpoint":         (2,   "N", "phys",  "Y", "дешевеет по энергии — ◆ ставим средним"),
    "PoisonedStab":     (1,   "N", "phys",  "Y", "яд — статус, удар клинка физический (иначе нежить, иммунная к яду, не получала бы урона)"),
    "Pounce":           (2,   "N", "phys",  "Y", ""),
    "PreciseCut":       (1,   "L", "phys",  "Y", ""),
    "Predator":         (2,   "N", "phys",  "Y", ""),
    "Ricochet":         (2,   "N", "phys",  "N", "4 рикошета"),
    "Skewer":           ("X", "N", "phys",  "Y", "◆ = X"),
    "Slice":            (1,   "L", "phys",  "Y", ""),
    "Strangle":         (1,   "N", "phys",  "N", "удушение — без оружия"),
    "SuckerPunch":      (1,   "N", "phys",  "N", "кулак"),
    "Suppress":         (1,   "L", "phys",  "Y", "Древний"),
    "TheHunt":          (1,   "N", "phys",  "Y", ""),

    # ---------------- Regent (звёзды = мана ✦) ----------------
    # Атаки Регента с ценой в звёздах автоматически становятся Заклинаниями (цена ✦ = цена в звёздах).
    # Регент — маг: пул ◆ 5, прирост 2, поэтому «звёздные» атаки стоят 0–1 ◆, а физические — как у всех.
    "StrikeRegent":     (1,   "N", "phys",  "Y", "базовый удар"),
    "FallingStar":      (0,   "L", "light", "N", "базовая звёздная карта: платит ✦, не ◆"),
    "AstralPulse":      (1,   "N", "light", "N", ""),
    "BeatIntoShape":    (1,   "N", "phys",  "Y", "Ковка"),
    "Bombardment":      (2,   "H", "light", "N", "авто-розыгрыш из сброса: дешевле по ◆, чтобы не надрываться"),
    "CelestialMight":   (2,   "N", "light", "N", ""),
    "CollisionCourse":  (1,   "N", "phys",  "N", ""),
    "Comet":            (1,   "H", "light", "N", "основная цена — в ✦"),
    "CrashLanding":     (2,   "H", "light", "N", ""),
    "CrescentSpear":    (1,   "N", "phys",  "Y", ""),
    "CrushUnder":       (1,   "N", "phys",  "N", ""),
    "Devastate":        (2,   "H", "light", "N", "30 урона за 1⚡"),
    "DyingStar":        (1,   "N", "light", "N", ""),
    "GammaBlast":       (1,   "N", "light", "N", ""),
    "GuidingStar":      (1,   "N", "light", "N", ""),
    "HeavenlyDrill":    ("X", "N", "light", "N", "◆ = X"),
    "Hegemony":         (2,   "N", "phys",  "Y", ""),
    "HeirloomHammer":   (2,   "H", "phys",  "Y", ""),
    "KinglyKick":       (3,   "H", "phys",  "N", "пинок; энергия дешевеет, ◆ — нет"),
    "KinglyPunch":      (1,   "N", "phys",  "N", "кулак"),
    "KnockoutBlow":     (3,   "H", "phys",  "N", ""),
    "LunarBlast":       (1,   "N", "light", "N", ""),
    "MakeItSo":         (1,   "N", "light", "N", ""),
    "MeteorShower":     (1,   "H", "fire",  "N", "Древний; основная цена — в ✦"),
    "PhotonCut":        (1,   "N", "light", "Y", ""),
    "Radiate":          (1,   "N", "light", "N", ""),
    "SevenStars":       (3,   "H", "light", "N", "7 ударов по всем"),
    "ShiningStrike":    (1,   "N", "light", "Y", ""),
    "SolarStrike":      (1,   "N", "light", "Y", ""),
    "Stardust":         (1,   "N", "light", "N", ""),
    "Supermassive":     (1,   "N", "light", "N", ""),
    "WroughtInWar":     (1,   "N", "phys",  "Y", "Ковка"),

    # ---------------- Colorless ----------------
    "Bolas":            (0,   "L", "phys",  "N", "возвращается в руку каждый ход — 0, иначе налог"),
    "DramaticEntrance": (1,   "N", "phys",  "N", ""),
    "Fisticuffs":       (1,   "N", "phys",  "N", "кулаки"),
    "FlashOfSteel":     (0,   "L", "phys",  "Y", "кантрип"),
    "GangUp":           (1,   "N", "phys",  "Y", ""),
    "GoldAxe":          (1,   "N", "phys",  "Y", ""),
    "HandOfGreed":      (2,   "N", "phys",  "Y", ""),
    "Jackpot":          (3,   "H", "phys",  "N", ""),
    "Knockdown":        (2,   "H", "phys",  "N", ""),
    "MindBlast":        (1,   "N", "magic", "N", "кандидат в Заклинания (Иллюзия)"),
    "Omnislice":        (1,   "N", "phys",  "Y", ""),
    "Rend":             (2,   "N", "phys",  "Y", ""),
    "Salvo":            (1,   "N", "phys",  "N", ""),
    "SeekerStrike":     (1,   "N", "phys",  "Y", ""),
    "TagTeam":          (2,   "N", "phys",  "Y", "кооп"),
    "ThrummingHatchet": (1,   "N", "phys",  "Y", "возвращается в руку"),
    "UltimateStrike":   (2,   "H", "phys",  "Y", "14 за 1⚡"),
    "Volley":           ("X", "N", "phys",  "N", "◆ = X"),

    # ---------------- Event / Ancient ----------------
    "ByrdSwoop":        (1,   "N", "phys",  "N", ""),
    "Clash":            (1,   "N", "phys",  "Y", ""),
    "Exterminate":      (2,   "N", "phys",  "N", "4 удара по всем"),
    "Maul":             (1,   "N", "phys",  "N", ""),
    "NeowsFury":        (1,   "N", "magic", "N", "сила Древней — магия"),
    "Peck":             (1,   "L", "phys",  "N", ""),
    "Rebound":          (1,   "N", "phys",  "Y", ""),
    "RipAndTear":       (1,   "N", "phys",  "N", ""),
    "Squash":           (1,   "N", "phys",  "N", ""),
    "Whistle":          (4,   "H", "phys",  "N", "33 урона + Оглушение"),

    # ---------------- Tokens ----------------
    "Shiv":             (0,   "L", "phys",  "N", "ядро архетипа Шивов; ограничитель — энергия/генерация"),
    "GiantRock":        (2,   "H", "phys",  "N", "16 урона"),
    "MinionDiveBomb":   (0,   "L", "phys",  "N", "бьёт приспешник, не герой"),
    "MinionStrike":     (0,   "L", "phys",  "N", "бьёт приспешник, не герой"),
    "SovereignBlade":   (1,   "N", "light", "Y", "ядро Регента; дешёвый по ◆ — маг с малым пулом"),
}

FIELDS = ["key", "category", "name_eng", "energy", "rarity", "damage", "hits",
          "stamina", "strike_class", "damage_type", "weapon", "note"]


def damage_of(card: dict) -> tuple[str, str]:
    v = card.get("variables", {})
    dmg = str(v.get("Damage", "")).rstrip("m")
    hits = str(v.get("Repeat", "1" if dmg else ""))
    return dmg, hits


def main(db: str, out: str) -> int:
    attacks = {}
    for path in glob.glob(os.path.join(db, "cards", "*.json")):
        with open(path, encoding="utf-8") as f:
            data = json.load(f)
        card = data["card"]
        if card["type"] == "Attack" and card["category"] in POOLS and card["key"] not in EXCLUDED_KEYS:
            attacks[card["key"]] = (card, data.get("game_version", "?"))

    missing = sorted(set(attacks) - set(MARKUP))
    unknown = sorted(set(MARKUP) - set(attacks))
    if missing or unknown:
        print("Без разметки:", missing)
        print("Разметка без карты в базе:", unknown)
        return 1

    versions = {v for _, v in attacks.values()}
    rows = []
    for key in sorted(attacks, key=lambda k: (attacks[k][0]["category"], k)):
        card, _ = attacks[key]
        stamina, cls, dtype, weapon, note = MARKUP[key]
        dmg, hits = damage_of(card)
        energy = "X" if card["cost"] == -2 else card["cost"]
        rows.append({
            "key": key, "category": card["category"], "name_eng": card["name_eng"],
            "energy": energy, "rarity": card["rarity"], "damage": dmg, "hits": hits,
            "stamina": stamina, "strike_class": cls, "damage_type": dtype,
            "weapon": weapon, "note": note,
        })

    os.makedirs(os.path.dirname(out), exist_ok=True)
    with open(out, "w", encoding="utf-8", newline="") as f:
        f.write(f"# Slay the Requiem: stamina markup of vanilla attacks. Card facts from sts2_database, game {', '.join(sorted(versions))}\n")
        f.write("# Necrobinder and Defect attacks are intentionally not marked: in the mod they are transformation forms, "
                "not heroes (docs/design/06 §4)\n")
        w = csv.DictWriter(f, fieldnames=FIELDS, lineterminator="\n")
        w.writeheader()
        w.writerows(rows)
    print(f"OK: {len(rows)} атак → {out}")
    return 0


if __name__ == "__main__":
    if len(sys.argv) < 2:
        print(__doc__)
        sys.exit(2)
    root = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
    default_out = os.path.join(root, "mod", "SlayTheRequiem", "data", "vanilla_attacks_stamina.csv")
    sys.exit(main(sys.argv[1], sys.argv[2] if len(sys.argv) > 2 else default_out))
