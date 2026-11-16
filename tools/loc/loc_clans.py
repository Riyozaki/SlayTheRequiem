"""Тексты кланов Латоносца: Горнокровные, Дюнные клинки, Цитадельцы и их 9 предысторий (docs/design/04, M4 п. 1).
Подключается в build_loc.py."""

# (класс, eng title, eng text, rus title, rus text)
CLANS_CARDS = [
    ("AimedShot", "Aimed Shot", "Deal {Damage:diff()} damage.\nWith a bow or crossbow, apply {VulnerablePower:diff()} [gold]Vulnerable[/gold].",
     "Прицельный выстрел", "Нанести {Damage:diff()} урона.\nС луком или арбалетом — наложить {VulnerablePower:diff()} [gold]Уязвимости[/gold]."),
    ("BladeSpirit", "Blade Spirit", "Summon or dismiss the blade spirit. While it is summoned, your attacks spend 1 ✦ and deal extra [purple]magic[/purple] damage equal to half your ✦.",
     "Дух клинка", "Призвать или отпустить дух клинка. Пока он призван, ваши атаки тратят 1 ✦ и наносят дополнительный урон [purple]магией[/purple] — половину ваших ✦."),
]

CLANS_POWERS = [
    ("LightSpiritPower", "Spirit of Light", "At the start of your turn, deal {Amount} [gold]light[/gold] damage to a random enemy.",
     "Дух света", "В начале хода — {Amount} урона [gold]светом[/gold] случайному врагу."),
    ("BladeSpiritPower", "Blade Spirit", "Your attacks spend 1 ✦ and deal extra [purple]magic[/purple] damage equal to half your ✦ (before spending).",
     "Дух клинка", "Ваши атаки тратят 1 ✦ и наносят дополнительный урон [purple]магией[/purple] — половину ваших ✦ (до траты)."),
    ("LoyalGuardPower", "Loyal-to-the-End", "A shield-bearing guard with {Amount} HP: takes attacks first and strikes an enemy at the end of your turn. Grows with your level.",
     "Верный-до-Конца", "Страж со щитом, {Amount} ♥: принимает удары первым и в конце хода бьёт врага. Растёт с вашим уровнем."),
]

# (класс, eng title, eng desc, rus title, rus desc, flavor eng, flavor rus)
CLANS_RELICS = [
    ("ForgebloodOrigin", "Forgeblood", "Max HP +10, ✦ −1. [gold]Forge Fury[/gold]: if you played 3 or more Attacks this turn, take 25% less damage during the enemy turn.",
     "Горнокровные", "♥ +10, ✦ −1. [gold]Ярость горна[/gold]: если за ход сыграно 3+ Атаки, в ход врага вы получаете на 25 % меньше урона.",
     "The deep forges never cool.", "Глубинные горны не остывают."),
    ("DunebladeOrigin", "Duneblades", "Max ◆ +1. [gold]Poison resistance II[/gold]. [gold]Dune Rhythm[/gold]: ◆ regeneration +1 while below 75% ◆, +2 below 50%.",
     "Дюнные клинки", "Пул ◆ +1. [gold]Сопротивление ядам II[/gold]. [gold]Ритм дюн[/gold]: прирост ◆ +1 при ◆ ниже 75 %, +2 — ниже 50 %.",
     "The sand teaches patience, then speed.", "Песок учит терпению, а потом — скорости."),
    ("CitadelOrigin", "Citadel-born", "Max ◆ +1, capacity +2. [gold]Drill[/gold]: +1 extra stat point every 3 levels.",
     "Цитадельцы", "Пул ◆ +1, груз +2. [gold]Выучка[/gold]: каждые 3 уровня — +1 дополнительное очко характеристик.",
     "Rank, file and the next order.", "Шеренга, строй и следующий приказ."),
    ("BerserkerBackground", "Berserker", "Heavy attacks (2+ ◆) cost 1 less ◆. Start with an [gold]Iron Battleaxe[/gold].",
     "Берсерк", "Тяжёлые атаки (◆ 2+) на 1 ◆ дешевле. Стартовый [gold]Железный боевой топор[/gold].",
     "Rage is a kind of rest.", "Ярость — тоже отдых."),
    ("PoacherBackground", "Poacher", "Start with a [gold]Heavy Bow[/gold]. [gold]Aimed Shot[/gold] replaces a Strike. Bows and crossbows weigh nothing.",
     "Браконьер", "Стартовый [gold]Тяжёлый лук[/gold]. [gold]Прицельный выстрел[/gold] вместо Удара. Луки и арбалеты ничего не весят.",
     "Every forest has an owner. Not tonight.", "У каждого леса есть хозяин. Но не этой ночью."),
    ("ThunderShamanBackground", "Thunder Shaman", "+1 Destruction mastery. [gold]Sparks[/gold] replaces a Strike. Aura: enemies are vulnerable to shock (I).",
     "Громовой шаман", "+1 владения Разрушением. [gold]Искры[/gold] вместо Удара. Аура: враги уязвимы к разряду (I).",
     "The storm answers in the old tongue.", "Гроза отвечает на старом наречии."),
    ("DawnBrotherBackground", "Brother of Dawn", "+1 Restoration mastery. A free [gold]Spirit of Light[/gold] fights at your side: 2 [gold]light[/gold] damage to a random enemy at the start of your turn.",
     "Брат Рассвета", "+1 владения Восстановлением. Рядом бесплатный [gold]Дух света[/gold]: в начале хода 2 урона [gold]светом[/gold] случайному врагу.",
     "Dawn is a blade that never dulls.", "Рассвет — клинок, что не тупится."),
    ("ConspiratorBackground", "Conspirator", "Weapon poisons get +1 charge and +25% potency. Start with a [gold]Combined Poison I[/gold].",
     "Заговорщик", "Яды на оружии: +1 заряд и +25 % силы. Стартовый [gold]Комбинированный яд I[/gold].",
     "A toast to your health.", "За ваше здоровье."),
    ("AnseiBackground", "Ansei", "[gold]Blade Spirit[/gold] is in your deck: your attacks deal extra [purple]magic[/purple] damage equal to half your ✦, but cost 1 ✦.",
     "Клинок-ансей", "В колоде — [gold]Дух клинка[/gold]: атаки наносят дополнительный урон [purple]магией[/purple] — половину ваших ✦, но стоят 1 ✦.",
     "The sword is a prayer said quickly.", "Меч — это молитва, сказанная быстро."),
    ("LegionaryBackground", "Legionary", "Start with an [gold]Oak Shield[/gold]. [gold]Shield Bash[/gold] replaces a Strike. On the first turn of combat, Block from cards +2 while holding a shield.",
     "Легионер", "Стартовый [gold]Дубовый щит[/gold]. [gold]Удар щитом[/gold] вместо Удара. В первый ход боя Блок от карт +2, если в руке щит.",
     "Shields together. Always.", "Щиты сомкнуть. Всегда."),
    ("EnvoyBackground", "Envoy", "Each combat begins with [gold]Loyal-to-the-End[/gold] at your side: a guard that takes attacks first and grows with your level.",
     "Посланник", "Каждый бой рядом [gold]Верный-до-Конца[/gold]: страж, который принимает удары первым и растёт с вашим уровнем.",
     "He carries the letter. You carry him.", "Он несёт письмо. Вы — его."),
    ("CitadelMageBackground", "Citadel Mage", "+1 Alteration mastery. [gold]Telekinesis[/gold] replaces a Strike.",
     "Маг цитадели", "+1 владения Изменением. [gold]Импульс[/gold] вместо Удара.",
     "Walls are just slow spells.", "Стены — это медленные заклинания."),
    ("OrdinatorBackground", "Ordinator", "Start with all three gifts of the Tribunal: [gold]Treachery[/gold], [gold]Web Weaver[/gold] and [gold]Dusk Star[/gold]. "
     "Oath: the Righteous form, vampirism, lycanthropy and lichdom are forbidden, Celestial blessings are unavailable, "
     "other Princes are refused. Of the holy Paths, Paladin is open.",
     "Ординатор", "Старт со всеми тремя дарами Тройственных: [gold]Измена[/gold], [gold]Паутина[/gold] и [gold]Сумеречная Звезда[/gold]. "
     "Клятва: запрещены Праведник, вампиризм, ликантропия и лич, благословения Небесных недоступны, "
     "чужим Князьям — отказ. Из «святых» Путей открыт Паладин.",
     "The Tribunal sees. The Tribunal judges.", "Трибунал видит. Трибунал судит."),
    ("AshNomadBackground", "Ash Nomad", "Tribunal gifts are a tier stronger: Treachery — attacks +3, Pierce 2; "
     "Web Weaver — up to 8 healing per turn, +3 damage after the Vow; Dusk Star — +9 max HP, +3 max ✦, Fire and Frost resistance II.",
     "Пепельный кочевник", "Дары Тройственных на ступень сильнее: Измена — атаки +3, Пробитие 2; "
     "Паутина — до 8 лечения за ход, +3 урона после Обета; Звезда — +9 макс. ♥, +3 пул ✦, сопротивление огню и холоду II.",
     "The ash walks with those who walk on it.", "Пепел идёт с тем, кто по нему идёт."),
]

CLANS_EQUIPMENT = [
    ("IronBattleaxe", "Iron Battleaxe", "Two-handed weapon (I). Weapon attacks +3 damage and cost 1 more ◆. Cleaving ×2, Stagger 2 per hit.",
     "Железный боевой топор", "Двуручное оружие (I). Оружейные атаки +3 урона и на 1 ◆ дороже. Рубящий ×2, Шаткость 2 за удар.",
     "Forged for one purpose, used for several.", "Выкован для одного, годится для многого."),
    ("HeavyBow", "Heavy Bow", "Bow (I), two-handed. Weapon attacks are shots: +3 damage.",
     "Тяжёлый лук", "Лук (I), двуручный. Оружейные атаки — выстрелы: +3 урона.",
     "Draws like a stubborn mule.", "Тянется, как упрямый мул."),
]

CLANS_POTIONS = [
    ("CombinedPoisonI", "Combined Poison I", "Coat your weapon ({Charges} charges). Weapon attacks: [gold]Poison[/gold] {Amount} and drain 2 ◆.",
     "Комбинированный яд I", "Нанести на оружие ({Charges} заряда). Оружейные атаки: [gold]Яд[/gold] {Amount} и −2 ◆ цели."),
]

CLANS_SETTINGS = {
    "eng": {
        "PLATEBEARER_ORIGIN.hover.desc": "Frostborn, Forgeblood, Duneblades or Citadel-born. Random — by the run seed; Choose — a screen at the start of the run.",
        "PLATEBEARER_ORIGIN.Random": "Random", "PLATEBEARER_ORIGIN.Forgeblood": "Forgeblood",
        "PLATEBEARER_ORIGIN.Duneblade": "Duneblades", "PLATEBEARER_ORIGIN.Citadel": "Citadel-born", "PLATEBEARER_ORIGIN.Choose": "Choose",
    },
    "rus": {
        "PLATEBEARER_ORIGIN.hover.desc": "Инеерождённые, Горнокровные, Дюнные клинки или Цитадельцы. «Случайно» — по сиду забега, «На выбор» — экран в начале забега.",
        "PLATEBEARER_ORIGIN.Random": "Случайно", "PLATEBEARER_ORIGIN.Forgeblood": "Горнокровные",
        "PLATEBEARER_ORIGIN.Duneblade": "Дюнные клинки", "PLATEBEARER_ORIGIN.Citadel": "Цитадельцы", "PLATEBEARER_ORIGIN.Choose": "На выбор",
    },
}
