"""Тексты карт и сил Регента (docs/design/18 §1, пулы — 19). Подключается в build_loc.py."""

POOLS_CARDS = [
    # Обычные
    ("StaffStrike", "Staff Strike", "Deal {Damage:diff()} damage.\nYour next spell this turn costs 1 less ✦.",
     "Удар посохом", "Нанести {Damage:diff()} урона.\nСледующее заклинание в этот ход на 1 ✦ дешевле."),
    ("RuneOfWarding", "Rune of Warding", "Gain {Block:diff()} [gold]Block[/gold].\nGain {SpellShieldPower:diff()} [gold]Ward[/gold].",
     "Руна защиты", "Получить {Block:diff()} [gold]Блока[/gold].\nПолучить {SpellShieldPower:diff()} [gold]Оберега[/gold]."),
    ("ArcaneFocus", "Arcane Focus", "Gain {Mana:diff()} ✦.\nYour next spell this turn costs 1 less ✦.",
     "Сосредоточение", "Получить {Mana:diff()} ✦.\nСледующее заклинание в этот ход на 1 ✦ дешевле."),
    ("ManaBurn", "Mana Burn", "Deal {Damage:diff()} [gold]shock[/gold] damage.\nApply {StaticPower:diff()} [gold]Static[/gold].",
     "Выжигание магии", "Нанести {Damage:diff()} урона [gold]разрядом[/gold].\nНаложить {StaticPower:diff()} [gold]Статики[/gold]."),
    ("GrimoireStudy", "Grimoire Study", "Draw {Cards:diff()} cards.\nGain {Mana} ✦ for each spell drawn.",
     "Изучение гримуара", "Взять {Cards:diff()} карты.\n+{Mana} ✦ за каждое взятое заклинание."),
    # Необычные
    ("Concentration", "Concentration", "Whenever you play a spell, gain {ConcentrationPower:diff()} [gold]Block[/gold].",
     "Концентрация", "Каждое ваше заклинание даёт {ConcentrationPower:diff()} [gold]Блока[/gold]."),
    ("MagickaWell", "Magicka Well", "✦ regeneration +{InsightPower} this combat.",
     "Родник магии", "Прирост ✦ +{InsightPower} до конца боя."),
    ("SoulRend", "Soul Rend", "Deal {Damage:diff()} [purple]magic[/purple] damage.\nIf the enemy has [gold]Blight[/gold], deal it twice.",
     "Удар по душе", "Нанести {Damage:diff()} урона [purple]магией[/purple].\nЕсли у врага [gold]Хворь[/gold] — дважды."),
    ("ManaSurge", "Mana Surge", "Gain {Mana:diff()} ✦.\nExhaust.",
     "Прилив магии", "Получить {Mana:diff()} ✦.\nИзгнание."),
    # Редкие
    ("DualCasting", "Dual Casting", "Your next spell this turn is played twice.",
     "Двойное заклинание", "Следующее заклинание в этот ход срабатывает дважды."),
    ("ArcaneMastery", "Arcane Mastery", "The first spell you play each turn costs 1 less ✦.",
     "Мастерство заклинателя", "Первое заклинание каждого хода на 1 ✦ дешевле."),
    ("ManaStorm", "Mana Storm", "Spend all ✦. Deal {Damage:diff()} [purple]magic[/purple] damage to ALL enemies for each ✦ spent.",
     "Магическая буря", "Потратить все ✦. За каждый — {Damage:diff()} урона [purple]магией[/purple] ВСЕМ врагам."),
]

POOLS_POWERS = [
    ("SpellFocusPower", "Spell Focus", "Your next {Amount} spells this turn cost 1 less ✦.",
     "Фокус", "Следующие заклинания в этот ход ({Amount}) на 1 ✦ дешевле."),
    ("DualCastPower", "Dual Casting", "Your next {Amount} spells this turn are played twice.",
     "Двойное заклинание", "Следующие заклинания в этот ход ({Amount}) срабатывают дважды."),
    ("ConcentrationPower", "Concentration", "Whenever you play a spell, gain {Amount} [gold]Block[/gold].",
     "Концентрация", "Каждое ваше заклинание даёт {Amount} [gold]Блока[/gold]."),
    ("ArcaneMasteryPower", "Arcane Mastery", "The first {Amount} spells you play each turn cost 1 less ✦.",
     "Мастерство заклинателя", "Первые заклинания каждого хода ({Amount}) на 1 ✦ дешевле."),
]
