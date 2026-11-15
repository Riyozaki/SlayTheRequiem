"""Тексты M5 п. 1: боссы актов 2 и 4, их свита и силы (docs/design/11 §6.1)."""

from loc_stage7 import _m

# ───────────── враги (ключ — класс в UPPER_SNAKE) ─────────────
M5_MONSTERS = {
    # 2а — Ледяные Курганы
    "KING_UNDER_BARROW": _m("King Under the Barrow", "Король-под-Курганом", {
        "WINTER_BLADE": ("Winter Blade", "Зимний клинок"), "CALL_THE_OATHSWORN": ("Call the Oathsworn", "Созвать дружину"),
        "CROWN_OF_RIME": ("Crown of Rime", "Корона инея"), "KINGS_JUDGEMENT": ("King's Judgement", "Королевский суд"),
        "GRAVE_COLD": ("Grave Cold", "Могильный холод"), "RAISE_OATHSWORN": ("Raise the Oathsworn", "Поднять дружинника")}),
    "BARROW_HUSKARL": _m("Barrow Huskarl", "Хускарл кургана", {
        "HUSKARL_AXE": ("Bearded Axe", "Бородовидная секира"), "HUSKARL_SHIELD": ("Shield Wall", "Стена щитов")}),
    "BONE_FROST_WYRM": _m("Bone Frost Wyrm", "Костяной змей стужи", {
        "WING_GUST": ("Wing Gust", "Порыв крыльев"), "FROST_BREATH": ("Frost Breath", "Ледяное дыхание"),
        "DIVE": ("Dive", "Пикирование"), "GROUND_BITE": ("Grounded Bite", "Укус на земле"),
        "BONE_STORM": ("Bone Storm", "Костяная буря"), "GLACIAL_ROAR": ("Glacial Roar", "Ледниковый рёв"),
        "TAIL_SWEEP": ("Tail Sweep", "Удар хвостом")}),
    "BARROW_WAILER": _m("Barrow Wailer", "Плакальщица курганов", {
        "GRAVE_TOUCH": ("Grave Touch", "Могильное касание"), "MOURNING_WAIL": ("Mourning Wail", "Погребальный плач"),
        "FUNERAL_DIRGE": ("Funeral Dirge", "Похоронная песнь"), "DEATH_SHRIEK": ("Death Shriek", "Предсмертный крик"),
        "LAST_LAMENT": ("Last Lament", "Последний плач")}),
    # 2б — Заводные Глубины
    "CLOCKWORK_COLOSSUS": _m("Clockwork Colossus", "Заводной колосс", {
        "PISTON_SLAM": ("Piston Slam", "Поршневой удар"), "BUILD_PRESSURE": ("Build Pressure", "Набор давления"),
        "STEAM_VOLLEY": ("Steam Volley", "Паровой залп"), "PISTON_FLURRY": ("Piston Flurry", "Поршневой шквал"),
        "BOILER_BURST": ("Boiler Burst", "Разрыв котла"), "OVERDRIVE": ("Overdrive", "Разгон")}),
    "AUTOMATON_ARCHITECT": _m("Automaton Architect", "Архитектор автоматонов", {
        "DEPLOY_SENTRY": ("Deploy Sentries", "Поставить часовых"), "CALIPER_STRIKE": ("Caliper Strike", "Удар штангенциркулем"),
        "REINFORCE": ("Reinforce", "Укрепить"), "EXO_HAMMER": ("Exo-Hammer", "Экзомолот"),
        "RIVET_BURST": ("Rivet Burst", "Заклёпочный залп"), "EMERGENCY_REPAIR": ("Emergency Repair", "Аварийный ремонт")}),
    "BRASS_SENTRY": _m("Brass Sentry", "Латунный часовой", {"SENTRY_BOLT": ("Arc Bolt", "Дуговой болт")}),
    "BOILER_WYRM": _m("Boiler Wyrm", "Котловой змей", {
        "SCALDING_BITE": ("Scalding Bite", "Ошпаривающий укус"), "IRON_COIL": ("Iron Coil", "Железные кольца"),
        "PRESSURE_RELEASE": ("Pressure Release", "Сброс давления")}),
    # 4а — Кровавый Двор
    "CRIMSON_COUNTESS": _m("Crimson Countess", "Алая Графиня", {
        "BLOOD_ROSE": ("Blood Rose", "Кровавая роза"), "MASKED_BALL": ("Masked Ball", "Бал масок"),
        "CRIMSON_KISS": ("Crimson Kiss", "Алый поцелуй"), "BAT_SWARM": ("Bat Swarm", "Рой летучих мышей"),
        "BLOOD_RAIN": ("Blood Rain", "Кровавый дождь"), "ETERNAL_KISS": ("Eternal Kiss", "Вечный поцелуй")}),
    "ECLIPSE_WARDEN": _m("Eclipse Warden", "Страж Затмения", {
        "SOLAR_PLATE": ("Solar Plate", "Солнечная плита"), "DAWN_RAY": ("Dawn Ray", "Луч рассвета"),
        "MOON_TALONS": ("Moon Talons", "Лунные когти"), "NIGHTFALL": ("Nightfall", "Сумерки")}),
    "CHALICE_HIEROPHANT": _m("Hierophant of the Crimson Chalice", "Иерофант Багряной Чаши", {
        "BLOOD_BEAM": ("Blood Beam", "Кровавый луч"), "CALL_THE_FAITHFUL": ("Call the Faithful", "Созыв паствы"),
        "BLOOD_SACRAMENT": ("Blood Sacrament", "Кровавое причастие"), "SANGUINE_JUDGEMENT": ("Sanguine Judgement", "Багряный суд"),
        "CHALICE_FLOOD": ("Chalice Flood", "Разлив чаши"), "LAST_RITES": ("Last Rites", "Последние обряды")}),
    "CHALICE_ACOLYTE": _m("Chalice Acolyte", "Послушник Чаши", {
        "OFFERING": ("Offering", "Подношение"), "PENITENT_STRIKE": ("Penitent Strike", "Покаянный удар")}),
    # 4б — Звёздная Башня
    "ASTROLABE_KEEPER": _m("Keeper of the Astrolabe", "Хранитель Астролябии", {
        "STARFALL": ("Starfall", "Звездопад"), "ALIGN_SPHERES": ("Align the Spheres", "Выравнивание сфер"),
        "MIND_ECLIPSE": ("Mind Eclipse", "Затмение разума"), "CONSTELLATION_RAY": ("Constellation Ray", "Луч созвездия"),
        "METEOR_SHOWER": ("Meteor Shower", "Метеоритный дождь"), "ORRERY_WARD": ("Orrery Ward", "Оберег орбит")}),
    "LEY_WYRM": _m("Ley Wyrm", "Змей лей-линий", {
        "ARCANE_COIL": ("Arcane Coil", "Кольцо чар"), "LEY_SURGE": ("Ley Surge", "Всплеск линии"),
        "DEVOUR_MANA": ("Devour Mana", "Пожирание маны"), "LEY_RUPTURE": ("Ley Rupture", "Разрыв линии"),
        "SERPENT_STRIKE": ("Serpent Strike", "Удар змея"), "ARCANE_FEAST": ("Arcane Feast", "Пир чар")}),
    "ARCHMAGE_SIMULACRUM": _m("Archmage Simulacrum", "Симулякр Архимага", {
        "ARCANE_BARRAGE": ("Arcane Barrage", "Чародейский шквал"), "LAYERED_WARD": ("Layered Ward", "Многослойный оберег"),
        "FROST_LANCE": ("Frost Lance", "Ледяное копьё"), "PRISM_RAY": ("Prism Ray", "Призменный луч"),
        "REASSEMBLE": ("Reassemble", "Сборка"), "GLASS_STORM": ("Glass Storm", "Стеклянная буря")}),
    "MIRROR_SHARD": _m("Mirror Shard", "Зеркальный осколок", {"SHARD_BOLT": ("Shard Bolt", "Осколочный разряд")}),
}

M5_ENCOUNTERS = {
    "KING_UNDER_BARROW_BOSS": {"eng": "King Under the Barrow", "rus": "Король-под-Курганом"},
    "BONE_FROST_WYRM_BOSS": {"eng": "Bone Frost Wyrm", "rus": "Костяной змей стужи"},
    "BARROW_WAILER_BOSS": {"eng": "Barrow Wailer", "rus": "Плакальщица курганов"},
    "CLOCKWORK_COLOSSUS_BOSS": {"eng": "Clockwork Colossus", "rus": "Заводной колосс"},
    "AUTOMATON_ARCHITECT_BOSS": {"eng": "Automaton Architect", "rus": "Архитектор автоматонов"},
    "BOILER_WYRM_BOSS": {"eng": "Boiler Wyrm", "rus": "Котловой змей"},
    "CRIMSON_COUNTESS_BOSS": {"eng": "Crimson Countess", "rus": "Алая Графиня"},
    "ECLIPSE_WARDEN_BOSS": {"eng": "Eclipse Warden", "rus": "Страж Затмения"},
    "CHALICE_HIEROPHANT_BOSS": {"eng": "Hierophant of the Crimson Chalice", "rus": "Иерофант Багряной Чаши"},
    "ASTROLABE_KEEPER_BOSS": {"eng": "Keeper of the Astrolabe", "rus": "Хранитель Астролябии"},
    "LEY_WYRM_BOSS": {"eng": "Ley Wyrm", "rus": "Змей лей-линий"},
    "ARCHMAGE_SIMULACRUM_BOSS": {"eng": "Archmage Simulacrum", "rus": "Симулякр Архимага"},
}

# (класс, eng, eng desc, rus, rus desc)
M5_POWERS = [
    ("OathGuardPower", "Oath of the Guard", "While another enemy stands beside it, takes {Amount}% less damage.",
     "Клятва стражи", "Пока рядом стоит другой враг, получает на {Amount} % меньше урона."),
    ("PressurePower", "Pressure", "Builds by 1 each turn; [gold]Frost[/gold] damage vents 1. At the threshold: Pressure Release.",
     "Давление", "Растёт на 1 каждый ход; урон [gold]холодом[/gold] сбивает 1. На пороге — «Сброс давления»."),
    ("SunStancePower", "Sun Stance", "Day: takes 50% less damage, except [gold]Light[/gold].",
     "Солнечная стойка", "День: получает на 50 % меньше урона, кроме [gold]Света[/gold]."),
    ("MoonStancePower", "Moon Stance", "Night: [gold]Light[/gold] damage against it ×1.5.",
     "Лунная стойка", "Ночь: урон [gold]Светом[/gold] по нему ×1,5."),
]
