"""Локализация M5 п. 5 (лагерь, docs/design/10): вьючный зверь, услуги прилавка для него, метапрогресс лагеря.

События «Брошенный мул» и «Засада» — в loc_m5events.py (общий формат событий актов).
"""

# ───────────── реликвии (7-кортежи: класс, eng name, eng desc, rus name, rus desc, eng flavor, rus flavor) ─────────────
M5_CAMP_RELICS = [
    ("PackMule", "Pack Mule",
     "+{Burden} Burden capacity (shod: +{Shod} more). Weightless. The camp chest is also open at any shop counter. "
     "Doesn't fight. Shoe it at a camp Forge or an Armorer.",
     "Вьючный мул",
     "+{Burden} к вместимости груза (подкованный — ещё +{Shod}). Ничего не весит. Сундук лагеря доступен и у прилавка любой лавки. "
     "В бою не участвует. Подковать — Кузня в лагере или Оружейник.",
     "Stubborn, patient, and smarter than the last three hirelings.", "Упрямый, терпеливый и умнее трёх последних наёмников."),
]

# ───────────── карты прилавка (5-кортежи) ─────────────
M5_CAMP_CARDS = [
    ("CounterBuyMule", "Buy a Pack Mule",
     "[gold]{Price}[/gold] gold. +{Amount} Burden; the camp chest becomes available at shop counters.",
     "Купить вьючного мула", "[gold]{Price}[/gold] золотых. +{Amount} груза; Сундук лагеря доступен у прилавков лавок."),
    ("CounterShoeMule", "Shoe the Mule",
     "[gold]{Price}[/gold] gold. Your Pack Mule carries +{Amount} more Burden.",
     "Подковать мула", "[gold]{Price}[/gold] золотых. Вьючный мул несёт ещё +{Amount} груза."),
    ("CounterMuleChest", "Mule's Chest",
     "Free. Open the camp chest carried by your mule ({Amount} stored): move cards and gear in or out.",
     "Сундук мула", "Бесплатно. Открыть Сундук, который везёт мул (внутри: {Amount}): убрать или забрать карты и вещи."),
]

# ───────────── настройки ─────────────
M5_CAMP_SETTINGS = {
    "eng": {
        "ENABLE_CAMP_META.title": "Camp meta-progression",
        "ENABLE_CAMP_META.hover.title": "Camp meta-progression",
        "ENABLE_CAMP_META.hover.desc": "Each won run unlocks the next camp building for all future runs: Kettle (cooking without the skill), "
                                       "Anvil (Forge costs 1 point), Enchanting Table and Altar (in every camp), Herbalist's Tent "
                                       "(brewing one tier higher). Only a bonus. Singleplayer only.",
        "CAMP_BUILDINGS_UNLOCKED.title": "Camp buildings unlocked",
    },
    "rus": {
        "ENABLE_CAMP_META.title": "Метапрогресс лагеря",
        "ENABLE_CAMP_META.hover.title": "Метапрогресс лагеря",
        "ENABLE_CAMP_META.hover.desc": "Каждая победа в забеге открывает следующую постройку лагеря для всех будущих забегов: Котёл "
                                       "(Готовка без навыка), Наковальня (Кузня за 1 очко), Зачаровальный стол и Алтарь (в каждом лагере), "
                                       "Палатка травника (Варка ступенью выше). Только бонус. Лишь в одиночной игре.",
        "CAMP_BUILDINGS_UNLOCKED.title": "Открыто построек лагеря",
    },
}
