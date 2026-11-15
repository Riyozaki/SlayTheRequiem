"""Тексты экрана «две двери» (M5 п. 2, docs/design/13 §2, ревью R-12) и настройки «Вариант актов 2 и 4».

Ключи без префикса — build_loc.py добавляет «SLAYTHEREQUIEM-». Экран: static_hover_tips.json (UI/NActChoiceScreen →
RequiemUi.TipText). Числа в правилах — переменные SmartFormat из Core/ActRules.cs, не литералы. Обещаем только то, что
уже работает (Тьма курганов, переключение рёбер, порталы Башни и прочее отложенное на этап 8 — см. ActRuntime.cs — нет).
Правила — обычный текст без [gold]: экран рисует их простым Label.
"""

DOORS_TIPS = {
    "eng": {
        "DOORS.heading": "Two Doors",
        "DOORS.subtitle": "Act {Act}: where does the road lead?",
        "DOORS.fate": "Trust fate",
        "DOORS.fateHint": "Do not vote: your companions decide, or the variant rolled when the run began stays.",
        "DOORS.rolledHint": "Fate already leans this way: if nobody chooses, this door opens.",
        "DOORS.waiting": "Waiting for the others: {Voted} of {Total}",
        "DOORS.decided": "The road is chosen",
        "DOORS.you": "your vote",
        "DOORS.FROZEN_BARROWS.foes": "Barrow dead",
        "DOORS.FROZEN_BARROWS.rules": "Bitter cold: every fight starts with Chill {Chill}. Warm up at a campfire to skip it for "
                                      "{Warmth} fights; frost resistance or freeze immunity protects you. Elites drop Claw Keys "
                                      "(up to {Keys} per act); a key opens a locked vault for {Gold} gold.",
        "DOORS.CLOCKWORK_DEPTHS.foes": "Clockwork guardians and coast raiders",
        "DOORS.CLOCKWORK_DEPTHS.rules": "Steam: every {Period}th round a vent burst inflicts Burning {Burning} on the heroes. "
                                        "Turn a valve on the map to stop the bursts for the next {Relief} fights.",
        "DOORS.BLOOD_COURT.foes": "The Blood Court",
        "DOORS.BLOOD_COURT.rules": "Day and night: map rows alternate in blocks of {Block}. At night enemies have +{Hp}% HP, "
                                   "and every fight pays +{Gold} gold.",
        "DOORS.STAR_TOWER.foes": "Renegades of the Star Tower",
        "DOORS.STAR_TOWER.rules": "Ley lines: some fights on the map lie on a ley line — there every hero starts the fight "
                                  "with +{Mana} ✦.",
    },
    "rus": {
        "DOORS.heading": "Две двери",
        "DOORS.subtitle": "Акт {Act}: куда ведёт путь?",
        "DOORS.fate": "Довериться судьбе",
        "DOORS.fateHint": "Не голосовать: решат спутники или останется вариант, выпавший при создании забега.",
        "DOORS.rolledHint": "Судьба уже склоняется сюда: если никто не выберет, откроется эта дверь.",
        "DOORS.waiting": "Ждём остальных: {Voted} из {Total}",
        "DOORS.decided": "Путь выбран",
        "DOORS.you": "ваш голос",
        "DOORS.FROZEN_BARROWS.foes": "Курганные мертвецы",
        "DOORS.FROZEN_BARROWS.rules": "Стужа: каждый бой начинается с Озноба {Chill}. «Отогреться» у костра — {Warmth} боя без "
                                      "Стужи; сопротивление холоду или иммунитет к заморозке защищают. Элиты роняют Когтистые "
                                      "ключи (до {Keys} за акт); ключ открывает запертый склеп: +{Gold} золота.",
        "DOORS.CLOCKWORK_DEPTHS.foes": "Заводные стражи и береговые грабители",
        "DOORS.CLOCKWORK_DEPTHS.rules": "Пар: каждый {Period}-й раунд выброс накладывает на героев Горение {Burning}. "
                                        "Перекрытый на карте вентиль снимает выбросы на {Relief} следующих боя.",
        "DOORS.BLOOD_COURT.foes": "Кровавый двор",
        "DOORS.BLOOD_COURT.rules": "День и ночь: ряды карты чередуются блоками по {Block}. Ночью у врагов +{Hp} % ♥, "
                                   "а каждый бой приносит +{Gold} золота.",
        "DOORS.STAR_TOWER.foes": "Отступники Звёздной Башни",
        "DOORS.STAR_TOWER.rules": "Лей-линии: часть боёв на карте лежит на лей-линии — там каждый герой начинает бой "
                                  "с +{Mana} ✦.",
    },
}

DOORS_SETTINGS = {
    "eng": {
        "ACT_VARIANT.title": "Acts 2 and 4", "ACT_VARIANT.hover.title": "Two Doors",
        "ACT_VARIANT.hover.desc": "Two doors — after the boss of acts 1 and 3 you choose the next act (in co-op — by vote: "
                                  "majority wins, a tie is settled by the run seed). Random — the variant is rolled when the "
                                  "run begins, no screen (in co-op such a player abstains). Five acts only.",
        "ACT_VARIANT.Doors": "Two doors", "ACT_VARIANT.Random": "Random",
    },
    "rus": {
        "ACT_VARIANT.title": "Акты 2 и 4", "ACT_VARIANT.hover.title": "Две двери",
        "ACT_VARIANT.hover.desc": "«Две двери» — после босса актов 1 и 3 вы выбираете следующий акт (в коопе — голосованием: "
                                  "решает большинство, ничью — сид забега). «Случайно» — вариант выпадает при создании забега, "
                                  "экрана нет (в коопе такой игрок воздерживается). Только в режиме «Пять актов».",
        "ACT_VARIANT.Doors": "Две двери", "ACT_VARIANT.Random": "Случайно",
    },
}
