# Б2.2: source/content automation контрмер боссов

Это **не** семантическая сборка полного мода, PCK, native execution/preview trace или visual/co-op acceptance.
Изменённый код — [отчёт пакета](../../stage-two-counterplay-implementation-2026-11-18.md),
все 21 боя сохраняют открытую финальную матрицу в [паспортах](../../stage-two-combat-passports-2026-11-18.md).

## Локально выполнено

[`checks.json`](checks.json) хранит команды/exit codes/SHA256 stdout+stderr и **1802 SHA256 входных файлов**.
Snapshot обновлён на рабочем дереве поверх `7e83e62ea9ddf4a0a4a818a841ac89c7ecd02d18` после исправления
устаревшего Core assertion 199→201; parent SHA не выдаётся за commit позднего исправления. Входы включают код/данные/localization/ресурсы мода, tools, art, дизайн и generated card documents.
Документы самого evidence не хешируются внутрь себя. Все входные и логовые хеши сверены после копирования: **0 несовпадений**.

**29/29 команд PASS:**

- 51 checker regressions, включая 11 wiring-контрактов нового пакета и 10 fabricated fixtures проверки Core transport;
- 35 build/export/install safety contracts, 151 Python rules — не native бои;
- 314 C# grammar files / 0 syntax errors; 133 Harmony targets / 0 ошибок сигнатур из pinned decompiled refs;
- 54 statically typed moves / 201 role rows / 0 ошибок source combat checker;
- 1412 localization checks / 0 ошибок, RU=EN=3955 ключей; regeneration не меняет ни один generated JSON;
- 913 asset models / 1984 references / 0 missing; корректность art paths/размеров, не визуальное качество;
- art-fit: 0 нарушений типа/видимых дублей, 0 повторов внутри мода, 0 исключений с причиной;
- catalog/audit/design tables/pools/relics/source economy/build directions/diff whitespace.

Все логи — [`logs/`](logs/). Один warning card-audit (рекомендательное распределение) не скрыт.
Обнаруженные до финального snapshot ошибки art-fit были исправлены подбором пяти разных Skills из невидимых пулов,
а не отключением проверок. Старый экономический manifest 29/399 не перезаписан и не выдаётся за этот пакет.

## Отдельное исполнение C#

На `ed39e1552fd0c2ec8dd8f8e8bd80781ddf8a7c7b`
[CI 44259875985](https://github.com/Riyozaki/SlayTheRequiem/actions/runs/44259875985) прошёл **все четыре jobs**.
Core check **134650356474** сообщил **858 total / 858 executed / 858 passed / 0 failed / 0 notExecuted**.
Новые `BossCounterplayTests.cs`, session-5 и combat contracts действительно исполнены production Core CI;
локальный 29-командный manifest их **не исполняет** и не вычисляет по `[Fact]`.
[`core/ci.json`](core/ci.json) хранит действительные metadata/counters, а также предыдущую неуспешную попытку
`7e83e62` / CI 44259635997: 857 executed, 856 PASS и единственная ошибка старого count 199→201. Исправление сохраняет
точный count и добавляет явные роли обоих новых тел и typing призрака, не отключает/ослабляет проверку.
TRX локально **не скачан**; API metadata/counters не выдаются за его XML.

**Новый actual synthetic output:** [`core/annotations.json`](core/annotations.json) — исходные bounded notices API,
[`core/verification.json`](core/verification.json) — проверка **8/8 частей / 611069 распакованных байт bundle**,
[`core/core-scenarios.summary.json`](core/core-scenarios.summary.json) — действительная сводка C# runner на `ed39e15`.
Bundle SHA256 `5d046894b9c682e2c5b240770b04c4c6d87a4ab755538b30ea6ff6d0a27ca1aa`.
60 ledgers / 30 same-seed pairs / 300 act rows / **native runs 0**, не реконструкция Python и не 60 побед.
Проверка сохранённых пакетов воспроизводится без GitHub:

```sh
python3 tools/balance/collect_core_evidence.py \
  docs/review/evidence/stage-two-counterplay-2026-11-18/core/annotations.json \
  --commit ed39e1552fd0c2ec8dd8f8e8bd80781ddf8a7c7b \
  --out .arena/stage2/recovered-counterplay-core
```

Повторно полученный `core-scenarios.json` **байт-в-байт совпадает** с полным output
[Б2.5 на `20ddd2f`](../stage-two-economy-2026-11-18/README.md), SHA256
`76afa0960095cacc7254c568273cfcc0fe18209e32cc545b2d6abd6a74c32331`; второй распакованный JSON не дублируется в Git.
Новый summary фиксирует `ed39e15`, это не выдача старой квитанции за новый запуск. Неполные старые notices отвергнуты.

Пять новых fixtures `IntegrityCounterplay.cs` написаны для финальной legal-game автоматизации и **не исполнены**.
Публичный Core CI не компилирует game-facing Monsters/Rules/Powers/UI.

Полная сборка/PCK и реальные столкновения/матрица остаются обязательными. Исторические успешные сборки/PCK/плейтест
автора не отменены отсутствием новой среды. Ручной, визуальный и двухклиентный плейтест — **только в самом конце цикла**.
