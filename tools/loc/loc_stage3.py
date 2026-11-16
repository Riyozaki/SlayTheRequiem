"""Тексты этапа 3 «Безмолвная, алхимия, яды» + хвосты этапов 1–2 (происхождения, предыстории, аффиксы, Атаманша).

Подключается из build_loc.py. Форматы кортежей такие же, как там. Зелья генерируются по семействам × ступеням.
"""

# ───────────── карты Безмолвной и Иллюзия ─────────────
# (класс, eng title, eng text, rus title, rus text)
STAGE3_CARDS = [
    ("Backstab", "Backstab", "Deal {Damage:diff()} damage.\n[gold]Sneaky[/gold]: deal {Sneak:diff()} more.",
     "Удар в спину", "Нанести {Damage:diff()} урона.\n[gold]Подлый[/gold]: ещё {Sneak:diff()}."),
    ("PoisonedBlade", "Poisoned Blade", "Deal {Damage:diff()} damage.\nIf your weapon is coated, its poison takes effect twice more.",
     "Отравленный клинок", "Нанести {Damage:diff()} урона.\nЕсли на оружии яд — его эффект ещё дважды."),
    ("Garrote", "Garrote", "Deal {Damage:diff()} damage. Apply {BleedPower:diff()} [gold]Bleed[/gold].\n[gold]Sneaky[/gold]: apply {WeakPower:diff()} [gold]Weak[/gold].",
     "Удавка", "Нанести {Damage:diff()} урона. Наложить {BleedPower:diff()} [gold]Кровотечения[/gold].\n[gold]Подлый[/gold]: наложить {WeakPower:diff()} [gold]Слабости[/gold]."),
    ("RottingCut", "Rotting Cut", "Deal {Damage:diff()} damage.\nApply {BlightPower:diff()} [gold]Blight[/gold].",
     "Гнилостный надрез", "Нанести {Damage:diff()} урона.\nНаложить {BlightPower:diff()} [gold]Хвори[/gold]."),
    ("PickPocket", "Pick Pocket", "Deal {Damage:diff()} damage. Gain {Gold:diff()} gold.\n[gold]Sneaky[/gold]: also steal a random tier I potion.\n[gold]Exhaust[/gold].",
     "Обчистить", "Нанести {Damage:diff()} урона. Получить {Gold:diff()} золота.\n[gold]Подлый[/gold]: ещё украсть случайное зелье I.\n[gold]Изгнание[/gold]."),
    ("Ambush", "Ambush", "Deal {Damage:diff()} damage to ALL enemies.\nEnemies not intending to attack also get {BleedPower:diff()} [gold]Bleed[/gold].",
     "Засада", "Нанести {Damage:diff()} урона ВСЕМ врагам.\nВраги без атакующего намерения получают ещё {BleedPower:diff()} [gold]Кровотечения[/gold]."),
    ("ShadowStrike", "Shadow Strike", "Deal {Damage:diff()} damage.\nIf you are [gold]Invisible[/gold], deal it twice.",
     "Удар из тени", "Нанести {Damage:diff()} урона.\nЕсли вы [gold]Невидимы[/gold] — дважды."),
    ("Sidestep", "Sidestep", "Gain {Block:diff()} [gold]Block[/gold].\nGain {DodgePower:diff()} [gold]Dodge[/gold] charge until your next turn.",
     "Шаг в сторону", "Получить {Block:diff()} [gold]Блока[/gold].\n+{DodgePower:diff()} заряд [gold]Уворота[/gold] до следующего хода."),
    ("ShadowStep", "Shadow Step", "Gain {InvisibilityPower:diff()} [gold]Invisibility[/gold].",
     "Шаг тени", "Получить {InvisibilityPower:diff()} [gold]Невидимости[/gold]."),
    ("FlaskToFace", "Flask to the Face", "A random poison from your potion slots affects the enemy at ×{Multiplier} strength. The flask is not used up.\nNo poisons — your weapon's coating; no coating — [gold]Poison[/gold] 4.",
     "Флакон в лицо", "Случайный яд из ваших слотов действует на врага с силой ×{Multiplier}. Флакон не тратится.\nНет ядов — яд с оружия; нет и его — [gold]Яд[/gold] 4."),
    ("Distillation", "Distillation", "Brew a random tier I potion.\n[gold]Exhaust[/gold].",
     "Перегонка", "Сварить случайное зелье ступени I.\n[gold]Изгнание[/gold]."),
    ("ToxicCoating", "Toxic Coating", "Coat your weapon with Toxin I ({Charges:diff()} charges).\n[gold]Exhaust[/gold].",
     "Ядовитая смазка", "Нанести на оружие Токсин I ({Charges:diff()} заряда).\n[gold]Изгнание[/gold]."),
    ("VolatileMixture", "Volatile Mixture", "[gold]Poison[/gold] and [gold]Blight[/gold] on ALL enemies detonate: each takes damage equal to their total stacks, then they are removed.",
     "Летучий состав", "[gold]Яд[/gold] и [gold]Хворь[/gold] на ВСЕХ врагах детонируют: урон = сумма их стаков, после чего они снимаются."),
    ("Riposte", "Riposte", "Whenever you [gold]Dodge[/gold], deal {RipostePower:diff()} damage to the attacker; your next attack is [gold]Sneaky[/gold].",
     "Контрудар", "Каждый раз, когда вы [gold]Увернулись[/gold], атакующий получает {RipostePower:diff()} урона, а ваша следующая атака — [gold]Подлая[/gold]."),
    ("Evasion", "Evasion", "Gain {DodgePower:diff()} [gold]Dodge[/gold] charge every turn.",
     "Уклонение", "+{DodgePower:diff()} заряд [gold]Уворота[/gold] каждый ход."),
    ("VenomMastery", "Venom Mastery", "At the start of your turn, your weapon's poison gains {VenomMasteryPower:diff()} charge.",
     "Мастер ядов", "В начале хода яд на оружии получает +{VenomMasteryPower:diff()} заряд."),
    # Иллюзия
    ("MuffledSteps", "Muffled Steps", "Gain {Block:diff()} [gold]Block[/gold].\nYour next attack is [gold]Sneaky[/gold].",
     "Приглушённые шаги", "Получить {Block:diff()} [gold]Блока[/gold].\nСледующая атака — [gold]Подлая[/gold]."),
    ("Confusion", "Confusion", "An enemy with 40 HP or less is [gold]Confused[/gold] for {ConfusionPower:diff()} turn: its attacks hit a random other enemy.",
     "Смятение", "Враг с ♥ ≤ 40 в [gold]Смятении[/gold] {ConfusionPower:diff()} ход: его атаки бьют случайного другого врага."),
    ("Terror", "Fear", "An enemy with 70 HP or less is [gold]Afraid[/gold] for {FearPower:diff()} turns: deals half damage.",
     "Страх", "Враг с ♥ ≤ 70 в [gold]Страхе[/gold] {FearPower:diff()} хода: наносит вдвое меньше урона."),
    ("Invisibility", "Invisibility", "Gain {InvisibilityPower:diff()} [gold]Invisibility[/gold].",
     "Невидимость", "Получить {InvisibilityPower:diff()} [gold]Невидимости[/gold]."),
    ("Madness", "Madness", "Two random non-boss enemies are [gold]Confused[/gold] for {ConfusionPower:diff()} turns and attack each other.",
     "Безумие", "Два случайных врага (не боссы) в [gold]Смятении[/gold] {ConfusionPower:diff()} хода — бьют друг друга."),
]

# ───────────── силы ─────────────
STAGE3_POWERS = [
    ("BlightResistancePower", "Blight Resistance", "[gold]Blight[/gold] reduced by tier {Amount}.",
     "Сопротивление хвори", "[gold]Хворь[/gold] снижена на ступень {Amount}."),
    ("BeastSpiritPower", "Beast Spirit", "A free spirit beast with {Amount} HP fights at your side: attacks a random enemy at the end of your turn.",
     "Дух зверя", "Бесплатный дух зверя с {Amount} ♥ сражается рядом: в конце хода бьёт случайного врага."),
    ("RejuvenationPower", "Rejuvenation", "Heal at the start of your turn. {Amount} turns left.",
     "Омоложение", "Лечение в начале хода. Осталось ходов: {Amount}."),
    ("SecondWindPower", "Second Wind", "Gain ◆ at the start of your turn. {Amount} turns left.",
     "Второе дыхание", "+◆ в начале хода. Осталось ходов: {Amount}."),
    ("ClearMindPower", "Clarity", "Gain ✦ at the start of your turn. {Amount} turns left.",
     "Ясность", "+✦ в начале хода. Осталось ходов: {Amount}."),
    ("GiantPower", "Giant's Might", "Poise +{Amount} this combat.",
     "Сила великана", "Стойкость +{Amount} до конца боя."),
    ("SorcererPower", "Sorcerer's Draught", "The first {Amount} spells each turn cost 1 less ✦.",
     "Зелье чародея", "Первые {Amount} заклинания каждого хода на 1 ✦ дешевле."),
    ("MoonSugarPower", "Moon Sugar", "Spells cost 1 less ✦. {Amount} turns left; then lose 2 ✦ (crash).",
     "Лунный сахар", "Заклинания на 1 ✦ дешевле. Осталось ходов: {Amount}; потом «Отходняк» −2 ✦."),
    ("InvisibilityPower", "Invisibility", "Enemy attacks against you deal 50% less. Your attacks are [gold]Sneaky[/gold]. Ends after your first attack. {Amount} turns.",
     "Невидимость", "Атаки врагов по вам −50 %. Ваши атаки — [gold]Подлые[/gold]. Спадает после первой вашей атаки. Ходов: {Amount}."),
    ("SneakNextPower", "Muffled Steps", "Your next attack is [gold]Sneaky[/gold].",
     "Приглушённые шаги", "Следующая атака — [gold]Подлая[/gold]."),
    ("ConfusionPower", "Confusion", "Attacks against heroes don't reach HP — the damage hits a random other enemy instead. {Amount} turns.",
     "Смятение", "Атаки по героям не доходят до ♥ — этот урон получает случайный другой враг. Ходов: {Amount}."),
    ("StaminaSurgePower", "Vigor", "◆ regeneration +{Amount} this combat.",
     "Бодрость", "Прирост ◆ +{Amount} до конца боя."),
    ("InsightPower", "Insight", "✦ regeneration +{Amount} this combat.",
     "Прозрение", "Прирост ✦ +{Amount} до конца боя."),
    ("RipostePower", "Riposte", "After a [gold]Dodge[/gold], the attacker takes {Amount} damage and your next attack is [gold]Sneaky[/gold].",
     "Контрудар", "После [gold]Уворота[/gold] атакующий получает {Amount} урона, а следующая атака — [gold]Подлая[/gold]."),
    ("TemporaryDodgePower", "Sidestep", "{Amount} extra [gold]Dodge[/gold] charge until your next turn.",
     "Рывок", "+{Amount} заряд [gold]Уворота[/gold] до следующего хода."),
    ("VenomMasteryPower", "Venom Mastery", "At the start of your turn, your weapon's poison gains {Amount} charge.",
     "Мастер ядов", "В начале хода яд на оружии получает +{Amount} заряд."),
    ("GangPower", "Gang", "Passive until a hero strikes this bandit or the Chieftain.",
     "Шайка", "Пассивен, пока герой не ударит этого разбойника или атаманшу."),
    ("PanicPower", "Panic", "The next attack deals no damage.",
     "Паника", "Следующая атака не наносит урона."),
    ("AdrenalinePower", "Adrenaline", "Physical damage taken −50%. {Amount} hero turns.",
     "Адреналин", "Получаемый физический урон −50 %. Ходов героев: {Amount}."),
    # аффиксы
    ("HardenedAffixPower", "Hardened", "Affix: [gold]Plating[/gold] +2.", "Закалённый", "Аффикс: [gold]Латы[/gold] +2."),
    ("ElementalAffixPower", "Elemental", "Affix: a permanent elemental cloak 3 and resistance II to it.",
     "Стихийный", "Аффикс: постоянный Плащ стихии 3 и сопротивление II к ней."),
    ("DrainingAffixPower", "Draining", "Affix: attacks that hit HP drain 2 ◆.", "Выпивающий силы", "Аффикс: атаки, задевшие ♥, отнимают 2 ◆."),
    ("ManaEaterAffixPower", "Mana Eater", "Affix: attacks that hit HP burn 2 ✦.", "Пожиратель маны", "Аффикс: атаки, задевшие ♥, выжигают 2 ✦."),
    ("VampiricAffixPower", "Vampiric", "Affix: heals 50% of HP damage dealt.", "Вампирический", "Аффикс: лечит 50 % урона, нанесённого по ♥."),
    ("PoisonedBladeAffixPower", "Poisoned Blade", "Affix: attacks that hit HP apply 2 [gold]Poison[/gold].",
     "Отравленный клинок", "Аффикс: атаки, задевшие ♥, накладывают 2 [gold]Яда[/gold]."),
]

_POISON_TEXT = {
    "SnakeBite": ("Snake Bite", "+{Amount} direct damage (ignores Plating)", "Укус змеи", "+{Amount} прямого урона (мимо Лат)"),
    "Toxin": ("Toxin", "[gold]Poison[/gold] {Amount}", "Токсин", "[gold]Яд[/gold] {Amount}"),
    "Rot": ("Rot", "[gold]Blight[/gold] {Amount}", "Гнилец", "[gold]Хворь[/gold] {Amount}"),
    "Exhaustion": ("Exhaustion", "−{Amount} ◆", "Истощение", "−{Amount} ◆"),
    "Emptybloom": ("Emptybloom", "−{Amount} ✦", "Пустоцвет", "−{Amount} ✦"),
    "Weakness": ("Weakness", "[gold]Weak[/gold] {Amount}", "Слабость", "[gold]Слабость[/gold] {Amount}"),
    "Corrosion": ("Corrosion", "[gold]Sunder[/gold] {Amount}", "Разъедание", "[gold]Раскол[/gold] {Amount}"),
    "Paralytic": ("Paralytic", "{Amount}% chance to [gold]Paralyze[/gold] (100 — once per combat; not bosses)",
                  "Паралитик", "шанс {Amount} % [gold]Паралича[/gold] (100 — раз за бой; не боссы)"),
}
_POISON_SHORT = {  # эффект без числа — для силы на оружии (Amount силы = заряды)
    "SnakeBite": ("extra direct damage (ignores Plating)", "доп. прямой урон (мимо Лат)"),
    "Toxin": ("[gold]Poison[/gold]", "[gold]Яд[/gold]"),
    "Rot": ("[gold]Blight[/gold]", "[gold]Хворь[/gold]"),
    "Exhaustion": ("drain ◆", "отнимают ◆"),
    "Emptybloom": ("burn ✦", "выжигают ✦"),
    "Weakness": ("[gold]Weak[/gold]", "[gold]Слабость[/gold]"),
    "Corrosion": ("[gold]Sunder[/gold]", "[gold]Раскол[/gold]"),
    "Paralytic": ("chance to [gold]Paralyze[/gold]", "шанс [gold]Паралича[/gold]"),
}
for _k, (_et, _ed, _rt, _rd) in _POISON_TEXT.items():
    _se, _sr = _POISON_SHORT[_k]
    STAGE3_POWERS.append((f"{_k}PoisonPower", f"{_et} (weapon)", f"Weapon attacks: {_se}. Charges left: {{Amount}}.",
                          f"{_rt} (оружие)", f"Оружейные атаки: {_sr}. Осталось зарядов: {{Amount}}."))
STAGE3_POWERS.append(("CombinedPoisonPower", "Combined poison (weapon)", "Weapon attacks: Toxin and Exhaustion. Charges: {Amount}.",
                      "Комбинированный яд (оружие)", "Оружейные атаки: Токсин и Истощение. Заряды: {Amount}."))

# ───────────── реликвии: происхождения, предыстории, служебные ─────────────
# (класс, eng title, eng desc, rus title, rus desc, flavor eng, flavor rus)
STAGE3_RELICS = [
    ("FrostbornOrigin", "Frostborn", "Max HP +5, capacity +2. [gold]Frost resistance I[/gold]; [gold]Chill[/gold] on you wears off at once.",
     "Инеерождённые", "♥ +5, груз +2. [gold]Сопротивление холоду I[/gold]; [gold]Озноб[/gold] на вас спадает целиком.",
     "Winter is a relative.", "Зима — родственница."),
    ("MoonbornOrigin", "Moonborn", "Max ◆ +1. [gold]Night Eyes[/gold]: unarmed weapon attacks (claws) apply 1 [gold]Bleed[/gold], like a dagger.",
     "Лунная порода", "Пул ◆ +1. [gold]Ночные глаза[/gold]: безоружные оружейные атаки (когти) накладывают 1 [gold]Кровотечение[/gold], как кинжал.",
     "The Spire is never quite dark to them.", "Для них в Шпиле никогда не бывает совсем темно."),
    ("ThicketbornOrigin", "Thicketborn", "Max HP −5, capacity +3. [gold]Blight resistance II[/gold]. Beasts don't attack on the first turn.",
     "Сыны Чащи", "♥ −5, груз +3. [gold]Сопротивление хвори II[/gold]. Звери не атакуют в первый ход.",
     "The wolves nod as you pass.", "Волки кивают вам вслед."),
    ("MarshbornOrigin", "Marshborn", "Max HP +5. [gold]Poison resistance I[/gold], [gold]Blight resistance II[/gold]. Below 50% HP heal 2 at turn start (below 25% — 4).",
     "Тинники", "♥ +5. [gold]Сопротивление ядам I[/gold], [gold]хвори II[/gold]. При ♥ < 50 % — лечение 2 в начале хода (при < 25 % — 4).",
     "The swamp keeps what it loves.", "Топь бережёт то, что любит."),
    ("VagrantBackground", "Vagrant", "Max HP +5, ◆ +1, ✦ +1, capacity +3. Remove a card at the start of the run.",
     "Бродяга", "♥ +5, ◆ +1, ✦ +1, груз +3. Удалить карту в начале забега.", "No past, no debts.", "Ни прошлого, ни долгов."),
    ("BeastSpeakerBackground", "Beast Speaker", "Each combat begins with a free spirit beast (wolf → sabrecat → bear as you slay elites).",
     "Зверобой-духовидец", "Каждый бой начинается с бесплатным духом зверя (волк → саблезуб → медведь по убитым элитам).",
     "It answers to no name you could say.", "Отзывается на имя, которое не выговорить."),
    ("SkaldBackground", "Skald", "[gold]Battle Cry[/gold] replaces a Defend.", "Скальд", "[gold]Боевой клич[/gold] вместо одной Защиты.",
     "Every saga needs a first verse.", "У каждой саги есть первая строка."),
    ("FrostbinderBackground", "Frostbinder", "[gold]Ice Wind[/gold] replaces a Strike. Aura: enemies are vulnerable to frost (I).",
     "Хладовяз", "[gold]Ледяной ветер[/gold] вместо Удара. Аура: враги уязвимы к холоду (I).", "Breath that hangs in the air.", "Дыхание, что застывает в воздухе."),
    ("FugitiveBackground", "Fugitive", "Attacks deal +1 damage for each 0-cost card played this turn (max +3).",
     "Беглец", "Атаки +1 урона за каждую сыгранную в этом ходу карту за 0 (до +3).", "Always one step ahead.", "Всегда на шаг впереди."),
    ("RogueBackground", "Rogue",
     "[gold]Pick Pocket[/gold] is in your deck. [gold]Theft[/gold] is available in shops. Treasure chests offer +1 relic to choose from.",
     "Плут", "В колоде — [gold]Обчистить[/gold]. В лавках доступна [gold]Кража[/gold]. В сундуках — +1 реликвия на выбор.",
     "Nothing is stolen until it's missed.", "Не пропало — не украдено."),
    ("DuskSingerBackground", "Dusk Singer", "+1 Restoration mastery. Start with a [gold]Moon Sugar Elixir[/gold].",
     "Сумеречный певец", "+1 владения Восстановлением. Стартовый [gold]Эликсир лунного сахара[/gold].", "Songs for the hour between.", "Песни для часа между."),
    ("WinddancerBackground", "Winddancer", "Attacks deal +1 damage for every 3rd card played this turn.",
     "Ветроплясун", "Атаки +1 урона за каждую 3-ю карту, сыгранную за ход.", "Moves like a rumor.", "Движется, как слух."),
    ("HunterBackground", "Hunter", "Shivs deal +2 damage.", "Охотник", "Ножи наносят +2 урона.", "Patience is the sharpest arrow.", "Терпение — самая острая стрела."),
    ("HermitBackground", "Hermit", "After each combat gain 1 reagent (Bloodmoss or Nightcap).",
     "Отшельник", "После каждого боя +1 реагент (Кровяной мох или Ночная поганка).", "The mushrooms talk, if you listen.", "Грибы говорят, если слушать."),
    ("GladiatorBackground", "Gladiator", "Heal 1 at the start of your turn. Start with a [gold]Rejuvenation Potion I[/gold].",
     "Гладиатор", "Лечение 1 в начале хода. Стартовое [gold]Зелье омоложения I[/gold].", "The crowd still chants your name.", "Толпа всё ещё скандирует ваше имя."),
    ("ShadowscaleBackground", "Shadowscale", "[gold]From the Shadows[/gold]: your first attack each combat deals ×2 and costs no ◆.",
     "Тёмный ящер", "[gold]Из тени[/gold]: первая атака в бою ×2 и не тратит ◆.", "Born under the Shadow sign.", "Рождён под знаком Тени."),
    ("VenomweaverBackground", "Venomweaver", "[gold]Poison Spray[/gold] replaces a Strike.", "Ядовяз", "[gold]Ядовитый спрей[/gold] вместо Удара.",
     "Every bloom has a bite.", "У каждого цветка есть жало."),
    ("JusticiarBackground", "Justiciar", "Each combat begins with a free storm atronach.", "Юстициар", "Каждый бой начинается с бесплатным грозовым атронахом.",
     "Order, enforced by lightning.", "Порядок, наведённый молнией."),
    ("ShadowSorcererBackground", "Shadow Sorcerer", "+1 Illusion mastery. [gold]Confusion[/gold] replaces a Strike.",
     "Чародей теней", "+1 владения Иллюзией. [gold]Смятение[/gold] вместо Удара.", "Did you see that? Neither did they.", "Видели? Вот и они нет."),
    ("WizardBackground", "Wizard", "Spells deal +1 damage for each 25% of ✦ pool you are missing.",
     "Волшебник", "Заклинания +1 урона за каждые недостающие 25 % пула ✦.", "Power drawn from emptiness.", "Сила, черпаемая из пустоты."),
    ("KnightBackground", "Knight", "[gold]Plating[/gold] +1/+2/+3 while below 90/70/50% HP.",
     "Рыцарь", "[gold]Латы[/gold] +1/+2/+3 при ♥ ниже 90/70/50 %.", "The oath grows heavier as you bleed.", "Клятва тяжелеет с каждой раной."),
    ("EnchanterBackground", "Enchanter", "Non-spell attacks restore 1 ✦.", "Зачарователь", "Атаки (не заклинания) восстанавливают 1 ✦.",
     "Every blade hums a little.", "Каждый клинок немного поёт."),
    ("SummonerBackground", "Summoner", "Summons +5 HP and +1 damage. [gold]Conjure Spirit Wolf[/gold] replaces a Strike.",
     "Призыватель", "Призывы +5 ♥ и +1 урона. [gold]Призвать волка-духа[/gold] вместо Удара.", "They come when called. Mostly.", "Приходят, когда зовут. Обычно."),
    ("MushroomTowerBackground", "Mushroom Tower Apprentice", "[gold]Firebolt[/gold] replaces a Strike. Aura: enemies are vulnerable to fire (I).",
     "Ученик Грибной Башни", "[gold]Огненная стрела[/gold] вместо Удара. Аура: враги уязвимы к огню (I).", "Spores and sparks.", "Споры и искры."),
    ("HerbalistPouch", "Herbalist's Pouch",
     "Holds up to 12 reagents (weight 0). Victories yield reagents by enemy kind. [gold]Brew[/gold] potions and poisons at rest sites. +2 potion slots (poison belt).\n"
     "Bloodmoss {Bloodmoss}, Bluebloom {Bluebloom}, Yellow sorrel {Yellowsorrel}, Ash salt {AshSalt}, Frost salt {FrostSalt}, Storm crystal {StormCrystal}, Nightcap {Nightcap}, Grave dust {GraveDust}, Dragon scale {DragonScale}, Fragrant tear {FragrantTear}.",
     "Сумка травника",
     "До 12 реагентов (вес 0). Победы дают реагенты по виду врагов. [gold]Варка[/gold] зелий и ядов в лагере. +2 слота зелий (пояс для ядов).\n"
     "Кровяной мох {Bloodmoss}, Синецвет {Bluebloom}, Жёлтая кислица {Yellowsorrel}, Пепельная соль {AshSalt}, Ледяная соль {FrostSalt}, Грозовой кристалл {StormCrystal}, Ночная поганка {Nightcap}, Прах мертвеца {GraveDust}, Драконий порошок {DragonScale}, Душистая слеза {FragrantTear}.",
     "Smells of a whole forest.", "Пахнет целым лесом."),
    ("TravelPack", "Travel Pack",
     "Burden: {Weight} / {Capacity} (overload {Excess}). Overload 1–5: −1 ◆ regen; 6–10: also −1 opening card; over 10: you can't pick up cards. Camp chest: {Stored} items.",
     "Походный мешок",
     "Груз: {Weight} / {Capacity} (перегруз {Excess}). Перегруз 1–5: −1 к приросту ◆; 6–10: ещё −1 карта в стартовой руке; больше 10: карты не подбираются. В сундуке лагеря: {Stored}.",
     "Everything you own, minus what you left behind.", "Всё ваше, кроме оставленного."),
]

# ───────────── зелья ─────────────
_ROMAN = {1: "I", 3: "III", 5: "V"}
# семейство: (eng name, eng desc, rus name, rus desc)
_POTIONS = {
    "Health": ("Health Potion", "Heal {Amount} HP.", "Зелье здоровья", "Вылечить {Amount} ♥."),
    "Stamina": ("Stamina Potion", "Restore {Amount} ◆ (tier V — full ◆ and +1 ◆ regen this combat).",
                "Зелье выносливости", "Восстановить {Amount} ◆ (ступень V — полная ◆ и +1 к приросту на бой)."),
    "Mana": ("Mana Potion", "Restore {Amount} ✦ (tier V — full ✦ and +1 ✦ regen this combat).",
             "Зелье маны", "Восстановить {Amount} ✦ (ступень V — полная ✦ и +1 к приросту на бой)."),
    "Rejuvenation": ("Rejuvenation Potion", "Heal {Amount} at the start of your turn for {Turns} turns.",
                     "Зелье омоложения", "Лечение {Amount} в начале хода, {Turns} хода."),
    "SecondWind": ("Second Wind Potion", "Gain {Amount} ◆ at the start of your turn for {Turns} turns.",
                   "Зелье второго дыхания", "+{Amount} ◆ в начале хода, {Turns} хода."),
    "Clarity": ("Clarity Potion", "Gain {Amount} ✦ at the start of your turn for {Turns} turns.",
                "Зелье ясности", "+{Amount} ✦ в начале хода, {Turns} хода."),
    "Giant": ("Giant's Potion", "Poise +{Amount} this combat (tier V — also Strength +1).",
              "Зелье великана", "Стойкость +{Amount} на бой (ступень V — ещё Сила +1)."),
    "Knight": ("Knight's Potion", "Gain {Amount} [gold]Plating[/gold].", "Зелье рыцаря", "Получить {Amount} [gold]Лат[/gold]."),
    "Sorcerer": ("Sorcerer's Potion", "The first {Amount} spells each turn cost 1 less ✦ this combat.",
                 "Зелье чародея", "Первые {Amount} заклинания каждого хода на 1 ✦ дешевле до конца боя."),
    "FireWard": ("Fire Ward Potion", "[gold]Fire resistance[/gold] +{Amount} this combat.", "Зелье защиты от огня", "[gold]Сопротивление огню[/gold] +{Amount} на бой."),
    "FrostWard": ("Frost Ward Potion", "[gold]Frost resistance[/gold] +{Amount} this combat.", "Зелье защиты от холода", "[gold]Сопротивление холоду[/gold] +{Amount} на бой."),
    "ShockWard": ("Shock Ward Potion", "[gold]Shock resistance[/gold] +{Amount} this combat.", "Зелье защиты от разряда", "[gold]Сопротивление разряду[/gold] +{Amount} на бой."),
    "Feather": ("Feather Potion", "Capacity +{Amount} until the next rest site.", "Зелье пёрышка", "Груз +{Amount} до следующего лагеря."),
    "WitchHunter": ("Witch Hunter's Potion", "Gain {Amount} [gold]Spell Absorption[/gold].", "Зелье охотника на ведьм", "Получить {Amount} [gold]Поглощения заклинаний[/gold]."),
    "Invisibility": ("Invisibility Potion", "Gain {Amount} [gold]Invisibility[/gold].", "Зелье невидимости", "Получить {Amount} [gold]Невидимости[/gold]."),
}

STAGE3_POTIONS = []  # (класс, eng title, eng desc, rus title, rus desc)
for fam, (et, ed, rt, rd) in _POTIONS.items():
    for tier, r in _ROMAN.items():
        STAGE3_POTIONS.append((f"{fam}Potion{r}", f"{et} {r}", ed, f"{rt} {r}", rd))
for fam, (et, ed, rt, rd) in _POISON_TEXT.items():
    for tier, r in _ROMAN.items():
        STAGE3_POTIONS.append((f"{fam}Poison{r}", f"{et} {r}", f"Coat your weapon ({{Charges}} charges). Weapon attacks: {ed}.",
                               f"{rt} {r}", f"Нанести на оружие ({{Charges}} заряда). Оружейные атаки: {rd}."))
STAGE3_POTIONS += [
    ("Panacea", "Panacea", "Remove [gold]Blight[/gold], [gold]Poison[/gold], [gold]Bleed[/gold], [gold]Burning[/gold] and [gold]Chill[/gold] from yourself.",
     "Панацея", "Снять с себя [gold]Хворь[/gold], [gold]Яд[/gold], [gold]Кровотечение[/gold], [gold]Горение[/gold] и [gold]Озноб[/gold]."),
    ("MoonSugarElixir", "Moon Sugar Elixir", "Gain {Amount} ✦. Spells cost 1 less ✦ for {Turns} turns, then lose 2 ✦.",
     "Эликсир лунного сахара", "+{Amount} ✦. Заклинания на 1 ✦ дешевле {Turns} хода, затем «Отходняк» −2 ✦."),
    ("HearthMotherTears", "Hearth Mother's Tears", "When you would die, revive with 25% of your Max HP.",
     "Слёзы Матери Очага", "Когда вы должны умереть — воскрешение с 25 % макс. ♥."),
]

# ───────────── враги ─────────────
STAGE3_MONSTERS = {
    "CRIMSON_CHIEFTAIN": {
        "eng": {"name": "Chieftain of the Crimson Knives", "moves": {"DAGGER": "Dirty Dagger", "SABER": "Thirsting Saber", "RALLY": "To Me, Knives!"}},
        "rus": {"name": "Атаманша Багряных Ножей", "moves": {"DAGGER": "Грязный кинжал", "SABER": "Жаждущая сабля", "RALLY": "Ко мне, ножи!"}},
    },
    "CRIMSON_BANDIT": {
        "eng": {"name": "Crimson Knife", "moves": {"LURK": "Lurking", "AMBUSH": "Ambush", "STAB": "Stab"}},
        "rus": {"name": "Багряный нож", "moves": {"LURK": "Затаился", "AMBUSH": "Засада", "STAB": "Удар ножом"}},
    },
}
STAGE3_ENCOUNTERS = {"CRIMSON_CHIEFTAIN_ELITE": {"eng": "Crimson Knives", "rus": "Багряные Ножи"}}

# ───────────── настройки и лагерь ─────────────
STAGE3_SETTINGS = {
    "eng": {
        "AUTO_DODGE.title": "Auto-dodge", "AUTO_DODGE.hover.title": "Auto-dodge",
        "AUTO_DODGE.hover.desc": "Spend a Dodge charge and 2 ◆ automatically when an attack would hit your HP. Off — Dodge never triggers.",
        "PLATEBEARER_ORIGIN.title": "Platebearer clan", "PLATEBEARER_ORIGIN.hover.title": "Platebearer clan",
        "PLATEBEARER_ORIGIN.hover.desc": "Until the origin select screen exists, the origin is taken from here.",
        "PLATEBEARER_ORIGIN.Frostborn": "Frostborn", "PLATEBEARER_ORIGIN.None": "None",
        "SILENT_ORIGIN.title": "Silent tribe", "SILENT_ORIGIN.hover.title": "Silent tribe",
        "SILENT_ORIGIN.hover.desc": "Until the origin select screen exists, the origin is taken from here.",
        "SILENT_ORIGIN.Random": "Random", "SILENT_ORIGIN.Moonborn": "Moonborn", "SILENT_ORIGIN.Thicketborn": "Thicketborn",
        "SILENT_ORIGIN.Marshborn": "Marshborn", "SILENT_ORIGIN.None": "None",
        "BACKGROUND.title": "Background", "BACKGROUND.hover.title": "Background",
        "BACKGROUND.hover.desc": "1st/2nd/3rd background of the chosen origin, or Vagrant. Unfinished backgrounds are replaced with a random ready one.",
        "BACKGROUND.Random": "Random", "BACKGROUND.First": "First", "BACKGROUND.Second": "Second", "BACKGROUND.Third": "Third",
        "BACKGROUND.Vagrant": "Vagrant", "BACKGROUND.None": "None",
    },
    "rus": {
        "AUTO_DODGE.title": "Автоуворот", "AUTO_DODGE.hover.title": "Автоуворот",
        "AUTO_DODGE.hover.desc": "Тратить заряд Уворота и 2 ◆ автоматически, когда атака должна снять ♥. Выкл — Уворот не срабатывает.",
        "PLATEBEARER_ORIGIN.title": "Клан Латоносца", "PLATEBEARER_ORIGIN.hover.title": "Клан Латоносца",
        "PLATEBEARER_ORIGIN.hover.desc": "Пока нет экрана выбора происхождения, оно берётся отсюда.",
        "PLATEBEARER_ORIGIN.Frostborn": "Инеерождённые", "PLATEBEARER_ORIGIN.None": "Без происхождения",
        "SILENT_ORIGIN.title": "Племя Безмолвной", "SILENT_ORIGIN.hover.title": "Племя Безмолвной",
        "SILENT_ORIGIN.hover.desc": "Пока нет экрана выбора происхождения, оно берётся отсюда.",
        "SILENT_ORIGIN.Random": "Случайно", "SILENT_ORIGIN.Moonborn": "Лунная порода", "SILENT_ORIGIN.Thicketborn": "Сыны Чащи",
        "SILENT_ORIGIN.Marshborn": "Тинники", "SILENT_ORIGIN.None": "Без происхождения",
        "BACKGROUND.title": "Предыстория", "BACKGROUND.hover.title": "Предыстория",
        "BACKGROUND.hover.desc": "1-я/2-я/3-я предыстория выбранного происхождения или Бродяга. Несделанные предыстории заменяются случайной готовой.",
        "BACKGROUND.Random": "Случайно", "BACKGROUND.First": "Первая", "BACKGROUND.Second": "Вторая", "BACKGROUND.Third": "Третья",
        "BACKGROUND.Vagrant": "Бродяга", "BACKGROUND.None": "Без предыстории",
    },
}

# Ключи rest_site_ui — без префикса мода (OptionId уже содержит SLAYTHEREQUIEM_).
STAGE3_REST_SITE = {
    "eng": {
        "OPTION_SLAYTHEREQUIEM_BREW.name": "Brew",
        "OPTION_SLAYTHEREQUIEM_BREW.description": "Brew one potion or poison from your reagents ({Count} in the pouch). Doesn't end your rest.",
        "OPTION_SLAYTHEREQUIEM_BREW.descriptionDisabled": "Not enough reagents for any recipe.",
        "OPTION_SLAYTHEREQUIEM_CHEST.name": "Chest",
        "OPTION_SLAYTHEREQUIEM_CHEST.description": "Store cards and gear (they stop weighing) or take them back. Burden {Weight}/{Capacity}, in the chest: {Stored}. Doesn't end your rest.",
        "SLAYTHEREQUIEM_CHEST_STORE_PROMPT": "Choose cards to put in the chest",
        "SLAYTHEREQUIEM_CHEST_TAKE_PROMPT": "Choose cards to take from the chest",
    },
    "rus": {
        "OPTION_SLAYTHEREQUIEM_BREW.name": "Варка",
        "OPTION_SLAYTHEREQUIEM_BREW.description": "Сварить одно зелье или яд из реагентов (в сумке: {Count}). Привал не заканчивается.",
        "OPTION_SLAYTHEREQUIEM_BREW.descriptionDisabled": "Реагентов не хватает ни на один рецепт.",
        "OPTION_SLAYTHEREQUIEM_CHEST.name": "Сундук",
        "OPTION_SLAYTHEREQUIEM_CHEST.description": "Убрать карты и снаряжение (перестают весить) или забрать обратно. Груз {Weight}/{Capacity}, в сундуке: {Stored}. Привал не заканчивается.",
        "SLAYTHEREQUIEM_CHEST_STORE_PROMPT": "Выберите карты, которые убрать в сундук",
        "SLAYTHEREQUIEM_CHEST_TAKE_PROMPT": "Выберите карты, которые забрать из сундука",
    },
}

# ───────────── снаряжение Подлых ударов (с пометкой веса, как RELICS в build_loc.py) ─────────────
STAGE3_EQUIPMENT = [
    ("ScaledDagger", "Scaled Dagger", "Weapon (dagger III). Weapon attacks cost 1 less ◆. [gold]Sneaky[/gold]: ×1.5, Bleed 1; sneaky hits restore 1 ✦.",
     "Чешуйчатый кинжал", "Оружие (кинжал III). Оружейные атаки на 1 ◆ дешевле. [gold]Подлый[/gold]: ×1.5, Кровотечение 1; подлый удар возвращает 1 ✦.",
     "Scales of something that is no longer hungry.", "Чешуя того, кто больше не голоден."),
    ("TwilightWidowBlade", "Blade of the Twilight Widow", "Artifact (dagger). [gold]Sneaky[/gold] ×2 instead of ×1.5. Killing with a weapon attack: [gold]Invisibility[/gold] until the end of your turn.",
     "Клинок Сумеречной Вдовы", "Артефакт (кинжал). [gold]Подлый[/gold] ×2 вместо ×1.5. Убийство оружейной атакой — [gold]Невидимость[/gold] до конца хода.",
     "She never mourns for long.", "Она никогда не скорбит долго."),
    ("SmugglerCoif", "Smuggler's Coif", "Light armor II. Set [gold]Smuggler's Mail[/gold]: 2 — +1 Dodge charge; 4 — after a Dodge your next attack is [gold]Sneaky[/gold].",
     "Капюшон контрабандиста", "Лёгкая броня II. Комплект [gold]Кольчуга контрабандиста[/gold]: 2 — +1 заряд Уворота; 4 — после Уворота следующая атака [gold]Подлая[/gold].",
     "Rings sewn on the inside.", "Кольца нашиты изнутри."),
    ("SmugglerHauberk", "Smuggler's Hauberk", "Light armor II. Set [gold]Smuggler's Mail[/gold]: 2 — +1 Dodge charge; 4 — after a Dodge your next attack is [gold]Sneaky[/gold].",
     "Кольчуга контрабандиста", "Лёгкая броня II. Комплект [gold]Кольчуга контрабандиста[/gold]: 2 — +1 заряд Уворота; 4 — после Уворота следующая атака [gold]Подлая[/gold].",
     "Quiet as a whisper, until it isn't.", "Тиха, как шёпот, — пока не звякнет."),
    ("SmugglerBracers", "Smuggler's Bracers", "Light armor II. Set [gold]Smuggler's Mail[/gold]: 2 — +1 Dodge charge; 4 — after a Dodge your next attack is [gold]Sneaky[/gold].",
     "Наручи контрабандиста", "Лёгкая броня II. Комплект [gold]Кольчуга контрабандиста[/gold]: 2 — +1 заряд Уворота; 4 — после Уворота следующая атака [gold]Подлая[/gold].",
     "A hidden pocket in each.", "В каждом — потайной кармашек."),
    ("SmugglerBoots", "Smuggler's Boots", "Light armor II. Set [gold]Smuggler's Mail[/gold]: 2 — +1 Dodge charge; 4 — after a Dodge your next attack is [gold]Sneaky[/gold].",
     "Сапоги контрабандиста", "Лёгкая броня II. Комплект [gold]Кольчуга контрабандиста[/gold]: 2 — +1 заряд Уворота; 4 — после Уворота следующая атака [gold]Подлая[/gold].",
     "Soles worn by a thousand back alleys.", "Подошвы стёрты тысячей подворотен."),
    ("BootsOfSilence", "Boots of Silence", "Unique light armor (feet). Your attacks don't remove [gold]Invisibility[/gold].",
     "Сапоги Безмолвия", "Уникальная лёгкая броня (ноги). Ваши атаки не снимают [gold]Невидимость[/gold].",
     "You hear nothing. Neither do they.", "Вы ничего не слышите. Они — тоже."),
]
