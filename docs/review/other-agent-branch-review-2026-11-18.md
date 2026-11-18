# Что полезного в ветке другого агента — 18.11.2026

> После этого сравнительного ревью выполнен **избирательный перенос 63 файлов**, не whole-history merge.
> [Prior/import/landed SHA и границы реконструкции manifest](evidence/stage-one-2026-11-18/port-manifest.json) ·
> [наши последующие исправления и проверка Б1](stage-one-implementation-2026-11-18.md).
> Формулировки ниже «не перенесено»/«готового исправления нет» относятся к моменту ревью и проверенному `03d6360`.

**Проверенная ветка:** `arena/1d7c3746-slaytherequiem`, коммит [`03d636049e2a48101389be7f5856c3976b7730e2`][other-commit].
**Сравнение:** с нашим `main@8000886e9c52db78227972bdd4740ee71f7eca03`, а не только с родителем чужого коммита.
Рабочая ветка осталась `arena/5906afa4-slaytherequiem`. Это ревью, **не перенос или слияние кода**.

## Короткий вывод

**Полезная работа есть, и существенная.** Это заготовка инфраструктуры сборки и защиты от потери карт/вещей/оплаты,
а не просто ещё один аудит. Она хорошо подходит как материал для S0–S2 нашего [плана](../plan/stabilization-plan-2026-11-18.md).

**Но целиком переносить её сейчас не рекомендую.** Ветка вносит большой интеграционный пакет
без подтверждённой сборки и игровой приёмки. Два ключевых блокера из нашего аудита — **N-01, компиляция расчёта урона, и N-02, возврат Кличей — остаются**.
При этом новый механизм неопределённых операций может сохранять блокировку после Continue.

Предлагаю сохранить наработки, переносить по зависимостям небольшими пакетами и не начинать заново уже написанные
контракты/тестовые сценарии. Чужой реестр статусов достаточно честный: **G0/G1 прямо объявлены незакрытыми**.

## 1. Что там действительно новое

Общий предок веток — `ec598de3375af1dd61e3c6c0a7b524018187f7e6`. У другой ветки один большой коммит поверх этого предка;
содержимое последующих изменений main во многом включено в него без соответствующей истории коммитов.
Поэтому размер всего чужого коммита завышает именно новое для нас содержимое.

| Срез | Фактический результат |
|---|---|
| Деревья `8000886 → 03d6360` | **112 файлов, +18 150 / −605 строк** |
| Только `mod/SlayTheRequiemCode/` | **52 C#-файла, +5 125 / −504 строки** |
| Изображения/исходники арта/генераторы | `art/`, `mod/SlayTheRequiem/images/`, `tools/art/`, `tools/assets/` **идентичны main** |
| Уже знакомые UI | `NCampChoices`, `NEnemyTraitChips`, `MapVisuals`, `PaperDoll` **идентичны main** |
| Новое поверх этого | Build/install CLI, Core-контракты операций, переработка сундука/выдачи/услуг, диагностика, два файла Core-тестов, игровые harness-сценарии и документы |
| PR и CI другой ветки | На момент запросов `gh` не нашёл PR и вернул пустой список последних runs этой ветки |

Последняя строка не означает, что пользователь никогда не собирал мод. Историческая успешная сборка и плейтест
из основного аудита остаются фактом; они не подтверждают **новый пакет `03d6360`**.

Сохранены [diff/stat](evidence/branch-comparison-2026-11-18/tree-diff.txt),
[метаданные сравнения, проверки и SHA256](evidence/branch-comparison-2026-11-18/checks.json).
Все 112 отличающихся файлов использованного snapshot побайтно сверены с проверенным Git-коммитом.

## 2. Что стоит использовать

| Наработка | Реальная польза | Что требуется перед переносом/приёмкой |
|---|---|---|
| **G0: сборка → тесты → отдельный PCK → проверка → чистая установка** | [CLI][g0-build] запрещает устаревший output, фиксирует версии/входы/hashes, сохраняет binlog, проверяет ресурсы в пустом проекте, устанавливает совпадающую пару DLL/PCK с backup. Убирает прежний сценарий «новая DLL + старый PCK» | Выполнить настоящий pipeline с легальной игрой. При отдельном переносе tooling согласовать schema/protocol с реально перенесённым runtime: текущие source-contract tests ожидают новые `RunDiagnostics`/`ShopOperations` |
| **Копирование изменяемого состояния моделей** | `DeepCloneFields` добавлен для [сундука][pack-clone], [реагентов][pouch-clone], [торгового счётчика][ledger-clone] и полки Кличей. Это небольшая, содержательная защита от общих списков/массивов у разных экземпляров | Переносить отдельные hunks, а не весь новый `TravelPack`. Проверить двух игроков, новый забег после старого и reload; восстановленные данные не должны ссылаться на каноническую модель |
| **`OperationResult`, `TransferProtocol`, полный payload** | [Core-контракт][operation-core] различает отказ и изменение состояния перед поздним исключением; источник удаляется после наблюдаемого результата. [SavedPayload][payload-core] сравнивает свойства глубже, чем `SerializableCard.Equals`; неизвестные данные не выбрасываются молча | Запустить новые xUnit, затем проверить настоящие игровые adapters. Сам Core-контракт не доказывает корректность выбранного adapter-предиката |
| **Сундук как перенос экземпляра, а не создание вещи по имени** | [TravelPack][pack-transfer] сохраняет полный `SerializableRelic`, charges/enchants/заточку, различает одинаковые вещи, оставляет неизвестный payload, мигрирует legacy. Учитывает сохранение между записью источника и attach/detach; не повторяет obtain/history/equip | Это основная заготовка для N-03/N-04, не доказанный фикс. Нужны game build, реальные вещи/карты, veto, callback/late exception, save/quit/Continue и кооп |
| **Обязательные выдачи и переписывание свитка** | [CardAcquisition][acquisition] пишет сохранённое право на выдачу до Add, откладывает отказавшую карту, учитывает obtain-clone и замену стартера. [Scrolls][scrolls] привязывает результат к конкретному свитку; оплата/расход следуют за вставкой | Хорошее направление для N-05/N-06. Нельзя копировать только вызовы `AddOrDefer`: нужны pending store, claim identity, reconciliation и тесты частичных результатов |
| **Награды: реальный fallback, диагностика всей цепочки** | [RewardSwap][reward-swap] сначала готовит изменения правила; три replacement-правила имеют явный порядок. [Trace][reward-trace] охватывает factory/pool → NCard → Add → combat copy/draw; [панель][reward-panel] добавляет holders, причину запрета и разгрузку | Это помогает исследовать N-07/N-08/N-15. Не устраняет само по себе `0/2`, `nope` или проблему добора. RNG не откатывается, порядок правил/кандидатов меняется — это не только безвредное логирование |
| **Граница операций лавки** | [Policy/executor][shop-policy] повторно проверяет цель/владельца/цену после выбора; [shop operations][shop-operations] вводит run/revision/receipts и защиту от повторного исполнения. Доверие учитывает фактическую оплату | Самая рискованная интеграционная часть: новые сетевые контракты и сохранённые Pending/Block. Принимать только после fault-injection и двух настоящих клиентов |
| **Тестовые сценарии и честные статусы** | Новые Core-тесты и [55 сценариев игрового адаптера][integrity-harness] покрывают veto, late throw, клоны, legacy, коллизии, save между стадиями. [Реестр][registry] разделяет source, automation, игру и визуальную приёмку | Сценарии стоит сохранить и доработать, а не переписывать с нуля. Но наличие метода с названием теста — не его выполнение |

### Важное дополнительное наблюдение: clone isolation

Это полезное дополнение к нашему первоначальному списку N-01…N-18. В закреплённой игре
[`AbstractModel.MutableClone`][game-clone] выполняет `MemberwiseClone`, затем виртуальный `DeepCloneFields`.
`RelicModel` копирует свои DynamicVars, но не поля произвольного наследника; [BaseLib `CustomRelicModel`][baselib-relic]
не добавляет универсального глубокого копирования этих полей.

В main у сундука, реагентов и торгового счётчика нет соответствующих overrides. Следовательно, новые экземпляры
могут разделять эти изменяемые объекты до отдельной переинициализации. Ветка другого агента закрывает именно этот
исходный путь aliasing. Это **подтверждённый статический риск**, но не наш выполненный игровой тест утечки между игроками.

## 3. Полезно не только новое программирование

Чужой [аудит][other-audit] в основном согласуется с нашим: потеря карт/свойств вещей, ложный fallback, незавершённый старт,
разрыв между старым «реализовано» и реальной приёмкой. Не стоит заводить два конкурирующих текущих roadmap.
Но есть дополнительные конкретные пункты, которые имеет смысл включить в дальнейшую работу:

1. **A03: стандартная награда может завершиться после неуспешного Add.** В [нативном `CardReward.OnSelect`][game-reward]
   `rewardComplete`/`endSelection` выставляются до Add, а ветка `success == false` их не возвращает.
   Это отдельная причина «нажал взять — не получил», не объяснение отсутствующих **видимых** вариантов `0/2`.
   Новая ветка пытается исправить это IL-adapter'ом; его выполнение ещё надо доказать.
2. **A04: откат Камнем Памяти теряет широкую карту.** В текущем main
   [`MemoryStoneRelic.cs:154–164`](https://github.com/Riyozaki/SlayTheRequiem/blob/8000886e9c52db78227972bdd4740ee71f7eca03/mod/SlayTheRequiemCode/Rollback/MemoryStoneRelic.cs#L154-L164)
   безусловно строится `StandardActMap` и передаётся `replaceTreasureWithElites: false`, в отличие от
   [обычного создания модового акта](https://github.com/Riyozaki/SlayTheRequiem/blob/8000886e9c52db78227972bdd4740ee71f7eca03/mod/SlayTheRequiemCode/Acts/RequiemActs.cs#L46-L47).
   В другой ветке этот файл не изменён: находка полезная, исправления пока нет.
3. **A05: вход в акт не равен завершению стартового выбора.** Снимок снимается из
   [`AfterActEntered`](https://github.com/Riyozaki/SlayTheRequiem/blob/8000886e9c52db78227972bdd4740ee71f7eca03/mod/SlayTheRequiemCode/Rollback/MemoryStoneRelic.cs#L137-L145), но
   [игровой `EnterAct`][game-act] лишь входит в стартовую комнату; [EventRoom][game-event] не ждёт её завершения.
   Готовность всех Ancient-выборов обрабатывается отдельно. Поэтому обещание «отпечаток после стартового дара» не обеспечено.
   Новая operation timeline в `MemoryStone` эту точку фиксации не чинит.

A04/A05 перепроверены по коду/контрактам. Их видимые последствия и правильную точку нового снимка нужно принять
отдельным игровым сценарием, особенно в коопе. Это не основание вслепую переносить остальные выводы чужого аудита.

## 4. Почему нельзя считать ветку готовой заменой main

### 4.1. N-01 остался без изменения — P0

[`DefensePowers.cs:67–83`][defense] в обеих ветках **побайтно одинаков**:
параметр `ref decimal __result` захвачен внутри `modifiers.Any(...)`. Это запрещённый C# ref-capture, CS1628.
Новый build CLI не исправляет исходный код и не означает, что полная сборка уже прошла.

Вывод установлен статически; свежего compiler log здесь нет. Историческая успешная сборка более раннего кода
этому не противоречит.

### 4.2. N-02 улучшен по диагностике, но обычный возврат Клича не восстановлен — P0

В [новом `Shouts.cs:65–77`][shout-return] запись больше не теряется при неудаче, есть trace и учёт позднего исключения.
Но до Add по-прежнему стоит `if (card.Owner == null || card.CombatState == null) continue`.

По [getter игры][game-card-combat] `card.CombatState` возвращает состояние **только для карты в боевой стопке**
(либо combat upgrade preview). Клич отправляется в `PileType.None`, снят со стопки, поэтому обычный shelf-entry
попадает в эту ветку и не доходит до возвращения. Сохранить его в списке недостаточно.

Это не просто «нужен тест на всякий случай»: исходный конфликт с контрактом остался. Нужны явное владение полкой
и проверка текущего боя, а не проверка CombatState снятой карты. Готового исправления в этой ветке нет.

### 4.3. Критичный патч async state machine ещё не запускался

[`CardRewardCommitPatch`][reward-il] переписывает `CardReward.OnSelect.MoveNext`:
ищет конкретные поля/IL-инструкции, заменяет Add и удаление выбранной опции, меняет flags после отказа.
У него есть fail-closed проверки количества совпадений — это лучше тихого несовпадения,
но совместимость нельзя установить tree-sitter или проверкой названий Harmony-целей.

Нужны запуск на настоящей игре 0.107.1, успешная установка патча и тесты обычной/альтернативной/множественной награды,
obtain-clone, retry/skip, reroll, UI/history и native multiplayer selectors. Это не безопасная «пара строк диагностики».

### 4.4. Защита данных может превращаться в сохранённую блокировку

[`InventoryGate`][inventory-gate] превращает загруженный Pending без живого scope в сохранённый Block.
[`TravelPack.ShouldAddToDeck`][burden-gate] при Block отказывает обычным картам; новые custom inventory operations
также закрыты. Для Curse/Status у нашего burden-hook есть явное исключение, чужие veto не обходятся.

В [shop adapter][shop-operations] отсутствие согласованных квитанций после ожидания до **8 секунд** тоже блокирует
операции. Continue не снимает Block и не ремонтирует оплату. Это сознательный контракт ветки, не скрытая ошибка,
но цена отказа существенна: часть функций забега может остаться недоступной до восстановления известного корректного сейва.

Перед переносом нужно принять этот UX/recovery-контракт и проверить ложные срабатывания, задержки, отключение участника,
save/quit/Continue и восстановление. Просто убирать marker из сохранения — не ремонт.

### 4.5. Это не решение всех замечаний плейтеста

- Полного нового создания героя, отдельного стартового бонуса и возобновления всех шагов `OriginGranted` здесь нет.
- Это не переработка наборов/баланса карт трёх героев, поведения боссов или боевых контрактов из N-11…N-14.
- Новые диагностические плашки/панель награды не заменяют полноценные экраны лагеря и героя.
- Нет нового набора иллюстраций, подтверждённого решения `nope`, замеров FPS или игровых результатов `0/2`/draw/shout.
- Миграция legacy не восстановит уже утраченные старым форматом чары/заточку/заряды: этих данных в строке имени нет.

## 5. Что я действительно запустил

Проверки выполнялись на отдельном архивном snapshot `03d6360`, без переключения нашей ветки и без установки в игру.

| Проверка | Новый локальный результат | Граница результата |
|---|---|---|
| `unittest discover -s tools/g0 -v` | **29/29, OK** | Синтетические bundles/preflight/installer/schema, **не сборка и не экспорт** |
| `unittest discover -s tools/sim -v` | **150/150, OK, без skip** после установки numpy | Существующее Python-зеркало/контент; не новое исполнение C# |
| `check_csharp_syntax.py` | **250 файлов, 0 грамматических ошибок** | Не типы/семантика, не обнаруживает N-01 |
| `check_loc.py` | **1204 модели, 0 ошибок** | Не внешний вид и не загрузка PCK |
| Source manifest `str_integrity` | **55 уникальных source cases**, SHA совпадает с manifest | Все остаются **не скомпилированными и не выполненными здесь** |

Первый Python-sim запуск имел **149 PASS + 1 skip** из-за отсутствия numpy; этот лог сохранён отдельно.
После установки `numpy==2.4.6` suite выполнен целиком, без пропусков. В тестах остались предупреждения
`ResourceWarning` о незакрытых файлах. Их не выдаю за ошибку игрового кода или за устранённый дефект.

**Не запускались:** новые xUnit, full-mod build, Godot import/export/isolated ResourceLoader, `str_integrity`, `str_factory`,
сама игра, disk Continue/rollback и два клиента. В этой среде нет SDK 9/движка и игровых references.
216 smoke / 5712 full на seed у `str_factory` — размеры написанного перебора, **не полученные результаты**.

Чужие сохранённые safe-check logs полезны как история, но не заменяют эти недостающие уровни.
Небольшая несогласованность документов: `tools/g0/README.md` ещё говорит о 48 integrity cases, актуальный source/реестр — о 55.

## 6. Как использовать без переноса всего риска

1. **S0:** устранить N-01, перенести и адаптировать G0 tooling/версии/метаданные. Получить настоящую свежую сборку
   DLL + PCK. Не маркировать её schema 3/protocol 1 только потому, что эти числа скопированы из чужого проекта.
2. **Узкий пакет clone isolation + диагностика:** отдельные изменения `DeepCloneFields`, trace и нужные команды.
   Полный `RunDiagnostics` уже зависит от новых контрактов — переносить эти зависимости осознанно либо выделить минимальный вариант.
   **N-02 исправить отдельно**, не принимать чужую правку полки как готовую.
3. **S1:** использовать Core contracts/tests и payload/chest codec как основу. Затем переносить согласованный набор
   adapter/identity/migration, каждый раз проверяя no-loss/no-dupe, а не один огромный файл без его зависимостей.
4. **S1–S2:** claims обязательных выдач, свиток/платные награды и native reward adapter — после компиляции и с отдельной
   матрицей veto/late failure/clone/save. Сохранить связь с N-03…N-08; исходные симптомы закрывать только воспроизведением.
5. **Сетевой пакет — последним в этой серии:** новые shop requests/receipts, lobby/saved-settings guards, timeline rollback
   и persisted Block принимать вместе с тестами двух клиентов и согласованным способом восстановления.
6. **Не переносить повторно** уже имеющиеся арт/UI-фиксы и весь исторический audit/evidence-каталог.
   Взять дополнительные A03/A04/A05 и сценарии в единый план; не объявлять чужие G0/G1 выполненными или заменой S0–S8.

Так сохраняется полезная работа другого агента, но не появляется ложное «раз там много защит и тестов — можно сразу слить».
**В этом проходе код мода не изменялся, merge/cherry-pick/commit не выполнялись.**

[other-commit]: https://github.com/Riyozaki/SlayTheRequiem/commit/03d636049e2a48101389be7f5856c3976b7730e2
[g0-build]: https://github.com/Riyozaki/SlayTheRequiem/blob/03d636049e2a48101389be7f5856c3976b7730e2/tools/g0/mod_build.py#L165-L266
[pack-clone]: https://github.com/Riyozaki/SlayTheRequiem/blob/03d636049e2a48101389be7f5856c3976b7730e2/mod/SlayTheRequiemCode/Alchemy/TravelPack.cs#L63-L77
[pouch-clone]: https://github.com/Riyozaki/SlayTheRequiem/blob/03d636049e2a48101389be7f5856c3976b7730e2/mod/SlayTheRequiemCode/Alchemy/HerbalistPouch.cs#L35-L41
[ledger-clone]: https://github.com/Riyozaki/SlayTheRequiem/blob/03d636049e2a48101389be7f5856c3976b7730e2/mod/SlayTheRequiemCode/Shops/MerchantLedger.cs#L53-L57
[operation-core]: https://github.com/Riyozaki/SlayTheRequiem/blob/03d636049e2a48101389be7f5856c3976b7730e2/mod/SlayTheRequiemCode/Core/OperationContracts.cs#L5-L78
[payload-core]: https://github.com/Riyozaki/SlayTheRequiem/blob/03d636049e2a48101389be7f5856c3976b7730e2/mod/SlayTheRequiemCode/Core/SavedPayload.cs
[pack-transfer]: https://github.com/Riyozaki/SlayTheRequiem/blob/03d636049e2a48101389be7f5856c3976b7730e2/mod/SlayTheRequiemCode/Alchemy/TravelPack.cs#L155-L445
[acquisition]: https://github.com/Riyozaki/SlayTheRequiem/blob/03d636049e2a48101389be7f5856c3976b7730e2/mod/SlayTheRequiemCode/Rules/CardAcquisition.cs#L78-L157
[scrolls]: https://github.com/Riyozaki/SlayTheRequiem/blob/03d636049e2a48101389be7f5856c3976b7730e2/mod/SlayTheRequiemCode/Shops/Scrolls.cs#L66-L126
[reward-swap]: https://github.com/Riyozaki/SlayTheRequiem/blob/03d636049e2a48101389be7f5856c3976b7730e2/mod/SlayTheRequiemCode/Rules/RewardSwap.cs
[reward-trace]: https://github.com/Riyozaki/SlayTheRequiem/blob/03d636049e2a48101389be7f5856c3976b7730e2/mod/SlayTheRequiemCode/Debugging/CardRewardDiagnostics.cs
[reward-panel]: https://github.com/Riyozaki/SlayTheRequiem/blob/03d636049e2a48101389be7f5856c3976b7730e2/mod/SlayTheRequiemCode/UI/RewardAcquisitionPanel.cs#L16-L76
[shop-policy]: https://github.com/Riyozaki/SlayTheRequiem/blob/03d636049e2a48101389be7f5856c3976b7730e2/mod/SlayTheRequiemCode/Shops/ShopActionPolicy.cs
[shop-operations]: https://github.com/Riyozaki/SlayTheRequiem/blob/03d636049e2a48101389be7f5856c3976b7730e2/mod/SlayTheRequiemCode/Shops/ShopOperations.cs#L142-L254
[integrity-harness]: https://github.com/Riyozaki/SlayTheRequiem/blob/03d636049e2a48101389be7f5856c3976b7730e2/mod/SlayTheRequiemCode/Debugging/IntegrityRegression.cs#L85-L140
[registry]: https://github.com/Riyozaki/SlayTheRequiem/blob/03d636049e2a48101389be7f5856c3976b7730e2/docs/plan/implementation-registry.md
[game-clone]: https://github.com/zhiyue/sts2-rl-agent/blob/5d874e855ae1d0550d5c30416fadd7dbb0d133b7/decompiled/MegaCrit.Sts2.Core.Models/AbstractModel.cs#L162-L174
[baselib-relic]: https://github.com/Alchyr/BaseLib-StS2/blob/6fc4adb72556f2f30a57e540dbe7722ce0827aa8/Abstracts/CustomRelicModel.cs
[other-audit]: https://github.com/Riyozaki/SlayTheRequiem/blob/03d636049e2a48101389be7f5856c3976b7730e2/docs/review/project-audit-2026-11-17.md
[game-reward]: https://github.com/zhiyue/sts2-rl-agent/blob/5d874e855ae1d0550d5c30416fadd7dbb0d133b7/decompiled/MegaCrit.Sts2.Core.Rewards/CardReward.cs#L234-L285
[game-act]: https://github.com/zhiyue/sts2-rl-agent/blob/5d874e855ae1d0550d5c30416fadd7dbb0d133b7/decompiled/MegaCrit.Sts2.Core.Runs/RunManager.cs#L1251-L1282
[game-event]: https://github.com/zhiyue/sts2-rl-agent/blob/5d874e855ae1d0550d5c30416fadd7dbb0d133b7/decompiled/MegaCrit.Sts2.Core.Rooms/EventRoom.cs#L56-L83
[defense]: https://github.com/Riyozaki/SlayTheRequiem/blob/03d636049e2a48101389be7f5856c3976b7730e2/mod/SlayTheRequiemCode/Powers/DefensePowers.cs#L67-L83
[shout-return]: https://github.com/Riyozaki/SlayTheRequiem/blob/03d636049e2a48101389be7f5856c3976b7730e2/mod/SlayTheRequiemCode/Cards/Shouts.cs#L65-L77
[game-card-combat]: https://github.com/zhiyue/sts2-rl-agent/blob/5d874e855ae1d0550d5c30416fadd7dbb0d133b7/decompiled/MegaCrit.Sts2.Core.Models/CardModel.cs#L1017-L1028
[reward-il]: https://github.com/Riyozaki/SlayTheRequiem/blob/03d636049e2a48101389be7f5856c3976b7730e2/mod/SlayTheRequiemCode/Rules/CardAcquisition.cs#L241-L305
[inventory-gate]: https://github.com/Riyozaki/SlayTheRequiem/blob/03d636049e2a48101389be7f5856c3976b7730e2/mod/SlayTheRequiemCode/Rules/InventoryGate.cs#L19-L36
[burden-gate]: https://github.com/Riyozaki/SlayTheRequiem/blob/03d636049e2a48101389be7f5856c3976b7730e2/mod/SlayTheRequiemCode/Alchemy/TravelPack.cs#L628-L634
