# Б2.4 — матрица проверяемых направлений, 2026-11-18

> Автоматическая **проверка исходного доступа**, не пройденные забеги, не точные шансы наград и не winrate.
> Источник рецептов: `tools/balance/build_directions.json`; генератор: `python3 tools/balance/build_directions.py`.

По три кандидата на героя. Это роли и взаимозаменяемые ориентиры, не обязательная колода и не гарантия выпадения всех карт.
Названия карт/пулы/редкости/цены берутся из текущего каталога. Проверяются стартовые пары, наличие снаряжения/навыков,
удалённые ванильные карты и роли. Условия текста, порядок хуков и победы требуют нативной финальной проверки.

## Общие ограничения доступа

- Обычные награды: сначала доля своего пула по редкости, затем правила заклинаний и Пути могут заменить её. 30% — не финальная доля.
- Spell/Path-подмена сохраняет редкость исходной опции; если кандидата этой редкости нет, исходная карта остаётся.
- Школа в наградах открыта при владении ≥1, ступень ≤ владение+1. В лавке можно увидеть низкие ступени и без владения; это не отменяет цену/состояние каста.
- Путь — сохранённые 2 своих + 1 чужой, а не гарантированный конкретный Путь. Базовая связка должна работать до него.
- Полный маршрут: выбор после босса 3 при уровне ≥8. Короткий 1→3→5: после босса 1 без порога уровня 8.
- Формы: необязательный обряд за 1 очко лагеря; отмена бесплатна. Совместимость, праведность и заработанные ступени обязательны.
- Договор → Вестник: Хранитель Гробницы либо две элиты после Договора. Лич: поздний акт и 10 побед Вестником (6 в явном short run).
- Только выбранная подпись Повелителя стихии получает компетентность Мастера от Сущности. Другие заклинания/школы/возврат очков её не получают.

## Ironclad · Оружие и ритм выносливости (`plate_weapon`)

Старт: `ForgebloodOrigin` → `BerserkerBackground`. Поддерживающий, но не обязательный Путь: `Barbarian`.

| Роль | Доступные кандидаты (⚡/◆/✦ до скидок/предрасположенности) |
|---|---|
| Связки | `CatchBreath` — Ironclad, Common, ⚡/◆/✦ 0/0/0; `ArmorSplitter` — Ironclad, Common, ⚡/◆/✦ 1/1/0; `CrushingBlow` — Ironclad, Common, ⚡/◆/✦ 2/3/0 |
| Отдача | `OverheadSmash` — Ironclad, Uncommon, ⚡/◆/✦ 2/3/0; `AgainstAllOdds` — Ironclad, Uncommon, ⚡/◆/✦ 1/0/0 |
| Защита | `SteadyStance` — Ironclad, Common, ⚡/◆/✦ 1/0/0; `RaiseShield` — Ironclad, Common, ⚡/◆/✦ 1/0/0 |
| Добор | `Ironclad/BattleTrance` (сохранённый ванильный пул) |
| Ресурсы | `CatchBreath` — Ironclad, Common, ⚡/◆/✦ 0/0/0; `Tempering` — Ironclad, Uncommon, ⚡/◆/✦ 1(-1)/0/0; `ChangeGrip` — Ironclad, Uncommon, ⚡/◆/✦ 0/0/0 |
| Контрмеры | `ArmorSplitter` — Ironclad, Common, ⚡/◆/✦ 1/1/0; `SmotherFlames` — Ironclad, Common, ⚡/◆/✦ 0/0/0 |
| Необязательные редкие | `MountainBreaker` — Ironclad, Rare, ⚡/◆/✦ 2/4/0 |

Снаряжение: `IronBattleaxe`.
Узлы: `Sweep`, `HeavyHand`, `Conditioning`.

Background guarantees the tier-I two-hander; common armor answer/breath, uncommon finishers. No rare is needed.

## Ironclad · Щит и защита (`plate_shield`)

Старт: `CitadelOrigin` → `LegionaryBackground`. Поддерживающий, но не обязательный Путь: `Champion`.

| Роль | Доступные кандидаты (⚡/◆/✦ до скидок/предрасположенности) |
|---|---|
| Связки | `ShieldBash` — Ironclad, Common, ⚡/◆/✦ 1/1/0; `LockShields` — Ironclad, Common, ⚡/◆/✦ 1/0/0; `RaiseShield` — Ironclad, Common, ⚡/◆/✦ 1/0/0 |
| Отдача | `ShieldCharge` — Ironclad, Uncommon, ⚡/◆/✦ 2/2/0; `SwordAndBoard` — Ironclad, Uncommon, ⚡/◆/✦ 1/1/0 |
| Защита | `RaiseShield` — Ironclad, Common, ⚡/◆/✦ 1/0/0; `LegionDrill` — Ironclad, Uncommon, ⚡/◆/✦ 1/0/0; `CloseRanks` — Ironclad, Uncommon, ⚡/◆/✦ 2/0/0 |
| Добор | `MeasuredStrike` — Ironclad, Common, ⚡/◆/✦ 1/1/0; `Ironclad/BattleTrance` (сохранённый ванильный пул) |
| Ресурсы | `CatchBreath` — Ironclad, Common, ⚡/◆/✦ 0/0/0; `SteadyStance` — Ironclad, Common, ⚡/◆/✦ 1/0/0 |
| Контрмеры | `ArmorSplitter` — Ironclad, Common, ⚡/◆/✦ 1/1/0; `SmotherFlames` — Ironclad, Common, ⚡/◆/✦ 0/0/0 |
| Необязательные редкие | `Unbreakable` — Ironclad, Rare, ⚡/◆/✦ 3/0/0 |

Снаряжение: `OakShield`, `RustyCleaver`.
Узлы: `ShieldBearer`, `ShieldWall`, `Fighter`.

Legionary guarantees shield and Shield Bash. A tier-I one-hander costs 45 before discounts; 1H is an optional draw/attack improvement, not required for Shield Charge.

## Ironclad · Кличи и холод (`plate_voice`)

Старт: `FrostbornOrigin` → `SkaldBackground`. Поддерживающий, но не обязательный Путь: `Battlemage`.

| Роль | Доступные кандидаты (⚡/◆/✦ до скидок/предрасположенности) |
|---|---|
| Связки | `BattleCry` — Ironclad, Uncommon, ⚡/◆/✦ 1/0/0; `IceWind` — Ironclad, Common, ⚡/◆/✦ 1/1/0; `SteadyStance` — Ironclad, Common, ⚡/◆/✦ 1/0/0 |
| Отдача | `UnrelentingForce` — Ironclad, Uncommon, ⚡/◆/✦ 1/0/0; `FrostBreath` — Ironclad, Uncommon, ⚡/◆/✦ 1/0/0; `FrostStrike` — Ironclad, Uncommon, ⚡/◆/✦ 1/2/0 |
| Защита | `RaiseShield` — Ironclad, Common, ⚡/◆/✦ 1/0/0; `DesertPatience` — Ironclad, Common, ⚡/◆/✦ 1/0/0 |
| Добор | `Ironclad/BattleTrance` (сохранённый ванильный пул) |
| Ресурсы | `CatchBreath` — Ironclad, Common, ⚡/◆/✦ 0/0/0; `ChangeGrip` — Ironclad, Uncommon, ⚡/◆/✦ 0/0/0 |
| Контрмеры | `ArmorSplitter` — Ironclad, Common, ⚡/◆/✦ 1/1/0; `FireBreath` — Ironclad, Uncommon, ⚡/◆/✦ 1/0/0 |
| Необязательные редкие | `Frostblood` — Ironclad, Rare, ⚡/◆/✦ 2(-1)/0/0; `StormCall` — Ironclad, Rare, ⚡/◆/✦ 2/0/0 |

Снаряжение: не является обязательным стартовым условием.
Узлы: `Conditioning`, `SecondBreath`.

Skald guarantees Battle Cry and two physical shelf slots. Other shouts are uncommon; the build still uses ordinary attacks between cooldowns and against cold resistance.

## Silent · Скрытность и подлый удар (`silent_stealth`)

Старт: `MoonbornOrigin` → `DuskSingerBackground`. Поддерживающий, но не обязательный Путь: `Assassin`.

| Роль | Доступные кандидаты (⚡/◆/✦ до скидок/предрасположенности) |
|---|---|
| Связки | `Backstab` — Silent, Common, ⚡/◆/✦ 1/1/0; `Garrote` — Silent, Common, ⚡/◆/✦ 1/1/0; `ShadowStep` — Silent, Uncommon, ⚡/◆/✦ 1(-1)/0/0 |
| Отдача | `Backstab` — Silent, Common, ⚡/◆/✦ 1/1/0; `Garrote` — Silent, Common, ⚡/◆/✦ 1/1/0; `Riposte` — Silent, Uncommon, ⚡/◆/✦ 1/0/0 |
| Защита | `Sidestep` — Silent, Common, ⚡/◆/✦ 1/0/0; `SlipAway` — Silent, Common, ⚡/◆/✦ 0/0/0 |
| Добор | `SlipAway` — Silent, Common, ⚡/◆/✦ 0/0/0; `Silent/Backflip` (сохранённый ванильный пул) |
| Ресурсы | `SlipAway` — Silent, Common, ⚡/◆/✦ 0/0/0; `FlaskToFace` — Silent, Common, ⚡/◆/✦ 1(-1)/0/0 |
| Контрмеры | `Garrote` — Silent, Common, ⚡/◆/✦ 1/1/0; `HuntingShot` — Silent, Common, ⚡/◆/✦ 1/1/0 |
| Необязательные редкие | `ShadowStrike` — Silent, Rare, ⚡/◆/✦ 2/2/0; `Evasion` — Silent, Rare, ⚡/◆/✦ 2(-1)/0/0 |

Снаряжение: `SmugglerDagger`.
Узлы: `Nimble`, `Counterstrike`, `Bloodletter`.

Dusk Singer supplies Muffled Steps. Common Backstab/Garrote work against non-attacking intent even before Shadow Step; dagger is an improvement, not a condition for Sneak.

## Silent · Яд, Хворь и алхимия (`silent_poison`)

Старт: `MarshbornOrigin` → `VenomweaverBackground`. Поддерживающий, но не обязательный Путь: `Blightbringer`.

| Роль | Доступные кандидаты (⚡/◆/✦ до скидок/предрасположенности) |
|---|---|
| Связки | `ToxicCoating` — Silent, Common, ⚡/◆/✦ 0/0/0; `PoisonedBlade` — Silent, Common, ⚡/◆/✦ 1/1/0; `RottingCut` — Silent, Common, ⚡/◆/✦ 1/1/0 |
| Отдача | `PoisonedBlade` — Silent, Common, ⚡/◆/✦ 1/1/0; `FlaskToFace` — Silent, Common, ⚡/◆/✦ 1(-1)/0/0 |
| Защита | `Sidestep` — Silent, Common, ⚡/◆/✦ 1/0/0; `Silent/Backflip` (сохранённый ванильный пул) |
| Добор | `SlipAway` — Silent, Common, ⚡/◆/✦ 0/0/0; `Silent/Backflip` (сохранённый ванильный пул) |
| Ресурсы | `ToxicCoating` — Silent, Common, ⚡/◆/✦ 0/0/0; `Distillation` — Silent, Uncommon, ⚡/◆/✦ 1(-1)/0/0 |
| Контрмеры | `HuntingShot` — Silent, Common, ⚡/◆/✦ 1/1/0; `Backstab` — Silent, Common, ⚡/◆/✦ 1/1/0; `Garrote` — Silent, Common, ⚡/◆/✦ 1/1/0 |
| Необязательные редкие | `VenomMastery` — Silent, Rare, ⚡/◆/✦ 1/0/0; `VolatileMixture` — Silent, Rare, ⚡/◆/✦ 2(-1)/0/0 |

Снаряжение: не является обязательным стартовым условием.
Узлы: `Poisoner`, `Toxicologist`, `Nimble`.

Background guarantees Poison Spray; Toxic Coating and Flask to Face supply Toxin without a potion roll. Keep physical/Sunder/Sneak answers to undead and constructs.

## Silent · Охота и стрелковый темп (`silent_hunt`)

Старт: `ThicketbornOrigin` → `HunterBackground`. Поддерживающий, но не обязательный Путь: `MysticArcher`.

| Роль | Доступные кандидаты (⚡/◆/✦ до скидок/предрасположенности) |
|---|---|
| Связки | `HuntingShot` — Silent, Common, ⚡/◆/✦ 1/1/0; `ThirdStep` — Silent, Common, ⚡/◆/✦ 1/1/0; `Silent/BladeDance` (сохранённый ванильный пул) |
| Отдача | `CoveringShot` — Silent, Uncommon, ⚡/◆/✦ 1/1/0; `FleetingStrike` — Silent, Uncommon, ⚡/◆/✦ 0/1/0 |
| Защита | `CoveringShot` — Silent, Uncommon, ⚡/◆/✦ 1/1/0; `Sidestep` — Silent, Common, ⚡/◆/✦ 1/0/0; `SlipAway` — Silent, Common, ⚡/◆/✦ 0/0/0 |
| Добор | `CoveringShot` — Silent, Uncommon, ⚡/◆/✦ 1/1/0; `SlipAway` — Silent, Common, ⚡/◆/✦ 0/0/0; `Silent/Backflip` (сохранённый ванильный пул) |
| Ресурсы | `SlipAway` — Silent, Common, ⚡/◆/✦ 0/0/0; `WindRhythm` — Silent, Uncommon, ⚡/◆/✦ 1(-1)/0/0 |
| Контрмеры | `HuntingShot` — Silent, Common, ⚡/◆/✦ 1/1/0; `Garrote` — Silent, Common, ⚡/◆/✦ 1/1/0 |
| Необязательные редкие | `Evasion` — Silent, Rare, ⚡/◆/✦ 2(-1)/0/0 |

Снаряжение: `HeavyBow`.
Узлы: `Marksman`, `QuickDraw`, `AimedShot`.

Hunter guarantees bow and Hunting Shot, retains Shiv +2. Covering Shot is uncommon; both new attacks remain playable without a ranged weapon. Mystic Archer additionally requires acquiring an elemental spell.

## Regent · Школа Разрушения и стихии (`regent_elements`)

Старт: `HighbornOrigin` → `WizardBackground`. Поддерживающий, но не обязательный Путь: `ElementLord`.

| Роль | Доступные кандидаты (⚡/◆/✦ до скидок/предрасположенности) |
|---|---|
| Связки | `Firebolt` — Spell, Destruction/Novice, ⚡/◆/✦ 1/0/1; награды: Destruction ≥1; `Sparks` — Spell, Destruction/Novice, ⚡/◆/✦ 0/0/1; награды: Destruction ≥1; `GrimoireStudy` — Regent, Common, ⚡/◆/✦ 1/0/0 |
| Отдача | `Fireball` — Spell, Destruction/Adept, ⚡/◆/✦ 2/0/3; награды: Destruction ≥2; `IceSpike` — Spell, Destruction/Adept, ⚡/◆/✦ 1/0/3; награды: Destruction ≥2; `BallLightning` — Spell, Destruction/Adept, ⚡/◆/✦ 2/0/3; награды: Destruction ≥2 |
| Защита | `RuneOfWarding` — Regent, Common, ⚡/◆/✦ 1/0/0; `LesserWard` — Spell, Restoration/Novice, ⚡/◆/✦ 0/0/1; награды: Restoration ≥1 |
| Добор | `GrimoireStudy` — Regent, Common, ⚡/◆/✦ 1/0/0 |
| Ресурсы | `ArcaneFocus` — Regent, Common, ⚡/◆/✦ 0/0/0; `MagickaWell` — Regent, Uncommon, ⚡/◆/✦ 1(-1)/0/0; `StaffStrike` — Regent, Common, ⚡/◆/✦ 1/1/0 |
| Контрмеры | `Sparks` — Spell, Destruction/Novice, ⚡/◆/✦ 0/0/1; награды: Destruction ≥1; `IceSpike` — Spell, Destruction/Adept, ⚡/◆/✦ 1/0/3; награды: Destruction ≥2; `Fireball` — Spell, Destruction/Adept, ⚡/◆/✦ 2/0/3; награды: Destruction ≥2 |
| Необязательные редкие | `DualCasting` — Regent, Rare, ⚡/◆/✦ 1(-1)/0/0 |

Снаряжение: не является обязательным стартовым условием.
Узлы: `Destruction1`, `Destruction2`, `Destruction3`.

Firebolt/Wolf/Arcane Focus are in every NEW Regent baseline. Adept spells are clumsy at mastery 2, free at 3. Element Lord teaches its selected Master signature, not all school spells.

## Regent · Мана и боевой маг (`regent_mana`)

Старт: `MistbornOrigin` → `EnchanterBackground`. Поддерживающий, но не обязательный Путь: `Battlemage`.

| Роль | Доступные кандидаты (⚡/◆/✦ до скидок/предрасположенности) |
|---|---|
| Связки | `StaffStrike` — Regent, Common, ⚡/◆/✦ 1/1/0; `ArcaneFocus` — Regent, Common, ⚡/◆/✦ 0/0/0; `ManaBurn` — Regent, Common, ⚡/◆/✦ 1/0/1 |
| Отдача | `Concentration` — Regent, Uncommon, ⚡/◆/✦ 1/0/0; `Regent/FallingStar` (сохранённый ванильный пул); `SoulRend` — Regent, Uncommon, ⚡/◆/✦ 1/0/1 |
| Защита | `RuneOfWarding` — Regent, Common, ⚡/◆/✦ 1/0/0; `Concentration` — Regent, Uncommon, ⚡/◆/✦ 1/0/0; `Oakflesh` — Spell, Alteration/Novice, ⚡/◆/✦ 1(-1)/0/1; награды: Alteration ≥1 |
| Добор | `GrimoireStudy` — Regent, Common, ⚡/◆/✦ 1/0/0 |
| Ресурсы | `MagickaWell` — Regent, Uncommon, ⚡/◆/✦ 1(-1)/0/0; `ArcaneFocus` — Regent, Common, ⚡/◆/✦ 0/0/0; `ManaSurge` — Regent, Uncommon, ⚡/◆/✦ 0/0/0 |
| Контрмеры | `ManaBurn` — Regent, Common, ⚡/◆/✦ 1/0/1; `Firebolt` — Spell, Destruction/Novice, ⚡/◆/✦ 1/0/1; награды: Destruction ≥1; `Telekinesis` — Spell, Alteration/Novice, ⚡/◆/✦ 1/0/1; награды: Alteration ≥1 |
| Необязательные редкие | `ManaStorm` — Regent, Rare, ⚡/◆/✦ 2/0/X; `ArcaneMastery` — Regent, Rare, ⚡/◆/✦ 2(-1)/0/0 |

Снаряжение: `OakStaff`.
Узлы: `Alteration1`, `Restoration1`.

Enchanter recovers mana on non-spell attacks. Common links + uncommon Concentration/Well sustain the plan; rare X-mana finisher is optional and never the only damage source.

## Regent · Колдовство и призывы (`regent_summons`)

Старт: `MistbornOrigin` → `SummonerBackground`. Поддерживающий, но не обязательный Путь: `Medium`.

| Роль | Доступные кандидаты (⚡/◆/✦ до скидок/предрасположенности) |
|---|---|
| Связки | `ConjureSpiritWolf` — Spell, Conjuration/Novice, ⚡/◆/✦ 1/0/2; награды: Conjuration ≥1; `BoundBlade` — Spell, Conjuration/Novice, ⚡/◆/✦ 0/0/1; награды: Conjuration ≥1; `GrimoireStudy` — Regent, Common, ⚡/◆/✦ 1/0/0 |
| Отдача | `ConjureFlameAtronach` — Spell, Conjuration/Adept, ⚡/◆/✦ 2/0/3; награды: Conjuration ≥2; `ConjureFrostAtronach` — Spell, Conjuration/Adept, ⚡/◆/✦ 2/0/3; награды: Conjuration ≥2 |
| Защита | `RuneOfWarding` — Regent, Common, ⚡/◆/✦ 1/0/0; `LesserWard` — Spell, Restoration/Novice, ⚡/◆/✦ 0/0/1; награды: Restoration ≥1; `Oakflesh` — Spell, Alteration/Novice, ⚡/◆/✦ 1(-1)/0/1; награды: Alteration ≥1 |
| Добор | `GrimoireStudy` — Regent, Common, ⚡/◆/✦ 1/0/0 |
| Ресурсы | `ArcaneFocus` — Regent, Common, ⚡/◆/✦ 0/0/0; `MagickaWell` — Regent, Uncommon, ⚡/◆/✦ 1(-1)/0/0; `ManaSurge` — Regent, Uncommon, ⚡/◆/✦ 0/0/0 |
| Контрмеры | `BoundBlade` — Spell, Conjuration/Novice, ⚡/◆/✦ 0/0/1; награды: Conjuration ≥1; `Firebolt` — Spell, Destruction/Novice, ⚡/◆/✦ 1/0/1; награды: Destruction ≥1; `Telekinesis` — Spell, Alteration/Novice, ⚡/◆/✦ 1/0/1; награды: Alteration ≥1 |
| Необязательные редкие | `DualCasting` — Regent, Rare, ⚡/◆/✦ 1(-1)/0/0 |

Снаряжение: не является обязательным стартовым условием.
Узлы: `Conjuration1`, `Conjuration2`, `Conjuration3`.

Baseline guarantees Spirit Wolf; Summoner adds Bound Blade instead of a redundant second wolf, plus mastery/HP/damage. Medium increases slots later; early summons replace, not stack indefinitely.

## Объём реализации и оставшаяся проверка

Две новые карты охоты закрывают общедоступный Раскол и защиту/добор стрелка; оружие и карта охоты гарантированы предысторией.
У Регента в NEW-колоде три звена (заклинание/призыв/восстановление маны) вместо трёх базовых карт, без роста числа карт.
Старые сохранения и Перерождение не прогоняют новую стартовую колоду. Обязательные карты формы/Пути используют существующие saved claims/defer, не бесконтрольную выдачу.
Статическая матрица не доказывает игровые комбо, частоты появления или проходимость. Для финала цикла подготовлены нативные integrity cases и opt-in трассы; их исполнение не подменяется этим отчётом.
