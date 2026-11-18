# Б3 — evidence исходников и автоматических проверок, 18.11.2026

**Не игровой релиз и не native-приёмка.** Здесь нет нового DLL/PCK, игры, private saves/binlogs, принятого ZIP или seal.
Все 257 финальных требований пока `NOT_RUN`. Исторические успешные сборка/плейтест пользователя остаются реальными,
но не проверяют новые Б1–Б3. Ручной/визуальный-в-клиенте/двухклиентный тест — только в самом конце.
Историческое evidence Б2 не переписывалось.

## Идентичность

| Корпус | К чему относится | Результат / граница |
|---|---|---|
| `ci-6c1234d/run.json` | **`6c1234da4f85db0ab447c1cbddd7c2d5288df0aa`**, [CI 44268028884](https://github.com/Riyozaki/SlayTheRequiem/actions/runs/44268028884) | Пять jobs success; Core **869/869**; это не полный mod build |
| `ci-6c1234d/core-annotations.json`, `core/` | Тот же SHA, check **134677329674** | Actual C# Core output, strict 8 частей / 611069 decoded bytes; 60 synthetic scenarios/300 act rows/30 pairs; **native 0** |
| `ci-6c1234d/views-annotations.json`, `views/` | Тот же SHA, check **134677329491** | Все **28 actual C# trials**, medians проверены заново из raw data; не Godot/FPS/native invalidation |
| `local-source/checks.json` + `logs/` | Working source поверх 6c, input **`d64e0c5f3f311dcd744a407146d7a7d2e7d07e15298a51c93f377e3ddb480716`**, 1877 build inputs | **33/33** safe local checks, actual C#/full build/native не запускались локально |

Локальные unit suites: **151 sim + 38 G0 + 24 release + 87 checker = 300 PASS**, без skipped tests.
C# grammar: **319 files / 0 errors**; Harmony declaration targets: **137 / 0 missing** по pinned native 0.107.1 source.
Генераторы повторно проверены: ни один build input не изменился (`source-generation-idempotence.log`).
Dummy bytes/archive/passports/metrics в unit tests — временные явно синтетические fixtures, не evidence запуска игры.
Expected duplicate-ZIP warning и намеренные ERROR-строки в отрицательных G0 fixtures — тест отказа инструмента.
Пути workspace в local logs заменены `<repo>`; оригиналы ignored в `.arena/stage3/local-01`.

**Эти локальные изменения добавляют double-buffer helper, финальный package/matrix/metrics gate и паспортные поля.**
CI на 6c их **не покрывает**. Новый hosted C# checkpoint будет добавлен отдельно после фактического исполнения.
`files.sha256.json` — проверяемые hashes файлов evidence, не подпись, не certification прав/правдивости наблюдений.

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

Видна **allocation-регрессия** старого List+ToArray builder; она не скрыта за более быстрым idle. Поэтому следующий
исходный пакет вводит общий production/benchmark `InvalidatedListView<T>` с двумя переиспользуемыми списками и
отдельными xUnit-тестами reset/reentry/exception/роста/очистки references. Пока свежий CI не исполнен, никаких его
цифр здесь нет. Native event subscriptions/gear/load, allocations всей игры, 100 оконных циклов и аппаратный FPS
этим benchmark не проверяются; для них нужен frozen `str_metrics` baseline/result и реальный клиент.

## Повторная проверка сохранённого CI без artifact-host download

Из matching checkout (collectors также умеют читать исторический 6c без нового helper):

```bash
python tools/balance/collect_core_evidence.py docs/review/evidence/stage-three-quality-2026-11-18/ci-6c1234d/core-annotations.json --commit 6c1234da4f85db0ab447c1cbddd7c2d5288df0aa --out .arena/recheck-core-6c
python tools/perf/collect_views.py --annotations docs/review/evidence/stage-three-quality-2026-11-18/ci-6c1234d/views-annotations.json --commit 6c1234da4f85db0ab447c1cbddd7c2d5288df0aa --out .arena/recheck-views-6c
```

Оба output должны быть новыми. Collectors отвергают неполные/повторные/смешанные packets, source mismatch,
неверные medians/types и oversized decompression; они ничего не реконструируют по выдуманным значениям.
Перенос источника/хэша не превращает синтетику в native-прогон.
