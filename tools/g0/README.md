# G0: воспроизводимая сборка с легальной игрой

Pipeline **не скачивает игру/её DLL**, не заменяет references декомпиляцией или stub и не считается игровым тестом.
Статусы G0/G1 — в [едином реестре](../../docs/plan/implementation-registry.md). В Arena полный pipeline ещё **не выполнен**:
здесь проверяются только source/content и синтетические Python-контракты инструмента.
Исходники инструмента находятся в `tools/g0/`, а не в каталоге с именем `build` для временных артефактов.

## 1. Требования

На машине сборки нужны:

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

1. Git SHA, dirty и content hash входов `mod/`, `tools/`, `.github/`, `global.json`: включая неигнорируемые untracked
   файлы и сведения об удалённых tracked inputs. Это паспорт рабочей копии, не требование делать commit перед проверкой.
2. Изолированный `output/project` с source inputs. Игнорируемые локальные `Directory.Build.props`, caches и старые
   артефакты не копируются; file symlinks отвергаются. Внутри checkout output разрешён только под Git-ignore (`.arena/`).
3. **Полный Release build игрового проекта**, отдельно xUnit проекта `Core/`. Сохраняются `dotnet-info.log`,
   **`build.log` и `build.binlog` со всеми warnings**, `core-tests.log`, `test-results/core.trx`.
   Успешная отдельная компиляция Core не заменяет полный mod build.
4. Проверка фактически восстановленных версий по `project.assets.json`. Игра/Harmony — внешние references,
   `Private=false`, не часть поставки.
5. Godot import, затем **отдельный** `--export-pack BasicExport`, `import.log` и `export.log`.
   Нет engine/успешного экспорта — нет успешного G0 и build passport. Старый PCK не засчитывается.
6. Полученный PCK монтируется в **пустом проекте**: собственные textures/audio загружаются через ResourceLoader,
   JSON/CSV проверяются через FileAccess. Исходники не маскируют отсутствующие ресурсы.
   Отчёт — `pack-check.json`, `game_runtime=false` остаётся false.
7. Если inputs/DLL не изменились во время export, записывается `SlayTheRequiem.build.json`: toolchain/dependency versions,
   hashes DLL/PCK/manifest и references/engine, дата экспорта, save schema **3**, shop protocol **1**,
   список установленных (не «runtime-confirmed») модов. Любая неуспешная стадия не выпускает паспорт.

Даже при exit code 0 просмотреть warnings и Godot `ERROR` в логах вручную. Hashes/паспорт не являются цифровой подписью
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

Первый прогон — **только BaseLib + SlayTheRequiem**, без прочих модов. В диагностическом disposable **singleplayer** run,
вне боя, с включёнными системами (для integrity также Pantheon + ShopVariety):

```text
str_passport
str_integrity
str_factory smoke 2
str_factory full 8
```

`str_integrity` содержит **55 source cases**, factory — 216 smoke / 5712 full **на seed**. Они **ещё не запускались**.
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
Критерии — [G0/G1](../../docs/plan/stabilization-roadmap-2026-11-17.md) и [реестр](../../docs/plan/implementation-registry.md).

Saved `OperationPending`/`OperationBlocked` при неизвестном результате — **не приглашение повторить оплату или очистить поле**.
Continue не ремонтирует partial commit. Сохранить логи, восстановить всем участникам известную непротиворечивую копию хоста.
Автоматического payment repair/atomic disk transaction здесь нет.

## 4. CI и публикация

[Публичный CI](../../.github/workflows/ci.yml) проверяет Core/данные/syntax/Harmony declarations и synthetic installer contracts.
Он **не** собирает мод с игрой и **не** экспортирует PCK. Private runner с легальной игрой может выполнять этот pipeline;
игровой/кооп-прогон всё равно фиксируется отдельно.

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
`str_metrics on` собирает до 256 последних кадров/обновлений награды без покадрового лога; `str_metrics status` выдаёт
число, медиану, p95 и максимум в миллисекундах. Рядом сохранить `str_passport`; до запуска нет измеренного baseline.

При неопределённой операции: значок «Реквием» → повтор сверки квитанций / восстановление безопасной копии.
`str_recover status|retry|restore` открывает те же пути. Повтор передаёт исходный immutable request/receipt, не выполняет
новую покупку. Автоматическое снятие блока разрешено только для receipt-only timeout после полного совпадения
результатов, revision и наблюдаемого digest. PartialCommit/stale/новая ошибка так не разблокируются.
Восстановление требует подтверждения, возвращает **весь забег**, сохраняет неудачную попытку, меняет timeline и в коопе
открывает лобби сохранения хоста. Нет подходящей копии — понятный отказ, не небезопасное «unlock».

В каталоге saves профиля: `requiem_inventory_checkpoint[_coop].save` (checksummed envelope),
`requiem_inventory_failed[_coop].save`, `requiem_before_migration[_coop].save`; предыдущая атомарная генерация — `.previous`.
Это пользовательские файлы, не содержимое Git/сборки. Миграция сохраняет исходный native run до добавления identity;
неизвестная версия/повреждённые правила или конфликт snapshots отвергаются без подстановки локального меню.
