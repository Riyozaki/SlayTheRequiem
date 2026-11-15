"""Тексты переработки ванильных реликвий (docs/design/07 §7, M4 п. 4): бижутерия вместо Vajra, Anchor/Orichalcum
и Bronze Scales, подсказки веса и правки к ванильным реликвиям. Подключается в build_loc.py."""

# (класс, eng title, eng desc, rus title, rus desc, flavor eng, flavor rus) — снаряжение, к описанию добавляется WEIGHT_NOTE.
RELICS_EQUIPMENT = [
    ("RingOfFuryStrike", "Ring of Fury Strike", "Ring. Weapon attacks deal 1 more damage.",
     "Кольцо Ярого Удара", "Кольцо. Оружейные атаки наносят на 1 урона больше.",
     "A thunderbolt, filed down to fit a finger.", "Молния, подпиленная под палец."),
    ("RingOfIronSkin", "Ring of Iron Skin", "Ring. At the start of each turn, gain 3 Block while you have [gold]Plating[/gold].",
     "Кольцо Железной Кожи", "Кольцо. В начале каждого хода — 3 Блока, пока у вас есть [gold]Латы[/gold].",
     "Iron answers iron.", "Железо отвечает железу."),
    ("NecklaceOfThorns", "Necklace of Thorns", "Amulet. Start each combat with 2 [gold]Thorns[/gold].",
     "Ожерелье Колючих Шипов", "Амулет. Каждый бой начинается с 2 [gold]Шипов[/gold].",
     "Hug at your own risk.", "Обнимать на свой страх и риск."),
]

# Подсказки к ванильным реликвиям (UI/RequiemUi.Tip, static_hover_tips.json).
RELICS_TIPS = {
    "eng": {
        "RELIC_WEIGHT.title": "Weight",
        "RELIC_WEIGHT.description": "This relic adds {Weight} to your [gold]Burden[/gold].",
        "RELIC_REWORK_ENERGY.title": "Reworked",
        "RELIC_REWORK_ENERGY.description": "Energy relic: in Requiem it also costs −1 regeneration of your main resource — "
                                           "◆ for the Platebearer and the Silent, ✦ for the Regent.",
    },
    "rus": {
        "RELIC_WEIGHT.title": "Вес",
        "RELIC_WEIGHT.description": "Реликвия добавляет {Weight} к [gold]Грузу[/gold].",
        "RELIC_REWORK_ENERGY.title": "Переработана",
        "RELIC_REWORK_ENERGY.description": "Реликвия энергии: в Реквиеме она ещё и стоит −1 прироста основного ресурса — "
                                           "◆ у Латоносца и Безмолвной, ✦ у Регента.",
    },
}
