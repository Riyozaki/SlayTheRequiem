# Каталог карт мода (сгенерировано)

> Создаётся `python3 tools/cards/card_catalog.py --out docs/plan/card-catalog.md`. Не править руками.
> Значения `N(+M)` — база и прибавка улучшения. ⚡ — энергия, ◆ — выносливость, ✦ — мана (звёзды).
> Текст — русская локализация без разметки, переменные `{…}` оставлены как есть.

Всего карт: **340**. Латоносец — 39, Безмолвная — 20, Регент — 12, Кличи — 5, Заклинания — 42, Формы и Пути — 59, Зачарование карт — 6, Камень Памяти — 4, Услуги лавки — 35, Жетоны выбора — 118

## Латоносец (39)

| Класс | Название | Пул | ⚡ | ◆ | ✦ | Тип | Редкость | Цель | Урон | Блок | Прочее | Ключевые | Текст |
|---|---|---|---|---|---|---|---|---|---|---|---|---|---|
| `ShieldBash` | Удар щитом | Ironclad | 1 | 1 |  | Attack | Common | AnyEnemy | 5(+2) |  | StaggerPower 3(+1) |  | Нанести {Damage:diff()} урона. Наложить {StaggerPower:diff()} Шаткости. |
| `CrushingBlow` | Сокрушающий удар | Ironclad | 2 | 3 |  | Attack | Common | AnyEnemy | 14(+4) |  | SunderPower 2(+1) |  | Нанести {Damage:diff()} урона. Наложить {SunderPower:diff()} Раскола. |
| `ArmorSplitter` | Рассечь латы | Ironclad | 1 | 1 |  | Attack | Common | AnyEnemy | 6(+2) |  | SunderPower 2(+1) |  | Нанести {Damage:diff()} урона. Наложить {SunderPower:diff()} Раскола. |
| `IceWind` | Ледяной ветер | Ironclad | 1 | 1 |  | Attack | Common | AllEnemies | 4(+2) |  | ChillPower 2(+1) |  | Нанести {Damage:diff()} урона холодом ВСЕМ врагам. Наложить {ChillPower:diff()} Озноба ВСЕМ врагам. |
| `PiercingThrust` | Пробивающий выпад | Ironclad | 1 | 1 |  | Attack | Common | AnyEnemy | 7(+2) |  | BleedPower 2(+1) |  | Нанести {Damage:diff()} урона. Наложить {BleedPower:diff()} Кровотечения. |
| `FlatStrike` | Удар плашмя | Ironclad | 0 | 1 |  | Attack | Common | AnyEnemy | 3(+1) |  | StaggerPower 4(+2) |  | Нанести {Damage:diff()} урона. Наложить {StaggerPower:diff()} Шаткости. |
| `SweepingBlow` | Размашистый удар | Ironclad | 2 | 2 |  | Attack | Uncommon | AllEnemies | 8(+3) |  | StaggerPower 2(+1) |  | Нанести {Damage:diff()} урона ВСЕМ врагам. Наложить {StaggerPower:diff()} Шаткости ВСЕМ врагам. |
| `FrostStrike` | Морозный удар | Ironclad | 1 | 2 |  | Attack | Uncommon | AnyEnemy | 8(+3) |  | ChillPower 3(+1) |  | Нанести {Damage:diff()} урона холодом. Наложить {ChillPower:diff()} Озноба. |
| `AgainstAllOdds` | Вопреки всему | Ironclad | 1 | 0 |  | Attack | Uncommon | AnyEnemy | 7(+3) |  |  |  | Не тратит ◆. Нанести {Damage:diff()} урона. Вдвое больше, если вы Устали. |
| `WrathHammer` | Молот гнева | Ironclad | 3 | 4 |  | Attack | Rare | AnyEnemy | 22(+6) |  | StaggerPower 8(+2), SunderPower 3 |  | Нанести {Damage:diff()} урона. Наложить {StaggerPower:diff()} Шаткости и {SunderPower:diff()} Раскола. |
| `AimedShot` | Прицельный выстрел | Ironclad | 1 | 1 |  | Attack | Common | AnyEnemy | 7(+3) |  | VulnerablePower 1(+1) |  | Нанести {Damage:diff()} урона. С луком или арбалетом — наложить {VulnerablePower:diff()} Уязвимости. |
| `StokeTheForge` | Раздуть горн | Ironclad | 1 | 1 |  | Attack | Common | AnyEnemy | 5(+2) |  |  |  | Нанести {Damage:diff()} урона. Если в этом ходу сыграно 2+ атаки — нанести ещё раз. |
| `ThunderingBlow` | Громовой удар | Ironclad | 1 | 2 |  | Attack | Uncommon | AnyEnemy | 8(+3) |  | DrainVar 2(+1) |  | Нанести {Damage:diff()} урона разрядом. Выжечь {ManaDrain:diff()} ✦ врага. |
| `ForgeHeart` | Сердце горна | Ironclad | 2(-1) |  |  | Power | Rare | Self |  |  | ForgeHeartPower 1 |  | Каждая 3-я атака за ход: взять {ForgeHeartPower:diff()} карту. |
| `DuneDance` | Танец дюн | Ironclad | 1 | 0 |  | Attack | Common | AnyEnemy | 6(+2) |  | WearyBonusVar 4(+2) |  | Не тратит ◆. Нанести {Damage:diff()} урона. Если вы Устали — ещё {WearyBonus:diff()} и вернуть 1 ◆. |
| `DesertPatience` | Терпение пустыни | Ironclad | 1 |  |  | Skill | Common | Self |  | 6(+3) | CatchBreath.GainVar 3(+1) |  | Получить {Block:diff()} Блока. Если вы Устали — вернуть {StaminaGain:diff()} ◆. |
| `LockShields` | Сцепить щиты | Ironclad | 1 |  |  | Skill | Common | Self |  | 6(+3) | PlatingPower 1 |  | Получить {Block:diff()} Блока. Со щитом — получить {PlatingPower:diff()} Лат. |
| `ShieldCharge` | Таран щитом | Ironclad | 2 | 2 |  | Attack | Uncommon | AnyEnemy | 4(+3) |  |  |  | Нанести {Damage:diff()} урона плюс ваш текущий Блок. |
| `LegionDrill` | Строй легиона | Ironclad | 1 |  |  | Power | Uncommon | Self |  |  | LegionDrillPower 3(+1) |  | В начале хода, если надет щит, получать {LegionDrillPower:diff()} Блока. |
| `RendingCut` | Рваная рана | Ironclad | 1 | 1 |  | Attack | Common | AnyEnemy | 3(+1) |  | BleedPower 1(+1) |  | Дважды нанести {Damage:diff()} урона. Каждый удар накладывает {BleedPower:diff()} Кровотечения. |
| `OpenVeins` | Вскрыть жилы | Ironclad | 1(-1) |  |  | Skill | Uncommon | AnyEnemy |  |  |  |  | Удвоить Кровотечение цели. |
| `Hemorrhage` | Кровопускание | Ironclad | 2 | 2 |  | Attack | Rare | AnyEnemy | 8(+3) |  | PerBleedVar 2(+1) |  | Нанести {Damage:diff()} урона и ещё {PerBleed:diff()} за каждое Кровотечение цели. |
| `SteelEndurance` | Стальная выдержка | Ironclad | 1 |  |  | Skill | Common | Self |  | 4(+2) | PerPieceVar 2(+1) |  | Получить {Block:diff()} Блока и ещё {PerHeavy:diff()} за каждую надетую вещь тяжёлой брони. |
| `LivingFortress` | Живая крепость | Ironclad | 2 |  |  | Power | Rare | Self |  |  | PlatingPower 1(+1) |  | Получить {PlatingPower:diff()} Лат и ещё 1 за каждую надетую вещь тяжёлой брони. |
| `MeasuredStrike` | Выверенный удар | Ironclad | 1 | 1 |  | Attack | Common | AnyEnemy | 7(+3) |  |  |  | Оружейная. Нанести {Damage:diff()} урона. С одноручным оружием — взять 1 карту. |
| `SwordAndBoard` | Меч и щит | Ironclad | 1 | 1 |  | Attack | Uncommon | AnyEnemy | 6(+2) | 5(+2) |  |  | Оружейная. Нанести {Damage:diff()} урона. С одноручным оружием и щитом — получить {Block:diff()} Блока. |
| `OverheadSmash` | Удар с плеча | Ironclad | 2 | 3 |  | Attack | Uncommon | AnyEnemy | 12(+4) |  | TwoHandedBonusVar 6(+2) |  | Оружейная. Нанести {Damage:diff()} урона. С двуручным оружием — ещё {TwoHandedBonus:diff()}. |
| `MountainBreaker` | Горолом | Ironclad | 2 | 4 |  | Attack | Rare | AllEnemies | 9(+3) |  |  |  | Оружейная. Нанести {Damage:diff()} урона ВСЕМ врагам. С двуручным оружием — дважды. |
| `CloseRanks` | Сомкнуть латы | Ironclad | 2 |  |  | Power | Uncommon | Self |  |  | PlatingPower 2(+1) |  | Получить {PlatingPower:diff()} Лат. |
| `Tempering` | Закалка | Ironclad | 1(-1) |  |  | Power | Uncommon | Self |  |  | TemperingPower 1 |  | В начале хода восстанавливать на {TemperingPower:diff()} ◆ больше. |
| `Frostblood` | Кровь стужи | Ironclad | 2(-1) |  |  | Power | Rare | Self |  |  | FrostbloodPower 1, FrostResistancePower 1 |  | Получить Сопротивление холоду {FrostResistancePower:diff()}. Ваш Озноб сильнее на {FrostbloodPower:diff()}. Озноб на вас спадает целиком. |
| `Unbreakable` | Непоколебимость | Ironclad | 3 |  |  | Power | Rare | Self |  |  | PlatingPower 3(+1), UnbreakablePower 1 |  | Получить {PlatingPower:diff()} Лат. Вас больше нельзя ошеломить. |
| `CatchBreath` | Перевести дух | Ironclad | 0 |  |  | Skill | Common | Self |  |  | GainVar 3(+2) |  | Получить {StaminaGain:diff()} ◆. |
| `RaiseShield` | Поднять щит | Ironclad | 1 |  |  | Skill | Common | Self |  | 7(+3) | BonusVar 3(+1) |  | Получить {Block:diff()} Блока. Если у вас есть Латы — ещё {PlatingBonus:diff()}. |
| `SteadyStance` | Твёрдая стойка | Ironclad | 1 |  |  | Skill | Common | Self |  | 5(+3) | CatchBreath.GainVar 2 |  | Получить {Block:diff()} Блока. Получить {StaminaGain:diff()} ◆. |
| `SmotherFlames` | Сбить пламя | Ironclad | 0 |  |  | Skill | Common | Self |  | 3(+3) |  |  | Снять с себя Горение и Озноб. Получить {Block:diff()} Блока. |
| `BattleCry` | Боевой клич | Ironclad | 1 |  |  | Skill | Uncommon | AllEnemies |  |  | WeakPower 1(+1), StaggerPower 3 |  | Наложить {WeakPower:diff()} Слабости и {StaggerPower:diff()} Шаткости ВСЕМ врагам. Клич: вернётся в руку через 4 хода. |
| `ChangeGrip` | Сменить хват | Ironclad | 0 |  |  | Skill | Uncommon | Self |  |  | GripPower 3(+2) |  | Следующая атака в этом ходу не тратит ◆ и наносит на {GripPower:diff()} урона больше. |
| `BladeSpirit` | Дух клинка | Ironclad | 1(-1) |  |  | Skill | Uncommon | Self |  |  |  |  | Призвать или отпустить дух клинка. Пока он призван, ваши атаки тратят 1 ✦ и наносят дополнительный урон магией — половину ваших ✦. |

## Безмолвная (20)

| Класс | Название | Пул | ⚡ | ◆ | ✦ | Тип | Редкость | Цель | Урон | Блок | Прочее | Ключевые | Текст |
|---|---|---|---|---|---|---|---|---|---|---|---|---|---|
| `Backstab` | Удар в спину | Silent | 1 | 1 |  | Attack | Common | AnyEnemy | 6(+3) |  | SneakVar 6(+2) |  | Нанести {Damage:diff()} урона. Подлый: ещё {Sneak:diff()}. |
| `PoisonedBlade` | Отравленный клинок | Silent | 1 | 1 |  | Attack | Common | AnyEnemy | 5(+2) |  |  |  | Нанести {Damage:diff()} урона. Если на оружии яд — его эффект ещё дважды. |
| `Garrote` | Удавка | Silent | 1 | 1 |  | Attack | Common | AnyEnemy | 4(+2) |  | BleedPower 3(+1), WeakPower 1 |  | Нанести {Damage:diff()} урона. Наложить {BleedPower:diff()} Кровотечения. Подлый: наложить {WeakPower:diff()} Слабости. |
| `RottingCut` | Гнилостный надрез | Silent | 1 | 1 |  | Attack | Common | AnyEnemy | 5(+2) |  | BlightPower 1(+1) |  | Нанести {Damage:diff()} урона. Наложить {BlightPower:diff()} Хвори. |
| `PickPocket` | Обчистить | Silent | 1 | 1 |  | Attack | Uncommon | AnyEnemy | 4(+2) |  | GoldVar 12(+6) | Exhaust | Нанести {Damage:diff()} урона. Получить {Gold:diff()} золота. Подлый: ещё украсть случайное зелье I. Изгнание. |
| `Ambush` | Засада | Silent | 2 | 2 |  | Attack | Uncommon | AllEnemies | 7(+3) |  | BleedPower 2(+1) |  | Нанести {Damage:diff()} урона ВСЕМ врагам. Враги без атакующего намерения получают ещё {BleedPower:diff()} Кровотечения. |
| `ShadowStrike` | Удар из тени | Silent | 2 | 2 |  | Attack | Rare | AnyEnemy | 12(+4) |  |  |  | Нанести {Damage:diff()} урона. Если вы Невидимы — дважды. |
| `Sidestep` | Шаг в сторону | Silent | 1 |  |  | Skill | Common | Self |  | 5(+3) | DodgePower 1 |  | Получить {Block:diff()} Блока. +{DodgePower:diff()} заряд Уворота до следующего хода. |
| `ShadowStep` | Шаг тени | Silent | 1(-1) |  |  | Skill | Uncommon | Self |  |  | InvisibilityPower 1 |  | Получить {InvisibilityPower:diff()} Невидимости. |
| `FlaskToFace` | Флакон в лицо | Silent | 1(-1) |  |  | Skill | Common | AnyEnemy |  |  | Multiplier 2 |  | Случайный яд из ваших слотов действует на врага с силой ×{Multiplier}. Флакон не тратится. Нет ядов — яд с оружия; нет и его — Яд 4. |
| `Distillation` | Перегонка | Silent | 1(-1) |  |  | Skill | Uncommon | Self |  |  |  | Exhaust | Сварить случайное зелье ступени I. Изгнание. |
| `ToxicCoating` | Ядовитая смазка | Silent | 0 |  |  | Skill | Common | Self |  |  | ChargesVar 2(+1) | Exhaust | Нанести на оружие Токсин I ({Charges:diff()} заряда). Изгнание. |
| `VolatileMixture` | Летучий состав | Silent | 2(-1) |  |  | Skill | Rare | AllEnemies |  |  |  |  | Яд и Хворь на ВСЕХ врагах детонируют: урон = сумма их стаков, после чего они снимаются. |
| `Riposte` | Контрудар | Silent | 1 |  |  | Power | Uncommon | Self |  |  | RipostePower 4(+2) |  | Каждый раз, когда вы Увернулись, атакующий получает {RipostePower:diff()} урона, а ваша следующая атака — Подлая. |
| `Evasion` | Уклонение | Silent | 2(-1) |  |  | Power | Rare | Self |  |  | DodgePower 1 |  | +{DodgePower:diff()} заряд Уворота каждый ход. |
| `VenomMastery` | Мастер ядов | Silent | 1 |  |  | Power | Rare | Self |  |  | VenomMasteryPower 1(+1) |  | В начале хода яд на оружии получает +{VenomMasteryPower:diff()} заряд. |
| `ThirdStep` | Третий шаг | Silent | 1 | 1 |  | Attack | Common | AnyEnemy | 5(+2) |  |  |  | Оружейная. Нанести {Damage:diff()} урона. Если в этом ходу сыграно 2+ карты — нанести ещё раз. |
| `WindRhythm` | Ритм ветра | Silent | 1(-1) |  |  | Power | Uncommon | Self |  |  | WindRhythmPower 1 |  | Каждая 3-я карта за ход: взять {WindRhythmPower:diff()} карту. |
| `SlipAway` | Ускользнуть | Silent | 0 |  |  | Skill | Common | Self |  | 3(+2) |  |  | Получить {Block:diff()} Блока. Взять 1 карту. |
| `FleetingStrike` | Мимолётный удар | Silent | 0 | 1 |  | Attack | Uncommon | AnyEnemy | 3(+1) |  | PerZeroVar 2(+1) |  | Нанести {Damage:diff()} урона и ещё {PerZero:diff()} за каждую 0-энергетическую карту, сыгранную раньше в этом ходу (до 3). |

## Регент (12)

| Класс | Название | Пул | ⚡ | ◆ | ✦ | Тип | Редкость | Цель | Урон | Блок | Прочее | Ключевые | Текст |
|---|---|---|---|---|---|---|---|---|---|---|---|---|---|
| `StaffStrike` | Удар посохом | Regent | 1 | 1 |  | Attack | Common | AnyEnemy | 6(+3) |  | SpellFocusPower 1 |  | Нанести {Damage:diff()} урона. Следующее заклинание в этот ход на 1 ✦ дешевле. |
| `RuneOfWarding` | Руна защиты | Regent | 1 |  |  | Skill | Common | Self |  | 5(+2) | SpellShieldPower 3(+2) |  | Получить {Block:diff()} Блока. Получить {SpellShieldPower:diff()} Оберега. |
| `ArcaneFocus` | Сосредоточение | Regent | 0 |  |  | Skill | Common | Self |  |  | ManaVar 1(+1), SpellFocusPower 1 |  | Получить {Mana:diff()} ✦. Следующее заклинание в этот ход на 1 ✦ дешевле. |
| `ManaBurn` | Выжигание магии | Regent | 1 |  | 1 | Attack | Common | AnyEnemy | 6(+2) |  | StaticPower 3(+1) |  | Нанести {Damage:diff()} урона разрядом. Наложить {StaticPower:diff()} Статики. |
| `GrimoireStudy` | Изучение гримуара | Regent | 1 |  |  | Skill | Common | Self |  |  | Cards 2(+1), ManaVar 1 |  | Взять {Cards:diff()} карты. +{Mana} ✦ за каждое взятое заклинание. |
| `Concentration` | Концентрация | Regent | 1 |  |  | Power | Uncommon | Self |  |  | ConcentrationPower 3(+1) |  | Каждое ваше заклинание даёт {ConcentrationPower:diff()} Блока. |
| `MagickaWell` | Родник магии | Regent | 1(-1) |  |  | Power | Uncommon | Self |  |  | InsightPower 1 |  | Прирост ✦ +{InsightPower} до конца боя. |
| `SoulRend` | Удар по душе | Regent | 1 |  | 1 | Attack | Uncommon | AnyEnemy | 8(+3) |  |  |  | Нанести {Damage:diff()} урона магией. Если у врага Хворь — дважды. |
| `ManaSurge` | Прилив магии | Regent | 0 |  |  | Skill | Uncommon | Self |  |  | ManaVar 3(+1) | Exhaust | Получить {Mana:diff()} ✦. Изгнание. |
| `DualCasting` | Двойное заклинание | Regent | 1(-1) |  |  | Skill | Rare | Self |  |  | DualCastPower 1 |  | Следующее заклинание в этот ход срабатывает дважды. |
| `ArcaneMastery` | Мастерство заклинателя | Regent | 2(-1) |  |  | Power | Rare | Self |  |  | ArcaneMasteryPower 1 |  | Первое заклинание каждого хода на 1 ✦ дешевле. |
| `ManaStorm` | Магическая буря | Regent | 2 |  | X | Attack | Rare | AllEnemies | 4(+1) |  |  |  | Потратить все ✦. За каждый — {Damage:diff()} урона магией ВСЕМ врагам. |

## Кличи (5)

| Класс | Название | Пул | ⚡ | ◆ | ✦ | Тип | Редкость | Цель | Урон | Блок | Прочее | Ключевые | Текст |
|---|---|---|---|---|---|---|---|---|---|---|---|---|---|
| `UnrelentingForce` | Безжалостная сила | Ironclad | 1 |  |  | Attack | Uncommon | AnyEnemy | 6(+3) |  | StaggerPower 4 |  | Нанести {Damage:diff()} урона. Наложить {StaggerPower:diff()} Шаткости. Клич: вернётся в руку через 3 хода. |
| `FireBreath` | Огненный выдох | Ironclad | 1 |  |  | Attack | Uncommon | AllEnemies | 4(+2) |  | BurningPower 4(+1) |  | Нанести {Damage:diff()} урона огнём ВСЕМ врагам. Наложить {BurningPower:diff()} Горения ВСЕМ врагам. Клич: вернётся в руку через 3 хода. |
| `FrostBreath` | Морозный выдох | Ironclad | 1 |  |  | Skill | Uncommon | AllEnemies |  |  | ChillPower 4(+2) |  | Наложить {ChillPower:diff()} Озноба ВСЕМ врагам, уже охлаждённым — вдвое. Клич: вернётся в руку через 3 хода. |
| `StormCall` | Зов грозы | Ironclad | 2 |  |  | Attack | Rare | AllEnemies | 5(+3) |  | StaggerPower 2 |  | Нанести {Damage:diff()} урона разрядом ВСЕМ врагам. Шок: {StaggerPower:diff()} Шаткости ВСЕМ врагам. Клич: вернётся в руку через 4 хода. |
| `SoulTear` | Разрыв души | Ironclad | 2 |  |  | Attack | Rare | AnyEnemy | 10(+4) |  |  |  | Нанести {Damage:diff()} урона. Если у цели осталось меньше 25 % ♥ — она погибает (не боссы). Клич: вернётся в руку через 5 ходов. |

## Заклинания (42)

| Класс | Название | Пул | ⚡ | ◆ | ✦ | Тип | Редкость | Цель | Урон | Блок | Прочее | Ключевые | Текст |
|---|---|---|---|---|---|---|---|---|---|---|---|---|---|
| `Oakflesh` | Дубовая плоть | Spell | 1(-1) |  | 1 | Skill | Alteration/Novice | Self |  |  | TemporaryPlatingPower 1 |  | Получить {TemporaryPlatingPower:diff()} Лат на 3 хода. |
| `Telekinesis` | Импульс | Spell | 1 |  | 1 | Attack | Alteration/Novice | AnyEnemy | 5(+3) |  | SunderPower 1(+1) |  | Нанести {Damage:diff()} урона магией. Наложить {SunderPower:diff()} Раскола. |
| `Stoneflesh` | Каменная плоть | Spell | 1 |  | 3 | Skill | Alteration/Adept | Self |  |  | TemporaryPlatingPower 2(+1) |  | Получить {TemporaryPlatingPower:diff()} Лат на 3 хода. |
| `Paralyze` | Паралич | Spell | 2(-1) |  | 5 | Skill | Alteration/Expert | AnyEnemy |  |  |  |  | Враг с ♥ не выше 120 пропускает намерение. Боссы и Непоколебимые вместо этого получают 5 Шаткости. |
| `SpellAbsorption` | Поглощение | Spell | 1 |  | 3 | Skill | Alteration/Expert | Self |  |  | SpellAbsorptionPower 2(+1) |  | Получить {SpellAbsorptionPower:diff()} Поглощения заклинаний до вашего следующего хода. |
| `Dragonhide` | Драконья кожа | Spell | 2 |  | 6 | Skill | Alteration/Master | Self |  |  | TemporaryPlatingPower 3(+1), SpellAbsorptionPower 1(+1) |  | Получить {TemporaryPlatingPower:diff()} Лат на 3 хода и {SpellAbsorptionPower:diff()} Поглощения заклинаний. |
| `MassParalysis` | Массовый паралич | Spell | 3(-1) |  | 8 | Skill | Alteration/Master | AllEnemies |  |  |  | Exhaust | ВСЕ враги, кроме боссов, пропускают намерение. Боссы получают 5 Шаткости. |
| `SpectralBlade` | Призрачный клинок | Spell | 0 | 1 |  | Attack | Token | AnyEnemy | 6(+3) |  |  | Exhaust | Оружейная атака. Нанести {Damage:diff()} урона. |
| `BoundBlade` | Призванный клинок | Spell | 0 |  | 1 | Skill | Conjuration/Novice | Self |  |  |  |  | Добавить в руку Призрачный клинок. |
| `ConjureSpiritWolf` | Призыв волка-духа | Spell | 1 |  | 2 | Skill | Conjuration/Novice | Self |  |  | SummonHp 8(+4) |  | Призвать Волка-духа ({SummonHp:diff()} ♥). Бьёт на 4 в конце вашего хода. |
| `ConjureFlameAtronach` | Огненный атронах | Spell | 2 |  | 3 | Skill | Conjuration/Adept | Self |  |  | SummonHp 14(+4) |  | Призвать Огненного атронаха ({SummonHp:diff()} ♥; каждый ход 5 урона огнём и 2 Горения). Получить 2 Плаща огня. |
| `ConjureFrostAtronach` | Ледяной атронах | Spell | 2 |  | 3 | Skill | Conjuration/Adept | Self |  |  | SummonHp 22(+6) |  | Призвать Ледяного атронаха ({SummonHp:diff()} ♥). Атакующие его получают 2 Озноба. |
| `ConjureStormAtronach` | Грозовой атронах | Spell | 2 |  | 4 | Skill | Conjuration/Expert | Self |  |  | SummonHp 16(+4) |  | Призвать Грозового атронаха ({SummonHp:diff()} ♥). Каждый ход 6 урона разрядом ВСЕМ врагам. |
| `RaiseDead` | Поднять мертвеца | Spell | 1 |  | 2 | Skill | Conjuration/Apprentice | Self |  |  | PercentVar 50(+15) |  | Поднять последнего павшего в этом бою врага (не босса) нежитью-призывом с {Percent:diff()} % его макс. ♥. Бьёт на 4 в конце вашего хода. |
| `ConjureAbyssGuardian` | Страж Бездны | Spell | 3 |  | 6 | Skill | Conjuration/Master | Self |  |  | SummonHp 30(+8) |  | Призвать Дремлющего стража ({SummonHp:diff()} ♥). Первый ход дремлет, затем каждый ход бьёт на 10 магией. |
| `CallSpiritKnight` | Зов Рыцаря-Духа | Spell | 3 |  | 6 | Skill | Conjuration/Master | Self |  |  | SummonHp 25(+7) |  | Призвать Призрачного рыцаря ({SummonHp:diff()} ♥). Каждый ход бьёт на 5; пока он стоит, ваши атаки наносят на 2 урона больше. |
| `Banish` | Изгнание | Spell | 1 |  | 3 | Attack | Conjuration/Expert | AnyEnemy | 10(+4) |  |  |  | Уничтожить вражеского прислужника. Иначе — {Damage:diff()} урона магией. |
| `Sparks` | Искры | Spell | 0 |  | 1 | Attack | Destruction/Novice | AnyEnemy | 3(+1) |  | StaticPower 1 | Retain | Удержание. Нанести {Damage:diff()} урона разрядом. Наложить {StaticPower:diff()} Статики. Пока в руке, +1 урона за каждый ход. |
| `Firebolt` | Огненная стрела | Spell | 1 |  | 1 | Attack | Destruction/Novice | AnyEnemy | 6(+3) |  | BurningPower 3(+1) |  | Нанести {Damage:diff()} урона огнём. Наложить {BurningPower:diff()} Горения. |
| `FireRune` | Огненная руна | Spell | 1 |  | 2 | Skill | Destruction/Apprentice | Self |  |  | FireRunePower 10(+4) |  | Следующий враг, атаковавший вас, получает {FireRunePower:diff()} урона огнём и вдвое меньше Горения. |
| `FlameCloak` | Плащ пламени | Spell | 1 |  | 2 | Skill | Destruction/Apprentice | Self |  |  | FireCloakPower 3(+2) |  | Получить {FireCloakPower:diff()} Плаща огня на 3 хода. |
| `Fireball` | Огненный шар | Spell | 2 |  | 3 | Attack | Destruction/Adept | AllEnemies | 8(+3) |  | BurningPower 4(+1) |  | Нанести {Damage:diff()} урона огнём ВСЕМ врагам. Наложить {BurningPower:diff()} Горения ВСЕМ врагам. |
| `IceSpike` | Ледяное копьё | Spell | 1 |  | 3 | Attack | Destruction/Adept | AnyEnemy | 12(+4) |  |  |  | Нанести {Damage:diff()} урона холодом. Вдвое больше по Обледеневшему врагу. |
| `BallLightning` | Шаровая молния | Spell | 2 |  | 3 | Attack | Destruction/Adept | AnyEnemy | 10(+3) |  |  |  | Нанести {Damage:diff()} урона разрядом. Перескакивает ещё на 2 врагов, −3 урона за перескок. |
| `WallOfFlames` | Стена пламени | Spell | 2 |  | 4 | Skill | Destruction/Expert | AllEnemies |  |  | FlameWallPower 2(+1) |  | В начале ваших следующих {FlameWallPower:diff()} ходов накладывает 6 Горения ВСЕМ врагам. |
| `Blizzard` | Буран | Spell | 2 |  | 4 | Attack | Destruction/Expert | AllEnemies | 7(+3) |  | ChillPower 3(+1) |  | Нанести {Damage:diff()} урона холодом ВСЕМ врагам. Наложить {ChillPower:diff()} Озноба ВСЕМ врагам. |
| `ChainLightning` | Цепь молний | Spell | 3 |  | 6 | Attack | Destruction/Master | RandomEnemy | 12(+4) |  | Hits 3 |  | Выжечь всю ✦ вражеского мага. Нанести {Damage:diff()} урона разрядом случайному врагу {Hits} раза. |
| `Meteor` | Метеор | Spell | 3 |  | 7 | Attack | Destruction/Master | AnyEnemy | 30(+8) |  | SplashVar 10(+3), BurningPower 10(+2) |  | Нанести {Damage:diff()} урона огнём, остальным врагам — {Splash:diff()}. Наложить {BurningPower:diff()} Горения ВСЕМ врагам. |
| `IceTomb` | Ледяная гробница | Spell | 3 |  | 6 | Attack | Destruction/Master | AnyEnemy | 20(+6) |  |  |  | Нанести {Damage:diff()} урона холодом. Враг Обледеневает и пропускает намерение (боссы: 5 Шаткости). |
| `MuffledSteps` | Приглушённые шаги | Spell | 0 |  | 1 | Skill | Illusion/Novice | Self |  | 3(+3) |  |  | Получить {Block:diff()} Блока. Следующая атака — Подлая. |
| `Confusion` | Смятение | Spell | 1 |  | 2 | Skill | Illusion/Apprentice | AnyEnemy |  |  | ConfusionPower 1(+1) |  | Враг с ♥ ≤ 40 в Смятении {ConfusionPower:diff()} ход: его атаки бьют случайного другого врага. |
| `Terror` | Страх | Spell | 1 |  | 3 | Skill | Illusion/Adept | AnyEnemy |  |  | FearPower 2(+1) |  | Враг с ♥ ≤ 70 в Страхе {FearPower:diff()} хода: наносит вдвое меньше урона. |
| `Invisibility` | Невидимость | Spell | 1 |  | 4 | Skill | Illusion/Expert | Self |  |  | InvisibilityPower 2(+1) |  | Получить {InvisibilityPower:diff()} Невидимости. |
| `Madness` | Безумие | Spell | 3(-1) |  | 6 | Skill | Illusion/Master | AllEnemies |  |  | ConfusionPower 2(+1) |  | Два случайных врага (не боссы) в Смятении {ConfusionPower:diff()} хода — бьют друг друга. |
| `HealingLight` | Исцеляющий свет | Spell | 1 |  | 2 | Skill | Restoration/Novice | Self |  |  | Heal 5(+2) |  | Восстановить {Heal:diff()} ♥. Изгоняется на 3-м применении за бой. |
| `LesserWard` | Малый оберег | Spell | 0 |  | 1 | Skill | Restoration/Novice | Self |  |  | SpellShieldPower 6(+3) |  | Получить {SpellShieldPower:diff()} Оберега. |
| `Sunbeam` | Солнечный луч | Spell | 1 |  | 2 | Attack | Restoration/Apprentice | AnyEnemy | 7(+3) |  |  |  | Нанести {Damage:diff()} урона светом (вдвое по нежити, духам и Бездне). |
| `PoisonSpray` | Ядовитый спрей | Spell | 1 |  | 1 | Skill | Restoration/Apprentice | AnyEnemy |  |  | VanillaPoison 4(+2) |  | Наложить {PoisonPower:diff()} Яда. |
| `TurnUndead` | Изгнание нежити | Spell | 2 |  | 3 | Attack | Restoration/Adept | AllEnemies | 8(+3) |  | ThresholdVar 15(+5) |  | Нежить с ♥ не выше {Threshold:diff()} уничтожается. Остальным врагам — {Damage:diff()} урона светом; уцелевшая нежить пропускает намерение. |
| `Pestilence` | Мор | Spell | 2 |  | 4 | Skill | Restoration/Expert | AllEnemies |  |  | VanillaPoison 5(+2), BlightPower 2(+1) |  | Наложить {PoisonPower:diff()} Яда и {BlightPower:diff()} Хвори ВСЕМ врагам. |
| `Sunburst` | Солнечная вспышка | Spell | 2 |  | 4 | Attack | Restoration/Expert | AllEnemies | 12(+4) |  |  |  | Нанести {Damage:diff()} урона светом ВСЕМ врагам (вдвое по нежити). |
| `GreaterWard` | Великий оберег | Spell | 2 |  | 6 | Skill | Restoration/Master | Self |  |  | SpellShieldPower 30(+10) |  | Получить {SpellShieldPower:diff()} Оберега. До конца боя вас нельзя Парализовать. |

## Формы и Пути (59)

| Класс | Название | Пул | ⚡ | ◆ | ✦ | Тип | Редкость | Цель | Урон | Блок | Прочее | Ключевые | Текст |
|---|---|---|---|---|---|---|---|---|---|---|---|---|---|
| `Windup` | Подзавод | Token | 1(-1) |  |  | Skill | Event | Self |  |  | Max 3 |  | Добавить сферу стихии (Пар → Искра → Иней, до {Max}). |
| `HeraldStanceCard` | Стойка Вестника | Token | 0 |  |  | Skill | Event | Self |  |  |  |  | Сменить Стойку: Чума → Раздор → Распад. |
| `HolyStrike` | Святой удар | Path | 1 | 2 |  | Attack | Uncommon | AnyEnemy | 9(+3) |  |  |  | Нанести {Damage:diff()} урона светом. |
| `AegisOfFaith` | Эгида веры | Path | 1 |  |  | Skill | Uncommon | Self |  | 8(+3) | SpellShieldPower 4(+2) |  | Получить {Block:diff()} Блока и {SpellShieldPower:diff()} Оберега. |
| `Consecrate` | Освящение | Path | 2 |  | 1 | Attack | Rare | AllEnemies | 6(+3) |  | StaggerPower 4 |  | Нанести {Damage:diff()} урона светом ВСЕМ врагам. Нежити и Бездне — {StaggerPower:diff()} Шаткости. |
| `DreadStrike` | Удар ужаса | Path | 1 | 2 |  | Attack | Uncommon | AnyEnemy | 9(+3) |  | VanillaWeak 1(+1) |  | Нанести {Damage:diff()} урона. Наложить {WeakPower:diff()} Слабости. |
| `SoulDrain` | Высасывание | Path | 1 | 1 |  | Attack | Uncommon | AnyEnemy | 6(+3) |  |  |  | Нанести {Damage:diff()} урона и вылечить нанесённое. |
| `DarkAura` | Тёмная аура | Path | 1 |  |  | Skill | Rare | AllEnemies |  |  | Strength 1(+1) | Exhaust | ВСЕ враги теряют {Strength:diff()} Силы. Изгнание. |
| `Shieldbreaker` | Сокрушитель щитов | Path | 2 | 3 |  | Attack | Rare | AnyEnemy | 16(+5) |  | SunderPower 2 |  | Нанести {Damage:diff()} урона. Наложить {SunderPower:diff()} Раскола. |
| `RallyCry` | Боевой клич | Path | 1 |  |  | Skill | Uncommon | Self |  | 10(+4) |  |  | Получить {Block:diff()} Блока. |
| `Overpower` | Натиск | Path | 1 | 2 |  | Attack | Uncommon | AnyEnemy | 8(+3) |  | StaggerPower 4(+2) |  | Нанести {Damage:diff()} урона. Наложить {StaggerPower:diff()} Шаткости. |
| `Rampage` | Буйство | Path | 1 | 2 |  | Attack | Uncommon | AnyEnemy | 5(+2) |  |  |  | Дважды нанести {Damage:diff()} урона. |
| `BloodFury` | Кровавое неистовство | Path | 0 |  |  | Skill | Rare | Self |  |  | HpLoss 4, VanillaStrength 2(+1) | Exhaust | Потерять {HpLoss} ♥. Получить {StrengthPower:diff()} Силы. Изгнание. |
| `Whirlwind` | Размах | Path | 1 | 2 |  | Attack | Uncommon | AllEnemies | 8(+3) |  |  |  | Нанести {Damage:diff()} урона ВСЕМ врагам. |
| `PalmStrike` | Удар ладонью | Path | 0 | 1 |  | Attack | Uncommon | AnyEnemy | 5(+2) |  | StaggerPower 2(+1) |  | Нанести {Damage:diff()} урона. Наложить {StaggerPower:diff()} Шаткости. |
| `FlurryOfBlows` | Град ударов | Path | 1 | 2 |  | Attack | Rare | AnyEnemy | 3(+1) |  |  |  | 4 раза нанести {Damage:diff()} урона. |
| `InnerCalm` | Внутренний покой | Path | 1 |  |  | Skill | Uncommon | Self |  | 7(+3) | DodgePower 1 |  | Получить {Block:diff()} Блока и {DodgePower:diff()} Уворота. |
| `PathGarrote` | Удавка ассасина | Path | 1 | 1 |  | Attack | Uncommon | AnyEnemy | 7(+2) |  | BleedPower 3(+1) |  | Нанести {Damage:diff()} урона. Наложить {BleedPower:diff()} Кровотечения. |
| `Vanish` | Исчезновение | Path | 1 |  |  | Skill | Uncommon | Self |  |  | DodgePower 2(+1) | Exhaust | Получить {DodgePower:diff()} Уворота. Изгнание. |
| `DeathMark` | Метка смерти | Path | 1 |  |  | Skill | Rare | AnyEnemy |  |  | VanillaVulnerable 2(+1) |  | Наложить {VulnerablePower:diff()} Уязвимости и Сломлен. |
| `PlagueTouch` | Касание чумы | Path | 1 | 1 |  | Attack | Uncommon | AnyEnemy | 5(+2) |  | VanillaPoison 4(+2) |  | Нанести {Damage:diff()} урона. Наложить {PoisonPower:diff()} Яда и 1 Хвори. |
| `Miasma` | Миазмы | Path | 2 |  |  | Skill | Uncommon | AllEnemies |  |  | VanillaPoison 5(+2) |  | Наложить {PoisonPower:diff()} Яда ВСЕМ врагам. |
| `Wither` | Иссушение | Path | 1(-1) |  |  | Skill | Rare | AnyEnemy |  |  |  | Exhaust | Удвоить Яд на цели. Изгнание. |
| `ArcaneShot` | Чародейский выстрел | Path | 1 | 1 |  | Attack | Uncommon | AnyEnemy | 8(+3) |  |  |  | Нанести {Damage:diff()} урона магией. |
| `ElementalVolley` | Стихийный залп | Path | 2 | 2 |  | Attack | Rare | AllEnemies | 6(+3) |  | BurningPower 2(+1) |  | Нанести {Damage:diff()} урона огнём ВСЕМ врагам. Наложить {BurningPower:diff()} Горения ВСЕМ. |
| `FocusedAim` | Прицел | Path | 0 |  |  | Skill | Uncommon | Self |  |  | Mana 2(+1) | Exhaust | Получить {Mana:diff()} ✦. Изгнание. |
| `SpellBlade` | Клинок-заклинание | Path | 1 | 1 | 1 | Attack | Uncommon | AnyEnemy | 7(+2) |  | Fire 5(+2) |  | Нанести {Damage:diff()} урона, затем {Fire:diff()} урона огнём. |
| `RuneArmor` | Рунная броня | Path | 1 |  | 1 | Skill | Uncommon | Self |  | 8(+3) | FrostCloakPower 2(+1) |  | Получить {Block:diff()} Блока и {FrostCloakPower:diff()} Плаща холода. |
| `ArcaneCharge` | Чародейский рывок | Path | 2 | 2 |  | Attack | Rare | AnyEnemy | 13(+4) |  |  |  | Нанести {Damage:diff()} урона разрядом. |
| `MirrorWard` | Зеркальный оберег | Path | 1 |  | 1 | Skill | Uncommon | Self |  |  | SpellShieldPower 12(+4) |  | Получить {SpellShieldPower:diff()} Оберега. |
| `Rebound` | Отдача | Path | 1 |  |  | Skill | Uncommon | Self |  | 6(+3) | SpellAbsorptionPower 1 |  | Получить {Block:diff()} Блока и {SpellAbsorptionPower:diff()} Поглощения заклинаний. |
| `PrismBolt` | Призменная стрела | Path | 1 |  | 2 | Attack | Rare | AnyEnemy | 6(+3) |  |  |  | Нанести {Damage:diff()} урона магией плюс ваш Оберег. |
| `SpiritCall` | Зов духов | Path | 1 |  | 2 | Skill | Uncommon | Self |  |  | Hp 10(+4) |  | Призвать волка-духа с {Hp:diff()} ♥. |
| `Seance` | Сеанс | Path | 1 |  |  | Skill | Uncommon | Self |  | 6(+3) | Mana 2 |  | Получить {Block:diff()} Блока и {Mana} ✦. |
| `EtherealLink` | Эфирная связь | Path | 1 |  |  | Skill | Rare | Self |  |  | Hp 6(+3) |  | Ваши призывы получают {Hp:diff()} ♥. |
| `PickPathPaladin` | Паладин | Token |  |  |  |  |  |  |  |  |  |  | Путь: Паладин. Латы +1; нежить бьёт вас на 1 слабее за удар; Кара без ✦. |
| `PickPathBlackKnight` | Чёрный рыцарь | Token |  |  |  |  |  |  |  |  |  |  | Путь: Чёрный рыцарь. Аура страха: враги теряют 1 Силы в начале каждого 2-го хода. Ваши атаки лечат 1 за удар. |
| `PickPathChampion` | Чемпион | Token |  |  |  |  |  |  |  |  |  |  | Путь: Чемпион. Тяжёлые атаки (от 2 ◆) накладывают 2 Шаткости (Непоколебимым — половину). |
| `PickPathBarbarian` | Варвар | Token |  |  |  |  |  |  |  |  |  |  | Путь: Варвар. Ярость: при ♥ ниже 50 % атаки +50 % урона и не тратят ◆. |
| `PickPathMonk` | Монах | Token |  |  |  |  |  |  |  |  |  |  | Путь: Монах. Без оружия: «оружейные» атаки бьют как 4 + уровень/2; каждая 3-я — 3 Шаткости. |
| `PickPathAssassin` | Ассасин | Token |  |  |  |  |  |  |  |  |  |  | Путь: Ассасин. Шаг тени: первая атака каждого хода — «в спину», урон ×2. |
| `PickPathBlightbringer` | Губитель | Token |  |  |  |  |  |  |  |  |  |  | Путь: Губитель. Ваш Яд +1 стак; Яд на врагах с Хворью не убывает. |
| `PickPathMysticArcher` | Мистический лучник | Token |  |  |  |  |  |  |  |  |  |  | Путь: Мистический лучник. Выстрелы из лука и арбалета накладывают стихию последнего заклинания (2 стака). |
| `PickPathElementLord` | Повелитель стихии | Token |  |  |  |  |  |  |  |  |  |  | Путь: Повелитель стихии. Выбор стихии: её подписное заклинание — в колоду; ваши статусы этой стихии +1 стак. |
| `PickPathBattlemage` | Боевой маг | Token |  |  |  |  |  |  |  |  |  |  | Путь: Боевой маг. Любой Плащ стихии накидывает и два других; ваши атаки добавляют 2 урона стихией (огонь → холод → разряд). |
| `PickPathReflector` | Отражатель | Token |  |  |  |  |  |  |  |  |  |  | Путь: Отражатель. Оберег отражает 50 % поглощённого урона заклинаний в заклинателя. |
| `PickPathMedium` | Медиум | Token |  |  |  |  |  |  |  |  |  |  | Путь: Медиум. +1 слот призыва; призывы +2 урона. |
| `PickPathReborn` | Перерождённый | Token |  |  |  |  |  |  |  |  |  |  | Путь: Перерождённый. Без бонуса. Единожды: новая предыстория и сброс очков характеристик (распределить заново в лагере). |
| `Smite` | Кара | Token | 1 |  | 1 | Attack | Event | AnyEnemy | 8(+3) |  | StaggerPower 5 |  | Нанести {Damage:diff()} урона светом. Нежити и Бездне — ещё {StaggerPower:diff()} Шаткости. |
| `Bite` | Укус | Token | 0 |  |  | Attack | Event | AnyEnemy | 6(+3) |  |  | Exhaust | Нанести {Damage:diff()} урона и вылечить нанесённое. Кормление: Жажда падает до 0. Изгнание. |
| `BloodLordStance` | Кровавый лорд | Token | 0 |  |  | Skill | Event | Self |  |  |  |  | Войти в форму Кровавого лорда или выйти из неё: атаки накладывают 1 Хвори и лечат 25 % непогашенного урона. |
| `MoonCall` | Зов луны | Token | 2 |  |  | Skill | Event | Self |  |  | Turns 3 | Exhaust | Обращение в зверя на {Turns:diff()} хода. Изгнание. |
| `Claws` | Когти | Token | 1 | 1 |  | Attack | Token | AnyEnemy | 6 |  |  |  | Нанести {Damage:diff()} урона. Эфирная. Изгнание. |
| `Pounce` | Прыжок | Token | 1 | 2 |  | Attack | Token | AnyEnemy | 9 | 5 |  |  | Нанести {Damage:diff()} урона. Получить {Block:diff()} Блока. Эфирная. Изгнание. |
| `Roar` | Рык | Token | 1 |  |  | Skill | Token | AllEnemies |  |  | WeakPower 1, StaggerPower 3 |  | Наложить {WeakPower:diff()} Слабости и {StaggerPower:diff()} Шаткости ВСЕМ врагам. Эфирная. Изгнание. |
| `Devour` | Пожрать | Token | 1 |  |  | Skill | Token | Self |  |  | Heal 6 |  | Съесть павшего врага: +{Heal} ♥ и +1 ход формы зверя. Эфирная. Изгнание. |
| `PickBeastHunter` | Клык | Token |  |  |  |  |  |  |  |  |  |  | Узел {Value}: I +2 урона зверя, II звериные атаки — 2 Кровотечения, III ещё +2 урона. |
| `PickBeastStalker` | Долгая ночь | Token |  |  |  |  |  |  |  |  |  |  | Узел {Value}: I +1 ход формы, II ход за 2 удара вместо 3, III ещё +1 ход. |
| `PickBeastAlpha` | Альфа | Token |  |  |  |  |  |  |  |  |  |  | Узел {Value}: при Обращении — волк (8/12/16 ♥); III +1 слот призыва. |

## Зачарование карт (6)

| Класс | Название | Пул | ⚡ | ◆ | ✦ | Тип | Редкость | Цель | Урон | Блок | Прочее | Ключевые | Текст |
|---|---|---|---|---|---|---|---|---|---|---|---|---|---|
| `PickCardEnchantFlaming` | Пламенная | Token |  |  |  |  |  |  |  |  |  |  | Атака: ещё 3 Горения. |
| `PickCardEnchantFrost` | Морозная | Token |  |  |  |  |  |  |  |  |  |  | Атака: ещё 2 Озноба. |
| `PickCardEnchantStorm` | Грозовая | Token |  |  |  |  |  |  |  |  |  |  | Атака: выжигает 2 ✦ цели. |
| `PickCardEnchantLightened` | Облегчённая | Token |  |  |  |  |  |  |  |  |  |  | Любая карта: вес −1 (мин. 0). |
| `PickCardEnchantBreathing` | Дыхательная | Token |  |  |  |  |  |  |  |  |  |  | Атака: на 1 ◆ дешевле. |
| `PickCardEnchantDouble` | Двойная | Token |  |  |  |  |  |  |  |  |  |  | Две чары на одной карте (узел Зачарование). |

## Камень Памяти (4)

| Класс | Название | Пул | ⚡ | ◆ | ✦ | Тип | Редкость | Цель | Урон | Блок | Прочее | Ключевые | Текст |
|---|---|---|---|---|---|---|---|---|---|---|---|---|---|
| `DeathEcho` | Предсмертное эхо | Token | -1 |  |  | Curse | Curse | None |  |  |  | Unplayable | Неиграбельна. Весит 2. |
| `ScarOfFlesh` | Шрам плоти | Token |  |  |  |  |  |  |  |  |  | Unplayable | −6% макс. ♥ до конца забега (складывается). |
| `ScarOfSoul` | Шрам души | Token |  |  |  |  |  |  |  |  |  | Unplayable | В колоду добавляется Предсмертное эхо — неиграбельное проклятие весом 2. |
| `ScarOfPurse` | Шрам кошелька | Token |  |  |  |  |  |  |  |  |  | Unplayable | −40% золота Отпечатка. |

## Услуги лавки (35)

| Класс | Название | Пул | ⚡ | ◆ | ✦ | Тип | Редкость | Цель | Урон | Блок | Прочее | Ключевые | Текст |
|---|---|---|---|---|---|---|---|---|---|---|---|---|---|
| `CounterSell` | Продать рюкзак | Token |  |  |  |  |  |  |  |  |  |  | Продать снятые вещи из рюкзака ({Amount} шт.) за {Price} золотых. |
| `CounterSharpenWeapon` | Заточка оружия | Token |  |  |  |  |  |  |  |  |  |  | {Price} золотых. Оружие становится +{Amount}: свойства считают его на ступень выше, выше V — +1 урона. |
| `CounterSharpenArmor` | Укрепление брони | Token |  |  |  |  |  |  |  |  |  |  | {Price} золотых. Наименее улучшенная вещь брони становится +{Amount} (для Лат — ступенью выше). |
| `CounterReforge` | Перековка | Token |  |  |  |  |  |  |  |  |  |  | {Price} золотых. Перековать оружие в другое оружие ступени {Amount}; улучшения сохраняются. |
| `CounterBrewPotion` | Сварить зелье | Token |  |  |  |  |  |  |  |  |  |  | {Price} золотых. Травница сварит сильнейшее зелье из вашей сумки (ступень {Amount}). |
| `CounterBrewPoison` | Сварить яд | Token |  |  |  |  |  |  |  |  |  |  | {Price} золотых. Травница сварит сильнейший яд из вашей сумки (ступень {Amount}). |
| `CounterReagentKit` | Набор реагентов | Token |  |  |  |  |  |  |  |  |  |  | {Price} золотых. {Amount} обычных реагента в сумку. |
| `CounterSellReagents` | Продать реагенты | Token |  |  |  |  |  |  |  |  |  |  | Продать все реагенты из сумки ({Amount} шт.) за {Price} золотых. |
| `CounterPanacea` | Панацея | Token |  |  |  |  |  |  |  |  |  |  | {Price} золотых. Излечить все болезни в колоде ({Amount}). |
| `CounterOrder` | Сделать заказ | Token |  |  |  |  |  |  |  |  |  |  | Доверие 3: половина цены сейчас — в следующей лавке этого вида заказанный товар будет на витрине. |
| `CounterMore` | Ещё… | Token |  |  |  |  |  |  |  |  |  |  | Показать другие услуги. |
| `CounterEnchantOwnStone` | Зачаровать (свой камень) | Token |  |  |  |  |  |  |  |  |  |  | {Price} золотых. Чародей зачарует надетую вещь вашим камнем душ силы {Amount}. |
| `CounterEnchantShopStone` | Зачаровать (камень лавки) | Token |  |  |  |  |  |  |  |  |  |  | {Price} золотых. Чародей зачарует надетую вещь своим камнем силы {Amount}. |
| `CounterRewriteScroll` | Переписать свиток | Token |  |  |  |  |  |  |  |  |  |  | {Price} золотых. Заклинание свитка с пояса (ступень {Amount}) навсегда уходит в колоду, свиток тратится. |
| `CounterEnchantCard` | Зачаровать карту | Token |  |  |  |  |  |  |  |  |  |  | {Price} золотых. Наложить чары на карту колоды. |
| `CounterSoulStone` | Камень душ | Token |  |  |  |  |  |  |  |  |  |  | {Price} золотых. Купить камень душ силы {Amount} для зачарования. |
| `CounterEngrave` | Гравировка | Token |  |  |  |  |  |  |  |  |  |  | {Price} золотых. Нанести чары силы 1 на надетое кольцо или амулет. |
| `CounterFacet` | Огранка | Token |  |  |  |  |  |  |  |  |  |  | {Price} золотых. Перенести чары с одной надетой вещи на другую, которой они подходят. |
| `CounterCleanse` | Очищение | Token |  |  |  |  |  |  |  |  |  |  | {Price} золотых. Удалить карту из колоды (Праведным дешевле). |
| `CounterTempleHeal` | Храмовое исцеление | Token |  |  |  |  |  |  |  |  |  |  | {Price} золотых. Восстановить {Amount} ♥. |
| `CounterAtonement` | Искупление | Token |  |  |  |  |  |  |  |  |  |  | {Price} золотых. +1 ступень праведности (раз за акт). |
| `CounterChangeBlessing` | Сменить благословение | Token |  |  |  |  |  |  |  |  |  |  | {Price} золотых. Принять благословение бога этого храма вместо нынешнего. |
| `CounterRemoveCurse` | Снять проклятие | Token |  |  |  |  |  |  |  |  |  |  | Потерять {Amount} макс. ♥. Скупщик заберёт проклятие — без лишних вопросов. |
| `CounterClearName` | Очистить имя | Token |  |  |  |  |  |  |  |  |  |  | {Price} золотых. Лавки вида, который доверяет вам меньше всех (кража обнуляет доверие), снова на доверии 1. |
| `CounterTrade` | Обмен | Token |  |  |  |  |  |  |  |  |  |  | Отдать вещь из рюкзака за другую той же ступени или на одну ниже. Раз за визит. |
| `CounterBuyMule` | Купить вьючного мула | Token |  |  |  |  |  |  |  |  |  |  | {Price} золотых. +{Amount} груза; Сундук лагеря доступен у прилавков лавок. |
| `CounterShoeMule` | Подковать мула | Token |  |  |  |  |  |  |  |  |  |  | {Price} золотых. Вьючный мул несёт ещё +{Amount} груза. |
| `CounterMuleChest` | Сундук мула | Token |  |  |  |  |  |  |  |  |  |  | Бесплатно. Открыть Сундук, который везёт мул (внутри: {Amount}): убрать или забрать карты и вещи. |
| `OrderWeapon` | Заказ: оружие | Token |  |  |  |  |  |  |  |  |  |  | {Price} золотых. Оружие ступени {Amount} у следующего Оружейника. |
| `OrderArmor` | Заказ: броня | Token |  |  |  |  |  |  |  |  |  |  | {Price} золотых. Броня вашего класса ступени {Amount} у следующего Оружейника. |
| `OrderShield` | Заказ: щит | Token |  |  |  |  |  |  |  |  |  |  | {Price} золотых. Щит ступени {Amount} у следующего Оружейника. |
| `OrderEquipment` | Заказ: снаряжение | Token |  |  |  |  |  |  |  |  |  |  | {Price} золотых. Снаряжение ступени {Amount} у следующего Странника. |
| `OrderSpell` | Заказ: заклинание | Token |  |  |  |  |  |  |  |  |  |  | {Price} золотых. Заклинание высшей доступной ступени в следующей Чародейской. |
| `OrderPotion` | Заказ: зелье | Token |  |  |  |  |  |  |  |  |  |  | {Price} золотых. Зелье ступени V у следующей Травницы. |
| `OrderPoison` | Заказ: яд | Token |  |  |  |  |  |  |  |  |  |  | {Price} золотых. Яд ступени V у следующей Травницы. |

## Жетоны выбора (118)

| Класс | Название | Текст |
|---|---|---|
| `PickStatHealth` | Здоровье | +{Value} макс. ♥ (вложено: {Cost}). Каждые 3 очка — ступень: I сопротивление ядам I, II +6 груза, III Латы 1. |
| `PickStatStamina` | Выносливость | +{Value} пул ◆ (вложено: {Cost}). Каждые 3 очка — ступень: I +1 прирост ◆, II атаки +1 урона, III первая атака хода без ◆. |
| `PickStatMana` | Магия | +{Value} пул ✦ (вложено: {Cost}). Каждые 3 очка — ступень: I +1 прирост ✦, II первое заклинание хода на 1 ✦ дешевле, III Покров 3. |
| `PickStatBurden` | Переносимый вес | +{Value} груза (вложено: {Cost}). |
| `PickStatMastery` | Владение | +1 владение выбранной школой: родная — 1 очко, чужая — 2. Свободных очков: {Cost}. |
| `PickSchoolDestruction` | Разрушение | Текущее владение: {Value}. Поднять на 1 за {Cost} очк. характеристик. |
| `PickSchoolRestoration` | Восстановление | Текущее владение: {Value}. Поднять на 1 за {Cost} очк. характеристик. |
| `PickSchoolAlteration` | Изменение | Текущее владение: {Value}. Поднять на 1 за {Cost} очк. характеристик. |
| `PickSchoolConjuration` | Колдовство | Текущее владение: {Value}. Поднять на 1 за {Cost} очк. характеристик. |
| `PickSchoolIllusion` | Иллюзия | Текущее владение: {Value}. Поднять на 1 за {Cost} очк. характеристик. |
| `PickDishBloodmossStew` | Похлёбка из кровяного мха | +6 макс. ♥ на {Value} боя. Реагентов: {Cost}. |
| `PickDishVenisonRoast` | Жаркое из оленины | +1 прирост ◆ на {Value} боя. Реагентов: {Cost}. |
| `PickDishFishSoup` | Рыбный суп с синецветом | +1 прирост ✦ на {Value} боя. Реагентов: {Cost}. |
| `PickDishSpicedSausage` | Пряная колбаса | Сопротивление холоду I и +2 ◆ в начале боя на {Value} боя. Реагентов: {Cost}. |
| `PickDishMushroomStew` | Грибная похлёбка | Регенерация 1 на {Value} боя. Совместима с любой едой. Реагентов: {Cost}. |
| `PickDishSweetRoll` | Сладкий рулет | На 1-м ходу каждого боя +1 карта, {Value} боя. Реагентов: {Cost}. |
| `PickDishFeast` | Пир | Всё перечисленное на {Value} бой; снимает Усталость похода. Реагентов: {Cost}. |
| `PickEnchantHealth` | Укрепление здоровья | +{Value} макс. ♥, пока вещь надета. |
| `PickEnchantStamina` | Укрепление выносливости | +{Value} пул ◆, пока вещь надета. |
| `PickEnchantMagic` | Укрепление магии | +{Value} пул ✦, пока вещь надета. |
| `PickEnchantFireResistance` | Сопротивление огню | Сопротивление огню {Value}, пока вещь надета (считается лучшее). |
| `PickEnchantFrostResistance` | Сопротивление холоду | Сопротивление холоду {Value}, пока вещь надета (считается лучшее). |
| `PickEnchantShockResistance` | Сопротивление разряду | Сопротивление разряду {Value}, пока вещь надета (считается лучшее). |
| `PickEnchantWeaponMight` | Укрепление силы | Оружейные атаки +{Value} урона. |
| `PickEnchantBurden` | Пёрышко | +{Value} груза, пока вещь надета. |
| `PickEnchantFireBrand` | Огненное клеймо | Оружейные атаки накладывают {Value} Горения. Зарядов: {Cost}. |
| `PickEnchantFrostBrand` | Ледяное клеймо | Оружейные атаки накладывают {Value} Озноба. Зарядов: {Cost}. |
| `PickEnchantShockBrand` | Грозовое клеймо | Оружейные атаки выжигают {Value} ✦ врага. Зарядов: {Cost}. |
| `PickEnchantCraftDestruction` | Ремесло Разрушения | Первые {Value} заклинания Разрушения за ход стоят на 1 ✦ меньше. |
| `PickEnchantCraftRestoration` | Ремесло Восстановления | Первые {Value} заклинания Восстановления за ход стоят на 1 ✦ меньше. |
| `PickEnchantCraftAlteration` | Ремесло Изменения | Первые {Value} заклинания Изменения за ход стоят на 1 ✦ меньше. |
| `PickEnchantCraftConjuration` | Ремесло Колдовства | Первые {Value} заклинания Колдовства за ход стоят на 1 ✦ меньше. |
| `PickEnchantCraftIllusion` | Ремесло Иллюзии | Первые {Value} заклинания Иллюзии за ход стоят на 1 ✦ меньше. |
| `PickEnchantAbsorbHealth` | Поглощение здоровья | Оружейные атаки лечат вас на {Value} ♥ за каждую цель. Зарядов: {Cost}. |
| `PickEnchantAbsorbStamina` | Поглощение выносливости | Оружейные атаки отнимают у врага {Value} ◆ и дают вам {Value} ◆. Зарядов: {Cost}. |
| `PickEnchantAbsorbMagic` | Поглощение магии | Оружейные атаки выжигают у врага {Value} ✦ и дают вам {Value} ✦. Зарядов: {Cost}. |
| `PickEnchantBanish` | Изгнание | Оружейные атаки мгновенно убивают призванных врагов. Заряд тратится только на убийство. Зарядов: {Cost}. |
| `PickEnchantParalysis` | Паралич | Раз в бой Оружейная атака парализует цель (сила {Value}; боссы вместо этого получают Шаткость). Зарядов: {Cost}. |
| `PickStoneSmall` | Малый камень душ | Сила чар {Cost}. Есть: {Value}. |
| `PickStoneLarge` | Большой камень душ | Сила чар {Cost}. Есть: {Value}. |
| `PickStoneBlack` | Чёрный камень душ | Сила чар {Cost}. Есть: {Value}. |
| `PickMeditateStamina` | Медитация: тело | Ступень выносливости — {Value} до конца следующего боя. |
| `PickMeditateMana` | Медитация: разум | Ступень магии — {Value} до конца следующего боя. |
| `PickPrayRedeem` | Искупление | Праведность +1 ступень (раз в акт). |
| `PickPraySwitch` | Сменить благословение | Принять благословение бога этого алтаря вместо нынешнего. |
| `PickMentorSkill` | Наставник: навык | Наставник обучит узлу дерева: {Value} золотых, чужая школа — {Cost}. До 3 за забег. |
| `PickLessonTwoHanded` | Урок: двуручное | {Value} золотых. Больше никакого Неумелого хвата с двуручным оружием. |
| `PickLessonCrossbow` | Урок: арбалет | {Value} золотых. Больше никакого Неумелого хвата с арбалетом. |
| `PickLessonShield` | Урок: тяжёлый щит | {Value} золотых. Больше никакого Неумелого хвата с тяжёлым щитом. |
| `NodeCraftAlchemy` | Алхимия | Ярус {Value}. Очков навыка: {Cost}. Варка — зелья и яды ступенью выше. |
| `NodeCraftEnchanting` | Зачарование | Ярус {Value}. Очков навыка: {Cost}. Предмет держит 2 чар вместо 1. |
| `NodeCraftSmithing` | Кузнечное дело | Ярус {Value}. Очков навыка: {Cost}. Кузня улучшает вещи до +3 (Легендарный). |
| `NodeCraftArtisan` | Мастер-ремесленник | Ярус {Value}. Очков навыка: {Cost}. Раз за привал первое ремесло (Варка, Кузня, Зачарование) возвращает 1 очко лагеря. |
| `NodeWeaponTraining` | Владение оружием | Ярус {Value}. Очков навыка: {Cost}. Никакого Неумелого хвата ни с каким оружием и щитом. |
| `NodeConditioning` | Закалка тела | Ярус {Value}. Очков навыка: {Cost}. +1 пул ◆. |
| `NodeSecondBreath` | Второе дыхание | Ярус {Value}. Очков навыка: {Cost}. Раз за бой: если ход начался без ◆ — +3 ◆. |
| `NodeVeteran` | Ветеран | Ярус {Value}. Очков навыка: {Cost}. Стойкость +1. |
| `NodeCampKettle` | Походный котёл | Ярус {Value}. Очков навыка: {Cost}. Открывает Готовку в лагере. |
| `NodeDeftPacking` | Ловкая укладка | Ярус {Value}. Очков навыка: {Cost}. +4 груза. В Сундуке лагеря — сколько угодно вещей за раз. |
| `NodeFirstAid` | Первая помощь | Ярус {Value}. Очков навыка: {Cost}. Короткий привал лечит 20 % вместо 15 %. |
| `NodeWayfarer` | Опытный путник | Ярус {Value}. Очков навыка: {Cost}. +1 очко лагеря. |
| `NodeAcclimatization` | Акклиматизация | Ярус {Value}. Очков навыка: {Cost}. Сопротивление огню и холоду I на первые 2 хода боя. |
| `NodeTradeName` | Торговое имя | Ярус {Value}. Очков навыка: {Cost}. Цены в лавках −10 %. |
| `NodeWellRested` | Отдохнувший | Ярус {Value}. Очков навыка: {Cost}. После Отдыха: Бодрость — +1 прирост ◆ и ✦ на 2 следующих боя. |
| `NodeCampMaster` | Мастер привала | Ярус {Value}. Очков навыка: {Cost}. +1 очко лагеря; Кузня и Зачарование стоят 1. |
| `NodeHerbLore` | Знаток трав | Ярус {Value}. Очков навыка: {Cost}. Реагенты с врагов ×2; Варка ступенью выше. |
| `NodeFighter` | Боец | Ярус {Value}. Очков навыка: {Cost}. Атаки одноручным оружием +1 урона. |
| `NodeFencer` | Фехтовальщик | Ярус {Value}. Очков навыка: {Cost}. Крит Острого меча — ×2 вместо ×1.5. |
| `NodeButcher` | Мясник | Ярус {Value}. Очков навыка: {Cost}. Атаки топором +3 урона по врагам ниже 50 % ♥. |
| `NodeCrusher` | Дробитель | Ярус {Value}. Очков навыка: {Cost}. Атаки булавой накладывают 1 Раскола. |
| `NodeSweep` | Размах | Ярус {Value}. Очков навыка: {Cost}. Атаки двуручным оружием +2 урона. |
| `NodeHeavyHand` | Тяжёлая рука | Ярус {Value}. Очков навыка: {Cost}. Атаки двуручным оружием накладывают 1 Шаткости. |
| `NodeShatter` | Сокрушение | Ярус {Value}. Очков навыка: {Cost}. Атаки двуручным оружием: Пробитие 1. |
| `NodeRelentless` | Неудержимый | Ярус {Value}. Очков навыка: {Cost}. Первая атака двуручным оружием за ход — на 1 ◆ дешевле. |
| `NodeShieldBearer` | Щитоносец | Ярус {Value}. Очков навыка: {Cost}. Со щитом карты дают на 1 Блока больше. |
| `NodeShieldWall` | Стена щитов | Ярус {Value}. Очков навыка: {Cost}. Со щитом: Латы +1. |
| `NodeShieldBash` | Удар щитом | Ярус {Value}. Очков навыка: {Cost}. Со щитом первая атака хода накладывает 2 Шаткости. |
| `NodeBastion` | Бастион | Ярус {Value}. Очков навыка: {Cost}. Со щитом: 2 Блока в начале каждого хода. |
| `NodeHardened` | Закалённый | Ярус {Value}. Очков навыка: {Cost}. 2+ вещи тяжёлой брони: Латы +1. |
| `NodeSteadfast` | Непреклонный | Ярус {Value}. Очков навыка: {Cost}. Стойкость +1 за каждые 2 вещи тяжёлой брони. |
| `NodeWeightBearer` | Привычка к весу | Ярус {Value}. Очков навыка: {Cost}. Тяжёлая броня больше не снижает прирост ✦. |
| `NodeAgainstAllOdds` | Вопреки всему | Ярус {Value}. Очков навыка: {Cost}. Раз за бой при падении ниже 25 % ♥ — 3 Лат. |
| `NodeBloodletter` | Кровопускатель | Ярус {Value}. Очков навыка: {Cost}. Атаки кинжалом и копьём накладывают 1 Кровотечения. |
| `NodeAmbush` | Засада | Ярус {Value}. Очков навыка: {Cost}. Подлые атаки кинжалом и копьём +3 урона. |
| `NodePrecision` | Точность | Ярус {Value}. Очков навыка: {Cost}. Атаки кинжалом и копьём +1 урона. |
| `NodeHemorrhage` | Кровоизлияние | Ярус {Value}. Очков навыка: {Cost}. Атаки +2 урона по врагам с Кровотечением. |
| `NodeMarksman` | Меткий стрелок | Ярус {Value}. Очков навыка: {Cost}. Атаки луком и арбалетом +1 урона. |
| `NodeQuickDraw` | Быстрая стрельба | Ярус {Value}. Очков навыка: {Cost}. Первая атака луком или арбалетом за ход — на 1 ◆ дешевле. |
| `NodeAimedShot` | Прицельный выстрел | Ярус {Value}. Очков навыка: {Cost}. На первом ходу атаки луком и арбалетом +3 урона. |
| `NodeArmorPiercer` | Бронебой | Ярус {Value}. Очков навыка: {Cost}. Атаки луком и арбалетом: Пробитие 1. |
| `NodeNimble` | Ловкач | Ярус {Value}. Очков навыка: {Cost}. Уворот 1 в начале боя. |
| `NodeCounterstrike` | Контрудар | Ярус {Value}. Очков навыка: {Cost}. Контрудар 2 в начале боя. |
| `NodeLightStep` | Лёгкий шаг | Ярус {Value}. Очков навыка: {Cost}. 2+ вещи лёгкой брони: +1 пул ◆. |
| `NodeElusive` | Неуловимый | Ярус {Value}. Очков навыка: {Cost}. Каждый 3-й ход — Уворот 1. |
| `NodePoisoner` | Отравитель | Ярус {Value}. Очков навыка: {Cost}. Яды на оружии — на 1 заряд дольше. |
| `NodeToxicologist` | Токсиколог | Ярус {Value}. Очков навыка: {Cost}. Накладываемый вами Яд +1. |
| `NodeVenomWard` | Иммунитет к ядам | Ярус {Value}. Очков навыка: {Cost}. Сопротивление ядам +1 ступень. |
| `NodeVenomMaster` | Мастер ядов | Ярус {Value}. Очков навыка: {Cost}. Варка ядов ступенью выше. |
| `NodeDestructionI` | Разрушение I | Ярус {Value}. Очков навыка: {Cost}. Изучение школы Разрушения. +1 владение школой Разрушения. |
| `NodeDestructionII` | Разрушение II | Ярус {Value}. Очков навыка: {Cost}. Изучение школы Разрушения. +1 владение школой Разрушения. |
| `NodeDestructionIII` | Разрушение III | Ярус {Value}. Очков навыка: {Cost}. Изучение школы Разрушения. +1 владение школой Разрушения. |
| `NodeElementLord` | Повелитель стихии | Ярус {Value}. Очков навыка: {Cost}. Заклинания Разрушения +2 урона. +1 владение школой Разрушения. |
| `NodeRestorationI` | Восстановление I | Ярус {Value}. Очков навыка: {Cost}. Изучение школы Восстановления. +1 владение школой Восстановления. |
| `NodeRestorationII` | Восстановление II | Ярус {Value}. Очков навыка: {Cost}. Изучение школы Восстановления. +1 владение школой Восстановления. |
| `NodeRestorationIII` | Восстановление III | Ярус {Value}. Очков навыка: {Cost}. Изучение школы Восстановления. +1 владение школой Восстановления. |
| `NodeWarden` | Хранитель | Ярус {Value}. Очков навыка: {Cost}. Покров +2. +1 владение школой Восстановления. |
| `NodeAlterationI` | Изменение I | Ярус {Value}. Очков навыка: {Cost}. Изучение школы Изменения. +1 владение школой Изменения. |
| `NodeAlterationII` | Изменение II | Ярус {Value}. Очков навыка: {Cost}. Изучение школы Изменения. +1 владение школой Изменения. |
| `NodeAlterationIII` | Изменение III | Ярус {Value}. Очков навыка: {Cost}. Изучение школы Изменения. +1 владение школой Изменения. |
| `NodeStoneskin` | Каменная кожа | Ярус {Value}. Очков навыка: {Cost}. Латы +1. +1 владение школой Изменения. |
| `NodeConjurationI` | Колдовство I | Ярус {Value}. Очков навыка: {Cost}. Изучение школы Колдовства. +1 владение школой Колдовства. |
| `NodeConjurationII` | Колдовство II | Ярус {Value}. Очков навыка: {Cost}. Изучение школы Колдовства. +1 владение школой Колдовства. |
| `NodeConjurationIII` | Колдовство III | Ярус {Value}. Очков навыка: {Cost}. Изучение школы Колдовства. +1 владение школой Колдовства. |
| `NodeBoundMaster` | Повелитель призванного | Ярус {Value}. Очков навыка: {Cost}. Призывы +2 урона. +1 владение школой Колдовства. |
| `NodeIllusionI` | Иллюзия I | Ярус {Value}. Очков навыка: {Cost}. Изучение школы Иллюзии. +1 владение школой Иллюзии. |
| `NodeIllusionII` | Иллюзия II | Ярус {Value}. Очков навыка: {Cost}. Изучение школы Иллюзии. +1 владение школой Иллюзии. |
| `NodeIllusionIII` | Иллюзия III | Ярус {Value}. Очков навыка: {Cost}. Изучение школы Иллюзии. +1 владение школой Иллюзии. |
| `NodePhantasm` | Фантазм | Ярус {Value}. Очков навыка: {Cost}. Первое заклинание Иллюзии за ход — на 1 ✦ дешевле. +1 владение школой Иллюзии. |

