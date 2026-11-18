# Б3 — качество и выпуск: реализация и границы проверки, 18.11.2026

**Не объявление принятого игрового релиза.** База перед Б3 — `65af8b3` (документы поверх проверенного `ed39e15`).
Ручная, визуальная **в клиенте** и двухклиентная приёмка — только в самом конце, как решил автор.
Нового полного mod build/PCK/native-исполнения в этой среде нет: легальные references, MegaDot и локальный SDK отсутствуют.
Это **BLOCKED**, не stub/syntax PASS. Исторические успешные сборка/PCK и плейтест пользователя остаются реальными.

## Идентифицированные автоматические результаты

- **[`dc66b0c8398512ec608d7f6e4579af3d72ad984f`, CI 44277797604](https://github.com/Riyozaki/SlayTheRequiem/actions/runs/44277797604)**:
  **879/879 Core**, все **5 jobs success**, включая release contracts. В Core реально прошли **10 cases** production
  double-buffer helper: в том числе два новых exception/Reset cleanup cases и усиленная проверка non-throwing rejection.
  Это не проверка native-подписок или утечек Godot.
- На том же dc66 strict-collected actual Core output: **8 частей / 611069 decoded bytes**, 60 synthetic scenarios /
  300 act rows / 30 pairs, **native 0**. Также реально исполнены и strict-verified **28 C# cache trials** (4 workloads × 7).
- Совпадающий input hash **`d7085986046213e2d690f981a78dcb642cb5949d0f358e51a149549b75640a3e`**:
  **33/33** local checks; **151 sim + 38 G0 + 25 release + 87 checker = 301 PASS**, без skipped suites;
  **319 C# grammar files / 0 errors**, **137 Harmony declarations / 0 missing**; генераторы не изменили build inputs.
  Package/native-metrics unit fixtures — временная синтетика, не реальные DLL/PCK/passports/runs.
- Frozen Git source на dc66 согласован с этим input hash: **1947 files / 80463223 bytes**, docs links/archived plan и
  `source_sha256` referenced documents проверены. Это чтение source snapshot, **не** реальный ZIP/DLL/PCK.
- Сохранены также [`49288c9`, 877/877](https://github.com/Riyozaki/SlayTheRequiem/actions/runs/44275682452) и
  [`6c1234d`, 869/869](https://github.com/Riyozaki/SlayTheRequiem/actions/runs/44268028884) с собственными raw trials;
  allocation-регрессия 6c не скрыта. [Evidence Б3](evidence/stage-three-quality-2026-11-18/README.md) отделено от Б2.
  Промежуточный [CI byte-binding `6eecb8c`](https://github.com/Riyozaki/SlayTheRequiem/actions/runs/44277344159) тоже success;
  dc66 уже включает эту защиту. Непроверенных package/helper изменений после dc66 нет.

Baseline microbenchmark — collection reimplementation, не старый игровой binary. Ни Core, ни microbenchmark не являются
semantic mod build/балансной приёмкой и не запускают Godot hooks. Служебные CI warnings Actions v4/Node 20→24 и runner image
не являются разбором compiler/analyzer warnings полного мода. Последующий documentary/evidence commit не меняет build inputs.

## Б3.1. Собственные материалы, исходники и осмысленный fallback

- 20 крупных силуэтов узлов карты + 20 alpha-масок штатной подсветки; лавки различаются внешней формой, не общим
  прилавком с микрозначком. 14 собственных действий лагеря по полному runtime OptionId.
- Шесть сцен карт: `ShieldBash`, `Backstab`, `ArcaneFocus`, `Whirlwind`, `FocusedAim`, `Firebolt`. Первые две — common
  reward pool, ShieldBash также доступна Legionary; ArcaneFocus/Firebolt — новый старт Регента. Два Path-эталона
  не выдаются за стартовые карты всех трёх героев. FocusedAim механически не требует обязательного арбалета.
- Король-под-Курганом: сидящая/вставшая боевая фаза, 440×520 RGBA. Носитель Зверя: человек/зверь, 300×400 RGBA.
  В каждой паре — одинаковый runtime canvas, привязка к земле, alpha без прямоугольника, 8 px inset.
- Полные AI-мастера, prompts/reference-chain, hashes, crops/flip/matte/fit и runtime-пути:
  [`art/illustrations/`](../../art/illustrations/). SVG-геометрия и receipt: [`art/readable-icons.json`](../../art/readable-icons.json).
  Legacy renderers защищают promoted raster от затирки, deterministic exports проверяются по pixels.
- [`asset-coverage.csv`](../release/asset-coverage.csv): **913 models / 2004 expected slots / 873 own / 1131 vanilla / 0 missing**.
  `own` — наличие файла, не качество/права/runtime PASS. Устаревшая пометка о template у resource icons заменена
  ссылкой на реальные editable SVG. Для vanilla slots записаны конкретные mappings, а не «дорисовать всё потом».

**Права/согласование:** внешних изображений/семплов для новых мастеров не подавалось; производные фаза/зверь опираются
только на сохранённые generated bases. AI output не объявляется exclusive copyright/детерминированной регенерацией
или лицензией проекта. Корневой LICENSE не придуман. [Политика provenance/fallback](../release/ART_PROVENANCE.md)
сохраняет обязательное решение автора перед раздачей. Native Strike/Defend сохраняют собственный игровой арт;
остальной старый SVG/fallback не объявлен автоматически принятым по новому эталону. Жалоба `nope` остаётся runtime-сценарием.

Source previews (не игровые скриншоты, не выбор/одобрение автора):

- [Карта 64/32 px, светлый/тёмный/серый фон](assets/stage-three-2026-11-18/map-native-size.png)
- [Лагерь](assets/stage-three-2026-11-18/camp-symbols.png)
- [Карты, реальные 250×190 после исправления трёх crops](assets/stage-three-2026-11-18/card-scenes-250.png)
- [Пара фаз и пара форм](assets/stage-three-2026-11-18/phase-form-cutouts.png)

Кропнутый лист карт повторно открыт после исправления. Контактные листы/проверка силуэта при 32 px — лишь source prefilter,
не финальная художественная приёмка или native resource loading.

## Б3.2. Движение, полка и звук

Локальная `ReducedMotion` с RU/EN-описанием не меняет RunSettings/save/network schema. Декоративные loops и реакции
привязаны к дереву: OFF останавливает/восстанавливает позу, exit снимает static subscriptions. Покрыты кнопки/двери/toast,
ресурсные шкалы, sprite idle/lunge, оружие/аура, Знаки/сферы, добавочные VFX, summon layout и shop transition.
Native-анимации игры не отключаются; обязательные phase/intent/state тексты не спрятаны за VFX/SFX.

Phase/form texture фиксируется **до** необязательной вспышки, не в отменяемом callback. Поздний Hit не воскрешает
own-art sprite. Оружейная action pose отделена от idle; death/Revive имеют отдельный alpha-слой. Повторные denial/layout
не суммируют смещение. Новых обязательных задержек обычного хода не добавлено; настоящий timing ещё проверяется в клиенте.

Полка хранит клетки физических экземпляров: countdown/ёмкость/tooltip/ожидание подтверждения не зависят от VFX/SFX.
Удаление записи не объявляется возвратом: наблюдаются настоящая Hand/Discard, владелец и тот же бой. Exhaust/передача/cleanup
не получают положительное сообщение. Read-only UI не меняет piles; точность .2s observation/focus/rapid return остаётся native case.

SFX независим от motion/VFX. Per-target throttle дополнен объединением одинакового короткого AoE-hit burst. Свои
AudioStreamPlayer при OFF теряют Autoplay **до Ready** (в том числе deferred AddChild), останавливаются и освобождаются.
FMOD one-shot не получает обещания отмены уже начатого звука. **Actual mix/громкость/multihit ещё не прослушаны и не приняты.**

## Б3.3. Кэш, устранение allocation-регрессии и измеритель

`InvalidatedView<T>` coalesces events, сохраняет invalidation внутри builder, retry после исключения и generation Reset.
`NEnemyTraitChips` подписан на power presence/DisplayAmountChanged, включая traits на нуле. Чистый кадр не делает
collect/sort/hash/QueueRedraw. Bind/exit/re-entry обновляют handlers; высота powers считается от HP, не суммой shifts.
Compact-toggle переукладывает UI без удаления/применения игровых powers. Native subscriptions/gear/load ещё не приняты.

**Реальный 6c benchmark выявил allocation-регрессию**: List+ToArray на rebuild, до **3 073 792 B** при change-every-frame
против 1480 B у старого списка. Результат сохранён, не скрыт за быстрым idle. Поэтому следующий код вводит
`InvalidatedListView<T>`: два переиспользуемых списка, rebuild только inactive buffer, очистка старых references после swap/exit.
Финальный cleanup в `finally` также очищает rejected/throwing scratch, даже если старый builder продолжил добавление после
Reset. Вложенный Refresh не очищает активный buffer внешней сборки. Исключение не публикует partial; Reset не принимает
продолжение старого владельца. Borrowed view действителен до следующего refresh/reset, не immutable snapshot.
**10 helper cases прошли** в составе 879/879; тесты смотрят и на rejected storage до следующего refresh, не только public Value.
Benchmark линкует **тот же production helper**, pin проверяет оба cache sources. На dc66 stress baseline → candidate:
**6.759 → 1.661 ms**, **1480 → 2960 B**, 60000 → 12004 builds; idle **12.991 → .292 ms**, 1480 → 1480 B.
2960 B включают первоначальное заполнение обоих буферов (у baseline один); они одинаковы при 204/500/12004 rebuilds.
GC counts 0 и exact synthetic view equality true во всех workloads. Это не нулевые allocations native UI и не FPS.
Времена сравниваются только попарно внутри одного CI runner, не между 6c/492/dc66 jobs.

`str_metrics` schema 2: до 1024 последних frame/reward/trait samples, allocation/GC/node/memory deltas, CPU/GPU/runtime
и паспорт. OFF замораживает sample. Passport дополнен реальным run nonce, role/local Net ID, vsync/refresh Hz/frame cap.
`tools/perf/native_metrics.py` не генерирует измерения: проверяет frozen captures, arithmetic/counts и совпадение recorded
hardware/display/flags/seed/rules/player setup. PERF-01/04 требуют pre-change baseline/result, отдельный comparison review
и описание нагрузки, не microbenchmark вместо native. Само сравнение не решает, что регрессия приемлема.

Polling шкал/pips/части UI сохранён; «весь UI event-driven» не заявлено. Same-configuration engine baseline/result,
100 оконных циклов, act transitions, stress, 720p/1080p/1440p × scale × RU/EN × keyboard/gamepad ещё открыты.

## Б3.4. Заморозка и полная финальная матрица

[`final-matrix.plan.json`](../release/final-matrix.plan.json) генерируется из полного checklist/каталогов: **257, все NOT_RUN**.
Включает 12 full routes (3 героя × 4 сочетания дверей), 3 short отдельно, 9 первых актов/30 balance observations,
все 21 encounter, 7 forms/13 Paths/7 variants, 9 build directions, полный 5-актный кооп + targeted network/recovery cases,
24 display/input configs, 7 окон × 100 циклов, presentation и финальные reviews. **92 prepared native adapters** — исходники,
не 92 запуска. Наличие fixtures не исключает обещанных фаз/контрмер/выходных критериев Б1/Б2.

`acceptance.py init/record/check/seal` связывает observations с source/input/DLL/PCK/manifest/public-passport/archive/requirements.
Проверяются private evidence hashes, case setup, native seed/run nonce, game/реально загруженный BaseLib, distinct Host/Client
и одинаковая партия; полные solo runs нельзя заменить коопом. Документарный plan сверяется также **в самом архиве**.
`source_sha256` связывает реальные bytes plan/checklist/combat-passport, не только путь: document-only phase/threshold fix
тоже меняет requirements digest и делает старую матрицу stale. Архив проверяет исходные документы повторно.
Сохраняется история попыток, нет WAIVED/skip. Late fix требует нового NOT_RUN matrix, старый PASS не переносится автоматически.
Это validator записей, не доказательство правды утверждения оператора, не бот-плейтестер и не winrate certification.

## Б3.5. Воспроизводимый комплект и документация

`package_release.py candidate/verify` принимает проверенный G0 bundle и clean matching checkout, замораживает source по
source SHA и input digest (включая editable `art/`), ставит фиксированные ZIP metadata/order. Внутри matched DLL/PCK/manifest,
public nested-allowlisted passport, install/compatibility/release notes/provenance/diagnostics/final plan/CSV и source tree.
Guide сохраняет `source/docs/` relative layout; root Markdown links переписываются детерминированно. Size/count/path/symlink/
case-collision/duplicate JSON/tamper/private-file negatives проверены на synthetic fixtures. Source snapshot исключает
historical review evidence/contact sheets, игры/saves/binlogs/caches; новые изображения не скачиваются упаковщиком.

G0 staging сохраняет внешний Compile-link `tools/balance/Trace/Scenario.cs`; SOURCE_ROOTS общие у build/package.
Контракты остаются **session 5 / creation 2 / save 4 / shop 2 / economy 1 / envelope v1**. Manifest пока `v0.0.1`, новый
релизный номер без принятого build не назначен. Вся инструкция собрана в [INSTALL](../release/INSTALL.md),
[COMPATIBILITY](../release/COMPATIBILITY.md), [RELEASE_NOTES](../release/RELEASE_NOTES.md),
[ART_PROVENANCE](../release/ART_PROVENANCE.md), [DIAGNOSTICS](../release/DIAGNOSTICS.md),
[FINAL_ACCEPTANCE](../release/FINAL_ACCEPTANCE.md) и [release tools](../../tools/release/README.md).
Обновлены README/индекс/guide/changelog/bug report: актуальные rules, old saves/config, known limits и privacy.

## Что не закрыто

Нет нового полного Release build/разбора настоящих compiler warnings, export/isolated PCK load/clean install,
принятого архива/реальной candidate-bound матрицы/seal. Нет native performance/арт/audio/UI/Continue/co-op evidence,
12 принятых full routes или финального подтверждения отсутствия критичных P0/P1. Права/лицензия/fallback требуют решения автора.
Следующий шаг среды — легальный full build, затем финальная игровая матрица целиком; source/tool checks её не заменяют.
**Публикация, merge и GitHub release не выполнялись.**
