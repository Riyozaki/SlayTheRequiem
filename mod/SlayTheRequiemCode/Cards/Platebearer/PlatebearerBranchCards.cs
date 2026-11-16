using BaseLib.Utils;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models.CardPools;
using SlayTheRequiem.SlayTheRequiemCode.Core;
using SlayTheRequiem.SlayTheRequiemCode.Equipment;
using SlayTheRequiem.SlayTheRequiemCode.Powers;
using SlayTheRequiem.SlayTheRequiemCode.Progression;
using SlayTheRequiem.SlayTheRequiemCode.Resources;
using SlayTheRequiem.SlayTheRequiemCode.Rules;
using PlatingPower = SlayTheRequiem.SlayTheRequiemCode.Powers.PlatingPower;

namespace SlayTheRequiem.SlayTheRequiemCode.Cards.Platebearer;

// M4 п. 7 (card spec §3.2, 16 §3): карты кланов Латоносца (Горнокровные, Дюнные клинки, Цитадельцы), ветки
// Кровотечения и Тяжёлой брони, ветки Одноручного и Двуручного оружия. Числа и обоснование — docs/design/16.

/// <summary>Общие проверки снаряжения для карт веток.</summary>
internal static class PlatebearerGear
{
    public static WeaponKind MainHand(MegaCrit.Sts2.Core.Entities.Players.Player player) =>
        Loadout.InSlot(player, EquipSlot.MainHand)?.Weapon ?? WeaponKind.None;

    public static bool OneHanded(MegaCrit.Sts2.Core.Entities.Players.Player player) => HeroRecord.IsOneHanded(MainHand(player));

    public static bool TwoHanded(MegaCrit.Sts2.Core.Entities.Players.Player player) => MainHand(player) == WeaponKind.TwoHanded;

    public static bool Shield(MegaCrit.Sts2.Core.Entities.Players.Player player) => HeroRecord.Of(player) is { HasShield: true };

    /// <summary>Надетые предметы тяжёлой брони (голова, тело, руки, ноги — до 4).</summary>
    public static int HeavyPieces(MegaCrit.Sts2.Core.Entities.Players.Player player) =>
        Loadout.Equipped(player).Count(e => EquipmentRules.IsArmorSlot(e.Slot) && e.Armor == ArmorClass.Heavy);

    public static int AttacksThisTurn(MegaCrit.Sts2.Core.Entities.Players.Player player) => HeroRecord.Of(player)?.AttacksThisTurn ?? 0;
}

// ═════════════ Горнокровные: ярость горна (3+ атаки за ход), разряд ═════════════

/// <summary>Раздуть горн: если в этом ходу уже сыграно 2+ атаки (эта — третья), бьёт дважды.</summary>
[Pool(typeof(IroncladCardPool))]
public sealed class StokeTheForge : RequiemCard
{
    public const int AttacksNeeded = 2;

    public StokeTheForge() : base(1, CardType.Attack, CardRarity.Common, TargetType.AnyEnemy)
    {
        WithDamage(5, 2);
        WithStamina(1);
    }

    protected override async Task OnPlay(PlayerChoiceContext ctx, CardPlay play)
    {
        ArgumentNullException.ThrowIfNull(play.Target);
        var twice = PlatebearerGear.AttacksThisTurn(Owner) >= AttacksNeeded;
        await Hit(ctx, play.Target, "vfx/vfx_attack_blunt");
        if (twice && play.Target.IsAlive) await Hit(ctx, play.Target, "vfx/vfx_attack_blunt");
    }
}

/// <summary>Громовой удар: урон разрядом и выжечь ✦ врага.</summary>
[Pool(typeof(IroncladCardPool))]
public sealed class ThunderingBlow : RequiemCard
{
    public const string DrainVar = "ManaDrain";
    public override DamageType DamageType => DamageType.Shock;

    public ThunderingBlow() : base(1, CardType.Attack, CardRarity.Uncommon, TargetType.AnyEnemy)
    {
        WithDamage(8, 3);
        WithVar(DrainVar, 2, 1);
        WithStamina(2);
    }

    protected override async Task OnPlay(PlayerChoiceContext ctx, CardPlay play)
    {
        ArgumentNullException.ThrowIfNull(play.Target);
        await Hit(ctx, play.Target, "vfx/vfx_attack_blunt");
        if (play.Target.GetPower<EnemyManaPower>() is { } mana) await mana.Drain((int)Var(DrainVar));
    }
}

/// <summary>Сердце горна: каждая 3-я атака за ход тянет карту.</summary>
[Pool(typeof(IroncladCardPool))]
public sealed class ForgeHeart : RequiemCard
{
    public ForgeHeart() : base(2, CardType.Power, CardRarity.Rare, TargetType.Self)
    {
        WithPower<ForgeHeartPower>(1);
        WithCostUpgradeBy(-1);
    }

    protected override Task OnPlay(PlayerChoiceContext ctx, CardPlay play) =>
        Apply<ForgeHeartPower>(ctx, Owner.Creature, Var(nameof(ForgeHeartPower)));
}

// ═════════════ Дюнные клинки: ритм ◆ (игра от низкой ◆) ═════════════

/// <summary>Танец дюн: ◆ не тратит; если вы Устали — сильнее и возвращает 1 ◆.</summary>
[Pool(typeof(IroncladCardPool))]
public sealed class DuneDance : RequiemCard, IWearyPayoff
{
    public const string WearyBonusVar = "WearyBonus";
    public const int StaminaBack = 1;

    public DuneDance() : base(1, CardType.Attack, CardRarity.Common, TargetType.AnyEnemy)
    {
        WithDamage(6, 2);
        WithVar(WearyBonusVar, 4, 2);
        WithStamina(0);
    }

    protected override async Task OnPlay(PlayerChoiceContext ctx, CardPlay play)
    {
        ArgumentNullException.ThrowIfNull(play.Target);
        var weary = RequiemCombatRules.IsOwnerWeary(Owner, this);
        await Hit(ctx, play.Target, "vfx/vfx_flying_slash", DynamicVars.Damage.BaseValue + (weary ? Var(WearyBonusVar) : 0));
        if (weary) RequiemResource.Of<StaminaResource>(Owner)?.ModifyAmount(StaminaBack);
    }
}

/// <summary>Терпение пустыни: Блок; если вы Устали — ещё и ◆.</summary>
[Pool(typeof(IroncladCardPool))]
public sealed class DesertPatience : RequiemCard
{
    public DesertPatience() : base(1, CardType.Skill, CardRarity.Common, TargetType.Self)
    {
        WithBlock(6, 3);
        WithVar(CatchBreath.GainVar, 3, 1);
    }

    protected override async Task OnPlay(PlayerChoiceContext ctx, CardPlay play)
    {
        var weary = RequiemCombatRules.IsOwnerWeary(Owner, this);
        await Block(play);
        if (weary) RequiemResource.Of<StaminaResource>(Owner)?.ModifyAmount((int)Var(CatchBreath.GainVar));
    }
}

// ═════════════ Цитадельцы: щит и легион ═════════════

/// <summary>Сцепить щиты: Блок; со щитом — ещё и Латы.</summary>
[Pool(typeof(IroncladCardPool))]
public sealed class LockShields : RequiemCard
{
    public LockShields() : base(1, CardType.Skill, CardRarity.Common, TargetType.Self)
    {
        WithBlock(6, 3);
        WithPower<PlatingPower>(1);
    }

    protected override async Task OnPlay(PlayerChoiceContext ctx, CardPlay play)
    {
        await Block(play);
        if (PlatebearerGear.Shield(Owner)) await Apply<PlatingPower>(ctx, Owner.Creature, Var(nameof(PlatingPower)));
    }
}

/// <summary>Таран щитом: урон + текущий Блок героя.</summary>
[Pool(typeof(IroncladCardPool))]
public sealed class ShieldCharge : RequiemCard
{
    public ShieldCharge() : base(2, CardType.Attack, CardRarity.Uncommon, TargetType.AnyEnemy)
    {
        WithDamage(4, 3);
        WithStamina(2);
    }

    protected override async Task OnPlay(PlayerChoiceContext ctx, CardPlay play)
    {
        ArgumentNullException.ThrowIfNull(play.Target);
        await Hit(ctx, play.Target, "vfx/vfx_attack_blunt", DynamicVars.Damage.BaseValue + Owner.Creature.Block);
    }
}

/// <summary>Строй легиона: в начале хода со щитом +Блок.</summary>
[Pool(typeof(IroncladCardPool))]
public sealed class LegionDrill : RequiemCard
{
    public LegionDrill() : base(1, CardType.Power, CardRarity.Uncommon, TargetType.Self)
    {
        WithPower<LegionDrillPower>(3, 1);
    }

    protected override Task OnPlay(PlayerChoiceContext ctx, CardPlay play) =>
        Apply<LegionDrillPower>(ctx, Owner.Creature, Var(nameof(LegionDrillPower)));
}

// ═════════════ Кровотечение ═════════════

/// <summary>Рваная рана: два удара, каждый накладывает Кровотечение.</summary>
[Pool(typeof(IroncladCardPool))]
public sealed class RendingCut : RequiemCard
{
    public RendingCut() : base(1, CardType.Attack, CardRarity.Common, TargetType.AnyEnemy)
    {
        WithDamage(3, 1);
        WithPower<BleedPower>(1, 1);
        WithStamina(1);
    }

    protected override async Task OnPlay(PlayerChoiceContext ctx, CardPlay play)
    {
        ArgumentNullException.ThrowIfNull(play.Target);
        for (var i = 0; i < 2 && play.Target.IsAlive; i++)
        {
            await Hit(ctx, play.Target, "vfx/vfx_flying_slash");
            if (play.Target.IsAlive) await Apply<BleedPower>(ctx, play.Target, Var(nameof(BleedPower)));
        }
    }
}

/// <summary>Вскрыть жилы: удвоить Кровотечение цели.</summary>
[Pool(typeof(IroncladCardPool))]
public sealed class OpenVeins : RequiemCard
{
    public OpenVeins() : base(1, CardType.Skill, CardRarity.Uncommon, TargetType.AnyEnemy)
    {
        WithTip(typeof(BleedPower));
        WithCostUpgradeBy(-1);
    }

    protected override async Task OnPlay(PlayerChoiceContext ctx, CardPlay play)
    {
        ArgumentNullException.ThrowIfNull(play.Target);
        if (play.Target.GetPower<BleedPower>() is { Amount: > 0 } bleed)
            await Apply<BleedPower>(ctx, play.Target, bleed.Amount);
    }
}

/// <summary>Кровопускание: урон + по 2 за каждое Кровотечение цели.</summary>
[Pool(typeof(IroncladCardPool))]
public sealed class Hemorrhage : RequiemCard
{
    public const string PerBleedVar = "PerBleed";

    public Hemorrhage() : base(2, CardType.Attack, CardRarity.Rare, TargetType.AnyEnemy)
    {
        WithDamage(8, 3);
        WithVar(PerBleedVar, 2, 1);
        WithTip(typeof(BleedPower));
        WithStamina(2);
    }

    protected override async Task OnPlay(PlayerChoiceContext ctx, CardPlay play)
    {
        ArgumentNullException.ThrowIfNull(play.Target);
        var bleed = play.Target.GetPower<BleedPower>()?.Amount ?? 0;
        await Hit(ctx, play.Target, "vfx/vfx_flying_slash", DynamicVars.Damage.BaseValue + bleed * Var(PerBleedVar));
    }
}

// ═════════════ Тяжёлая броня ═════════════

/// <summary>Стальная выдержка: Блок + за каждую надетую вещь тяжёлой брони.</summary>
[Pool(typeof(IroncladCardPool))]
public sealed class SteelEndurance : RequiemCard
{
    public const string PerPieceVar = "PerHeavy";

    public SteelEndurance() : base(1, CardType.Skill, CardRarity.Common, TargetType.Self)
    {
        WithBlock(4, 2);
        WithVar(PerPieceVar, 2, 1);
    }

    protected override async Task OnPlay(PlayerChoiceContext ctx, CardPlay play)
    {
        await Block(play);
        var pieces = PlatebearerGear.HeavyPieces(Owner);
        if (pieces > 0) await Block(play, pieces * Var(PerPieceVar));
    }
}

/// <summary>Живая крепость: Латы = надетая тяжёлая броня + N.</summary>
[Pool(typeof(IroncladCardPool))]
public sealed class LivingFortress : RequiemCard
{
    public LivingFortress() : base(2, CardType.Power, CardRarity.Rare, TargetType.Self)
    {
        WithPower<PlatingPower>(1, 1);
    }

    protected override Task OnPlay(PlayerChoiceContext ctx, CardPlay play) =>
        Apply<PlatingPower>(ctx, Owner.Creature, Var(nameof(PlatingPower)) + PlatebearerGear.HeavyPieces(Owner));
}

// ═════════════ Одноручное оружие ═════════════

/// <summary>Выверенный удар: Оружейная атака; с одноручным оружием тянет карту.</summary>
[Pool(typeof(IroncladCardPool))]
public sealed class MeasuredStrike : RequiemCard, IWeaponCard
{
    public MeasuredStrike() : base(1, CardType.Attack, CardRarity.Common, TargetType.AnyEnemy)
    {
        WithDamage(7, 3);
        WithStamina(1);
    }

    protected override async Task OnPlay(PlayerChoiceContext ctx, CardPlay play)
    {
        ArgumentNullException.ThrowIfNull(play.Target);
        await Hit(ctx, play.Target);
        if (PlatebearerGear.OneHanded(Owner)) await CardPileCmd.Draw(ctx, 1, Owner);
    }
}

/// <summary>Меч и щит: Оружейная атака; с одноручным оружием и щитом — ещё и Блок.</summary>
[Pool(typeof(IroncladCardPool))]
public sealed class SwordAndBoard : RequiemCard, IWeaponCard
{
    public SwordAndBoard() : base(1, CardType.Attack, CardRarity.Uncommon, TargetType.AnyEnemy)
    {
        WithDamage(6, 2);
        WithBlock(5, 2);
        WithStamina(1);
    }

    protected override async Task OnPlay(PlayerChoiceContext ctx, CardPlay play)
    {
        ArgumentNullException.ThrowIfNull(play.Target);
        await Hit(ctx, play.Target);
        if (PlatebearerGear.OneHanded(Owner) && PlatebearerGear.Shield(Owner)) await Block(play);
    }
}

// ═════════════ Двуручное оружие ═════════════

/// <summary>Удар с плеча: тяжёлая Оружейная атака; с двуручным оружием +урон.</summary>
[Pool(typeof(IroncladCardPool))]
public sealed class OverheadSmash : RequiemCard, IWeaponCard
{
    public const string TwoHandedBonusVar = "TwoHandedBonus";

    public OverheadSmash() : base(2, CardType.Attack, CardRarity.Uncommon, TargetType.AnyEnemy)
    {
        WithDamage(12, 4);
        WithVar(TwoHandedBonusVar, 6, 2);
        WithStamina(3);
    }

    protected override async Task OnPlay(PlayerChoiceContext ctx, CardPlay play)
    {
        ArgumentNullException.ThrowIfNull(play.Target);
        var bonus = PlatebearerGear.TwoHanded(Owner) ? Var(TwoHandedBonusVar) : 0;
        await Hit(ctx, play.Target, "vfx/vfx_attack_blunt", DynamicVars.Damage.BaseValue + bonus);
    }
}

/// <summary>Горолом: Оружейная атака по всем; с двуручным оружием — дважды.</summary>
[Pool(typeof(IroncladCardPool))]
public sealed class MountainBreaker : RequiemCard, IWeaponCard
{
    public MountainBreaker() : base(2, CardType.Attack, CardRarity.Rare, TargetType.AllEnemies)
    {
        WithDamage(9, 3);
        WithStamina(4);
    }

    protected override async Task OnPlay(PlayerChoiceContext ctx, CardPlay play)
    {
        var twice = PlatebearerGear.TwoHanded(Owner);
        await HitAll(ctx, "vfx/vfx_attack_blunt");
        if (twice) await HitAll(ctx, "vfx/vfx_attack_blunt");
    }
}
