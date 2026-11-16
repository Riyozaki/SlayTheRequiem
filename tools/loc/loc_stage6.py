"""Тексты этапа 6: трансформации (Праведник, вампир, оборотень, Договор → Вестник → Лич, Заводное сердце), Пути Героя
(Сущности, Знаки, карты), события форм, Дух божества, зачарование карт, значок «Ни одной свечи» (docs/design/06, 07 §6.2)."""

PREFIX = "SLAYTHEREQUIEM-"

# ───────────── карты: (класс, eng title, eng desc, rus title, rus desc) ─────────────
FORM_CARDS = [
    ("Smite", "Smite", "Deal {Damage:diff()} [gold]light[/gold] damage.\nUndead and the Abyss: also apply {StaggerPower:diff()} [gold]Stagger[/gold].",
     "Кара", "Нанести {Damage:diff()} урона [gold]светом[/gold].\nНежити и Бездне — ещё {StaggerPower:diff()} [gold]Шаткости[/gold]."),
    ("Bite", "Bite", "Deal {Damage:diff()} damage and heal the damage dealt.\n[gold]Feeding[/gold]: Thirst drops to 0.\nExhaust.",
     "Укус", "Нанести {Damage:diff()} урона и вылечить нанесённое.\n[gold]Кормление[/gold]: Жажда падает до 0.\nИзгнание."),
    ("BloodLordStance", "Blood Lord", "Enter or leave the Blood Lord form: your attacks apply 1 [gold]Blight[/gold] and heal 25% of unblocked damage.",
     "Кровавый лорд", "Войти в форму Кровавого лорда или выйти из неё: атаки накладывают 1 [gold]Хвори[/gold] и лечат 25 % непогашенного урона."),
    ("MoonCall", "Moon Call", "[gold]Transform[/gold] into the beast for {Turns:diff()} turns.\nExhaust.",
     "Зов луны", "[gold]Обращение[/gold] в зверя на {Turns:diff()} хода.\nИзгнание."),
    ("Claws", "Claws", "Deal {Damage:diff()} damage.\nEthereal. Exhaust.", "Когти", "Нанести {Damage:diff()} урона.\nЭфирная. Изгнание."),
    ("Pounce", "Pounce", "Deal {Damage:diff()} damage. Gain {Block:diff()} [gold]Block[/gold].\nEthereal. Exhaust.",
     "Прыжок", "Нанести {Damage:diff()} урона. Получить {Block:diff()} [gold]Блока[/gold].\nЭфирная. Изгнание."),
    ("Roar", "Roar", "Apply {WeakPower:diff()} [gold]Weak[/gold] and {StaggerPower:diff()} [gold]Stagger[/gold] to ALL enemies.\nEthereal. Exhaust.",
     "Рык", "Наложить {WeakPower:diff()} [gold]Слабости[/gold] и {StaggerPower:diff()} [gold]Шаткости[/gold] ВСЕМ врагам.\nЭфирная. Изгнание."),
    ("Devour", "Devour", "Eat a fallen enemy: heal {Heal} and extend the beast form by 1 turn.\nEthereal. Exhaust.",
     "Пожрать", "Съесть павшего врага: +{Heal} ♥ и +1 ход формы зверя.\nЭфирная. Изгнание."),
    ("HeraldStanceCard", "Herald's Stance", "Switch the [gold]Stance[/gold]: Plague → Discord → Decay.",
     "Стойка Вестника", "Сменить [gold]Стойку[/gold]: Чума → Раздор → Распад."),
    ("Windup", "Wind-up", "Add an elemental sphere (Steam → Spark → Rime, up to {Max}).",
     "Подзавод", "Добавить сферу стихии (Пар → Искра → Иней, до {Max})."),
]

PATH_CARDS = [
    # Паладин
    ("HolyStrike", "Holy Strike", "Deal {Damage:diff()} [gold]light[/gold] damage.", "Святой удар", "Нанести {Damage:diff()} урона [gold]светом[/gold]."),
    ("AegisOfFaith", "Aegis of Faith", "Gain {Block:diff()} [gold]Block[/gold] and {SpellShieldPower:diff()} [gold]Ward[/gold].",
     "Эгида веры", "Получить {Block:diff()} [gold]Блока[/gold] и {SpellShieldPower:diff()} [gold]Оберега[/gold]."),
    ("Consecrate", "Consecrate", "Deal {Damage:diff()} [gold]light[/gold] damage to ALL enemies. Undead and the Abyss: {StaggerPower:diff()} [gold]Stagger[/gold].",
     "Освящение", "Нанести {Damage:diff()} урона [gold]светом[/gold] ВСЕМ врагам. Нежити и Бездне — {StaggerPower:diff()} [gold]Шаткости[/gold]."),
    # Чёрный рыцарь
    ("DreadStrike", "Dread Strike", "Deal {Damage:diff()} damage. Apply {WeakPower:diff()} [gold]Weak[/gold].",
     "Удар ужаса", "Нанести {Damage:diff()} урона. Наложить {WeakPower:diff()} [gold]Слабости[/gold]."),
    ("SoulDrain", "Soul Drain", "Deal {Damage:diff()} damage and heal the damage dealt.", "Высасывание", "Нанести {Damage:diff()} урона и вылечить нанесённое."),
    ("DarkAura", "Dark Aura", "ALL enemies lose {Strength:diff()} [gold]Strength[/gold].\nExhaust.",
     "Тёмная аура", "ВСЕ враги теряют {Strength:diff()} [gold]Силы[/gold].\nИзгнание."),
    # Чемпион
    ("Shieldbreaker", "Shieldbreaker", "Deal {Damage:diff()} damage. Apply {SunderPower:diff()} [gold]Sunder[/gold].",
     "Сокрушитель щитов", "Нанести {Damage:diff()} урона. Наложить {SunderPower:diff()} [gold]Раскола[/gold]."),
    ("RallyCry", "Rally Cry", "Gain {Block:diff()} [gold]Block[/gold].", "Боевой клич", "Получить {Block:diff()} [gold]Блока[/gold]."),
    ("Overpower", "Overpower", "Deal {Damage:diff()} damage. Apply {StaggerPower:diff()} [gold]Stagger[/gold].",
     "Натиск", "Нанести {Damage:diff()} урона. Наложить {StaggerPower:diff()} [gold]Шаткости[/gold]."),
    # Варвар
    ("Rampage", "Rampage", "Deal {Damage:diff()} damage twice.", "Буйство", "Дважды нанести {Damage:diff()} урона."),
    ("BloodFury", "Blood Fury", "Lose {HpLoss} HP. Gain {StrengthPower:diff()} [gold]Strength[/gold].\nExhaust.",
     "Кровавое неистовство", "Потерять {HpLoss} ♥. Получить {StrengthPower:diff()} [gold]Силы[/gold].\nИзгнание."),
    ("Whirlwind", "Whirlwind", "Deal {Damage:diff()} damage to ALL enemies.", "Размах", "Нанести {Damage:diff()} урона ВСЕМ врагам."),
    # Монах
    ("PalmStrike", "Palm Strike", "Deal {Damage:diff()} damage. Apply {StaggerPower:diff()} [gold]Stagger[/gold].",
     "Удар ладонью", "Нанести {Damage:diff()} урона. Наложить {StaggerPower:diff()} [gold]Шаткости[/gold]."),
    ("FlurryOfBlows", "Flurry of Blows", "Deal {Damage:diff()} damage 4 times.", "Град ударов", "4 раза нанести {Damage:diff()} урона."),
    ("InnerCalm", "Inner Calm", "Gain {Block:diff()} [gold]Block[/gold] and {DodgePower:diff()} [gold]Dodge[/gold].",
     "Внутренний покой", "Получить {Block:diff()} [gold]Блока[/gold] и {DodgePower:diff()} [gold]Уворота[/gold]."),
    # Ассасин
    ("PathGarrote", "Assassin's Garrote", "Deal {Damage:diff()} damage. Apply {BleedPower:diff()} [gold]Bleed[/gold].",
     "Удавка ассасина", "Нанести {Damage:diff()} урона. Наложить {BleedPower:diff()} [gold]Кровотечения[/gold]."),
    ("Vanish", "Vanish", "Gain {DodgePower:diff()} [gold]Dodge[/gold].\nExhaust.", "Исчезновение", "Получить {DodgePower:diff()} [gold]Уворота[/gold].\nИзгнание."),
    ("DeathMark", "Death Mark", "Apply {VulnerablePower:diff()} [gold]Vulnerable[/gold] and [gold]Broken[/gold].",
     "Метка смерти", "Наложить {VulnerablePower:diff()} [gold]Уязвимости[/gold] и [gold]Сломлен[/gold]."),
    # Губитель
    ("PlagueTouch", "Plague Touch", "Deal {Damage:diff()} damage. Apply {PoisonPower:diff()} [gold]Poison[/gold] and 1 [gold]Blight[/gold].",
     "Касание чумы", "Нанести {Damage:diff()} урона. Наложить {PoisonPower:diff()} [gold]Яда[/gold] и 1 [gold]Хвори[/gold]."),
    ("Miasma", "Miasma", "Apply {PoisonPower:diff()} [gold]Poison[/gold] to ALL enemies.", "Миазмы", "Наложить {PoisonPower:diff()} [gold]Яда[/gold] ВСЕМ врагам."),
    ("Wither", "Wither", "Double the target's [gold]Poison[/gold].\nExhaust.", "Иссушение", "Удвоить [gold]Яд[/gold] на цели.\nИзгнание."),
    # Мистический лучник
    ("ArcaneShot", "Arcane Shot", "Deal {Damage:diff()} [purple]magic[/purple] damage.", "Чародейский выстрел", "Нанести {Damage:diff()} урона [purple]магией[/purple]."),
    ("ElementalVolley", "Elemental Volley", "Deal {Damage:diff()} [red]fire[/red] damage to ALL enemies. Apply {BurningPower:diff()} [gold]Burning[/gold] to ALL enemies.",
     "Стихийный залп", "Нанести {Damage:diff()} урона [red]огнём[/red] ВСЕМ врагам. Наложить {BurningPower:diff()} [gold]Горения[/gold] ВСЕМ."),
    ("FocusedAim", "Focused Aim", "Gain {Mana:diff()} ✦.\nExhaust.", "Прицел", "Получить {Mana:diff()} ✦.\nИзгнание."),
    # Повелитель стихии — подписные заклинания этапа 2 (тексты в loc_magic.py)
    # Боевой маг
    ("SpellBlade", "Spellblade", "Deal {Damage:diff()} damage, then {Fire:diff()} [red]fire[/red] damage.",
     "Клинок-заклинание", "Нанести {Damage:diff()} урона, затем {Fire:diff()} урона [red]огнём[/red]."),
    ("RuneArmor", "Rune Armor", "Gain {Block:diff()} [gold]Block[/gold] and {FrostCloakPower:diff()} [gold]Frost Cloak[/gold].",
     "Рунная броня", "Получить {Block:diff()} [gold]Блока[/gold] и {FrostCloakPower:diff()} [gold]Плаща холода[/gold]."),
    ("ArcaneCharge", "Arcane Charge", "Deal {Damage:diff()} [blue]shock[/blue] damage.", "Чародейский рывок", "Нанести {Damage:diff()} урона [blue]разрядом[/blue]."),
    # Отражатель
    ("MirrorWard", "Mirror Ward", "Gain {SpellShieldPower:diff()} [gold]Ward[/gold].", "Зеркальный оберег", "Получить {SpellShieldPower:diff()} [gold]Оберега[/gold]."),
    ("Rebound", "Rebound", "Gain {Block:diff()} [gold]Block[/gold] and {SpellAbsorptionPower:diff()} [gold]Spell Absorption[/gold].",
     "Отдача", "Получить {Block:diff()} [gold]Блока[/gold] и {SpellAbsorptionPower:diff()} [gold]Поглощения заклинаний[/gold]."),
    ("PrismBolt", "Prism Bolt", "Deal {Damage:diff()} [purple]magic[/purple] damage plus your [gold]Ward[/gold].",
     "Призменная стрела", "Нанести {Damage:diff()} урона [purple]магией[/purple] плюс ваш [gold]Оберег[/gold]."),
    # Медиум
    ("SpiritCall", "Spirit Call", "Summon a spirit wolf with {Hp:diff()} HP.", "Зов духов", "Призвать волка-духа с {Hp:diff()} ♥."),
    ("Seance", "Séance", "Gain {Block:diff()} [gold]Block[/gold] and {Mana} ✦.", "Сеанс", "Получить {Block:diff()} [gold]Блока[/gold] и {Mana} ✦."),
    ("EtherealLink", "Ethereal Link", "Your summons gain {Hp:diff()} HP.", "Эфирная связь", "Ваши призывы получают {Hp:diff()} ♥."),
]

# Пути: (суффикс, eng имя, rus имя, eng Сущность, rus Сущность, класс Знака, eng Знак, rus Знак, eng текст Знака, rus текст Знака)
PATHS = [
    ("Paladin", "Paladin", "Паладин",
     "[gold]Plating[/gold] +1; undead hit you for 1 less per hit; [gold]Smite[/gold] costs no ✦.",
     "[gold]Латы[/gold] +1; нежить бьёт вас на 1 слабее за удар; [gold]Кара[/gold] без ✦.",
     "RadiantCuirass", "Radiant Cuirass", "Сияющий нагрудник", "Heavy body armor IV. Start each combat with 4 [gold]Ward[/gold].",
     "Тяжёлая броня тела IV. В начале боя 4 [gold]Оберега[/gold]."),
    ("BlackKnight", "Black Knight", "Чёрный рыцарь",
     "Aura of Fear: enemies lose 1 [gold]Strength[/gold] at the start of every 2nd turn. Your attacks heal 1 per hit.",
     "Аура страха: враги теряют 1 [gold]Силы[/gold] в начале каждого 2-го хода. Ваши атаки лечат 1 за удар.",
     "SpikedHelm", "Spiked Helm", "Шипастый шлем", "Heavy helm IV. Start each combat with 2 [gold]Riposte[/gold].",
     "Тяжёлый шлем IV. В начале боя 2 [gold]Контрудара[/gold]."),
    ("Champion", "Champion", "Чемпион",
     "Heavy attacks (2+ ◆) apply 2 [gold]Stagger[/gold] (half to the [gold]Unshakeable[/gold]).",
     "Тяжёлые атаки (от 2 ◆) накладывают 2 [gold]Шаткости[/gold] (Непоколебимым — половину).",
     "RegimentBanner", "Regiment Banner", "Знамя полка", "Cloak. [gold]Poise[/gold] +1.", "Плащ. [gold]Стойкость[/gold] +1."),
    ("Barbarian", "Barbarian", "Варвар",
     "Rage: below 50% HP your attacks deal 50% more damage and cost no ◆.",
     "Ярость: при ♥ ниже 50 % атаки +50 % урона и не тратят ◆.",
     "HornedHelm", "Horned Helm", "Рогатый шлем", "Light helm. +1 max ◆.", "Лёгкий шлем. +1 пул ◆."),
    ("Monk", "Monk", "Монах",
     "Unarmed: weapon attacks hit as 4 + level/2; every 3rd applies 3 [gold]Stagger[/gold].",
     "Без оружия: «оружейные» атаки бьют как 4 + уровень/2; каждая 3-я — 3 [gold]Шаткости[/gold].",
     "MonkWraps", "Hand Wraps", "Обмотки", "Hands. Start each combat with 1 [gold]Dodge[/gold].", "Руки. В начале боя 1 [gold]Уворот[/gold]."),
    ("Assassin", "Assassin", "Ассасин",
     "Shadow Step: your first attack each turn strikes from behind for double damage.",
     "Шаг тени: первая атака каждого хода — «в спину», урон ×2.",
     "FacelessMask", "Faceless Mask", "Маска без лица", "Head. Start each combat with 1 [gold]Dodge[/gold]; +1 ◆ regeneration.",
     "Голова. В начале боя 1 [gold]Уворот[/gold]; +1 прирост ◆."),
    ("Blightbringer", "Blightbringer", "Губитель",
     "Your [gold]Poison[/gold] +1 stack; Poison on enemies with [gold]Blight[/gold] does not decay.",
     "Ваш [gold]Яд[/gold] +1 стак; Яд на врагах с [gold]Хворью[/gold] не убывает.",
     "VialPendant", "Vial Pendant", "Флакон-кулон", "Amulet. Poison Resistance I.", "Амулет. Сопротивление ядам I."),
    ("MysticArcher", "Mystic Archer", "Мистический лучник",
     "Bow and crossbow shots apply the element of your last spell (2 stacks).",
     "Выстрелы из лука и арбалета накладывают стихию последнего заклинания (2 стака).",
     "RunicQuiver", "Runic Quiver", "Рунный колчан", "Cloak. +2 max ✦.", "Плащ. +2 пул ✦."),
    ("ElementLord", "Element Lord", "Повелитель стихии",
     "Choose an element: its signature spell joins your deck; your statuses of that element +1 stack.",
     "Выбор стихии: её подписное заклинание — в колоду; ваши статусы этой стихии +1 стак.",
     "ElementStaff", "Elemental Staff", "Посох стихии", "Staff. +3 max ✦, +1 ✦ regeneration.", "Посох. +3 пул ✦, +1 прирост ✦."),
    ("Battlemage", "Battlemage", "Боевой маг",
     "Any elemental Cloak puts on the other two as well; your attacks add 2 elemental damage (fire → frost → shock).",
     "Любой Плащ стихии накидывает и два других; ваши атаки добавляют 2 урона стихией (огонь → холод → разряд).",
     "RunicBracers", "Runic Bracers", "Рунные наручи", "Light gloves. Start each combat with 2 [gold]Flame Cloak[/gold].",
     "Лёгкие перчатки. В начале боя 2 [gold]Плаща огня[/gold]."),
    ("Reflector", "Reflector", "Отражатель",
     "[gold]Ward[/gold] reflects 50% of the spell damage it absorbs back at the caster.",
     "[gold]Оберег[/gold] отражает 50 % поглощённого урона заклинаний в заклинателя.",
     "MirrorShield", "Mirror Shield", "Зеркальный щит", "Shield. Start each combat with 6 [gold]Ward[/gold].",
     "Щит. В начале боя 6 [gold]Оберега[/gold]."),
    ("Medium", "Medium", "Медиум",
     "+1 summon slot; your summons deal 2 more damage.", "+1 слот призыва; призывы +2 урона.",
     "SpiritBell", "Spirit Bell", "Колокольчик духов", "Amulet. Summon a spirit wolf (8 HP) at the start of each combat.",
     "Амулет. В начале боя — волк-дух (8 ♥)."),
    ("Reborn", "Reborn", "Перерождённый",
     "No bonus. Once: choose a new background and reset your stat points (spend them again at camp).",
     "Без бонуса. Единожды: новая предыстория и сброс очков характеристик (распределить заново в лагере).",
     None, None, None, None, None),
]

PATH_PICKS = [(f"PickPath{s}", en, f"Path of the {en}. {ee}" if s != "Reborn" else f"{en}. {ee}",
               rn, f"Путь: {rn}. {re_}") for s, en, rn, ee, re_, *_ in PATHS]

OTHER_PICKS = [
    ("PickBeastHunter", "Fang", "Node {Value}: I +2 beast damage, II beast attacks apply 2 [gold]Bleed[/gold], III +2 more damage.",
     "Клык", "Узел {Value}: I +2 урона зверя, II звериные атаки — 2 [gold]Кровотечения[/gold], III ещё +2 урона."),
    ("PickBeastStalker", "Long Night", "Node {Value}: I +1 form turn, II a turn per 2 hits instead of 3, III +1 more form turn.",
     "Долгая ночь", "Узел {Value}: I +1 ход формы, II ход за 2 удара вместо 3, III ещё +1 ход."),
    ("PickBeastAlpha", "Alpha", "Node {Value}: a wolf joins each Transformation (8/12/16 HP); III +1 summon slot.",
     "Альфа", "Узел {Value}: при Обращении — волк (8/12/16 ♥); III +1 слот призыва."),
    ("PickCardEnchantFlaming", "Flaming", "Attack: also apply 3 [gold]Burning[/gold].", "Пламенная", "Атака: ещё 3 [gold]Горения[/gold]."),
    ("PickCardEnchantFrost", "Frosty", "Attack: also apply 2 [gold]Chill[/gold].", "Морозная", "Атака: ещё 2 [gold]Озноба[/gold]."),
    ("PickCardEnchantStorm", "Stormy", "Attack: burns 2 ✦ of the target.", "Грозовая", "Атака: выжигает 2 ✦ цели."),
    ("PickCardEnchantLightened", "Lightened", "Any card: weight −1 (min 0).", "Облегчённая", "Любая карта: вес −1 (мин. 0)."),
    ("PickCardEnchantBreathing", "Breathing", "Attack: costs 1 less ◆.", "Дыхательная", "Атака: на 1 ◆ дешевле."),
    ("PickCardEnchantDouble", "Double", "Two enchantments on one card ([gold]Enchanting[/gold] node).",
     "Двойная", "Две чары на одной карте (узел [gold]Зачарование[/gold])."),
]

STAGE6_CARDS = FORM_CARDS + PATH_CARDS + PATH_PICKS + OTHER_PICKS

STAGE6_POWERS = [
    ("BloodLordStancePower", "Blood Lord", "Your attacks apply 1 [gold]Blight[/gold] and heal 25% of unblocked damage.",
     "Кровавый лорд", "Ваши атаки накладывают 1 [gold]Хвори[/gold] и лечат 25 % непогашенного урона."),
    ("BeastFormPower", "Beast Form", "{Amount} turn(s) left. Only beast cards; attacks +4. Every 3 hits on enemies extend the form by 1 turn.",
     "Форма зверя", "Осталось ходов: {Amount}. Только звериные карты; атаки +4. Каждые 3 удара по врагам — +1 ход формы."),
    ("SteamOrbPower", "Steam Sphere", "{Amount} sphere(s): at the end of your turn each applies 3 [gold]Burning[/gold] to a random enemy.",
     "Сфера пара", "Сфер: {Amount}; в конце хода каждая — 3 [gold]Горения[/gold] случайному врагу."),
    ("SparkOrbPower", "Spark Sphere", "{Amount} sphere(s): at the end of your turn each applies 1 [gold]Static[/gold] and deals 3 shock damage to a random enemy.",
     "Сфера искры", "Сфер: {Amount}; в конце хода каждая — 1 [gold]Статики[/gold] и 3 урона разрядом случайному врагу."),
    ("RimeOrbPower", "Rime Sphere", "{Amount} sphere(s): at the end of your turn each applies 2 [gold]Chill[/gold] to a random enemy.",
     "Сфера инея", "Сфер: {Amount}; в конце хода каждая — 2 [gold]Озноба[/gold] случайному врагу."),
]

_APOSTATE = {"eng": " While you have this form, you are an [gold]Apostate[/gold].", "rus": " Пока форма с вами — вы [gold]Отступник[/gold]."}

FORM_RELICS = [
    ("RighteousForm", "Righteous",
     "Halo: once per combat below 50% HP — heal 10 and gain 10 [gold]Ward[/gold]. Vow: no Abyssal items (taking one ends the form). "
     "Each act boss defeated under the vow — a step: 1 Aura of Fortitude ([gold]Plating[/gold] +1), 2 Heavenly Shield (opens the Divine Spirit). "
     "Silent while you are not Righteous. Shown number — step.",
     "Праведник",
     "Ореол: раз за бой при ♥ ниже 50 % — +10 ♥ и 10 [gold]Оберега[/gold]. Обет: никаких Бездонных вещей (взяв такую, теряете форму). "
     "Босс акта, побеждённый под обетом, — ступень: 1 Аура стойкости ([gold]Латы[/gold] +1), 2 Небесный щит (открывает Дух божества). "
     "Молчит, пока вы не Праведник. Число на иконке — ступень.",
     "Light does not ask; it answers.", "Свет не спрашивает — он отвечает."),
    ("VampireForm", "Child of the Night",
     "Thirst {Thirst}/4, +1 for each combat without feeding ([gold]Bite[/gold]). 0: Regeneration 1, fire vulnerability I. "
     "2–3: attacks +2, fire and light ×1.5, healing −50%. 4: attacks +4, lifesteal 25%, fire and light ×2, heal only by feeding. "
     "Night Wings: once per combat below 25% HP — [gold]Intangible[/gold] and a free turn. Feedings: {Feedings} (3 — Blood Lord)." + _APOSTATE["eng"],
     "Дитя Ночи",
     "Жажда {Thirst}/4, +1 за каждый бой без кормления ([gold]Укус[/gold]). 0: Регенерация 1, уязвимость к огню I. "
     "2–3: атаки +2, огонь и свет ×1.5, лечение −50 %. 4: атаки +4, вампиризм 25 %, огонь и свет ×2, лечение только кормлением. "
     "Крылья ночи: раз за бой при ♥ ниже 25 % — [gold]Неосязаемость[/gold] и бесплатный ход. Кормлений: {Feedings} (3 — Кровавый лорд)." + _APOSTATE["rus"],
     "The night is long, and so is hunger.", "Ночь длинна, как и голод."),
    ("WerewolfForm", "Beast Bearer",
     "[gold]Moon Call[/gold] — transform. Outside the form Regeneration does not work; rest heals 25% less. "
     "Every 2 Transformations in won combats — a branch node at camp: Fang {Hunter}/3, Long Night {Stalker}/3, Alpha {Alpha}/3 (pending: {Pending})." + _APOSTATE["eng"],
     "Носитель Зверя",
     "[gold]Зов луны[/gold] — Обращение. Вне формы Регенерация не действует, отдых лечит на 25 % меньше. "
     "Каждые 2 Обращения в выигранных боях — узел ветки в лагере: Клык {Hunter}/3, Долгая ночь {Stalker}/3, Альфа {Alpha}/3 (ждут: {Pending})." + _APOSTATE["rus"],
     "The moon always finds you.", "Луна всегда тебя находит."),
    ("DarkPactForm", "Dark Pact",
     "+4 max ✦; summoning spells cost 1 less ✦; −8 max HP. Defeat the Tomb Guardian to become a Herald." + _APOSTATE["eng"],
     "Тёмный договор",
     "+4 пул ✦; заклинания призыва на 1 ✦ дешевле; −8 макс. ♥. Победите Хранителя Гробницы, чтобы стать Вестником." + _APOSTATE["rus"],
     "The urn remembers every name.", "Урна помнит каждое имя."),
    ("HeraldForm", "Herald",
     "Pact: +4 max ✦, summoning −1 ✦, −8 max HP. [gold]Stance[/gold]: your spells apply Plague (2 [gold]Blight[/gold]), Discord (−1 [gold]Strength[/gold]) "
     "or Decay (1 [gold]Sunder[/gold]). Wins as Herald: {Wins}/{Required} (the Phylactery event — Lich)." + _APOSTATE["eng"],
     "Вестник",
     "Договор: +4 пул ✦, призыв −1 ✦, −8 макс. ♥. [gold]Стойка[/gold]: ваши заклинания накладывают Чуму (2 [gold]Хвори[/gold]), Раздор (−1 [gold]Силы[/gold]) "
     "или Распад (1 [gold]Раскола[/gold]). Побед Вестником: {Wins}/{Required} (событие «Филактерия» — Лич)." + _APOSTATE["rus"],
     "Three voices, one mouth.", "Три голоса, одни уста."),
    ("LichForm", "Lich",
     "Pact: +4 max ✦, summoning −1 ✦, −8 max HP. Bone: −30% max HP, physical damage taken −25%, immune to Poison, Blight and Bleed; "
     "heal only by absorption and lifesteal. Death Blast: when your summon falls — 5 magic damage to ALL enemies. Each combat a Bone Thrall (10 HP) rises at your side." + _APOSTATE["eng"],
     "Лич",
     "Договор: +4 пул ✦, призыв −1 ✦, −8 макс. ♥. Кость: −30 % макс. ♥, физический урон по вам −25 %, иммунитет к Яду, Хвори и Кровотечению; "
     "лечение только поглощением и вампиризмом. Взрыв смерти: гибель призыва — 5 урона магией ВСЕМ врагам. В каждом бою рядом встаёт Костяной прислужник (10 ♥)." + _APOSTATE["rus"],
     "Death is a door. You kept the key.", "Смерть — дверь. Ключ остался у тебя."),
    ("Phylactery", "Phylactery",
     "Weight 3. When you would die — revive with {Percent}% HP, once per run; then it shatters.",
     "Филактерия", "Вес 3. При смерти — воскрешение с {Percent} % ♥, один раз за забег; затем разрушается.",
     "Your soul, in a jar.", "Душа в сосуде."),
    ("ClockworkHeart", "Clockwork Heart",
     "Core: [gold]Plating[/gold] +2; immune to Poison, Blight and Bleed; healing −50%. Each combat starts with an elemental sphere "
     "([gold]Wind-up[/gold] adds more, up to 3). Shock damage to you burns ◆ as well as ✦. Compatible only with the Righteous.",
     "Заводное сердце",
     "Ядро: [gold]Латы[/gold] +2; иммунитет к Яду, Хвори и Кровотечению; лечение −50 %. Бой начинается со сферой стихии "
     "([gold]Подзавод[/gold] добавляет ещё, до 3). Разряд по вам выжигает и ◆, и ✦. Совместимо только с Праведником.",
     "Tick. Tock. Still alive.", "Тик. Так. Ещё жив."),
]

PATH_RELICS = []
STAGE6_EQUIPMENT = []  # Знаки Пути — снаряжение (к описанию добавляется приписка про вес)
for s, en, rn, ee, re_, sign, sen, srn, set_, srt in PATHS:
    PATH_RELICS.append((f"{s}Essence", f"Essence: {en}", ee + " Path of the Hero.", f"Сущность: {rn}", re_ + " Путь Героя.", "", ""))
    if sign:
        STAGE6_EQUIPMENT.append((sign, sen, set_ + f" Sign of the {en} Path.", srn, srt + f" Знак Пути: {rn}.", "", ""))

STAGE6_RELICS = FORM_RELICS + PATH_RELICS

_COST = {"eng": " Costs {Cost} camp point(s), you have {Points}.", "rus": " Цена: {Cost} очк. лагеря, есть {Points}."}

STAGE6_REST_SITE = {
    "eng": {
        "OPTION_SLAYTHEREQUIEM_BEAST_RITES.name": "Call of Blood",
        "OPTION_SLAYTHEREQUIEM_BEAST_RITES.description": "Choose a beast branch node ({Pending} pending). Free.",
        "OPTION_SLAYTHEREQUIEM_BEAST_RITES.descriptionDisabled": "No pending nodes.",
        "OPTION_SLAYTHEREQUIEM_CARD_ENCHANT.name": "Enchant Card",
        "OPTION_SLAYTHEREQUIEM_CARD_ENCHANT.description": "Spend a soul stone ({Stones} left) to enchant a card in your deck." + _COST["eng"],
        "OPTION_SLAYTHEREQUIEM_CARD_ENCHANT.descriptionDisabled": "No soul stones.",
        "SLAYTHEREQUIEM_BEAST_PROMPT": "Choose a branch node",
        "SLAYTHEREQUIEM_PATH_PROMPT": "Choose your Path",
        "SLAYTHEREQUIEM_ELEMENT_PROMPT": "Choose your element",
        "SLAYTHEREQUIEM_CARD_ENCHANT_PROMPT": "Choose an enchantment",
    },
    "rus": {
        "OPTION_SLAYTHEREQUIEM_BEAST_RITES.name": "Зов крови",
        "OPTION_SLAYTHEREQUIEM_BEAST_RITES.description": "Выбрать узел ветки зверя (ждут: {Pending}). Бесплатно.",
        "OPTION_SLAYTHEREQUIEM_BEAST_RITES.descriptionDisabled": "Нет ожидающих узлов.",
        "OPTION_SLAYTHEREQUIEM_CARD_ENCHANT.name": "Зачаровать карту",
        "OPTION_SLAYTHEREQUIEM_CARD_ENCHANT.description": "Камень душ (осталось {Stones}) — чара на карту колоды." + _COST["rus"],
        "OPTION_SLAYTHEREQUIEM_CARD_ENCHANT.descriptionDisabled": "Нет камней душ.",
        "SLAYTHEREQUIEM_BEAST_PROMPT": "Выберите узел ветки",
        "SLAYTHEREQUIEM_PATH_PROMPT": "Выберите Путь",
        "SLAYTHEREQUIEM_ELEMENT_PROMPT": "Выберите стихию",
        "SLAYTHEREQUIEM_CARD_ENCHANT_PROMPT": "Выберите чары",
    },
}


def _event(eid, title, initial, options, pages):
    out = {f"{eid}.title": title, f"{eid}.pages.INITIAL.description": initial}
    for key, (t, d) in options.items():
        out[f"{eid}.pages.INITIAL.options.{key}.title"] = t
        out[f"{eid}.pages.INITIAL.options.{key}.description"] = d
    for page, d in pages.items():
        out[f"{eid}.pages.{page}.description"] = d
    return out


_COMMON = {
    "eng": {"INCOMPATIBLE": ("Not for you", "Your current form or worldview forbids it."),
            "LEAVE": ("Leave", "Walk away.")},
    "rus": {"INCOMPATIBLE": ("Не для вас", "Ваша форма или мировоззрение не позволяют."),
            "LEAVE": ("Уйти", "Пройти мимо.")},
}

# (класс, eng title, eng intro, eng accept, eng accepted, rus title, rus intro, rus accept, rus accepted, extra locked {KEY: (eng t, eng d, rus t, rus d)})
FORM_EVENTS = [
    ("BeastBloodEvent", "Beast Blood", "A wounded wolf the size of a horse watches you from the brush. Its eyes are almost human.",
     ("Let it bite", "Become a [gold]Beast Bearer[/gold]."), "The fever passes by dawn. Something else stays.",
     "Кровь зверя", "Из кустов на вас смотрит раненый волк размером с коня. Глаза почти человечьи.",
     ("Подставить руку", "Стать [gold]Носителем Зверя[/gold]."), "К рассвету жар спадает. Что-то другое остаётся.", {}),
    ("UrnWhisperEvent", "Whisper from the Urn", "An urn of black clay whispers your name and offers a pact: power over the dead for a piece of your life.",
     ("Accept the pact", "[gold]Dark Pact[/gold]: +4 max ✦, −8 max HP."), "The whisper falls silent. Your heart beats a little slower.",
     "Шёпот из урны", "Урна из чёрной глины шепчет ваше имя и предлагает договор: власть над мёртвыми за кусочек жизни.",
     ("Принять договор", "[gold]Тёмный договор[/gold]: +4 пул ✦, −8 макс. ♥."), "Шёпот смолкает. Сердце бьётся чуть медленнее.", {}),
    ("VigilEvent", "Vigil", "An empty chapel with one candle still burning. A night of prayer here could change you.",
     ("Keep the vigil", "Become [gold]Righteous[/gold] (a vow: no Abyssal items)."), "At dawn the candle is still burning. So are you.",
     "Бдение", "Пустая часовня, где ещё горит одна свеча. Ночь молитвы здесь может вас изменить.",
     ("Нести бдение", "Стать [gold]Праведником[/gold] (обет: никаких Бездонных вещей)."), "На рассвете свеча всё ещё горит. Как и вы.",
     {"NOT_RIGHTEOUS": ("Unworthy", "Only the Righteous may keep this vigil.", "Недостойны", "Нести бдение может лишь Праведник.")}),
    ("DeepHeartEvent", "Heart of the Deep", "A dwarven automaton lies broken in the dark, its clockwork core still ticking.",
     ("Take the core", "[gold]Clockwork Heart[/gold] replaces yours."), "It hurts. Then it ticks.",
     "Сердце Глубин", "В темноте лежит сломанный двемерский автомат; его заводное ядро всё ещё тикает.",
     ("Забрать ядро", "Ваше сердце заменит [gold]Заводное сердце[/gold]."), "Больно. Потом — тик.", {}),
    ("CryptFeastEvent", "Feast in the Crypt", "Pale nobles feast in a crypt and invite you to their table. The wine is very red.",
     ("Drink", "Become a [gold]Child of the Night[/gold]."), "You wake in a coffin. You are no longer cold.",
     "Пир в склепе", "Бледные вельможи пируют в склепе и зовут вас к столу. Вино очень красное.",
     ("Выпить", "Стать [gold]Дитя Ночи[/gold]."), "Вы просыпаетесь в гробу. Холода больше нет.", {}),
    ("PhylacteryEvent", "Phylactery", "A soul jar on an altar of bone. A Herald could finish the rite here.",
     ("Finish the rite", "Become a [gold]Lich[/gold] and gain the [gold]Phylactery[/gold]."), "Your heart stops. You do not.",
     "Филактерия", "Сосуд для души на костяном алтаре. Вестник мог бы завершить здесь обряд.",
     ("Завершить обряд", "Стать [gold]Личем[/gold] и получить [gold]Филактерию[/gold]."), "Сердце останавливается. Вы — нет.",
     {"NOT_HERALD": ("Not yet", "Requires a Herald with 10 victories.", "Рано", "Нужен Вестник с 10 победами.")}),
]


def _snake(name):
    import re
    return re.sub(r"(?<=[a-z0-9])(?=[A-Z])", "_", name).upper()


def _events(lang):
    out = {}
    for cls, et, ei, ea, eac, rt, ri, ra, rac, extra in FORM_EVENTS:
        eng = lang == "eng"
        opts = {"ACCEPT": ea if eng else ra, **_COMMON[lang]}
        for key, (a, b, c, d) in extra.items():
            opts[key] = (a, b) if eng else (c, d)
        out.update(_event(PREFIX + _snake(cls), et if eng else rt, ei if eng else ri, opts,
                          {"ACCEPTED": eac if eng else rac, "LEAVE": "You move on." if eng else "Вы идёте дальше."}))
    return out


STAGE6_EVENTS = {"eng": _events("eng"), "rus": _events("rus")}

# Зачарование карт (таблица enchantments)
_ENCH = [
    ("FlamingEnchantment", "Flaming", "Also apply 3 [gold]Burning[/gold].", "Пламенная", "Ещё 3 [gold]Горения[/gold]."),
    ("FrostyEnchantment", "Frosty", "Also apply 2 [gold]Chill[/gold].", "Морозная", "Ещё 2 [gold]Озноба[/gold]."),
    ("StormEnchantment", "Stormy", "Burns 2 ✦ of the target.", "Грозовая", "Выжигает 2 ✦ цели."),
    ("LightenedEnchantment", "Lightened", "Weight −1.", "Облегчённая", "Вес −1."),
    ("BreathingEnchantment", "Breathing", "Costs 1 less ◆.", "Дыхательная", "На 1 ◆ дешевле."),
    ("DoubleEnchantment", "Double", "Two enchantments at once.", "Двойная", "Две чары сразу."),
]
STAGE6_ENCHANTMENTS = {
    lang: {k: v for cls, et, ed, rt, rd in _ENCH for k, v in (
        (f"{PREFIX}{_snake(cls)}.title", et if lang == "eng" else rt),
        (f"{PREFIX}{_snake(cls)}.description", ed if lang == "eng" else rd))}
    for lang in ("eng", "rus")
}

STAGE6_BADGES = {
    "eng": {f"{PREFIX}NO_CANDLE.title": "Not a Single Candle",
            f"{PREFIX}NO_CANDLE.description": "Win a Requiem run without returning by the Memory Stone."},
    "rus": {f"{PREFIX}NO_CANDLE.title": "Ни одной свечи",
            f"{PREFIX}NO_CANDLE.description": "Победа в забеге «Реквием» без единого возврата Камнем памяти."},
}

STAGE6_SETTINGS = {
    "eng": {
        "REGENT_ORIGIN.hover.desc": "\"Choose\" shows a relic selection screen when the run starts.",
        "SILENT_ORIGIN.hover.desc": "\"Choose\" shows a relic selection screen when the run starts.",
        "REGENT_ORIGIN.Choose": "Choose",
        "SILENT_ORIGIN.Choose": "Choose",
        "BACKGROUND.Choose": "Choose",
        "BACKGROUND.hover.desc": "1st/2nd/3rd background of the chosen origin, or Vagrant. Unfinished backgrounds are replaced with a random ready one. "
                                 "\"Choose\" shows a relic selection screen when the run starts.",
    },
    "rus": {
        "REGENT_ORIGIN.hover.desc": "«На выбор» — экран выбора реликвии в начале забега.",
        "SILENT_ORIGIN.hover.desc": "«На выбор» — экран выбора реликвии в начале забега.",
        "REGENT_ORIGIN.Choose": "На выбор",
        "SILENT_ORIGIN.Choose": "На выбор",
        "BACKGROUND.Choose": "На выбор",
        "BACKGROUND.hover.desc": "1-я/2-я/3-я предыстория выбранного происхождения или Бродяга. Несделанные предыстории заменяются случайной готовой. "
                                 "«На выбор» — экран выбора реликвии в начале забега.",
    },
}
