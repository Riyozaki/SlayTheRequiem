# Б2.5 — evidence: source/content отдельно от Core и native

`checks.json`: **29 checks, all exit 0 / 399 SHA256 inputs**. Снято на dirty исходниках после родителя `72ee189`;
`base_commit` не утверждает, что родитель содержит проверенные изменения: сверять `inputs_sha256`.
В `logs/` только 29 перечисленных логов доступной локальной автоматики. 30 checker regressions (из них 10 parser,
6 economy wiring), 35 build-safety, 151 rules Python; 308 C# grammar files / 0 errors; 133 Harmony targets / 0 errors;
1397 localization checks / 0 errors; localization regeneration идемпотентна.

Это **не C# semantic/full-mod build**, не настоящая native карта/награда/бой/витрина, не PCK/Godot layout,
не controller/two-client acceptance. Новые xUnit и C# ledger исполняются отдельным CI, результат сохраняется после запуска.
Parser fixtures в unit-тестах выдуманы; они **не** записаны как native evidence.

После CI здесь фиксируются реальные Core counters и output runner (producer=core-scenario).
60 экономических журналов / 30 same-seed revision pairs / 300 act rows — **не игровые прогоны**.
Ни одной native трассы Б2.5 пока не собрано. Ручные/визуальные/двухклиентные проверки на этом промежуточном этапе
не проводились по решению автора; они остаются на конец релизного цикла.
Нефатальные ResourceWarning старых catalog readers не являются игровыми ошибками. Нет credentials/game DLL/saves.
