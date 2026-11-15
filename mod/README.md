# mod/ — код и ресурсы Slay the Requiem

> ⚠️ **Pre-alpha (этапы 0–7 написаны).** Код написан по исходникам BaseLib и декомпилированной игре, но **ещё ни разу не компилировался и не
> запускался в игре** (кроме `Core/` — он собирается и тестируется в CI): в среде, где он писался, нет .NET SDK и Slay the Spire 2. Ждите ошибок компиляции при первой сборке.
> Чистая логика (`SlayTheRequiemCode/Core`) проверена Python-зеркалом (`tools/sim`) и покрыта xUnit-тестами.

## Сборка (по шаблону Alchyr/ModTemplate-StS2)

Нужно: Slay the Spire 2 (≥ 0.107), .NET 9 SDK, [MegaDot](https://megadot.megacrit.com/) (Godot 4.5.1 mono) для `.pck`, мод BaseLib ≥ 3.3.0.

1. Путь к игре находится автоматически (`Sts2PathDiscovery.props`). Если нет — скопируйте `Directory.Build.props.example`
   в `Directory.Build.props` и пропишите `Sts2Path` / `GodotPath`.
2. `dotnet build mod/SlayTheRequiem.csproj` — DLL и (если задан GodotPath) `.pck` копируются в `<игра>/mods/SlayTheRequiem/`.
3. Тесты логики без игры: `dotnet test mod/SlayTheRequiem.Tests` и `python3 -m unittest discover -s tools/sim`.

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
| `Debugging/` | отладочные команды консоли `str_*` (список — [путеводитель](../docs/GUIDE.md#-для-тестеров-консоль)) |

Ресурсы: `SlayTheRequiem/localization/{eng,rus}` (генерируется `tools/loc/build_loc.py`),
`SlayTheRequiem/data/*.csv` (генерируется `tools/markup`, руками не править; попадает и в `.pck` — через `*.csv.import`
с `importer="keep"`, — и в DLL как встроенный ресурс), `SlayTheRequiem/images/{ui,map}` (собирается из SVG в `art/`
скриптом `tools/art/render_ui_icons.mjs`). Остальной арт — ванильный, по таблице `tools/art/vanilla_art.csv`
([art spec §0](../docs/plan/art-ui-animation-spec.md)).

Рискованные места первой сборки (предполагаемые имена API игры) перечислены в комментариях в начале файлов
`Acts/ActFlowPatches.cs` и `Acts/ActRuntime.cs`. Дальше — [ROADMAP](../docs/ROADMAP.md), Этап 8.
