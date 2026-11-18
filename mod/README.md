# mod/ — код и ресурсы Slay the Requiem

> ⚠️ **Pre-alpha после первого плейтеста.** Исторические build/PCK и запуск автором на `ec598de` действительны.
> После аудита реализован [блок 1 «Надёжное ядро»](../docs/review/stage-one-implementation-2026-11-18.md).
> Публичный CI: **560/560 Core**, Python/content. Ручной workflow полного build/PCK на доверенном runner с легальной игрой
> добавлен, но здесь не запускался; новая игровая сборка ещё не принята. Ручные/двухклиентные прогоны — в самом конце.
> Критерии: [план релиза](../docs/plan/next-playable-release-2026-11-18.md), [матрица](../docs/review/regression-checklist-2026-11-18.md).

## Воспроизводимая сборка и чистая установка

Контракт: легальная игра **0.113.0**, .NET SDK **9.0.x**, [MegaDot **4.5.1 mono**](https://megadot.megacrit.com/),
установленный **BaseLib 3.4.8**; NuGet BaseLib `[3.4.8]` / ModAnalyzers `[0.2.1]` закреплены.

Полная инструкция — [tools/g0/README.md](../tools/g0/README.md): `probe → build → verify → install`.
Pipeline выполняет полный Release build игрового проекта, отдельный Core/TRX, отдельный export и загрузку ресурсов
из PCK в пустом проекте. Успех этих стадий не означает запуск игры. Installer проверяет hashes DLL/PCK/manifest,
сохраняет прежний каталог вне `mods` и не удаляет неизвестные/дублирующие установки.

Для разработки из корня репозитория:

```bash
dotnet build mod/SlayTheRequiem.csproj   # только DLL, БЕЗ export и БЕЗ установки в игру
dotnet test mod/SlayTheRequiem.Tests    # отдельный Core-проект без игровых references
python3 -m unittest discover -s tools/sim
python3 -m unittest discover -s tools/g0 -v  # только контракты build tool, не полный mod build
```

`Sts2PathDiscovery.props` ищет игру автоматически. Если не нашёл, скопируйте `Directory.Build.props.example`
в игнорируемый `Directory.Build.props` и укажите `Sts2Path` / `GodotPath` (либо передайте `-p:Sts2DataDir=...`).
Старый convenience `-p:CopyModOnBuild=true` копирует **только** DLL/manifest и предупреждает об отсутствии согласованной
PCK-пары; это не релизный installer. `dotnet publish`/явный target `ExportModPack` требуют MegaDot и не маскируют сбой.
Ручной CI: [legal-game-build.yml](../.github/workflows/legal-game-build.yml), подготовка runner — в инструкции G0.

## Что где лежит

| Папка `SlayTheRequiemCode/` | Что делает |
|---|---|
| `MainFile.cs` | вход мода: конфиг, загрузка CSV разметки ◆, Harmony |
| `Core/` | чистые правила без зависимостей от игры (все числа дизайна) — собираются тестовым проектом |
| `Config/` | настройки мода (BaseLib `ModConfig`) |
| `Resources/`, `Rules/` | ◆ Выносливость и ✦ Мана, глобальные правила боя, настройка врагов |
| `Cards/`, `Spells/`, `Powers/`, `Potions/` | карты героев, заклинания, силы и статусы, зелья и яды |
| `Equipment/`, `Relics/` | снаряжение со слотами, груз, реликвии |
| `Alchemy/` | реагенты, варка, лагерные действия |
| `Pantheon/` | Знамения, Небесные, Князья Бездны, праведность, события |
| `Transformations/` | формы (вампир, зверь, лич…) и Пути Героя |
| `Progression/` | Летопись героя, деревья навыков, зачарование карт |
| `Shops/` | лавки Реквиема, доверие, услуги |
| `Rollback/` | Камень Памяти, Свечи, Шрамы, счёт забега |
| `Acts/`, `Monsters/` | пять актов, правила актов, элиты, супербоссы |
| `Net/` | кооператив: сообщения BaseLib, голосования |
| `Patches/`, `Extensions/` | Harmony-патчи и вспомогательные методы |
| `Art/` | выбор картинки: свой файл → ванильный арт (`VanillaArtTable.g.cs` генерируется `tools/art/build_vanilla_art.py`) → заглушка |
| `UI/` | интерфейс систем (M1): шкалы ◆/✦, значки цены ◆, стихии и веса на картах, груз, ◆/✦ врагов и цены намерений, подсказки, Латоносец, скрытие Некробиндера/Дефекта. Узлы строятся кодом, без `.tscn` |
| `Debugging/` | отладочные команды консоли `str_*` (список — [путеводитель](../docs/GUIDE.md#-для-тестеров-консоль)) |

Ресурсы: `SlayTheRequiem/localization/{eng,rus}` (генерируется `tools/loc/build_loc.py`),
`SlayTheRequiem/data/*.csv` (генерируется `tools/markup`, руками не править; попадает и в `.pck` — через `*.csv.import`
с `importer="keep"`, — и в DLL как встроенный ресурс), `SlayTheRequiem/images/{ui,map}` (собирается из SVG в `art/`
скриптом `tools/art/render_ui_icons.mjs`). Остальной арт — ванильный, по таблице `tools/art/vanilla_art.csv`
([art spec §0](../docs/plan/art-ui-animation-spec.md)).

После обновления игры повторно проверяйте сигнатуры API, установку Harmony-патчей и полный DLL/PCK, а не только Core.
Комментарии о «первой сборке» в старых файлах описывают историю разработки. Текущие риски и дальнейшая приёмка —
в [аудите](../docs/review/main-audit-2026-11-18.md) и [плане](../docs/plan/stabilization-plan-2026-11-18.md).
