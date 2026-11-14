# mod/ — код и ресурсы Slay the Requiem

> ⚠️ **Этап 0.** Код написан по исходникам BaseLib и декомпилированной игре, но **ещё ни разу не компилировался и не
> запускался в игре**: в среде, где он писался, нет .NET SDK и Slay the Spire 2. Ждите ошибок компиляции при первой сборке.
> Чистая логика (`SlayTheRequiemCode/Core`) проверена Python-зеркалом (`tools/sim`) и покрыта xUnit-тестами.

## Сборка (по шаблону Alchyr/ModTemplate-StS2)

Нужно: Slay the Spire 2 (≥ 0.107), .NET 9 SDK, [MegaDot](https://megadot.megacrit.com/) (Godot 4.5.1 mono) для `.pck`, мод BaseLib ≥ 3.3.0.

1. Путь к игре находится автоматически (`Sts2PathDiscovery.props`). Если нет — скопируйте `Directory.Build.props.example`
   в `Directory.Build.props` и пропишите `Sts2Path` / `GodotPath`.
2. `dotnet build mod/SlayTheRequiem.csproj` — DLL и (если задан GodotPath) `.pck` копируются в `<игра>/mods/SlayTheRequiem/`.
3. Тесты логики без игры: `dotnet test mod/SlayTheRequiem.Tests` и `python3 -m unittest discover -s tools/sim`.

## Что уже есть

| Файл | Что делает |
|---|---|
| `SlayTheRequiemCode/MainFile.cs` | вход мода: конфиг, загрузка CSV разметки ◆, Harmony |
| `Core/*` | правила без зависимостей от игры: выносливость и надрыв, Латы/Раскол/Пробитие, сопротивления, тики статусов, предрасположенность к школам, груз, статы героев, парсер CSV |
| `Resources/RequiemResource.cs` | ◆ Выносливость и ✦ Мана (BaseLib `BasicCustomResource`): полный запас в начале боя, прирост за ход до пула |
| `Rules/RequiemCombatRules.cs` | глобальный синглтон боя: оплата атак ◆, Устал/Изнурён (и в предпросмотре урона), надрыв ♥ |
| `Rules/RequiemMode.cs` | в системе только Латоносец (Ironclad), Безмолвная, Регент; Некробиндер/Дефект — облики, не персонажи системы |
| `Powers/*` | Горение, Озноб, Статика, Кровотечение, Латы, Раскол |
| `SlayTheRequiem/data/vanilla_attacks_stamina.csv` | ручная разметка 131 ванильной атаки (генерируется `tools/markup/build_vanilla_attacks.py`, руками не править) |
| `SlayTheRequiem/localization/{eng,rus}` | тексты сил, подсказки ресурсов, настройки |

Следующее — см. [ROADMAP](../docs/ROADMAP.md), Этап 0–1.
