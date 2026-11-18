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
        "REQUIREDSTAMINA.title": "◆ Required stamina",
        "REQUIREDSTAMINA.description": "This Skill requires its full, fixed ◆ cost. Not enough stamina: it cannot be played; no HP overstrain. Attack discounts and Tired/Exhausted damage penalties do not apply. Replays pay only once.",
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
        "MAGIC.description": "Magic resistance reduces it. Ordinary Block, Ward, caps and special defenses still apply." + _RES["eng"],
        "LIGHT.title": "Light damage",
        "LIGHT.description": "×2 against undead, vampires and the Abyss, ignoring their spell absorption. Normal damage to others.",
        "TRUEDAMAGE.title": "True damage",
        "TRUEDAMAGE.description": "Ignores Plating, Sunder and resistances, but not ordinary Block, damage caps (such as Intangible) or special invulnerability. Weak/Vulnerable and stamina still apply to powered attacks.",
    },
    "rus": {
        "STAMINA.title": "◆ Выносливость",
        "STAMINA.description": "Атаки стоят ◆ (шар под ценой энергии). Меньше половины пула — [gold]Устал[/gold] (−25 % урона атак). "
                               "Не хватает на атаку — [gold]Изнурён[/gold] (−50 %) и −2 ОЗ за каждую недостающую ◆. "
                               "Копится между ходами и восстанавливается каждый ход.",
        "REQUIREDSTAMINA.title": "◆ Обязательная выносливость",
        "REQUIREDSTAMINA.description": "Навык требует полной фиксированной цены ◆. Не хватает — сыграть нельзя, без надрыва за ОЗ. Скидки на Атаки и штрафы урона Устал/Изнурён не действуют. Повторы платят один раз.",
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
        "MAGIC.description": "Снижается сопротивлением магии. Обычный Блок, Оберег, ограничения урона и особая защита сохраняются." + _RES["rus"],
        "LIGHT.title": "Урон светом",
        "LIGHT.description": "×2 по нежити, вампирам и Бездне, игнорируя их поглощение заклинаний. Остальным — обычный урон.",
        "TRUEDAMAGE.title": "Чистый урон",
        "TRUEDAMAGE.description": "Игнорирует Латы, Раскол и сопротивления, но не обычный Блок, ограничение урона (например, Бесплотность) и особую неуязвимость. Слабость/Уязвимость и выносливость всё ещё действуют на силовые атаки.",
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
        "CAMP_CAT_REST.title": "Rest",
        "CAMP_CAT_REST.description": "Recover HP, meditate, pray.",
        "CAMP_CAT_CRAFT.title": "Craft",
        "CAMP_CAT_CRAFT.description": "Forge, brew, cook.",
        "CAMP_CAT_MAGIC.title": "Enchanting",
        "CAMP_CAT_MAGIC.description": "Enchant gear and cards.",
        "CAMP_CAT_GEAR.title": "Gear & training",
        "CAMP_CAT_GEAR.description": "Chest, level points, lifting.",
        "CAMP_CAT_SPECIAL.title": "Special",
        "CAMP_CAT_SPECIAL.description": "Tavern, rites and other rare options.",
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
        "CAMP_CAT_REST.title": "Отдых",
        "CAMP_CAT_REST.description": "Восстановить здоровье, медитировать, молиться.",
        "CAMP_CAT_CRAFT.title": "Ремесло",
        "CAMP_CAT_CRAFT.description": "Кузня, алхимия, готовка.",
        "CAMP_CAT_MAGIC.title": "Зачарование",
        "CAMP_CAT_MAGIC.description": "Зачаровать снаряжение и карты.",
        "CAMP_CAT_GEAR.title": "Снаряжение и тренировка",
        "CAMP_CAT_GEAR.description": "Сундук, очки уровня, тренировки.",
        "CAMP_CAT_SPECIAL.title": "Особое",
        "CAMP_CAT_SPECIAL.description": "Таверна, обряды и другие редкие действия.",
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

# G0/G1: persistent explanations/results, not toast-only silent vetoes.
_INTEGRITY_TIPS = {
    "eng": {
        "CHEST_RESULT.title": "Chest: {Moved}/{Total} fully completed, {Issues} need attention. Uncommitted entries are retained.",
        "CHEST_RESULT_ITEM.title": "{Target}: {Status}",
        "REWARD_UNLOAD.title": "Open deck / unload, then retry",
        "REWARD_READY.title": "can take",
        "REWARD_PAYMENT_BLOCKED.title": "requires {Cost} gold; available {Gold}. No card or payment committed",
        "REWARD_OFFER_STALE.title": "offer is no longer available; close it without buying",
        "REWARD_PAID_READY.title": "buy for {Cost} gold after the card is accepted; skip is free (no combat-reward alternatives)",
        "REWARD_OVERLOAD.title": "blocked: overload above +10; unload first",
        "REWARD_OPERATION_BLOCKED.title": "blocked: an earlier operation is unresolved; keep the log and inspect the run passport",
        "REWARD_VETO.title": "blocked by {Source}",
        "REWARD_CARD_STATE.title": "{Card} · weight {Weight} · {Reason}",
        "REWARD_STATE.title": "Burden {Weight}/{Capacity}. A blocked pick does not spend the reward. Skip is a separate choice.",
        "OPERATION_RESULT.title": "Operation: {Status}. See [ShopOperation] in the log for the exact result.",
        "OPERATION_BLOCKED.title": "Operation outcome is uncertain. Click the Requiem badge: retry receipts (no repeated payment), or restore the host's pre-operation checkpoint after confirmation. The failed save is kept. Continue alone does not repair a partial effect.",
        "GRANT_DEFERRED.title": "The card grant was not accepted. Its exact card is saved and will be offered again in a non-combat room. Unload first; an original starter is kept until the replacement is taken.",
        "RUN_PASSPORT.title": "Run passport · actual artifacts and rules (not gameplay acceptance)",
        "RUN_PASSPORT_CLOSE.title": "Close passport",
        "RUN_ENABLED.title": "Requiem: ON",
        "RUN_DISABLED.title": "Requiem: OFF",
        "RUN_DEGRADED.title": "Requiem: DEGRADED",
        "OP_STATUS_COMMITTED.title": "completed",
        "OP_STATUS_REJECTED.title": "rejected — no commit",
        "OP_STATUS_CANCELLED.title": "cancelled",
        "OP_STATUS_ERROR.title": "error — inspect log",
        "OP_STATUS_PARTIALCOMMIT.title": "partial / uncertain — inspect state before retrying",
        "OP_STATUS_DUPLICATE.title": "already processed",
        "OP_STATUS_STALE.title": "stale request",
        "OP_STATUS_BUSY.title": "busy / blocked",
    },
    "rus": {
        "CHEST_RESULT.title": "Сундук: полностью завершено {Moved}/{Total}, требуют внимания: {Issues}. Незавершённые записи сохранены.",
        "CHEST_RESULT_ITEM.title": "{Target}: {Status}",
        "REWARD_UNLOAD.title": "Открыть колоду / разгрузиться и повторить",
        "REWARD_READY.title": "можно взять",
        "REWARD_PAYMENT_BLOCKED.title": "нужно {Cost} золота; есть {Gold}. Карта не выдана, оплата не списана",
        "REWARD_OFFER_STALE.title": "предложение больше недоступно; закройте его без покупки",
        "REWARD_PAID_READY.title": "купить за {Cost} золота после получения карты; пропуск бесплатен (без альтернатив боевой награды)",
        "REWARD_OVERLOAD.title": "нельзя: перегруз больше +10; сначала разгрузитесь",
        "REWARD_OPERATION_BLOCKED.title": "нельзя: предыдущая операция не подтверждена; сохраните лог и проверьте паспорт забега",
        "REWARD_VETO.title": "получение запрещает {Source}",
        "REWARD_CARD_STATE.title": "{Card} · вес {Weight} · {Reason}",
        "REWARD_STATE.title": "Груз {Weight}/{Capacity}. Отказ не расходует награду. Пропустить — отдельное решение.",
        "OPERATION_RESULT.title": "Операция: {Status}. Точный результат записан в лог [ShopOperation].",
        "OPERATION_BLOCKED.title": "Исход операции не подтверждён. Нажмите значок Реквиема: повторите сверку квитанций (без повторной оплаты) или подтвердите возврат к копии хоста до операции. Повреждённая попытка сохраняется отдельно. Обычный Continue не чинит частичный эффект.",
        "GRANT_DEFERRED.title": "Карта дара не получена. Её экземпляр сохранён и будет предложен снова в комнате вне боя. Сначала разгрузитесь; старая стартовая карта остаётся до получения замены.",
        "RUN_PASSPORT.title": "Паспорт забега · действительные артефакты и правила (не игровая приёмка)",
        "RUN_PASSPORT_CLOSE.title": "Закрыть паспорт",
        "RUN_ENABLED.title": "Реквием: ВКЛ",
        "RUN_DISABLED.title": "Реквием: ВЫКЛ",
        "RUN_DEGRADED.title": "Реквием: СБОЙ ПАТЧЕЙ",
        "OP_STATUS_COMMITTED.title": "завершено",
        "OP_STATUS_REJECTED.title": "отказ — без применения",
        "OP_STATUS_CANCELLED.title": "отменено",
        "OP_STATUS_ERROR.title": "ошибка — проверьте лог",
        "OP_STATUS_PARTIALCOMMIT.title": "частично / не подтверждено — проверьте состояние до повтора",
        "OP_STATUS_DUPLICATE.title": "уже обработано",
        "OP_STATUS_STALE.title": "запрос устарел",
        "OP_STATUS_BUSY.title": "занято / заблокировано",
    },
}
_INTEGRITY_TIPS["eng"].update({
    "RECOVERY_RETRY.title": "Retry receipt agreement (no new action)",
    "RECOVERY_RESTORE.title": "Restore safe run checkpoint…",
    "RECOVERY_HEADER.title": "Restore the whole run?",
    "RECOVERY_BODY.title": "Return to the pre-operation copy on floor {Floor}. ALL later changes by ALL players are cancelled. The failed state is kept separately. No candles are spent. In co-op the host opens a saved-run lobby; clients rejoin it. This is a rollback, not a promise to repair individual fields.",
    "RECOVERY_HOST.title": "Only the host can restore the run checkpoint. Ask the host to open the Requiem badge; do not repeat purchases.",
    "RECOVERY_UNAVAILABLE.title": "No compatible safe checkpoint could be restored. Original files are kept. Keep the log and use the matching mod build / a known-good host backup; there is no unsafe unlock.",
    "RECOVERY_BUSY.title": "An inventory action is still executing. Close its selection and wait for its result before restoring.",
})
_INTEGRITY_TIPS["rus"].update({
    "RECOVERY_RETRY.title": "Повторить сверку квитанций (без действия)",
    "RECOVERY_RESTORE.title": "Восстановить безопасную копию забега…",
    "RECOVERY_HEADER.title": "Вернуть весь забег к копии?",
    "RECOVERY_BODY.title": "Вернуться к копии до операции на этаже {Floor}. ВСЕ последующие изменения ВСЕХ игроков отменяются. Состояние неудачной попытки сохранится отдельно. Свечи не тратятся. В коопе хост откроет лобби загрузки; клиенты присоединятся заново. Это откат забега, а не обещание починить отдельные поля.",
    "RECOVERY_HOST.title": "Восстановить копию забега может только хост. Попросите его открыть значок Реквиема; не повторяйте покупки.",
    "RECOVERY_UNAVAILABLE.title": "Совместимую безопасную копию восстановить не удалось. Исходные файлы сохранены. Сохраните лог; используйте подходящую версию мода или проверенную копию хоста. Небезопасной разблокировки нет.",
    "RECOVERY_BUSY.title": "Операция с вещами ещё выполняется. Закройте её выбор и дождитесь результата перед восстановлением.",
})

for _lang, _tips in _INTEGRITY_TIPS.items():
    UI_TIPS[_lang].update(_tips)
    # The UI checker requires both halves even when a call only renders the title.
    for _key, _text in _tips.items():
        if _key.endswith('.title'):
            UI_TIPS[_lang].setdefault(_key.removesuffix('.title') + '.description', _text)

# Б2.3: readable sheets use the same generated table, including enum labels.
from loc_journal import SHEET_TIPS
for _lang, _tips in SHEET_TIPS.items():
    UI_TIPS[_lang].update(_tips)
