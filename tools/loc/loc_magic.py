"""Тексты этапа 2 «Магия и Регент»: заклинания, магические силы, Гримуар/трактаты/происхождения, Хранитель Гробницы.

Подключается из build_loc.py. Форматы кортежей такие же, как там.
"""

SPELL_NOTE = {"eng": "", "rus": ""}

# (класс, eng title, eng text, rus title, rus text)
SPELL_CARDS = [
    # Разрушение
    ("Sparks", "Sparks", "[gold]Retain[/gold].\nDeal {Damage:diff()} [purple]shock[/purple] damage. Apply {StaticPower:diff()} [gold]Static[/gold].\nWhile retained, gains 1 damage each turn.",
     "Искры", "[gold]Удержание[/gold].\nНанести {Damage:diff()} урона [purple]разрядом[/purple]. Наложить {StaticPower:diff()} [gold]Статики[/gold].\nПока в руке, +1 урона за каждый ход."),
    ("Firebolt", "Firebolt", "Deal {Damage:diff()} [red]fire[/red] damage.\nApply {BurningPower:diff()} [gold]Burning[/gold].",
     "Огненная стрела", "Нанести {Damage:diff()} урона [red]огнём[/red].\nНаложить {BurningPower:diff()} [gold]Горения[/gold]."),
    ("FireRune", "Fire Rune", "The next enemy to attack you takes {FireRunePower:diff()} [red]fire[/red] damage and half as much [gold]Burning[/gold].",
     "Огненная руна", "Следующий враг, атаковавший вас, получает {FireRunePower:diff()} урона [red]огнём[/red] и вдвое меньше [gold]Горения[/gold]."),
    ("FlameCloak", "Flame Cloak", "Gain {FireCloakPower:diff()} [gold]Flame Cloak[/gold] for 3 turns.",
     "Плащ пламени", "Получить {FireCloakPower:diff()} [gold]Плаща огня[/gold] на 3 хода."),
    ("Fireball", "Fireball", "Deal {Damage:diff()} [red]fire[/red] damage to ALL enemies.\nApply {BurningPower:diff()} [gold]Burning[/gold] to ALL enemies.",
     "Огненный шар", "Нанести {Damage:diff()} урона [red]огнём[/red] ВСЕМ врагам.\nНаложить {BurningPower:diff()} [gold]Горения[/gold] ВСЕМ врагам."),
    ("IceSpike", "Ice Spike", "Deal {Damage:diff()} [blue]frost[/blue] damage. Double against a [gold]Frozen[/gold] enemy.",
     "Ледяное копьё", "Нанести {Damage:diff()} урона [blue]холодом[/blue]. Вдвое больше по [gold]Обледеневшему[/gold] врагу."),
    ("BallLightning", "Ball Lightning", "Deal {Damage:diff()} [purple]shock[/purple] damage. Jumps to 2 more enemies, 3 less damage each jump.",
     "Шаровая молния", "Нанести {Damage:diff()} урона [purple]разрядом[/purple]. Перескакивает ещё на 2 врагов, −3 урона за перескок."),
    ("WallOfFlames", "Wall of Flames", "At the start of your next {FlameWallPower:diff()} turns, apply 6 [gold]Burning[/gold] to ALL enemies.",
     "Стена пламени", "В начале ваших следующих {FlameWallPower:diff()} ходов накладывает 6 [gold]Горения[/gold] ВСЕМ врагам."),
    ("Blizzard", "Blizzard", "Deal {Damage:diff()} [blue]frost[/blue] damage to ALL enemies.\nApply {ChillPower:diff()} [gold]Chill[/gold] to ALL enemies.",
     "Буран", "Нанести {Damage:diff()} урона [blue]холодом[/blue] ВСЕМ врагам.\nНаложить {ChillPower:diff()} [gold]Озноба[/gold] ВСЕМ врагам."),
    ("ChainLightning", "Chain Lightning", "Burn away all ✦ of an enemy mage.\nDeal {Damage:diff()} [purple]shock[/purple] damage to a random enemy {Hits} times.",
     "Цепь молний", "Выжечь всю ✦ вражеского мага.\nНанести {Damage:diff()} урона [purple]разрядом[/purple] случайному врагу {Hits} раза."),
    ("Meteor", "Meteor", "Deal {Damage:diff()} [red]fire[/red] damage, {Splash:diff()} to other enemies.\nApply {BurningPower:diff()} [gold]Burning[/gold] to ALL enemies.",
     "Метеор", "Нанести {Damage:diff()} урона [red]огнём[/red], остальным врагам — {Splash:diff()}.\nНаложить {BurningPower:diff()} [gold]Горения[/gold] ВСЕМ врагам."),
    ("IceTomb", "Ice Tomb", "Deal {Damage:diff()} [blue]frost[/blue] damage. The enemy is [gold]Frozen[/gold] and skips its intent (bosses: 5 [gold]Stagger[/gold]).",
     "Ледяная гробница", "Нанести {Damage:diff()} урона [blue]холодом[/blue]. Враг [gold]Обледеневает[/gold] и пропускает намерение (боссы: 5 [gold]Шаткости[/gold])."),
    # Восстановление
    ("HealingLight", "Healing Light", "Heal {Heal:diff()} HP.\n[gold]Exhausts[/gold] on the 3rd play in a combat.",
     "Исцеляющий свет", "Восстановить {Heal:diff()} ♥.\n[gold]Изгоняется[/gold] на 3-м применении за бой."),
    ("LesserWard", "Lesser Ward", "Gain {SpellShieldPower:diff()} [gold]Ward[/gold].",
     "Малый оберег", "Получить {SpellShieldPower:diff()} [gold]Оберега[/gold]."),
    ("Sunbeam", "Sunbeam", "Deal {Damage:diff()} [gold]light[/gold] damage (double against undead, spirits and the Abyss).",
     "Солнечный луч", "Нанести {Damage:diff()} урона [gold]светом[/gold] (вдвое по нежити, духам и Бездне)."),
    ("PoisonSpray", "Poison Spray", "Apply {PoisonPower:diff()} [gold]Poison[/gold].",
     "Ядовитый спрей", "Наложить {PoisonPower:diff()} [gold]Яда[/gold]."),
    ("TurnUndead", "Turn Undead", "Undead with {Threshold:diff()} HP or less are destroyed. Deal {Damage:diff()} [gold]light[/gold] damage to ALL other enemies; surviving undead skip their intent.",
     "Изгнание нежити", "Нежить с ♥ не выше {Threshold:diff()} уничтожается. Остальным врагам — {Damage:diff()} урона [gold]светом[/gold]; уцелевшая нежить пропускает намерение."),
    ("Pestilence", "Pestilence", "Apply {PoisonPower:diff()} [gold]Poison[/gold] and {BlightPower:diff()} [gold]Blight[/gold] to ALL enemies.",
     "Мор", "Наложить {PoisonPower:diff()} [gold]Яда[/gold] и {BlightPower:diff()} [gold]Хвори[/gold] ВСЕМ врагам."),
    ("Sunburst", "Sunburst", "Deal {Damage:diff()} [gold]light[/gold] damage to ALL enemies (double against undead).",
     "Солнечная вспышка", "Нанести {Damage:diff()} урона [gold]светом[/gold] ВСЕМ врагам (вдвое по нежити)."),
    ("GreaterWard", "Greater Ward", "Gain {SpellShieldPower:diff()} [gold]Ward[/gold]. You can't be [gold]Paralyzed[/gold] this combat.",
     "Великий оберег", "Получить {SpellShieldPower:diff()} [gold]Оберега[/gold]. До конца боя вас нельзя [gold]Парализовать[/gold]."),
    # Изменение
    ("Oakflesh", "Oakflesh", "Gain {TemporaryPlatingPower:diff()} [gold]Plating[/gold] for 3 turns.",
     "Дубовая плоть", "Получить {TemporaryPlatingPower:diff()} [gold]Лат[/gold] на 3 хода."),
    ("Telekinesis", "Telekinesis", "Deal {Damage:diff()} [purple]magic[/purple] damage.\nApply {SunderPower:diff()} [gold]Sunder[/gold].",
     "Импульс", "Нанести {Damage:diff()} урона [purple]магией[/purple].\nНаложить {SunderPower:diff()} [gold]Раскола[/gold]."),
    ("Stoneflesh", "Stoneflesh", "Gain {TemporaryPlatingPower:diff()} [gold]Plating[/gold] for 3 turns.",
     "Каменная плоть", "Получить {TemporaryPlatingPower:diff()} [gold]Лат[/gold] на 3 хода."),
    ("Paralyze", "Paralyze", "An enemy with 120 HP or less skips its intent. Bosses and the Unshakeable get 5 [gold]Stagger[/gold] instead.",
     "Паралич", "Враг с ♥ не выше 120 пропускает намерение. Боссы и Непоколебимые вместо этого получают 5 [gold]Шаткости[/gold]."),
    ("SpellAbsorption", "Absorb Spell", "Gain {SpellAbsorptionPower:diff()} [gold]Spell Absorption[/gold] until your next turn.",
     "Поглощение", "Получить {SpellAbsorptionPower:diff()} [gold]Поглощения заклинаний[/gold] до вашего следующего хода."),
    ("Dragonhide", "Dragonhide", "Gain {TemporaryPlatingPower:diff()} [gold]Plating[/gold] for 3 turns and {SpellAbsorptionPower:diff()} [gold]Spell Absorption[/gold].",
     "Драконья кожа", "Получить {TemporaryPlatingPower:diff()} [gold]Лат[/gold] на 3 хода и {SpellAbsorptionPower:diff()} [gold]Поглощения заклинаний[/gold]."),
    ("MassParalysis", "Mass Paralysis", "ALL non-boss enemies skip their intent. Bosses get 5 [gold]Stagger[/gold].",
     "Массовый паралич", "ВСЕ враги, кроме боссов, пропускают намерение. Боссы получают 5 [gold]Шаткости[/gold]."),
    # Колдовство
    ("SpectralBlade", "Spectral Blade", "Weapon attack. Deal {Damage:diff()} damage.",
     "Призрачный клинок", "Оружейная атака. Нанести {Damage:diff()} урона."),
    ("BoundBlade", "Bound Blade", "Add a [gold]Spectral Blade[/gold] to your hand.",
     "Призванный клинок", "Добавить в руку [gold]Призрачный клинок[/gold]."),
    ("ConjureSpiritWolf", "Spirit Wolf", "Summon a [gold]Spirit Wolf[/gold] with {SummonHp:diff()} HP. It deals 4 damage at the end of your turn.",
     "Призыв волка-духа", "Призвать [gold]Волка-духа[/gold] ({SummonHp:diff()} ♥). Бьёт на 4 в конце вашего хода."),
    ("ConjureFlameAtronach", "Flame Atronach", "Summon a [gold]Flame Atronach[/gold] with {SummonHp:diff()} HP (5 [red]fire[/red] damage and 2 [gold]Burning[/gold] each turn). Gain 2 [gold]Flame Cloak[/gold].",
     "Огненный атронах", "Призвать [gold]Огненного атронаха[/gold] ({SummonHp:diff()} ♥; каждый ход 5 урона [red]огнём[/red] и 2 [gold]Горения[/gold]). Получить 2 [gold]Плаща огня[/gold]."),
    ("ConjureFrostAtronach", "Frost Atronach", "Summon a [gold]Frost Atronach[/gold] with {SummonHp:diff()} HP. Attackers get 2 [gold]Chill[/gold].",
     "Ледяной атронах", "Призвать [gold]Ледяного атронаха[/gold] ({SummonHp:diff()} ♥). Атакующие его получают 2 [gold]Озноба[/gold]."),
    ("ConjureStormAtronach", "Storm Atronach", "Summon a [gold]Storm Atronach[/gold] with {SummonHp:diff()} HP. Deals 6 [purple]shock[/purple] damage to ALL enemies each turn.",
     "Грозовой атронах", "Призвать [gold]Грозового атронаха[/gold] ({SummonHp:diff()} ♥). Каждый ход 6 урона [purple]разрядом[/purple] ВСЕМ врагам."),
    ("Banish", "Banish", "Destroy an enemy minion. Otherwise deal {Damage:diff()} [purple]magic[/purple] damage.",
     "Изгнание", "Уничтожить вражеского прислужника. Иначе — {Damage:diff()} урона [purple]магией[/purple]."),
]

# (класс, eng title, eng desc, rus title, rus desc)
MAGIC_POWERS = [
    ("SpellShieldPower", "Ward", "Absorbs up to {Amount} spell and elemental damage. Burns away at the start of your turn.",
     "Оберег", "Поглощает до {Amount} урона заклинаниями и стихиями. Сгорает в начале вашего хода."),
    ("SpellAbsorptionPower", "Spell Absorption", "The next {Amount} spells that hit you are negated and restore 2 ✦. Ends at the start of your turn.",
     "Поглощение заклинаний", "Следующие {Amount} заклинаний по вам гасятся и дают 2 ✦. Спадает в начале вашего хода."),
    ("MistbloodPower", "Mistblood", "The first enemy spell that hits you this combat is negated and restores ✦.",
     "Туманная кровь", "Первое вражеское заклинание в бою по вам гасится и даёт ✦."),
    ("FireCloakPower", "Flame Cloak", "Attackers take {Amount} [red]fire[/red] damage and get [gold]Burning[/gold].",
     "Плащ огня", "Атакующие получают {Amount} урона [red]огнём[/red] и [gold]Горение[/gold]."),
    ("FrostCloakPower", "Frost Cloak", "Attackers take {Amount} [blue]frost[/blue] damage and get [gold]Chill[/gold].",
     "Плащ холода", "Атакующие получают {Amount} урона [blue]холодом[/blue] и [gold]Озноб[/gold]."),
    ("ShockCloakPower", "Lightning Cloak", "Attackers take {Amount} [purple]shock[/purple] damage and get [gold]Static[/gold].",
     "Плащ молний", "Атакующие получают {Amount} урона [purple]разрядом[/purple] и [gold]Статику[/gold]."),
    ("BlightPower", "Blight", "Can't heal. While [gold]Poisoned[/gold], takes {Amount} extra damage each turn.",
     "Хворь", "Не лечится. Пока отравлен — ещё {Amount} урона каждый ход."),
    ("TemporaryPlatingPower", "Conjured Plating", "{Amount} of your [gold]Plating[/gold] fades after 3 turns.",
     "Наколдованные Латы", "{Amount} ваших [gold]Лат[/gold] исчезнут через 3 хода."),
    ("ParalysisWardPower", "Steadfast Mind", "Can't be [gold]Paralyzed[/gold].",
     "Твёрдый разум", "Нельзя [gold]Парализовать[/gold]."),
    ("FireRunePower", "Fire Rune", "The next enemy to attack takes {Amount} [red]fire[/red] damage.",
     "Огненная руна", "Следующий атаковавший враг получит {Amount} урона [red]огнём[/red]."),
    ("FlameWallPower", "Wall of Flames", "At the start of your turn, apply 6 [gold]Burning[/gold] to ALL enemies. {Amount} turns left.",
     "Стена пламени", "В начале вашего хода — 6 [gold]Горения[/gold] ВСЕМ врагам. Осталось ходов: {Amount}."),
    ("FearPower", "Fear", "Deals 50% less damage. {Amount} turns left.",
     "Страх", "Наносит на 50 % меньше урона. Осталось ходов: {Amount}."),
    ("SpiritWolfPower", "Spirit Wolf", "Summon with {Amount} HP: takes enemy attacks first, deals 4 damage at the end of your turn.",
     "Волк-дух", "Призыв ({Amount} ♥): первым принимает атаки врагов, бьёт на 4 в конце вашего хода."),
    ("FlameAtronachPower", "Flame Atronach", "Summon with {Amount} HP: takes attacks first, deals 5 [red]fire[/red] damage and 2 [gold]Burning[/gold] at the end of your turn.",
     "Огненный атронах", "Призыв ({Amount} ♥): первым принимает атаки, в конце вашего хода — 5 урона [red]огнём[/red] и 2 [gold]Горения[/gold]."),
    ("FrostAtronachPower", "Frost Atronach", "Summon with {Amount} HP: takes attacks first (attackers get 2 [gold]Chill[/gold]), deals 4 [blue]frost[/blue] damage at the end of your turn.",
     "Ледяной атронах", "Призыв ({Amount} ♥): первым принимает атаки (атакующим — 2 [gold]Озноба[/gold]), в конце вашего хода — 4 урона [blue]холодом[/blue]."),
    ("StormAtronachPower", "Storm Atronach", "Summon with {Amount} HP: takes attacks first, deals 6 [purple]shock[/purple] damage to ALL enemies at the end of your turn.",
     "Грозовой атронах", "Призыв ({Amount} ♥): первым принимает атаки, в конце вашего хода — 6 урона [purple]разрядом[/purple] ВСЕМ врагам."),
    ("EnemyManaPower", "Mana", "Spells cost 2–3 ✦; regains 2 ✦ per turn. Without ✦ the mage [gold]Focuses[/gold] instead of casting.",
     "Мана", "Заклинания стоят 2–3 ✦; +2 ✦ за ход. Без ✦ маг [gold]Сосредотачивается[/gold] вместо заклинания."),
    ("SpectralGuardPower", "Spectral Knights", "Takes half damage while the knights stand ({Amount} HP left; light and fire hit them twice as hard). Melee attackers get 2 [gold]Stagger[/gold].",
     "Рыцари-духи", "Получает вдвое меньше урона, пока стоят рыцари (осталось {Amount} ♥; свет и огонь бьют их вдвое). Ударившие в ближнем бою получают 2 [gold]Шаткости[/gold]."),
]

# (класс, eng title, eng desc, rus title, rus desc, flavor eng, flavor rus) — без строки про вес.
MAGIC_RELICS = [
    ("Grimoire", "Grimoire", "Holds your mastery of the five schools of magic. Spells above your mastery cost more ✦; far above — only from scrolls.",
     "Гримуар", "Хранит владение пятью школами магии. Заклинания выше владения дороже по ✦, намного выше — только со свитка.",
     "Margins full of crossed-out formulae.", "Поля исписаны зачёркнутыми формулами."),
    ("TreatiseOfDestruction", "Treatise: Destruction", "On pickup: +1 mastery of Destruction.", "Трактат: Разрушение", "При получении: +1 владения Разрушением.",
     "Smells of ozone and soot.", "Пахнет озоном и сажей."),
    ("TreatiseOfRestoration", "Treatise: Restoration", "On pickup: +1 mastery of Restoration.", "Трактат: Восстановление", "При получении: +1 владения Восстановлением.",
     "Pages warm to the touch.", "Страницы тёплые на ощупь."),
    ("TreatiseOfAlteration", "Treatise: Alteration", "On pickup: +1 mastery of Alteration.", "Трактат: Изменение", "При получении: +1 владения Изменением.",
     "The letters rearrange when you blink.", "Буквы переставляются, стоит моргнуть."),
    ("TreatiseOfConjuration", "Treatise: Conjuration", "On pickup: +1 mastery of Conjuration.", "Трактат: Колдовство", "При получении: +1 владения Колдовством.",
     "Something on the other side reads along.", "С той стороны кто-то читает вместе с вами."),
    ("TreatiseOfIllusion", "Treatise: Illusion", "On pickup: +1 mastery of Illusion.", "Трактат: Иллюзия", "При получении: +1 владения Иллюзией.",
     "You are sure it had more pages.", "Вы уверены, что страниц было больше."),
    ("HighbornOrigin", "Highborn", "+1 Destruction mastery. Max ✦ +3, Max HP −5. The first spell each turn costs 1 less ✦. [gold]Magic weakness I[/gold].",
     "Златокровные", "+1 владения Разрушением. Пул ✦ +3, ♥ −5. Первое заклинание каждого хода на 1 ✦ дешевле. [gold]Уязвимость к магии I[/gold].",
     "Starlight runs thinner in other veins.", "В чужих жилах звёздный свет жиже."),
    ("MistbornOrigin", "Mistborn", "+1 Restoration mastery. Max ◆ +1. [gold]Magic resistance I[/gold]. The first enemy spell each combat is absorbed.",
     "Туманные", "+1 владения Восстановлением. Пул ◆ +1. [gold]Сопротивление магии I[/gold]. Первое вражеское заклинание в бою поглощается.",
     "The fog remembers every oath.", "Туман помнит каждую клятву."),
    ("AshbornOrigin", "Ashborn", "Max HP +5. [gold]Fire resistance I[/gold]. [gold]Heirs of the Tribunal[/gold]: gifts of Treachery, the Web Weaver "
     "and the Dusk Star cost no righteousness, one of them is always offered at the Dark Shrine, and their Price is halved "
     "(summons take +25% damage, resting heals 15% less, one star mark fewer).",
     "Пеплорождённые", "♥ +5. [gold]Сопротивление огню I[/gold]. [gold]Наследники Тройственных[/gold]: дары Измены, Паутины и Сумеречной Звезды "
     "не стоят праведности, один из них всегда есть в Тёмном святилище, а их Цена вдвое мягче "
     "(призывы получают +25 % урона, отдых лечит на 15 % меньше, звёздных меток на одну меньше).",
     "Born under a red mountain, raised on its ash.", "Рождены под красной горой, вскормлены её пеплом."),
]

MONSTERS = {
    "TOMB_GUARDIAN": {
        "eng": {"name": "Tomb Guardian", "moves": {"RAISE_KNIGHTS": "Raise the Knights", "RERAISE_KNIGHTS": "Call the Fallen", "STAFF_DRAIN": "Staff of Thirst", "SOUL_FLAME": "Soul Flame"}},
        "rus": {"name": "Хранитель Гробницы", "moves": {"RAISE_KNIGHTS": "Поднять рыцарей", "RERAISE_KNIGHTS": "Зов павших", "STAFF_DRAIN": "Посох жажды", "SOUL_FLAME": "Пламя душ"}},
    },
}

ENCOUNTERS = {
    "TOMB_GUARDIAN_ELITE": {"eng": "Tomb Guardian", "rus": "Хранитель Гробницы"},
}

SETTINGS = {
    "eng": {
        "HERO_SECTION.title": "Heroes",
        "REGENT_ORIGIN.title": "Regent origin",
        "REGENT_ORIGIN.hover.title": "Regent origin",
        "REGENT_ORIGIN.hover.desc": "Until the origin select screen exists, the origin is taken from here.",
        "REGENT_ORIGIN.Random": "Random", "REGENT_ORIGIN.Highborn": "Highborn", "REGENT_ORIGIN.Mistborn": "Mistborn",
        "REGENT_ORIGIN.Ashborn": "Ashborn", "REGENT_ORIGIN.None": "None",
    },
    "rus": {
        "HERO_SECTION.title": "Герои",
        "REGENT_ORIGIN.title": "Происхождение Регента",
        "REGENT_ORIGIN.hover.title": "Происхождение Регента",
        "REGENT_ORIGIN.hover.desc": "Пока нет экрана выбора происхождения, оно берётся отсюда.",
        "REGENT_ORIGIN.Random": "Случайно", "REGENT_ORIGIN.Highborn": "Златокровные", "REGENT_ORIGIN.Mistborn": "Туманные",
        "REGENT_ORIGIN.Ashborn": "Пеплорождённые", "REGENT_ORIGIN.None": "Без происхождения",
    },
}
