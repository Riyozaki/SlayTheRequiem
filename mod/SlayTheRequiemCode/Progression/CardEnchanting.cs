using BaseLib.Abstracts;
using BaseLib.Extensions;
using BaseLib.Utils;
using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.CardPools;
using SlayTheRequiem.SlayTheRequiemCode.Alchemy;
using SlayTheRequiem.SlayTheRequiemCode.Core;
using SlayTheRequiem.SlayTheRequiemCode.Powers;
using SlayTheRequiem.SlayTheRequiemCode.Rules;

namespace SlayTheRequiem.SlayTheRequiemCode.Progression;

/// <summary>
/// Зачарование карт (docs/design/07 §6.2) — ванильные enchantments: Пламенная (+Горение 3), Морозная (+Озноб 2),
/// Грозовая (выжигает 2 ✦ цели), Облегчённая (вес −1), Дыхательная (−1 ◆), Двойная (две чары; перк — узел
/// «Зачарование», Ремесло II). Ставятся за камень душ у зачаровального стола (<see cref="CardEnchantRestSiteOption"/>).
/// </summary>
public abstract class RequiemCardEnchantment : CustomEnchantmentModel
{
    public abstract CardEnchant Kind { get; }

    public virtual IEnumerable<CardEnchant> Kinds => [Kind];

    /// <summary>Свой значок images/enchantments/&lt;id&gt;.png, иначе ванильный по таблице tools/art/vanilla_art.csv.</summary>
    protected override string? CustomIconPath => Art.VanillaArt.FirstExisting(Art.VanillaArt.MissingEnchantmentIcon,
        $"{MainFile.ResPath}/images/enchantments/{Id.Entry.RemovePrefix().ToLowerInvariant()}.png",
        Art.VanillaArt.EnchantmentIcon(GetType().Name));

    public override bool CanEnchantCardType(CardType cardType) =>
        TransformationRules.CanEnchantCard(Kind, cardType == CardType.Attack, doublePerk: true);

    public override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay? cardPlay)
    {
        if (cardPlay == null || Card.Owner?.Creature is not { } hero) return;
        var targets = cardPlay.Target is { IsAlive: true, IsPlayer: false } t
            ? [t]
            : Card.TargetType == TargetType.AllEnemies && hero.CombatState != null
                ? hero.CombatState.HittableEnemies.Where(e => e.IsAlive).ToList()
                : new List<Creature>();
        foreach (var kind in Kinds)
        foreach (var target in targets.Where(x => x.IsAlive))
        {
            switch (kind)
            {
                case CardEnchant.Flaming:
                    await PowerCmd.Apply<BurningPower>(choiceContext, target, TransformationRules.FlamingBurning, hero, Card);
                    break;
                case CardEnchant.Frost:
                    await PowerCmd.Apply<ChillPower>(choiceContext, target, TransformationRules.FrostChill, hero, Card);
                    break;
                case CardEnchant.Storm:
                    if (target.GetPower<EnemyManaPower>() is { } mana) await mana.Drain(TransformationRules.StormManaBurn);
                    break;
            }
        }
    }

    public static bool Has(CardModel card, CardEnchant kind) =>
        card.Enchantment is RequiemCardEnchantment e && e.Kinds.Contains(kind);

    public static Type TypeFor(CardEnchant kind) => kind switch
    {
        CardEnchant.Flaming => typeof(FlamingEnchantment),
        CardEnchant.Frost => typeof(FrostyEnchantment),
        CardEnchant.Storm => typeof(StormEnchantment),
        CardEnchant.Lightened => typeof(LightenedEnchantment),
        CardEnchant.Breathing => typeof(BreathingEnchantment),
        _ => typeof(DoubleEnchantment),
    };
}

public sealed class FlamingEnchantment : RequiemCardEnchantment { public override CardEnchant Kind => CardEnchant.Flaming; }
public sealed class FrostyEnchantment : RequiemCardEnchantment { public override CardEnchant Kind => CardEnchant.Frost; }
public sealed class StormEnchantment : RequiemCardEnchantment { public override CardEnchant Kind => CardEnchant.Storm; }
public sealed class LightenedEnchantment : RequiemCardEnchantment { public override CardEnchant Kind => CardEnchant.Lightened; }
public sealed class BreathingEnchantment : RequiemCardEnchantment { public override CardEnchant Kind => CardEnchant.Breathing; }

/// <summary>Двойная: пара чар в Amount (<see cref="TransformationRules.EncodeDouble"/>).</summary>
public sealed class DoubleEnchantment : RequiemCardEnchantment
{
    public override CardEnchant Kind => CardEnchant.Double;

    public override IEnumerable<CardEnchant> Kinds
    {
        get
        {
            var (a, b) = TransformationRules.DecodeDouble(Amount);
            return [a, b];
        }
    }

    /// <summary>Пара может включать «Облегчённую» — тогда подойдёт и не-атака, если вторая тоже не атакующая.</summary>
    public override bool CanEnchantCardType(CardType cardType) => true;
}

/// <summary>Зачаровать карту (лагерь, у стола): камень душ — одна чара на карту колоды.</summary>
public sealed class CardEnchantRestSiteOption(Player owner) : RequiemRestSiteOption(owner)
{
    public override string OptionId => "SLAYTHEREQUIEM_CARD_ENCHANT";
    public override string FallbackIcon => "LIFT";
    public override CampAction Action => CampAction.Enchant;

    protected override bool Available => HeroRecord.Of(Owner) is { TotalStones: > 0 };

    protected override void Fill(LocString loc) => loc.Add("Stones", HeroRecord.Of(Owner)?.TotalStones ?? 0);

    private static Type PickFor(CardEnchant kind) => kind switch
    {
        CardEnchant.Flaming => typeof(PickCardEnchantFlaming),
        CardEnchant.Frost => typeof(PickCardEnchantFrost),
        CardEnchant.Storm => typeof(PickCardEnchantStorm),
        CardEnchant.Lightened => typeof(PickCardEnchantLightened),
        CardEnchant.Breathing => typeof(PickCardEnchantBreathing),
        _ => typeof(PickCardEnchantDouble),
    };

    protected override async Task<bool> Perform()
    {
        var hero = HeroRecord.Of(Owner);
        if (hero == null) return false;
        var power = new[] { 1, 2, 3 }.FirstOrDefault(p => hero.Stones(p) > 0);
        if (power == 0) return false;
        var result = await EnchantFlow(Owner, () => hero.Stones(power) > 0,
            () => Task.FromResult(hero.SpendStone(power)), InventoryLease);
        return result.Changed;
    }

    /// <summary>
    /// Зачаровать карту колоды: синхронный выбор, повторная проверка цены/цели, наблюдаемое наложение, затем оплата.
    /// Отмена/отказ не тратят цену; исключение после EnchantInternal — явный PartialCommit, без повторного наложения.
    /// </summary>
    public static async Task<OperationResult> EnchantFlow(Player owner, Func<bool> canCommit, Func<Task<bool>> commit,
        OperationGate.Lease? held = null)
    {
        using var lease = await InventoryGate.For(owner).Enter(held);
        if (lease == null) return new(OperationStatus.Busy, "card-enchant", "Inventory synchronization blocked");
        var room = owner.RunState.CurrentRoom;
        var perk = HeroRecord.Of(owner)?.Has(Skill.CraftEnchanting) == true;
        var kinds = Enum.GetValues<CardEnchant>().Where(k => k != CardEnchant.Double || perk).ToList();
        if (await CampMenu.Pick(owner, kinds.Select(k => new CampMenu.Choice(PickFor(k), 0, 0, k)).ToList(),
                "SLAYTHEREQUIEM_CARD_ENCHANT_PROMPT") is not CardEnchant kind) return new(OperationStatus.Cancelled, "card-enchant");

        var amount = 1;
        var attacksOnly = kind != CardEnchant.Lightened;
        if (kind == CardEnchant.Double)
        {
            var singles = Enum.GetValues<CardEnchant>().Where(k => k != CardEnchant.Double).ToList();
            if (await CampMenu.Pick(owner, singles.Select(k => new CampMenu.Choice(PickFor(k), 0, 0, k)).ToList(),
                    "SLAYTHEREQUIEM_CARD_ENCHANT_PROMPT") is not CardEnchant first) return new(OperationStatus.Cancelled, "card-enchant");
            var rest = singles.Where(k => TransformationRules.ValidDouble(first, k)).ToList();
            if (await CampMenu.Pick(owner, rest.Select(k => new CampMenu.Choice(PickFor(k), 0, 0, k)).ToList(),
                    "SLAYTHEREQUIEM_CARD_ENCHANT_PROMPT") is not CardEnchant second) return new(OperationStatus.Cancelled, "card-enchant");
            amount = TransformationRules.EncodeDouble(first, second);
            attacksOnly = first != CardEnchant.Lightened || second != CardEnchant.Lightened;
        }

        var canonical = ModelDb.GetById<EnchantmentModel>(ModelDb.GetId(RequiemCardEnchantment.TypeFor(kind)));
        var prefs = new CardSelectorPrefs(CardSelectorPrefs.EnchantSelectionPrompt, 1) { Cancelable = true };
        var picked = (await CardSelectCmd.FromDeckForEnchantment(owner, canonical, amount,
            c => c != null && (!attacksOnly || c.Type == CardType.Attack), prefs)).FirstOrDefault();
        if (picked == null) return new(OperationStatus.Cancelled, "card-enchant");
        return await EnchantSelected(owner, picked, canonical, amount,
            () => ReferenceEquals(room, owner.RunState.CurrentRoom) && (!attacksOnly || picked.Type == CardType.Attack) && canCommit(), commit, lease);
    }

    /// <summary>Production adapter, also exercised by str_integrity without replacing the native selector or command.</summary>
    internal static async Task<OperationResult> EnchantSelected(Player owner, CardModel card, EnchantmentModel canonical,
        int amount, Func<bool> canCommit, Func<Task<bool>> commit, OperationGate.Lease? held = null)
    {
        using var lease = await InventoryGate.For(owner).Enter(held);
        if (lease == null) return new(OperationStatus.Busy, "card-enchant", "Inventory synchronization blocked");
        if (InventoryGate.For(owner).Blocked || !RequiemMode.IsInSystem(owner) || owner.Creature.IsDead || MegaCrit.Sts2.Core.Combat.CombatManager.Instance.IsInProgress
            || !Shops.ShopActionPolicy.OwnsCard(owner, card) || !canonical.CanEnchant(card) || amount <= 0 || !canCommit())
            return OperationResult.Reject("card-enchant", "Card enchant target or payment changed after selection");
        var key = InventoryIdentity.CardKey(card);
        using var mutation = InventoryGate.Track(owner, lease, $"card-enchant:{key}");
        var before = card.Enchantment;
        var beforeAmount = before?.Amount;
        var result = await TransferProtocol.MoveAsync(key,
            () => Task.FromResult(CardCmd.Enchant(canonical.ToMutable(), card, amount) != null),
            () => !ReferenceEquals(card.Enchantment, before) || card.Enchantment?.Amount != beforeAmount,
            () => TransferProtocol.Finish(
                async () => { if (!await commit()) throw new InvalidOperationException("Card enchant committed but payment was rejected"); },
                () => { TravelPack.Of(owner)?.Refresh(); HeroRecord.Of(owner)?.Refresh(); return Task.CompletedTask; }));
        MainFile.Logger.Info($"[InventoryOperation] card-enchant {key} {result.Status}: {result.Detail}");
        if (result.Status == OperationStatus.PartialCommit) InventoryGate.Block(owner, $"Card enchant {key}: {result.Detail}");
        mutation.Complete();
        return result;
    }
}

[Pool(typeof(TokenCardPool))] public sealed class PickCardEnchantFlaming : PickCard { }
[Pool(typeof(TokenCardPool))] public sealed class PickCardEnchantFrost : PickCard { }
[Pool(typeof(TokenCardPool))] public sealed class PickCardEnchantStorm : PickCard { }
[Pool(typeof(TokenCardPool))] public sealed class PickCardEnchantLightened : PickCard { }
[Pool(typeof(TokenCardPool))] public sealed class PickCardEnchantBreathing : PickCard { }
[Pool(typeof(TokenCardPool))] public sealed class PickCardEnchantDouble : PickCard { }
