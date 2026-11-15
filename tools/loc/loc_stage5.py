"""Тексты этапа 5: лагерь (очки, Готовка, Зачарование, Медитация, Молитва, Развитие), Летопись героя, деревья навыков,
Кличи (docs/design/02 §5, 04, 10)."""

# ───────────── узлы деревьев навыков ─────────────
# (класс-суффикс Skill, eng title, eng desc, rus title, rus desc)
SKILLS = [
    # Ремесло
    ("CraftAlchemy", "Alchemy", "Brewing makes potions and poisons one tier higher.",
     "Алхимия", "Варка — зелья и яды ступенью выше."),
    ("CraftEnchanting", "Enchanting", "Each item holds 2 enchantments instead of 1.",
     "Зачарование", "Предмет держит 2 чар вместо 1."),
    ("CraftSmithing", "Smithing", "The Forge can improve items up to +3 ([gold]Legendary[/gold]).",
     "Кузнечное дело", "Кузня улучшает вещи до +3 ([gold]Легендарный[/gold])."),
    ("CraftArtisan", "Master Artisan", "Once per rest, your first craft (Brew, Forge, Enchant) refunds 1 camp point.",
     "Мастер-ремесленник", "Раз за привал первое ремесло (Варка, Кузня, Зачарование) возвращает 1 очко лагеря."),
    # Ратное дело
    ("WeaponTraining", "Weapon Training", "No [gold]Clumsy Grip[/gold] penalty with any weapon or shield.",
     "Владение оружием", "Никакого [gold]Неумелого хвата[/gold] ни с каким оружием и щитом."),
    ("Conditioning", "Conditioning", "+1 max ◆.", "Закалка тела", "+1 пул ◆."),
    ("SecondBreath", "Second Breath", "Once per combat, if a turn starts with 0 ◆, gain 3 ◆.",
     "Второе дыхание", "Раз за бой: если ход начался без ◆ — +3 ◆."),
    ("Veteran", "Veteran", "[gold]Poise[/gold] +1.", "Ветеран", "[gold]Стойкость[/gold] +1."),
    # Выживание
    ("CampKettle", "Camp Kettle", "Unlocks [gold]Cooking[/gold] at camp.", "Походный котёл", "Открывает [gold]Готовку[/gold] в лагере."),
    ("DeftPacking", "Deft Packing", "+4 burden. Move any number of items at the camp Chest.",
     "Ловкая укладка", "+4 груза. В Сундуке лагеря — сколько угодно вещей за раз."),
    ("FirstAid", "First Aid", "Short Rest heals 20% instead of 15%.", "Первая помощь", "Короткий привал лечит 20 % вместо 15 %."),
    ("Wayfarer", "Wayfarer", "+1 camp point.", "Опытный путник", "+1 очко лагеря."),
    ("Acclimatization", "Acclimatization", "Fire and Frost Resistance I for the first 2 turns of each combat.",
     "Акклиматизация", "Сопротивление огню и холоду I на первые 2 хода боя."),
    ("TradeName", "Trade Name", "Shop prices −10%.", "Торговое имя", "Цены в лавках −10 %."),
    ("WellRested", "Well Rested", "After a Rest: [gold]Vigor[/gold] — +1 ◆ and ✦ regeneration for the next 2 combats.",
     "Отдохнувший", "После Отдыха: [gold]Бодрость[/gold] — +1 прирост ◆ и ✦ на 2 следующих боя."),
    ("CampMaster", "Camp Master", "+1 camp point; Forge and Enchant cost 1.",
     "Мастер привала", "+1 очко лагеря; Кузня и Зачарование стоят 1."),
    ("HerbLore", "Herb Lore", "Reagents from enemies ×2; Brewing one tier higher.",
     "Знаток трав", "Реагенты с врагов ×2; Варка ступенью выше."),
    # Одноручное
    ("Fighter", "Fighter", "One-handed weapon attacks deal 1 more damage.", "Боец", "Атаки одноручным оружием +1 урона."),
    ("Fencer", "Fencer", "[gold]Sharp[/gold] sword crits deal ×2 instead of ×1.5.",
     "Фехтовальщик", "Крит [gold]Острого[/gold] меча — ×2 вместо ×1.5."),
    ("Butcher", "Butcher", "Axe attacks deal 3 more damage to enemies below 50% HP.",
     "Мясник", "Атаки топором +3 урона по врагам ниже 50 % ♥."),
    ("Crusher", "Crusher", "Mace attacks apply 1 [gold]Sunder[/gold].", "Дробитель", "Атаки булавой накладывают 1 [gold]Раскола[/gold]."),
    # Двуручное
    ("Sweep", "Sweep", "Two-handed weapon attacks deal 2 more damage.", "Размах", "Атаки двуручным оружием +2 урона."),
    ("HeavyHand", "Heavy Hand", "Two-handed weapon attacks apply 1 [gold]Stagger[/gold].",
     "Тяжёлая рука", "Атаки двуручным оружием накладывают 1 [gold]Шаткости[/gold]."),
    ("Shatter", "Shatter", "Two-handed weapon attacks: [gold]Pierce[/gold] 1.", "Сокрушение", "Атаки двуручным оружием: [gold]Пробитие[/gold] 1."),
    ("Relentless", "Relentless", "Your first two-handed weapon attack each turn costs 1 less ◆.",
     "Неудержимый", "Первая атака двуручным оружием за ход — на 1 ◆ дешевле."),
    # Блок
    ("ShieldBearer", "Shield Bearer", "With a shield, cards grant 1 more [gold]Block[/gold].",
     "Щитоносец", "Со щитом карты дают на 1 [gold]Блока[/gold] больше."),
    ("ShieldWall", "Shield Wall", "With a shield: [gold]Plating[/gold] +1.", "Стена щитов", "Со щитом: [gold]Латы[/gold] +1."),
    ("ShieldBash", "Shield Bash", "With a shield, your first attack each turn applies 2 [gold]Stagger[/gold].",
     "Удар щитом", "Со щитом первая атака хода накладывает 2 [gold]Шаткости[/gold]."),
    ("Bastion", "Bastion", "With a shield, gain 2 [gold]Block[/gold] at the start of each turn.",
     "Бастион", "Со щитом: 2 [gold]Блока[/gold] в начале каждого хода."),
    # Тяжёлая броня
    ("Hardened", "Hardened", "2+ heavy armor pieces: [gold]Plating[/gold] +1.", "Закалённый", "2+ вещи тяжёлой брони: [gold]Латы[/gold] +1."),
    ("Steadfast", "Steadfast", "[gold]Poise[/gold] +1 per 2 heavy armor pieces.", "Непреклонный", "[gold]Стойкость[/gold] +1 за каждые 2 вещи тяжёлой брони."),
    ("WeightBearer", "Weight Bearer", "Heavy armor no longer reduces ✦ regeneration.",
     "Привычка к весу", "Тяжёлая броня больше не снижает прирост ✦."),
    ("AgainstAllOdds", "Against All Odds", "Once per combat, falling below 25% HP grants 3 [gold]Plating[/gold].",
     "Вопреки всему", "Раз за бой при падении ниже 25 % ♥ — 3 [gold]Лат[/gold]."),
    # Кинжалы и копья
    ("Bloodletter", "Bloodletter", "Dagger and spear attacks apply 1 [gold]Bleed[/gold].",
     "Кровопускатель", "Атаки кинжалом и копьём накладывают 1 [gold]Кровотечения[/gold]."),
    ("Ambush", "Ambush", "[gold]Sneaky[/gold] dagger and spear attacks deal 3 more damage.",
     "Засада", "[gold]Подлые[/gold] атаки кинжалом и копьём +3 урона."),
    ("Precision", "Precision", "Dagger and spear attacks deal 1 more damage.", "Точность", "Атаки кинжалом и копьём +1 урона."),
    ("Hemorrhage", "Hemorrhage", "Attacks deal 2 more damage to [gold]Bleeding[/gold] enemies.",
     "Кровоизлияние", "Атаки +2 урона по врагам с [gold]Кровотечением[/gold]."),
    # Стрельба
    ("Marksman", "Marksman", "Bow and crossbow attacks deal 1 more damage.", "Меткий стрелок", "Атаки луком и арбалетом +1 урона."),
    ("QuickDraw", "Quick Draw", "Your first ranged weapon attack each turn costs 1 less ◆.",
     "Быстрая стрельба", "Первая атака луком или арбалетом за ход — на 1 ◆ дешевле."),
    ("AimedShot", "Aimed Shot", "Ranged weapon attacks deal 3 more damage on turn 1.",
     "Прицельный выстрел", "На первом ходу атаки луком и арбалетом +3 урона."),
    ("ArmorPiercer", "Armor Piercer", "Ranged weapon attacks: [gold]Pierce[/gold] 1.", "Бронебой", "Атаки луком и арбалетом: [gold]Пробитие[/gold] 1."),
    # Уклонение
    ("Nimble", "Nimble", "Start each combat with [gold]Dodge[/gold] 1.", "Ловкач", "[gold]Уворот[/gold] 1 в начале боя."),
    ("Counterstrike", "Counterstrike", "Start each combat with [gold]Riposte[/gold] 2.", "Контрудар", "[gold]Контрудар[/gold] 2 в начале боя."),
    ("LightStep", "Light Step", "2+ light armor pieces: +1 max ◆.", "Лёгкий шаг", "2+ вещи лёгкой брони: +1 пул ◆."),
    ("Elusive", "Elusive", "Every 3rd turn, gain [gold]Dodge[/gold] 1.", "Неуловимый", "Каждый 3-й ход — [gold]Уворот[/gold] 1."),
    # Яды и токсины
    ("Poisoner", "Poisoner", "Weapon poisons last 1 more charge.", "Отравитель", "Яды на оружии — на 1 заряд дольше."),
    ("Toxicologist", "Toxicologist", "[gold]Poison[/gold] you apply +1.", "Токсиколог", "Накладываемый вами [gold]Яд[/gold] +1."),
    ("VenomWard", "Venom Ward", "Poison Resistance +1 tier.", "Иммунитет к ядам", "Сопротивление ядам +1 ступень."),
    ("VenomMaster", "Venom Master", "Brewing poisons one tier higher.", "Мастер ядов", "Варка ядов ступенью выше."),
]

SCHOOLS = [
    # (школа, eng, rus, перк, eng title, eng desc, rus title, rus desc)
    ("Destruction", "Destruction", "Разрушения", "ElementLord", "Element Lord", "Destruction spells deal 2 more damage.",
     "Повелитель стихии", "Заклинания Разрушения +2 урона."),
    ("Restoration", "Restoration", "Восстановления", "Warden", "Warden", "[gold]Ward[/gold] +2.",
     "Хранитель", "[gold]Покров[/gold] +2."),
    ("Alteration", "Alteration", "Изменения", "Stoneskin", "Stoneskin", "[gold]Plating[/gold] +1.",
     "Каменная кожа", "[gold]Латы[/gold] +1."),
    ("Conjuration", "Conjuration", "Колдовства", "BoundMaster", "Bound Master", "Summons deal 2 more damage.",
     "Повелитель призванного", "Призывы +2 урона."),
    ("Illusion", "Illusion", "Иллюзии", "Phantasm", "Phantasm", "Your first Illusion spell each turn costs 1 less ✦.",
     "Фантазм", "Первое заклинание Иллюзии за ход — на 1 ✦ дешевле."),
]

SCHOOL_NAMES_RUS = {"Destruction": "Разрушение", "Restoration": "Восстановление", "Alteration": "Изменение",
                    "Conjuration": "Колдовство", "Illusion": "Иллюзия"}

NODE_PREFIX = {"eng": "Rank {Value}. Costs {Cost} skill point(s).\n", "rus": "Ярус {Value}. Очков навыка: {Cost}.\n"}
MASTERY_NOTE = {"eng": "\n+1 {school} mastery.", "rus": "\n+1 владение школой {school}."}


def _node_cards():
    cards = []
    for cls, et, ed, rt, rd in SKILLS:
        cards.append(("Node" + cls, et, NODE_PREFIX["eng"] + ed, rt, NODE_PREFIX["rus"] + rd))
    roman = ["I", "II", "III"]
    for school, eng, rus_gen, perk, pet, ped, prt, prd in SCHOOLS:
        for i in range(3):
            cards.append((f"Node{school}{roman[i]}", f"{eng} {roman[i]}", NODE_PREFIX["eng"] + f"Study of {eng}." + MASTERY_NOTE["eng"].format(school=eng),
                          f"{SCHOOL_NAMES_RUS[school]} {roman[i]}", NODE_PREFIX["rus"] + f"Изучение школы {rus_gen}." + MASTERY_NOTE["rus"].format(school=rus_gen)))
        cards.append((f"Node{perk}", pet, NODE_PREFIX["eng"] + ped + MASTERY_NOTE["eng"].format(school=eng),
                      prt, NODE_PREFIX["rus"] + prd + MASTERY_NOTE["rus"].format(school=rus_gen)))
    return cards


# ───────────── карты выбора лагеря ─────────────
PICK_CARDS = [
    # характеристики: Value — прибавка за очко, Cost — уже вложено
    ("PickStatHealth", "Health", "+{Value} max HP (invested: {Cost}).\nEvery 3 points — a tier: I Poison Resistance I, II +6 burden, III [gold]Plating[/gold] 1.",
     "Здоровье", "+{Value} макс. ♥ (вложено: {Cost}).\nКаждые 3 очка — ступень: I сопротивление ядам I, II +6 груза, III [gold]Латы[/gold] 1."),
    ("PickStatStamina", "Stamina", "+{Value} max ◆ (invested: {Cost}).\nEvery 3 points — a tier: I +1 ◆ regeneration, II attacks +1 damage, III the first attack each turn costs no ◆.",
     "Выносливость", "+{Value} пул ◆ (вложено: {Cost}).\nКаждые 3 очка — ступень: I +1 прирост ◆, II атаки +1 урона, III первая атака хода без ◆."),
    ("PickStatMana", "Magicka", "+{Value} max ✦ (invested: {Cost}).\nEvery 3 points — a tier: I +1 ✦ regeneration, II the first spell each turn costs 1 less ✦, III [gold]Ward[/gold] 3.",
     "Магия", "+{Value} пул ✦ (вложено: {Cost}).\nКаждые 3 очка — ступень: I +1 прирост ✦, II первое заклинание хода на 1 ✦ дешевле, III [gold]Покров[/gold] 3."),
    ("PickStatBurden", "Carry Weight", "+{Value} burden (invested: {Cost}).", "Переносимый вес", "+{Value} груза (вложено: {Cost})."),
    ("PickStatMastery", "Mastery", "+1 mastery of a school of your choice: native schools cost 1 point, foreign — 2. Free points: {Cost}.",
     "Владение", "+1 владение выбранной школой: родная — 1 очко, чужая — 2. Свободных очков: {Cost}."),
]
for _school, _eng, _rus_gen, *_ in SCHOOLS:
    PICK_CARDS.append((f"PickSchool{_school}", _eng, "Current mastery: {Value}. Raise by 1 for {Cost} stat point(s).",
                       SCHOOL_NAMES_RUS[_school], "Текущее владение: {Value}. Поднять на 1 за {Cost} очк. характеристик."))

PICK_CARDS += [
    # блюда: Value — боёв, Cost — реагентов
    ("PickDishBloodmossStew", "Bloodmoss Stew", "+6 max HP for {Value} combats. Uses {Cost} reagents.",
     "Похлёбка из кровяного мха", "+6 макс. ♥ на {Value} боя. Реагентов: {Cost}."),
    ("PickDishVenisonRoast", "Venison Roast", "+1 ◆ regeneration for {Value} combats. Uses {Cost} reagents.",
     "Жаркое из оленины", "+1 прирост ◆ на {Value} боя. Реагентов: {Cost}."),
    ("PickDishFishSoup", "Fish Soup", "+1 ✦ regeneration for {Value} combats. Uses {Cost} reagents.",
     "Рыбный суп с синецветом", "+1 прирост ✦ на {Value} боя. Реагентов: {Cost}."),
    ("PickDishSpicedSausage", "Spiced Sausage", "Frost Resistance I and +2 ◆ at the start of combat for {Value} combats. Uses {Cost} reagents.",
     "Пряная колбаса", "Сопротивление холоду I и +2 ◆ в начале боя на {Value} боя. Реагентов: {Cost}."),
    ("PickDishMushroomStew", "Mushroom Stew", "[gold]Regeneration[/gold] 1 for {Value} combats. Goes with any other food. Uses {Cost} reagents.",
     "Грибная похлёбка", "[gold]Регенерация[/gold] 1 на {Value} боя. Совместима с любой едой. Реагентов: {Cost}."),
    ("PickDishSweetRoll", "Sweet Roll", "Draw 1 more card on turn 1 of each combat, for {Value} combats. Uses {Cost} reagents.",
     "Сладкий рулет", "На 1-м ходу каждого боя +1 карта, {Value} боя. Реагентов: {Cost}."),
    ("PickDishFeast", "Feast", "Everything above for {Value} combat; removes Travel Fatigue. Uses {Cost} reagents.",
     "Пир", "Всё перечисленное на {Value} бой; снимает Усталость похода. Реагентов: {Cost}."),
    # чары: Value — сила, Cost — заряды (оружейные)
    ("PickEnchantHealth", "Fortify Health", "+{Value} max HP while equipped.", "Укрепление здоровья", "+{Value} макс. ♥, пока вещь надета."),
    ("PickEnchantStamina", "Fortify Stamina", "+{Value} max ◆ while equipped.", "Укрепление выносливости", "+{Value} пул ◆, пока вещь надета."),
    ("PickEnchantMagic", "Fortify Magicka", "+{Value} max ✦ while equipped.", "Укрепление магии", "+{Value} пул ✦, пока вещь надета."),
    ("PickEnchantFireResistance", "Resist Fire", "Fire Resistance {Value} while equipped (the best one counts).",
     "Сопротивление огню", "Сопротивление огню {Value}, пока вещь надета (считается лучшее)."),
    ("PickEnchantFrostResistance", "Resist Frost", "Frost Resistance {Value} while equipped (the best one counts).",
     "Сопротивление холоду", "Сопротивление холоду {Value}, пока вещь надета (считается лучшее)."),
    ("PickEnchantShockResistance", "Resist Shock", "Shock Resistance {Value} while equipped (the best one counts).",
     "Сопротивление разряду", "Сопротивление разряду {Value}, пока вещь надета (считается лучшее)."),
    ("PickEnchantWeaponMight", "Fortify Might", "Weapon attacks deal {Value} more damage.", "Укрепление силы", "Оружейные атаки +{Value} урона."),
    ("PickEnchantBurden", "Feather", "+{Value} burden while equipped.", "Пёрышко", "+{Value} груза, пока вещь надета."),
    ("PickEnchantFireBrand", "Fire Brand", "Weapon attacks apply {Value} [gold]Burning[/gold]. {Cost} charges.",
     "Огненное клеймо", "Оружейные атаки накладывают {Value} [gold]Горения[/gold]. Зарядов: {Cost}."),
    ("PickEnchantFrostBrand", "Frost Brand", "Weapon attacks apply {Value} [gold]Chill[/gold]. {Cost} charges.",
     "Ледяное клеймо", "Оружейные атаки накладывают {Value} [gold]Озноба[/gold]. Зарядов: {Cost}."),
    ("PickEnchantShockBrand", "Shock Brand", "Weapon attacks burn {Value} enemy ✦. {Cost} charges.",
     "Грозовое клеймо", "Оружейные атаки выжигают {Value} ✦ врага. Зарядов: {Cost}."),
    # отложенные чары 07 §6.1 (M4 п. 6)
    ("PickEnchantCraftDestruction", "Destruction Craft", "Your first {Value} Destruction spells each turn cost 1 less ✦.",
     "Ремесло Разрушения", "Первые {Value} заклинания Разрушения за ход стоят на 1 ✦ меньше."),
    ("PickEnchantCraftRestoration", "Restoration Craft", "Your first {Value} Restoration spells each turn cost 1 less ✦.",
     "Ремесло Восстановления", "Первые {Value} заклинания Восстановления за ход стоят на 1 ✦ меньше."),
    ("PickEnchantCraftAlteration", "Alteration Craft", "Your first {Value} Alteration spells each turn cost 1 less ✦.",
     "Ремесло Изменения", "Первые {Value} заклинания Изменения за ход стоят на 1 ✦ меньше."),
    ("PickEnchantCraftConjuration", "Conjuration Craft", "Your first {Value} Conjuration spells each turn cost 1 less ✦.",
     "Ремесло Колдовства", "Первые {Value} заклинания Колдовства за ход стоят на 1 ✦ меньше."),
    ("PickEnchantCraftIllusion", "Illusion Craft", "Your first {Value} Illusion spells each turn cost 1 less ✦.",
     "Ремесло Иллюзии", "Первые {Value} заклинания Иллюзии за ход стоят на 1 ✦ меньше."),
    ("PickEnchantAbsorbHealth", "Absorb Health", "Weapon attacks heal you {Value} HP per target. {Cost} charges.",
     "Поглощение здоровья", "Оружейные атаки лечат вас на {Value} ♥ за каждую цель. Зарядов: {Cost}."),
    ("PickEnchantAbsorbStamina", "Absorb Stamina", "Weapon attacks drain {Value} enemy ◆ and give you {Value} ◆. {Cost} charges.",
     "Поглощение выносливости", "Оружейные атаки отнимают у врага {Value} ◆ и дают вам {Value} ◆. Зарядов: {Cost}."),
    ("PickEnchantAbsorbMagic", "Absorb Magicka", "Weapon attacks burn {Value} enemy ✦ and give you {Value} ✦. {Cost} charges.",
     "Поглощение магии", "Оружейные атаки выжигают у врага {Value} ✦ и дают вам {Value} ✦. Зарядов: {Cost}."),
    ("PickEnchantBanish", "Banish", "Weapon attacks instantly kill summoned enemies. A charge is spent only on a kill. {Cost} charges.",
     "Изгнание", "Оружейные атаки мгновенно убивают призванных врагов. Заряд тратится только на убийство. Зарядов: {Cost}."),
    ("PickEnchantParalysis", "Paralysis", "Once per combat, a weapon attack paralyzes the target (power {Value}; bosses are Staggered instead). {Cost} charges.",
     "Паралич", "Раз в бой Оружейная атака парализует цель (сила {Value}; боссы вместо этого получают Шаткость). Зарядов: {Cost}."),
    # камни: Value — сколько есть, Cost — сила
    ("PickStoneSmall", "Lesser Soul Stone", "Enchantment power {Cost}. You have {Value}.", "Малый камень душ", "Сила чар {Cost}. Есть: {Value}."),
    ("PickStoneLarge", "Greater Soul Stone", "Enchantment power {Cost}. You have {Value}.", "Большой камень душ", "Сила чар {Cost}. Есть: {Value}."),
    ("PickStoneBlack", "Black Soul Stone", "Enchantment power {Cost}. You have {Value}.", "Чёрный камень душ", "Сила чар {Cost}. Есть: {Value}."),
    # Медитация и Молитва
    ("PickMeditateStamina", "Meditate: Body", "Stamina tier becomes {Value} until the end of the next combat.",
     "Медитация: тело", "Ступень выносливости — {Value} до конца следующего боя."),
    ("PickMeditateMana", "Meditate: Mind", "Magicka tier becomes {Value} until the end of the next combat.",
     "Медитация: разум", "Ступень магии — {Value} до конца следующего боя."),
    ("PickPrayRedeem", "Atonement", "Righteousness +1 step (once per act).", "Искупление", "Праведность +1 ступень (раз в акт)."),
    ("PickPraySwitch", "Change Blessing", "Take the blessing of this altar's god instead of your current one.",
     "Сменить благословение", "Принять благословение бога этого алтаря вместо нынешнего."),
]

# ───────────── Кличи ─────────────
SHOUT_NOTE = {"eng": "\n[gold]Shout[/gold]: returns to your hand in {turns} turns.", "rus": "\n[gold]Клич[/gold]: вернётся в руку через {turns} {word}."}


def _turns_ru(n: int) -> str:
    """1 ход, 2–4 хода, 5–20 ходов."""
    return "ход" if n % 10 == 1 and n % 100 != 11 else "хода" if 2 <= n % 10 <= 4 and not 12 <= n % 100 <= 14 else "ходов"

SHOUT_CARDS = [
    ("BattleCry", 4, "Battle Cry", "Apply {WeakPower:diff()} [gold]Weak[/gold] and {StaggerPower:diff()} [gold]Stagger[/gold] to ALL enemies.",
     "Боевой клич", "Наложить {WeakPower:diff()} [gold]Слабости[/gold] и {StaggerPower:diff()} [gold]Шаткости[/gold] ВСЕМ врагам."),
    ("UnrelentingForce", 3, "Unrelenting Force", "Deal {Damage:diff()} damage.\nApply {StaggerPower:diff()} [gold]Stagger[/gold].",
     "Безжалостная сила", "Нанести {Damage:diff()} урона.\nНаложить {StaggerPower:diff()} [gold]Шаткости[/gold]."),
    ("FireBreath", 3, "Fire Breath", "Deal {Damage:diff()} [red]fire[/red] damage to ALL enemies.\nApply {BurningPower:diff()} [gold]Burning[/gold] to ALL enemies.",
     "Огненный выдох", "Нанести {Damage:diff()} урона [red]огнём[/red] ВСЕМ врагам.\nНаложить {BurningPower:diff()} [gold]Горения[/gold] ВСЕМ врагам."),
    ("FrostBreath", 3, "Frost Breath", "Apply {ChillPower:diff()} [gold]Chill[/gold] to ALL enemies, double to those already Chilled.",
     "Морозный выдох", "Наложить {ChillPower:diff()} [gold]Озноба[/gold] ВСЕМ врагам, уже охлаждённым — вдвое."),
    ("StormCall", 4, "Storm Call", "Deal {Damage:diff()} [blue]shock[/blue] damage to ALL enemies.\n[gold]Shock[/gold]: apply {StaggerPower:diff()} [gold]Stagger[/gold] to ALL enemies.",
     "Зов грозы", "Нанести {Damage:diff()} урона [blue]разрядом[/blue] ВСЕМ врагам.\n[gold]Шок[/gold]: {StaggerPower:diff()} [gold]Шаткости[/gold] ВСЕМ врагам."),
    ("SoulTear", 5, "Soul Tear", "Deal {Damage:diff()} damage. If the target falls below 25% HP, it dies (not bosses).",
     "Разрыв души", "Нанести {Damage:diff()} урона. Если у цели осталось меньше 25 % ♥ — она погибает (не боссы)."),
]


def _shout_cards():
    return [(cls, et, ed + SHOUT_NOTE["eng"].format(turns=t), rt, rd + SHOUT_NOTE["rus"].format(turns=t, word=_turns_ru(t)))
            for cls, t, et, ed, rt, rd in SHOUT_CARDS]


STAGE5_CARDS = _node_cards() + PICK_CARDS + _shout_cards()

STAGE5_POWERS = [
    ("ShoutShelfPower", "Shouts Recharging", "{Amount} Shout(s) recharging; they return to your hand at the start of a turn.",
     "Кличи на полке", "Кличей на перезарядке: {Amount}; вернутся в руку в начале хода."),
]

STAGE5_RELICS = [
    ("HeroRecord", "Hero's Chronicle",
     "Level {Level} (renown {Renown}, {ToNext} to the next level). Unspent: {StatPoints} stat and {SkillPoints} skill points — spend them "
     "at camp ([gold]Train[/gold]). Skills learned: {Skills}. Meals: {Fed}. Travel Fatigue: {Fatigue}. Soul stones: {Stones}.\n"
     "Renown: 1 per combat, 3 per elite, 6 per boss. Shown number — level.",
     "Летопись героя",
     "Уровень {Level} (Слава {Renown}, до следующего — {ToNext}). Не потрачено: очков характеристик {StatPoints}, навыков {SkillPoints} — "
     "тратятся в лагере ([gold]Развитие[/gold]). Изучено навыков: {Skills}. Сытость: {Fed}. Усталость похода: {Fatigue}. Камни душ: {Stones}.\n"
     "Слава: бой 1, элита 3, босс 6. Число на иконке — уровень.",
     "Every scar has a line in it.", "У каждого шрама в ней своя строка."),
]

_COST = {"eng": " Costs {Cost} camp point(s), you have {Points}.", "rus": " Цена: {Cost} очк. лагеря, есть {Points}."}

STAGE5_REST_SITE = {
    "eng": {
        "OPTION_SLAYTHEREQUIEM_BREW.description": "Brew one potion or poison from your reagents ({Count} in the pouch)." + _COST["eng"],
        "OPTION_SLAYTHEREQUIEM_CHEST.description": "Store cards and gear (they stop weighing) or take them back. Burden {Weight}/{Capacity}, in the chest: {Stored}. Free.",
        "OPTION_SLAYTHEREQUIEM_FORGE.description": "Improve one equipped item by +1 (up to +2, [gold]Smithing[/gold] — +3) or shoe your pack animal." + _COST["eng"],
        "OPTION_SLAYTHEREQUIEM_SHORT_REST.name": "Short Rest",
        "OPTION_SLAYTHEREQUIEM_SHORT_REST.description": "Heal {Percent}% of max HP ({Heal})." + _COST["eng"],
        "OPTION_SLAYTHEREQUIEM_SHORT_REST.descriptionDisabled": "Not enough camp points.",
        "OPTION_SLAYTHEREQUIEM_COOK.name": "Cook",
        "OPTION_SLAYTHEREQUIEM_COOK.description": "Cook a meal from reagents: its effect lasts 3 combats." + _COST["eng"],
        "OPTION_SLAYTHEREQUIEM_COOK.descriptionDisabled": "Not enough reagents for any dish.",
        "OPTION_SLAYTHEREQUIEM_ENCHANT.name": "Enchant",
        "OPTION_SLAYTHEREQUIEM_ENCHANT.description": "Spend a soul stone ({Stones} left) to enchant an equipped item." + _COST["eng"],
        "OPTION_SLAYTHEREQUIEM_ENCHANT.descriptionDisabled": "No soul stones or nothing to enchant.",
        "OPTION_SLAYTHEREQUIEM_MEDITATE.name": "Meditate",
        "OPTION_SLAYTHEREQUIEM_MEDITATE.description": "+1 Stamina or Magicka tier until the end of the next combat." + _COST["eng"],
        "OPTION_SLAYTHEREQUIEM_MEDITATE.descriptionDisabled": "Already meditated, or both tiers are at III.",
        "OPTION_SLAYTHEREQUIEM_PRAY.name": "Pray",
        "OPTION_SLAYTHEREQUIEM_PRAY.description": "At this altar: change your blessing to its god, or atone (+1 righteousness, once per act)." + _COST["eng"],
        "OPTION_SLAYTHEREQUIEM_PRAY.descriptionDisabled": "Nothing to pray for here.",
        "OPTION_SLAYTHEREQUIEM_TRAIN.name": "Train",
        "OPTION_SLAYTHEREQUIEM_TRAIN.description": "Level {Level}: spend {StatPoints} stat and {SkillPoints} skill points. Free.",
        "OPTION_SLAYTHEREQUIEM_TRAIN.descriptionDisabled": "No unspent points.",
        "SLAYTHEREQUIEM_COOK_PROMPT": "Choose a dish",
        "SLAYTHEREQUIEM_STONE_PROMPT": "Choose a soul stone",
        "SLAYTHEREQUIEM_ENCHANT_PROMPT": "Choose an enchantment",
        "SLAYTHEREQUIEM_MEDITATE_PROMPT": "Choose what to meditate on",
        "SLAYTHEREQUIEM_PRAY_PROMPT": "Choose a prayer",
        "SLAYTHEREQUIEM_TRAIN_PROMPT": "Spend a point (close to finish)",
        "SLAYTHEREQUIEM_SCHOOL_PROMPT": "Choose a school",
    },
    "rus": {
        "OPTION_SLAYTHEREQUIEM_BREW.description": "Сварить одно зелье или яд из реагентов (в сумке: {Count})." + _COST["rus"],
        "OPTION_SLAYTHEREQUIEM_CHEST.description": "Убрать карты и снаряжение (перестают весить) или забрать обратно. Груз {Weight}/{Capacity}, в сундуке: {Stored}. Бесплатно.",
        "OPTION_SLAYTHEREQUIEM_FORGE.description": "Улучшить одну надетую вещь на +1 (до +2, с [gold]Кузнечным делом[/gold] — до +3) или подковать вьючного зверя." + _COST["rus"],
        "OPTION_SLAYTHEREQUIEM_SHORT_REST.name": "Короткий привал",
        "OPTION_SLAYTHEREQUIEM_SHORT_REST.description": "Вылечить {Percent} % макс. ♥ ({Heal})." + _COST["rus"],
        "OPTION_SLAYTHEREQUIEM_SHORT_REST.descriptionDisabled": "Не хватает очков лагеря.",
        "OPTION_SLAYTHEREQUIEM_COOK.name": "Готовка",
        "OPTION_SLAYTHEREQUIEM_COOK.description": "Приготовить еду из реагентов: эффект на 3 боя." + _COST["rus"],
        "OPTION_SLAYTHEREQUIEM_COOK.descriptionDisabled": "Реагентов не хватает ни на одно блюдо.",
        "OPTION_SLAYTHEREQUIEM_ENCHANT.name": "Зачарование",
        "OPTION_SLAYTHEREQUIEM_ENCHANT.description": "Потратить камень душ (осталось {Stones}) и зачаровать надетую вещь." + _COST["rus"],
        "OPTION_SLAYTHEREQUIEM_ENCHANT.descriptionDisabled": "Нет камней душ или нечего зачаровать.",
        "OPTION_SLAYTHEREQUIEM_MEDITATE.name": "Медитация",
        "OPTION_SLAYTHEREQUIEM_MEDITATE.description": "+1 к ступени выносливости или магии до конца следующего боя." + _COST["rus"],
        "OPTION_SLAYTHEREQUIEM_MEDITATE.descriptionDisabled": "Медитация уже была, или обе ступени — III.",
        "OPTION_SLAYTHEREQUIEM_PRAY.name": "Молитва",
        "OPTION_SLAYTHEREQUIEM_PRAY.description": "У этого алтаря: сменить благословение на его бога или искупиться (+1 праведность, раз в акт)." + _COST["rus"],
        "OPTION_SLAYTHEREQUIEM_PRAY.descriptionDisabled": "Здесь не о чем молиться.",
        "OPTION_SLAYTHEREQUIEM_TRAIN.name": "Развитие",
        "OPTION_SLAYTHEREQUIEM_TRAIN.description": "Уровень {Level}: потратить очки характеристик ({StatPoints}) и навыков ({SkillPoints}). Бесплатно.",
        "OPTION_SLAYTHEREQUIEM_TRAIN.descriptionDisabled": "Нет неистраченных очков.",
        "SLAYTHEREQUIEM_COOK_PROMPT": "Выберите блюдо",
        "SLAYTHEREQUIEM_STONE_PROMPT": "Выберите камень душ",
        "SLAYTHEREQUIEM_ENCHANT_PROMPT": "Выберите чары",
        "SLAYTHEREQUIEM_MEDITATE_PROMPT": "Выберите, на чём сосредоточиться",
        "SLAYTHEREQUIEM_PRAY_PROMPT": "Выберите молитву",
        "SLAYTHEREQUIEM_TRAIN_PROMPT": "Потратьте очко (закройте, чтобы закончить)",
        "SLAYTHEREQUIEM_SCHOOL_PROMPT": "Выберите школу",
    },
}

# Описание «недоступно» для действий, которые раньше его не имели (Сундук бесплатен, но вне системы не нужен).
STAGE5_REST_SITE["eng"]["OPTION_SLAYTHEREQUIEM_CHEST.descriptionDisabled"] = "Nothing to store or take."
STAGE5_REST_SITE["rus"]["OPTION_SLAYTHEREQUIEM_CHEST.descriptionDisabled"] = "Нечего убрать или забрать."
