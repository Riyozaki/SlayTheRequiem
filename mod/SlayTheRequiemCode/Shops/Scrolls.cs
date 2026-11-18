using BaseLib.Abstracts;
using BaseLib.Extensions;
using BaseLib.Utils;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Potions;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.PotionPools;
using SlayTheRequiem.SlayTheRequiemCode.Core;
using SlayTheRequiem.SlayTheRequiemCode.Extensions;
using SlayTheRequiem.SlayTheRequiemCode.Rules;
using SlayTheRequiem.SlayTheRequiemCode.Spells;

namespace SlayTheRequiem.SlayTheRequiemCode.Shops;

// Свитки Чародейской лавки (docs/design/14 §2.3, 07 §3.4): одноразовое заклинание в слоте зелий (пояс).
// Применение кладёт в руку карту-заклинание за 1 ⚡ и 0 ✦ с Истощением; владение школой не проверяется.
// Ступень свитка и заклинание задаёт класс. SavedProperties сохраняют идентичность и защиту от повтора переписывания.

/// <summary>Свиток: ступень I–III и заклинание, которое он сотворяет.</summary>
public abstract class RequiemScroll : CustomPotionModel
{
    /// <summary>Энергия карты со свитка (07 §3.4: «стоит 0 ✦ и 1 энергию»).</summary>
    public const int CastEnergy = 1;

    /// <summary>1, 2 или 3 (I / II / III).</summary>
    public abstract int Tier { get; }

    /// <summary>Каноническое заклинание свитка.</summary>
    public abstract SpellCard Spell { get; }

    public override PotionRarity Rarity => Tier >= 3 ? PotionRarity.Rare : Tier >= 2 ? PotionRarity.Uncommon : PotionRarity.Common;
    public override PotionUsage Usage => PotionUsage.CombatOnly;
    public override TargetType TargetType => TargetType.Self;

    protected virtual string VanillaArt => Tier switch { >= 3 => "liquid_memories", 2 => "power_potion", _ => "skill_potion" };
    private string ImageName => $"{Id.Entry.RemovePrefix().ToLowerInvariant()}.png";
    public override string? CustomPackedImagePath => ImageName.PotionImagePath(VanillaArt);
    public override string? CustomPackedOutlinePath => ImageName.PotionImagePath(VanillaArt, outline: true);

    protected override async Task OnUse(PlayerChoiceContext ctx, Creature? target)
    {
        if (CombatManager.Instance.IsOverOrEnding || Owner.Creature.CombatState is not { } combat) return;
        var card = combat.CreateCard(Spell, Owner);
        if (card is SpellCard spell) spell.MakeFreeCast(CastEnergy);
        await CardPileCmd.AddGeneratedCardToCombat(card, PileType.Hand, Owner);
    }

    /// <summary>Все канонические свитки (для витрины), упорядочены по id — одинаково у всех игроков.</summary>
    public static List<RequiemScroll> All() =>
        ModelDb.AllPotions.OfType<RequiemScroll>().OrderBy(s => s.Id.Entry, StringComparer.Ordinal).ToList();

    private bool _rewriteCommitted;
    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public bool RewriteCommitted { get => _rewriteCommitted; set { AssertMutable(); _rewriteCommitted = value; } }

    private string _rewriteClaimKey = "";
    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public string RewriteClaimKey { get => _rewriteClaimKey; set { AssertMutable(); _rewriteClaimKey = value ?? ""; } }

    /// <summary>
    /// Shop OR library event: veto never spends the scroll/price. A durable q: claim is attached BEFORE obtain
    /// hooks, so a save made during insertion reconciles the existing deck/chest card instead of creating another.
    /// Payment follows an observed commit; RewriteCommitted precedes payment/discard. A partial paid result is NOT
    /// retried as a new sale. Nested callers must explicitly borrow their inventory lease.
    /// </summary>
    public static async Task<OperationResult> Rewrite(Player player, RequiemScroll scroll, Func<Task>? pay = null,
        OperationGate.Lease? held = null)
    {
        using var lease = await InventoryGate.For(player).Enter(held);
        if (lease == null) return new(OperationStatus.Busy, scroll.Id.Entry, "Inventory synchronization blocked");
        if (InventoryGate.For(player).Blocked || !RequiemMode.IsInSystem(player) || player.Creature.IsDead || CombatManager.Instance.IsInProgress
            || scroll.Owner != player || !player.PotionSlots.Contains(scroll)) return OperationResult.Reject(scroll.Id.Entry, "Stale scroll instance/environment");
        if (scroll.RewriteCommitted) return new(OperationStatus.Stale, scroll.Id.Entry, "This scroll already committed a card; inspect the previous partial result");
        var key = InventoryIdentity.PotionKey(scroll);
        if (scroll.RewriteClaimKey.Length == 0) scroll.RewriteClaimKey = InventoryIdentity.NewClaimKey(player);
        var claim = scroll.RewriteClaimKey;
        var beforeGold = player.Gold;
        bool HasCommit() => player.Deck.Cards.Any(c => CardAcquisition.ClaimKey(c) == claim)
            || Alchemy.TravelPack.Of(player)?.StoredCards.Any(s => CardAcquisition.ClaimKey(s) == claim) == true;
        CardModel? card = null;
        if (!HasCommit())
        {
            card = player.RunState.CreateCard(scroll.Spell, player);
            CardAcquisition.MarkClaim(card, claim);
            CardAcquisition.BeginAdd(card);
        }
        var result = await TransferProtocol.MoveAsync(key,
            async () => card == null || (await CardPileCmd.Add(card, PileType.Deck)).success,
            HasCommit,
            async () =>
            {
                using var mutation = InventoryGate.Track(player, lease, $"scroll-finalize:{key}");
                scroll.RewriteCommitted = true; // BEFORE awaits: a failed finalizer cannot mint another card on retry/load.
                Exception? error = null;
                try { if (pay != null) await pay(); }
                catch (Exception e) { error = e; }
                try { await PotionCmd.Discard(scroll); }
                catch (Exception e) { error = error == null ? e : new AggregateException(error, e); }
                finally { if (card != null) CardAcquisition.CleanUncommitted(card, player); }
                if (player.PotionSlots.Contains(scroll)) error = new InvalidOperationException("Card committed but scroll discard did not commit", error);
                if (error != null) throw error;
                foreach (var actual in player.Deck.Cards.Where(c => CardAcquisition.ClaimKey(c) == claim)) CardAcquisition.MarkClaim(actual, "");
                mutation.Complete();
            },
            () =>
            {
                if (card != null) CardAcquisition.CleanUncommitted(card, player);
                return Task.CompletedTask;
            });
        MainFile.Logger.Info($"[CardReward] phase=scroll-rewrite target={key} status={result.Status} {result.Detail}");
        if (result.Status == OperationStatus.PartialCommit) InventoryGate.Block(player, $"Scroll rewrite {key}: {result.Detail}");
        return result with { Gold = beforeGold - player.Gold };
    }

}

// ───── I (Новичок) ─────

[Pool(typeof(TokenPotionPool))]
public sealed class ScrollOfFirebolt : RequiemScroll
{
    public override int Tier => 1;
    public override SpellCard Spell => ModelDb.Card<Firebolt>();
}

[Pool(typeof(TokenPotionPool))]
public sealed class ScrollOfHealingLight : RequiemScroll
{
    public override int Tier => 1;
    public override SpellCard Spell => ModelDb.Card<HealingLight>();
}

[Pool(typeof(TokenPotionPool))]
public sealed class ScrollOfOakflesh : RequiemScroll
{
    public override int Tier => 1;
    public override SpellCard Spell => ModelDb.Card<Oakflesh>();
}

// ───── II (Ученик) ─────

[Pool(typeof(TokenPotionPool))]
public sealed class ScrollOfSunbeam : RequiemScroll
{
    public override int Tier => 2;
    public override SpellCard Spell => ModelDb.Card<Sunbeam>();
}

[Pool(typeof(TokenPotionPool))]
public sealed class ScrollOfFlameCloak : RequiemScroll
{
    public override int Tier => 2;
    public override SpellCard Spell => ModelDb.Card<FlameCloak>();
}

[Pool(typeof(TokenPotionPool))]
public sealed class ScrollOfConfusion : RequiemScroll
{
    public override int Tier => 2;
    public override SpellCard Spell => ModelDb.Card<Confusion>();
}

// ───── III (Адепт) ─────

[Pool(typeof(TokenPotionPool))]
public sealed class ScrollOfFireball : RequiemScroll
{
    public override int Tier => 3;
    public override SpellCard Spell => ModelDb.Card<Fireball>();
}

[Pool(typeof(TokenPotionPool))]
public sealed class ScrollOfIceSpike : RequiemScroll
{
    public override int Tier => 3;
    public override SpellCard Spell => ModelDb.Card<IceSpike>();
}

[Pool(typeof(TokenPotionPool))]
public sealed class ScrollOfStoneflesh : RequiemScroll
{
    public override int Tier => 3;
    public override SpellCard Spell => ModelDb.Card<Stoneflesh>();
}
