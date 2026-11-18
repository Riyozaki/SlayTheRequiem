# Текущий реестр реализации и доказательств

**Обновлён:** 17 ноября 2026. **Область текущей работы:** G0/G1 [плана стабилизации](stabilization-roadmap-2026-11-17.md).
**Базовый срез аудита:** `8000886e9c52db78227972bdd4740ee71f7eca03`. Рабочая копия содержит последующие исправления; фактические git SHA/dirty и source hash прогона записаны в evidence. Это не собранный release.

> **G0 и G1 НЕ закрыты.** Код исправлений, инструменты сборки и регрессионные сценарии добавлены. В этой среде нет .NET SDK 9, MegaDot и предоставленной легальной установки игры. Полная компиляция, xUnit нового кода, настоящий экспорт PCK, игровой адаптер, disk Continue и два клиента пока не проверены. Зелёные syntax/Harmony/Python не заменяют эти результаты.

Это **единственный текущий реестр статусов**, а не ещё один параллельный roadmap. Исходная постановка и критерии — в плане G0–G6; [аудит `8000886`](../review/project-audit-2026-11-17.md) и прежние M0–M9/этапы 0–8 — история и детализация, не текущая приёмка. «Код» ниже означает наличие реализации в исходниках, **не** успешную компиляцию.

## 1. Пять независимых состояний

| Система / задача | Спроектировано | Реализовано | Автоматизировано | Проверено в игре | Визуально принято |
|---|---|---|---|---|---|
| G0: полная сборка + отдельный PCK + чистая установка | G0 | `tools/g0`, pinned dependencies; запуск блокирован окружением | 29 Python-тестов pipeline, **синтетические**; не build/export | Нет | Не применимо |
| G0: паспорт сборки/забега, effective rules/degraded | G0 | `RunDiagnostics`, `NRunStatusBadge`, metadata DLL, CLI build passport | Проверка согласованности schema/protocol в Python; формат source-only | Нет | Нет |
| G0: host snapshot, личный выбор, Continue | G0 | `RunSettings`, nonce, согласование сохранённых снимков, lobby receiver bridge для BaseLib 3.4.8 | Core encoding tests добавлены; новый xUnit не запущен | Нет, лобби/два клиента открыты | Нет |
| G1.1: карты сундука, per-item outcomes, veto/late exception | 02, 10; A01 | `TravelPack`, `TransferProtocol`, write-ahead snapshot до detach | Core source tests + игровые сценарии `str_integrity`; последние не выполнены | Нет | Нет |
| G1.1: экземпляры вещей/charges/enchants, legacy/opaque | 07, 10; A02 | JSON экземпляра, legacy names → v2, stable IDs, без obtain/equip при restore | `StoredItemCodec`, `SavedPayloadTests`; legacy insertion-save/collision в harness | Нет | Нет |
| G1.1: full payload comparison и unknown properties | G1 | Directional `SavedPayload.Preserves`: допускаются только добавленные typed defaults; неизвестное не теряется молча | xUnit source: вложенные карты, порядок массивов, дубли имён, malformed JSON | Нет | Не применимо |
| G1.2: обычный reward не завершается на false Add | A03 | Узкий critical IL adapter `CardReward.OnSelect`, actual obtain clone/source mapping | Native choice/replay-сценарии в harness; **IL ещё не исполнялся** | Нет | Нет |
| G1.2: вес/причина до выбора, разгрузка/retry, отдельный skip | G1 | `RewardAcquisitionPanel`, RU/EN, native loop и сохранение upgrade-roll | Локализация проверена; UI/retry в игре не прогонялись | Нет | Нет: 1080p/4K и UI scale открыты |
| G1.2: одноразовые дары/замены, события/покупки, свиток | G1 | Write-ahead claim до первого obtain/veto hook, exact deferred payload/room; card offer платит после actual Add; бесплатный skip отдельно | Harness: pending/clone/paid offer/Library/scroll/brew, save до/после вставки дара; не запущен | Нет | Нет |
| G1.3: pool → model → holder → Add → combat copy/draw/shout trace | A11 | `str_trace`, `CardRewardDiagnostics`, holder/resources и shout stages | Статические Harmony-цели; не выполнение патчей | Нет: исходные «0/2», `nope`, рука и кличи **открыты** | Нет |
| G1.3: фиксированный порядок replacement/fallback, настоящий CardFactory | A11 | `RewardSwap.Guard`, единый coordinator; `str_factory` | 216 smoke / 5712 full случаев **на seed, в исходниках**; не запускались | Нет | Нет |
| G1.4: owner/deck/mode/overload/CanDiscard и реальные цены | A06 | `ShopActionPolicy`, `DeckDiscard`, повторная проверка после выбора; instance keys вместо индекса | Core authority tests + stale quote/owner/overload в harness | Нет | Нет |
| G1.4: услуги/продажа после observed commit, trust по оплате | A06 | `ShopActions`: post-effect finalizers, actual gold delta, явный PartialCommit; camp/enchant/brew тоже наблюдают эффект | Late effect/payment/display и serialized pending в harness; не запущен | Нет | Нет |
| G1.4: id/revision, async gate, replay receipts и co-op agreement | G1 | `OperationGate`, journal 256, saved high-water, run/owner auth, receipt digests; pending до согласия участников | Core source tests, wire round-trip; **не** два клиента/реальная повторная доставка | Нет, блокер G1 | Не применимо |
| G1: save внутри операции, неизвестный результат | G1 | Saved Pending/Block; Continue закрывает новые custom mutations; это не rollback/repair | Нормальные/вложенные/abandoned scopes и JSON reload в harness | Нет: disk save/quit/Continue открыт | Нет |
| Ресурсы/защиты/элементы (02, 03) | Да | Существующий код; контракты A07–A10 остаются работой G2 | Старые Core/sim + статические проверки; не игровые хуки | Текущая приёмка отсутствует | Нет |
| Герои/происхождения/предыстории/Пути/формы (04, 06, 16–19) | Да | Существующий код; в G1 изменена сохранность card grants, не завершён G2 | Catalog/pools/loc; стартовые решения A16 не закрыты | Нет текущего лога | Нет |
| Алхимия/снаряжение/лагерь (07–10) вне G1-контрактов | Да | Существующие механики; массового расширения не было | sim/content; числовая и игровая приёмка впереди | Нет текущего лога | Нет |
| Пантеон (05) | Да | Существующий код; в G1 obtain нового blessing предшествует revoke старого, pending при partial | Один fault-injection adapter case; не запущен | Нет | Нет |
| Враги/акты/события/карта (11, 13) | Да | Существующий код; A04/A05/A13 и UX — G2/G3 | Данные/ассеты/синтаксис; не пятиактный забег | Нет текущего лога | Нет |
| Камень Памяти (15) | Да | Существующий код + общая новая operation timeline при rollback; lifecycle A04/A05 не переделан | Core старых правил; disk rollback/кооп не доказаны | Нет текущего лога | Нет |
| Арт/анимации/звук, баланс и производительность | Спеки есть | Существующие ассеты и прокси; G3–G6 не объявлены реализованными | Генераторы/manifest ≠ ResourceLoader/визуальная приёмка/FPS | Нет текущего лога | Нет |

## 2. Принятые в G0/G1 решения

| Решение | Контракт и ограничение |
|---|---|
| D01 — commit наблюдается | Возвращённый bool/исключение команды не заменяет проверку назначения. Late failure → PartialCommit. Обязательные финализаторы пытаются выполниться все; автоматического отката чужих хуков нет. |
| D02 — relocation ≠ obtain | Сундук не начисляет второй obtain/history-бонус. Вещь помечается stowed до inventory notification; текущий комплект не переодевается. Нативные preview/holder-модели не лишаются Owner посреди анимации. |
| D03 — полная сохранность | Нельзя использовать `SerializableCard.Equals` для разрушительной сверки: он игнорирует часть payload. Непредставимые SavedProperties/недоступная модель сохраняются в источнике, операция отказывается. Новые default SpireFields допустимы только как направленная миграция. Restore/preview сундука и pending grant получает deep copy SavedProperties; финализатор сверяет исходную запись и полный payload до удаления. Совпавший q: с другим payload не считается тем же даром. |
| D04 — legacy и одинаковые экземпляры | Два одноимённых предмета — две записи. Старые имена не содержат утраченных upgrades/charges: их значения не выдумываются. Перед первым restore назначается и сохраняется ID источника. Коллизия ID с иным payload не разрешает удалить источник. |
| D05 — добровольное/принудительное | Добровольный pick сохраняет все native veto hooks и upgrade-roll. TravelPack явно не запрещает навязанные Curse/Status по Грузу/operation block; остальные игровые veto не обходятся. Право на одноразовый дар/замену сохраняется **до** первого Add, не только после отказа. При вставке/obtain clone оно сверяется с уже полученной картой; исходный starter не удаляется заранее. Изменившийся source остаётся для разбора, поздняя ошибка не превращается в новый дар. |
| D06 — paid offer | Событие продаёт принятую карту, а не боевую alternative reward. Цена после Add; бесплатный skip без heal/gold alternatives. Поздняя ошибка оплаты не даёт купить тот же эффект повторно. |
| D07 — Pending/Block | `OperationPending` сохраняется до эффекта/оплаты и до peer agreement в shop adapter. Загруженный pending без живого scope → сохранённый Block. Reload не ремонтирует оплату, не снимает Block и не является atomic disk transaction. Нужны лог и известное непротиворечивое сохранение; вручную очищать marker в save нельзя. |
| D08 — replay и co-op | Journal хранит 256 immutable receipts; persisted revision отказывает старому сообщению даже после eviction/reload, но не восстанавливает старую квитанцию. Отсутствие/расхождение receipt блокирует операции. Pending — локальный lifecycle marker, поэтому исключён из сравнения inventory digest; Block/revision и состояние остаются в контракте. Повтор локально завершённого request переотправляет исходную квитанцию без ожидания gate незавершённого peer agreement; owner retry также переотправляет свою receipt. Runtime co-op ещё не доказан. |
| D09 — старые правила | Согласованный saved snapshot имеет приоритет над меню. Неизвестные legacy solo rules явно помечаются fallback. Legacy co-op с Гримуаром, но без общего snapshot, отказывается продолжаться вместо чтения разных локальных конфигов; исходное сохранение не переписывается. |
| D10 — доказательства | Публичный CI — Core/content. Полный private legal-game build/export — отдельный CLI pipeline. Ни hashes, ни паспорт не являются подписью/доказательством безопасного происхождения; игровой прогон фиксируется отдельно. |

### Согласованные версии форматов

- Игра-цель **0.107.1**, BaseLib **3.4.8**, ModAnalyzers **0.2.1**, .NET SDK **9**, MegaDot **4.5.1 mono**.
- **Save contract schema 3**: диагностическая версия контрактов мода, не версия save-файла самой игры. Дополнены optional saved markers Pending/Block, instance IDs, acquisition/brew obligations. Старые отсутствующие поля получают defaults; неизвестные chest payload остаются opaque.
- Chest envelope **v2**; legacy `StoredItems` читается до успешной миграции. Corrupt/future envelope не перезаписывается пустым.
- Run settings encoding **v1**, shop wire protocol **1**. Новые поля имеют compatibility defaults; разные версии/артефакты у участников не поддерживаются.
- Schema/protocol согласованы в `RunDiagnostics`, assembly metadata и build/install CLI. Это проверяет Python contract test; compiled metadata ещё не проверена.

## 3. Проверки и ближайшая приёмка

Выполненные безопасные проверки и ограничения: [evidence G0/G1](../review/evidence/g0-g1-2026-11-17/README.md). В `str_integrity` сейчас **55 source cases**: добавлены save до/после вставки одноразового дара, полный pending payload, unknown properties, stale/source-change/collision. Прежние 48 сценариев сохранены. Наличие source case не является PASS.

Порядок, без перескока к G2/балансу:

1. На машине с легальной игрой выполнить [полный G0 pipeline](../../tools/g0/README.md): full mod build с warnings/binlog → xUnit → import → отдельный export → isolated pack loader → verify → clean install. Исправить реальные compiler/API ошибки и повторить весь pipeline на свежем output.
2. На диагностическом **singleplayer** run, только BaseLib + мод, вне боя: включить системы, Pantheon и ShopVariety; `str_passport`, `str_integrity`, `str_factory smoke 2`, затем `str_factory full 8`. Сохранить фактические summaries и fingerprints. Fixtures не заменяют экраны награды и disk Continue.
3. В той же DLL/PCK провести интерактивные G1.1/G1.2 сценарии: burden +10/+11, 1/3 карты, veto второй, разгрузка/retry/skip, Eggs clone, paid event/Library/scroll, legacy/две вещи/charges/enchants, save/quit/Continue до/после, отказ/late error оплаты. Проверить причины до клика и per-item UI RU/EN в 1080p и 4K с двумя UI scale.
4. `str_trace on` → минимальное воспроизведение «0/2», `nope`, draw и shout normal/full hand, cooldown/copy/exhaust/end combat. Если не воспроизведено, приложить точную матрицу проверенных конфигураций, **не** закрывать симптом числом моделей.
5. Два клиента с одной парой hashes: new/Continue/rollback settings; sell/enchant/paid offer/discard; повтор сообщения, stale instance/quote, быстрое двойное действие, конфликт с selector, потеря/задержка receipt, save между эффектом и agreement. Сравнить полные состояния, деньги/материалы/доверие/revisions и логи обоих. Любое расхождение — блокер.
6. Только после этих доказательств отмечать колонки «игра/визуально», закрывать G0/G1 и переходить к G2. Исторический build `ec598de` и CI `8000886` не подходят как evidence новых изменений.
