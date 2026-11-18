"""Б2.4 — two targeted hunter cards and deterministic optional access to forms."""
BUILD_CARDS = [
    ("HuntingShot", "Hunting Shot",
     "[gold]Weapon[/gold]. Deal {Damage:diff()} damage.\nWith a bow or crossbow, apply {SunderPower:diff()} [gold]Sunder[/gold].",
     "Охотничий выстрел", "[gold]Оружейная[/gold]. Нанести {Damage:diff()} урона.\nС луком или арбалетом — наложить {SunderPower:diff()} [gold]Раскола[/gold]."),
    ("CoveringShot", "Covering Shot",
     "[gold]Weapon[/gold]. Deal {Damage:diff()} damage. Gain {Block:diff()} [gold]Block[/gold].\nWith a bow or crossbow, draw 1 card.",
     "Прикрывающий выстрел", "[gold]Оружейная[/gold]. Нанести {Damage:diff()} урона. Получить {Block:diff()} [gold]Блока[/gold].\nС луком или арбалетом — взять 1 карту."),
]
_TIPS = {
    'formRites.title': ('Form rites', 'Обряды формы'),
    'formRites.description': (
        'Optional, 1 camp point only on success. Beast/Pact from act 1; Righteous/Clockwork/Vampire from act 2. '
        'A worldview is exclusive. Herald: Tomb Guardian OR 2 elite wins with the Pact. Lich: late act and 10 Herald wins (6 in short 1→3→5). '
        'Events remain an earlier alternative, not a required lucky drop. Read all costs before accepting.',
        'Необязательно; 1 очко лагеря только за успех. Зверь/Договор — с акта 1; Праведник/Механизм/Вампир — с акта 2. '
        'Мировоззрения взаимоисключающие. Вестник: Хранитель Гробницы ИЛИ 2 победы над элитами с Договором. '
        'Лич: поздний акт и 10 побед Вестником (6 в коротком 1→3→5). События остаются ранней альтернативой, а не обязательной удачей. Прочитайте цену формы.'),
    'formHeraldLocked.title': ('Requires the Dark Pact and {Count} elite victories after accepting it (or the Tomb Guardian).',
                               'Нужны Тёмный договор и {Count} победы над элитами после принятия (либо Хранитель Гробницы).'),
    'formLichLocked.title': ('Requires {Count} victories as a Herald; the Phylactery rite opens in a late act.',
                             'Нужны {Count} побед Вестником; обряд Филактерии доступен в позднем акте.'),
    'formLater.title': ('This rite opens in a later act; it does not expire if you postpone it.',
                        'Этот обряд откроется в следующем подходящем акте и не исчезнет от отсрочки.'),
    'formPietyLocked.title': ('Requires Righteous piety. Restore piety before this rite.',
                              'Нужна праведность «Праведник». Сначала восстановите праведность.'),
    'formIncompatible.title': ('Incompatible with your present worldview, Clockwork Heart or Ordinator oath. An existing worldview cannot be replaced here.',
                               'Несовместимо с нынешним мировоззрением, Заводным сердцем или клятвой Ординатора. Сменить выбранное мировоззрение здесь нельзя.'),
}
BUILD_TIPS = {lang: {'UI.' + key: pair[i] for key, pair in _TIPS.items()} for i, lang in enumerate(('eng', 'rus'))}
BUILD_REST_SITE = {
    'eng': {
        'OPTION_SLAYTHEREQUIEM_FORM_RITES.title': 'Form rites',
        'OPTION_SLAYTHEREQUIEM_FORM_RITES.description': 'Choose a compatible form or earned chain step. {Cost} camp point on success (left: {Points}). No random event required. Cancellation is free.',
        'OPTION_SLAYTHEREQUIEM_FORM_RITES.descriptionDisabled': 'No compatible or earned form is available in this act. {Cost} camp point on success (left: {Points}).',
    },
    'rus': {
        'OPTION_SLAYTHEREQUIEM_FORM_RITES.title': 'Обряды формы',
        'OPTION_SLAYTHEREQUIEM_FORM_RITES.description': 'Выбрать совместимую форму или заслуженную ступень. {Cost} очко лагеря за успех (осталось: {Points}). Не требуется случайное событие. Отмена бесплатна.',
        'OPTION_SLAYTHEREQUIEM_FORM_RITES.descriptionDisabled': 'В этом акте нет доступной совместимой или заслуженной формы. {Cost} очко лагеря за успех (осталось: {Points}).',
    },
}
