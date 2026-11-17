"""Тексты интерфейса M1 (completion-plan M1, art spec §4): ключевые слова-подсказки, подсказки шкал ◆/✦/⚖,
имя Латоносца (characters.json) и настройка скрытия Некробиндера/Дефекта.

Ключи без префикса — build_loc.py добавляет «SLAYTHEREQUIEM-». Ключевые слова: поле RequiemKeywords в верхнем
регистре (BaseLib CustomEnum), таблица card_keywords.json. Подписи шкал: UI/RequiemUi.Tip, static_hover_tips.json.
"""

_RES = {
    "eng": " Resistance I/II/III: −25/−50/−75%; vulnerability: +50%.",
    "rus": " Сопротивление I/II/III: −25/−50/−75 %; уязвимость: +50 %.",
}

UI_KEYWORDS = {
    "eng": {
        "STAMINA.title": "◆ Stamina",
        "STAMINA.description": "Attacks cost ◆ (the orb under the energy cost). Below half the pool you are [gold]Tired[/gold] "
                               "(−25% attack damage). An attack you cannot pay for leaves you [gold]Exhausted[/gold] (−50%) "
                               "and costs 2 HP per missing ◆. Carries over between turns and regenerates each turn.",
        "MANA.title": "✦ Mana",
        "MANA.description": "Spells cost ✦. Every hero has mana; the price depends on your mastery of the spell's school. "
                            "Combat starts at half the pool; unspent ✦ carries over.",
        "BURDEN.title": "⚖ Burden",
        "BURDEN.description": "Total weight of your deck and relics against your capacity. Overload 1–5: −1 ◆ regeneration; "
                              "6–10: also −1 card in the opening hand; above 10: you cannot pick up anything else.",
        "WEIGHT.title": "Weight",
        "WEIGHT.description": "How much this card adds to your [gold]Burden[/gold]. Basic, common and uncommon — 1, rare — 2, "
                              "curses — 2, statuses — 0. [gold]Lightened[/gold] cards weigh less.",
        "TIRED.title": "Tired",
        "TIRED.description": "Less than half of your ◆ pool is left: attacks deal 25% less damage.",
        "EXHAUSTED.title": "Exhausted",
        "EXHAUSTED.description": "Not enough ◆ to pay for this attack: it deals 50% less damage.",
        "OVERSTRAIN.title": "Overstrain",
        "OVERSTRAIN.description": "You can still play an attack without enough ◆ — each missing ◆ costs 2 HP.",
        "SNEAK.title": "Sneak",
        "SNEAK.description": "A sneak attack: the target has no attack intent, or you are [gold]Invisible[/gold] / used "
                             "[gold]Muffled Steps[/gold]. Cards with \"Sneak:\" get their extra effect; daggers deal ×1.5.",
        "SHOUT.title": "Shout",
        "SHOUT.description": "After it is played the Shout is shelved and returns to your hand after its cooldown "
                             "(3 turns by default).",
        "WEAPONATTACK.title": "Weapon attack",
        "WEAPONATTACK.description": "A Strike or weapon card: your equipped weapon's bonuses and its ◆ discount apply.",
        "FIRE.title": "Fire damage",
        "FIRE.description": "Burning: bursts at the end of the target's turn, melts Block (×2) and halves each turn." + _RES["eng"],
        "FROST.title": "Frost damage",
        "FROST.description": "Also drains as much ◆. Chill takes ◆ every turn; at 0 ◆ the target is Frozen." + _RES["eng"],
        "SHOCK.title": "Shock damage",
        "SHOCK.description": "Also burns as much ✦. Static takes ✦ every turn; without mana it shocks for HP." + _RES["eng"],
        "POISON.title": "Poison damage",
        "POISON.description": "Vanilla Poison: ticks at the start of turn, ignores Block, −1 each turn. Resistance reduces stacks." + _RES["eng"],
        "BLIGHT.title": "Blight",
        "BLIGHT.description": "Forbids healing and Regeneration; every Poison tick deals extra damage. Undead and constructs are immune." + _RES["eng"],
        "MAGIC.title": "Magic damage",
        "MAGIC.description": "Pure magic: only magic resistance and Ward reduce it." + _RES["eng"],
        "LIGHT.title": "Light damage",
        "LIGHT.description": "×2 against undead, vampires and the Abyss, ignoring their spell absorption. Normal damage to others.",
        "TRUEDAMAGE.title": "True damage",
        "TRUEDAMAGE.description": "Ignores Plating and all resistances.",
    },
    "rus": {
        "STAMINA.title": "◆ Выносливость",
        "STAMINA.description": "Атаки стоят ◆ (шар под ценой энергии). Меньше половины пула — [gold]Устал[/gold] (−25 % урона атак). "
                               "Не хватает на атаку — [gold]Изнурён[/gold] (−50 %) и −2 ОЗ за каждую недостающую ◆. "
                               "Копится между ходами и восстанавливается каждый ход.",
        "MANA.title": "✦ Мана",
        "MANA.description": "Заклинания стоят ✦. Мана есть у всех героев; цена зависит от владения школой заклинания. "
                            "Бой начинается с половиной пула; непотраченная ✦ сохраняется.",
        "BURDEN.title": "⚖ Груз",
        "BURDEN.description": "Общий вес колоды и реликвий против вместимости. Перегруз 1–5: прирост ◆ −1; "
                              "6–10: ещё и −1 карта в стартовой руке; больше 10: больше ничего не поднять.",
        "WEIGHT.title": "Вес",
        "WEIGHT.description": "Сколько карта добавляет к [gold]Грузу[/gold]. Базовые, обычные и необычные — 1, редкие — 2, "
                              "проклятия — 2, статусы — 0. [gold]Облегчённые[/gold] карты весят меньше.",
        "TIRED.title": "Устал",
        "TIRED.description": "Осталось меньше половины пула ◆: атаки наносят на 25 % меньше урона.",
        "EXHAUSTED.title": "Изнурён",
        "EXHAUSTED.description": "◆ не хватает, чтобы оплатить эту атаку: она нанесёт на 50 % меньше урона.",
        "OVERSTRAIN.title": "Надрыв",
        "OVERSTRAIN.description": "Атаку можно сыграть и без ◆ — каждая недостающая ◆ стоит 2 ОЗ.",
        "SNEAK.title": "Подлый",
        "SNEAK.description": "Подлый удар: у цели нет атакующего намерения, либо вы [gold]Невидимы[/gold] или сыграли "
                             "[gold]Приглушённые шаги[/gold]. Карты с пометкой «Подлый:» получают доп. эффект; кинжал — ×1,5 урона.",
        "SHOUT.title": "Клич",
        "SHOUT.description": "После розыгрыша Клич уходит на полку и возвращается в руку через перезарядку (обычно 3 хода).",
        "WEAPONATTACK.title": "Оружейная атака",
        "WEAPONATTACK.description": "Удар или карта оружия: действуют бонусы надетого оружия и его скидка на ◆.",
        "FIRE.title": "Урон огнём",
        "FIRE.description": "Горение: взрывной урон в конце хода цели, по Блоку ×2, каждый ход вдвое слабее." + _RES["rus"],
        "FROST.title": "Урон холодом",
        "FROST.description": "Отнимает столько же ◆. Озноб забирает ◆ каждый ход; на нуле ◆ — Обледенение." + _RES["rus"],
        "SHOCK.title": "Урон разрядом",
        "SHOCK.description": "Выжигает столько же ✦. Статика забирает ✦ каждый ход; без маны — бьёт током по ОЗ." + _RES["rus"],
        "POISON.title": "Урон ядом",
        "POISON.description": "Ванильный Яд: в начале хода, игнорирует Блок, −1 за ход. Сопротивление уменьшает стаки." + _RES["rus"],
        "BLIGHT.title": "Хворь",
        "BLIGHT.description": "Запрещает лечение и Регенерацию; каждый тик Яда больнее. Нежить и конструкты невосприимчивы." + _RES["rus"],
        "MAGIC.title": "Магический урон",
        "MAGIC.description": "Чистая магия: снижают только сопротивление магии и Оберег." + _RES["rus"],
        "LIGHT.title": "Урон светом",
        "LIGHT.description": "×2 по нежити, вампирам и Бездне, игнорируя их поглощение заклинаний. Остальным — обычный урон.",
        "TRUEDAMAGE.title": "Чистый урон",
        "TRUEDAMAGE.description": "Игнорирует Латы и все сопротивления.",
    },
}

UI_TIPS = {
    "eng": {
        "STAMINA_BAR.title": "◆ Stamina",
        "STAMINA_BAR.description": "{Amount} of {Pool}, +{Regen} each turn. Attacks cost ◆; below half — [gold]Tired[/gold] (−25%), "
                                   "not enough for an attack — [gold]Exhausted[/gold] (−50%) and 2 HP per missing ◆.",
        "MANA_BAR.title": "✦ Mana",
        "MANA_BAR.description": "{Amount} of {Pool}, +{Regen} each turn. Spells cost ✦.",
        "STATE_TIRED.title": "Tired",
        "STATE_TIRED.description": "Less than half of the ◆ pool: attacks deal 25% less damage.",
        "STATE_EXHAUSTED.title": "Exhausted",
        "STATE_EXHAUSTED.description": "No ◆ left: attacks deal 50% less damage and each missing ◆ costs 2 HP.",
        "ENEMY_STAMINA.title": "◆ Enemy stamina",
        "ENEMY_STAMINA.description": "{Amount} of {Pool}, +{Regen} at the end of its turn. Attacks cost ◆ (multi-hit and heavy ones — 2). "
                                     "Low ◆ weakens its attacks; at 0 it [gold]Catches its breath[/gold] ({Block} Block, full ◆) instead of attacking. "
                                     "Frost and Chill drain ◆.",
        "ENEMY_MANA.title": "✦ Enemy mana",
        "ENEMY_MANA.description": "{Amount} of {Pool}, +{Regen} at the end of its turn. Its attacks are spells and cost ✦; without mana it "
                                  "[gold]Focuses[/gold] (Ward, full ✦). Shock and Static burn ✦ and break its spells.",
        "BURDEN.title": "⚖ Burden",
        "BURDEN.description": "Weight {Weight} of {Capacity}. Cards and relics weigh 1–2, equipment — by item, starting relics — 0.",
        "BURDEN_OK.title": "No overload",
        "BURDEN_OK.description": "Within capacity: no penalties.",
        "BURDEN_LIGHT.title": "Overload +{Excess}",
        "BURDEN_LIGHT.description": "Overload {Excess} (1–5): ◆ regeneration −{StaminaPenalty}.",
        "BURDEN_HEAVY.title": "Heavy overload +{Excess}",
        "BURDEN_HEAVY.description": "Overload {Excess} (6–10): ◆ regeneration −{StaminaPenalty}, opening hand −{HandPenalty} card.",
        "BURDEN_CRUSHING.title": "Crushing overload +{Excess}",
        "BURDEN_CRUSHING.description": "Overload {Excess} (above 10): ◆ regeneration −{StaminaPenalty}, opening hand −{HandPenalty} card, "
                                       "you cannot pick up anything until you drop something.",
        "BURDEN_BLOCKED.title": "Too heavy: {Card} was not taken (Burden {Weight} / {Capacity}, overload +{Excess}). "
                                "Drop a card from the deck view (click a card → Drop), store it in the camp Chest, or stow equipment.",
        "DISCARD_BUTTON.title": "Drop (−{Weight} ⚖)",
        "DISCARD_CONFIRM.title": "Click again to drop",
        "DISCARD_HINT.title": "Overload +{Excess}. The card is lost for good; its weight {Weight} is freed.",
        "DISCARD_NOT_THIS.title": "Basic cards, curses, statuses and Eternal cards cannot be dropped — remove them at a shop or event.",
        "DISCARD_NOT_OVERLOADED.title": "You can drop cards only while overloaded. To thin the deck, use card removal at a shop.",
        "DISCARD_BUTTON.description": "Dropping is allowed outside combat while overloaded; the card is gone for good.",
        "DISCARD_CONFIRM.description": "Dropping is allowed outside combat while overloaded; the card is gone for good.",
        "DISCARD_HINT.description": "Dropping is allowed outside combat while overloaded; the card is gone for good.",
        "DISCARD_NOT_THIS.description": "Dropping is allowed outside combat while overloaded; the card is gone for good.",
        "DISCARD_NOT_OVERLOADED.description": "Dropping is allowed outside combat while overloaded; the card is gone for good.",
    },
    "rus": {
        "STAMINA_BAR.title": "◆ Выносливость",
        "STAMINA_BAR.description": "{Amount} из {Pool}, +{Regen} за ход. Атаки стоят ◆; меньше половины — [gold]Устал[/gold] (−25 %), "
                                   "не хватает на атаку — [gold]Изнурён[/gold] (−50 %) и 2 ОЗ за каждую недостающую ◆.",
        "MANA_BAR.title": "✦ Мана",
        "MANA_BAR.description": "{Amount} из {Pool}, +{Regen} за ход. Заклинания стоят ✦.",
        "STATE_TIRED.title": "Устал",
        "STATE_TIRED.description": "Меньше половины пула ◆: атаки наносят на 25 % меньше урона.",
        "STATE_EXHAUSTED.title": "Изнурён",
        "STATE_EXHAUSTED.description": "◆ не осталось: атаки наносят на 50 % меньше урона, каждая недостающая ◆ стоит 2 ОЗ.",
        "ENEMY_STAMINA.title": "◆ Выносливость врага",
        "ENEMY_STAMINA.description": "{Amount} из {Pool}, +{Regen} в конце его хода. Атака стоит ◆ (многоударная и тяжёлая — 2). "
                                     "Мало ◆ — бьёт слабее; на нуле вместо атаки [gold]Отдышаться[/gold] ({Block} Блока и полная ◆). "
                                     "Холод и Озноб отнимают ◆.",
        "ENEMY_MANA.title": "✦ Мана врага",
        "ENEMY_MANA.description": "{Amount} из {Pool}, +{Regen} в конце его хода. Его атаки — заклинания и стоят ✦; без маны он "
                                  "[gold]Сосредотачивается[/gold] (Оберег, полная ✦). Разряд и Статика выжигают ✦ и срывают заклинания.",
        "BURDEN.title": "⚖ Груз",
        "BURDEN.description": "Вес {Weight} из {Capacity}. Карты и реликвии весят 1–2, снаряжение — по предмету, стартовые реликвии — 0.",
        "BURDEN_OK.title": "Без перегруза",
        "BURDEN_OK.description": "В пределах вместимости: штрафов нет.",
        "BURDEN_LIGHT.title": "Перегруз +{Excess}",
        "BURDEN_LIGHT.description": "Перегруз {Excess} (1–5): прирост ◆ −{StaminaPenalty}.",
        "BURDEN_HEAVY.title": "Тяжёлый перегруз +{Excess}",
        "BURDEN_HEAVY.description": "Перегруз {Excess} (6–10): прирост ◆ −{StaminaPenalty}, стартовая рука −{HandPenalty} карта.",
        "BURDEN_CRUSHING.title": "Неподъёмный груз +{Excess}",
        "BURDEN_CRUSHING.description": "Перегруз {Excess} (больше 10): прирост ◆ −{StaminaPenalty}, стартовая рука −{HandPenalty} карта, "
                                       "ничего не поднять, пока что-нибудь не выбросите.",
        "BURDEN_BLOCKED.title": "Слишком тяжело: «{Card}» не взята (груз {Weight} / {Capacity}, перегруз +{Excess}). "
                                "Выбросьте карту на экране колоды (клик по карте → «Выбросить»), уберите в Сундук лагеря или снимите снаряжение.",
        "DISCARD_BUTTON.title": "Выбросить (−{Weight} ⚖)",
        "DISCARD_CONFIRM.title": "Нажмите ещё раз, чтобы выбросить",
        "DISCARD_HINT.title": "Перегруз +{Excess}. Карта пропадёт насовсем, освободится {Weight} веса.",
        "DISCARD_NOT_THIS.title": "Базовые карты, проклятия, статусы и «Вечные» не выбрасываются — их снимают лавка и события.",
        "DISCARD_NOT_OVERLOADED.title": "Выбрасывать карты можно только при перегрузе. Чтобы проредить колоду — удаление карт в лавке.",
        "DISCARD_BUTTON.description": "Выбрасывать можно вне боя при перегрузе; карта пропадает насовсем.",
        "DISCARD_CONFIRM.description": "Выбрасывать можно вне боя при перегрузе; карта пропадает насовсем.",
        "DISCARD_HINT.description": "Выбрасывать можно вне боя при перегрузе; карта пропадает насовсем.",
        "DISCARD_NOT_THIS.description": "Выбрасывать можно вне боя при перегрузе; карта пропадает насовсем.",
        "DISCARD_NOT_OVERLOADED.description": "Выбрасывать можно вне боя при перегрузе; карта пропадает насовсем.",
    },
}

# Ironclad → Латоносец (глоссарий 12, R-11): ключи мода, подставляются патчем UI/HeroPatches.PlatebearerNames.
UI_CHARACTERS = {
    "eng": {
        "PLATEBEARER.title": "The Platebearer",
        "PLATEBEARER.titleObject": "the Platebearer",
        "PLATEBEARER.description": "A clan warrior in heavy plate who climbed the Spire for glory.\n"
                                   "Pays for attacks with [gold]◆ Stamina[/gold], fights with weapon and shield, "
                                   "carries the most [gold]⚖ Burden[/gold].",
    },
    "rus": {
        "PLATEBEARER.title": "Латоносец",
        "PLATEBEARER.titleObject": "Латоносца",
        "PLATEBEARER.description": "Воин клана в тяжёлых латах, поднявшийся на Шпиль за славой.\n"
                                   "Платит за атаки [gold]◆ выносливостью[/gold], бьётся оружием и щитом, "
                                   "уносит больше всех [gold]⚖ груза[/gold].",
    },
}

UI_SETTINGS = {
    "eng": {
        "SHOW_VANILLA_ONLY_HEROES.title": "Necrobinder and Defect as heroes",
        "SHOW_VANILLA_ONLY_HEROES.hover.title": "Necrobinder and Defect",
        "SHOW_VANILLA_ONLY_HEROES.hover.desc": "In the mod they are transformation forms (Lich, Clockwork Heart), so they are hidden "
                                               "on the character select screen and their cards never appear in rewards. On — bring them "
                                               "back as vanilla heroes (the mod's systems do not apply to them). In co-op the host's setting applies.",
        "COLOR_ELEMENTAL_TITLES.title": "Elemental card titles in colour",
        "COLOR_ELEMENTAL_TITLES.hover.title": "Elemental titles",
        "COLOR_ELEMENTAL_TITLES.hover.desc": "The title of a fire, frost, shock, poison, blight, light or magic card takes the "
                                             "colour of its element. Upgraded cards keep the green outline and the \"+\". Cosmetic only.",
        "SHOW_PAPER_DOLL.title": "Equipment on the hero model",
        "SHOW_PAPER_DOLL.hover.title": "Paper doll",
        "SHOW_PAPER_DOLL.hover.desc": "In combat the hero holds the equipped weapon and shield (or staff), coloured by material, "
                                      "and glows with an aura of the armour class and tier. Cosmetic only.",
        "COMBAT_VFX.title": "Requiem combat effects",
        "COMBAT_VFX.hover.title": "Combat effects",
        "COMBAT_VFX.hover.desc": "Recoloured vanilla effects: an elemental burst on hits by damage type (frost, fire, shock, poison, "
                                 "blight, light, magic, true), Overstrain, Broken, Dodge, Shouts, summoning, the Memory Stone rewind and the Crimson portal. "
                                 "Off — only the vanilla attack effects. Cosmetic only.",
        "REQUIEM_SOUNDS.title": "Requiem sounds",
        "REQUIEM_SOUNDS.hover.title": "Requiem sounds",
        "REQUIEM_SOUNDS.hover.desc": "Sounds for hits by damage type, Overstrain, Broken, Dodge, Shouts, summoning, the Memory Stone rewind, "
                                     "the Crimson portal and Caches — vanilla sounds plus a few of our own. Volume follows the game's Sound slider. "
                                     "Independent of combat effects.",
    },
    "rus": {
        "SHOW_VANILLA_ONLY_HEROES.title": "Некробиндер и Дефект как герои",
        "SHOW_VANILLA_ONLY_HEROES.hover.title": "Некробиндер и Дефект",
        "SHOW_VANILLA_ONLY_HEROES.hover.desc": "В моде это облики превращений (Лич, Заводное сердце), поэтому они скрыты на экране "
                                               "выбора, а их карты не попадают в награды. Вкл — вернуть их как ванильных героев "
                                               "(системы мода на них не действуют). В коопе действует настройка хоста.",
        "COLOR_ELEMENTAL_TITLES.title": "Цветные названия стихийных карт",
        "COLOR_ELEMENTAL_TITLES.hover.title": "Цвет стихии в названии",
        "COLOR_ELEMENTAL_TITLES.hover.desc": "Название карты огня, холода, разряда, яда, хвори, света или магии окрашено в цвет "
                                             "стихии. У улучшенных остаются зелёная обводка и «+». Только оформление.",
        "SHOW_PAPER_DOLL.title": "Снаряжение на модели героя",
        "SHOW_PAPER_DOLL.hover.title": "Кукла героя",
        "SHOW_PAPER_DOLL.hover.desc": "В бою герой держит надетое оружие и щит (или посох) цвета их материала, а вокруг — аура "
                                      "класса и ступени брони. Только оформление.",
        "COMBAT_VFX.title": "Боевые эффекты Реквиема",
        "COMBAT_VFX.hover.title": "Боевые эффекты",
        "COMBAT_VFX.hover.desc": "Ванильные эффекты в цветах Реквиема: вспышка стихии при ударе по типу урона (холод, огонь, "
                                 "разряд, яд, хворь, свет, магия, чистый), надрыв, Слом, Уворот, Кличи, призыв, откат Камня Памяти и Багровый портал. "
                                 "Выкл — только ванильные эффекты атак. Только оформление.",
        "REQUIEM_SOUNDS.title": "Звуки Реквиема",
        "REQUIEM_SOUNDS.hover.title": "Звуки Реквиема",
        "REQUIEM_SOUNDS.hover.desc": "Звуки ударов по типу урона, надрыва, Слома, Уворота, Кличей, призыва, отката Камня Памяти, Багрового портала "
                                     "и Тайников — ванильные звуки игры и несколько своих. Громкость — ползунок «Звуки» игры. "
                                     "Не зависит от боевых эффектов.",
    },
}
