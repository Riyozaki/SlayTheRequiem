"""Тексты этапа 3.6: кооп Камня Памяти и лавок, прилавок услуг, Заказ, кража, Кузня (docs/design/07, 10, 14, 15)."""

# (класс, eng title, eng text, rus title, rus text) — карты прилавка: {Price} — цена, {Amount} — количество/ступень.
STAGE36_CARDS = [
    ("CounterSell", "Sell Backpack", "Sell {Amount} unequipped item(s) from your backpack for [gold]{Price}[/gold] gold.",
     "Продать рюкзак", "Продать снятые вещи из рюкзака ({Amount} шт.) за [gold]{Price}[/gold] золотых."),
    ("CounterSharpenWeapon", "Sharpen Weapon", "[gold]{Price}[/gold] gold. Your weapon becomes +{Amount}: +1 damage per step above V, property thresholds count it as higher tier.",
     "Заточка оружия", "[gold]{Price}[/gold] золотых. Оружие становится +{Amount}: свойства считают его на ступень выше, выше V — +1 урона."),
    ("CounterSharpenArmor", "Reinforce Armor", "[gold]{Price}[/gold] gold. Your least improved armor piece becomes +{Amount} (counts as a higher tier for Plating).",
     "Укрепление брони", "[gold]{Price}[/gold] золотых. Наименее улучшенная вещь брони становится +{Amount} (для Лат — ступенью выше)."),
    ("CounterReforge", "Reforge", "[gold]{Price}[/gold] gold. Reforge your weapon into a different weapon of tier {Amount}; improvements are kept.",
     "Перековка", "[gold]{Price}[/gold] золотых. Перековать оружие в другое оружие ступени {Amount}; улучшения сохраняются."),
    ("CounterBrewPotion", "Brew a Potion", "[gold]{Price}[/gold] gold. The herbalist brews your strongest potion from the pouch (tier {Amount}).",
     "Сварить зелье", "[gold]{Price}[/gold] золотых. Травница сварит сильнейшее зелье из вашей сумки (ступень {Amount})."),
    ("CounterBrewPoison", "Brew a Poison", "[gold]{Price}[/gold] gold. The herbalist brews your strongest poison from the pouch (tier {Amount}).",
     "Сварить яд", "[gold]{Price}[/gold] золотых. Травница сварит сильнейший яд из вашей сумки (ступень {Amount})."),
    ("CounterReagentKit", "Reagent Kit", "[gold]{Price}[/gold] gold. {Amount} common reagents into the pouch.",
     "Набор реагентов", "[gold]{Price}[/gold] золотых. {Amount} обычных реагента в сумку."),
    ("CounterSellReagents", "Sell Reagents", "Sell all {Amount} reagents from the pouch for [gold]{Price}[/gold] gold.",
     "Продать реагенты", "Продать все реагенты из сумки ({Amount} шт.) за [gold]{Price}[/gold] золотых."),
    ("CounterPanacea", "Panacea", "[gold]{Price}[/gold] gold. Cure all ailments in your deck ({Amount}).",
     "Панацея", "[gold]{Price}[/gold] золотых. Излечить все болезни в колоде ({Amount})."),
    ("CounterOrder", "Place an Order", "Trust 3: pay half the price now — the next shop of this kind will have the ordered item.",
     "Сделать заказ", "Доверие 3: половина цены сейчас — в следующей лавке этого вида заказанный товар будет на витрине."),
    ("CounterMore", "More…", "Show other services.", "Ещё…", "Показать другие услуги."),
    ("OrderWeapon", "Order: Weapon", "[gold]{Price}[/gold] gold. A tier {Amount} weapon in the next Armorer.",
     "Заказ: оружие", "[gold]{Price}[/gold] золотых. Оружие ступени {Amount} у следующего Оружейника."),
    ("OrderArmor", "Order: Armor", "[gold]{Price}[/gold] gold. Tier {Amount} armor of your class in the next Armorer.",
     "Заказ: броня", "[gold]{Price}[/gold] золотых. Броня вашего класса ступени {Amount} у следующего Оружейника."),
    ("OrderShield", "Order: Shield", "[gold]{Price}[/gold] gold. A tier {Amount} shield in the next Armorer.",
     "Заказ: щит", "[gold]{Price}[/gold] золотых. Щит ступени {Amount} у следующего Оружейника."),
    ("OrderEquipment", "Order: Gear", "[gold]{Price}[/gold] gold. Tier {Amount} gear in the next Wanderer's shop.",
     "Заказ: снаряжение", "[gold]{Price}[/gold] золотых. Снаряжение ступени {Amount} у следующего Странника."),
    ("OrderSpell", "Order: Spell", "[gold]{Price}[/gold] gold. The highest-tier spell available to you in the next Arcanist.",
     "Заказ: заклинание", "[gold]{Price}[/gold] золотых. Заклинание высшей доступной ступени в следующей Чародейской."),
    ("OrderPotion", "Order: Potion", "[gold]{Price}[/gold] gold. A tier V potion in the next Herbalist.",
     "Заказ: зелье", "[gold]{Price}[/gold] золотых. Зелье ступени V у следующей Травницы."),
    ("OrderPoison", "Order: Poison", "[gold]{Price}[/gold] gold. A tier V poison in the next Herbalist.",
     "Заказ: яд", "[gold]{Price}[/gold] золотых. Яд ступени V у следующей Травницы."),
]

STAGE36_RELIC_EXTRA = {
    "eng": {
        "MERCHANT_LEDGER.theftHeader": "Steal it?",
        "MERCHANT_LEDGER.theftBody": "You can't afford it ({Gold} gold). Chance to slip away: {Chance}%. "
                                     "If caught — the guards beat you (−20% max HP) and this kind of shop stops trusting you.",
        "MERCHANT_LEDGER.theftYes": "Steal",
        "MERCHANT_LEDGER.theftNo": "Don't",
        "MEMORY_STONE_RELIC.coopOfferHeader": "Offer a return to the Memory Stone?",
        "MEMORY_STONE_RELIC.coopOfferBody": "The party votes on going back to the start of act {Act} for one shared Candle (left: {Candles}; −1 — unlimited). "
                                            "Ties are yours to break. Each player chooses their own Scar.",
        "MEMORY_STONE_RELIC.coopOfferYes": "Put to a vote",
        "MEMORY_STONE_RELIC.coopOfferNo": "Accept death",
        "MEMORY_STONE_RELIC.coopVoteHeader": "Return to the Memory Stone?",
        "MEMORY_STONE_RELIC.coopVoteBody": "The host proposes going back to the start of act {Act} for one shared Candle (left: {Candles}; −1 — unlimited). "
                                           "A new map, the same boss; you will choose a Scar. After the vote, rejoin the host's lobby.",
        "MEMORY_STONE_RELIC.coopVoteYes": "For",
        "MEMORY_STONE_RELIC.coopVoteNo": "Against",
    },
    "rus": {
        "MERCHANT_LEDGER.theftHeader": "Украсть?",
        "MERCHANT_LEDGER.theftBody": "Золота не хватает ({Gold}). Шанс уйти незамеченным: {Chance}%. "
                                     "Если поймают — стража побьёт (−20% макс. ♥), а лавки этого вида перестанут вам доверять.",
        "MERCHANT_LEDGER.theftYes": "Украсть",
        "MERCHANT_LEDGER.theftNo": "Не стоит",
        "MEMORY_STONE_RELIC.coopOfferHeader": "Предложить вернуться к Камню Памяти?",
        "MEMORY_STONE_RELIC.coopOfferBody": "Отряд проголосует за возврат к началу акта {Act} за одну общую Свечу (осталось: {Candles}; −1 — без ограничения). "
                                            "При ничьей решаете вы. Шрам каждый выбирает сам.",
        "MEMORY_STONE_RELIC.coopOfferYes": "На голосование",
        "MEMORY_STONE_RELIC.coopOfferNo": "Принять смерть",
        "MEMORY_STONE_RELIC.coopVoteHeader": "Вернуться к Камню Памяти?",
        "MEMORY_STONE_RELIC.coopVoteBody": "Хост предлагает вернуться к началу акта {Act} за одну общую Свечу (осталось: {Candles}; −1 — без ограничения). "
                                           "Новая карта, тот же босс; Шрам выберете сами. После голосования присоединитесь к лобби хоста.",
        "MEMORY_STONE_RELIC.coopVoteYes": "За",
        "MEMORY_STONE_RELIC.coopVoteNo": "Против",
    },
}

STAGE36_SETTINGS = {
    "eng": {
        "ALLOW_THEFT.title": "Theft in shops", "ALLOW_THEFT.hover.title": "Theft in shops",
        "ALLOW_THEFT.hover.desc": "Click an item you can't afford to try stealing it (80% −10% per 50 gold, once per shop). "
                                  "Later theft will come from relics and origins; this switch allows it without them. Must match in co-op.",
        "ENABLE_MEMORY_STONE.hover.desc": "Imprint at the start of every act; after death you may return to it for a Candle and a Scar. "
                                          "The act gets a new map, the boss stays. In co-op the host proposes, the party votes, Candles are shared.",
    },
    "rus": {
        "ALLOW_THEFT.title": "Кража в лавках", "ALLOW_THEFT.hover.title": "Кража в лавках",
        "ALLOW_THEFT.hover.desc": "Щелчок по товару, на который не хватает золота, — попытка кражи (80% −10% за каждые 50 золотых, раз за лавку). "
                                  "Позже кражу дадут реликвии и происхождения; переключатель разрешает её без них. В коопе должен совпадать.",
        "ENABLE_MEMORY_STONE.hover.desc": "Отпечаток в начале каждого акта; после смерти можно вернуться к нему за Свечу и Шрам. "
                                          "Карта акта — новая, босс — прежний. В коопе хост предлагает, отряд голосует, Свечи общие.",
    },
}

STAGE36_REST_SITE = {
    "eng": {
        "OPTION_SLAYTHEREQUIEM_FORGE.name": "Forge",
        "OPTION_SLAYTHEREQUIEM_FORGE.description": "Improve one equipped item by +1 (up to +2). Ends your rest.",
        "OPTION_SLAYTHEREQUIEM_FORGE.descriptionDisabled": "No equipped item can be improved further.",
    },
    "rus": {
        "OPTION_SLAYTHEREQUIEM_FORGE.name": "Кузня",
        "OPTION_SLAYTHEREQUIEM_FORGE.description": "Улучшить одну надетую вещь на +1 (не выше +2). Привал заканчивается.",
        "OPTION_SLAYTHEREQUIEM_FORGE.descriptionDisabled": "Ни одну надетую вещь нельзя улучшить дальше.",
    },
}
