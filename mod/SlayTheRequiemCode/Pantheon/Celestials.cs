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
using SlayTheRequiem.SlayTheRequiemCode.Cards;
using SlayTheRequiem.SlayTheRequiemCode.Powers;
using SlayTheRequiem.SlayTheRequiemCode.Rules;
using SlayTheRequiem.SlayTheRequiemCode.Spells;
using MegaCrit.Sts2.Core.Commands.Builders;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.ValueProps;

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
    /// Числовая часть Духа божества. Нечисловые эффекты (копия заклинания, иллюзии без ✦, лечение +50 %, Сияние, Шок Кличей)
    /// — переопределения в наследниках (этап 6); Дух открывает ступень Праведника «Небесный щит».
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

    /// <summary>Observe the new blessing BEFORE revoking the old one; a failed obtain may not lose both.</summary>
    public static async Task Grant(Player player, Celestial god, OperationGate.Lease? held = null)
    {
        using var lease = await InventoryGate.For(player).Enter(held);
        if (lease == null || InventoryGate.For(player).Blocked || !Enum.IsDefined(god)
            || !RequiemMode.IsInSystem(player) || player.Creature.IsDead
            || MegaCrit.Sts2.Core.Combat.CombatManager.Instance.IsInProgress)
            throw new InvalidOperationException("Blessing change is not available in the current inventory/environment");
        var old = Of(player);
        if (old != null && old.God == god) return;
        using var mutation = InventoryGate.Track(player, lease, $"blessing:{god}");
        var fresh = Create(Blessings[(int)god]);
        var result = await TransferProtocol.MoveAsync($"blessing:{god}",
            async () => { await RelicCmd.Obtain(fresh, player); return true; },
            () => player.Relics.Contains(fresh),
            () => TransferProtocol.Finish(
                async () =>
                {
                    if (old != null && player.Relics.Contains(old)) await old.RemoveSelf();
                    if (old != null && player.Relics.Contains(old)) throw new InvalidOperationException("New blessing committed but the old blessing remains");
                },
                () => { Faith.RefreshAll(player); return Task.CompletedTask; }));
        MainFile.Logger.Info($"[InventoryOperation] {result.Target} {result.Status}: {result.Detail}");
        if (result.Status == OperationStatus.PartialCommit) InventoryGate.Block(player, $"{result.Target}: {result.Detail}");
        mutation.Complete();
        if (!result.Succeeded) throw new InvalidOperationException($"Blessing change {result.Status}: {result.Detail}");
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

    private bool _copiedThisTurn;

    protected override Task OnTurnStarted(PlayerChoiceContext ctx)
    {
        _copiedThisTurn = false;
        return Task.CompletedTask;
    }

    /// <summary>Дух: первое заклинание хода разыгрывается дважды (+3 слота чар — Enchanting.SlotsFor).</summary>
    public override int ModifyCardPlayCount(CardModel card, Creature? target, int playCount)
    {
        if (!Owned || _copiedThisTurn || !HasDivineSpirit || card is not SpellCard || card.Owner != Owner) return playCount;
        _copiedThisTurn = true;
        Flash();
        return playCount + 1;
    }
}

[Pool(typeof(SharedRelicPool))]
public sealed class BlessingOfMirrorMaiden : CelestialBlessing
{
    public override Celestial God => Celestial.MirrorMaiden;

    private bool _freeUsed;

    protected override Task OnTurnStarted(PlayerChoiceContext ctx)
    {
        _freeUsed = false;
        return Task.CompletedTask;
    }

    private bool FreeIllusion(CardModel card) =>
        Owned && !_freeUsed && HasDivineSpirit && card.Owner == Owner && card is SpellCard { School: SpellSchool.Illusion };

    /// <summary>Дух: иллюзии и очарования раз за ход без ✦.</summary>
    public override bool TryModifyStarCost(CardModel card, decimal originalCost, out decimal modifiedCost)
    {
        modifiedCost = originalCost;
        if (originalCost <= 0 || !FreeIllusion(card)) return false;
        modifiedCost = 0;
        return true;
    }

    public override Task AfterCardPlayed(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        if (cardPlay.IsLastInSeries && FreeIllusion(cardPlay.Card))
        {
            _freeUsed = true;
            Flash();
        }
        return Task.CompletedTask;
    }
}

[Pool(typeof(SharedRelicPool))]
public sealed class BlessingOfWindMistress : CelestialBlessing
{
    public override Celestial God => Celestial.WindMistress;
    protected override Boon SpiritBoon => new() { PoisonRes = 2 };
}

/// <summary>Матерь Очага: Регенерация 2 в первые 3 хода боя; иммунитет к Параличу.</summary>
[Pool(typeof(SharedRelicPool))]
public sealed class BlessingOfHearthMother : CelestialBlessing, Transformations.IHealModifier
{
    public const int SpiritHealPercent = 50;

    /// <summary>Дух: любое лечение +50 %.</summary>
    public int HealPercent(Transformations.HealSource source) => Owned && HasDivineSpirit ? SpiritHealPercent : 0;
    public bool BlocksHealing(Transformations.HealSource source) => false;

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
    public const int SpiritRadiance = 3;

    public override Celestial God => Celestial.MercyShield;

    private bool _radiantThisTurn;

    protected override Task OnTurnStarted(PlayerChoiceContext ctx)
    {
        _radiantThisTurn = false;
        return Task.CompletedTask;
    }

    /// <summary>Дух: первая атака хода по нежити/Бездне — Сияние (ещё 3 урона светом).</summary>
    protected override async Task OnOwnAttack(PlayerChoiceContext ctx, AttackCommand command, CardModel card)
    {
        if (_radiantThisTurn || !HasDivineSpirit || card.Type != CardType.Attack) return;
        var targets = command.Results.SelectMany(h => h).Select(r => r.Receiver).Distinct()
            .Where(t => t != null && t.IsAlive && EnemySetup.IsUndeadOrAbyss(t)).ToList();
        if (targets.Count == 0) return;
        _radiantThisTurn = true;
        Flash();
        foreach (var target in targets)
            using (DamageTyping.As(DamageType.Light))
                await CreatureCmd.Damage(ctx, target!, SpiritRadiance, ValueProp.Unpowered, Owner.Creature, null, null);
    }
}

[Pool(typeof(SharedRelicPool))]
public sealed class BlessingOfThunderCrowned : CelestialBlessing
{
    public const int SpiritShockStagger = 2;

    public override Celestial God => Celestial.ThunderCrowned;

    /// <summary>Дух: Кличи накладывают Шок — Ошеломление 2 цели (без цели — всем врагам).</summary>
    public override async Task AfterCardPlayed(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        if (!Owned || !HasDivineSpirit || cardPlay.Card is not IShoutCard || cardPlay.Card.Owner != Owner || !cardPlay.IsLastInSeries) return;
        var combat = Owner.Creature.CombatState;
        if (combat == null) return;
        var targets = cardPlay.Target is { IsAlive: true, IsPlayer: false } t
            ? [t]
            : combat.HittableEnemies.Where(e => e.IsAlive).ToList();
        Flash();
        foreach (var enemy in targets)
            await PowerCmd.Apply<StaggerPower>(choiceContext, enemy, SpiritShockStagger, Owner.Creature, null);
    }
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
    protected override bool IsAllowedInternal(IRunState runState) => false;
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
    protected override bool IsAllowedInternal(IRunState runState) => false;
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
