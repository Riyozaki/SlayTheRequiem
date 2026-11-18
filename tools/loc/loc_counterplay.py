"""Б2: encounter-only combat answers; no permanent cards, rare-drop gate or mastery requirement."""
COUNTER_CARDS = [
    ("EnvoySidestep", "Sidestep", "Requires 1 ◆, without HP overstrain or Attack discounts. Spend once per play series. Avoid the Envoy's fire circle this turn. Retain. Exhaust. A missing copy is supplied each turn.",
     "Отступ", "Нужно 1 ◆, без надрыва и скидок на Атаки. Оплата один раз за серию повторов. Избежать огненного круга Посланника в этом ходу. Удержание. Изгнание. Недостающая карта выдаётся каждый ход."),
    ("CarrySolarStandard", "Carry the Standard", "Requires the Solar Standard on the field. Close the Abyss Gates. The Envoy's move damage to you is halved for the next enemy turn. Retain. Exhaust.",
     "Нести штандарт", "Нужен Солнечный штандарт на поле. Закрыть Врата Бездны. Урон ходов Посланника по вам в следующем ходу врагов вдвое меньше. Удержание. Изгнание."),
    ("CocoonSunbeam", "Sunbeam — Cocoon", "Break the Blood Lord's current cocoon immediately. No damage, mana or school requirement. Retain. Exhaust. Supplied each cocoon phase.",
     "Солнечный луч — кокон", "Немедленно раскрыть текущий кокон Кровавого Владыки. Без урона, маны и требования школы. Удержание. Изгнание. Выдаётся на каждой фазе кокона."),
    ("TearTheWing", "Tear the Wing", "Ground the World-Eater for 2 full enemy turns. Retain. Shout: normal shelf, 3-turn cooldown (reduced by bonuses). Full shelf: Discard; reissue never skips the cooldown. No ◆ or ✦.",
     "Сорвать крыло", "Приземлить Пожирателя Миров на 2 полных хода врагов. Удержание. Клич: обычная полка, перезарядка 3 (меньше с бонусами). Полка занята — в сброс; повторная выдача не обходит перезарядку. Без ◆ и ✦."),
    ("ClearTheSkies", "Clear Skies", "Cancel the next warned Meteor Rain, for the whole party. Retain. Shout: normal shelf, 3-turn cooldown (reduced by bonuses). Full shelf: Discard; reissue never skips the cooldown. No ◆ or ✦.",
     "Ясное небо", "Отменить ближайший предупреждённый Метеоритный дождь для всего отряда. Удержание. Клич: обычная полка, перезарядка 3 (меньше с бонусами). Полка занята — в сброс; повторная выдача не обходит перезарядку. Без ◆ и ✦."),
]

COUNTER_POWERS = [
    ("ArmorPiercingPower", "Pierce", "Powered attacks ignore {Amount} positive Plating, before damage caps. Does not create bonus damage against zero/negative Plating. True damage already bypasses Plating.",
     "Пробитие", "Силовые атаки игнорируют {Amount} положительных Лат, до ограничения урона. Не добавляет урон против нулевых/отрицательных Лат. Чистый урон и так обходит Латы."),
    ("ValveClosedPower", "Valve Closed", "This valve does not fuel Smelting Blast for {Amount} more enemy turns. An Attack refreshes the duration to 2, without stacking. A destroyed valve is also safe.",
     "Вентиль закрыт", "Этот вентиль не питает Плавильный взрыв ещё {Amount} ходов врагов. Атака обновляет срок до 2, без накопления. Уничтоженный вентиль тоже безопасен."),
    ("SolarStandardGuardPower", "Under the Solar Standard", "The Envoy who granted this protection deals 50% less move damage to this hero for the next enemy turn. Other enemies and permanent defenses are unchanged.",
     "Под Солнечным штандартом", "Посланник — источник защиты — наносит этому герою на 50 % меньше урона своими ходами в следующий ход врагов. Другие враги и постоянная защита не меняются."),
    ("BlackBookAuraPower", "Black Book Aura", "While this source lives, heroes have −I elemental, poison, blight and magic resistance. Does not alter permanent counters; no stacking from multiple sources. No effect on Light or True damage.",
     "Аура Чёрной книги", "Пока источник жив, у героев −I сопротивлений стихиям, яду, Хвори и магии. Постоянные счётчики не меняются; несколько источников не складываются. Не влияет на Свет и чистый урон."),
    ("GroundedByShoutPower", "Grounded", "Flight is removed for {Amount} full enemy turns. Then it returns. Replaying the same shout does not stack extra grounded turns.",
     "Приземлён", "Полёт снят ещё на {Amount} полных ходов врагов. Затем он возвращается. Повтор того же Клича не накапливает лишние ходы на земле."),
    ("MeteorWarningPower", "Meteor Rain Incoming", "The next action is Meteor Rain. Clear Skies cancels it for the party; Block and Fire Resistance also work. Meteors are on enemy actions 4/8/12, warning one action earlier.",
     "Надвигаются метеоры", "Следующее действие — Метеоритный дождь. Ясное небо отменяет его для отряда; Блок и огнестойкость тоже работают. Метеоры на действиях врага 4/8/12, предупреждение за действие."),
    ("EncounterCounterPendingPower", "Encounter Answer Reserved", "{Amount} encounter card(s) await a free hand slot. The same cards stay in this combat and delivery is retried after playing/discarding a card or next turn's draw. They never enter the permanent deck.",
     "Контрмера ожидает места", "{Amount} карт боя ожидают свободного места в руке. Те же экземпляры сохраняются в этом бою; повтор выдачи после розыгрыша/сброса карты или добора следующего хода. В постоянную колоду не попадают."),
]

COUNTER_MONSTERS = {
    "SOLAR_STANDARD": {
        "eng": {"name": "Solar Standard", "moves": {"SUN_BANNER": "Solar Banner"}},
        "rus": {"name": "Солнечный штандарт", "moves": {"SUN_BANNER": "Солнечное знамя"}},
    },
    "DEVOURED_SOUL_GHOST": {
        "eng": {"name": "Devoured Soul", "moves": {"DEVOUR_GHOST_TOUCH": "Soul Touch"}},
        "rus": {"name": "Поглощённая душа", "moves": {"DEVOUR_GHOST_TOUCH": "Касание души"}},
    },
}
