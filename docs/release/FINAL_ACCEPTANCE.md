# Финальная приёмка и заморозка кандидата

**Выполнять только в самом конце релизного цикла.** Подготовка матрицы/fixture/архива не является запуском.
[`final-matrix.plan.json`](final-matrix.plan.json) содержит **257 требований, все `NOT_RUN`**, без принятой пары DLL/PCK.
Это не 257 отдельных прохождений: один настоящий run/лог может подтверждать несколько проверок с честными ссылками.

## Что обязательно осталось в матрице

| Группа | Требования | Не подменять |
|---|---:|---|
| Исходная регрессия Б1/Б2 | 103 | source/Core тестами вместо native UI/Continue/receipt/оплаты |
| Полные соло-маршруты | **12**: 3 героя × 2 двери акта 2 × 2 двери акта 4 | смертью, short run, console skip или fixture |
| Short run | 3, отдельно по героям | одним из 12 full routes; Путь после босса 1 без level-8 gate |
| Первые акты / балансные наблюдения | 9 / 30 | synthetic all-win ledgers; наблюдение смерти не означает победу |
| Полный кооп | ≥1 пятиактный run двух клиентов | двумя копиями host-паспорта; targeted COOP-01…07 отдельно |
| Бои | **21** | отсутствие одного босса не прощается его отсутствием в случайном seed |
| Формы / Пути / варианты форм | 7 / 13 / 7 | созданием объекта без проверки входа/выхода, эффектов и Continue |
| Девять направлений | 9 | source recipes вместо реально разыгранного resource/defense/payoff loop |
| UI display/input | 24: 720p/1080p/1440p × 100/125% × RU/EN × keyboard/gamepad | source contact sheet и проверкой одной кнопки |
| Окна | 7 × 100 открытий/закрытий | синтетическими 100 rebind в Core |
| Presentation / финальные reviews | 5 / 6 | наличием настройки, YAML или автоматически выставленным PASS |

Общая база: тестовый профиль/копии сейвов, только BaseLib + мод; зафиксировать игру, hardware/settings,
происхождение/предысторию, seed/сложность, настоящие run IDs. Уточнения боёв —
[`stage-two-combat-passports`](../review/stage-two-combat-passports-2026-11-18.md) в репозитории.
Границы корпуса — исходные планы/регрессия; таблица не снимает ни одну обещанную фазу/контрмеру.

## 1. Получить идентифицированный тестовый архив

Из **чистого** checkout полного финального кода/арта/инструментов:

```bash
python tools/g0/mod_build.py build --game-dir "$STS2_GAME_DIR" --godot "$MEGADOT" --output .arena/builds/final-001
python tools/g0/mod_build.py verify --bundle .arena/builds/final-001
python tools/release/package_release.py candidate --bundle .arena/builds/final-001 --out .arena/releases/final-001.zip
python tools/release/package_release.py verify .arena/releases/final-001.zip
python tools/release/acceptance.py init --archive .arena/releases/final-001.zip --out .arena/acceptance/final-001/matrix.json
```

G0 требует настоящий full mod build/Core/import/export/isolated PCK load; нет легальной среды — **BLOCKED**, не stub.
Пакетирование сверяет Git snapshot с input hash, включая редактируемый `art/`, и runtime hashes. Source archive
не включает `.git`, historical review logs/contact sheets, игру, saves или private binlogs. Одинаковые входы и Python/zlib
дают стабильный ZIP; бит-в-бит повторение компилятора на любой ОС не обещается.

Установить **из этого ZIP**, как описано в INSTALL. Public build passport очищен от private списка модов/путей;
его hash также фиксируется. До этого native-паспорт от private bundle не считается приёмкой публичного кандидата.

## 2. Выполнять и записывать только наблюдённое

`matrix.json` приватный, evidence хранится **внутри того же каталога**. Полные logs/saves/passports с Net IDs
не коммитить/не загружать публично. Для PASS нужны совпадающие runtime-паспорта, реальные native logs,
а для UI — screenshots, для PERF/циклов — frozen `str_metrics`. Два клиента требуют разных `local_net_id` и ролей Host/Client.
Случаи review/build требуют осмысленного review artifact, а не fake native log. Seed и **фактический 32-hex `run_id`**
берутся из паспорта, не придумываются для счётчика маршрутов. Паспорта снимаются в согласованной стабильной точке:
при dropout/refusal дополнительно сохраняются логи самого отказа, затем состояние после восстановления.
Проверяются также версия/hash **реально загруженного** BaseLib и состав партии, не только DLL мода.

Пример **структуры**, не реальный PASS: создать `result.json` рядом с matrix и заполнить после наблюдения:

```json
{
  "status": "BLOCKED",
  "reviewer": "alias",
  "observed_at_utc": "2026-11-18T00:00:00Z",
  "notes": "Пока нет полного build/native evidence; тест не исполнен",
  "executed_in_game": false,
  "context": {},
  "evidence": []
}
```

Для PASS: `context` должен содержать **точные** поля выбранного case из plan плюс `run_id` (реальный nonce паспорта) и `seed`; время/notes —
фактические, `executed_in_game=true`. Evidence задаётся как `{"kind":"native_log","path":"logs/run.txt"}`,
`passport`, `screenshot`, `metrics` или `review`; tool добавит hashes/bytes. Структурированный `str_passport` JSON
сохранять отдельно от окружающих log prefixes. Файлы не должны быть symlink или выходить из private matrix directory.

```bash
python tools/release/acceptance.py record --archive .arena/releases/final-001.zip --matrix .arena/acceptance/final-001/matrix.json --case CARD-01 --result .arena/acceptance/final-001/result.json
python tools/release/acceptance.py check --archive .arena/releases/final-001.zip --matrix .arena/acceptance/final-001/matrix.json
```

Допустимы `NOT_RUN/PASS/FAIL/BLOCKED`; `WAIVED`, skip и пустая выборка не закрывают релиз. Предыдущая попытка сохраняется
в history. Один run не считается дважды внутри одной серии full/short/first-act/balance. Full victory требует записи
реального маршрута без debug rescue. Несколько case могут честно использовать одни и те же logs/screenshots.
**Tool проверяет комплектность/идентичность/hashes записанного наблюдения, не умеет доказать правду человеческой подписи
или сам сыграть бой.** Фабрикованные unit fixtures не используются как evidence игры.

## 3. Native performance — до/после на одной конфигурации

- Один и тот же hardware, игра/BaseLib, режим отображения, scale, язык, локальные flags, seed/setup. Сохранить паспорта
  **обоих** инструментированных builds. Различия gameplay/setup не скрывать под названием baseline.
- Одинаковый warm-up, `str_trace off`, `str_metrics on`, затем `str_metrics reset`; не опрашивать status в каждом кадре.
- Тот же combat stress, большая рука/tooltip; для окон по 100 циклов с checkpoints после стабилизации/освобождения.
  Затем `str_metrics off`, сохранить frozen JSON. Rolling окно frame/операций — последние **1024**, memory/GC deltas —
  от reset до off; instrumentation тоже входит. Считать монотонный рост по серии checkpoints, не одному before/after.
- Сопоставить frame median/p95/max, allocations/GC, managed/native memory, nodes/orphans, reward open/refresh,
  trait rebuild/redraw, act transitions. Проверить актуальные статы после gear/power/Continue/re-entry.
- Если baseline с измерителем недоступен — не заменять его cache microbenchmark. Отдельно задокументировать BLOCKED
  и подготовить корректно инструментированную базу; пока нет native измерений, не заявлять FPS или улучшение памяти.

Паспорта также фиксируют vsync, refresh Hz и frame cap. Сравнение существующих private captures:

```bash
python tools/perf/native_metrics.py --baseline .arena/acceptance/final-001/before.json --candidate .arena/acceptance/final-001/after.json --setup "Фактические fixture, warm-up, длительность/число действий, vsync/cap" --out .arena/acceptance/final-001/comparison.json
```

Validator отвергает active/empty/microbenchmark вместо native sample, неверные allocation/GC deltas, нечисловые timings
и разные machine/display/flags/seed/rules/геройские setup. Разные реальные run nonces/account IDs сами по себе не меняют
конфигурацию. Он **не** проверяет правдивость записи, равенство нагрузки и не превращает положительную/отрицательную delta
в автоматический performance PASS. Длительность и frame counts видны рядом с timings; их нельзя скрыть усреднением.

`PERF-01` и `PERF-04` требуют один frozen `baseline_metrics` + один `metrics` от кандидата, отдельный `review` с разбором
сравнения и `context.performance_setup`. Сохранить дополнительные повторы/checkpoints в private logs/review.
Baseline должен иметь свой до-изменений source hash и согласованный native build proof; копия результата не засчитывается.
Для циклов/длинного run одного финального memory snapshot недостаточно: последовательность стабильных checkpoints и
подписок записывается в native log, reviewer проверяет отсутствие монотонного роста. Новая матрица требует текущий plan
**в самом архиве**, а не изменённый задним числом внешний список.

## 4. Поздние исправления и выпуск

Изменился любой build input, DLL/PCK/manifest, ZIP или требования — старый PASS становится stale. Нужны новый build,
новый ZIP и `init` новой матрицы с `NOT_RUN`. Инструмент намеренно консервативен: автоматически переносить PASS
на новый комплект он **не позволяет**. Определить и перепроверить затронутые сценарии, повторить обязательный маршрутный smoke.
Простое переименование старых файлов или редактирование source SHA не переносит приёмку.

После всех фактических PASS:

```bash
python tools/release/acceptance.py seal --archive .arena/releases/final-001.zip --matrix .arena/acceptance/final-001/matrix.json --out .arena/releases/final-001.acceptance.json
```

`seal` не перепаковывает уже протестированный ZIP: его SHA неизменен. Выход — небольшой allowlisted summary без private
logs/путей/reviewer IDs; это не цифровая подпись и не юридический сертификат. Отдельно владелец проверяет права,
release notes/известные ограничения/отсутствие P0/P1 и даёт разрешение на публикацию. Ни одна команда не делает merge,
push, GitHub release или публикацию автоматически.
