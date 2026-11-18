# Как прислать воспроизводимый отчёт

Не присылайте пароли/ключи/Steam credentials, игру, игровые DLL, весь build bundle, `.binlog` или чужие saves.
Публичный отчёт — короткие обезличенные факты; полные локальные логи/сейвы могут содержать пути, сетевые IDs и личные данные.

## Ошибка в игре

1. Записать ожидаемое/фактическое действие и минимальные шаги, герой/origin/background/форма/Путь, этаж/бой,
   new/Continue/rollback, seed и сложность. Указать, было ли вмешательство консоли/fixture.
2. Взять полный **source SHA**, input hash, hashes DLL/PCK/manifest и номер кандидата из build passport/`candidate.json`.
   `v0.0.1` без hashes недостаточно; несовпадающие пары не сравнивать как одну сборку.
3. `str_patches` показывает состояние hooks. `str_passport` выдаёт JSON: game/BaseLib/другие моды, артефакты,
   rules source/requested/effective, реальный run nonce, персонажей, язык/window/scale/vsync/frame cap, presentation flags и client role.
4. Для узкого воспроизведения — `str_trace on`, выполнить действие, `str_trace off`. Для ресурсного/награды бага
   приложить записи IDs/count/holder/actual runtime portrait/конкретное исключение, не только screenshot «0/2».
5. При pending/blocked — `str_recover status`; receipt retry/restore только штатными путями. Не очищать сохранённые
   флаги и не повторять списание ради снятия блокировки. Сохранить исходную проблемную копию отдельно.

Те же сведения доступны через плашку Реквиема / F8 / нажатие правого стика, если ввод не занят другим окном:
паспорт открывается в native modal, PageUp/PageDown/правый стик прокручивают, Esc/B закрывают.

`str_passport`/native metrics содержат player Net IDs и могут раскрывать список частных модов. Оригиналы хранить приватно;
для публичного issue заменить IDs последовательными alias, пути — `<local>`, убрать account/profile данные.
Хэши модовых DLL/PCK и относительные runtime resource paths обычно достаточны. Не редактировать оригинал evidence,
на который ссылается private acceptance matrix: сделать отдельную очищенную копию.

## Сборка / импорт / PCK

Указать ОС, .NET SDK, MegaDot, игру/BaseLib и команду без личных абсолютных путей. Приложить коды ошибок и небольшой
очищенный фрагмент вокруг **первой** ошибки, stage name и warning-code counts. Full logs/binlog остаются приватными.
Публичный CI не собирает игровую DLL. Core PASS и синтаксис не опровергают semantic mod build error.
`legal_game_build.py` выгружает только allowlisted `public-result.json`; private runner paths и bundle не загружать.

## Производительность

`str_trace off` → одинаковый warm-up → `str_metrics on` → `str_metrics reset` → действие → `str_metrics off`.
Сохранить frozen `str_metrics status` JSON и setup/hardware/resolution/scale/язык/flags обоих сравниваемых builds.
Не снимать status покадрово и не смешивать metrics разных условий. Данные bounded (1024 последних frame/operation samples),
allocations/GC/node deltas шире rolling frame window и включают измеритель. `tools/perf/native_metrics.py` сравнивает
уже записанные captures и отвергает разные конфигурации/несогласованные числа; он не запускает игру и не ставит
performance PASS. Не переводить cache microbenchmark в «игровой FPS».

## Два клиента

Нужны факты **с host и peer**, одинаковые build hashes, роли и временной порядок request/receipt/действия/rollback,
ожидаемые и наблюдаемые revision/timeline. Две копии host-log не заменяют второго клиента. Разрыв/таймаут/отказ не
имитировать на ценном run; использовать disposable профиль. Не публиковать оригинальные peer IDs/полный save без согласия.
