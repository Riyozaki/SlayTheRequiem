# Передача на сборку — 18.11.2026

**Можно переходить к полному build на машине с игрой. Это не объявление принятого релиза.**
Историческая успешная сборка/PCK и плейтест автора действительны. Новые исходники требуют своей полной сборки;
публичный CI не располагает игровыми DLL/MegaDot и не может её заменить.

## Что доделано перед передачей

- Удалён второй `PaperDollDeathPatch`: это был реальный конфликт объявлений C#, который не ловили Core/grammar.
  Проверка дублей теперь выполняется для каждого `.csproj` в CI; добавлены отрицательные регрессии.
- Оружие/аура/Знаки/сферы отсоединяются **до** отложенного `QueueFree`: замена в том же кадре не получает другое имя,
  старое поколение не остаётся в дереве. Death alpha применяется к родителю с потомками; обновление мёртвого героя
  не показывает новое оружие. Переключение PaperDoll применяется к текущим visuals.
- Выход из дерева завершает optional motion и восстанавливает исходную позу. Deferred focus пропускает disabled,
  hidden, удалённые строки и потомков уже закрываемого modal; старое окно не перехватывает фокус у нового.
- Полка сохраняет `0/ёмкость` и доступ к журналу после последнего возврата; исчезновение сфокусированной карты
  переводит фокус на живой элемент, если его не заняло другое окно. UI по-прежнему не перемещает карты.
- Паспорт — прокручиваемое native modal вместо панели фиксированной ширины. Есть keyboard/gamepad navigation,
  закрытие Esc/B, прокрутка PageUp/PageDown/правым стиком. Открытие: плашка Реквиема или F8/нажатие правого стика,
  если ввод не занят другим окном/действием. Recovery освобождает modal перед запросом подтверждения;
  портал/Камень Памяти не ждут подтверждение в popup, который native Add молча отбросил.

Изменения presentation локальны: **session 5 / creation 2 / save 4 / shop 2 / economy 1** не менялись.

## Проверки

- Локально: **34/34** safe source/content/tool checks; **322 Python tests PASS**
  (151 sim + 38 G0 + 25 release + 108 checker); C# grammar **323/0**, Harmony targets **136/0**;
  проверены объявления всех пяти C# projects, генераторы не изменили входные файлы.
- **[`3d9acfb7e4cca78831fad63837366b31edb05637`, CI 44282711349](https://github.com/Riyozaki/SlayTheRequiem/actions/runs/44282711349): 6/6 jobs success, 879/879 Core**, заново проверены 28 actual C# cache trials.
  Input hash `bb73a110b17a503d70890f8795a580c39286dedc3b9fd599a7e8df0acfa11952`.
  Результаты привязаны к этому source, а не унаследованы от старого CI.
  Подробности — [отчёт Б3](../review/stage-three-quality-implementation-2026-11-18.md) и его evidence.
- Отдельный public-Godot smoke **реально прошёл 12 групп** с production node/focus helpers, без игровых DLL.
  Его scope — helper lifecycle/input, **не** игровой мод, MegaDot/PCK, rendered layout, FPS или 100 открытий игровых окон.

## Полная сборка у автора

Нужны легальная **Slay the Spire 2 0.107.1**, установленный **BaseLib 3.4.8**, **Python ≥3.11**,
**.NET 9 SDK** и **MegaDot 4.5.1 mono**. Обычный Godot из helper CI не подменяет MegaDot.
Закрыть игру и сохранить резервные копии важных saves/config вне `mods`. Команды выполнять из корня checkout.

Пример для PowerShell: заменить **два** примерных пути своими. Каждый build использует **новый** каталог.

```powershell
$Game = "D:\path\to\Slay the Spire 2"
$MegaDot = "D:\path\to\MegaDot.exe"
$Bundle = ".arena/builds/prebuild-001"

python tools/g0/mod_build.py probe --game-dir "$Game" --godot "$MegaDot"
if ($LASTEXITCODE -ne 0) { throw "Probe failed: сначала устранить указанную причину" }

python tools/g0/mod_build.py build --game-dir "$Game" --godot "$MegaDot" --output "$Bundle"
if ($LASTEXITCODE -ne 0) { throw "Build/export failed: сохранить логи, не устанавливать неполный комплект" }

python tools/g0/mod_build.py verify --bundle "$Bundle"
if ($LASTEXITCODE -ne 0) { throw "Bundle verification failed" }

python tools/g0/mod_build.py install --bundle "$Bundle" --mods-dir "$Game/mods" --game-closed
if ($LASTEXITCODE -ne 0) { throw "Install failed: разобрать конфликт без удаления пользовательских файлов" }
```

Для Linux/macOS и нестандартного расположения DLL/BaseLib — `tools/g0/README.md`, параметры `--game-data`,
`--mods-dir`, `--dotnet`. `probe` сам ничего не собирает. Обычный `dotnet build` больше не устанавливает мод
и не выдаётся за экспорт PCK.

Успех `build` означает: **полный Release build → Core/TRX → отдельные import/export → загрузка ресурсов из PCK
в пустом проекте → согласованный build passport**. `install` меняет весь каталог, не смешивает новый DLL со старым PCK;
прежний комплект сохраняется в `.requiem-backups` вне `mods`. Loose DLL/PCK, дубликат mod ID и неизвестная установка
дают безопасный отказ, а не автоматическое удаление.

При ошибке нужны stage и небольшой очищенный фрагмент вокруг **первой** ошибки из `build.log` / `import.log` /
`export.log` / `pack-check.log`. Не публиковать игровые DLL, весь bundle, binlog, приватные пути или saves.
Warnings полного build требуют отдельного разбора; зелёный Core их не отменяет.

## После build — только финальная игровая приёмка

Первый disposable запуск — только BaseLib + SlayTheRequiem; сначала `str_patches` / `str_passport`, затем native suites,
UI/арт/звук, сохранение/Continue и полная [финальная матрица](FINAL_ACCEPTANCE.md). В ней остаются **257 NOT_RUN**:
в том числе 12 полных маршрутов, все 21 боя/формы/Пути, 5-актный кооп и целевые сетевые/recovery проверки.
Helper smoke не закрывает эти строки. Публикация/merge/релиз отдельно, автоматически не выполняются.
