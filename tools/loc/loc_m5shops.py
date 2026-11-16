"""Тексты M5 п. 4: лавки docs/design/14 — свитки, посохи, услуги Чародея/Ювелира/Храма/Скупщика/Каравана, Таверна
с Наставником, Стража лавки, предупреждение о владении школой."""

from loc_stage7 import _m

# ───────────── свитки (зелья): класс, eng title, eng desc, rus title, rus desc ─────────────
_SCROLL_EN = "Add [gold]{spell}[/gold] to your hand. It costs 1 energy and 0 ✦, ignores school mastery and [gold]Exhausts[/gold]."
_SCROLL_RU = "Положить в руку [gold]{spell}[/gold]: стоит 1 энергию и 0 ✦, владение школой не нужно, [gold]Истощение[/gold]."

_SCROLLS = [
    ("ScrollOfFirebolt", "Firebolt", "Огненная стрела", "Огненной стрелы"),
    ("ScrollOfHealingLight", "Healing Light", "Исцеляющий свет", "Исцеляющего света"),
    ("ScrollOfOakflesh", "Oakflesh", "Дубовая плоть", "Дубовой плоти"),
    ("ScrollOfSunbeam", "Sunbeam", "Солнечный луч", "Солнечного луча"),
    ("ScrollOfFlameCloak", "Flame Cloak", "Плащ пламени", "Плаща пламени"),
    ("ScrollOfConfusion", "Confusion", "Смятение", "Смятения"),
    ("ScrollOfFireball", "Fireball", "Огненный шар", "Огненного шара"),
    ("ScrollOfIceSpike", "Ice Spike", "Ледяное копьё", "Ледяного копья"),
    ("ScrollOfStoneflesh", "Stoneflesh", "Каменная плоть", "Каменной плоти"),
]

M5_SCROLLS = [
    (cls, f"Scroll of {en}", _SCROLL_EN.format(spell=en), f"Свиток {gen}", _SCROLL_RU.format(spell=ru))
    for cls, en, ru, gen in _SCROLLS
]

# ───────────── посохи (реликвии без строки про вес): класс, eng, eng desc, rus, rus desc, flavor eng, flavor rus ─────────────
_STAFF_EN = ("Off-hand focus (staff {tier}). At the start of combat, spend a charge: add {what} to your hand "
             "(0 ✦, no mastery needed, [gold]Exhaust[/gold]). {charges} charges; a camp restores 2.\nShown number — charges left.")
_STAFF_RU = ("Фокус в левую руку (посох {tier}). В начале боя тратит заряд: в руку — {what} "
             "(0 ✦, владение не нужно, [gold]Истощение[/gold]). Зарядов: {charges}; лагерь восстанавливает 2.\nЧисло на иконке — оставшиеся заряды.")

M5_STAVES = [
    ("OakStaff", "Oak Staff", _STAFF_EN.format(tier="I", what="[gold]Sparks[/gold]", charges=6),
     "Дубовый посох", _STAFF_RU.format(tier="I", what="[gold]Искры[/gold]", charges=6),
     "Every apprentice's first argument.", "Первый довод любого ученика."),
    ("SilverStaff", "Silver Staff", _STAFF_EN.format(tier="II", what="[gold]Turn Undead[/gold]", charges=5),
     "Серебряный посох", _STAFF_RU.format(tier="II", what="[gold]Изгнание нежити[/gold]", charges=5),
     "The dead dislike the shine.", "Мёртвые не любят блеска."),
    ("ObsidianStaff", "Obsidian Staff", _STAFF_EN.format(tier="III", what="[gold]Fireball[/gold]", charges=4),
     "Обсидиановый посох", _STAFF_RU.format(tier="III", what="[gold]Огненный шар[/gold]", charges=4),
     "Still warm from the mountain that made it.", "Ещё тёплый от горы, что его родила."),
    ("EverfrostStaff", "Everfrost Staff", _STAFF_EN.format(tier="IV", what="[gold]Blizzard[/gold]", charges=4),
     "Посох вечного льда", _STAFF_RU.format(tier="IV", what="[gold]Буран[/gold]", charges=4),
     "Frost gathers on the hand that holds it.", "Иней ползёт по держащей его руке."),
    ("AbyssalStaff", "Abyssal Staff", _STAFF_EN.format(tier="V", what="two [gold]Chain Lightning[/gold]", charges=3)
     + "\n[purple]Abyssal[/purple]: your piety cannot rise above Tainted.",
     "Бездонный посох", _STAFF_RU.format(tier="V", what="две [gold]Цепи молний[/gold]", charges=3)
     + "\n[purple]Бездонное[/purple]: праведность не выше «Запятнанного».",
     "Sold only by those who never ask where things come from.", "Продают лишь те, кто не спрашивает, откуда вещь."),
]

# ───────────── карты прилавка и Таверны: класс, eng title, eng text, rus title, rus text ─────────────
M5_SHOP_CARDS = [
    ("CounterEnchantOwnStone", "Enchant (Your Stone)",
     "[gold]{Price}[/gold] gold. The arcanist enchants an equipped item with your soul stone of power {Amount}.",
     "Зачаровать (свой камень)", "[gold]{Price}[/gold] золотых. Чародей зачарует надетую вещь вашим камнем душ силы {Amount}."),
    ("CounterEnchantShopStone", "Enchant (Shop Stone)",
     "[gold]{Price}[/gold] gold. The arcanist enchants an equipped item with his own stone of power {Amount}.",
     "Зачаровать (камень лавки)", "[gold]{Price}[/gold] золотых. Чародей зачарует надетую вещь своим камнем силы {Amount}."),
    ("CounterRewriteScroll", "Rewrite a Scroll",
     "[gold]{Price}[/gold] gold. The spell of a scroll in your belt (tier {Amount}) goes into your deck for good; the scroll is spent.",
     "Переписать свиток", "[gold]{Price}[/gold] золотых. Заклинание свитка с пояса (ступень {Amount}) навсегда уходит в колоду, свиток тратится."),
    ("CounterEnchantCard", "Enchant a Card",
     "[gold]{Price}[/gold] gold. Put a card enchantment on a card in your deck.",
     "Зачаровать карту", "[gold]{Price}[/gold] золотых. Наложить чары на карту колоды."),
    ("CounterSoulStone", "Soul Stone",
     "[gold]{Price}[/gold] gold. Buy a soul stone of power {Amount} for enchanting.",
     "Камень душ", "[gold]{Price}[/gold] золотых. Купить камень душ силы {Amount} для зачарования."),
    ("CounterEngrave", "Engrave",
     "[gold]{Price}[/gold] gold. Engrave a power 1 enchantment on an equipped ring or amulet.",
     "Гравировка", "[gold]{Price}[/gold] золотых. Нанести чары силы 1 на надетое кольцо или амулет."),
    ("CounterFacet", "Facet",
     "[gold]{Price}[/gold] gold. Move enchantments from one equipped item to another that can hold them.",
     "Огранка", "[gold]{Price}[/gold] золотых. Перенести чары с одной надетой вещи на другую, которой они подходят."),
    ("CounterCleanse", "Cleansing",
     "[gold]{Price}[/gold] gold. Remove a card from your deck (cheaper for the Righteous).",
     "Очищение", "[gold]{Price}[/gold] золотых. Удалить карту из колоды (Праведным дешевле)."),
    ("CounterTempleHeal", "Temple Healing",
     "[gold]{Price}[/gold] gold. Heal {Amount} HP.",
     "Храмовое исцеление", "[gold]{Price}[/gold] золотых. Восстановить {Amount} ♥."),
    ("CounterAtonement", "Atonement",
     "[gold]{Price}[/gold] gold. Raise your piety by one step (once per act).",
     "Искупление", "[gold]{Price}[/gold] золотых. +1 ступень праведности (раз за акт)."),
    ("CounterChangeBlessing", "Change Blessing",
     "[gold]{Price}[/gold] gold. Take the blessing of this temple's god instead of your current one.",
     "Сменить благословение", "[gold]{Price}[/gold] золотых. Принять благословение бога этого храма вместо нынешнего."),
    ("CounterRemoveCurse", "Lift a Curse",
     "Lose {Amount} max HP. The fence takes a curse off your hands — no questions asked.",
     "Снять проклятие", "Потерять {Amount} макс. ♥. Скупщик заберёт проклятие — без лишних вопросов."),
    ("CounterClearName", "Clear Your Name",
     "[gold]{Price}[/gold] gold. The kind of shop that trusts you least (a theft wipes trust) is back to trust 1.",
     "Очистить имя", "[gold]{Price}[/gold] золотых. Лавки вида, который доверяет вам меньше всех (кража обнуляет доверие), снова на доверии 1."),
    ("CounterTrade", "Trade",
     "Give an item from your backpack for another of the same tier or one lower. Once per visit.",
     "Обмен", "Отдать вещь из рюкзака за другую той же ступени или на одну ниже. Раз за визит."),
    ("PickMentorSkill", "Mentor: Skill",
     "The mentor teaches a skill node: [gold]{Value}[/gold] gold, a foreign school [gold]{Cost}[/gold]. Up to 3 per run.",
     "Наставник: навык", "Наставник обучит узлу дерева: [gold]{Value}[/gold] золотых, чужая школа — [gold]{Cost}[/gold]. До 3 за забег."),
    ("PickLessonTwoHanded", "Lesson: Two-Handed",
     "[gold]{Value}[/gold] gold. No more [gold]Clumsy Grip[/gold] with two-handed weapons.",
     "Урок: двуручное", "[gold]{Value}[/gold] золотых. Больше никакого [gold]Неумелого хвата[/gold] с двуручным оружием."),
    ("PickLessonCrossbow", "Lesson: Crossbow",
     "[gold]{Value}[/gold] gold. No more [gold]Clumsy Grip[/gold] with crossbows.",
     "Урок: арбалет", "[gold]{Value}[/gold] золотых. Больше никакого [gold]Неумелого хвата[/gold] с арбалетом."),
    ("PickLessonShield", "Lesson: Heavy Shield",
     "[gold]{Value}[/gold] gold. No more [gold]Clumsy Grip[/gold] with heavy shields.",
     "Урок: тяжёлый щит", "[gold]{Value}[/gold] золотых. Больше никакого [gold]Неумелого хвата[/gold] с тяжёлым щитом."),
]

# ───────────── Стража лавки ─────────────
M5_SHOP_MONSTERS = {
    "SHOP_GUARD": _m("Shop Guard", "Стража лавки", {
        "GUARD_HALT": ("Halt!", "Стоять!"), "GUARD_HALBERD": ("Halberd", "Алебарда"), "GUARD_BASH": ("Shield Bash", "Удар щитом")}),
}

M5_SHOP_ENCOUNTERS = {
    "SHOP_GUARD_ELITE": {"eng": "Shop Guard", "rus": "Стража лавки"},
}

# ───────────── окна Книги торговца (таблица relics) ─────────────
M5_SHOP_RELIC_EXTRA = {
    "eng": {
        "MERCHANT_LEDGER.masteryHeader": "Too advanced?",
        "MERCHANT_LEDGER.masteryBody": "This spell is tier {Tier}, your mastery of its school is {Mastery}. "
                                       "Until you raise it you will cast it with strain: more ✦, and much more beyond one step. Buy anyway?",
        "MERCHANT_LEDGER.masteryYes": "Buy",
        "MERCHANT_LEDGER.masteryNo": "Not yet",
    },
    "rus": {
        "MERCHANT_LEDGER.masteryHeader": "Не по силам?",
        "MERCHANT_LEDGER.masteryBody": "Ступень заклинания — {Tier}, ваше владение его школой — {Mastery}. "
                                       "Пока не поднимете владение, колдовать придётся с трудом: больше ✦, а через ступень — намного больше. Всё равно купить?",
        "MERCHANT_LEDGER.masteryYes": "Купить",
        "MERCHANT_LEDGER.masteryNo": "Пока нет",
    },
}

# ───────────── Таверна (rest_site_ui) ─────────────
M5_SHOP_REST_SITE = {
    "eng": {
        "OPTION_SLAYTHEREQUIEM_TAVERN.name": "Tavern",
        "OPTION_SLAYTHEREQUIEM_TAVERN.description": "A mentor drinks here. Skill node for [gold]{SkillPrice}[/gold] gold "
                                                    "(bought {Bought}/3), weapon lesson for [gold]{LessonPrice}[/gold]. Costs no camp points.",
        "OPTION_SLAYTHEREQUIEM_TAVERN.descriptionDisabled": "The mentor has nothing you can afford.",
        "SLAYTHEREQUIEM_TAVERN_PROMPT": "What will the mentor teach? (close to leave)",
        "SLAYTHEREQUIEM_MENTOR_PROMPT": "Choose the skill node",
    },
    "rus": {
        "OPTION_SLAYTHEREQUIEM_TAVERN.name": "Таверна",
        "OPTION_SLAYTHEREQUIEM_TAVERN.description": "Здесь пьёт Наставник. Узел навыка — [gold]{SkillPrice}[/gold] золотых "
                                                    "(куплено {Bought}/3), урок владения оружием — [gold]{LessonPrice}[/gold]. Очков лагеря не тратит.",
        "OPTION_SLAYTHEREQUIEM_TAVERN.descriptionDisabled": "Наставнику нечего вам предложить по карману.",
        "SLAYTHEREQUIEM_TAVERN_PROMPT": "Чему научит Наставник? (закройте, чтобы уйти)",
        "SLAYTHEREQUIEM_MENTOR_PROMPT": "Выберите узел навыка",
    },
}
