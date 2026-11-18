"""B3 local presentation preferences and persistent shout shelf states; no gameplay/protocol fields."""

QUALITY_SETTINGS = {
    "eng": {
        "REDUCED_MOTION.title": "Reduced Requiem motion",
        "REDUCED_MOTION.hover.title": "Reduced motion (local)",
        "REDUCED_MOTION.hover.desc": "Stops the mod's breathing/sway, lunges, shake, flashes and bouncing UI. Optional combat bursts are skipped; "
                                     "phase/form sprites, intents, resource numbers, Path signs and shout cooldown/return labels remain. "
                                     "Sounds have their own switch. Does not disable the base game's animations. Cosmetic only; not sent to other players.",
    },
    "rus": {
        "REDUCED_MOTION.title": "Меньше движения Реквиема",
        "REDUCED_MOTION.hover.title": "Меньше движения (локально)",
        "REDUCED_MOTION.hover.desc": "Убирает дыхание/покачивание, выпады, тряску, вспышки и пружины интерфейса мода. Дополнительные боевые эффекты пропускаются; "
                                     "картинки фаз/форм, интенты, числа ресурсов, Знаки Путей и подписи перезарядки/возврата Кличей остаются. "
                                     "У звука свой выключатель. Не отключает анимации основной игры. Только оформление; другим игрокам не передаётся.",
    },
}

QUALITY_TIPS = {
    "eng": {
        "UI.shelf.capacity.title": "Shouts {Used}/{Capacity}",
        "UI.shelf.waitLabel.title": "{Turns} turns",
        "UI.shelf.readyLabel.title": "↩ Ready",
        "UI.shelf.waiting.title": "{Name}: on the shelf for {Turns} more owner turns. Not Exhausted.",
        "UI.shelf.pending.title": "{Name}: cooldown is over; return has not committed. The exact card remains due for the next owner turn.",
        "UI.shelf.departed.title": "On cooldown: {Name}",
        "UI.shelf.returnedHand.title": "Returned to hand: {Name}",
        "UI.shelf.returnedDiscard.title": "Returned to discard: {Name}",
    },
    "rus": {
        "UI.shelf.capacity.title": "Кличи {Used}/{Capacity}",
        "UI.shelf.waitLabel.title": "{Turns} х.",
        "UI.shelf.readyLabel.title": "↩ Готов",
        "UI.shelf.waiting.title": "{Name}: ещё {Turns} ходов владельца на полке. Не изгнан.",
        "UI.shelf.pending.title": "{Name}: перезарядка закончилась, возврат ещё не подтверждён. Эта же карта ожидает следующего хода владельца.",
        "UI.shelf.departed.title": "Перезарядка: {Name}",
        "UI.shelf.returnedHand.title": "Вернулся в руку: {Name}",
        "UI.shelf.returnedDiscard.title": "Вернулся в сброс: {Name}",
    },
}
