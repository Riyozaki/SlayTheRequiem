using BaseLib.Abstracts;
using BaseLib.Extensions;
using HarmonyLib;
using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.Events;
using MegaCrit.Sts2.Core.Factories;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Acts;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using MegaCrit.Sts2.Core.Rewards;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.ValueProps;
using SlayTheRequiem.SlayTheRequiemCode.Acts;
using SlayTheRequiem.SlayTheRequiemCode.Alchemy;
using SlayTheRequiem.SlayTheRequiemCode.Config;
using SlayTheRequiem.SlayTheRequiemCode.Core;
using SlayTheRequiem.SlayTheRequiemCode.Extensions;
using SlayTheRequiem.SlayTheRequiemCode.Pantheon;
using SlayTheRequiem.SlayTheRequiemCode.Progression;
using SlayTheRequiem.SlayTheRequiemCode.Shops;
using SlayTheRequiem.SlayTheRequiemCode.Spells;

namespace SlayTheRequiem.SlayTheRequiemCode.Events;

/// <summary>
/// Событие акта Реквиема (docs/design/13 §5, completion-plan M5 п. 3): по 6 на 2а, 2б, 4а, 4б и по 3 на оверлеи Улья и Славы.
/// <para>
/// В пул «?» попадает только своего акта: новые акты отдают их в <see cref="RequiemActModel.AllEvents"/>, Улей и Слава —
/// через <see cref="OverlayEventsPatch"/>. Поэтому <c>base(autoAdd: false)</c>: иначе BaseLib сочтёт событие общим
/// (без <c>Acts</c>) и подмешает его во все акты. Числа — в <see cref="Numbers"/> (DynamicVars), тексты берут их оттуда.
/// </para>
/// Картинка — своя <c>images/events/{id}.jpg</c> (tools/art/events), иначе ванильная по vanilla_art.csv.
/// </summary>
public abstract class RequiemEvent() : CustomEventModel(autoAdd: false)
{
    /// <summary>Акт, в «?» которого встречается событие.</summary>
    public abstract ActKind HomeAct { get; }

    /// <summary>Другие акты, где событие тоже встречается (вьючный зверь, M5 п. 5); в ActEvents.For — во всех.</summary>
    public virtual IReadOnlyList<ActKind> AlsoIn => [];

    /// <summary>Числа события: золото, ♥ и т. п. — доступны в текстах как {Имя}.</summary>
    protected abstract IEnumerable<(string Name, int Value)> Numbers { get; }

    protected override IEnumerable<DynamicVar> CanonicalVars =>
        Numbers.Select(n => new DynamicVar(n.Name, n.Value)).ToList();

    public override bool IsAllowed(IRunState runState)
    {
        var act = ActKinds.Current(runState);
        if (act != HomeAct && !AlsoIn.Contains(act)) return false;
        return (!IsVanillaAct(act) || RequiemConfig.EnableRequiemSystems) && Allowed(runState);
    }

    /// <summary>Доп. условие появления (по умолчанию — всегда).</summary>
    protected virtual bool Allowed(IRunState runState) => true;

    /// <summary>Ванильные акты (Подножие, оверлеи Улья и Славы): события мода там — только с системами Реквиема.</summary>
    private static bool IsVanillaAct(ActKind act) => act is ActKind.Foothills or ActKind.Hive or ActKind.Glory;

    public override string? CustomInitialPortraitPath =>
        $"{Id.Entry.RemovePrefix().ToLowerInvariant()}.jpg".EventImagePath(Art.VanillaArt.EventPortrait(GetType().Name));

    protected int N(string name) => DynamicVars[name].IntValue;

    // ───── варианты и страницы ─────

    /// <summary>Вариант INITIAL.options.{key}; <paramref name="enabled"/> = false — тот же текст, но недоступен.</summary>
    protected EventOption Choice(string key, Func<Task> onChosen, bool enabled = true) =>
        new(this, enabled ? onChosen : null, $"{Id.Entry}.pages.INITIAL.options.{key}");

    protected void Finish(string page) => SetEventFinished(L10NLookup($"{Id.Entry}.pages.{page}.description"));

    protected EventOption LeaveChoice() => Choice("LEAVE", () =>
    {
        Finish("LEAVE");
        return Task.CompletedTask;
    });

    /// <summary>Случайное число события [0, n) — ГСЧ события детерминирован (одинаков после загрузки).</summary>
    protected int Roll(int n) => n <= 0 ? 0 : Rng.NextInt(n);

    protected int ActTier => Owner?.RunState is { } run ? ActKinds.Tier(run) + 1 : 1;

    // ───── золото и ♥ ─────

    protected bool CanPay(int gold) => Owner != null && Owner.Gold >= gold;

    protected Task Gold(int amount) => PlayerCmd.GainGold(amount, Owner!);

    protected Task Pay(int amount) => PlayerCmd.LoseGold(amount, Owner!);

    protected async Task Hurt(int amount) =>
        await CreatureCmd.Damage(new ThrowingPlayerChoiceContext(), Owner!.Creature, amount, ValueProp.Unblockable | ValueProp.Unpowered, null, null);

    protected Task Heal(int amount) => CreatureCmd.Heal(Owner!.Creature, amount);

    protected Task GainMaxHp(int amount) => CreatureCmd.GainMaxHp(Owner!.Creature, amount);

    protected Task LoseMaxHp(int amount) =>
        CreatureCmd.LoseMaxHp(new ThrowingPlayerChoiceContext(), Owner!.Creature, Math.Min(amount, Owner!.Creature.MaxHp - 1), isFromCard: false);

    // ───── колода ─────

    protected async Task UpgradeChosen(int count)
    {
        var cards = await CardSelectCmd.FromDeckForUpgrade(Owner!, new CardSelectorPrefs(CardSelectorPrefs.UpgradeSelectionPrompt, count));
        foreach (var card in cards) CardCmd.Upgrade(card, CardPreviewStyle.EventLayout);
    }

    protected void UpgradeRandom(int count)
    {
        var pool = Owner!.Deck.Cards.Where(c => c.IsUpgradable).ToList();
        var picked = new List<CardModel>();
        for (var i = 0; i < count && pool.Count > 0; i++)
        {
            var card = pool[Roll(pool.Count)];
            pool.Remove(card);
            picked.Add(card);
        }
        if (picked.Count > 0) CardCmd.Upgrade(picked, CardPreviewStyle.MessyLayout);
    }

    protected async Task RemoveChosen(int count)
    {
        var cards = (await CardSelectCmd.FromDeckForRemoval(Owner!, new CardSelectorPrefs(CardSelectorPrefs.RemoveSelectionPrompt, count))).ToList();
        if (cards.Count > 0) await CardPileCmd.RemoveFromDeck(cards);
    }

    protected async Task TransformChosen(int count)
    {
        var cards = await CardSelectCmd.FromDeckForTransformation(Owner!, new CardSelectorPrefs(CardSelectorPrefs.TransformSelectionPrompt, count));
        foreach (var card in cards.ToList()) await CardCmd.TransformToRandom(card, Rng, CardPreviewStyle.EventLayout);
    }

    protected Task Curse<T>() where T : CardModel => CardPileCmd.AddCurseToDeck<T>(Owner!);

    /// <summary>Чары мода на выбранную карту (Пламенная, Морозная…), как у Зачарования в лагере.</summary>
    protected async Task Enchant<T>(bool attacksOnly) where T : RequiemCardEnchantment
    {
        var canonical = ModelDb.Enchantment<T>();
        var prefs = new CardSelectorPrefs(CardSelectorPrefs.EnchantSelectionPrompt, 1);
        var picked = (await CardSelectCmd.FromDeckForEnchantment(Owner!, canonical, 1,
            c => c != null && c.Enchantment == null && (!attacksOnly || c.Type == CardType.Attack), prefs)).FirstOrDefault();
        if (picked != null) CardCmd.Enchant(canonical.ToMutable(), picked, 1);
    }

    protected bool HasEnchantable(bool attacksOnly) =>
        Owner != null && Owner.Deck.Cards.Any(c => c.Enchantment == null && (!attacksOnly || c.Type == CardType.Attack));

    protected Task CardReward() => RewardsCmd.OfferCustom(Owner!, [
        new CardReward(CardCreationOptions.ForNonCombatWithDefaultOdds([Owner!.Character.CardPool]), 3, Owner!),
    ]);

    /// <summary>Выбор одного из <paramref name="count"/> заклинаний, доступных герою по владению (как в Чародейской лавке).</summary>
    protected async Task OfferSpells(int count)
    {
        var owner = Owner!;
        var pool = ShopStock.SpellCandidates(owner);
        var cards = new List<CardModel>();
        for (var i = 0; i < count && pool.Count > 0; i++)
        {
            var canonical = pool[Roll(pool.Count)];
            pool.Remove(canonical);
            cards.Add(owner.RunState.CreateCard(canonical, owner));
        }
        if (cards.Count == 0)
        {
            await CardReward();
            return;
        }
        var chosen = await CardSelectCmd.FromChooseACardScreen(new BlockingPlayerChoiceContext(), cards, owner, canSkip: true);
        foreach (var card in cards.Where(c => c != chosen)) owner.RunState.RemoveCard(card);
        if (chosen != null) CardCmd.PreviewCardPileAdd(await CardPileCmd.Add(chosen, PileType.Deck));
    }

    // ───── реликвии, зелья, снаряжение ─────

    protected async Task Relic(RelicRarity? rarity = null)
    {
        var relic = rarity is { } r ? RelicFactory.PullNextRelicFromFront(Owner!, r) : RelicFactory.PullNextRelicFromFront(Owner!);
        await RelicCmd.Obtain(relic.ToMutable(), Owner!);
    }

    /// <summary>Зелье (или яд) Реквиема ступени акта; если их нет — случайное ванильное.</summary>
    protected Task Potion(bool poison = false)
    {
        var brew = ShopStock.PickBrew(Owner!, poison, ActTier);
        return RewardsCmd.OfferCustom(Owner!, [brew != null ? new PotionReward(brew, Owner!) : new PotionReward(Owner!)]);
    }

    /// <summary>Предмет снаряжения ступени <paramref name="minTier"/>–<paramref name="maxTier"/>; нет подходящего — золото.</summary>
    protected async Task Equipment(int minTier, int maxTier)
    {
        var pick = ShopStock.PickEquipment(Owner!, minTier, maxTier, null, new HashSet<Type>());
        if (pick != null) await RelicCmd.Obtain(pick.ToMutable(), Owner!);
        else await Gold(ShopRules.EquipmentPrice(EquipCategory.Armor, minTier) / 2);
    }

    // ───── системы мода ─────

    protected void Reagents(Reagent reagent, int amount) => HerbalistPouch.Of(Owner)?.Add(reagent, amount);

    protected bool RaiseMastery(SpellSchool school) => Mastery.GrimoireOf(Owner)?.Raise(school) ?? false;

    protected void Sin() => Faith.Sin(Owner!);

    protected bool CanRedeem => Owner != null && Faith.SigilOf(Owner) != null && Faith.CanRedeem(Owner);

    protected void Redeem() => Faith.Redeem(Owner!);

    protected ActLedger? Ledger => ActLedger.Of(Owner);
}

/// <summary>События мода по актам (новые акты — <see cref="RequiemActModel.AllEvents"/>, оверлеи — патч ниже).</summary>
public static class ActEvents
{
    public static IEnumerable<EventModel> For(ActKind kind) => kind switch
    {
        ActKind.Foothills => [ModelDb.Event<AbandonedMuleEvent>()],
        ActKind.FrozenBarrows =>
        [
            ModelDb.Event<FrozenPilgrimEvent>(), ModelDb.Event<BarrowSealEvent>(), ModelDb.Event<FrostAltarEvent>(),
            ModelDb.Event<DraugrWakeEvent>(), ModelDb.Event<BurialUrnsEvent>(), ModelDb.Event<SnowboundSledEvent>(),
            ModelDb.Event<AbandonedMuleEvent>(), ModelDb.Event<MuleAmbushEvent>(),
        ],
        ActKind.ClockworkDepths =>
        [
            ModelDb.Event<BrokenGolemEvent>(), ModelDb.Event<SteamValveEvent>(), ModelDb.Event<BrassArchiveEvent>(),
            ModelDb.Event<CoastRaidersEvent>(), ModelDb.Event<AutomatonForgeEvent>(), ModelDb.Event<ClockworkOracleEvent>(),
            ModelDb.Event<AbandonedMuleEvent>(), ModelDb.Event<MuleAmbushEvent>(),
        ],
        ActKind.BloodCourt =>
        [
            ModelDb.Event<MasqueradeEvent>(), ModelDb.Event<BloodFountainEvent>(), ModelDb.Event<CountsDiceEvent>(),
            ModelDb.Event<ThrallMarketEvent>(), ModelDb.Event<BlindMirrorEvent>(), ModelDb.Event<NightCarriageEvent>(),
            ModelDb.Event<MuleAmbushEvent>(),
        ],
        ActKind.StarTower =>
        [
            ModelDb.Event<StarExamEvent>(), ModelDb.Event<LeyWellEvent>(), ModelDb.Event<BrokenOrreryEvent>(),
            ModelDb.Event<RenegadeApprenticeEvent>(), ModelDb.Event<MirrorLibraryEvent>(), ModelDb.Event<StarCartographerEvent>(),
            ModelDb.Event<MuleAmbushEvent>(),
        ],
        ActKind.Hive =>
        [
            ModelDb.Event<WhisperingRiftEvent>(), ModelDb.Event<HiveAmberEvent>(), ModelDb.Event<AshPilgrimEvent>(),
            ModelDb.Event<MuleAmbushEvent>(),
        ],
        ActKind.Glory => [ModelDb.Event<FallenHeraldEvent>(), ModelDb.Event<VoidMerchantEvent>(), ModelDb.Event<LastCampfireEvent>()],
        _ => [],
    };
}

/// <summary>Оверлеи ванильных актов (13 §5): «Трещины Бездны» в Улье и «Вторжение Бездны» в Славе — по 3 события.</summary>
[HarmonyPatch(typeof(Hive), nameof(ActModel.AllEvents), MethodType.Getter)]
public static class OverlayEventsPatch
{
    [HarmonyPostfix]
    public static void Postfix(ref IEnumerable<EventModel> __result) => __result = Append(__result, ActKind.Hive);

    internal static IEnumerable<EventModel> Append(IEnumerable<EventModel> events, ActKind kind)
    {
        var list = events.ToList();
        foreach (var e in ActEvents.For(kind))
            if (list.All(x => x.Id != e.Id)) list.Add(e);
        return list;
    }
}

[HarmonyPatch(typeof(Glory), nameof(ActModel.AllEvents), MethodType.Getter)]
public static class GloryEventsPatch
{
    [HarmonyPostfix]
    public static void Postfix(ref IEnumerable<EventModel> __result) => __result = OverlayEventsPatch.Append(__result, ActKind.Glory);
}

/// <summary>Подножие (ванильные Заросли и Подземные доки): «Брошенный мул» (10 §6, акты 1–2).</summary>
[HarmonyPatch(typeof(Overgrowth), nameof(ActModel.AllEvents), MethodType.Getter)]
public static class OvergrowthEventsPatch
{
    [HarmonyPostfix]
    public static void Postfix(ref IEnumerable<EventModel> __result) => __result = OverlayEventsPatch.Append(__result, ActKind.Foothills);
}

[HarmonyPatch(typeof(Underdocks), nameof(ActModel.AllEvents), MethodType.Getter)]
public static class UnderdocksEventsPatch
{
    [HarmonyPostfix]
    public static void Postfix(ref IEnumerable<EventModel> __result) => __result = OverlayEventsPatch.Append(__result, ActKind.Foothills);
}
