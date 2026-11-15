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
    # ── кольца 07 §5.2 (M4 п. 3) ──
    ("RingOfQuickBreath", "Ring of Quick Breath", "Ring. +1 ◆ regeneration.",
     "Кольцо Скорого Дыхания", "Кольцо. +1 прирост ◆.",
     "In, out. In, out. Swing.", "Вдох, выдох. Вдох, выдох. Замах."),
    ("RingOfClearMind", "Ring of Clear Mind", "Ring. +1 ✦ regeneration.",
     "Кольцо Ясного Ума", "Кольцо. +1 прирост ✦.",
     "The stone hums when your thoughts wander.", "Камень гудит, когда мысли разбредаются."),
    ("RingOfTheSerpentSmile", "Ring of the Serpent's Smile", "Ring. Draw 1 more card at the start of each combat.",
     "Кольцо Змеиной Улыбки", "Кольцо. В первой руке боя на 1 карту больше.",
     "It always knows one card more than you.", "Оно всегда знает на одну карту больше."),
    ("RingOfThePyromancer", "Pyromancer's Ring", "Ring. Whenever you apply [gold]Burning[/gold], apply 2 more.",
     "Кольцо Пиромана", "Кольцо. Накладываемое вами [gold]Горение[/gold] +2.",
     "Warm to the touch. Then hot.", "Тёплое на ощупь. Потом горячее."),
    ("RingOfHoarfrost", "Ring of Hoarfrost", "Ring. Whenever you apply [gold]Chill[/gold], apply 1 more.",
     "Кольцо Изморози", "Кольцо. Накладываемый вами [gold]Озноб[/gold] +1.",
     "Frost blooms on every cup you hold.", "Иней расцветает на каждой кружке в вашей руке."),
    ("RingOfTheLightningRod", "Lightning Rod Ring", "Ring. Shock Resistance 2. Half of the shock damage you take returns as ✦.",
     "Кольцо Громоотвода", "Кольцо. Сопротивление разряду 2. Половина полученного урона разрядом возвращается ✦.",
     "Your hair stands on end. Your magicka too.", "Волосы дыбом. Магия тоже."),
    ("RingOfTheWitchHunter", "Witch Hunter's Ring", "Ring. Your attacks deal 4 more damage to enemies that intend to cast a spell.",
     "Кольцо Охотника на Ведьм", "Кольцо. Ваши атаки по врагу, который собирается колдовать, +4 урона.",
     "Silver, salt and a short temper.", "Серебро, соль и короткий нрав."),
    ("RingOfRegeneration", "Ring of Regeneration", "Ring. At the start of your turn, heal 1 HP while below half HP.",
     "Кольцо Регенерации", "Кольцо. В начале хода лечит 1 ♥, пока ♥ меньше половины.",
     "Slow, stubborn, alive.", "Медленно, упрямо, живо."),
    ("RingOfTheDrunkard", "Drunkard's Ring", "Ring. Requiem potions and weapon poisons are 50% stronger. −1 max ◆.",
     "Кольцо Пьяницы", "Кольцо. Зелья и яды Реквиема сильнее на 50 %. −1 пул ◆.",
     "Smells of mead. Always.", "Пахнет медовухой. Всегда."),
    ("RingOfTheFacelessThief", "Ring of the Faceless Thief", "Ring. You can steal in Requiem shops, one extra attempt per visit.",
     "Кольцо Безликого Вора", "Кольцо. Можно красть в лавках Реквиема, на одну попытку за визит больше.",
     "Nobody remembers who sold it to you.", "Никто не помнит, кто вам его продал."),
    ("RingOfTheSummoner", "Summoner's Ring", "Ring. +1 summon slot.",
     "Кольцо Призывателя", "Кольцо. +1 слот призыва.",
     "Two circles drawn in one.", "Два круга, вписанные в один."),
    ("RingOfTheViceroy", "Viceroy's Ring", "Ring. Once per combat, the first spell you play is played twice.",
     "Кольцо Наместника", "Кольцо. Раз в бой первое сыгранное заклинание срабатывает дважды.",
     "Orders are given twice. Spells too.", "Приказы отдают дважды. Заклинания тоже."),
    # ── амулеты 07 §5.1 ──
    ("AmuletOfTheHunter", "Amulet of the Huntsman", "Amulet. Your attacks deal 3 more damage to beasts. Beast Form lasts 1 more turn.",
     "Амулет Ловчего", "Амулет. Ваши атаки по зверям +3 урона. Обращение длится на 1 ход дольше.",
     "A fang on a cord, still warm.", "Клык на шнурке, ещё тёплый."),
    ("AmuletOfTheHearth", "Amulet of the Hearth Mother", "Amulet. After winning a combat, heal 4 HP.",
     "Амулет Матери Очага", "Амулет. После победы в бою лечит 4 ♥.",
     "Smells of bread and smoke.", "Пахнет хлебом и дымом."),
    ("AmuletOfTheDeadspeaker", "Amulet of the Deadspeaker", "Amulet. Your undead summons get 3 more HP.",
     "Амулет Говорящего с Мёртвыми", "Амулет. Ваши призывы-нежить получают +3 ♥.",
     "They answer. That is the problem.", "Они отвечают. В этом и беда."),
    ("AmuletOfTheWeaver", "Amulet of the Weaver", "Amulet. Your Sneak attacks apply 2 [gold]Poison[/gold].",
     "Амулет Ткачихи", "Амулет. Ваши Подлые атаки накладывают 2 [gold]Яда[/gold].",
     "Eight legs carved in black amber.", "Восемь лапок в чёрном янтаре."),
    ("NecklaceOfTheVoid", "Necklace of the Void", "Amulet. Start each combat with 1 [gold]Spell Absorption[/gold]. −1 max ✦.",
     "Ожерелье Пустоты", "Амулет. Каждый бой начинается с 1 [gold]Поглощения заклинаний[/gold]. −1 пул ✦.",
     "It drinks the light around it.", "Оно пьёт свет вокруг себя."),
    ("NecklaceOfStarstone", "Starstone Necklace", "Amulet. Once per combat, when you run out of ✦, regain 4 ✦.",
     "Ожерелье Звёздного Камня", "Амулет. Раз в бой, когда ✦ кончается, — восстановить 4 ✦.",
     "A fallen star, still falling inward.", "Упавшая звезда, всё ещё падающая внутрь."),
]

# Новые реликвии рюкзака (07 §7 «Прочее», M4 п. 5) — не снаряжение, без пометки о весе на иконке.
RELICS_BACKPACK = [
    ("FeatherSack", "Feather Sack", "+8 [gold]Burden[/gold] capacity. Weighs nothing.",
     "Мешок-Перышко", "+8 к вместимости [gold]Груза[/gold]. Сам ничего не весит.",
     "Stuffed with something lighter than air. Probably feathers.", "Набит чем-то легче воздуха. Наверное, перьями."),
    ("CampKettle", "Camp Kettle", "Food gives +1 to each of its effects.",
     "Походный котелок", "Еда даёт +1 к каждому своему действию.",
     "Dented, sooty, irreplaceable.", "Мятый, закопчённый, незаменимый."),
    ("BarrowMap", "Barrow Map", "Hover to see the next elite fights of this act.",
     "Карта Курганов", "Наведите, чтобы увидеть ближайшие бои с элитой в этом акте.",
     "Someone marked the graves that bite back.", "Кто-то пометил могилы, которые кусаются."),
    ("SharpeningStone", "Sharpening Stone", "Weapon attacks deal 1 more damage on the first turn of combat.",
     "Точильный камень", "Оружейные атаки на первом ходу боя наносят на 1 урона больше.",
     "Three strokes before every fight.", "Три взмаха перед каждым боем."),
    ("BottleOfMead", "Bottle of Mead", "At the start of each combat, if you have an empty potion slot, gain a [gold]Hangover?[/gold] potion. It fizzles out after combat.",
     "Бутыль Хмельного", "В начале каждого боя, если есть свободный слот, — зелье [gold]«Похмелье?»[/gold]. После боя оно выдыхается.",
     "Never empty. Never quite the same twice.", "Никогда не пустеет. Никогда не бывает одинаковой."),
]

RELICS_POTIONS = [
    ("HangoverPotion", "Hangover?", "Random: heal {Heal} HP, or restore {Stamina} ◆, or gain {Mana} ✦, or gain {Strength} [gold]Strength[/gold], "
                                   "or a headache — 1 [gold]Weak[/gold] and draw {Draw} cards.",
     "Похмелье?", "Случайно: лечит {Heal} ♥, или {Stamina} ◆, или {Mana} ✦, или {Strength} [gold]Силы[/gold], "
                  "или головная боль — 1 [gold]Слабости[/gold] и добор {Draw}."),
]

# Подсказки к ванильным реликвиям (UI/RequiemUi.Tip, static_hover_tips.json).
RELICS_TIPS = {
    "eng": {
        "RELIC_WEIGHT.title": "Weight",
        "RELIC_WEIGHT.description": "This relic adds {Weight} to your [gold]Burden[/gold].",
        "RELIC_REWORK_ENERGY.title": "Reworked",
        "RELIC_REWORK_ENERGY.description": "Energy relic: in Requiem it also costs −1 regeneration of your main resource — "
                                           "◆ for the Platebearer and the Silent, ✦ for the Regent.",
        "BARROW_MAP.title": "Next elites",
        "RING_COMBO_STEAM.title": "Combo: Steam",
        "RING_COMBO_STEAM.description": "With the Pyromancer's Ring and the Ring of Hoarfrost: when a target has both [gold]Burning[/gold] "
                                        "and [gold]Chill[/gold], both are removed and it takes their sum as damage, ignoring resistances.",
        "RING_COMBO_QUICKFURY.title": "Combo: Quick Fury",
        "RING_COMBO_QUICKFURY.description": "With the Ring of Quick Breath and the Ring of Fury Strike: attacks that cost 0 ◆ deal 2 more damage.",
        "RING_COMBO_CLEARVICEROY.title": "Combo: Clear Command",
        "RING_COMBO_CLEARVICEROY.description": "With the Ring of Clear Mind and the Viceroy's Ring: the Viceroy's replay works twice per combat.",
    },
    "rus": {
        "RELIC_WEIGHT.title": "Вес",
        "RELIC_WEIGHT.description": "Реликвия добавляет {Weight} к [gold]Грузу[/gold].",
        "RELIC_REWORK_ENERGY.title": "Переработана",
        "RELIC_REWORK_ENERGY.description": "Реликвия энергии: в Реквиеме она ещё и стоит −1 прироста основного ресурса — "
                                           "◆ у Латоносца и Безмолвной, ✦ у Регента.",
        "BARROW_MAP.title": "Ближайшие элиты",
        "RING_COMBO_STEAM.title": "Сочетание: Пар",
        "RING_COMBO_STEAM.description": "Кольца Пиромана и Изморози: когда на цели есть и [gold]Горение[/gold], и [gold]Озноб[/gold], "
                                        "оба снимаются, а цель получает урон, равный их сумме, без сопротивлений.",
        "RING_COMBO_QUICKFURY.title": "Сочетание: Скорая ярость",
        "RING_COMBO_QUICKFURY.description": "Кольца Скорого Дыхания и Ярого Удара: атаки за 0 ◆ наносят на 2 урона больше.",
        "RING_COMBO_CLEARVICEROY.title": "Сочетание: Ясный приказ",
        "RING_COMBO_CLEARVICEROY.description": "Кольца Ясного Ума и Наместника: повтор Наместника срабатывает дважды за бой.",
    },
}
