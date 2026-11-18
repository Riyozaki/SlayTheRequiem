# Блок 1: реализация надёжного ядра — 18.11.2026

**Ветка:** `arena/5906afa4-slaytherequiem` · **база:** `8000886e9c52db78227972bdd4740ee71f7eca03`.
Объём — Б1.1–Б1.5 из [плана следующего релиза](../plan/next-playable-release-2026-11-18.md), не перепись всего мода.
Это отчёт о внесённых исходниках и реально выполненной автоматике. **Полная игровая приёмка блока не объявлена.**
Ручные и двухклиентные прогоны по указанию автора отложены в конец всего релизного цикла, не запрашиваются сейчас.

## Проверка и границы результата

<!-- verification-summary:start -->
Подтверждённый публичный CI для последнего runtime-коммита:
[`b82c909`, run 44229461620](https://github.com/Riyozaki/SlayTheRequiem/actions/runs/44229461620).
**Core: 560 выполнено / 560 PASS / 0 FAIL / 0 пропусков.** Все четыре jobs завершились успешно.
Counters опубликованы Core job из реального TRX; это не синтетический счётчик Python build tests.
<!-- verification-summary:end -->

Локально: Python build-contracts, numeric rules sim, syntax/Harmony/content; команды, exit codes, logs и SHA —
в [evidence блока 1](evidence/stage-one-2026-11-18/README.md).
**Не выполнены:** полный C# build игрового проекта, Godot import/export/изолированная загрузка PCK, запуск игровых
adapter fixtures, обычная игра, два клиента, измеренный runtime/performance baseline. В этой среде нет SDK,
легальных игровых reference DLL и MegaDot; remote Core — настоящая компиляция Core, но не game-facing кода.
Историческая успешная сборка/PCK и плейтест автора на `ec598de` остаются действительным отдельным результатом.

## Что внесено

| Пакет | Реализация | Автоматическая/игровая граница |
|---|---|---|
| **Б1.1. Сборка** | Минимальное устранение CS1628; закреплённые game/BaseLib/Godot contracts; отдельные full build, Core, export и pack-load; паспорт SHA/inputs/DLL/PCK/manifest; clean swap с backup | Core/Python/source проходят. Полный pipeline подготовлен, но без легальных references/engine не исполнен |
| **Б1.2. Карты и экземпляры** | Полка хранит точную карту и исходный бой, переживает снятие из стопки/явный null Owner; возврат по наблюдаемому commit; отказ полки сохраняет карту в Discard. Обязательная выдача имеет сохраняемый источник; clone isolation для коллекций | Core lifecycle/source-ledger tests выполнены. Native model/боевые/full-hand сценарии — финальная приёмка |
| **Б1.3. Получение/перенос/оплата** | Opaque full-payload chest, instance IDs, write-ahead claims; veto не расходует источник; late failure не превращается в повторную выдачу; native reward сохраняет retry/skip; оплата по наблюдаемому результату | Pure transfer/payload/payment tests выполнены. IL/native reward и реальные `0/2`/draw/`nope` ещё требуют движка и исходного воспроизведения |
| **Б1.4. Сейвы и сеть** | Saved rules без menu fallback, read-only отказы, исходные byte backups; startup/Continue, fatigue/camp visits; timeline/revision/immutable receipts, epoch-aware recovery; отложенный отпечаток и policy-preserving map reroll | Core policies выполнены. Native save-read/async/private-field/Harmony adapters и 2 клиента не приняты |
| **Б1.5. Регрессии/диагностика** | Расширенный xUnit; 67 source adapter cases; factory smoke/full; end-to-end trace, actual portrait diagnostics; opt-in bounded timing windows; постоянное evidence | Счётчик fixtures не означает их запуск. Замеров миллисекунд/FPS до игрового прогона нет |

### Б1.1 — одна идентифицируемая пара, не случайная новая DLL

- `DefensePowers.cs`: `out/ref`-результат больше не захватывается лямбдой; формула этим исправлением не перебалансирована.
- `tools/g0/mod_build.py`: fresh isolated source project, полный Release build и binlog, отдельный Core/TRX,
  import, отдельный `--export-pack`, проверка пакета из **пустого** проекта. Ошибка export и Godot `ERROR`/`SCRIPT ERROR`
  даже при exit code 0 фатальны. Ноль выполненных/пропущенные/проваленные Core-тесты не дают успешный паспорт.
- Hash входов учитывает untracked source/deletions; в DLL записаны source metadata. Паспорт не является цифровой подписью.
- `install` проверяет одну DLL/PCK/manifest-пару, создаёт staging, сохраняет прежний каталог вне `mods`, возвращает его
  при ошибке swap. Не удаляет неизвестные каталоги, дубликаты mod ID, loose legacy files или symlinks.
- Добавлен ручной [`legal-game-build.yml`](../../.github/workflows/legal-game-build.yml): только доверенный ref,
  подтверждённый изолированный self-hosted runner с легальной игрой. Не запускается по PR, не скачивает игру,
  не устанавливает автоматически в рабочую игру, не публикует release. Наружу выгружается только allowlisted JSON
  с hashes/counts/status; binlog, локальные пути, references, saves и весь bundle остаются приватно на runner.
- Публичный CI отдельно сохраняет TRX и подтверждённые counters. Он не подменяет собой полный build/PCK.

Команды и подготовка runner: [tools/g0/README.md](../../tools/g0/README.md).

### Б1.2–Б1.3 — сохраняется источник, а не только признак «мы пытались»

- Полка Кличей keyed по reference identity, а не типу. В 0.107.1 обычный `RemoveFromState` ставит removed/Pile=null;
  отдельный `CombatState.RemoveCard` ещё и обнуляет Owner. Разрешён возврат **своего captured** снятого экземпляра,
  но не чужой карты и не карты, уже перемещённой в Exhaust/Deck/другую стопку. Новая сила не клонирует чужую полку.
- `MandatoryGrantSources` связывает источник `relic instance / effect` с уникальным claim. Сначала сохраняются
  entitlement и полный snapshot, потом выполняется Add. Завершённый источник остаётся завершённым, даже если игрок
  впоследствии удалил/продал карту или obtain-модификатор заменил её. Это не поиск «есть ли карта такого класса».
- Предыстории, карты форм/подписная карта Пути и BloodLord используют этот контракт. BloodLord flag выставляется после
  появления durable entitlement. Переход/снятие формы и получение Сущности+Знака охвачены lease/mutation/recovery.
  Неизвестный результат multi-step эффекта требует checkpoint, а не повторного AfterObtained.
- Путь до первого callback сохраняет exact instance/payload Знака на самой Сущности. Только после завершения issuer hooks
  entitlement переходит в ready. Уже существующая Сущность с ready source дополучает **свой** Знак без повторного
  Element/Reborn callback; Completed переживает хранение/потерю Знака. Поздняя ошибка insertion расходует source и блокирует
  неопределённую операцию; ошибка до insertion оставляет исходник для retry. Legacy без источника не считается «не выдано».
- Passive boon HP sync/revoke также имеют write-ahead marker (либо входят в уже отслеживаемую native callback chain).
  Save из `MaxHpChanged` не даёт вторую HP delta после Continue; занятый selector не разрешает снять реликвию без revoke.
- Карты/реликвии сундука хранят полные snapshots и разные instance IDs. Неизвестный/недоступный payload не удаляется
  и не «восстанавливается» с нулевыми чарами. Legacy class names не могут восстановить уже утраченную историей заточку.
- `TransferProtocol`, scoped reward acquisition и service finalizers различают rejection, commit, partial commit,
  cancellation и stale source; проверяют наблюдаемый destination, включая obtain clone и исключение после вставки.
  Прямой chest relocation не ставит отдельный `OperationPending`: до native attach/detach уже записан полный источник,
  а сохранённые обе стороны согласуются по exact ID/payload без нового obtain. Это отдельный reconciliation-контракт,
  не обещание атомарности произвольного callback; его native interruption fixtures пока не исполнялись.
- Critical native reward patch меняет узкую часть async choice loop: отказ Add не завершает награду и не удаляет опцию;
  успешный obtain clone удаляет также выбранный source. Сохраняются штатные альтернативы/skip/choice IDs/VFX.
  Число IL match и поля state machine проверяются при установке патча; без запуска это не принятая IL-совместимость.
- Никакого увеличения Груза, принудительного количества «всегда три» или случайной замены арта ради зелёной отметки нет.

### Б1.4 — проверка до перезаписи и явное восстановление

**Порядок загрузки:**

1. Guard проверяет primary **и** `.backup` на literal/decoded Requiem ID, включая escaped JSON. Для такой пары сохраняет
   **исходные байты** обеих копий до любого native reader/repair: BOM, пробелы и переводы строк не теряются.
   Читает те же byte arrays строгим настоящим native serializer с запретом неизвестных typed полей, в том числе nested.
   Duplicate fields/неверный тип/чужая native schema дают `ValidationFailed`; native aggressive recovery вообще не вызывается.
   Missing primary может читать backup; corrupt existing primary не заменяется молча более старой копией. Старые **игровые**
   native schemas требуют matching game build; legacy **mod** schema в текущем native format мигрирует ниже.
   MP canonicalization выполняется в read-only wrapper: ошибка model/ID не переименовывает primary/backup.
   Converter/extension-data поля имеют собственный native контракт, универсальной миграции произвольной чужой схемы нет.
2. `ValidateSaved` до `RunState.FromSerializable` проверяет shared snapshot, дубликаты stores, schema/protocol/timeline.
   Несогласованные/future/missing non-reconstructible rules не заменяются настройками локального меню.
   Неизвестные **v1** keys правил сохраняются; чистое старое состояние получает metadata migration с backup.
   Legacy Block/Pending без текущего recovery contract явно отклоняется, а не импортируется в незаметную вечную блокировку.
3. Подготовка миграции предшествует native `IncrementNumReloads` в SP и host MP. У обычного Continue не меняются timeline
   и revision. Если у входа нет дискового документа (memory/replay), его native-object backup назван отдельно и не
   выдаётся за original bytes. Универсальная миграция чужих неизвестных nested schemas не обещается.
4. Старое `OriginGranted=true`, включая осознанное «none», считается завершённым стартом — без replay HP/mastery/cards.
   Legacy relic без completed declaration неоднозначен: backup/явная остановка вместо повторной разовой выдачи.
   Сохраняемый fatigue visit key не пересчитывает ту же комнату при Continue; camp points/options также привязаны к visit.

**Неопределённая операция:**

- Общий gate принадлежит actual player instance; nested finalizer должен предъявить live lease. Mutation marker покрывает
  async effect/payment/receipt boundary; saved partial state после Continue не считается известным успехом.
- Безопасный whole-run checkpoint снимается вне боя на host/SP до доступной root mutation. Fixtures/clients, restoring,
  уже mutating/blocked/pending state и неготовый map/identity не перезаписывают его. Это **не** универсальная атомарная
  транзакция любого callback игры и не утверждение «checkpoint перед абсолютно каждой мутацией».
- Повтор receipts не повторяет Add/списание. Автоматическое снятие блока допускается только при receipt-only timeout,
  полном совпадении immutable результатов/gold/digests, revision, неизменном текущем digest и failure epoch,
  **после disposal mutation**. Новая ошибка или partial effect не разблокируются старой квитанцией.
- `str_recover status|retry|restore` и плашка забега объясняют состояние. Restore после подтверждения откатывает **весь**
  run, сохраняет failed state, меняет timeline, оставляет `.previous`; в коопе authority у host и загружается его lobby.
  Нет подходящего checkpoint — отказ с инструкцией, не unsafe unlock. Поздний callback старого run не очищает новый раунд.

**Камень Памяти:**

- Первый act hook только вооружает capture. Отпечаток разрешён после всей `Hook.AfterActEntered` цепочки, окончания
  стартовых выдач и задач Ancient option, включая задачи, добавленные cleanup continuations. Сбой задачи не поглощается.
  MapRoom, который не вызывает обычный room hook, явно завершает недостающие pouch/Grimoire шаги.
- Snapshot сохраняет finished event room. Imprint ищется по mode/seed/startTime/**act**/schema, повреждённый current
  не скрывает подходящую `.previous`; предыдущий акт не предлагается как будто это начало текущего.
- Reroll выполняется до native early→late modifiers. WideActMap сохраняет dimensions/treasure/second-boss policy;
  специальные карты другого типа не заменяются Standard. Pending scars очищаются только после полного применения;
  prepared rollback checkpoint позволяет восстановление при неопределённом callback.
- Co-op сообщения привязаны к timeline, host/участнику и фазе/раунду; повторы не повторяют голосование/menu transition.
  Steam rejoin bounded; для LAN оставлен штатный явный ввод адреса вместо неверного предположения `127.0.0.1`.

### Б1.5 — диагностировать реальную границу, не выдумывать причину жалобы

`str_trace on|off`: pool/count/replacement → model/owner/upgrade → reward holders → actual portrait path/null/nope
→ selection/Add → combat copy → draw/shout. Исключение визуала содержит ID и этап; ноль/два holder не маскируются
случайной генерацией дополнительных карт. Без исходного сценария `0/2`/draw/`nope` **не записаны как воспроизведённые
и исправленные симптомы**.

`str_metrics on|off|status` — до 256 кадров/обновлений reward screen, count/median/p95/max, без покадрового лога.
Числа снимаются только в реальном процессе и вместе с `str_passport`; лабораторных FPS/замеров здесь не придумано.

`str_integrity` содержит **67 подготовленных игровых adapter cases**, включая native removal/ownership полки,
source grants через JSON reload, legacy none, read-only future-schema refusal, opaque timeline edits, receipt epoch/disposal,
service clone isolation, pending Path sign и passive HP write-ahead. `str_factory smoke|full` проверяет factory models
(216/5712 cases на seed), но не видимые holders.
Эти команды подготовлены для **финального** прогона с BaseLib + модом, не исполнялись в sandbox без игры.

## Что остаётся открытым, а не спрятано словом «готово»

- Семантический full build игрового проекта и разбор warnings с настоящими references.
- Новый отдельный PCK/import/ResourceLoader и actual runtime patches, в том числе private-field/task adapters и reward IL.
- Финальный manual/two-client matrix, воспроизведение исходных визуальных жалоб и measured baseline.
- Для legacy saves, уже потерявших карту/чары **без сохранённого источника**, безопасного способа угадать удалённые данные
  нет. Это относится и к отсутствующему legacy Знаку без journal. Использовать original backup/подходящий build,
  не автоматическую выдачу потенциального дубля.

Дальнейшие UI/wizard/art/balance задачи блоков 2–3 не объявлены выполненными этим пакетом.
[Общая игровая матрица](regression-checklist-2026-11-18.md) остаётся открытой до финальной проверки.
