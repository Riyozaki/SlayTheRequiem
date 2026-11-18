# Б2.1 — создание и развитие героя: реализация и границы проверки

**18.11.2026 · исходники `f5028e7df2d3ad6b86f185ee37f01c99e932fbde` · ветка `arena/5906afa4-slaytherequiem`.**

Это реализация первого подпункта [блока 2](../plan/next-playable-release-2026-11-18.md), не закрытие всего блока
и не заявление о свежей игровой сборке. [Выбранные автором решения](../plan/stage-two-decisions-2026-11-18.md) ·
[воспроизводимые evidence](evidence/stage-two-creation-2026-11-18/README.md).
Б1 остаётся отдельным [историческим результатом](stage-one-implementation-2026-11-18.md).

## Что внесено в код

| Область | Реализация | Остаточная проверка |
|---|---|---|
| Обязательный старт | Происхождение → предыстория → **отдельно 60 золота / Common-зелье / удаление одной базовой карты** → подтверждение → реальная сводка → её acknowledgment. Нео/Знамения отдельно; старый Random не пропускает вопросы | Native modal/обычный старт всех героев и комбинации с Нео |
| Возобновление | Сохраняются решения и фазы, baseline колоды/школ/ресурсов/Груза, exact card/potion source. Отмена не становится Random. Receipt не повторяет подарки | Disk Save/Continue на каждой границе, replay/два клиента |
| Отдельные эффекты | Личность и бонус имеют разные mutation receipts. Полный пояс/отмена удаления возвращают только к ещё не выданному бонусу. Бродяга выбирает точную карту или явный skip **до** изменения личности | Native veto/late callback fixtures написаны, не исполнены |
| Владения | База/происхождение/предыстория/стат-покупки/навыки/приобретения имеют отдельные источники, включая вклад, скрытый cap. Родные школы пересчитываются по личности, не по трактату | Полный API build и сверка реальных боевых значений |
| Перерождение | Меняет происхождение и предысторию, возвращает только вложения характеристик и снимает купленное ими владение. Навыки/их оплата, трактаты, карты и добыча сохраняются. Стартовые подарки повторно не запускаются | Native issuer/callback/HP traces и игровая балансная проверка |
| ♥ после respec | HP характеристик/личности и **всех сохранённых BoonRelic** сводятся одним delta перед floor; не последовательными loss/gain с созданием HP на нижней границе | Настоящие callbacks и поздние исключения в движке |
| Пути | Full 5: после босса 3 при level ≥8. Short 1→3→5: после первого босса без level-8 gate. Сохранённое предложение держит исходный порядок/RNG, Continue/Cancel не делают reroll | Реальный маршрут, награда/переход/форма; Б2.4/Б2.5 отдельно |
| Старые saves | Schema 4 / startup protocol 2. Завершённый старый старт сохраняет opaque значения без нового бонуса; неоднозначный старый незавершённый one-shot старт отклоняется до native rewrite. Неизвестная современная операция сохраняется заблокированной для checkpoint recovery | Настоящий converter/disk/cloud/native reload, не только typed fixtures |
| Lobby | Согласование до Ready/BeginRun через **существующий строковый RunSettingsMessage**, без нового неизвестного типа BaseLib. Host/peer membership проверяется; уход/новое лобби стирают согласие. Настоящий snapshot отдельно требует `hero_creation=2`; envelope правил остаётся v1 | Native order/handler bridge, mixed-build и два клиента |
| Room/modal boundaries | До активного боя мастер обязателен; MapRoom/prefinished Continue имеют awaited resume без повтора victory hooks. Refused lease не подавляет same-room retry. После ожиданий проверяются текущие run/room, окно закрывается при уходе из забега | Реальные rewards/scene-tree/free ordering и восстановление событий |
| RU/EN и управление | Скроллируемый wizard, focus, клавиатура/D-pad, явные Back/Confirm, итоговый состав и реальные изменения. Динамические localization keys/placeholder parity под автоматикой | Layout/длинные тексты/масштаб/геймпад в конце цикла; это не UI acceptance |

Ключевые исходники:

- `Core/{HeroCreationPlan,HeroCreationSnapshot,MasterySources,HeroIdentityRules,PathOfferState,StartupState}.cs`;
- `Progression/HeroCreation.cs`, `Rules/HeroProgressionSave.cs`, `Spells/Grimoire.cs`;
- `UI/NHeroCreationScreen.cs`, `Config/RunSettings.cs`, `Net/RequiemMessages.cs`;
- источник-aware `Relics/{Origins,Backgrounds}.cs`, `Pantheon/BoonRelic.cs`, `Transformations/PathEssences.cs`;
- `StageTwoTests.cs`, `Debugging/IntegrityHeroCreation.cs`, `tools/ci/{check_loc,test_check_loc}.py`.

## Фактически исполненная автоматика

[CI 44239623951](https://github.com/Riyozaki/SlayTheRequiem/actions/runs/44239623951) — **четыре jobs PASS, 658/658 Core**,
ноль failed/not-executed. Число из публичной annotation успешного TRX; сам TRX в sandbox не скачан.
Локально **26/26 команд exit 0**: Python **151 sim / 35 G0 / 5 localization-checker**,
C# grammar **277/0**, Harmony-targets **125/0**, локализация **1265/0**, генерация **3713 ключей/язык**,
актуальные каталог/таблицы/пулы/арт/звук и `git diff --check`. В каталоге карт остаётся одно известное предупреждение
`RendingCut`; оно не превращено в утверждение о балансе.

Core/xUnit компилирует **Core**, а не интеграционные `Powers/UI/Rules/Progression` всего мода. Поэтому зелёный CI
не доказывает компилируемость game-facing проекта. Новые **10 native creation fixtures** входят в подготовленный
inventory **77 cases**, но **не скомпилированы и не запускались**. Они также не подменяют реальный ввод/кооп/disk Continue.

## Статус и продолжение

- Исходники Б2.1 и доступная автоматика переданы; полная приёмка остаётся открытой.
- Б2.2–Б2.5 **не закрыты этим отчётом**. Следующий пакет — боевые контракты/роли врагов, затем функциональные экраны,
  поддержка сборок и экономика пяти актов в порядке плана.
- Полный build/PCK при появлении легальной среды — автоматический gate. Ручные, визуальные и двухклиентные
  плейтесты выполняются **только в самом конце всего релизного цикла**, как указал автор.
- Историческая успешная сборка/PCK/плейтест пользователя не отменяется отсутствием новой такой проверки в sandbox.
