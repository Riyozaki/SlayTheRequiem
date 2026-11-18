"""Б2.1: mandatory, saved hero creation / Reborn sheets. Values are plain text (Godot Label)."""

# Runtime formats CREATION.<key>.title dynamically. check_loc.py validates all literal/enum keys and placeholders.
_CREATION = {
    "title": ("Create your hero", "Создание героя"),
    "heading": ("Create your hero", "Создание героя"),
    "rules": (
        "Choose your origin, background and an independent starting bonus, then review the result. Nothing is granted before confirmation. Legacy menu presets, including Random, do not skip these choices. Neow and Omens remain separate.",
        "Выберите происхождение, предысторию и отдельный стартовый бонус, затем проверьте результат. До подтверждения ничего не выдаётся. Старые настройки, включая «Случайно», не пропускают эти шаги. Нео и Знамения остаются отдельно.",
    ),
    "reviewRules": (
        "Confirm this identity and starting bonus. Starter cards and items are granted once. Vagrant's optional removal is selected separately before applying effects. A full potion belt or cancelled bonus removal returns you to the bonus step without repeating identity gifts.",
        "Подтвердите личность и стартовый бонус. Стартовые карты и предметы выдаются один раз. Необязательное удаление Бродяги выбирается отдельно до применения эффектов. Если пояс полон или удаление за бонус отменено, вы вернётесь к бонусу без повторной выдачи подарков личности.",
    ),
    "stage.Origin": ("1 · Origin", "1 · Происхождение"),
    "stage.Background": ("2 · Background", "2 · Предыстория"),
    "stage.Bonus": ("3 · Starting bonus", "3 · Стартовый бонус"),
    "stage.Review": ("Review before applying", "Проверка перед применением"),
    "stage.Applying": ("Applying confirmed choices", "Применение подтверждённых решений"),
    "stage.Receipt": ("Actual changes", "Фактические изменения"),
    "stage.Complete": ("Complete", "Завершено"),
    "selectedOrigin": ("Origin: {Name}", "Происхождение: {Name}"),
    "selectedBackground": ("Background: {Name}", "Предыстория: {Name}"),
    "selectedBonus": ("Independent starting bonus: {Name}", "Отдельный стартовый бонус: {Name}"),
    "none": ("None", "Нет"),
    "noOrigin": ("No origin", "Без происхождения"),
    "noBackground": ("No background", "Без предыстории"),
    "noneHint": ("An explicit choice, not a missing step. No identity benefits from this slot.",
                 "Осознанный выбор, не пропущенный шаг. Эта часть личности не даёт бонусов."),
    "random": ("Choose randomly", "Выбрать случайно"),
    "randomHint": ("Only this button authorizes a seeded random choice. The chosen identity is shown before confirmation.",
                   "Только эта кнопка разрешает выбор по сиду. Результат будет показан до подтверждения."),
    "back": ("Back", "Назад"),
    "apply": ("Apply these choices", "Применить решения"),
    "gold": ("60 gold", "60 золота"),
    "goldHint": ("Gain {Amount} gold through the normal gold-gain rules.",
                 "Получить {Amount} золота по обычным правилам получения золота."),
    "potion": ("One common potion", "Одно обычное зелье"),
    "potionHint": ("A seeded Common-rarity potion from your unlocked pool. Requires a free slot after background gifts; no gold or random replacement on refusal.",
                   "Зелье обычной редкости из открытого пула, выбранное по сиду. После подарков предыстории нужен свободный слот; при отказе нет молчаливой замены на золото или другой бонус."),
    "remove": ("Remove one basic card", "Удалить одну базовую карту"),
    "removeHint": ("Choose one removable Basic-rarity card after background replacements. Cancelling returns to this bonus choice; it never removes a different card.",
                   "Выберите удаляемую карту базовой редкости после замен предыстории. Отмена вернёт к выбору бонуса; другая карта вместо выбранной не удаляется."),
    "bonusRetry": ("Identity effects have already been applied once. Only the unconsumed starting bonus can change now.",
                   "Эффекты личности уже применены один раз. Сейчас можно изменить только ещё не полученный стартовый бонус."),
    "incompatible": ("Unavailable with your current form or oath.", "Недоступно при текущей форме или клятве."),
    "protocolMismatch": ("Startup not agreed. All players need the same mod build; wait for the lobby check, then ready again.",
                         "Старт не согласован. У всех должна быть одна сборка мода; дождитесь проверки лобби и снова нажмите «Готов»."),
    "identitySchool": ("Identity mastery: +1 {School}; this school counts as native while this identity is active.",
                       "Владение от личности: +1, {School}; пока эта личность активна, школа считается родной."),
    "schoolPreview": ("{School}: {Level}/5 · {Native}", "{School}: {Level}/5 · {Native}"),
    "schoolChange": ("{School}: {Before} → {After}/5 · {Native}", "{School}: {Before} → {After}/5 · {Native}"),
    "native": ("native · 1 stat point per rank", "родная · 1 очко характеристики за ранг"),
    "foreign": ("foreign · 2 stat points per rank", "чужая · 2 очка характеристики за ранг"),
    "school.Destruction": ("Destruction", "Разрушение"),
    "school.Restoration": ("Restoration", "Восстановление"),
    "school.Alteration": ("Alteration", "Изменение"),
    "school.Conjuration": ("Conjuration", "Колдовство"),
    "school.Illusion": ("Illusion", "Иллюзия"),
    "receipt": ("Hero · confirmed result", "Герой · подтверждённый результат"),
    "begin": ("Accept summary · continue", "Сводка понятна · продолжить"),
    "continue": ("Continue", "Продолжить"),
    "resources": (
        "Max HP: {OldHp} → {Hp}\nStamina pool ◆: {OldStamina} → {Stamina}\nMana pool ✦: {OldMana} → {Mana}\nBurden capacity: {OldCapacity} → {Capacity}; current load: {Weight}/{Capacity}\nGold: {OldGold} → {Gold}",
        "Максимум ♥: {OldHp} → {Hp}\nПул выносливости ◆: {OldStamina} → {Stamina}\nПул маны ✦: {OldMana} → {Mana}\nВместимость Груза: {OldCapacity} → {Capacity}; текущий вес: {Weight}/{Capacity}\nЗолото: {OldGold} → {Gold}",
    ),
    "deckChanges": ("Deck changes", "Изменения колоды"),
    "deck": ("Current deck · {Count} cards", "Текущая колода · карт: {Count}"),
    "unchanged": ("No changes.", "Без изменений."),
    "pending": ("Saved card grants still pending: {Count}. They are not counted as received cards in this summary; unload and retry outside combat.",
                "Сохранённые права на карты, ожидающие выдачи: {Count}. Они не считаются полученными в этой сводке; разгрузитесь и повторите получение вне боя."),
    "potionReceived": ("Starting potion actually received: {Name}", "Стартовое зелье действительно получено: {Name}"),
    "separate": ("Neow and Omens are separate from this bonus. Their choices and restrictions still apply.",
                 "Нео и Знамения независимы от этого бонуса. Их выборы и ограничения сохраняются."),
    "controls": ("Mouse / ↑ ↓ / Tab / D-pad · Enter / Space / A: confirm. Back is an explicit button. Closing the game retains the saved draft, not an automatic choice.",
                 "Мышь / ↑ ↓ / Tab / крестовина · Enter / Пробел / A: подтвердить. Возврат — отдельной кнопкой. При выходе из игры сохраняется записанный черновик, а не автоматический выбор."),
    "rebirth": ("Reborn · change identity and refund stats", "Перерождение · смена личности и возврат характеристик"),
    "rebirthRules": (
        "Change origin and background; refund invested stat points and remove only paid stat mastery. Learned skills, treatises and acquired loot stay. The origin-dependent point pool is recalculated. Starter cards, removals, potions and items are NOT granted again. Nothing changes until confirmation; cancel does not consume your Path.",
        "Смените происхождение и предысторию; вложения характеристик возвращаются, снимается только оплаченное ими владение. Навыки, трактаты и добыча остаются. Пул очков от происхождения пересчитывается. Стартовые карты, удаления, зелья и предметы повторно НЕ выдаются. До подтверждения ничего не меняется; отмена не расходует Путь.",
    ),
    "cancelRebirth": ("Cancel · keep Path choice", "Отменить · сохранить выбор Пути"),
    "confirmRebirth": ("Confirm Reborn · consume this Path", "Подтвердить Перерождение · выбрать этот Путь"),
    "refund": ("Current stat investments to reset: {Points}. Skill points are not refunded; learned nodes remain.",
               "Сбрасываемые вложения характеристик: {Points}. Очки навыков не возвращаются; изученные узлы остаются."),
    "refunded": ("Stat investment: {Spent} → 0. Unspent stat points: {OldFree} → {Free} (new origin's point pool applies).",
                 "Вложения характеристик: {Spent} → 0. Свободные очки характеристик: {OldFree} → {Free} (учтён пул нового происхождения)."),
    "rebirthPreserved": ("Skills, treatises and acquired loot retained. No starting gift repeated.",
                         "Навыки, трактаты и добыча сохранены. Стартовые подарки не повторялись."),
    "legacyRebirth": (
        "Reborn is unavailable: this hero lacks a complete, source-accounted mastery/stat/identity snapshot, or an earlier operation is unresolved. No Path has been consumed and no refund guessed. Completed legacy saves retain their recorded values; use the matching build/checkpoint for an unfinished old start.",
        "Перерождение недоступно: у героя нет полной истории источников владения, характеристик и личности либо не подтверждена предыдущая операция. Путь не потрачен, возврат не угадан. Завершённый старый сейв сохраняет записанные значения; для незавершённого старта используйте подходящую сборку или контрольную копию.",
    ),
}

CREATION_TIPS = {lang: {} for lang in ("eng", "rus")}
for _key, _texts in _CREATION.items():
    for _index, _lang in enumerate(("eng", "rus")):
        if _key == "title":
            CREATION_TIPS[_lang]["CREATION.title"] = _texts[_index]
            CREATION_TIPS[_lang]["CREATION.description"] = _CREATION["rules"][_index]
        else:
            CREATION_TIPS[_lang][f"CREATION.{_key}.title"] = _texts[_index]
            CREATION_TIPS[_lang][f"CREATION.{_key}.description"] = _texts[_index]

# Preserved dropdown values remain parseable for old configs, but cannot bypass the new wizard.
CREATION_SETTINGS = {
    "eng": {
        f"{key}.hover.desc": "Legacy preset retained for config compatibility. Every new Requiem run asks for origin and background in the saved hero wizard; neither Random nor a fixed preset skips your choice. Random is an explicit button in that wizard."
        for key in ("PLATEBEARER_ORIGIN", "SILENT_ORIGIN", "REGENT_ORIGIN", "BACKGROUND")
    },
    "rus": {
        f"{key}.hover.desc": "Старый пресет сохранён для совместимости настроек. В каждом новом забеге Реквиема происхождение и предыстория выбираются в сохраняемом мастере; ни «Случайно», ни фиксированный пресет не пропускают выбор. Случайный результат — отдельная кнопка самого мастера."
        for key in ("PLATEBEARER_ORIGIN", "SILENT_ORIGIN", "REGENT_ORIGIN", "BACKGROUND")
    },
}
