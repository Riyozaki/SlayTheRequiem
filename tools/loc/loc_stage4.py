"""Тексты этапа 4: Знамения, Осколки отлива, праведность, Небесные, Князья Бездны (docs/design/05)."""

# Названия богов и князей — для генерации однотипных реликвий.
GODS = [
    # (класс-суффикс, eng, rus, благословение eng, rus, амулет eng, rus, облачение eng, rus, Дух eng, rus)
    ("WingedHour", "the Winged Hour", "Крылатого Часа",
     "+2 max ◆; Fire Resistance I.", "+2 пул ◆; сопротивление огню I.",
     "+1 ◆ regeneration.", "+1 прирост ◆.",
     "Fire Resistance I.", "Сопротивление огню I.",
     "Shouts recharge 1 turn faster.", "Кличи перезаряжаются на 1 ход быстрее."),
    ("AnvilFather", "the Anvil Father", "Отца Наковальни",
     "+6 max HP; +1 ◆ regeneration.", "+6 макс. ♥; +1 прирост ◆.",
     "Plating +1.", "Латы +1.",
     "Heavy attacks (2+ ◆) deal 2 more damage.", "Тяжёлые атаки (от 2 ◆) +2 урона.",
     "Heavy attacks deal 3 more damage.", "Тяжёлые атаки +3 урона."),
    ("ScrollKeeper", "the Scroll Keeper", "Хранителя Свитков",
     "+2 max ✦; +1 ✦ regeneration.", "+2 пул ✦; +1 прирост ✦.",
     "Start each combat with 2 more ✦.", "+2 ✦ в начале боя.",
     "+2 max ✦.", "+2 пул ✦.",
     "The first spell each turn is copied.", "Первое заклинание хода копируется."),
    ("MirrorMaiden", "the Mirror Maiden", "Девы Зеркал",
     "+1 ✦ regeneration; Spell Absorption 1.", "+1 прирост ✦; Поглощение заклинаний 1.",
     "Ward 2.", "Покров 2.",
     "Spell Absorption +1.", "Поглощение заклинаний +1.",
     "Illusions and charms cost no ✦ once per turn.", "Иллюзии и очарования раз за ход без ✦."),
    ("WindMistress", "the Wind Mistress", "Владычицы Ветров",
     "+1 ◆ and ✦ regeneration.", "+1 прирост ◆ и ✦.",
     "Poise +2.", "Стойкость +2.",
     "Poison Resistance I.", "Сопротивление ядам I.",
     "Poison Resistance II.", "Сопротивление ядам II."),
    ("HearthMother", "the Hearth Mother", "Матери Очага",
     "Heal 2 at the start of each of the first 3 turns of combat; immune to Paralysis.",
     "Лечение 2 в начале каждого из первых 3 ходов боя; иммунитет к Параличу.",
     "+5 max HP.", "+5 макс. ♥.",
     "Resting heals 25% more.", "Отдых лечит на 25% больше.",
     "All healing +50%.", "Любое лечение +50%."),
    ("ThresholdWarden", "the Threshold Warden", "Стража Порога",
     "Plating +1; Frost Resistance I. Incompatible with necromancy.", "Латы +1; сопротивление холоду I. Несовместим с некромантией.",
     "Frost Resistance I; +1 damage to undead and the Abyss.", "Сопротивление холоду I; +1 урона по нежити и Бездне.",
     "Plating +1.", "Латы +1.",
     "Immune to Freeze.", "Иммунитет к Обледенению."),
    ("MercyShield", "the Mercy Shield", "Щита Милосердия",
     "Attacks deal 1 more damage, 3 more to undead and the Abyss. Incompatible with necromancy.",
     "Атаки +1 урона, по нежити и Бездне — +3. Несовместим с некромантией.",
     "+2 damage to undead and the Abyss.", "+2 урона по нежити и Бездне.",
     "Plating +1.", "Латы +1.",
     "The first attack each turn against undead adds Radiance.", "Первая атака хода по нежити — Сияние."),
    ("ThunderCrowned", "the Thunder-Crowned", "Громового Венценосца",
     "Shouts deal 3 more damage; Shock Resistance I.", "Кличи +3 урона; сопротивление разряду I.",
     "Shock Resistance I; +1 max ◆.", "Сопротивление разряду I; +1 пул ◆.",
     "Attacks deal 1 more damage.", "Атаки +1 урона.",
     "Shouts apply Shock.", "Кличи накладывают Шок."),
    ("SunArcher", "the Sun Archer", "Солнечного Лучника",
     "Radiance 2: your attacks against undead and the Abyss deal 2 extra light damage.",
     "Сияние 2: ваши атаки по нежити и Бездне наносят ещё 2 урона светом.",
     "Radiance +1.", "Сияние +1.",
     "Magic Resistance I.", "Сопротивление магии I.",
     "Aura: undead and the Abyss get Magic vulnerability I.", "Аура: нежить и Бездна — уязвимость к магии I."),
]

GOD_TITLES = {
    "WingedHour": ("Winged Hour", "Крылатый Час"), "AnvilFather": ("Anvil Father", "Отец Наковальни"),
    "ScrollKeeper": ("Scroll Keeper", "Хранитель Свитков"), "MirrorMaiden": ("Mirror Maiden", "Дева Зеркал"),
    "WindMistress": ("Wind Mistress", "Владычица Ветров"), "HearthMother": ("Hearth Mother", "Матерь Очага"),
    "ThresholdWarden": ("Threshold Warden", "Страж Порога"), "MercyShield": ("Mercy Shield", "Щит Милосердия"),
    "ThunderCrowned": ("Thunder-Crowned", "Громовой Венценосец"), "SunArcher": ("Sun Archer", "Солнечный Лучник"),
}

BLESSING_NOTE = (
    "\nRighteous — full; Tainted — only the first bonus; Apostate — silent. With this god's Amulet: {a} With his Robe: {r} "
    "All together + Heavenly Shield — Divine Spirit: {s}\nShown number — blessing strength (2 full, 1 dimmed, 0 silent).",
    "\nПраведник — полностью; Запятнанный — только первый бонус; Отступник — молчит. С Амулетом этого бога: {a} С Облачением: {r} "
    "Всё вместе + «Небесный щит» — Дух божества: {s}\nЧисло на иконке — сила благословения (2 полная, 1 тусклая, 0 молчит).",
)


def _god_relics():
    rows = []
    for key, gen_e, gen_r, be, br, ae, ar, re_, rr, se, sr in GODS:
        te, tr = GOD_TITLES[key]
        rows.append((f"BlessingOf{key}", f"Blessing of {te}", be + BLESSING_NOTE[0].format(a=ae, r=re_, s=se),
                     f"Благословение: {tr}", br + BLESSING_NOTE[1].format(a=ar, r=rr, s=sr), "", ""))
        rows.append((f"AmuletOf{key}", f"Amulet of {gen_e[4:]}",
                     f"Amulet of the god's set (Shrine, 120 gold). With the Blessing of {te} while Righteous: {ae}",
                     f"Амулет {gen_r}", f"Амулет комплекта бога (Святилище, 120 золотых). При благословении того же бога у Праведника: {ar}",
                     "", ""))
        rows.append((f"RobeOf{key}", f"Robe of {gen_e[4:]}",
                     f"Robe (body), [gold]Ward[/gold] 2 (Shrine, 150 gold). With the Blessing of {te} while Righteous: {re_}",
                     f"Облачение {gen_r}", f"Одеяние (тело), [gold]Покров[/gold] 2 (Святилище, 150 золотых). При благословении того же бога у Праведника: {rr}",
                     "", ""))
    return rows


# (класс, eng title, eng text, rus title, rus text, eng flavor, rus flavor)
STAGE4_RELICS = [
    ("FaithSigil", "Sigil of Faith",
     "Your righteousness: Righteous → Tainted → Apostate. Theft and robbery −1 step; a pact with a Prince of the Abyss — Apostate; "
     "Abyssal items keep you no higher than Tainted. Atonement at a Shrine: +1 step, once per act.\n"
     "Shown number — current state (0 Righteous, 1 Tainted, 2 Apostate).",
     "Знак веры",
     "Ваша праведность: Праведник → Запятнанный → Отступник. Кража и грабёж — −1 ступень; договор с Князем Бездны — Отступник; "
     "Бездонные вещи держат не выше Запятнанного. Искупление в Святилище: +1 ступень, раз в акт.\n"
     "Число на иконке — состояние (0 Праведник, 1 Запятнанный, 2 Отступник).",
     "Light or shadow — the sigil remembers.", "Свет или тень — знак помнит всё."),
    # Знамения.
    ("OmenOfTheBlade", "Omen of the Blade",
     "+6 max HP; attacks deal 1 more damage; +1 ◆ regeneration; being Tired does not reduce damage.",
     "Знамение Клинка", "+6 макс. ♥; атаки +1 урона; +1 прирост ◆; «Устал» не снижает урон.",
     "The Warrior's stars burn red.", "Звёзды Воина горят красным."),
    ("OmenOfTheBastion", "Omen of the Bastion",
     "Plating +1; Fire, Frost and Shock Resistance I; Poise +3; the first Concussion each combat is ignored.",
     "Знамение Бастиона", "Латы +1; сопротивление I огню, холоду и разряду; Стойкость +3; первое Оглушение за бой игнорируется.",
     "The Lord stands.", "Лорд стоит."),
    ("OmenOfTheEye", "Omen of the Eye",
     "+2 max ✦, +1 ✦ regeneration, +1 to all spell schools. Aura: enemies get Magic vulnerability I.",
     "Знамение Ока", "+2 пул ✦, +1 прирост ✦, +1 ко всем школам. Аура: враги — уязвимость к магии I.",
     "The Mage sees through.", "Маг видит насквозь."),
    ("OmenOfTheSponge", "Omen of the Sponge",
     "+6 max ✦; Spell Absorption 1; Ward 2. Downside: −1 ✦ regeneration.",
     "Знамение Губки", "+6 пул ✦; Поглощение заклинаний 1; Покров 2. Минус: прирост ✦ −1.",
     "The Atronach drinks, but cannot pour.", "Атронах пьёт, но не наливает."),
    ("OmenOfTheRite", "Omen of the Rite",
     "+1 summon slot; Conjuration spells cost 1 less ✦.",
     "Знамение Обряда", "+1 слот призыва; заклинания Колдовства −1 ✦.",
     "The Ritual answers.", "Ритуал отвечает."),
    ("OmenOfTheBurden", "Omen of the Burden",
     "+10 Burden capacity; immune to Freeze, Paralysis and Shock; attacks cost at most 1 ◆.",
     "Знамение Ноши", "+10 к вместимости Груза; иммунитет к Обледенению, Параличу и Шоку; атаки стоят не больше 1 ◆.",
     "The Steed carries all.", "Конь вынесет всё."),
    ("OmenOfTheTrickster", "Omen of the Trickster",
     "You may try [gold]Theft[/gold] in shops (once per visit, costs righteousness); gold gained +15%.",
     "Знамение Ловкача", "В лавках доступна [gold]Кража[/gold] (раз за визит, стоит праведности); золото +15%.",
     "The Thief smiles.", "Вор улыбается."),
    ("OmenOfTheVoice", "Omen of the Voice",
     "Shouts deal 3 more damage and recharge 1 turn faster; cards in shops are one rarity higher.",
     "Знамение Гласа", "Кличи +3 урона и перезаряжаются на 1 ход быстрее; карты в лавках на ступень редкости выше.",
     "The Voice carries over the mountain.", "Глас слышен за горой."),
    ("OmenOfTheShade", "Omen of the Shade",
     "Start each combat [gold]Invisible[/gold]: the first enemy attack against you misses; your attacks from Invisibility deal double damage.",
     "Знамение Тени", "Каждый бой начинается в [gold]Невидимости[/gold]: первая атака врага по вам промахивается; ваши атаки из Невидимости ×2.",
     "The Shadow hides you.", "Тень скрывает вас."),
    ("OmenOfTheSerpent", "Omen of the Serpent",
     "Poison you apply +1; Poison Resistance II; aura: enemies −I Poison resistance; weapon poisons hold 5 charges.",
     "Знамение Змея", "Накладываемый Яд +1; сопротивление ядам II; аура: враги −I к ядам; яды на оружии — до 5 зарядов.",
     "The Serpent wanders the sky.", "Змей бродит по небу."),
    # Осколки отлива.
    ("EarthShard", "Shard of Earth", "Until the end of the act: +1 max ◆, Plating +1. Downside: −1 card in your opening hand.",
     "Осколок Земли", "До конца акта: +1 пул ◆, Латы +1. Минус: −1 карта в первой руке.", "", ""),
    ("RootShard", "Shard of Root", "Until the end of the act: Magic Resistance I, Poison Resistance II. Downside: Fire vulnerability.",
     "Осколок Корня", "До конца акта: сопротивление магии I, ядам II. Минус: уязвимость к огню.", "", ""),
    ("BeastShard", "Shard of the Beast", "Until the end of the act: attacks +2 damage, Pierce 1. Downside: physical attacks deal 1 more damage to you.",
     "Осколок Зверя", "До конца акта: атаки +2 урона, Пробитие 1. Минус: физический урон по вам +1 за удар.", "", ""),
    ("WaterShard", "Shard of Water", "Until the end of the act: +3 max ✦, +1 ✦ regeneration. Downside: Shock vulnerability.",
     "Осколок Воды", "До конца акта: +3 пул ✦, +1 прирост ✦. Минус: уязвимость к разряду.", "", ""),
    ("WindShard", "Shard of Wind", "Until the end of the act: +1 card in your opening hand, +1 ◆ regeneration. Downside: Plating −1.",
     "Осколок Ветра", "До конца акта: +1 карта в первой руке, +1 прирост ◆. Минус: Латы −1.", "", ""),
    ("SunShard", "Shard of the Sun", "Until the end of the act: +8 max HP, heal 1 at the start of each turn. Downside: Frost vulnerability.",
     "Осколок Солнца", "До конца акта: +8 макс. ♥, лечение 1 в начале каждого хода. Минус: уязвимость к холоду.", "", ""),
    # Дары Князей Бездны.
    ("GiftOfPlagueLord", "Gift of the Plague Lord",
     "Gift: your attacks apply 1 [gold]Blight[/gold]; +1 ◆ regeneration. Price: a random Disease now and at the start of each act. "
     "Vow — kill 3 enemies under Blight: Diseases give their bonuses too.\nShown number — Vow progress.",
     "Дар Повелителя Мора",
     "Дар: ваши атаки накладывают [gold]Хворь[/gold] 1; +1 прирост ◆. Цена: случайная Болезнь сейчас и в начале каждого акта. "
     "Обет — убей 3 врагов под Хворью: Болезни дают и плюсы.\nЧисло на иконке — прогресс Обета.",
     "Every fever is a prayer.", "Каждая лихорадка — молитва."),
    ("GiftOfTwilightWidow", "Gift of the Twilight Widow",
     "Gift: start each combat [gold]Invisible[/gold]. Price: −5 max HP. Vow — win 2 combats without taking damage on the first turn: "
     "+1 ✦ regeneration and Invisibility lasts 2 turns.\nShown number — Vow progress.",
     "Дар Сумеречной Вдовы",
     "Дар: каждый бой начинается в [gold]Невидимости[/gold]. Цена: −5 макс. ♥. Обет — выиграй 2 боя, не получив урона в первый ход: "
     "+1 прирост ✦ и Невидимость на 2 хода.\nЧисло на иконке — прогресс Обета.",
     "Luck is a shadow that owes you.", "Удача — тень, которая вам должна."),
    ("GiftOfWebWeaver", "Gift of the Web Weaver",
     "Gift: heal 1 for each attack hit on a poisoned enemy (up to 5 per turn). Price: resting heals 30% less. "
     "Vow — poison 3 elites: attacks deal 2 more damage to enemies below 50% HP.\nShown number — Vow progress.",
     "Дар Ткачихи Паутины",
     "Дар: лечение 1 за каждый удар атакой по отравленному врагу (до 5 за ход). Цена: отдых лечит на 30% меньше. "
     "Обет — отрави 3 элиты: атаки +2 урона по врагам с ♥ меньше 50%.\nЧисло на иконке — прогресс Обета.",
     "Every thread leads to her.", "Каждая нить ведёт к ней."),
    ("GiftOfGateBreaker", "Gift of the Gate Breaker",
     "Gift: summons and Abyssal weapons deal 2 more damage; the first spell each turn costs 1 less ✦. Price: Apostate forever. "
     "Vow — kill an Abyss elite: aura — enemies get Fire vulnerability.\nShown number — Vow progress.",
     "Дар Разрушителя Врат",
     "Дар: призывы и Бездонное оружие +2 урона; первое заклинание хода −1 ✦. Цена: Отступник навсегда. "
     "Обет — убей элиту Бездны: аура — враги уязвимы к огню.\nЧисло на иконке — прогресс Обета.",
     "Gates are for breaking.", "Врата — чтобы их ломать."),
    ("GiftOfTreachery", "Gift of the Prince of Treachery",
     "Gift: attacks deal 2 more damage, Pierce 1. Price: your summons take 50% more damage. "
     "Vow — win an elite fight with no summons: +1 card in your opening hand.\nShown number — Vow progress.",
     "Дар Князя Измены",
     "Дар: атаки +2 урона, Пробитие 1. Цена: ваши призывы получают на 50% больше урона. "
     "Обет — победи элиту без призывов: +1 карта в первой руке.\nЧисло на иконке — прогресс Обета.",
     "Trust no one. Especially him.", "Не верь никому. Особенно ему."),
    ("GiftOfRevel", "Gift of the Revel Prince",
     "Gift: you may hold 2 Special foods at once (food arrives in Stage 5). Price: a [gold]Dazed[/gold] «Hangover» enters your hand at the start of each combat. "
     "Vow — drink 3 potions: drinks reveal their second properties (Stage 5).\nShown number — Vow progress.",
     "Дар Хмельного Князя",
     "Дар: можно держать 2 Особые еды одновременно (еда — этап 5). Цена: в начале каждого боя в руку приходит «Похмелье» ([gold]Головокружение[/gold]). "
     "Обет — выпей 3 зелья: у напитков открываются вторые свойства (этап 5).\nЧисло на иконке — прогресс Обета.",
     "One more round.", "Ещё по одной."),
    ("GiftOfOutcast", "Gift of the Outcast",
     "Gift: heal 2 after each combat; heavy attacks (2+ ◆) deal 2 more damage. Price: righteousness is unavailable; shops are 15% more expensive. "
     "Vow — defeat a boss without potions: bonuses double.\nShown number — Vow progress.",
     "Дар Отверженного",
     "Дар: лечение 2 после каждого боя; тяжёлые атаки (от 2 ◆) +2 урона. Цена: праведность недоступна; лавки на 15% дороже. "
     "Обет — победи босса без зелий: бонусы удваиваются.\nЧисло на иконке — прогресс Обета.",
     "Spite is a kind of strength.", "Злоба — тоже сила."),
    ("GiftOfDuskStar", "Gift of the Dusk Star",
     "Gift: +6 max HP, +2 max ✦; Fire and Frost Resistance I. Price: star marks — each combat that many random cards cost 1 more energy; "
     "one more mark each act. Vow — finish an act with no allies dying: the Price is lifted.\nShown number — Vow progress.",
     "Дар Сумеречной Звезды",
     "Дар: +6 макс. ♥, +2 пул ✦; сопротивление огню и холоду I. Цена: звёздные метки — в каждом бою столько случайных карт стоят на 1 энергию больше; "
     "с каждым актом меток на одну больше. Обет — пройди акт без смерти союзников: Цена снимается.\nЧисло на иконке — прогресс Обета.",
     "Twilight and dawn — both hers.", "Сумерки и рассвет — оба её."),
] + _god_relics()


def _event(eid, title, initial, options, pages):
    """options: {KEY: (title, description)}, pages: {PAGE: description}."""
    out = {f"{eid}.title": title, f"{eid}.pages.INITIAL.description": initial}
    for key, (t, d) in options.items():
        out[f"{eid}.pages.INITIAL.options.{key}.title"] = t
        out[f"{eid}.pages.INITIAL.options.{key}.description"] = d
    for page, d in pages.items():
        out[f"{eid}.pages.{page}.description"] = d
    return out


PREFIX = "SLAYTHEREQUIEM-"

STAGE4_EVENTS = {
    "eng": {
        **_event(PREFIX + "EBB_STONES_EVENT", "Six Stones by the Water",
                 "Six stones lie in the shallows, humming with the ebb. Three of them are within reach. "
                 "A shard taken now will last until the end of the act.",
                 {"LEAVE": ("Leave", "Let the tide keep them.")},
                 {"TAKEN": "The stone is warm in your hand. The tide will take it back when the act ends.",
                  "LEAVE": "You walk away. The stones keep humming."}),
        **_event(PREFIX + "SHRINE_EVENT", "Shrine",
                 "A quiet shrine of one of the Celestials. A priest tends the candles and sells relics of the god's set: "
                 "an Amulet for 120 gold and a Robe for 150.",
                 {"BLESSED_ALREADY": ("Blessed", "You already carry this god's blessing."),
                  "BLESSING_DENIED": ("Silence", "The Celestials do not answer the Apostate."),
                  "REDEEM": ("Atone", "Donate [gold]{Cost}[/gold] gold: righteousness +1 step (once per act)."),
                  "REDEEM_LOCKED": ("Atone", "Requires [gold]{Cost}[/gold] gold; once per act; impossible for the Outcast's chosen."),
                  "GARMENT_POOR": ("Not enough gold", "This relic costs [gold]{Price}[/gold] gold."),
                  "ROB": ("Rob the shrine", "Take [gold]{Gold}[/gold] gold. Righteousness −1 step."),
                  "LEAVE": ("Leave", "Bow and go.")},
                 {"BLESSED": "Warmth spreads through you. Anything else?",
                  "REDEEMED": "The priest nods. A weight lifts from your soul.",
                  "BOUGHT": "The priest wraps the relic in clean linen.",
                  "ROBBED": "You run, the offering bowl clinking. Somewhere a candle goes out.",
                  "LEAVE": "The candles flicker behind you."}),
        **_event(PREFIX + "DARK_SHRINE_EVENT", "Dark Shrine",
                 "An altar of black stone. Two Princes of the Abyss offer their gifts — each with a Price. "
                 "A pact makes you Apostate.",
                 {"REFUSE": ("Refuse", "Turn away from the altar.")},
                 {"ACCEPTED": "The pact is sealed. The Prince is watching.",
                  "LEAVE": "Whispers follow you out."}),
    },
    "rus": {
        **_event(PREFIX + "EBB_STONES_EVENT", "Шесть камней у воды",
                 "Шесть камней лежат на мелководье и гудят в такт отливу. Три из них можно достать. "
                 "Взятый осколок продержится до конца акта.",
                 {"LEAVE": ("Уйти", "Пусть их хранит прилив.")},
                 {"TAKEN": "Камень тёплый в ладони. Прилив заберёт его, когда кончится акт.",
                  "LEAVE": "Вы уходите. Камни продолжают гудеть."}),
        **_event(PREFIX + "SHRINE_EVENT", "Святилище",
                 "Тихое святилище одного из Небесных. Жрец следит за свечами и продаёт святыни комплекта бога: "
                 "Амулет за 120 золотых и Облачение за 150.",
                 {"BLESSED_ALREADY": ("Благословлены", "Вы уже несёте благословение этого бога."),
                  "BLESSING_DENIED": ("Тишина", "Небесные не отвечают Отступнику."),
                  "REDEEM": ("Искупление", "Пожертвовать [gold]{Cost}[/gold] золотых: праведность +1 ступень (раз в акт)."),
                  "REDEEM_LOCKED": ("Искупление", "Нужно [gold]{Cost}[/gold] золотых; раз в акт; недоступно избранникам Отверженного."),
                  "GARMENT_POOR": ("Не хватает золота", "Святыня стоит [gold]{Price}[/gold] золотых."),
                  "ROB": ("Ограбить святилище", "Забрать [gold]{Gold}[/gold] золотых. Праведность −1 ступень."),
                  "LEAVE": ("Уйти", "Поклониться и уйти.")},
                 {"BLESSED": "Тепло разливается по телу. Что-нибудь ещё?",
                  "REDEEMED": "Жрец кивает. С души падает тяжесть.",
                  "BOUGHT": "Жрец заворачивает святыню в чистое полотно.",
                  "ROBBED": "Вы бежите, звеня чашей для подношений. Где-то гаснет свеча.",
                  "LEAVE": "Свечи мерцают вам вслед."}),
        **_event(PREFIX + "DARK_SHRINE_EVENT", "Тёмное святилище",
                 "Алтарь из чёрного камня. Два Князя Бездны предлагают дары — каждый со своей Ценой. "
                 "Договор делает вас Отступником.",
                 {"REFUSE": ("Отказаться", "Отвернуться от алтаря.")},
                 {"ACCEPTED": "Договор скреплён. Князь смотрит на вас.",
                  "LEAVE": "Шёпот провожает вас до выхода."}),
    },
}

STAGE4_ANCIENTS = {
    "eng": {
        PREFIX + "NEOW_OMEN.title": "Accept an Omen",
        PREFIX + "NEOW_OMEN.description": "Instead of this gift — choose one of the Guardian Stones' Omens. One per run; it cannot be changed.",
        PREFIX + "NEOW_OMEN.page": "The constellations turn. Which one will you be born under?",
        PREFIX + "NEOW_OMEN.more.title": "Other Omens",
        PREFIX + "NEOW_OMEN.more.description": "Look at the rest of the sky.",
    },
    "rus": {
        PREFIX + "NEOW_OMEN.title": "Принять Знамение",
        PREFIX + "NEOW_OMEN.description": "Вместо этого дара — выбрать одно из Знамений Камней-Хранителей. Одно на забег, сменить нельзя.",
        PREFIX + "NEOW_OMEN.page": "Созвездия поворачиваются. Под каким из них вы родитесь?",
        PREFIX + "NEOW_OMEN.more.title": "Другие Знамения",
        PREFIX + "NEOW_OMEN.more.description": "Взглянуть на остальное небо.",
    },
}

STAGE4_SETTINGS = {
    "eng": {
        "ENABLE_PANTHEON.title": "Pantheon", "ENABLE_PANTHEON.hover.title": "Pantheon",
        "ENABLE_PANTHEON.hover.desc": "An Omen at Neow; from act 2 some «?» become Shrines, Dark Shrines and Six Stones by the Water. "
                                      "Must match in co-op.",
    },
    "rus": {
        "ENABLE_PANTHEON.title": "Пантеон", "ENABLE_PANTHEON.hover.title": "Пантеон",
        "ENABLE_PANTHEON.hover.desc": "Знамение у Нео; со 2-го акта часть «?» — Святилища, Тёмные святилища и Шесть камней у воды. "
                                      "В коопе должен совпадать.",
    },
}
