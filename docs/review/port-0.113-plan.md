# План порта Slay the Requiem: игра 0.111.1 → 0.113.0 (2026-11-18)

> **Статус: НЕ ПРИМЕНЁН.** Порт был влит в PR #8 по ошибке: стабильная версия игры — 0.111.1, а 0.113.0 — бета.
> Код откатан к 0.111.1. Документ оставлен как готовый план на случай, когда 0.113 выйдет в стабильную ветку.
> Сами правки можно взять из коммита `4861e21` (`git show 4861e21 -- mod/`).

Мод был собран под API игры 0.111.1, потом игра обновилась (бета-ветка, 0.113.0). Изменения API сверялись статически:
декомпиляция 0.111.0 (Flaaax/STS2-source, коммит `706ef1c`) против 0.113.0 (`7ca913a`). **Здесь ничего не компилировалось**:
в песочнице нет .NET SDK и DLL игры. Если после сборки появятся ошибки, их надо собрать (`dotnet build`) и разобрать.

## Как проверяли

1. Цели Harmony (`[HarmonyPatch(typeof(X), "Метод", типы…)]`): есть ли метод в 0.113, совпадает ли число аргументов,
   совпадают ли имена параметров префиксов и постфиксов с параметрами цели.
2. `AccessTools.FieldRefAccess<…>("_поле")`, `AccessTools.Method/Field(…, "имя")`: есть ли поле или метод и не поменялся ли тип.
3. `override` в моде против изменившихся `virtual`/`abstract` членов базовых классов игры.
4. Публичные и приватные (сборка публикуется через Publicizer) члены, которые мод вызывает и которые удалены или сменили
   сигнатуру без совместимых значений по умолчанию.
5. Транспайлеры: сравнение исходников тел методов (`Hook.ModifyDamageInternal`, `CardReward.OnSelect`,
   `NRestSiteCharacter._Ready`, `CardFactory.RollForUpgrade`, `Darv`, `LanternKey`). Якоря транспайлеров на месте.

## Что исправлено

| Где | Что было в 0.111 | Что стало в 0.113 | Исправление |
|---|---|---|---|
| 17 реликвий (`ActLedger`, `PackMule`, `BoonRelic`, `Celestials`, `PactForms`…) | `virtual RelicModel.IsAllowed` | `IsAllowed` невиртуальный, переопределяется `protected virtual IsAllowedInternal` | `override IsAllowed` → `override IsAllowedInternal`. Без этого старая DLL «выключала» реликвии впустую, и служебные реликвии могли выпадать в наградах |
| 11 монстров и сил (`ActBosses`, `Superbosses`, `WerewolfForm`, `RequiemCombatRules`…) | `AfterDeath(ctx, creature, wasRemovalPrevented, animLen)` | добавлен `CardModel? source` | сигнатуры и вызовы `base.AfterDeath` обновлены |
| `CreatureCmd.LoseMaxHp` (7 мест) | `bool isFromCard` | `CardModel? cardSource` | `cardSource: null` |
| `RelicFactory` | `RollRarity(Player)`, `PullNextRelicFromFront(Player)` | удалены | `RollRarity(player.PlayerRng.Shops)` (как ванильная лавка 0.113), `PullNextRelicForRewardFromFront` |
| `MultiplayerScalingModel` | namespace `Models.Singleton` | `Models` | убран лишний `using` |
| `RunManager.ProceedFromTerminalRewardsScreen` | `async Task` | `void` (+ `…Async()`) | `await …Async()` |
| `NCombatRoom.SetCreatureIsInteractable` | был | удалён | `NCreature.SetIsInteractable(true, false)` |
| `CreatureCmd.Damage` (патч `DamageDefenseScopePatch`, `[CriticalPatch]`) | ядро — перегрузка с `IEnumerable<Creature>` (7 аргументов) | ядро — `IEnumerable<DamageTargetData>` + `ModifyDamageHookType` | патч перенацелен на новое ядро |
| `StartRunLobby` ctor (`[CriticalPatch]`) | `(GameMode, INetGameService, IStartRunLobbyListener, int)` | добавлен `bool isCustom` вторым | типы в `HarmonyPatch` обновлены |
| `StartRunLobby.ChangeCharacter` | параметр `isRandomCharacterResolution` | его нет; случайный герой разрешается в `BeginRunLocally` | префикс больше не просит несуществующий параметр (Harmony падал бы); окно `BeginRunLocally` отмечается счётчиком |
| Лавка: витрина | `MerchantInventory.Populate*Entries` (экземплярные) | статические `GetDefault*Entries(Player)`, затем `Setup()` | префиксы перенесены на `GetDefault*Entries` и возвращают свой список. Скидку и `Populate` делает ванильный `Setup()` |
| Лавка: покупка | `MerchantEntry.OnTryPurchaseWrapper` (локально) | `MerchantSynchronizer.PurchaseLocalEntry` шлёт покупку по сети, потом `OnTryPurchaseUnsynchronized` | окна «владение школой», «перегруз», «кража» стали префиксами `PurchaseLocalEntry`, то есть стоят до отправки сообщения. После подтверждения покупка идёт обычным синхронным путём |
| Кража в коопе | — | покупка «без цены» по сети не передаётся | **временно выключена в коопе** (`CanSteal`). В одиночной игре работает как раньше |
| `NGameOverScreen._serializableRun` (Камень памяти) | поле | заменено на `_result : PostRunProgressUpdateResult` | `ResultRef(...).run` |
| `TreasureRoomRelicSynchronizer._playerCollection` (Плут) | поле | заменено на `_runState` | читаем `_runState.Players` |
| Манифест, README, docs/release | `min_game_version 0.111.1` | — | `0.113.0` |

## Проверено, совместимо без правок

`SaveManager.UpdateProgressWithRunData` (теперь возвращает результат; постфиксу всё равно),
`Player.PopulateCombatState` (`ICombatState`), `ScoreUtility.CalculateScore` (публичные перегрузки те же),
`Glory`/`Underdocks.AllEvents` (постфикс дописывает), `Hook.ShouldTakeExtraTurn`/`AfterTakingExtraTurn` (виртуалы модели
не менялись), `CardPileCmd.Add/Draw`, конструкторы `CombatState`/`CardReward`/`RunState.CreateForTest`
(вызовы с именованными аргументами), `VfxCmd.PlayOnCreatureCenter`, `NMerchantInventory.Initialize`, `UpdateVisual`
слотов лавки, транспайлер `PlatingFinalStep` (флаг cap всё ещё аргумент 8, локали 0/1 — `decimal`/`List`).

## Что осталось непроверенным

- **Сборка.** Её нужно запустить у себя. Ошибки компилятора — первый источник правды.
- Узлы и пути сцен Godot игры (`GetNode`, `%Unique`): .tscn в декомпиляции нет.
- Новые режимы 0.113 (Blitz-лавка, Dominion, стрелы) с системами мода не сочетались. Blitz-лавка мод не трогает:
  `GetDefault*Entries` в ней не вызываются.
- Причина «Не удается создать новую страницу защиты для стека» (переполнение стека) статически **не доказана**.
  Несовместимость версий подтверждена, но после пересборки нужен лог игры, если вылет останется.
