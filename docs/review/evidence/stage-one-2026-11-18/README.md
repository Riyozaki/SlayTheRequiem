# Evidence блока 1 — 18.11.2026

Основной результат: [реализация «Надёжного ядра»](../../stage-one-implementation-2026-11-18.md).
Это **доказательства выполненной автоматики и идентичности исходников**, не паспорт полной игровой сборки.

## Идентичность и область проверки

- Ветка: `arena/5906afa4-slaytherequiem`; база: `8000886e9c52db78227972bdd4740ee71f7eca03`.
- Проверенный runtime-коммит: **`b82c909375a453dcf96aa459b34993519659bc5b`**.
- [Публичный CI 44229461620](https://github.com/Riyozaki/SlayTheRequiem/actions/runs/44229461620): все **4 jobs SUCCESS**.
- При локальном повторе изменённые executable/runtime/config inputs побайтно сверены с этим коммитом;
  поверх него находились только изменения документации. Логи не выдаются за проверки исходного `8000886`.
- Контракт игры: **0.107.1**; source для проверки Harmony: `zhiyue/sts2-rl-agent@5d874e855ae1d0550d5c30416fadd7dbb0d133b7`.
  Это pinned source reference, **не** легальные reference DLL для компиляции мода.

Исторический успешный build/PCK и плейтест автора на `ec598de` — отдельный действительный результат.
Никакого нового запуска игры, установки мода или публикации release этим evidence не заявлено.

## Фактически выполнено

| Проверка | Результат | Что именно доказано / не доказано |
|---|---|---|
| Remote .NET 9 Core/xUnit | **560 total / 560 executed / 560 passed / 0 failures / 0 skipped** | Реальная компиляция и выполнение Core в GitHub Actions; не game-facing проект |
| Локальные команды | **25 / 25 exit code 0** | 23 проверки/генератора плюс отсутствие diff сгенерированной локализации и whitespace errors; это не 25 игровых прогонов |
| Build/export/install contracts | **35 tests, OK** | Синтетические fixtures инструмента, включая отрицательные сценарии, не настоящий Godot/build/install |
| Numeric rules sim | **150 tests, OK, без skip** | Python-зеркало правил и контент; не C# gameplay hooks |
| C# grammar | **267 файлов / 0 ошибок** | Только tree-sitter syntax, не проверка типов/CS1628/нативной IL-совместимости |
| Harmony declarations | **123 цели / 0 ошибок** | Наличие методов/перегрузок в pinned source; не фактическая установка патчей |
| Локализация | **1206 моделей / 0 ошибок**, по **3593 ключа** RU/EN | Coverage/актуальность генерации, не внешний вид текста в игре |
| Card audit | **340 карт / 0 ошибок / 1 предупреждение** | Статический каталог/спецификации; предупреждение не скрыто и не превращено в балансный PASS |
| Art/audio/data | Все соответствующие команды exit 0 | Размеры/генераторы/путь-таблицы/контент; не ResourceLoader PCK и не прослушивание/визуальная приёмка |
| Game adapter inventory | **67 уникальных source cases**, `compiled=false`, `executed=false` | Только перечисление подготовленных сценариев, не 67 успешных тестов |

Все команды, exit codes, время и последние строки — в [checks.json](checks.json), полные нормализованные выводы —
в `logs/`. В sim есть прежние `ResourceWarning` незакрытых файлов; завершение `OK`, пропущенных suites нет.
Строки `ERROR` в build-contract fixtures намеренно проверяют отрицательные сценарии, не являются логом запущенного движка.
Время offline-команд в JSON **не** является runtime/FPS baseline мода.

### Core/TRX

[ci.json](ci.json) содержит публичную идентичность run/jobs, все реальные counters и metadata загруженного Actions artifact.
Counters взяты из annotation Core job, которую workflow формирует из **настоящего успешного TRX**;
Python fixtures со сконструированными counters сюда не подставлялись.
Сам `core-results-b82c909375a453dcf96aa459b34993519659bc5b` опубликован на странице Actions run.
Скачать его storage redirect в sandbox не удалось; локальный TRX не выдуман и `trx_downloaded_locally=false`.
Подписанные временные redirect URLs в evidence не сохраняются.

## Файлы и воспроизведение

- [checks.json](checks.json), `logs/*.log` — локальный повтор. Нужны Python и
  `pip install -r tools/ci/requirements.txt`; команды выполняются из корня checkout.
  Для Harmony аргумент `<sts2-rl-agent-checkout>/decompiled` — отдельный source checkout **закреплённого SHA выше**.
- [runtime-inputs.json](runtime-inputs.json) — SHA256 изменённых executable/runtime/config inputs, сверенных с CI-коммитом.
  Это не полный build passport и не hashes ещё не существующих новых DLL/PCK.
- [native-adapters.json](native-adapters.json) — имена/методы 67 игровых scenarios и SHA source; все execution flags false.
- [port-manifest.json](port-manifest.json) — **реконструкция области из 63 файлов**, исторически записанной при переносе:
  52 runtime C# + 11 project/test/tooling inputs из `03d636049e2a48101389be7f5856c3976b7730e2`, prior/import SHA256.
  Игнорируемый первоначальный scratch manifest утрачен. Pinned Git trees `8000886` / `03d6360` подтверждают hashes,
  а первый implementation commit `ab3d11b` — итоговые байты: 41 exact-import-content / 21 adapted-content / 1 retained-base
  (manifest мода оставлен на базовых значениях). **Точный первоначальный copy-list независимо не подтверждён**:
  Git не хранит события копирования до commit; реконструкция не выдаётся за восстановленный execution log.
  Import hashes описывают чужой исходник, не наши последующие исправления. Whole-history merge не выполнялся;
  чужие аудиты/арт/весь roadmap не импортировались.

Проверка целостности сохранённых logs/SHA/идентичностей и ссылок документации:

```bash
python3 docs/review/evidence/stage-one-2026-11-18/verify_evidence.py --workspace
```

Нужны Git-объекты указанных commits (в том числе `03d6360` из другой ветки); при их отсутствии проверка явно падает,
а не пропускает hash. После дальнейших изменений кода убрать `--workspace`, чтобы сверять исторический Git snapshot,
а не требовать от новой рабочей копии старые байты. Скрипт **не обращается в сеть и не запускает компиляцию/игру**:
он проверяет сохранённую CI metadata, не утверждает свежую повторную проверку Actions API.
[Сохранённый вывод](logs/verify-evidence.log): 100 изменённых code inputs, 25 logs, 16 Markdown-документов,
215 local targets и 52 pinned Git source targets/line ranges; другие внешние URL здесь не проверялись.

Дальнейший документационный commit может иметь другой SHA при тех же runtime-байтах. Новый игровой build должен
получить **свой** полный паспорт через [G0 pipeline](../../../../tools/g0/README.md); это evidence его не заменяет.

## Не исполнено / ограничения среды

- Локальная .NET/Core-компиляция: SDK в sandbox отсутствует; успешный Core выполнен удалённо, как указано выше.
- Полный C# build проекта мода и warnings с настоящими `sts2.dll` / `0Harmony.dll` / BaseLib.
- MegaDot import, отдельный PCK export и изолированный ResourceLoader: движка и легальных игровых references здесь нет.
- `str_integrity`, factory fixtures, save/Continue/rollback в движке, UI, обычная игра и два клиента.
- Измеренный runtime/performance baseline и воспроизведение исходных `0/2` / draw / `nope`.

Ручной [legal-game-build workflow](../../../../.github/workflows/legal-game-build.yml) реализован, но здесь не dispatch-ился:
настроенный доверенный runner/легальная среда не подтверждены. Отсутствие разрешения на runner inventory не означает,
что runner-ов нет. Подготовка — в [инструкции G0](../../../../tools/g0/README.md).

Ручные/визуальные/двухклиентные сценарии по указанию автора выполняются **только в самом конце всего цикла**.
Наличие prepared fixtures не считается игровой приёмкой. Ни игровые DLL, ни сейвы, private logs/binlog или данные
пользовательской установки в этот каталог не включены.
