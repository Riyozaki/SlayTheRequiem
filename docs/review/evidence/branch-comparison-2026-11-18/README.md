# Evidence: ревью ветки другого агента, 18.11.2026

Основной вывод: [сравнительное ревью](../../other-agent-branch-review-2026-11-18.md).
Это **проверки исходников/инструмента**, не паспорт собранного мода и не приёмка исправлений.

## Идентичность

- Рабочая ветка: `arena/5906afa4-slaytherequiem`, HEAD `8000886e9c52db78227972bdd4740ee71f7eca03`.
- Проверенная ветка: `arena/1d7c3746-slaytherequiem`, tip `03d636049e2a48101389be7f5856c3976b7730e2`.
- Merge base: `ec598de3375af1dd61e3c6c0a7b524018187f7e6`.
- История была shallow; перед вычислением предка получена недостающая история main. Ветки не объявляются unrelated.
- Тесты запускались из отдельного дерева `git archive 03d6360`, не из нашего HEAD. Ветка не переключалась.
  Snapshot всех 112 отличающихся файлов сверён по SHA256 с проверенным Git-коммитом.

## Результаты

| Команда / проверка | Результат | Ограничение |
|---|---|---|
| `python3 -m unittest discover -s tools/g0 -v` | **29 tests, OK** | Synthetic preflight/bundles/installer; не build/export |
| `python3 -m unittest discover -s tools/sim -v` | Первый запуск: **149 PASS + 1 skip**; после numpy полный повтор: **150 tests, OK, без skip** | Существующее зеркало правил/контент, не C# hooks |
| `python3 tools/ci/check_csharp_syntax.py` | **250 files, 0 errors** | Только грамматика, не semantic C# compilation |
| `python3 tools/ci/check_loc.py` | **1204 модели, 0 ошибок** | Не визуальная проверка, не PCK loading |
| Integrity source/manifest | **55 уникальных source cases**, SHA исходника совпадает с сохранённым manifest | `compiled=false`, `executed=false` остаются false |
| Git tree comparison | **112 файлов, +18150/−605**; runtime C#: **52, +5125/−504** | Оценка масштаба/overlap, не корректность кода |
| `gh` PR/runs | Оба ответа `[]` на момент запроса | Не доказательство отсутствия любых частных сборок/плейтестов |

Инструменты среды: Python и версии библиотек записаны в `checks.json`; tree-sitter **0.26.1**,
tree-sitter-c-sharp **0.23.5**, numpy **2.4.6**. Первый skip был вызван только отсутствием numpy для проверки звуков.
В обоих sim-логах есть прежние `ResourceWarning` незакрытых файлов. Абсолютный путь рабочего checkout в логах заменён на `$REPO`.

## Файлы

- [checks.json](checks.json) — команды, время/exit codes, SHA обоих деревьев/предка, source-case manifest,
  SHA256 112 файлов и проверки неизменённых участков.
- [tree-diff.txt](tree-diff.txt) — `--stat` и `--numstat` сравнения деревьев.
- [reference-contracts.json](reference-contracts.json) — закреплённые внешние source contracts, использованные для проверки
  CombatState, клонирования, native reward и точки снятия отпечатка; только URL/hashes, не game DLL.
- [pull-requests.json](pull-requests.json), [ci-runs.json](ci-runs.json) — результаты запросов `gh`.
- [logs/g0-contracts.log](logs/g0-contracts.log), [logs/csharp-syntax.log](logs/csharp-syntax.log),
  [logs/localization.log](logs/localization.log) — фактический вывод.
- [logs/rules-sim-without-numpy.log](logs/rules-sim-without-numpy.log) — первоначальный прогон с одним skip.
- [logs/rules-sim.log](logs/rules-sim.log) — полный повтор без skip после установки numpy.

## Воспроизведение без переключения рабочей ветки

Понадобится Git-объект проверенного коммита и Python-зависимости. В отдельный **новый пустой** игнорируемый каталог
извлечь `git archive 03d636049e2a48101389be7f5856c3976b7730e2`, затем выполнить команды таблицы из корня извлечённого дерева.
В нём нет собственного `.git`: команды Git оттуда могут обращаться к родительскому checkout.
**Build pipeline из такого snapshot в этом ревью не запускался.** Его source-identity предполагает настоящий checkout.

Не запускать установку против каталога пользовательской игры ради повторения synthetic tests: unittest использует
свои временные fixtures и не требует игровых файлов.

## Чего нет

Нет .NET build/xUnit нового кода, MegaDot import/export, ResourceLoader полученного PCK, игровых `str_integrity` /
`str_factory`, disk save/Continue/rollback, двух клиентов и визуальной приёмки. Отсутствуют SDK 9/движок/игровые references.
Грамматический PASS не обнаруживает оставшийся ref-capture CS1628. Новые сценарии в source не отмечаются выполненными.
Ветка не сливалась; runtime-файлы рабочего HEAD не изменялись.
