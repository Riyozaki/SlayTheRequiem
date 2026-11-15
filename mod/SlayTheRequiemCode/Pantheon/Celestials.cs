using BaseLib.Utils;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.RelicPools;
using MegaCrit.Sts2.Core.Runs;
using SlayTheRequiem.SlayTheRequiemCode.Core;
using SlayTheRequiem.SlayTheRequiemCode.Equipment;

namespace SlayTheRequiem.SlayTheRequiemCode.Pantheon;

/// <summary>
/// Благословение Небесного (docs/design/05 §3). Держать можно одно, смена — в Святилище другого бога.
/// Сила зависит от праведности: Праведник — полное (с Амулетом и Облачением того же бога), Запятнанный — только первый
/// бонус, Отступник или запрет праведности — ничего (реликвия остаётся, но молчит: искупившись, получите её обратно).
/// </summary>
public abstract class CelestialBlessing : BoonRelic
{
    public abstract Celestial God { get; }

    public BlessingStrength Strength => Faith.Strength(Owner);

    public bool HasAmulet => Loadout.InSlot(Owner, EquipSlot.Amulet) is CelestialAmulet a && a.God == God;
    public bool HasRobe => Loadout.InSlot(Owner, EquipSlot.Body) is CelestialRobe r && r.God == God;

    public bool HasDivineSpirit =>
        PantheonRules.HasDivineSpirit(Strength, HasAmulet, HasRobe, Faith.HasHeavenlyShield(Owner));

    /// <summary>Дим. иконка: 0 — молчит, 1 — тускнеет, 2 — полное.</summary>
    public override bool ShowCounter => true;
    public override int DisplayAmount => Owned ? (int)Strength : 0;

    protected override Boon ComputeBoon()
    {
        var boon = PantheonRules.CelestialTotal(God, Strength, HasAmulet, HasRobe);
        if (HasDivineSpirit) boon += SpiritBoon;
        return boon;
    }

    /// <summary>
    /// Числовая часть Духа божества. Остальные эффекты Духа («повтор хода», копия заклинания, иллюзии без ✦…) —
    /// вместе со ступенью «Небесный щит» на этапе 6: без неё Дух недостижим.
    /// </summary>
    protected virtual Boon SpiritBoon => Boon.None;

    public static CelestialBlessing? Of(Player? player) => player?.Relics.OfType<CelestialBlessing>().FirstOrDefault();

    public static readonly Type[] Blessings =
    [
        typeof(BlessingOfWingedHour), typeof(BlessingOfAnvilFather), typeof(BlessingOfScrollKeeper), typeof(BlessingOfMirrorMaiden),
        typeof(BlessingOfWindMistress), typeof(BlessingOfHearthMother), typeof(BlessingOfThresholdWarden), typeof(BlessingOfMercyShield),
        typeof(BlessingOfThunderCrowned), typeof(BlessingOfSunArcher),
    ];

    public static readonly Type[] Amulets =
    [
        typeof(AmuletOfWingedHour), typeof(AmuletOfAnvilFather), typeof(AmuletOfScrollKeeper), typeof(AmuletOfMirrorMaiden),
        typeof(AmuletOfWindMistress), typeof(AmuletOfHearthMother), typeof(AmuletOfThresholdWarden), typeof(AmuletOfMercyShield),
        typeof(AmuletOfThunderCrowned), typeof(AmuletOfSunArcher),
    ];

    public static readonly Type[] Robes =
    [
        typeof(RobeOfWingedHour), typeof(RobeOfAnvilFather), typeof(RobeOfScrollKeeper), typeof(RobeOfMirrorMaiden),
        typeof(RobeOfWindMistress), typeof(RobeOfHearthMother), typeof(RobeOfThresholdWarden), typeof(RobeOfMercyShield),
        typeof(RobeOfThunderCrowned), typeof(RobeOfSunArcher),
    ];

    public static RelicModel Create(Type type) => ModelDb.GetById<RelicModel>(ModelDb.GetId(type)).ToMutable();

    /// <summary>Принять благословение бога, сняв прежнее (вместе с его ♥).</summary>
    public static async Task Grant(Player player, Celestial god)
    {
        var old = Of(player);
        if (old != null && old.God == god) return;
        if (old != null) await old.RemoveSelf();
        await RelicCmd.Obtain(Create(Blessings[(int)god]), player);
    }
}

[Pool(typeof(SharedRelicPool))]
public sealed class BlessingOfWingedHour : CelestialBlessing
{
    public override Celestial God => Celestial.WingedHour;
    protected override Boon SpiritBoon => new() { ShoutCooldown = 1 };
}

[Pool(typeof(SharedRelicPool))]
public sealed class BlessingOfAnvilFather : CelestialBlessing
{
    public override Celestial God => Celestial.AnvilFather;
    protected override Boon SpiritBoon => new() { HeavyAttackDamage = 3 };
}

[Pool(typeof(SharedRelicPool))]
public sealed class BlessingOfScrollKeeper : CelestialBlessing
{
    public override Celestial God => Celestial.ScrollKeeper;
}

[Pool(typeof(SharedRelicPool))]
public sealed class BlessingOfMirrorMaiden : CelestialBlessing
{
    public override Celestial God => Celestial.MirrorMaiden;
}

[Pool(typeof(SharedRelicPool))]
public sealed class BlessingOfWindMistress : CelestialBlessing
{
    public override Celestial God => Celestial.WindMistress;
    protected override Boon SpiritBoon => new() { PoisonRes = 2 };
}

/// <summary>Матерь Очага: Регенерация 2 в первые 3 хода боя; иммунитет к Параличу.</summary>
[Pool(typeof(SharedRelicPool))]
public sealed class BlessingOfHearthMother : CelestialBlessing
{
    public const int OpeningRegen = 2;
    public const int OpeningTurns = 3;

    public override Celestial God => Celestial.HearthMother;

    protected override async Task OnTurnStarted(PlayerChoiceContext ctx)
    {
        if (Strength == BlessingStrength.None) return;
        var round = Owner.Creature.CombatState?.RoundNumber ?? 99;
        if (round > OpeningTurns || Owner.Creature.CurrentHp >= Owner.Creature.MaxHp) return;
        Flash();
        await CreatureCmd.Heal(Owner.Creature, OpeningRegen);
    }
}

[Pool(typeof(SharedRelicPool))]
public sealed class BlessingOfThresholdWarden : CelestialBlessing
{
    public override Celestial God => Celestial.ThresholdWarden;
    protected override Boon SpiritBoon => new() { ImmuneFrozen = true };
}

[Pool(typeof(SharedRelicPool))]
public sealed class BlessingOfMercyShield : CelestialBlessing
{
    public override Celestial God => Celestial.MercyShield;
}

[Pool(typeof(SharedRelicPool))]
public sealed class BlessingOfThunderCrowned : CelestialBlessing
{
    public override Celestial God => Celestial.ThunderCrowned;
}

[Pool(typeof(SharedRelicPool))]
public sealed class BlessingOfSunArcher : CelestialBlessing
{
    public override Celestial God => Celestial.SunArcher;
    protected override Boon SpiritBoon => new() { EnemyMagicRes = -1 };
}

// ───────────── комплект бога: Амулет (бижутерия) и Облачение (тело) ─────────────

/// <summary>Вещь комплекта бога: сама по себе ничего не даёт — её бонус включает благословение того же бога.</summary>
public interface ICelestialGarment
{
    Celestial God { get; }
}

/// <summary>Амулет бога (Святилище, 120 зол.): второй бонус благословения того же бога у Праведника.</summary>
public abstract class CelestialAmulet : EquipmentRelic, ICelestialGarment
{
    public abstract Celestial God { get; }
    public override RelicRarity Rarity => RelicRarity.Event;
    public override bool IsAllowedInShops => false;
    public override bool IsAllowed(IRunState runState) => false;
    public override EquipSlot Slot => EquipSlot.Amulet;
    public override int Weight => 0;
    public override int Tier => 2;
    public override string? Set => "Celestial" + God;
    public override int MerchantCost => PantheonRules.AmuletPrice;
}

/// <summary>Облачение бога (Святилище, 150 зол.): одеяние, Покров 2; третий бонус благословения того же бога.</summary>
public abstract class CelestialRobe : ArmorRelic, ICelestialGarment
{
    public abstract Celestial God { get; }
    public override RelicRarity Rarity => RelicRarity.Event;
    public override bool IsAllowedInShops => false;
    public override bool IsAllowed(IRunState runState) => false;
    public override EquipSlot Slot => EquipSlot.Body;
    public override ArmorClass Armor => ArmorClass.Robe;
    public override int Tier => 2;
    public override int Ward => 2;
    public override string? Set => "Celestial" + God;
    public override int MerchantCost => PantheonRules.RobePrice;
}

[Pool(typeof(SharedRelicPool))] public sealed class AmuletOfWingedHour : CelestialAmulet { public override Celestial God => Celestial.WingedHour; }
[Pool(typeof(SharedRelicPool))] public sealed class AmuletOfAnvilFather : CelestialAmulet { public override Celestial God => Celestial.AnvilFather; }
[Pool(typeof(SharedRelicPool))] public sealed class AmuletOfScrollKeeper : CelestialAmulet { public override Celestial God => Celestial.ScrollKeeper; }
[Pool(typeof(SharedRelicPool))] public sealed class AmuletOfMirrorMaiden : CelestialAmulet { public override Celestial God => Celestial.MirrorMaiden; }
[Pool(typeof(SharedRelicPool))] public sealed class AmuletOfWindMistress : CelestialAmulet { public override Celestial God => Celestial.WindMistress; }
[Pool(typeof(SharedRelicPool))] public sealed class AmuletOfHearthMother : CelestialAmulet { public override Celestial God => Celestial.HearthMother; }
[Pool(typeof(SharedRelicPool))] public sealed class AmuletOfThresholdWarden : CelestialAmulet { public override Celestial God => Celestial.ThresholdWarden; }
[Pool(typeof(SharedRelicPool))] public sealed class AmuletOfMercyShield : CelestialAmulet { public override Celestial God => Celestial.MercyShield; }
[Pool(typeof(SharedRelicPool))] public sealed class AmuletOfThunderCrowned : CelestialAmulet { public override Celestial God => Celestial.ThunderCrowned; }
[Pool(typeof(SharedRelicPool))] public sealed class AmuletOfSunArcher : CelestialAmulet { public override Celestial God => Celestial.SunArcher; }

[Pool(typeof(SharedRelicPool))] public sealed class RobeOfWingedHour : CelestialRobe { public override Celestial God => Celestial.WingedHour; }
[Pool(typeof(SharedRelicPool))] public sealed class RobeOfAnvilFather : CelestialRobe { public override Celestial God => Celestial.AnvilFather; }
[Pool(typeof(SharedRelicPool))] public sealed class RobeOfScrollKeeper : CelestialRobe { public override Celestial God => Celestial.ScrollKeeper; }
[Pool(typeof(SharedRelicPool))] public sealed class RobeOfMirrorMaiden : CelestialRobe { public override Celestial God => Celestial.MirrorMaiden; }
[Pool(typeof(SharedRelicPool))] public sealed class RobeOfWindMistress : CelestialRobe { public override Celestial God => Celestial.WindMistress; }
[Pool(typeof(SharedRelicPool))] public sealed class RobeOfHearthMother : CelestialRobe { public override Celestial God => Celestial.HearthMother; }
[Pool(typeof(SharedRelicPool))] public sealed class RobeOfThresholdWarden : CelestialRobe { public override Celestial God => Celestial.ThresholdWarden; }
[Pool(typeof(SharedRelicPool))] public sealed class RobeOfMercyShield : CelestialRobe { public override Celestial God => Celestial.MercyShield; }
[Pool(typeof(SharedRelicPool))] public sealed class RobeOfThunderCrowned : CelestialRobe { public override Celestial God => Celestial.ThunderCrowned; }
[Pool(typeof(SharedRelicPool))] public sealed class RobeOfSunArcher : CelestialRobe { public override Celestial God => Celestial.SunArcher; }
