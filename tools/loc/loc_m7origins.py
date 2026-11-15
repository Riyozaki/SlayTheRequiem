"""Заголовки экрана выбора происхождения и предыстории (M7 п. 3, Relics/OriginLooks.cs, Core/OriginLookRules.HeadingKey).

Ключи без префикса — build_loc.py добавляет «SLAYTHEREQUIEM-» и кладёт их в static_hover_tips.json (RequiemUi.TipText).
"""

ORIGIN_TIPS = {
    "eng": {
        "ORIGIN_PICK.clan": "Choose your clan",
        "ORIGIN_PICK.tribe": "Choose your tribe",
        "ORIGIN_PICK.dynasty": "Choose your dynasty",
        "ORIGIN_PICK.background": "Choose your background",
    },
    "rus": {
        "ORIGIN_PICK.clan": "Выберите клан",
        "ORIGIN_PICK.tribe": "Выберите племя",
        "ORIGIN_PICK.dynasty": "Выберите династию",
        "ORIGIN_PICK.background": "Выберите предысторию",
    },
}
