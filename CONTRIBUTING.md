# Как помочь Slay the Requiem

Спасибо за интерес! Мод в стадии **pre-alpha**. Идёт стабилизация G0/G1: исторический build и плейтест
не проверяют новые изменения. Текущие статусы — [реестр](docs/plan/implementation-registry.md).

## Самое полезное сейчас
1. **Воспроизводимая сборка текущего кода.** Выполните [G0 pipeline](tools/g0/README.md) с легальной игрой 0.107.1,
   BaseLib 3.4.8, SDK 9 и MegaDot 4.5.1 mono: full build с warnings → отдельный PCK → чистая установка.
   Пришлите обезличенные ошибки/паспорт (issue «Баг» → «Сборка»); игровые DLL, saves и локальные пути в Git не добавлять.
2. **Плейтест.** Какие системы ломаются, где баланс улетает.
3. **Тексты.** Правки русской и английской локализации.

## Перед pull request
```bash
dotnet test mod/SlayTheRequiem.Tests                 # если трогали Core/
python3 -m unittest discover -s tools/sim            # правила и их Python-зеркало
python3 -m unittest discover -s tools/g0             # synthetic build/install contracts, НЕ экспорт
python3 tools/loc/build_loc.py                       # тексты генерируются — не правьте JSON руками
python3 tools/ci/check_loc.py
```

## Правила кода
- **Чистые правила — в `Core/`**, без `using` игры и Godot: их собирает тестовый проект и CI.
  Число или формула из дизайна → константа в `Core/*Rules.cs` + тест (xUnit и Python-зеркало в `tools/sim`).
- Тексты — только через `tools/loc/loc_*.py` и `build_loc.py`; ключи `SLAYTHEREQUIEM-<UPPER_SNAKE>`.
- Таблицы `data/*.csv` генерируются скриптами из `tools/markup` — правьте скрипт, а не CSV.
- Кооп: всё, что влияет на состояние забега, должно одинаково считаться у всех игроков (броски от сида забега).
- Документ дизайна (`docs/design/NN-*.md`) — источник правды по числам; меняете правило — обновите документ.

## Коммиты
Короткое описание сути изменения; на русском или английском. Один PR — одна тема.
