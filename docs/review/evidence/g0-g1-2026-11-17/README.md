# G0/G1 — evidence рабочих изменений, 17 ноября 2026

Это **не** паспорт собранного DLL/PCK bundle и **не** закрытие G0/G1. Статусы систем и принятые решения —
[единый реестр](../../../plan/implementation-registry.md); legal-game pipeline — [tools/g0](../../../../tools/g0/README.md).
Исторический аудит `8000886` находится в [соседнем каталоге](../audit-2026-11-17/README.md), его цифры не заменяют новые результаты.

## Исходный полный safe batch

**28 проверок, 0 неуспешных.** Это исходный прогон до последующей узкой правки даров; её отдельные результаты приведены ниже. Фактический HEAD прогона — `ec598de3375af1dd61e3c6c0a7b524018187f7e6`,
`dirty=true`; это рабочая копия с последующими изменениями, не проверка одного неизменённого коммита.
Content hash входов: `d6e4d2568e089b861c2ad58aa4bda46a7e8e87bc3809b04a72ec0583e7f0ec50`.

| Слой | Полученный результат | Ограничение |
|---|---|---|
| Python rules sim | **150/150**, OK | Зеркало правил, не игровые hooks; есть прежние ResourceWarning о незакрытых файлах |
| Python G0 contracts | **29/29**, OK | Tiny synthetic bundles, проверки installer/probe/schema; **не build/export** |
| C# syntax | **250 файлов, 0 ошибок** | Tree-sitter, не типы/компилятор/nullability |
| Harmony targets | **112 целей, 0 ошибок** | Декларации против закреплённой декомпиляции, не runtime IL/patch order |
| Локализация | **3579 ключей на язык**, 1204 модели / 0 ошибок; повторная генерация идемпотентна | Не визуальная проверка UI |
| Контент/ассеты | Генераторы, пулы, каталоги, события и manifest прошли; manifest: **1951 ожидаемый ресурс, 0 missing** | Не ResourceLoader экспортированного PCK; audit карт сохраняет 1 предупреждение `RendingCut` |
| Целостность файлов | `git diff --check` и локальные Markdown-пути без ошибок | Не semantic build/игровой результат |

## Узкий повторный прогон: write-ahead одноразовых даров

[follow-up-checks.json](follow-up-checks.json): **7 безопасных проверок, 0 неуспешных**; тот же HEAD, `dirty=true`.
Content hash: `1603a80599449af4ab07ad63e951abe98110501c928bf14fcd60767ee9fc6466`.
От исходного batch изменились только **3 runtime C#-файла и игровой harness**; их before/after SHA перечислены в JSON.
Исходные 28 проверок не выдаются за заново выполненные на этом hash.

Повторно получены: syntax **250/0**, Harmony **112/0**, G0 synthetic **29/29**, rules sim **150/150 без skip**
(прежние ResourceWarning остаются), localization coverage **1204/0**, `git diff --check` без ошибок.
Седьмая проверка сверяет только определения/уникальность и manifest **55 source cases**; это **не** запуск C#.

Право на одноразовый дар теперь записывается до первого obtain/veto hook. Pending restore использует отдельную
копию Props и проверяет полный round-trip до открытия выбора. Изменившаяся запись/коллизия q: не удаляется молча;
поздняя ошибка финализации уже выданного дара оставляет claim и Block. Добавлены 7 сценариев до/после вставки,
полного/unknown payload и callback/stale/collision. Они **не скомпилированы и не запущены**.

## Состав

- `checks.json` — команды, время и exit codes исходного полного safe batch.
- `check-output.txt` — их вывод; source-only сценарии не помечаются выполненными.
- `source-snapshot.json` — фактические git SHA/dirty/ветка, content hash входов, tool versions и границы среды исходного batch.
- `source-files.json` — SHA256 входов мод/tooling исходного batch; delta следующей правки сохранена отдельно.
- `follow-up-checks.json` — актуальный source fingerprint, delta четырёх файлов и короткий вывод узких повторных проверок; не build passport.
- `integrity-cases.json` — актуальные имена **55** сценариев игрового адаптера и SHA исходника, каждый с `executed=false`.
- `markdown-links.json` — локальные ссылки из Markdown, без загрузки внешних страниц.

В source присутствуют: full legal-game build/export/install CLI, inventory/operation gate с Pending/Block,
opaque chest codec v2, instance identity и проверка полного payload, observed transfers/finalizers,
reward retry/paid-offer/deferred claims, run/owner/revision/receipts, factory/trace и диагностический паспорт.
Последние исправления дополнительно защищают callback-изменения выбранной записи сундука и посторонних записей,
не допускают shallow alias SavedProperties при restore/preview, завершают once-per-visit Trade даже при позднем
исключении удаления и не снимают shop Pending до peer agreement. Retries переотправляют immutable receipts без повторного эффекта.

## Что НЕ было выполнено

- Компиляция всего игрового csproj и локальный `dotnet test` нового Core-кода.
- Настоящие Godot import / PCK export / isolated ResourceLoader. Python fixture не содержит рабочей игры/engine.
- Запуск `str_integrity` / `str_factory`: source counts — не PASS. JSON player round-trip внутри harness — не disk Continue.
- UI reward/holders/«0/2»/`nope`/draw/shout, визуальная проверка RU/EN в 1080p/4K, измерение FPS.
- Два клиента, реальные native selectors/messages и save/quit/Continue/rollback.

Причина: в среде нет .NET SDK 9, MegaDot и предоставленной легальной установки игры. Декомпиляция закреплённой
0.107.1 используется только для статической сверки Harmony/API, не вместо игровых references.
Полные следующие шаги и матрица приёмки — [G0/G1](../../../plan/stabilization-roadmap-2026-11-17.md).

Игровые DLL/лицензированные assets, saves, binlogs и локальные props сюда не включены. Source hash — fingerprint,
не криптографическая подпись происхождения. Наличие кодовых исправлений и зелёных safe checks не означает «весь мод собран/работает».
