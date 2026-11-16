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
using SlayTheRequiem.SlayTheRequiemCode.Extensions;
using SlayTheRequiem.SlayTheRequiemCode.Spells;

namespace SlayTheRequiem.SlayTheRequiemCode.Shops;

// Свитки Чародейской лавки (docs/design/14 §2.3, 07 §3.4): одноразовое заклинание в слоте зелий (пояс).
// Применение кладёт в руку карту-заклинание за 1 ⚡ и 0 ✦ с Истощением; владение школой не проверяется.
// Ступень свитка — часть типа (сейв зелья хранит только id), заклинание тоже задаёт класс.

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

    /// <summary>Переписать свиток (Чародей, 40): заклинание свитка навсегда в колоду, свиток исчезает.</summary>
    public static async Task<bool> Rewrite(Player player, RequiemScroll scroll)
    {
        var card = player.RunState.CreateCard(scroll.Spell, player);
        await PotionCmd.Discard(scroll);
        await CardPileCmd.Add(card, PileType.Deck);
        return true;
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
