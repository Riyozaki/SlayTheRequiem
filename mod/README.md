# mod/ — код и ресурсы Slay the Requiem

> ⚠️ **Pre-alpha · G0/G1 в работе, не приняты.** Историческая сборка `ec598de` и плейтест не проверяют новые изменения.
> В этой среде full mod build, новый xUnit, настоящий PCK export и игра/кооп не запускались. Синтаксис/Harmony declarations,
> Python и Core CI — отдельные слои. Текущий статус каждой системы — [единый реестр](../docs/plan/implementation-registry.md).

## Сборка и чистая установка

Цель: **игра 0.107.1**, .NET SDK **9.0.x**, [MegaDot](https://megadot.megacrit.com/) **4.5.1 mono**, BaseLib **3.4.8**,
ModAnalyzers **0.2.1**. Версии NuGet закреплены в csproj; игра/Harmony предоставляются вашей легальной установкой,
а не репозиторием. Полная инструкция с командами, зависимостями и паспортом — **[tools/g0/README.md](../tools/g0/README.md)**.

Из корня репозитория: `mod_build.py probe` → `build` в свежий игнорируемый `.arena/builds/...` → `verify` → `install --game-closed`.
Pipeline собирает весь csproj, пишет warnings/binlog, выполняет Core tests, **отдельно** импортирует/экспортирует PCK,
проверяет resources из пустого проекта и только потом устанавливает согласованную пару с backup вне `mods`.
Ни Python fixtures инструмента, ни один успешный `dotnet build` не являются игровым PASS.

Для итеративной разработки (команды тоже из корня):

```bash
dotnet build mod/SlayTheRequiem.csproj -c Release       # DLL; БЕЗ auto-install и БЕЗ auto-export
dotnet test mod/SlayTheRequiem.Tests -c Release          # только Core, без references игры
python -m unittest discover -s tools/sim -v
python -m unittest discover -s tools/g0 -v           # synthetic pipeline contracts
```

Путь к игре для обычного MSBuild определяется `Sts2PathDiscovery.props`. Override — `mod/Directory.Build.props` из `.example`
или `-p:Sts2DataDir=...`; `Sts2Path`/`ModsPath`/`GodotPath` не перезаписываются defaults. Локальные props игнорируются Git.
`CopyModOnBuild=false` по умолчанию: прежний opt-in копирует только DLL/manifest с warning, не верифицированный bundle.
Для приёмки использовать CLI pipeline, не смешивать новую DLL со старым PCK.

После установки: `str_passport` и плашка фактических правил; на disposable solo run вне боя — `str_integrity`,
`str_factory smoke 2`, затем full. Они ещё не прогонялись и не заменяют disk Continue, реальные экраны и два клиента.
Saved Pending/Block при неопределённом результате не снимается простым Continue; не повторять оплату и не очищать marker вручную.

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

Комментарии в `Acts/ActFlowPatches.cs`/`Acts/ActRuntime.cs` описывают исторические риски API; это не список всех
возможных compiler/runtime ошибок текущего кода. Ближайшая обязательная приёмка — [G0/G1](../docs/plan/stabilization-roadmap-2026-11-17.md),
статусы и решения — [реестр](../docs/plan/implementation-registry.md).
