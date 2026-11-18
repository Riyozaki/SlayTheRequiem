# Evidence Б2.1 — создание героя, 18.11.2026

Исходники: **`f5028e7df2d3ad6b86f185ee37f01c99e932fbde`**, ветка `arena/5906afa4-slaytherequiem`.
[Отчёт реализации](../../stage-two-creation-implementation-2026-11-18.md) ·
[решения](../../../plan/stage-two-decisions-2026-11-18.md).
Это отдельный пакет Б2.1; доказательства Б1 не переписывались и не выдаются за новые результаты.

## Фактически выполнено

| Проверка | Результат | Граница |
|---|---|---|
| [CI 44239623951](https://github.com/Riyozaki/SlayTheRequiem/actions/runs/44239623951) | **4 jobs SUCCESS** | Core / Python sim / content / G0 tooling, не полный мод |
| Remote .NET 9 Core/xUnit | **658 total / executed / passed, 0 failed / skipped** | Семантическая компиляция и исполнение `Core/**/*.cs` и тестов; game-facing C# сюда не входит |
| Локальные команды/проверки | **26/26 exit 0** | Полные выводы в `logs/`, команды/SHA в [checks.json](checks.json) |
| Python | **151 sim + 35 build-contracts + 5 checker tests, OK** | Python-зеркало/инструменты, не бой в C# |
| C# grammar | **277 файлов / 0 ошибок** | tree-sitter, не проверка типов/нативного API |
| Harmony declarations | **125 / 0 ошибок** | Наличие целей в pinned source 0.107.1; не факт установки патчей |
| Локализация | **1265 проверок / 0 ошибок**, по **3713 ключей** RU/EN | Включая конечные динамические CREATION keys и placeholder parity; не formatter/layout игры |
| Content | Каталог, таблицы, пулы, арт/звук актуальны | Card audit: **340 / 0 ошибок / 1 предупреждение** (`RendingCut`); предупреждение не скрыто |
| Native integrity inventory | **77 подготовленных cases**, из них 10 новых creation adapters | `compiled=false`, `executed=false`; это не 77 PASS |

Предыдущий [CI 44238907018](https://github.com/Riyozaki/SlayTheRequiem/actions/runs/44238907018)
на `1124f15479953d64413e95cdc476d601dae84734` тоже прошёл четыре jobs (**654/654 Core**), но финальным для этого
пакета считается **f5028e7 / 658**. Более поздняя документация не меняет эти runtime-байты.

## Происхождение и воспроизведение

- [ci.json](ci.json) — публичная metadata run/jobs и counters из annotation, которую CI сформировал из настоящего
  успешного TRX. Не подсчёт `[Fact]` и не синтетический TRX. Metadata published artifact сохранена; raw TRX в sandbox
  **не скачан**. Storage redirect предыдущего Б2-запроса вернул EOF; повторных download loops нет. Подписанные URLs,
  credentials, чужие saves/DLL и private binlogs не сохранялись.
- [checks.json](checks.json) — 26 реально выполненных команд/проверок и SHA256 355 code/test/tooling/localization inputs.
  Проверки исполнены до commit; **каждый из этих hashes затем побайтно сверен с f5028e7**. Это не полный build passport
  ещё не собранных DLL/PCK. Пути `<repo>` в логах нормализованы; SHA относятся к сохранённым нормализованным логам.
- [native-adapters.json](native-adapters.json) — source inventory и hashes двух fixture files. Подтверждённый план
  позволяет тестировать production effect adapter без поддельного результата выбора; реальные modal/synchronizer,
  disk Save/Continue, replay и кооп этими fixtures не заменяются.
- Reference для Harmony: `zhiyue/sts2-rl-agent@5d874e855ae1d0550d5c30416fadd7dbb0d133b7`, decompiled **0.107.1**.
  Это source reference, не легальные compile DLL. BaseLib networking source рассмотрен на **v3.4.8**;
  итог не отправляет новый неизвестный custom-message type старому peer.

```bash
python3 docs/review/evidence/stage-two-creation-2026-11-18/verify_evidence.py --workspace
```

После дальнейших правок убрать `--workspace`: тогда проверяется исторический Git snapshot, а не совпадение новой
копии со старым кодом. Скрипт проверяет сохранённые bytes/идентичности, **не** делает свежий запрос Actions и не запускает игру.
Нужен локально доступный Git-объект f5028e7. Для повторения команд нужны зависимости `tools/ci/requirements.txt` и отдельный
checkout reference SHA выше. Число секунд в offline logs не является FPS/runtime baseline.

В sim остаются прежние `ResourceWarning` чтения файлов; suite завершён `OK`, skipped suites нет.
Строки `ERROR` у отрицательных build-contract fixtures ожидаемы и не являются выводом настоящего Godot.

## Не выполнено

Полный game-facing C# build / warnings, native integrity/боевые трассы, Godot import, отдельный PCK export/load,
clean install, disk Save/Continue/rollback, реальный RU/EN formatter/layout, клавиатура/геймпад и два клиента.
Ручные/визуальные/двухклиентные проверки по решению автора — **только в конце всего релизного цикла**.
Исторический успешный build/PCK/плейтест пользователя остаётся действительным отдельным результатом;
новую игровую приёмку этим evidence не объявляем. Б2.2–Б2.5 данным пакетом не проверены.
