"""Тексты этапа 7: пять актов, элиты актов, супербоссы, уровни «Реквием», правила актов (docs/design/11, 13)."""


def _m(eng_name, rus_name, moves):
    """moves: {MOVE_ID: (eng, rus)} → формат MONSTERS. Повторы ходов (_2, _3) получают тот же заголовок."""
    eng, rus = {}, {}
    for move, (e, r) in moves.items():
        for suffix in ("", "_2", "_3"):
            eng[move + suffix] = e
            rus[move + suffix] = r
    return {"eng": {"name": eng_name, "moves": eng}, "rus": {"name": rus_name, "moves": rus}}


# ───────────── враги (ключ — класс в UPPER_SNAKE) ─────────────
STAGE7_MONSTERS = {
    # акт 1
    "PACK_LEADER": _m("Pack Leader", "Вожак стаи", {"HOWL": ("Howl", "Вой"), "MAUL": ("Maul", "Растерзать")}),
    "PACK_WOLF": _m("Pack Wolf", "Волк стаи", {"BITE": ("Bite", "Укус")}),
    "MOSS_ELDER": _m("Moss Elder", "Мшистый старейшина", {"ROOTS": ("Grasping Roots", "Цепкие корни"), "SLAM": ("Slam", "Удар"), "SWEEP": ("Sweep", "Взмах")}),
    "CAULDRON_MISTRESS": _m("Cauldron Mistress", "Хозяйка котла", {"DRINK": ("Drink Deep", "Глоток варева"), "LADLE": ("Ladle", "Половник"), "STIR": ("Stir the Pot", "Помешать котёл")}),
    # 2а
    "GLACIER_GIANT": _m("Glacier Giant", "Ледниковый великан", {"FIST": ("Ice Fist", "Ледяной кулак"), "FROST_BREATH": ("Frost Breath", "Морозное дыхание"), "STOMP": ("Stomp", "Топот")}),
    "PHANTOM_BLADE": _m("Phantom Blade", "Призрачный клинок", {"GUARD": ("Spectral Guard", "Призрачная защита"), "LUNGE": ("Lunge", "Выпад"), "TWIN_BLADES": ("Twin Blades", "Парные клинки")}),
    "FIRST_SNOW_PRINCE": _m("Prince of First Snow", "Принц Первого Снега", {"BLIZZARD": ("Blizzard", "Буран"), "ROYAL_GUARD": ("Royal Guard", "Королевская стража"), "SNOW_BLADE": ("Snow Blade", "Снежный клинок")}),
    "ICE_ATRONACH": _m("Ice Atronach", "Ледяной атронах", {"ICE_SHARD": ("Ice Shard", "Ледяной осколок")}),
    # 2б
    "DEEP_CENTURION": _m("Centurion of the Deep", "Центурион Глубин", {"OVERHEAT": ("Overheat", "Перегрев"), "PISTON_HAMMER": ("Piston Hammer", "Поршневой молот"), "STEAM_EXHALE": ("Steam Exhale", "Выдох пара")}),
    "SPIDER_MATRIARCH": _m("Spider Matriarch", "Паучиха-матриарх", {"BROOD": ("Brood", "Выводок"), "WEB_SPIT": ("Web Spit", "Плевок паутиной")}),
    "REPAIR_SPIDER": _m("Repair Spider", "Паук-ремонтник", {"REPAIR": ("Repair", "Починка")}),
    "RENEGADE_MECHANIC": _m("Renegade Mechanic", "Механик-отступник", {"DEPLOY_TURRET": ("Deploy Turret", "Поставить турель"), "WRENCH": ("Wrench", "Гаечный ключ")}),
    "MECHANIC_TURRET": _m("Turret", "Турель", {"BOLT": ("Bolt", "Болт")}),
    "REACTOR_HEART": _m("Reactor Heart", "Сердце реактора", {"CHARGE": ("Charge", "Зарядка"), "DISCHARGE": ("Discharge", "Разряд")}),
    "REACTOR_GUARD": _m("Reactor Guard", "Страж реактора", {"CLAMP": ("Clamp", "Захват"), "SHIELD_HEART": ("Shield the Heart", "Прикрыть сердце")}),
    # Бездна
    "DREMORA": _m("Dremora", "Дремора", {"ABYSS_BLADE": ("Abyss Blade", "Клинок Бездны"), "ABYSS_SHIELD": ("Abyss Shield", "Щит Бездны")}),
    "ABYSS_SCAMP": _m("Abyss Scamp", "Скамп Бездны", {"FIREBOLT": ("Firebolt", "Огненная стрела")}),
    "ABYSS_GATE_OBJECT": _m("Abyss Gate", "Врата Бездны", {"GATE_HUM": ("Hum", "Гул"), "GATE_SPAWN": ("Spill Forth", "Выплеск")}),
    # Улей
    "ABYSS_WARLORD": _m("Abyss Warlord", "Полководец Бездны", {"ABYSS_CLEAVE": ("Abyss Cleave", "Рассечь Бездной"), "OPEN_GATE": ("Open the Gate", "Открыть Врата"), "WAR_CRY": ("War Cry", "Боевой клич")}),
    "PLAGUE_MOTHER": _m("Plague Mother", "Мать чумы", {"FLY_SWARM": ("Fly Swarm", "Рой мух"), "ROT_TOUCH": ("Rotting Touch", "Гнилое касание")}),
    "SWARM_DEVOURER": _m("Swarm Devourer", "Пожиратель роя", {"DEVOUR": ("Devour", "Пожрать"), "GNASH": ("Gnash", "Грызть")}),
    "SWARMLING_BUG": _m("Swarmling", "Роевик", {"STING": ("Sting", "Жало")}),
    # Слава
    "BARROW_PRIEST": _m("Barrow Priest", "Жрец курганов", {"MASK_AURA": ("Aura of the Mask", "Аура маски"), "MASK_BOLT": ("Mask Bolt", "Удар маски")}),
    "BONE_CHAMPION": _m("Bone Champion", "Костяной поборник", {"BONE_WALL": ("Bone Wall", "Костяная стена"), "GREATSWORD": ("Greatsword", "Двуручник")}),
    "BONE_SHIELDBEARER": _m("Bone Shieldbearer", "Костяной щитоносец", {"SHIELD_BASH": ("Shield Bash", "Удар щитом")}),
    "ABYSS_BLACK_KNIGHT": _m("Black Knight of the Abyss", "Чёрный рыцарь Бездны", {"MIRROR_OMEN": ("Mirror Omen", "Зеркальное знамение"), "SUNDERING_BLOW": ("Sundering Blow", "Раскалывающий удар")}),
    # 4а
    "BLOOD_NOBLE": _m("Blood Noble", "Кровавый вельможа", {"BLOOD_BITE": ("Blood Bite", "Кровавый укус"), "RAPIER": ("Rapier", "Рапира"), "VANISH": ("Vanish", "Исчезнуть")}),
    "BLOOD_THRALL": _m("Blood Thrall", "Раб крови", {"THRALL_CLAW": ("Claw", "Когти")}),
    "GARGOYLE_WARDEN": _m("Gargoyle Warden", "Гаргулья-страж", {"NIGHT_CLAWS": ("Night Claws", "Ночные когти"), "STONE_SKIN": ("Stone Skin", "Каменная кожа")}),
    "VAMPIRE_HUNTER": _m("Vampire Hunter", "Охотник на вампиров", {"SET_TRAP": ("Set Trap", "Поставить капкан"), "SILVER_BOLT": ("Silver Bolt", "Серебряный болт")}),
    "LORDS_BRIDE": _m("Bride of the Lord", "Невеста Владыки", {"CRIMSON_KISS": ("Crimson Kiss", "Багровый поцелуй"), "SWAP_PLACES": ("Swap Places", "Поменяться местами")}),
    # 4б
    "SORCERESS_ILVARRE": _m("Sorceress Ilvarre", "Чародейка Ильварре", {"ADAPTIVE_SPELL": ("Adaptive Spell", "Подстраивающееся заклинание"), "WARD": ("Ward", "Оберег")}),
    "RENEGADE_SUMMONER": _m("Renegade Summoner", "Призыватель-отступник", {"ARC": ("Arc", "Дуга"), "CONJURE_ATRONACH": ("Conjure Atronach", "Вызвать атронаха")}),
    "FLAME_ATRONACH_MINION": _m("Flame Atronach", "Огненный атронах", {"FLAME_LASH": ("Flame Lash", "Огненный хлыст")}),
    "STORM_ATRONACH_MINION": _m("Storm Atronach", "Грозовой атронах", {"STORM_FIST": ("Storm Fist", "Грозовой кулак")}),
    "MIRROR_WARDEN": _m("Mirror Warden", "Зеркальный страж", {"MIRROR_SHARDS": ("Mirror Shards", "Зеркальные осколки"), "POLISH": ("Polish", "Полировка"), "PRISM_BEAM": ("Prism Beam", "Призматический луч")}),
    "STAR_EXAMINER": _m("Star Examiner", "Звёздный экзаменатор", {"ASK": ("Pose a Question", "Задать вопрос"), "GRADE": ("Grade", "Оценка")}),
    # супербоссы
    "BLACK_WANDERER": _m("The Black Wanderer", "Чёрный Странник", {"DUELIST_STRIKE": ("Duelist's Strike", "Удар дуэлянта"), "EBONY_FLURRY": ("Ebony Flurry", "Эбеновый шквал"), "SOUL_SALTS": ("Soul Salts", "Соли душ")}),
    "WRAITH_THRALL": _m("Wraith Thrall", "Раб-призрак", {"WRAITH_TOUCH": ("Wraith Touch", "Касание призрака")}),
    "BARROW_REAPER": _m("Reaper of the Barrows", "Жнец Курганов", {"CURSED_CRY": ("Cursed Cry", "Проклятый клич"), "FROST_SCYTHE": ("Frost Scythe", "Ледяная коса"), "RAISE_BARROW_DEAD": ("Raise the Barrow Dead", "Поднять мертвецов")}),
    "BARROW_DEAD": _m("Barrow Dead", "Курганный мертвец", {"BARROW_SLASH": ("Slash", "Рубящий удар")}),
    "DEEP_MASTER_SMITH": _m("Master Smith of the Deep", "Мастер-кузнец Глубин", {"FORGE_HAMMER": ("Forge Hammer", "Кузнечный молот"), "OPEN_VALVES": ("Open the Valves", "Открыть вентили"), "SMELTING_BLAST": ("Smelting Blast", "Плавильный взрыв")}),
    "STEAM_VALVE": _m("Steam Valve", "Вентиль", {"HISS": ("Hiss", "Шипение")}),
    "GATEBREAKER_ENVOY": _m("Envoy of the Gatebreaker", "Посланник Разрушителя Врат", {"ABYSS_HAMMER": ("Abyss Hammer", "Молот Бездны"), "OPEN_GATE": ("Open the Gate", "Открыть Врата")}),
    "BLOOD_COURT_LORD": _m("Lord of the Blood Court", "Владыка Кровавого Двора", {"BLOOD_CLAWS": ("Blood Claws", "Кровавые когти"), "MIST_FORM": ("Mist Form", "Обратиться туманом"), "SANGUINE_DRAIN": ("Sanguine Drain", "Испить крови")}),
    "GARGOYLE_SERVANT": _m("Gargoyle Servant", "Гаргулья-слуга", {"STONE_RAKE": ("Stone Rake", "Каменные когти")}),
    "FIRST_SUMMONER": _m("First Summoner of the Black Book", "Первый Призыватель Чёрной Книги", {"BLACK_TOME": ("Black Tome", "Чёрный том"), "CHAIN_LIGHTNING": ("Chain Lightning", "Цепь молний")}),
    "DRAGON_EGG": _m("Dragon Egg", "Драконье яйцо", {"SLUMBER": ("Slumber", "Дремлет")}),
    "CULTIST_THRALL": _m("Cultist Thrall", "Раб-культист", {"CHANT": ("Chant", "Песнопение"), "RITUAL_DAGGER": ("Ritual Dagger", "Ритуальный кинжал")}),
    "WORLD_EATER": _m("World-Eater, the Black Wing", "Пожиратель Миров, Чёрное Крыло", {"DEEP_BREATH": ("Deep Breath", "Вдох"), "DEVOURING_BITE": ("Devouring Bite", "Пожирающий укус"), "LAND": ("Land", "Приземлиться"), "METEOR_RAIN": ("Meteor Rain", "Метеоритный дождь"), "TAKE_WING": ("Take Wing", "Взлететь"), "WING_STORM": ("Wing Storm", "Буря крыльев")}),
    "FACELESS_PRIEST": _m("Faceless Priest", "Безликий жрец", {"FACELESS_WARD": ("Faceless Ward", "Безликий оберег"), "SOUL_DRAIN": ("Soul Drain", "Испить душу")}),
    "NAMELESS_SHADOW": _m("The Shadow Without a Name", "Тень Без Имени", {"CALL_SOULS": ("Call the Souls", "Зов душ"), "UMBRA_STRIKE": ("Umbral Strike", "Удар тени")}),
    "SHADOW_SOUL": _m("Shadow Soul", "Душа Тени", {"SOUL_BURST": ("Soul Burst", "Взрыв души"), "SOUL_GATHER": ("Gathering", "Сгущается")}),
    "ECHO_KNIGHT": _m("Echo Knight", "Рыцарь-эхо", {"ECHO_FLURRY": ("Echo Flurry", "Шквал эха"), "ECHO_GUARD": ("Echo Guard", "Защита эха")}),
}

STAGE7_ENCOUNTERS = {
    "PACK_LEADER_ELITE": {"eng": "Wolf Pack", "rus": "Волчья стая"},
    "MOSS_ELDER_ELITE": {"eng": "Moss Elder", "rus": "Мшистый старейшина"},
    "CAULDRON_MISTRESS_ELITE": {"eng": "Cauldron Mistress", "rus": "Хозяйка котла"},
    "GLACIER_GIANT_ELITE": {"eng": "Glacier Giant", "rus": "Ледниковый великан"},
    "PHANTOM_BLADE_ELITE": {"eng": "Phantom Blade", "rus": "Призрачный клинок"},
    "FIRST_SNOW_PRINCE_ELITE": {"eng": "Prince of First Snow", "rus": "Принц Первого Снега"},
    "DEEP_CENTURION_ELITE": {"eng": "Centurion of the Deep", "rus": "Центурион Глубин"},
    "SPIDER_MATRIARCH_ELITE": {"eng": "Spider Matriarch", "rus": "Паучиха-матриарх"},
    "RENEGADE_MECHANIC_ELITE": {"eng": "Renegade Mechanic", "rus": "Механик-отступник"},
    "REACTOR_HEART_ELITE": {"eng": "Reactor Heart", "rus": "Сердце реактора"},
    "ABYSS_RIFT_NORMAL": {"eng": "Abyss Breach", "rus": "Прорыв Бездны"},
    "ABYSS_WARLORD_ELITE": {"eng": "Abyss Warlord", "rus": "Полководец Бездны"},
    "PLAGUE_MOTHER_ELITE": {"eng": "Plague Mother", "rus": "Мать чумы"},
    "SWARM_DEVOURER_ELITE": {"eng": "Swarm Devourer", "rus": "Пожиратель роя"},
    "BARROW_PRIEST_ELITE": {"eng": "Barrow Priest", "rus": "Жрец курганов"},
    "BONE_CHAMPION_ELITE": {"eng": "Bone Champion", "rus": "Костяной поборник"},
    "ABYSS_BLACK_KNIGHT_ELITE": {"eng": "Black Knight of the Abyss", "rus": "Чёрный рыцарь Бездны"},
    "BLOOD_NOBLE_ELITE": {"eng": "Blood Noble", "rus": "Кровавый вельможа"},
    "GARGOYLE_WARDEN_ELITE": {"eng": "Gargoyle Warden", "rus": "Гаргулья-страж"},
    "VAMPIRE_HUNTER_ELITE": {"eng": "Vampire Hunter", "rus": "Охотник на вампиров"},
    "LORDS_BRIDE_ELITE": {"eng": "Bride of the Lord", "rus": "Невеста Владыки"},
    "SORCERESS_ILVARRE_ELITE": {"eng": "Sorceress Ilvarre", "rus": "Чародейка Ильварре"},
    "RENEGADE_SUMMONER_ELITE": {"eng": "Renegade Summoner", "rus": "Призыватель-отступник"},
    "MIRROR_WARDEN_ELITE": {"eng": "Mirror Warden", "rus": "Зеркальный страж"},
    "STAR_EXAMINER_ELITE": {"eng": "Star Examiner", "rus": "Звёздный экзаменатор"},
    "BLACK_WANDERER_BOSS": {"eng": "Crimson Portal: The Black Wanderer", "rus": "Багровый портал: Чёрный Странник"},
    "BARROW_REAPER_BOSS": {"eng": "Crimson Portal: Reaper of the Barrows", "rus": "Багровый портал: Жнец Курганов"},
    "DEEP_MASTER_SMITH_BOSS": {"eng": "Crimson Portal: Master Smith", "rus": "Багровый портал: Мастер-кузнец"},
    "GATEBREAKER_ENVOY_BOSS": {"eng": "Crimson Portal: Envoy of the Gatebreaker", "rus": "Багровый портал: Посланник"},
    "BLOOD_COURT_LORD_BOSS": {"eng": "Crimson Portal: Lord of the Blood Court", "rus": "Багровый портал: Владыка"},
    "FIRST_SUMMONER_BOSS": {"eng": "Crimson Portal: First Summoner", "rus": "Багровый портал: Первый Призыватель"},
    "WORLD_EATER_BOSS": {"eng": "Crimson Portal: World-Eater", "rus": "Багровый портал: Пожиратель Миров"},
    "NAMELESS_SHADOW_BOSS": {"eng": "The Shadow Without a Name", "rus": "Тень Без Имени"},
    "ECHO_KNIGHT_ELITE": {"eng": "Echo Knight", "rus": "Рыцарь-эхо"},
}

# ───────────── акты (таблица acts) ─────────────
STAGE7_ACTS = {
    "eng": {
        "FROZEN_BARROWS.title": "The Frozen Barrows",
        "CLOCKWORK_DEPTHS.title": "The Clockwork Depths",
        "BLOOD_COURT.title": "The Blood Court",
        "STAR_TOWER.title": "The Star Tower",
        "NAMELESS_LIMIT.title": "The Nameless Limit",
    },
    "rus": {
        "FROZEN_BARROWS.title": "Ледяные Курганы",
        "CLOCKWORK_DEPTHS.title": "Заводные Глубины",
        "BLOOD_COURT.title": "Кровавый Двор",
        "STAR_TOWER.title": "Звёздная Башня",
        "NAMELESS_LIMIT.title": "Безымянный предел",
    },
}

# ───────────── силы: (класс, eng title, eng desc, rus title, rus desc) ─────────────
STAGE7_POWERS = [
    ("BannermanAffixPower", "Bannerman", "While it lives, other enemies' attacks deal 2 more damage.",
     "Знаменосец", "Пока он жив, атаки остальных врагов наносят на 2 урона больше."),
    ("AbsorberAffixPower", "Absorber", "Regains 1 [gold]Spell Absorption[/gold] at the start of each hero turn.",
     "Поглотитель", "В начале каждого хода героев восстанавливает 1 [gold]Поглощение заклинаний[/gold]."),
    ("FrenziedAffixPower", "Frenzied", "Below 50% HP: attacks one more time.",
     "Неистовый", "При ♥ ниже 50 % атакует на один раз больше."),
    ("GhostlyAffixPower", "Ghostly", "Physical damage from non-spells −50%.",
     "Призрачный", "Физический урон не от заклинаний −50 %."),
    ("AvengerAffixPower", "Avenger", "On death, deals 6 damage to ALL heroes.",
     "Мститель", "При смерти наносит 6 урона ВСЕМ героям."),
    ("GhostlyPower", "Ghostly", "Physical damage from non-spells −75%. Spells, elements and silver ignore it.",
     "Призрачность", "Физический урон не от заклинаний −75 %. Заклинания, стихии и серебро — полностью."),
    ("InvulnerablePower", "Invulnerable", "Takes no damage.",
     "Неуязвимость", "Не получает урона."),
    ("SpellMirrorPower", "Spell Mirror", "Spell damage against it is doubled and half returns to the caster. Physical damage against it ×1.5.",
     "Зеркало заклинаний", "Урон заклинаниями по нему удваивается, половина возвращается заклинателю. Физический урон ×1,5."),
    ("PetrifiedPower", "Petrified", "Stone by day: gains [gold]Plating[/gold] but does not attack. [gold]Light[/gold] removes it.",
     "Окаменение", "Днём — камень: копит [gold]Латы[/gold], но не атакует. [gold]Свет[/gold] снимает."),
    ("FlightPower", "Flight", "Physical attacks (not spells) against it −50%. {Amount} turns aloft.",
     "Полёт", "Физические атаки (не заклинания) по нему −50 %. Ходов в воздухе: {Amount}."),
    ("MistFormPower", "Mist Form", "Attacks miss it, except [gold]Light[/gold] and spells. {Amount} turns.",
     "Туман", "Атаки проходят мимо, кроме [gold]Света[/gold] и заклинаний. Ходов: {Amount}."),
    ("WordOfBindingPower", "Word of Binding", "Magic damage against you +15% per stack (max 3). Stacks: {Amount}.",
     "Слово подчинения", "Магический урон по вам +15 % за стак (максимум 3). Стаков: {Amount}."),
    ("CursedCryPower", "Cursed Cry", "[gold]Plating[/gold] −{Amount} for the rest of combat.",
     "Проклятый клич", "[gold]Латы[/gold] −{Amount} до конца боя."),
    ("SoulSaltsPower", "Soul Salts", "{Amount} charges to call wraith thralls. A [gold]Sneaky[/gold] hit steals one.",
     "Соли душ", "Зарядов призыва призраков: {Amount}. [gold]Подлый[/gold] удар крадёт заряд."),
    ("OverheatPower", "Overheat", "Steam vent in {Amount} turns: [gold]Burning[/gold] 3 to all heroes.",
     "Перегрев", "Паровой выброс через {Amount} хода: [gold]Горение[/gold] 3 всем героям."),
    ("FrostbiteAuraPower", "Frostbite Aura", "The Frost: [gold]Chill[/gold] 1 at the start of each combat. Warm up at a camp.",
     "Аура обморожения", "Стужа: [gold]Озноб[/gold] 1 в начале каждого боя. Отогреться можно в лагере."),
    ("LeyLinePower", "Ley Line", "This fight is on a ley line: +3 ✦ to everyone at the start.",
     "Лей-линия", "Бой на лей-линии: +3 ✦ всем в начале."),
    ("AbyssSpawnPower", "Abyss Spawn", "Spawned by an Abyss Gate. Extra reward for the fight.",
     "Порождение Бездны", "Порождён Вратами Бездны. Дополнительная награда за бой."),
]

# ───────────── реликвии: (класс, eng, eng desc, rus, rus desc, eng flavor, rus flavor) ─────────────
STAGE7_RELICS = [
    ("ActLedger", "Road Map",
     "Remembers the acts of this run. Superbosses defeated: {Superbosses}. Claw Keys: {Keys}. Warmth: {Warmth} fights. Valve relief: {Relief} fights.",
     "Дорожная карта",
     "Помнит акты забега. Побеждено супербоссов: {Superbosses}. Когтистых ключей: {Keys}. Тепло: {Warmth} боёв. Вентили закрыты: {Relief} боёв.",
     "Marked nodes show Gates, vaults, valves and ley lines.", "Отмеченные узлы — Врата, склепы, вентили и лей-линии."),
]

STAGE7_RELIC_EXTRA = {
    "eng": {
        "ACT_LEDGER.portalHeader": "Crimson Portal",
        "ACT_LEDGER.portalBody": "A crimson rift opens where the boss fell. Step through to face the act's superboss (artifact, black soul stone, a Candle once per run)?",
        "ACT_LEDGER.portalYes": "Enter",
        "ACT_LEDGER.portalNo": "Move on",
    },
    "rus": {
        "ACT_LEDGER.portalHeader": "Багровый портал",
        "ACT_LEDGER.portalBody": "Там, где пал босс, раскрылся багровый разлом. Войти к супербоссу акта (артефакт, чёрный Камень душ, Свеча — раз за забег)?",
        "ACT_LEDGER.portalYes": "Войти",
        "ACT_LEDGER.portalNo": "Идти дальше",
    },
}

STAGE7_REST_SITE = {
    "eng": {
        "OPTION_SLAYTHEREQUIEM_WARM.name": "Warm Up",
        "OPTION_SLAYTHEREQUIEM_WARM.description": "No [gold]Chill[/gold] from the Frost for the next {Fights} fights. Free.",
        "OPTION_SLAYTHEREQUIEM_WARM.descriptionDisabled": "You are already warm.",
    },
    "rus": {
        "OPTION_SLAYTHEREQUIEM_WARM.name": "Отогреться",
        "OPTION_SLAYTHEREQUIEM_WARM.description": "Следующие {Fights} боя Стужа не накладывает [gold]Озноб[/gold]. Бесплатно.",
        "OPTION_SLAYTHEREQUIEM_WARM.descriptionDisabled": "Вы уже согрелись.",
    },
}

_LEVELS = {
    "eng": ["Wanderer", "Mercenary", "Veteran of the Road", "Hardened", "Seasoned", "Veteran", "Champion", "Legend",
            "Doomed", "Forsaken", "Damned"],
    "rus": ["Странник", "Наёмник", "Бывалый", "Закалённый", "Опытный", "Ветеран", "Чемпион", "Легенда",
            "Обречённый", "Отверженный", "Проклятый"],
}

STAGE7_SETTINGS = {
    "eng": {
        "FIVE_ACTS.title": "Five acts", "FIVE_ACTS.hover.title": "Five acts",
        "FIVE_ACTS.hover.desc": "Foothills → Frozen Barrows or Clockwork Depths → Hive → Blood Court or Star Tower → Glory. Off — the vanilla three acts.",
        "SHORT_RUN.hover.desc": "Acts 1 → 3 → 5 only (Foothills, Hive, Glory) with the mod's overlays. Overrides Five acts.",
        "REQUIEM_LEVEL_SETTING.title": "Requiem level", "REQUIEM_LEVEL_SETTING.hover.title": "Requiem level",
        "REQUIEM_LEVEL_SETTING.hover.desc": "−1 — by Ascension (Ascension N → level N+1). 0 Wanderer: −25% enemy HP and damage taken. "
                                            "5: travel fatigue. 7: elites get 2–3 affixes, +25% damage taken. 8: all Omens. "
                                            "10: −20% damage dealt, the portal is mandatory, Tainted start, no Candles.",
        "SUPERBOSSES.title": "Crimson Portal", "SUPERBOSSES.hover.title": "Crimson Portal",
        "SUPERBOSSES.hover.desc": "Superbosses after each act boss. Optional — you are asked (in co-op the portal is always entered).",
        "SUPERBOSSES.Off": "Off", "SUPERBOSSES.Optional": "Optional", "SUPERBOSSES.Always": "Always",
        "WIDE_MAPS.title": "Wide maps", "WIDE_MAPS.hover.title": "Wide maps",
        "WIDE_MAPS.hover.desc": "Maps of the new acts are 9 columns wide (more paths and ? rooms). Off — 7 columns.",
        **{f"REQUIEM_LEVEL_{i}": name for i, name in enumerate(_LEVELS["eng"])},
    },
    "rus": {
        "FIVE_ACTS.title": "Пять актов", "FIVE_ACTS.hover.title": "Пять актов",
        "FIVE_ACTS.hover.desc": "Подножие → Ледяные Курганы или Заводные Глубины → Улей → Кровавый Двор или Звёздная Башня → Слава. Выкл — ванильные три акта.",
        "SHORT_RUN.hover.desc": "Только акты 1 → 3 → 5 (Подножие, Улей, Слава) с оверлеями мода. Важнее, чем «Пять актов».",
        "REQUIEM_LEVEL_SETTING.title": "Уровень «Реквием»", "REQUIEM_LEVEL_SETTING.hover.title": "Уровень «Реквием»",
        "REQUIEM_LEVEL_SETTING.hover.desc": "−1 — по Возвышению (Возвышение N → уровень N+1). 0 «Странник»: −25 % ♥ врагов и получаемого урона. "
                                            "5: Усталость похода. 7: элиты с 2–3 аффиксами, +25 % получаемого урона. 8: все Знамения. "
                                            "10: −20 % наносимого урона, портал обязателен, старт «Запятнанным», Свечей нет.",
        "SUPERBOSSES.title": "Багровый портал", "SUPERBOSSES.hover.title": "Багровый портал",
        "SUPERBOSSES.hover.desc": "Супербоссы после босса каждого акта. «На выбор» — спрашивается (в коопе портал обязателен).",
        "SUPERBOSSES.Off": "Выкл", "SUPERBOSSES.Optional": "На выбор", "SUPERBOSSES.Always": "Всегда",
        "WIDE_MAPS.title": "Широкие карты", "WIDE_MAPS.hover.title": "Широкие карты",
        "WIDE_MAPS.hover.desc": "Карты новых актов шириной 9 колонок (больше путей и «?»). Выкл — 7 колонок.",
        **{f"REQUIEM_LEVEL_{i}": name for i, name in enumerate(_LEVELS["rus"])},
    },
}
