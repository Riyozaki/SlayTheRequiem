# Установка кандидата Slay the Requiem

**Пока нет принятого нового релиза.** Этот файл описывает будущий проверяемый пакет. Архив с
`candidate.json` имеет статус `unaccepted-candidate`: успешная упаковка не означает плейтест, баланс или разрешение публикации.
Финальная матрица выполняется только в конце цикла; исходники/CI не заменяют её.
Собираете сами: [предсборочная передача и команды](BUILD_HANDOFF.md), полный pipeline — `tools/g0/README.md`.

## Что требуется игроку

- Легальная **Slay the Spire 2 0.111.1**, отдельно установленный **BaseLib 3.4.8**.
- Для первого приёмочного запуска — **только BaseLib и SlayTheRequiem**, отдельный тестовый профиль/копия сейвов.
- .NET SDK и MegaDot нужны **сборщику**, не являются частью архива и не скачиваются установщиком.
- В коопе на обоих клиентах должна быть **одна и та же пара DLL/PCK/manifest**, а не просто одинаковая надпись `v0.0.1`.

Сначала закрыть игру и сохранить вне `mods` резервную копию важных saves/config. Установщик не гарантирует
совместимость любого старого сейва. Прочитать [COMPATIBILITY.md](COMPATIBILITY.md) до Continue.

## Состав архива

```text
candidate.json                 SHA256 и размер каждого файла, source commit/input hash; не цифровая подпись
mods/SlayTheRequiem/
    SlayTheRequiem.dll
    SlayTheRequiem.pck
    SlayTheRequiem.json
    SlayTheRequiem.build.passport  публичная, совместимая с installer/runtime часть паспорта
INSTALL.md / COMPATIBILITY.md / RELEASE_NOTES.md / ART_PROVENANCE.md
FINAL_ACCEPTANCE.md / DIAGNOSTICS.md / final-matrix.plan.json / asset-coverage.csv
source/                        исходное состояние mod/tools/art и документация; без .git, игры и private logs
    docs/GUIDE.md              путеводитель с рабочими относительными ссылками на дизайн и иллюстрации
```

Root-копии выпускных Markdown получают относительные ссылки на `source/`, если документ не вынесен рядом;
исходные документы внутри `source/` не переписываются. Сам путеводитель остаётся в `source/docs/GUIDE.md`.

BaseLib, игровые DLL, saves, binlogs и приватные логи сборщика **не входят** в поставку. Публичный паспорт намеренно
не содержит список частных модов/локальные пути. Его hash отличается от private build passport; поэтому финальные
native-паспорта снимаются **после установки именно содержимого этого архива**. DLL/PCK/manifest при этом не меняются.

## Проверка и чистая установка

В checkout исходников, совпадающих с кандидатом, до распаковки:

```bash
python tools/release/package_release.py verify .arena/releases/candidate.zip
```

Команда проверяет состав, дубликаты/небезопасные пути, hashes, исходный input hash и build proof. Это **не** запуск игры.
Потом распаковать в отдельный временный каталог, не поверх старого мода. Из того же checkout:

```bash
# Пути ниже — ваши локальные переменные, не данные для Git/чата.
python tools/g0/mod_build.py verify --bundle "$EXTRACTED/mods/SlayTheRequiem"
python tools/g0/mod_build.py install --bundle "$EXTRACTED/mods/SlayTheRequiem" --mods-dir "$STS2_MODS_DIR" --game-closed
```

Installer меняет каталог целиком; прежний уходит в `.requiem-backups` рядом с `mods`, не остаётся вторым модом.
При неудачном swap восстанавливается старая установка. Если обнаружены loose DLL/PCK в `mods`, другой каталог с тем же
mod ID, неизвестное содержимое или symlink — **отказ**, без удаления пользовательских файлов. Разобрать конфликт вручную.
Никогда не обновлять только DLL или только PCK. Ручная установка должна повторять эту же чистую замену каталога.

## Финальный smoke, не промежуточный плейтест

В конце релизного цикла: мод виден → `str_patches` → `str_passport` → новый тестовый run каждым героем → выход/Continue.
Проверить ресурсы, выбор старта, награду, бой и возврат в меню; дальше выполнить всю [финальную матрицу](FINAL_ACCEPTANCE.md).
Наличие архива/зелёного CI не выставляет здесь PASS автоматически.

При неисправности закрыть игру, сохранить диагностические данные, восстановить **целый** прежний комплект и совместимую
копию сейва. Downgrade DLL не превращает новый save в старую schema. Не удалять `OperationPending/Blocked` ради Continue.

## Повторная сборка

Инструменты: Python ≥3.11, .NET **9.0.x SDK**, MegaDot **4.5.1 mono**, легальные references игры/Harmony и BaseLib.
Получить чистый Git checkout по `source_commit` из `candidate.json`, сверить включённые `source/` и input hash,
следовать `tools/g0/README.md`. Файлы в `source/` — проверяемый снимок, **не Git-репозиторий**; G0 получает Git identity
из checkout, а не выдумывает commit для распакованного ZIP. Для release checkout отключить автоматическую замену
окончаний строк **локально в отдельном новом clone** (`git clone -c core.autocrlf=false …`), не перенормализовывать рабочий
проект с пользовательскими изменениями. G0 хэширует фактические bytes, а не только clean status: checkout с CRLF вместо
байтов Git snapshot будет корректно отвергнут упаковщиком. Сборки разных SDK/ОС не обещаны бит-в-бит одинаковыми.
Новая сборка имеет собственные hashes и нуждается в своём паспорте/приёмке.

О проблеме: [DIAGNOSTICS.md](DIAGNOSTICS.md) в репозитории. Полные saves/binlogs публично не прикладывать.
