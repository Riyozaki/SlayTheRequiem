# Б2.5 — evidence: source/content отдельно от Core и native

`checks.json`: **29 checks, all exit 0 / 399 SHA256 inputs**. Снято на dirty исходниках после родителя `c8c99ea`;
`base_commit` не утверждает, что родитель содержит проверенные изменения: сверять `inputs_sha256`.
В `logs/` только 29 перечисленных логов доступной локальной автоматики. 30 checker regressions (из них 10 parser,
6 economy wiring), 35 build-safety, 151 rules Python; 308 C# grammar files / 0 errors; 133 Harmony targets / 0 errors;
1397 localization checks / 0 errors; localization regeneration идемпотентна.

Это **не C# semantic/full-mod build**, не настоящая native карта/награда/бой/витрина, не PCK/Godot layout,
не controller/two-client acceptance. Исторический local snapshot выше не охватывает последующие контрмеры боссов.
Parser fixtures в unit-тестах выдуманы; они **не** записаны как native evidence.

## Реальное исполнение C# и сохранённый полный output

На `20ddd2f8eb27851c85870809f8205dbd5d160329` [CI 44253669236](https://github.com/Riyozaki/SlayTheRequiem/actions/runs/44253669236)
прошёл все четыре jobs; Core check **134630120149** сообщил **778 executed / 778 passed / 0 failed / 0 notExecuted**.
Метаданные jobs и counters — [`core/ci.json`](core/ci.json); локального TRX нет (не подменён этой квитанцией).

- [`core/core-scenarios.json`](core/core-scenarios.json) — полный исходный UTF-8 output реального C# runner;
- [`core/core-scenarios.summary.json`](core/core-scenarios.summary.json) и [`core/core-scenarios.md`](core/core-scenarios.md);
- [`core/verification.json`](core/verification.json) — проверка всех **8 частей / 611069 распакованных байт bundle**,
  общего SHA256 и SHA каждого файла. Bundle `d838827081a162b9ae8f1ddf59409662e8c9e0c60ea0d44861dd106954a8f6ca`;
  JSON `76afa0960095cacc7254c568273cfcc0fe18209e32cc545b2d6abd6a74c32331`.

Файлы получены из полного bounded annotation bundle через `tools/balance/collect_core_evidence.py`, **не пересчитаны Python**.
Первый длинный notice был обрезан; попытка с 12 частями сохранила только 9 из-за лимита notices на step. Оба набора отвергнуты.
Успешный транспорт: 2800 символов/часть, максимум 9 + сводка. Название producer/hash само по себе не является подписью native игры.
60 экономических журналов / 30 same-seed revision pairs / 300 act rows — **не игровые прогоны**.
Этот compiled checkpoint предшествует пакету контрмер боссов/session 5; не выдаётся за проверку позднего game-facing кода.
Ни одной native трассы Б2.5 пока не собрано. Ручные/визуальные/двухклиентные проверки на этом промежуточном этапе
не проводились по решению автора; они остаются на конец релизного цикла.
Нефатальные ResourceWarning старых catalog readers не являются игровыми ошибками. Нет credentials/game DLL/saves.
