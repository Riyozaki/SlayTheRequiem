"""Тексты этапа 3.5: лавки (docs/design/14) и Камень Памяти (docs/design/15)."""

# (класс, eng title, eng text, rus title, rus text, eng flavor, rus flavor)
STAGE35_RELICS = [
    ("MerchantLedger", "Merchant's Ledger",
     "Remembers every shop. Trust here: {Trust} (−{Discount}% prices). Spend 150 gold in shops of a kind for +1 trust (max 3). "
     "Combat gold ×1.3. Gear from your backpack can be sold to merchants.",
     "Книга торговца",
     "Помнит каждую лавку. Доверие здесь: {Trust} (−{Discount}% цен). Каждые 150 золотых в лавках одного вида — +1 доверия (до 3). "
     "Золото за бои ×1.3. Снаряжение из рюкзака можно продать торговцу.",
     "Debts in red, favours in gold.", "Долги — красным, услуги — золотом."),
    ("MemoryStoneRelic", "Requiem Candles",
     "At the start of each act the Memory Stone takes an Imprint of your run. On death you may return to it for a Candle and a Scar. "
     "Returns so far: {Rollbacks}.",
     "Свечи Реквиема",
     "В начале каждого акта Камень Памяти снимает Отпечаток забега. После смерти можно вернуться к нему за Свечу и Шрам. "
     "Возвратов: {Rollbacks}.",
     "The Spire sings a requiem for every fallen. The stone remembers your name.",
     "Шпиль поёт Реквием по каждому павшему. Камень помнит твоё имя."),
]

# Тексты окон (лежат в relics.json рядом с реликвиями).
STAGE35_RELIC_EXTRA = {
    "eng": {
        "MERCHANT_LEDGER.sellHeader": "Sell your backpack?",
        "MERCHANT_LEDGER.sellBody": "The merchant offers [gold]{Gold}[/gold] gold ({Percent}%) for the {Count} unequipped item(s) in your backpack.",
        "MERCHANT_LEDGER.sellYes": "Sell",
        "MERCHANT_LEDGER.sellNo": "Keep",
        "MEMORY_STONE_RELIC.offerHeader": "Return to the Memory Stone?",
        "MEMORY_STONE_RELIC.offerBody": "Go back to the start of act {Act} for one Candle (left: {Candles}; −1 means unlimited). "
                                        "You keep the boss, the map stays; choose a Scar as the price. Revenge: +10% damage to those who killed you.",
        "MEMORY_STONE_RELIC.offerYes": "Return",
        "MEMORY_STONE_RELIC.offerNo": "Accept death",
    },
    "rus": {
        "MERCHANT_LEDGER.sellHeader": "Продать рюкзак?",
        "MERCHANT_LEDGER.sellBody": "Торговец даёт [gold]{Gold}[/gold] золотых ({Percent}%) за снятые вещи из рюкзака: {Count} шт.",
        "MERCHANT_LEDGER.sellYes": "Продать",
        "MERCHANT_LEDGER.sellNo": "Оставить",
        "MEMORY_STONE_RELIC.offerHeader": "Вернуться к Камню Памяти?",
        "MEMORY_STONE_RELIC.offerBody": "Вернуться к началу акта {Act} за одну Свечу (осталось: {Candles}; −1 — без ограничения). "
                                        "Босс тот же, карта та же; цена — Шрам на выбор. Месть: +10% урона тем, кто вас убил.",
        "MEMORY_STONE_RELIC.offerYes": "Вернуться",
        "MEMORY_STONE_RELIC.offerNo": "Принять смерть",
    },
}

# (класс, eng title, eng text, rus title, rus text)
STAGE35_CARDS = [
    ("DeathEcho", "Death Echo", "Unplayable. Weighs 2.", "Предсмертное эхо", "Неиграбельна. Весит 2."),
    ("ScarOfFlesh", "Scar of Flesh", "Lose 6% Max HP for the rest of the run (stacks).",
     "Шрам плоти", "−6% макс. ♥ до конца забега (складывается)."),
    ("ScarOfSoul", "Scar of Soul", "Add [gold]Death Echo[/gold] to your deck: an unplayable curse that weighs 2.",
     "Шрам души", "В колоду добавляется [gold]Предсмертное эхо[/gold] — неиграбельное проклятие весом 2."),
    ("ScarOfPurse", "Scar of Purse", "Lose 40% of the Imprint's gold.",
     "Шрам кошелька", "−40% золота Отпечатка."),
]

STAGE35_SETTINGS = {
    "eng": {
        "SHOP_VARIETY.title": "Requiem shops", "SHOP_VARIETY.hover.title": "Requiem shops",
        "SHOP_VARIETY.hover.desc": "Each shop is a Wanderer, Armorer, Arcanist or Herbalist with its own stock; trust discounts; "
                                   "selling backpack gear; combat gold ×1.3. Off — vanilla shops.",
        "DEATH_SECTION.title": "Death and the Memory Stone",
        "ENABLE_MEMORY_STONE.title": "Memory Stone", "ENABLE_MEMORY_STONE.hover.title": "Memory Stone",
        "ENABLE_MEMORY_STONE.hover.desc": "Imprint at the start of every act; after death you may return to it for a Candle and a Scar. Single player only for now.",
        "MEMORY_STONE_CANDLES.title": "Requiem Candles", "MEMORY_STONE_CANDLES.hover.title": "Requiem Candles",
        "MEMORY_STONE_CANDLES.hover.desc": "Returns per run. −1 — by level (0: unlimited, 1–4: 3, 5–7: 2, 8–9: 1, 10: none). 0 — classic roguelike.",
    },
    "rus": {
        "SHOP_VARIETY.title": "Лавки Реквиема", "SHOP_VARIETY.hover.title": "Лавки Реквиема",
        "SHOP_VARIETY.hover.desc": "Каждая лавка — Странник, Оружейник, Чародейская или Травница со своей витриной; скидки доверия; "
                                   "продажа вещей из рюкзака; золото за бои ×1.3. Выкл — ванильные магазины.",
        "DEATH_SECTION.title": "Смерть и Камень Памяти",
        "ENABLE_MEMORY_STONE.title": "Камень Памяти", "ENABLE_MEMORY_STONE.hover.title": "Камень Памяти",
        "ENABLE_MEMORY_STONE.hover.desc": "Отпечаток в начале каждого акта; после смерти можно вернуться к нему за Свечу и Шрам. Пока только в одиночной игре.",
        "MEMORY_STONE_CANDLES.title": "Свечи Реквиема", "MEMORY_STONE_CANDLES.hover.title": "Свечи Реквиема",
        "MEMORY_STONE_CANDLES.hover.desc": "Возвратов за забег. −1 — по уровню (0: без ограничения, 1–4: 3, 5–7: 2, 8–9: 1, 10: ни одной). 0 — классический рогалик.",
    },
}
