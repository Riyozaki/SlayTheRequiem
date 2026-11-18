# G0: воспроизводимая сборка с легальной игрой

Pipeline **не скачивает игру/её DLL**, не заменяет references декомпиляцией или stub и не считается игровым тестом.
Актуальные статусы — [реализация Б1](../../docs/review/stage-one-implementation-2026-11-18.md) и
[evidence](../../docs/review/evidence/stage-one-2026-11-18/README.md). В Arena полный pipeline ещё **не выполнен**:
локально проверяются source/content/Python-контракты инструмента; публичный CI отдельно компилирует и исполняет Core.
Ручной full-build workflow подготовлен ниже, но наличие YAML не означает успешный build/PCK.
Исходники инструмента находятся в `tools/g0/`, а не в каталоге с именем `build` для временных артефактов.

## 1. Требования

На машине сборки нужны:

- **Python ≥3.11**;
- легальная **Slay the Spire 2 0.107.1**, включая `release_info.json`, `sts2.dll`, `0Harmony.dll`;
- отдельно установленный **BaseLib 3.4.8**: `mods/BaseLib/BaseLib.json` и `BaseLib.dll`;
- **.NET SDK 9.0.x**, не только runtime. Корневой `global.json` допускает актуальный feature band девятой версии;
- **MegaDot 4.5.1 mono** из доверенного источника. Более новый/обычный non-Mono Godot не подходит.
  Проверка `--version` не удостоверяет происхождение executable; его SHA256 записывается в паспорт;
- доступ к NuGet-пакетам из `mod/SlayTheRequiem.csproj`: BaseLib `[3.4.8]`, ModAnalyzers `[0.2.1]`, Godot.NET.Sdk `4.5.1` закреплены.

Закрыть игру перед установкой и отдельно сохранить резервные копии важных saves. Ниже `STS2_GAME_DIR`, `STS2_MODS_DIR`,
`MEGADOT` — переменные shell **вашей** машины: корень игры, её каталог `mods`, executable MegaDot.
На Windows можно подставить пути в кавычках непосредственно в команды. Реальные значения в Git не записывать.

## 2. Probe → build/export → verify → install

Из корня checkout:

```bash
python tools/g0/mod_build.py probe --game-dir "$STS2_GAME_DIR" --godot "$MEGADOT"

# Каждый запуск — НОВЫЙ output. Существующий каталог не перетирается.
python tools/g0/mod_build.py build --game-dir "$STS2_GAME_DIR" --godot "$MEGADOT" --output .arena/builds/g1-001

python tools/g0/mod_build.py verify --bundle .arena/builds/g1-001
python tools/g0/mod_build.py install --bundle .arena/builds/g1-001 --mods-dir "$STS2_MODS_DIR" --game-closed
```

Для нестандартного расположения передать `--game-data` (каталог DLL игры) и/или `--mods-dir` в `probe` **и** `build`.
`--dotnet` выбирает SDK executable. Дополнительные аргументы: `python tools/g0/mod_build.py --help`, `<command> --help`.
`probe` проверяет версии toolchain/установки и наличие references, **не** компилирует мод.
Заменять отсутствующие игровые DLL фиктивными сборками для «зелёного build» нельзя.

### Стадии build

1. Git SHA, dirty и content hash входов `mod/`, `tools/`, **`art/`**, `.github/`, `global.json`: включая неигнорируемые untracked
   файлы и сведения об удалённых tracked inputs. Это паспорт рабочей копии, не требование делать commit перед проверкой.
2. Изолированный `output/project` с source inputs плюс внешний Compile-link тестов `tools/balance/Trace/Scenario.cs`
   на правильном относительном пути под output. Игнорируемые локальные `Directory.Build.props`, caches и старые
   артефакты не копируются; file symlinks отвергаются. Внутри checkout output разрешён только под Git-ignore (`.arena/`).
3. **Полный Release build игрового проекта**, отдельно xUnit проекта `Core/`. Сохраняются `dotnet-info.log`,
   **`build.log` и `build.binlog` со всеми warnings**, `core-tests.log`, `test-results/core.trx`.
   Успешная отдельная компиляция Core не заменяет полный mod build. TRX должен содержать реальные Counters:
   `total = executed = passed > 0`, failures/errors/aborted/skipped и прочие non-pass counters равны нулю.
   Эти counts входят в проверяемый паспорт; exit code 0 или пустой TRX не считаются исполненными тестами.
4. Проверка фактически восстановленных версий по `project.assets.json`. Игра/Harmony — внешние references,
   `Private=false`, не часть поставки.
5. Godot import, затем **отдельный** `--export-pack BasicExport`, `import.log` и `export.log`.
   Нет engine/успешного экспорта — нет успешного G0 и build passport. Старый PCK не засчитывается.
6. Полученный PCK монтируется в **пустом проекте**: собственные textures/audio/fonts/scenes/tres/shaders
   загружаются через ResourceLoader, JSON/CSV проверяются через FileAccess. Исходники не маскируют отсутствующие ресурсы.
   Отчёт — `pack-check.json`, `game_runtime=false` остаётся false.
7. Если inputs/DLL не изменились во время export, записывается `SlayTheRequiem.build.json`: toolchain/dependency versions,
   hashes DLL/PCK/manifest и references/engine, дата экспорта, save schema **4**, shop protocol **2**,
   session protocol **5**, creation journal **2**, economy revision **1**,
   список установленных (не «runtime-confirmed») модов. Любая неуспешная стадия не выпускает паспорт.

Godot `ERROR`/`SCRIPT ERROR` фатальны даже при exit code 0. Warnings полного build всё равно требуют отдельного
разбора; зелёный exit code не означает «нет предупреждений». Hashes/паспорт не являются цифровой подписью
и не доказывают безопасное происхождение сборки или прохождение забега.

### Чистая установка

`verify`/`install` проверяют допустимые schema/protocol и hashes **трёх** артефактов. Installer создаёт staging и заменяет
каталог мода целиком, не оставляя старые лишние файлы возле новой пары. Предыдущая установка уходит в `.requiem-backups`
**вне `mods`**, при ошибке swap возвращается. Неизвестный каталог, symlink, loose legacy DLL/PCK или второй каталог
с таким же mod ID **блокируют** установку, а не удаляются. Разобрать их вручную, сохраняя нужные файлы.

`--game-closed` — подтверждение оператора, не автоматическая проверка процессов. `install-receipt.json` сохраняет
`runtime_verified=false`. Нельзя копировать обратно только одну DLL/PCK из разных build outputs.
Обычный `dotnet build` по умолчанию **не устанавливает мод и не экспортирует PCK**.

## 3. Паспорт и игровая приёмка

Следующие команды — для **финальной**, не промежуточной игровой приёмки. Первый прогон —
**только BaseLib + SlayTheRequiem**, без прочих модов. В диагностическом disposable **singleplayer** run,
вне боя, с включёнными системами (для integrity также Pantheon + ShopVariety):

```text
str_passport
str_integrity
str_factory smoke 2
str_factory full 8
```

`str_integrity` содержит **92 подготовленных source cases**. Количество factory fixtures зависит от текущего
каталога/режима/seeds и берётся из фактического `[CardFactorySuite]`, не старого числа в README. Эти native suites
**ещё не запускались здесь**; наличие деклараций не засчитывается как исполнение.
Изолированные non-local model fixtures не переключают RunManager на себя и не пишут пользовательские saves,
но это всё равно первый запуск непроверенного игрового кода: не использовать ценный забег.
Результаты нужны из `[IntegritySuite]` / `[CardFactorySuite]`, а не из счётчика сценариев в source.
Factory не проверяет число видимых holders и draw/shout UI.

Для интерактивного воспроизведения: `str_trace on`, после — `str_trace off`. Приложить `[RunPassport]`:
SHA/dirty, hashes DLL/PCK и dependencies/прочие моды, seed/location, героя, уровни сложности/Летописи,
origin/background, requested/effective settings и источник snapshot, язык, разрешение/UI scale, new/continue/rollback.
Плашка `Реквием: ВКЛ/ВЫКЛ/СБОЙ ПАТЧЕЙ` открывает эти сведения, не означает «сборка принята».

Обязательно отдельно: реальные награды/разгрузка/retry/skip; ложный Add/позднее исключение/оплата; legacy chest,
вещи с charges/enchants/дублями; disk save/quit/Continue; **два клиента**, повтор/stale/double input и missing receipt;
UI RU/EN в 1080p/4K на двух UI scales; «0/2», `nope`, рука и кличи normal/full hand.
Критерии — [план трёх блоков](../../docs/plan/next-playable-release-2026-11-18.md) и
[регрессионная матрица](../../docs/review/regression-checklist-2026-11-18.md).

Saved `OperationPending`/`OperationBlocked` при неизвестном результате — **не приглашение повторить оплату или очистить поле**.
Continue не ремонтирует partial commit. Сохранить логи, восстановить всем участникам известную непротиворечивую копию хоста.
Автоматического payment repair/atomic disk transaction здесь нет.

## 4. CI и публикация

[Публичный CI](../../.github/workflows/ci.yml) проверяет Core/данные/syntax/Harmony declarations и synthetic installer contracts.
Он **не** собирает мод с игрой и **не** экспортирует PCK. Core/TRX сохраняется как отдельный artifact; реальные
counters проверяются и публикуются Core job. Пустой/пропущенный прогон не считается PASS.

### Ручной full-build workflow с легальной игрой

[`legal-game-build.yml`](../../.github/workflows/legal-game-build.yml) и
[`legal_game_build.py`](../ci/legal_game_build.py) реализуют тот же pipeline на доверенном self-hosted runner.
**Workflow подготовлен, но здесь не запускался; доступность/настройка runner не подтверждена.**

1. Владелец подготавливает **изолированную build-машину**, не свою игровую сессию, с легальной игрой/BaseLib,
   Python ≥3.11, .NET 9 SDK и MegaDot 4.5.1 mono. Runner label: `requiem-legal-build`.
2. Создаёт GitHub environment `legal-game-build` с обязательным одобрением доверенного владельца. Не разрешать
   запуск PR-кода/непроверенных ref на машине с приватными файлами. У workflow нет PR trigger, checkout read-only.
3. В **приватном окружении процесса runner**, не в репозитории/чате, задаёт `STS2_GAME_DIR` и `MEGADOT`.
   При необходимости: `STS2_GAME_DATA`, `STS2_MODS_DIR`, `DOTNET_EXE`. Не печатать env и не класть пути в source.
4. После ревью выбранного ref запускает workflow вручную с явным подтверждением `trusted_legal_environment=true`.
   Это подтверждение наличия легальной среды оператором, а не способ обойти отсутствующие references.
5. Runner выполняет полный build, Core/TRX, отдельные import/export/PCK-load и двойной clean-install smoke
   в **scratch-каталог**, не в каталог установленной игры. Прежняя игровая установка/сейвы не трогаются.
6. Публичный artifact — только allowlisted `public-result.json`: hashes, версии, counts/status и warning-code counts.
   Полные logs/binlog, bundle, references, paths и список частных модов остаются в `.arena/legal-ci/.../private`
   **локально на runner**, не выгружаются. Перед переиспользованием машины очистить приватный scratch по политике владельца.

Успешный legal build всё ещё имеет `game_runtime=false` / `co_op=false`: запуск игровых adapters, UI и два клиента
фиксируются отдельно в самом конце. Workflow не создаёт release и не является разрешением публикации.

```bash
# Только сам инструмент; не компиляция/экспорт:
python -m unittest discover -s tools/g0 -v
```

В Git — исходники/tests и обезличенное evidence. **Не коммитить** игровые DLL/лицензированные assets, весь bundle/project,
binlog, saves и локальные абсолютные пути. Full logs/binlog содержат пути; хранить приватно, публичные выдержки очищать.
`.arena/` — временный игнорируемый output, не долговременное публичное evidence.


## Изменения блока 1 (18.11.2026)

Ручной плейтест перенесён пользователем в самый конец всего релизного цикла. Команды выше и сценарии двух клиентов
подготовлены для этого финального прогона; их наличие не означает, что они исполнены. Core/xUnit, Python, source/API
и упаковочные контрактные проверки выполняются при реализации.

`str_trace on` включает цепочку pool → replacement → model → holder → actual portrait → selection → Add → combat-copy → draw.
`str_metrics on` собирает до **1024** последних frame/reward/trait samples без покадрового лога; `off` замораживает
снимок schema 2. Есть allocation/GC/node/memory deltas и hardware/context; `status` сериализует данные и сам аллоцирует.
После одинакового warm-up использовать `reset`, не опрашивать status в capture loop. Полный протокол —
[финальная приёмка](../../docs/release/FINAL_ACCEPTANCE.md). До native запуска измеренного engine baseline нет.

При неопределённой операции: значок «Реквием» → повтор сверки квитанций / восстановление безопасной копии.
`str_recover status|retry|restore` открывает те же пути. Повтор передаёт исходный immutable request/receipt, не выполняет
новую покупку. Автоматическое снятие блока разрешено только для receipt-only timeout после полного совпадения
результатов, revision, наблюдаемого digest и failure epoch **после disposal активной mutation**.
PartialCommit/stale/новая ошибка так не разблокируются.
Восстановление требует подтверждения, возвращает **весь забег**, сохраняет неудачную попытку, меняет timeline и в коопе
открывает лобби сохранения хоста. Нет подходящей копии — понятный отказ, не небезопасное «unlock».

В каталоге saves профиля: `requiem_inventory_checkpoint[_coop].save` (checksummed envelope),
`requiem_inventory_failed[_coop].save`, `requiem_before_migration[_coop].save`; предыдущая атомарная генерация — `.previous`.
Это пользовательские файлы, не содержимое Git/сборки. Миграция сохраняет исходный native run до добавления identity;
неизвестная версия/повреждённые правила или конфликт snapshots отвергаются без подстановки локального меню.

Guard чтения проверяет primary и native `.backup`, распознаёт также escaped Requiem IDs и сохраняет исходные байты
в `requiem_before_load[_coop].save` / `requiem_before_load[_coop].native-backup.save`. Для этой пары native aggressive
repair не вызывается: strict typed preflight отклоняет duplicate/unknown fields, неверные типы и другую native game
schema до rewrite/rename. Если primary отсутствует, читается backup; corrupt existing primary не заменяется молча
старым. Legacy **mod** metadata в текущем native format мигрирует с backup; другая **game** schema требует matching game.
MP canonicalization также не переименовывает исходники при ошибке model/ID. Для in-memory input без дискового оригинала
копия названа `requiem_before_migration_native_object...`, не выдаётся за original bytes.
