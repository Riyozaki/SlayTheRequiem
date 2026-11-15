#!/usr/bin/env python3
"""Сливает тексты контента мода (карты, силы, реликвии-снаряжение) в localization/{eng,rus}/*.json.

Ключи: SLAYTHEREQUIEM-<UPPER_SNAKE>.<поле>. Существующие ключи, которых нет здесь, сохраняются.
Запуск: python3 tools/loc/build_loc.py
"""
import json
import os
import re
import sys

sys.path.insert(0, os.path.dirname(__file__))
from loc_magic import ENCOUNTERS, MAGIC_POWERS, MAGIC_RELICS, MONSTERS, SETTINGS, SPELL_CARDS  # noqa: E402
from loc_stage3 import (STAGE3_CARDS, STAGE3_ENCOUNTERS, STAGE3_EQUIPMENT, STAGE3_MONSTERS, STAGE3_POTIONS, STAGE3_POWERS,  # noqa: E402
                        STAGE3_RELICS, STAGE3_REST_SITE, STAGE3_SETTINGS)
from loc_stage35 import STAGE35_CARDS, STAGE35_RELIC_EXTRA, STAGE35_RELICS, STAGE35_SETTINGS  # noqa: E402
from loc_stage36 import STAGE36_CARDS, STAGE36_REST_SITE, STAGE36_RELIC_EXTRA, STAGE36_SETTINGS  # noqa: E402
from loc_stage4 import STAGE4_ANCIENTS, STAGE4_EVENTS, STAGE4_RELICS, STAGE4_SETTINGS  # noqa: E402
from loc_stage5 import STAGE5_CARDS, STAGE5_POWERS, STAGE5_REST_SITE, STAGE5_RELICS  # noqa: E402
from loc_stage6 import (STAGE6_BADGES, STAGE6_CARDS, STAGE6_ENCHANTMENTS, STAGE6_EVENTS, STAGE6_POWERS,  # noqa: E402
                        STAGE6_EQUIPMENT, STAGE6_RELICS, STAGE6_REST_SITE, STAGE6_SETTINGS)
from loc_flavor import FLAVOR  # noqa: E402
from loc_pools import POOLS_CARDS, POOLS_POWERS  # noqa: E402
from loc_clans import CLANS_CARDS, CLANS_EQUIPMENT, CLANS_POTIONS, CLANS_POWERS, CLANS_RELICS, CLANS_SETTINGS  # noqa: E402
from loc_ui import UI_CHARACTERS, UI_KEYWORDS, UI_SETTINGS, UI_TIPS  # noqa: E402
from loc_stage7 import (STAGE7_ACTS, STAGE7_ENCOUNTERS, STAGE7_MONSTERS, STAGE7_POWERS, STAGE7_RELIC_EXTRA,  # noqa: E402
                        STAGE7_RELICS, STAGE7_REST_SITE, STAGE7_SETTINGS)

ROOT = os.path.join(os.path.dirname(__file__), "..", "..", "mod", "SlayTheRequiem", "localization")
PREFIX = "SLAYTHEREQUIEM-"


def snake(name: str) -> str:
    return re.sub(r"(?<=[a-z0-9])(?=[A-Z])", "_", name).upper()


# (класс, eng title, eng text, rus title, rus text)
CARDS = [
    ("ShieldBash", "Shield Bash", "Deal {Damage:diff()} damage.\nApply {StaggerPower:diff()} [gold]Stagger[/gold].",
     "Удар щитом", "Нанести {Damage:diff()} урона.\nНаложить {StaggerPower:diff()} [gold]Шаткости[/gold]."),
    ("CrushingBlow", "Crushing Blow", "Deal {Damage:diff()} damage.\nApply {SunderPower:diff()} [gold]Sunder[/gold].",
     "Сокрушающий удар", "Нанести {Damage:diff()} урона.\nНаложить {SunderPower:diff()} [gold]Раскола[/gold]."),
    ("ArmorSplitter", "Armor Splitter", "Deal {Damage:diff()} damage.\nApply {SunderPower:diff()} [gold]Sunder[/gold].",
     "Рассечь латы", "Нанести {Damage:diff()} урона.\nНаложить {SunderPower:diff()} [gold]Раскола[/gold]."),
    ("IceWind", "Ice Wind", "Deal {Damage:diff()} [blue]frost[/blue] damage to ALL enemies.\nApply {ChillPower:diff()} [gold]Chill[/gold] to ALL enemies.",
     "Ледяной ветер", "Нанести {Damage:diff()} урона [blue]холодом[/blue] ВСЕМ врагам.\nНаложить {ChillPower:diff()} [gold]Озноба[/gold] ВСЕМ врагам."),
    ("PiercingThrust", "Piercing Thrust", "Deal {Damage:diff()} damage.\nApply {BleedPower:diff()} [gold]Bleed[/gold].",
     "Пробивающий выпад", "Нанести {Damage:diff()} урона.\nНаложить {BleedPower:diff()} [gold]Кровотечения[/gold]."),
    ("FlatStrike", "Flat Strike", "Deal {Damage:diff()} damage.\nApply {StaggerPower:diff()} [gold]Stagger[/gold].",
     "Удар плашмя", "Нанести {Damage:diff()} урона.\nНаложить {StaggerPower:diff()} [gold]Шаткости[/gold]."),
    ("SweepingBlow", "Sweeping Blow", "Deal {Damage:diff()} damage to ALL enemies.\nApply {StaggerPower:diff()} [gold]Stagger[/gold] to ALL enemies.",
     "Размашистый удар", "Нанести {Damage:diff()} урона ВСЕМ врагам.\nНаложить {StaggerPower:diff()} [gold]Шаткости[/gold] ВСЕМ врагам."),
    ("FrostStrike", "Frost Strike", "Deal {Damage:diff()} [blue]frost[/blue] damage.\nApply {ChillPower:diff()} [gold]Chill[/gold].",
     "Морозный удар", "Нанести {Damage:diff()} урона [blue]холодом[/blue].\nНаложить {ChillPower:diff()} [gold]Озноба[/gold]."),
    ("AgainstAllOdds", "Against All Odds", "Costs no ◆.\nDeal {Damage:diff()} damage. Double if you are [gold]Tired[/gold].",
     "Вопреки всему", "Не тратит ◆.\nНанести {Damage:diff()} урона. Вдвое больше, если вы [gold]Устали[/gold]."),
    ("WrathHammer", "Wrath Hammer", "Deal {Damage:diff()} damage.\nApply {StaggerPower:diff()} [gold]Stagger[/gold] and {SunderPower:diff()} [gold]Sunder[/gold].",
     "Молот гнева", "Нанести {Damage:diff()} урона.\nНаложить {StaggerPower:diff()} [gold]Шаткости[/gold] и {SunderPower:diff()} [gold]Раскола[/gold]."),
    ("CatchBreath", "Catch Breath", "Gain {StaminaGain:diff()} ◆.",
     "Перевести дух", "Получить {StaminaGain:diff()} ◆."),
    ("RaiseShield", "Raise Shield", "Gain {Block:diff()} [gold]Block[/gold].\nIf you have [gold]Plating[/gold], gain {PlatingBonus:diff()} more.",
     "Поднять щит", "Получить {Block:diff()} [gold]Блока[/gold].\nЕсли у вас есть [gold]Латы[/gold] — ещё {PlatingBonus:diff()}."),
    ("SteadyStance", "Steady Stance", "Gain {Block:diff()} [gold]Block[/gold].\nGain {StaminaGain:diff()} ◆.",
     "Твёрдая стойка", "Получить {Block:diff()} [gold]Блока[/gold].\nПолучить {StaminaGain:diff()} ◆."),
    ("SmotherFlames", "Smother Flames", "Remove [gold]Burning[/gold] and [gold]Chill[/gold] from yourself.\nGain {Block:diff()} [gold]Block[/gold].",
     "Сбить пламя", "Снять с себя [gold]Горение[/gold] и [gold]Озноб[/gold].\nПолучить {Block:diff()} [gold]Блока[/gold]."),
    # BattleCry — Клич, текст в loc_stage5.SHOUT_CARDS (с пометкой о перезарядке).
    ("ChangeGrip", "Change Grip", "Your next Attack this turn costs no ◆ and deals {GripPower:diff()} more damage.",
     "Сменить хват", "Следующая атака в этом ходу не тратит ◆ и наносит на {GripPower:diff()} урона больше."),
    ("CloseRanks", "Close Ranks", "Gain {PlatingPower:diff()} [gold]Plating[/gold].",
     "Сомкнуть латы", "Получить {PlatingPower:diff()} [gold]Лат[/gold]."),
    ("Tempering", "Tempering", "At the start of your turn, regain {TemperingPower:diff()} more ◆.",
     "Закалка", "В начале хода восстанавливать на {TemperingPower:diff()} ◆ больше."),
    ("Frostblood", "Frostblood", "Gain [gold]Frost Resistance[/gold] {FrostResistancePower:diff()}.\nChill you apply is increased by {FrostbloodPower:diff()}. Chill on you wears off entirely.",
     "Кровь стужи", "Получить [gold]Сопротивление холоду[/gold] {FrostResistancePower:diff()}.\nВаш Озноб сильнее на {FrostbloodPower:diff()}. Озноб на вас спадает целиком."),
    ("Unbreakable", "Unbreakable", "Gain {PlatingPower:diff()} [gold]Plating[/gold].\nYou can no longer be [gold]Staggered[/gold].",
     "Непоколебимость", "Получить {PlatingPower:diff()} [gold]Лат[/gold].\nВас больше нельзя [gold]ошеломить[/gold]."),
]

# (класс, eng title, eng desc, rus title, rus desc) — {Amount} подставляется игрой.
POWERS = [
    ("FireResistancePower", "Fire Resistance", "Fire damage and [gold]Burning[/gold] reduced by tier {Amount} (I −25%, II −50%, III −75%, IV immune; negative — vulnerable).",
     "Сопротивление огню", "Урон огнём и [gold]Горение[/gold] снижены на ступень {Amount} (I −25 %, II −50 %, III −75 %, IV иммунитет; отрицательная — уязвимость)."),
    ("FrostResistancePower", "Frost Resistance", "Frost damage and [gold]Chill[/gold] reduced by tier {Amount}.",
     "Сопротивление холоду", "Урон холодом и [gold]Озноб[/gold] снижены на ступень {Amount}."),
    ("ShockResistancePower", "Shock Resistance", "Shock damage and [gold]Static[/gold] reduced by tier {Amount}.",
     "Сопротивление разряду", "Урон разрядом и [gold]Статика[/gold] снижены на ступень {Amount}."),
    ("PoisonResistancePower", "Poison Resistance", "Poison damage and [gold]Poison[/gold] reduced by tier {Amount}.",
     "Сопротивление яду", "Урон ядом и [gold]Яд[/gold] снижены на ступень {Amount}."),
    ("MagicResistancePower", "Magic Resistance", "Magic damage reduced by tier {Amount}.",
     "Сопротивление магии", "Урон магией снижен на ступень {Amount}."),
    ("EnemyStaminaPower", "Stamina", "Attacks spend ◆ (heavy attacks 2). Regains 1 ◆ per turn. Low ◆ weakens attacks; at 0 ◆ the enemy must [gold]Catch Breath[/gold] instead of attacking.",
     "Выносливость", "Атаки тратят ◆ (тяжёлые — 2). +1 ◆ за ход. Мало ◆ — атаки слабее; без ◆ враг вместо атаки [gold]переводит дух[/gold] (Блок 5, полная ◆)."),
    ("PoisePower", "Poise", "When [gold]Stagger[/gold] reaches {Amount}, loses its intent. Threshold rises by 50% each time.",
     "Стойкость", "Когда [gold]Шаткость[/gold] достигает {Amount}, теряет намерение. Порог растёт на 50 % после каждого раза."),
    ("UnshakeablePower", "Unshakeable", "Cannot be Staggered. [gold]Stagger[/gold] {Amount} causes [gold]Break[/gold] instead.",
     "Непоколебим", "Не ошеломляется. [gold]Шаткость[/gold] {Amount} вызывает [gold]Слом[/gold]."),
    ("StaggerPower", "Stagger", "Accumulated imbalance: {Amount}. Compared against [gold]Poise[/gold].",
     "Шаткость", "Накопленная неустойчивость: {Amount}. Сравнивается со [gold]Стойкостью[/gold]."),
    ("BrokenPower", "Broken", "Takes 25% more damage until the end of its next turn.",
     "Сломлен", "Получает на 25 % больше урона до конца своего следующего хода."),
    ("FrozenImmunityPower", "Thawing", "Cannot be [gold]Frozen[/gold] for {Amount} turns.",
     "Оттаивание", "Не может быть [gold]скован льдом[/gold] ещё {Amount} ход(а)."),
    ("ConcussedPower", "Concussed", "Has {Amount} less energy next turn.",
     "Оглушение", "В следующем ходу на {Amount} энергии меньше."),
    ("GripPower", "Grip", "Next Attack this turn costs no ◆ and deals {Amount} more damage.",
     "Хват", "Следующая атака в этом ходу бесплатна по ◆ и наносит +{Amount} урона."),
    ("TemperingPower", "Tempering", "Regain {Amount} more ◆ each turn.",
     "Закалка", "Восстанавливать на {Amount} ◆ больше каждый ход."),
    ("FrostbloodPower", "Frostblood", "Chill you apply +{Amount}. Chill on you wears off entirely.",
     "Кровь стужи", "Накладываемый вами Озноб +{Amount}. Озноб на вас спадает целиком."),
    ("UnbreakablePower", "Unbreakable", "Cannot receive [gold]Stagger[/gold].",
     "Непоколебимость", "На вас не накладывается [gold]Шаткость[/gold]."),
    ("DodgePower", "Dodge", "{Amount} times per combat, negate an enemy attack by spending 2 ◆.",
     "Уворот", "{Amount} раз(а) за бой погасить атаку врага ценой 2 ◆."),
    ("WardPower", "Ward", "The first {Amount} HP damage each turn is paid with ✦ instead.",
     "Покров", "Первые {Amount} урона по ♥ за ход оплачиваются ✦."),
]

# (класс, eng title, eng desc, rus title, rus desc, flavor eng, flavor rus)
RELICS = [
    ("RustyCleaver", "Rusty Cleaver", "Weapon (sword I). Weapon attacks +1 damage. [gold]Sharp[/gold]: every 3rd weapon attack deals ×1.5.",
     "Ржавый тесак", "Оружие (меч I). Оружейные атаки +1 урона. [gold]Острый[/gold]: каждая 3-я оружейная атака ×1.5.", "Still sharp where it counts.", "Острый там, где надо."),
    ("SmugglerDagger", "Smuggler's Dagger", "Weapon (dagger I). Weapon attacks cost 1 less ◆. [gold]Sneaky[/gold]: ×1.5 vs enemies not intending to attack, Bleed 1; sneaky hits refund 1 ◆.",
     "Кинжал контрабандиста", "Оружие (кинжал I). Оружейные атаки на 1 ◆ дешевле. [gold]Подлый[/gold]: ×1.5 по врагу без атакующего намерения, Кровотечение 1; подлый удар возвращает 1 ◆.", "Never declared at customs.", "На таможне не предъявлялся."),
    ("WoodcutterAxe", "Woodcutter's Axe", "Weapon (axe I). Weapon attacks +1 damage. [gold]Chopping[/gold]: +2 vs enemies below 50% HP. Sells for more.",
     "Лесорубный топор", "Оружие (топор I). Оружейные атаки +1 урона. [gold]Рубящий[/gold]: +2 по врагу с ♥ ниже 50 %. Продаётся дороже.", "A tool first, a weapon second.", "Сначала инструмент, потом оружие."),
    ("IronMace", "Iron Mace", "Weapon (mace I). Weapon attacks +1 damage. [gold]Crushing[/gold]: Sunder 1 per hit.",
     "Железная булава", "Оружие (булава I). Оружейные атаки +1 урона. [gold]Дробящий[/gold]: Раскол 1 за удар.", "Armor is just a suggestion.", "Латы — всего лишь пожелание."),
    ("SteelWarhammer", "Steel Warhammer", "Two-handed weapon (II). Weapon attacks +4 damage and cost 1 more ◆. Crushing ×2, Stagger 2 per hit.",
     "Стальной боевой молот", "Двуручное оружие (II). Оружейные атаки +4 урона и на 1 ◆ дороже. Дробящий ×2, Шаткость 2 за удар.", "Swing once. Make it count.", "Один замах — один итог."),
    ("SilverSaber", "Silver Saber", "Weapon (sword III). Weapon attacks +3 damage, +2 more vs undead and spirits. Sharp.",
     "Серебряная сабля", "Оружие (меч III). Оружейные атаки +3 урона, ещё +2 по нежити и призракам. Острый.", "The dead remember silver.", "Мёртвые помнят серебро."),
    ("EverfrostHammer", "Everfrost Hammer", "Weapon (mace V). Weapon attacks +4 damage. Crushing ×2; each hit applies Chill 2.",
     "Молот из вечного льда", "Оружие (булава V). Оружейные атаки +4 урона. Дробящий ×2; каждый удар — Озноб 2.", "It never thaws.", "Он никогда не тает."),
    ("OakShield", "Oak Shield", "Off-hand shield. Block from cards +1.",
     "Дубовый щит", "Щит (левая рука). Блок от карт +1.", "Heavy, honest wood.", "Тяжёлое честное дерево."),
    ("MilitiaHelm", "Militia Helm", "Heavy armor (I). Set [gold]Militia Iron[/gold]: 2 — Plating +1; 4 — 5 Block at combat start.",
     "Шлем ополченца", "Тяжёлая броня (I). Комплект [gold]Железо ополченца[/gold]: 2 — Латы +1; 4 — 5 Блока в начале боя.", "", ""),
    ("MilitiaCuirass", "Militia Cuirass", "Heavy armor (I). Set [gold]Militia Iron[/gold]: 2 — Plating +1; 4 — 5 Block at combat start.",
     "Кираса ополченца", "Тяжёлая броня (I). Комплект [gold]Железо ополченца[/gold]: 2 — Латы +1; 4 — 5 Блока в начале боя.", "", ""),
    ("MilitiaGauntlets", "Militia Gauntlets", "Heavy armor (I). Set [gold]Militia Iron[/gold]: 2 — Plating +1; 4 — 5 Block at combat start.",
     "Рукавицы ополченца", "Тяжёлая броня (I). Комплект [gold]Железо ополченца[/gold]: 2 — Латы +1; 4 — 5 Блока в начале боя.", "", ""),
    ("MilitiaGreaves", "Militia Greaves", "Heavy armor (I). Set [gold]Militia Iron[/gold]: 2 — Plating +1; 4 — 5 Block at combat start.",
     "Поножи ополченца", "Тяжёлая броня (I). Комплект [gold]Железо ополченца[/gold]: 2 — Латы +1; 4 — 5 Блока в начале боя.", "", ""),
    ("StalkerHood", "Stalker Hood", "Light armor (I). Set [gold]Stalker Hides[/gold]: 2 — +1 max ◆, Dodge 1; 4 — first attack each combat +3.",
     "Капюшон загонщика", "Лёгкая броня (I). Комплект [gold]Шкуры загонщика[/gold]: 2 — +1 пул ◆, Уворот 1; 4 — первая атака боя +3.", "", ""),
    ("StalkerJerkin", "Stalker Jerkin", "Light armor (I). Set [gold]Stalker Hides[/gold]: 2 — +1 max ◆, Dodge 1; 4 — first attack each combat +3.",
     "Куртка загонщика", "Лёгкая броня (I). Комплект [gold]Шкуры загонщика[/gold]: 2 — +1 пул ◆, Уворот 1; 4 — первая атака боя +3.", "", ""),
    ("StalkerGloves", "Stalker Gloves", "Light armor (I). Set [gold]Stalker Hides[/gold]: 2 — +1 max ◆, Dodge 1; 4 — first attack each combat +3.",
     "Перчатки загонщика", "Лёгкая броня (I). Комплект [gold]Шкуры загонщика[/gold]: 2 — +1 пул ◆, Уворот 1; 4 — первая атака боя +3.", "", ""),
    ("StalkerBoots", "Stalker Boots", "Light armor (I). Set [gold]Stalker Hides[/gold]: 2 — +1 max ◆, Dodge 1; 4 — first attack each combat +3.",
     "Сапоги загонщика", "Лёгкая броня (I). Комплект [gold]Шкуры загонщика[/gold]: 2 — +1 пул ◆, Уворот 1; 4 — первая атака боя +3.", "", ""),
    ("StarAcademyRobe", "Star Academy Robe", "Robe (body). [gold]Ward[/gold] 4: the first 4 HP damage each turn is paid with ✦.",
     "Одеяние Звёздной Академии", "Одеяние (тело). [gold]Покров[/gold] 4: первые 4 урона по ♥ за ход оплачиваются ✦.", "Woven with starlight and debt.", "Соткано из звёздного света и долгов."),
    ("FirstBlizzardCloak", "Cloak of the First Blizzard", "Cloak. Frost Resistance I.",
     "Плащ Первой Метели", "Плащ. Сопротивление холоду I.", "Cold follows the wearer.", "Холод следует за владельцем."),
    ("RingOfStamina", "Ring of Stamina", "Ring. Start each combat with 2 more ◆ (up to max).",
     "Кольцо выносливости", "Кольцо. В начале боя +2 ◆ (не выше пула).", "", ""),
    ("RingOfMagicka", "Ring of Magicka", "Ring. Gain 1 ✦ at the start of each turn after the first.",
     "Кольцо магии", "Кольцо. +1 ✦ в начале каждого хода, кроме первого.", "", ""),
]

WEIGHT_NOTE = {"eng": "\nShown number — weight; 0 — stowed in the backpack.", "rus": "\nЧисло на иконке — вес; 0 — снято (в рюкзаке)."}


def merge(lang: str, file: str, entries: dict) -> int:
    path = os.path.join(ROOT, lang, file)
    data = {}
    if os.path.exists(path):
        with open(path, encoding="utf-8") as f:
            data = json.load(f)
    data.update(entries)
    with open(path, "w", encoding="utf-8") as f:
        json.dump(data, f, ensure_ascii=False, indent=2)
        f.write("\n")
    return len(entries)


def check_unique() -> None:
    """Один класс — один текст. Повтор с другим текстом молча перетирал первый
    (так «Удавка» Безмолвной потеряла строку «Подлый»). Одинаковые повторы (Клич в таблице Кличей) допустимы."""
    tables = {
        "cards": CARDS + SPELL_CARDS + STAGE3_CARDS + STAGE35_CARDS + STAGE36_CARDS + STAGE5_CARDS + STAGE6_CARDS + POOLS_CARDS + CLANS_CARDS,
        "powers": POWERS + MAGIC_POWERS + STAGE3_POWERS + STAGE5_POWERS + STAGE6_POWERS + STAGE7_POWERS + POOLS_POWERS + CLANS_POWERS,
        "relics": RELICS + STAGE3_EQUIPMENT + STAGE6_EQUIPMENT + CLANS_EQUIPMENT + MAGIC_RELICS + STAGE3_RELICS + STAGE35_RELICS
                  + STAGE4_RELICS + STAGE5_RELICS + STAGE6_RELICS + STAGE7_RELICS + CLANS_RELICS,
    }
    dupes = []
    for name, rows in tables.items():
        seen = {}
        for row in rows:
            if row[0] in seen and seen[row[0]] != row[1:]:
                dupes.append(f"{name}: {row[0]}")
            seen[row[0]] = row[1:]
    if dupes:
        sys.exit("Повторяющиеся классы в таблицах текстов:\n  " + "\n  ".join(dupes))


def main() -> None:
    check_unique()
    for li, lang in ((0, "eng"), (1, "rus")):
        cards, powers, relics = {}, {}, {}
        for cls, et, ed, rt, rd in CARDS + SPELL_CARDS + STAGE3_CARDS + STAGE35_CARDS + STAGE36_CARDS + STAGE5_CARDS + STAGE6_CARDS + POOLS_CARDS + CLANS_CARDS:
            k = PREFIX + snake(cls)
            cards[f"{k}.title"] = (et, rt)[li]
            cards[f"{k}.description"] = (ed, rd)[li]
        for cls, et, ed, rt, rd in POWERS + MAGIC_POWERS + STAGE3_POWERS + STAGE5_POWERS + STAGE6_POWERS + STAGE7_POWERS + POOLS_POWERS + CLANS_POWERS:
            k = PREFIX + snake(cls)
            powers[f"{k}.title"] = (et, rt)[li]
            powers[f"{k}.description"] = (ed, rd)[li]
            powers[f"{k}.smartDescription"] = (ed, rd)[li]
        for cls, et, ed, rt, rd, fe, fr in RELICS + STAGE3_EQUIPMENT + STAGE6_EQUIPMENT + CLANS_EQUIPMENT:
            k = PREFIX + snake(cls)
            relics[f"{k}.title"] = (et, rt)[li]
            relics[f"{k}.description"] = (ed, rd)[li] + WEIGHT_NOTE[lang]
            relics[f"{k}.flavor"] = (fe, fr)[li]
        for cls, et, ed, rt, rd, fe, fr in MAGIC_RELICS + STAGE3_RELICS + STAGE35_RELICS + STAGE4_RELICS + STAGE5_RELICS + STAGE6_RELICS + STAGE7_RELICS + CLANS_RELICS:
            k = PREFIX + snake(cls)
            relics[f"{k}.title"] = (et, rt)[li]
            relics[f"{k}.description"] = (ed, rd)[li]
            relics[f"{k}.flavor"] = (fe, fr)[li]
        relics.update({f"{PREFIX}{key}": text for key, text in {**STAGE35_RELIC_EXTRA[lang], **STAGE36_RELIC_EXTRA[lang], **STAGE7_RELIC_EXTRA[lang]}.items()})
        for key, texts in FLAVOR.items():  # цитаты для реликвий без своей (этап 8)
            if not relics.get(f"{PREFIX}{key}.flavor"):
                relics[f"{PREFIX}{key}.flavor"] = texts[li]
        monsters = {}
        for key, langs in {**MONSTERS, **STAGE3_MONSTERS, **STAGE7_MONSTERS}.items():
            data = langs[lang]
            monsters[f"{PREFIX}{key}.name"] = data["name"]
            for move, title in data["moves"].items():
                monsters[f"{PREFIX}{key}.moves.{move}.title"] = title
        encounters = {f"{PREFIX}{key}.title": names[lang] for key, names in {**ENCOUNTERS, **STAGE3_ENCOUNTERS, **STAGE7_ENCOUNTERS}.items()}
        settings = {f"{PREFIX}{key}": text for key, text in {**SETTINGS[lang], **STAGE3_SETTINGS[lang], **STAGE35_SETTINGS[lang], **STAGE36_SETTINGS[lang], **STAGE4_SETTINGS[lang], **STAGE6_SETTINGS[lang], **STAGE7_SETTINGS[lang], **UI_SETTINGS[lang], **CLANS_SETTINGS[lang]}.items()}
        potions = {}
        for cls, et, ed, rt, rd in STAGE3_POTIONS + CLANS_POTIONS:
            k = PREFIX + snake(cls)
            potions[f"{k}.title"] = (et, rt)[li]
            potions[f"{k}.description"] = (ed, rd)[li]
        n = merge(lang, "monsters.json", monsters) + merge(lang, "encounters.json", encounters) + merge(lang, "settings_ui.json", settings)
        n += merge(lang, "cards.json", cards) + merge(lang, "powers.json", powers) + merge(lang, "relics.json", relics)
        n += merge(lang, "potions.json", potions) + merge(lang, "rest_site_ui.json", {**STAGE3_REST_SITE[lang], **STAGE36_REST_SITE[lang], **STAGE5_REST_SITE[lang], **STAGE6_REST_SITE[lang], **STAGE7_REST_SITE[lang]})
        n += merge(lang, "events.json", {**STAGE4_EVENTS[lang], **STAGE6_EVENTS[lang]}) + merge(lang, "ancients.json", STAGE4_ANCIENTS[lang])
        n += merge(lang, "enchantments.json", STAGE6_ENCHANTMENTS[lang]) + merge(lang, "badges.json", STAGE6_BADGES[lang])
        n += merge(lang, "acts.json", {f"{PREFIX}{key}": text for key, text in STAGE7_ACTS[lang].items()})
        # M1, интерфейс: ключевые слова-подсказки, подписи шкал, имя Латоносца.
        n += merge(lang, "card_keywords.json", {f"{PREFIX}{key}": text for key, text in UI_KEYWORDS[lang].items()})
        n += merge(lang, "static_hover_tips.json", {f"{PREFIX}{key}": text for key, text in UI_TIPS[lang].items()})
        n += merge(lang, "characters.json", {f"{PREFIX}{key}": text for key, text in UI_CHARACTERS[lang].items()})
        print(f"{lang}: {n} ключей")


if __name__ == "__main__":
    main()
