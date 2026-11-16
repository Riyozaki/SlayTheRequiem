"""Тексты своей карты (M6 п. 1, docs/design/13 §4): подтипы узлов, группы легенды, особые рёбра; Факел и зелье Ночного зрения.

Ключи без префикса — build_loc.py добавляет «SLAYTHEREQUIEM-». Подсказки — static_hover_tips.json (UI/MapVisuals,
UI/NRequiemMapLegend → RequiemUi.TipText). Имя значка — NodeRules.IconName в верхнем регистре.
"""

# значок → (en название, en описание, ru название, ru описание)
NODES = {
    "SHRINE": ("Shrine", "A Celestial's blessing, Redemption, the Temple shop. In Glory a Celestial Banner also grants the Heavenly Shield.",
               "Святилище", "Благословение Небесного, Искупление, Храмовая лавка. В Славе Знамя Небесных ещё и даёт «Небесный щит»."),
    "DARK_ALTAR": ("Dark Altar", "A gift of an Abyss Prince — at a price.", "Тёмный алтарь", "Дар Князя Бездны — за свою цену."),
    "ARMORER": ("Armorer", "Weapons and armour, sharpening.", "Оружейник", "Оружие и броня, заточка."),
    "ENCHANTER": ("Arcanist", "Spells, scrolls, staves, enchanting.", "Чародейская лавка", "Заклинания, свитки, посохи, зачарование."),
    "HERBALIST": ("Herbalist", "Potions, poisons, reagents.", "Травница", "Зелья, яды, реагенты."),
    "JEWELER": ("Jeweler", "Rings, amulets, soul gems.", "Ювелир", "Кольца, амулеты, камни душ."),
    "CARAVAN": ("Caravan", "A little of everything, exotic goods, barter.", "Караван", "Понемногу всего, экзотика, обмен."),
    "FENCE": ("Fence", "Abyssal goods, buys stolen wares.", "Скупщик краденого", "Бездонные вещи, скупка краденого."),
    "TAVERN": ("Tavern", "Food, rumours, the Mentor, a short rest.", "Таверна", "Еда, слухи, Наставник, короткий отдых."),
    "LIBRARY": ("Library", "Learn a spell, copy a scroll, buy a treatise.", "Библиотека", "Изучить заклинание, переписать свиток, купить трактат."),
    "LAIR": ("Lair", "A lighter beast elite: reagents and hide armour.", "Логово", "Облегчённая элита-зверь: реагенты и броня из шкуры."),
    "TRIAL": ("Trial", "An empowered elite; an extra relic for the win.", "Испытание", "Усиленная элита; за победу — ещё реликвия."),
    "CACHE": ("Cache", "A chest with a lock (Claw Key) or a trap.", "Тайник", "Сундук с замком (Когтистый ключ) или с ловушкой."),
    "LOCKED_GATE": ("Locked Gate", "A sealed vault: the path opens only with a Claw Key.", "Запертые врата",
                    "Запечатанный склеп: путь открыт только с Когтистым ключом."),
    "VALVE": ("Valve", "Turning it switches the steam pipes and stops the bursts for a while.", "Вентиль",
              "Поворот переключает паровые трубы и на время снимает выбросы."),
    "ABYSS_GATE": ("Abyss Gate", "A fight with Abyss spawn: a relic or a soul gem.", "Врата Бездны", "Бой с Порождениями Бездны: реликвия или Камень душ."),
    "CRIMSON_PORTAL": ("Crimson Portal", "The path to the superboss (optional).", "Багровый портал", "Путь к супербоссу (по желанию)."),
    "MEMORY_STONE": ("Memory Stone", "Rollback point: the Imprint of the act.", "Камень Памяти", "Точка отката: Отпечаток акта."),
    "EBB_STONES": ("Ebb Stones", "Six stones by the water: Ebb Shards.", "Камни отлива", "Шесть камней у воды: Осколки отлива."),
    "AMBUSH": ("Ambush", "Enemies strike first and harder; gold after the fight. Seen only with night sight.", "Засада",
               "Враги бьют первыми и сильнее; после боя — золото. Видна только с Ночным зрением."),
}

GROUPS = {
    "TRADE": ("Trade", "Торговля"),
    "FAITH": ("Faith", "Вера"),
    "DANGER": ("Danger", "Опасность"),
    "NATURE": ("Nature and craft", "Природа и ремесло"),
    "MAGIC": ("Magic", "Магия"),
}

# ребро → (en, en описание, ru, ru описание)
EDGES = {
    "PORTAL": ("Portal", "Leaps over a row; a fight always waits at the far end.", "Портал", "Перескакивает через ряд; на том конце всегда бой."),
    "PIPE": ("Steam pipe", "Open: a valve can close it.", "Паровая труба", "Открыта: вентиль может её закрыть."),
    "PIPE_CLOSED": ("Closed pipe", "Impassable until a valve opens it.", "Закрытая труба", "Непроходима, пока вентиль её не откроет."),
    "SECRET": ("Secret trail", "Seen and walked only with night sight, the Barrow Map or the Omen of the Trickster.", "Тайная тропа",
               "Видна и проходима только с Ночным зрением, «Картой Курганов» или Знамением Ловкача."),
    "DANGER": ("Dangerous trail", "A shortcut to an elite or an ambush — a cache waits after the fight.", "Опасная тропа",
               "Короткий путь к элите или засаде — после боя ждёт Тайник."),
    "POCKET": ("Side pocket", "A dead-end treasure off the branch; returns to it one row later.", "Боковой карман",
               "Тупиковое сокровище в стороне от ветки; возвращает на неё через ряд."),
    "LOCKED": ("Locked gate", "The way into a vault: needs a Claw Key.", "Запертые врата", "Путь в склеп: нужен Когтистый ключ."),
    "LAST_REST": ("Last rest", "A camp and a soul gem, but the boss gets +10% HP.", "Последний привал", "Лагерь и Камень души, но босс получает +10 % ♥."),
}


def _tips(lang: str) -> dict:
    eng = lang == "eng"
    out = {
        "MAP_LEGEND.heading": "Map marks" if eng else "Значки карты",
        "MAP_DARK.text": "Hidden by the barrow darkness" if eng else "Скрыто Тьмой курганов",
        "MAP_POCKET.text": "Side pocket: a treasure off the branch" if eng else "Боковой карман: сокровище в стороне от ветки",
        "MAP_LAST_REST.name": "Last rest" if eng else "Последний привал",
        "MAP_LAST_REST.text": ("A camp and a small soul gem, but the act boss gets +10% HP." if eng
                               else "Лагерь и малый Камень души, но босс акта получает +10 % ♥."),
    }
    for key, (et, ed, rt, rd) in NODES.items():
        out[f"MAP_NODE.{key}.name"] = et if eng else rt
        out[f"MAP_NODE.{key}.text"] = ed if eng else rd
    for key, (et, rt) in GROUPS.items():
        out[f"MAP_GROUP.{key}.name"] = et if eng else rt
    for key, (et, ed, rt, rd) in EDGES.items():
        out[f"MAP_EDGE.{key}.name"] = et if eng else rt
        out[f"MAP_EDGE.{key}.text"] = ed if eng else rd
    return out


MAP_TIPS = {"eng": _tips("eng"), "rus": _tips("rus")}

# (класс, en, en описание, ru, ru описание, en цитата, ru цитата) — реликвии рюкзака.
M6_RELICS = [
    ("Torch", "Torch", "[gold]Night sight[/gold] for the whole party: the barrow darkness recedes to 4 rows, Ambushes and secret trails show.",
     "Факел", "[gold]Ночное зрение[/gold] всему отряду: Тьма курганов отступает до 4 рядов, видны Засады и Тайные тропы.",
     "Pitch and rags. The dead hate it.", "Смола и тряпьё. Мертвецы его ненавидят."),
]

# (класс, en, en описание, ru, ru описание) — зелья.
M6_POTIONS = [
    ("NightSightPotion", "Night Sight Potion",
     "Until the end of the act: [gold]night sight[/gold] for the party — the barrow darkness recedes to 4 rows, Ambushes and secret trails show.",
     "Зелье Ночного зрения",
     "До конца акта: [gold]Ночное зрение[/gold] отряду — Тьма курганов отступает до 4 рядов, видны Засады и Тайные тропы."),
]
