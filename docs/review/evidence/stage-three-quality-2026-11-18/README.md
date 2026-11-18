# Б3 — evidence исходников и автоматических проверок, 18.11.2026

**Не игровой релиз и не native-приёмка.** Здесь нет нового DLL/PCK, игры, private saves/binlogs, принятого ZIP или seal.
Все 257 финальных требований пока `NOT_RUN`. Исторические успешные сборка/плейтест пользователя остаются реальными,
но не проверяют новые Б1–Б3. Ручной/визуальный-в-клиенте/двухклиентный тест — только в самом конце.
Историческое evidence Б2 не переписывалось.

## Предсборочная доделка — актуальный `3d9acfb`

**[`3d9acfb7e4cca78831fad63837366b31edb05637`, CI 44282711349](https://github.com/Riyozaki/SlayTheRequiem/actions/runs/44282711349)** —
все **6 jobs success**, **879/879 Core**. Input hash
**`bb73a110b17a503d70890f8795a580c39286dedc3b9fd599a7e8df0acfa11952`**, 1889 build inputs.
Новая проверка не наследует старые PASS dc66/98. `files.sha256.json` содержит **189** hashes;
все прежние **136** сохранены без изменения bytes.

| Новый корпус | Идентичность / результат | Граница |
|---|---|---|
| `ci-3d9acfb/run.json`, `core-tests.summary.json` | CI **44282711349**, check **134727331681**, 879/879 | Actual hosted xUnit, не full-mod game-side compile |
| `ci-3d9acfb/core-annotations.json`, `core/` | Strict **8 частей / 611069 decoded B**, 60 scenarios / 300 act rows / 30 pairs | Actual Core rules, synthetic workload, native 0 |
| `ci-3d9acfb/views-annotations.json`, `views/` | Check **134727331700**, все **28** actual C# trials и medians проверены | Не engine/FPS/native-event acceptance; свой runner/pair |
| `ci-3d9acfb/presentation-annotations.json`, `presentation/` | Check **134727331596**, **12 групп PASS** в official public Godot 4.5.1 mono | Actual production node/focus helpers; **не** MegaDot, полный мод, rendered UI/art/audio, игровые window cycles или FPS |
| `ci-3d9acfb/source-snapshot.json` | **1959 files / 80518698 B**, input hash выше, plan/docs/requirement bytes согласованы | Frozen Git source only, не реальный bundle/ZIP |
| `local-prebuild/checks.json`, `logs/` | Matching input: **34/34**, **322 Python PASS** = 151 sim + 38 G0 + 25 release + 108 checker | Grammar **323/0**, Harmony **136/0**, пять declaration projects/0; генераторы idempotent. Local .NET/full mod/native не запускались |
| `prebuild/declarations-before.log`, `duplicate-reproduction.json` | Новый gate реально нашёл duplicate non-partial `PaperDollDeathPatch`; исходный файл/hash привязан к `98d671b` | Expected source-only failure до исправления, не semantic mod compilation |
| `prebuild/attempts.json` | Два failed harness CI (`f2eb029`, `166564f`) → новый полный success `3d9acfb` | Ошибки harness не спрятаны; failed run не засчитывается как успешная проверка |

Godot report содержит hashes **восьми** actual production/harness/scene/workflow inputs, сверенные с Git objects.
ZIP official mono закреплён SHA256 `b61697dd6921da76e3d2358ab592263dfaae398d0ac1229e82eab7e4a7c36cbf`;
reported executable SHA256 `cf499495ba071c5974e68602f14da9860269cc73bda7dc495ff28128e7972b64`.
Бинарник **не** скачивался повторно локально: receipt опирается на actual successful hosted job и pinned workflow,
это не signed provenance. 100 same-frame named replacements / 100 clear cycles относятся к helper scene,
**не** к 100 открытиям игровых окон. `RequiemKit`/PaperDoll/паспорт целиком здесь не компилировались и не рендерились.

Новые исходники исправляют duplicate patch, same-frame overlay replacement/death alpha, live PaperDoll setting,
exit pose, eligible/owner-scoped deferred focus, empty-shelf capacity и responsive native-modal passport/recovery.
Точные границы/команды — [отчёт Б3](../../stage-three-quality-implementation-2026-11-18.md) и
[передача на сборку](../../../release/BUILD_HANDOFF.md). Source/tool/isolated engine проверки не закрывают 257 game requirements.

## Историческая идентичность dc66/492/6c

| Корпус | К чему относится | Результат / граница |
|---|---|---|
| `ci-dc66b0c/run.json`, `core-tests.summary.json` | **`dc66b0c8398512ec608d7f6e4579af3d72ad984f`**, [CI 44277797604](https://github.com/Riyozaki/SlayTheRequiem/actions/runs/44277797604) | **Все 5 jobs success; 879/879 Core**, включая 10 helper cases; не full mod build |
| `ci-dc66b0c/core-annotations.json`, `core/` | Тот же SHA, check **134710277491** | Strict **8 частей / 611069 bytes** actual Core output; **native 0** |
| `ci-dc66b0c/views-annotations.json`, `views/` | Тот же SHA, check **134710278153** | **28 actual C# trials** production helper после cleanup fix; verified raw/medians/source hashes |
| `ci-dc66b0c/source-snapshot.json` | Clean dc66, input **`d7085986046213e2d690f981a78dcb642cb5949d0f358e51a149549b75640a3e`** | **1947 source files / 80463223 B**, digest/docs/plan/document bytes согласованы; не bundle/ZIP |
| `local-buffer-cleanup/checks.json` + `logs/` | Matching dc66 input **`d7085986046213e2d690f981a78dcb642cb5949d0f358e51a149549b75640a3e`**, 1877 files | **33/33** safe checks, **301 Python unit PASS**; actual C#/full build/native не запускались локально |
| `ci-49288c9/run.json`, `core-tests.summary.json` | **`49288c96ed171c01b7197777ace9c2892392261c`**, [CI 44275682452](https://github.com/Riyozaki/SlayTheRequiem/actions/runs/44275682452) | **Пять jobs success; 877/877 Core**. Новые release contracts также реально исполнены; не full mod build |
| `ci-49288c9/core-annotations.json`, `core/` | Тот же SHA, check **134703074632** | Strict **8 частей / 611069 bytes**; actual Core output, **native 0** |
| `ci-49288c9/views-annotations.json`, `views/` | Тот же SHA, check **134703074230** | Все **28 actual C# trials** double-buffer helper; raw medians/source hashes проверены |
| `ci-49288c9/source-snapshot.json` | 492, тот же input hash, clean checkout | **1947 source files / 80430384 B**, digest/docs/plan согласованы; это не реальный bundle/ZIP |
| `ci-6c1234d/run.json` | **`6c1234da4f85db0ab447c1cbddd7c2d5288df0aa`**, [CI 44268028884](https://github.com/Riyozaki/SlayTheRequiem/actions/runs/44268028884) | Пять jobs success; Core **869/869**; это не полный mod build |
| `ci-6c1234d/core-annotations.json`, `core/` | Тот же SHA, check **134677329674** | Actual C# Core output, strict 8 частей / 611069 decoded bytes; 60 synthetic scenarios/300 act rows/30 pairs; **native 0** |
| `ci-6c1234d/views-annotations.json`, `views/` | Тот же SHA, check **134677329491** | Все **28 actual C# trials**, medians проверены заново из raw data; не Godot/FPS/native invalidation |
| `local-source/checks.json` + `logs/` | Working source поверх 6c, input **`d64e0c5f3f311dcd744a407146d7a7d2e7d07e15298a51c93f377e3ddb480716`**, 1877 build inputs | **33/33** safe local checks, actual C#/full build/native не запускались локально |

Исторический `local-buffer-cleanup/` для dc66: **151 sim + 38 G0 + 25 release + 87 checker = 301 PASS**, без skipped tests.
Исторический `local-source/` соответствует 492: 24 release / 300 unit PASS; он не переписан результатом новых тестов.
C# grammar: **319 files / 0 errors**; Harmony declaration targets: **137 / 0 missing** по pinned native 0.107.1 source.
Генераторы повторно проверены: ни один build input не изменился (`source-generation-idempotence.log`).
Dummy bytes/archive/passports/metrics в unit tests — временные явно синтетические fixtures, не evidence запуска игры.
Expected duplicate-ZIP warning и намеренные ERROR-строки в отрицательных G0 fixtures — тест отказа инструмента.
Пути workspace в local logs заменены `<repo>`; оригиналы ignored в `.arena/stage3/local-01`, `local-02`, `local-03`.

`local-source/` совпадает с **492**, `local-final/` — с **6eec**, `local-buffer-cleanup/` — с **dc66**.
CI на dc66 покрывает последний helper cleanup и source-byte gate требований, не наследует старые PASS только по имени.
Ни один из этих CI **не** семантически компилирует/запускает game-side C#. `.gitattributes` сохраняет точные bytes
этого evidence, в том числе terminal blank line C# Markdown, вместо изменения файла и нарушения его receipt.
`files.sha256.json` — проверяемые hashes, не подпись, не certification прав/правдивости наблюдений.
Служебные CI warnings о Node 20→24/runner image не являются разбором compiler warnings полного mod build.

## Последующая финальная защита документов требований

`source_sha256` каждого case теперь связывает bytes plan/checklist/combat-passport, а архив проверяет их снова.
Исправление только паспортной фазы/порога тоже делает старую матрицу stale; filename не считается неизменным содержимым.
`local-final/` — **33/33**, **151 sim + 38 G0 + 25 release + 87 checker = 301 PASS**, input
**`30fd08a901ccb755f6274c68c1a283a829e12d390fa05705936b89e23391ba27`** — промежуточный checkpoint byte-binding,
на котором C# ещё совпадал с 492. Его [CI 44277344159](https://github.com/Riyozaki/SlayTheRequiem/actions/runs/44277344159)
успешен; последующий dc66 заново исполнил gate вместе с C# cleanup. Название `local-final` не означает native PASS.

## Реальный C# microbenchmark на 6c — не результаты нового double buffer

.NET **9.0.23**, Ubuntu **26.04.1 LTS**, X64, 4 logical CPUs. Пять actors × 24 synthetic powers × 12000 условных
frames, 7 trials/workload; чередование порядка baseline/candidate, одинаковый warm-up, tiered compilation OFF.
Baseline — **collection reimplementation** `65af8b30bb19ed86fd7e3a1ae45d319df45958d6`, не исходный игровой binary.
Untimed проверка membership/rank/amount/order на каждом synthetic кадре и count checksum прошла; GC counts в этих trials — 0.

| Workload | Whole-workload median ms, baseline → 6c | Allocated B | Rebuilds |
|---|---:|---:|---:|
| idle | 15.678 → 0.251 | 1480 → 2240 | 60000 → 5 |
| change/60 | 7.552 → 0.349 | 1480 → 49776 | 60000 → 204 |
| rebind/120 | 15.553 → 0.511 | 1480 → 224000 | 60000 → 500 |
| change every frame | 8.369 → 5.109 | 1480 → **3073792** | 60000 → 12004 |

Видна **allocation-регрессия** старого List+ToArray builder; она не скрыта за более быстрым idle. Поэтому 492 ввёл
общий production/benchmark `InvalidatedListView<T>` с двумя переиспользуемыми списками. Его **8 новых xUnit cases PASS**
в составе 877/877 проверяли published state/retry/рост/обычный swap/reset. Ссылки внутри rejected scratch отдельно проверены
и исправлены только на dc66; старому checkpoint эта новая гарантия не приписана. Это всё ещё не native lifecycle.

## Свежий C# checkpoint 492 — фактически исполнен

Тот же протокол/.NET 9.0.23/Ubuntu 26.04.1/X64/4 logical CPUs, **свой** CI runner и собственная baseline/candidate пара.
Времена 6c и 492 не сравниваются как одна hardware session. `list_cache_sha256` дополнительно pin-ит реальный helper;
trials SHA **`5e3ce4036beef5edbe4e51aa8df0cf48dd1d3d3d9b191b93e07fb9087d8af18b`**.

| Workload | Whole-workload median ms, baseline → 492 | Allocated B | Rebuilds |
|---|---:|---:|---:|
| idle | 11.281 → 0.199 | 1480 → **1480** | 60000 → 5 |
| change/60 | 5.223 → 0.218 | 1480 → **2960** | 60000 → 204 |
| rebind/120 | 10.813 → 0.284 | 1480 → **2960** | 60000 → 500 |
| change every frame | 5.975 → 1.384 | 1480 → **2960** | 60000 → 12004 |

Exact synthetic membership/rank/amount/order и checksums совпадают во всех 28 trials; GC counts 0. Капаситеты двух
буферов выделяются при первом заполнении, поэтому 2960 B всё ещё выше baseline 1480 B и не объявлены нулевыми allocations.
Но они не растут с 204 до 12004 rebuilds. Вся prepare/JIT/forced GC — вне timed interval по исходнику benchmark.
**Никаких native/FPS/engine-GC выводов** отсюда нет. Subscriptions/gear/load, 100 оконных циклов и общая память игры
проверяются только frozen `str_metrics` baseline/result в реальном клиенте.

## Финальный C# checkpoint dc66 — cleanup и новые регрессии исполнены

При exception или generation rejection scratch раньше мог удерживать ссылки до следующего Refresh/Reset, включая
добавленные **после** Reset. Теперь внешний Refresh освобождает его в `finally`, а вложенный вызов не очищает активный buffer.
Два новых theory cases + усиленный non-throwing rejection входят в **879/879**; **10 helper cases** всего.
Тесты инспектируют rejected storage сразу, без retry/GC, и проверяют сохранение опубликованного списка/повторное использование.
Это контракт списков, **не** доказательство отсутствия утечек игры/handlers/Godot.

Свой runner, тот же протокол/.NET 9.0.23/Ubuntu 26.04.1/X64/4 logical CPUs; baseline и candidate измерены попарно.
Trials SHA **`c31324a40823253c38d9476550248029f61df6aca91fea4711ceae7ffe921f85`**, exact equality true, GC counts 0.

| Workload | Whole-workload median ms, baseline → dc66 | Allocated B | Rebuilds |
|---|---:|---:|---:|
| idle | 12.991 → 0.292 | 1480 → **1480** | 60000 → 5 |
| change/60 | 5.991 → 0.303 | 1480 → **2960** | 60000 → 204 |
| rebind/120 | 12.888 → 0.385 | 1480 → **2960** | 60000 → 500 |
| change every frame | 6.759 → 1.661 | 1480 → **2960** | 60000 → 12004 |

Это не сравнение скорости dc66 с 492 на одном железе; runner sessions разные. Дополнительный cleanup не добавил
per-rebuild allocations в данном workload, но не обещает zero-allocation UI, engine FPS или native performance PASS.

## Повторная проверка сохранённого CI без artifact-host download

Из matching checkout (collectors также умеют читать исторический 6c без нового helper):

```bash
python tools/balance/collect_core_evidence.py docs/review/evidence/stage-three-quality-2026-11-18/ci-6c1234d/core-annotations.json --commit 6c1234da4f85db0ab447c1cbddd7c2d5288df0aa --out .arena/recheck-core-6c
python tools/perf/collect_views.py --annotations docs/review/evidence/stage-three-quality-2026-11-18/ci-6c1234d/views-annotations.json --commit 6c1234da4f85db0ab447c1cbddd7c2d5288df0aa --out .arena/recheck-views-6c
python tools/balance/collect_core_evidence.py docs/review/evidence/stage-three-quality-2026-11-18/ci-49288c9/core-annotations.json --commit 49288c96ed171c01b7197777ace9c2892392261c --out .arena/recheck-core-492
python tools/perf/collect_views.py --annotations docs/review/evidence/stage-three-quality-2026-11-18/ci-49288c9/views-annotations.json --commit 49288c96ed171c01b7197777ace9c2892392261c --out .arena/recheck-views-492
python tools/balance/collect_core_evidence.py docs/review/evidence/stage-three-quality-2026-11-18/ci-dc66b0c/core-annotations.json --commit dc66b0c8398512ec608d7f6e4579af3d72ad984f --out .arena/recheck-core-dc66
python tools/perf/collect_views.py --annotations docs/review/evidence/stage-three-quality-2026-11-18/ci-dc66b0c/views-annotations.json --commit dc66b0c8398512ec608d7f6e4579af3d72ad984f --out .arena/recheck-views-dc66
```

Все output должны быть новыми. Collectors отвергают неполные/повторные/смешанные packets, source mismatch,
неверные medians/types и oversized decompression; они ничего не реконструируют по выдуманным значениям.
Перенос источника/хэша не превращает синтетику в native-прогон.
