# Б2.2: source/content automation контрмер боссов

Это **не** семантическая сборка полного мода, PCK, native execution/preview trace или visual/co-op acceptance.
Изменённый код — [отчёт пакета](../../stage-two-counterplay-implementation-2026-11-18.md),
все 21 боя сохраняют открытую финальную матрицу в [паспортах](../../stage-two-combat-passports-2026-11-18.md).

## Локально выполнено

[`checks.json`](checks.json) хранит команды/exit codes/SHA256 stdout+stderr и **1802 SHA256 входных файлов**.
Snapshot сделан на рабочем дереве поверх `20ddd2f8eb27851c85870809f8205dbd5d160329`; parent SHA не выдаётся за
commit поздних изменений. Входы включают код/данные/localization/ресурсы мода, tools, art, дизайн и generated card documents.
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

Новые `BossCounterplayTests.cs` и session-5 regressions входят в проект Core CI. Для этого рабочего checkpoint
результат фиксируется только после реального запуска CI; этот manifest их **не исполняет** и не считает по `[Fact]`.
Пять новых fixtures `IntegrityCounterplay.cs` написаны для финальной legal-game автоматизации и **не исполнены**.
Публичный Core CI не компилирует game-facing Monsters/Rules/Powers/UI.

Предыдущий подтверждённый CI `20ddd2f` — **778/778 Core**. Полный реальный synthetic economic output с него теперь
сохранён [в evidence Б2.5](../stage-two-economy-2026-11-18/README.md): 8/8 частей/SHA проверены,
60 ledgers / 30 same-seed pairs / 300 act rows / **native runs 0**, не реконструкция Python и не 60 побед.
Старые неполные transport notices не использованы как полный output.

Полная сборка/PCK и реальные столкновения/матрица остаются обязательными. Исторические успешные сборки/PCK/плейтест
автора не отменены отсутствием новой среды. Ручной, визуальный и двухклиентный плейтест — **только в самом конце цикла**.
