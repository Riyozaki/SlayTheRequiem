using BaseLib.Abstracts;
using BaseLib.Utils;
using Godot;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.CardPools;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.ValueProps;
using SlayTheRequiem.SlayTheRequiemCode.Cards;
using SlayTheRequiem.SlayTheRequiemCode.Core;
using SlayTheRequiem.SlayTheRequiemCode.Powers;
using SlayTheRequiem.SlayTheRequiemCode.Progression;
using SlayTheRequiem.SlayTheRequiemCode.Rules;
using SlayTheRequiem.SlayTheRequiemCode.Spells;
using VanillaPoison = MegaCrit.Sts2.Core.Models.Powers.PoisonPower;
using VanillaStrength = MegaCrit.Sts2.Core.Models.Powers.StrengthPower;
using VanillaVulnerable = MegaCrit.Sts2.Core.Models.Powers.VulnerablePower;
using VanillaWeak = MegaCrit.Sts2.Core.Models.Powers.WeakPower;

namespace SlayTheRequiem.SlayTheRequiemCode.Transformations;

/// <summary>Пул карт Путей Героя: в награды — только карты Пути героя (<see cref="PathRewardRules"/>).</summary>
public sealed class PathCardPool : CustomCardPoolModel
{
    public override string Title => "requiem_paths";
    public override bool IsShared => true;
    public override bool IsColorless => true;
    public override Color ShaderColor => new("E0B050");
    public override Color DeckEntryCardColor => new("E0B050");
}

/// <summary>Карта Пути.</summary>
public interface IPathCard
{
    HeroPath Path { get; }
}

public abstract class PathCard(int cost, CardType type, CardRarity rarity, TargetType target)
    : RequiemCard(cost, type, rarity, target), IPathCard
{
    public abstract HeroPath Path { get; }
}

/// <summary>
/// Карты Пути в наградах за бой (06 §2): у героя с Путём каждая карта награды с шансом 25 % заменяется картой его Пути.
/// </summary>
public sealed class PathRewardRules() : CustomSingletonModel(CustomSingletonModel.HookType.Run)
{
    /// <summary>Подписные заклинания Повелителя стихии (карты его Пути).</summary>
    public static readonly Type[] ElementLordSpells = [typeof(Meteor), typeof(IceTomb), typeof(ChainLightning)];

    public override bool TryModifyCardRewardOptions(Player player, List<CardCreationResult> cardRewardOptions, CardCreationOptions creationOptions)
    {
        if (!RequiemMode.IsInSystem(player) || creationOptions.Source != CardCreationSource.Encounter) return false;
        var path = Paths.PathOf(player);
        if (path == HeroPath.None) return false;
        var eligible = ModelDb.AllCardPools.OfType<PathCardPool>().SelectMany(p => p.AllCards)
            .Where(c => c is IPathCard pc && pc.Path == path)
            .ToList();
        if (path == HeroPath.ElementLord)
            eligible.AddRange(ElementLordSpells.Select(t => ModelDb.GetById<CardModel>(ModelDb.GetId(t))));
        if (eligible.Count == 0) return false;
        var rng = player.PlayerRng.Rewards;
        var changed = false;
        foreach (var option in cardRewardOptions)
        {
            if (rng.NextInt(100) >= TransformationRules.PathCardRewardPercent) continue;
            var taken = cardRewardOptions.Select(o => o.Card.CanonicalInstance).ToHashSet();
            var pick = rng.NextItem(eligible.Where(e => !taken.Contains(e)));
            if (pick == null) break;
            option.ModifyCard(player.RunState.CreateCard(pick, player));
            changed = true;
        }
        return changed;
    }
}

// ═════════════ Паладин ═════════════

/// <summary>Святой удар: 1⚡ 2◆ — 9 урона светом.</summary>
[Pool(typeof(PathCardPool))]
public sealed class HolyStrike : PathCard
{
    public override HeroPath Path => HeroPath.Paladin;
    public override DamageType DamageType => DamageType.Light;

    public HolyStrike() : base(1, CardType.Attack, CardRarity.Uncommon, TargetType.AnyEnemy)
    {
        WithDamage(9, 3);
        WithStamina(2);
    }

    protected override Task OnPlay(PlayerChoiceContext ctx, CardPlay play)
    {
        ArgumentNullException.ThrowIfNull(play.Target);
        return Hit(ctx, play.Target, "vfx/vfx_attack_light");
    }
}

/// <summary>Эгида веры: 1⚡ — 8 Блока и Оберег 4.</summary>
[Pool(typeof(PathCardPool))]
public sealed class AegisOfFaith : PathCard
{
    public override HeroPath Path => HeroPath.Paladin;

    public AegisOfFaith() : base(1, CardType.Skill, CardRarity.Uncommon, TargetType.Self)
    {
        WithBlock(8, 3);
        WithPower<SpellShieldPower>(4, 2);
    }

    protected override async Task OnPlay(PlayerChoiceContext ctx, CardPlay play)
    {
        await Block(play);
        await Apply<SpellShieldPower>(ctx, Owner.Creature, Var(nameof(SpellShieldPower)));
    }
}

/// <summary>Освящение: 2⚡ 1✦ — 6 урона светом всем; нежить и Бездна — Ошеломление 4.</summary>
[Pool(typeof(PathCardPool))]
public sealed class Consecrate : PathCard
{
    public override HeroPath Path => HeroPath.Paladin;
    public override DamageType DamageType => DamageType.Light;
    public override int CanonicalStarCost => 1;

    public Consecrate() : base(2, CardType.Attack, CardRarity.Rare, TargetType.AllEnemies)
    {
        WithDamage(6, 3);
        WithPower<StaggerPower>(4);
    }

    protected override async Task OnPlay(PlayerChoiceContext ctx, CardPlay play)
    {
        await HitAll(ctx, "vfx/vfx_attack_light");
        foreach (var enemy in CombatState.HittableEnemies.Where(e => e.IsAlive && EnemySetup.IsUndeadOrAbyss(e)).ToList())
            await Apply<StaggerPower>(ctx, enemy, Var(nameof(StaggerPower)));
    }
}

// ═════════════ Чёрный рыцарь ═════════════

/// <summary>Удар ужаса: 1⚡ 2◆ — 9 урона и Слабость 1.</summary>
[Pool(typeof(PathCardPool))]
public sealed class DreadStrike : PathCard
{
    public override HeroPath Path => HeroPath.BlackKnight;

    public DreadStrike() : base(1, CardType.Attack, CardRarity.Uncommon, TargetType.AnyEnemy)
    {
        WithDamage(9, 3);
        WithPower<VanillaWeak>(1, 1);
        WithStamina(2);
    }

    protected override async Task OnPlay(PlayerChoiceContext ctx, CardPlay play)
    {
        ArgumentNullException.ThrowIfNull(play.Target);
        await Hit(ctx, play.Target);
        if (play.Target.IsAlive) await Apply<VanillaWeak>(ctx, play.Target, Var("WeakPower"));
    }
}

/// <summary>Высасывание: 1⚡ 1◆ — 6 урона; вылечить нанесённое (кормление).</summary>
[Pool(typeof(PathCardPool))]
public sealed class SoulDrain : PathCard
{
    public override HeroPath Path => HeroPath.BlackKnight;

    public SoulDrain() : base(1, CardType.Attack, CardRarity.Uncommon, TargetType.AnyEnemy)
    {
        WithDamage(6, 3);
        WithStamina(1);
    }

    protected override async Task OnPlay(PlayerChoiceContext ctx, CardPlay play)
    {
        ArgumentNullException.ThrowIfNull(play.Target);
        var before = play.Target.CurrentHp;
        await Hit(ctx, play.Target);
        var dealt = Math.Max(0, before - play.Target.CurrentHp);
        if (dealt > 0 && Owner.Creature.IsAlive)
            using (Healing.As(HealSource.Feeding))
                await CreatureCmd.Heal(Owner.Creature, dealt);
    }
}

/// <summary>Тёмная аура: 1⚡, Изгнание — все враги −1 Сила (улучшение — −2).</summary>
[Pool(typeof(PathCardPool))]
public sealed class DarkAura : PathCard
{
    public override HeroPath Path => HeroPath.BlackKnight;

    public DarkAura() : base(1, CardType.Skill, CardRarity.Rare, TargetType.AllEnemies)
    {
        WithVar("Strength", 1, 1);
        WithKeyword(CardKeyword.Exhaust);
    }

    protected override Task OnPlay(PlayerChoiceContext ctx, CardPlay play) =>
        ApplyAll<VanillaStrength>(ctx, -Var("Strength"));
}

// ═════════════ Чемпион ═════════════

/// <summary>Сокрушитель щитов: 2⚡ 3◆ — 16 урона и Раскол 2.</summary>
[Pool(typeof(PathCardPool))]
public sealed class Shieldbreaker : PathCard
{
    public override HeroPath Path => HeroPath.Champion;

    public Shieldbreaker() : base(2, CardType.Attack, CardRarity.Rare, TargetType.AnyEnemy)
    {
        WithDamage(16, 5);
        WithPower<SunderPower>(2);
        WithStamina(3);
    }

    protected override async Task OnPlay(PlayerChoiceContext ctx, CardPlay play)
    {
        ArgumentNullException.ThrowIfNull(play.Target);
        await Hit(ctx, play.Target, "vfx/vfx_attack_blunt");
        if (play.Target.IsAlive) await Apply<SunderPower>(ctx, play.Target, Var(nameof(SunderPower)));
    }
}

/// <summary>Боевой клич: 1⚡ — 10 Блока.</summary>
[Pool(typeof(PathCardPool))]
public sealed class RallyCry : PathCard
{
    public override HeroPath Path => HeroPath.Champion;

    public RallyCry() : base(1, CardType.Skill, CardRarity.Uncommon, TargetType.Self)
    {
        WithBlock(10, 4);
    }

    protected override Task OnPlay(PlayerChoiceContext ctx, CardPlay play) => Block(play);
}

/// <summary>Натиск: 1⚡ 2◆ — 8 урона и Ошеломление 4.</summary>
[Pool(typeof(PathCardPool))]
public sealed class Overpower : PathCard
{
    public override HeroPath Path => HeroPath.Champion;

    public Overpower() : base(1, CardType.Attack, CardRarity.Uncommon, TargetType.AnyEnemy)
    {
        WithDamage(8, 3);
        WithPower<StaggerPower>(4, 2);
        WithStamina(2);
    }

    protected override async Task OnPlay(PlayerChoiceContext ctx, CardPlay play)
    {
        ArgumentNullException.ThrowIfNull(play.Target);
        await Hit(ctx, play.Target, "vfx/vfx_attack_blunt");
        if (play.Target.IsAlive) await Apply<StaggerPower>(ctx, play.Target, Var(nameof(StaggerPower)));
    }
}

// ═════════════ Варвар ═════════════

/// <summary>Буйство: 1⚡ 2◆ — 2 раза по 5 урона.</summary>
[Pool(typeof(PathCardPool))]
public sealed class Rampage : PathCard
{
    public override HeroPath Path => HeroPath.Barbarian;

    public Rampage() : base(1, CardType.Attack, CardRarity.Uncommon, TargetType.AnyEnemy)
    {
        WithDamage(5, 2);
        WithStamina(2);
    }

    protected override async Task OnPlay(PlayerChoiceContext ctx, CardPlay play)
    {
        ArgumentNullException.ThrowIfNull(play.Target);
        await Hit(ctx, play.Target);
        if (play.Target.IsAlive) await Hit(ctx, play.Target);
    }
}

/// <summary>Кровавое неистовство: 0⚡, Изгнание — потерять 4 ♥, Сила +2.</summary>
[Pool(typeof(PathCardPool))]
public sealed class BloodFury : PathCard
{
    public override HeroPath Path => HeroPath.Barbarian;

    public BloodFury() : base(0, CardType.Skill, CardRarity.Rare, TargetType.Self)
    {
        WithVar("HpLoss", 4);
        WithPower<VanillaStrength>(2, 1);
        WithKeyword(CardKeyword.Exhaust);
    }

    protected override async Task OnPlay(PlayerChoiceContext ctx, CardPlay play)
    {
        await CreatureCmd.Damage(ctx, Owner.Creature, Var("HpLoss"), ValueProp.Unblockable | ValueProp.Unpowered, Owner.Creature, null);
        await Apply<VanillaStrength>(ctx, Owner.Creature, Var("StrengthPower"));
    }
}

/// <summary>Размах: 1⚡ 2◆ — 8 урона всем врагам.</summary>
[Pool(typeof(PathCardPool))]
public sealed class Whirlwind : PathCard
{
    public override HeroPath Path => HeroPath.Barbarian;

    public Whirlwind() : base(1, CardType.Attack, CardRarity.Uncommon, TargetType.AllEnemies)
    {
        WithDamage(8, 3);
        WithStamina(2);
    }

    protected override Task OnPlay(PlayerChoiceContext ctx, CardPlay play) => HitAll(ctx);
}

// ═════════════ Монах ═════════════

/// <summary>Удар ладонью: 0⚡ 1◆ — 5 урона и Ошеломление 2.</summary>
[Pool(typeof(PathCardPool))]
public sealed class PalmStrike : PathCard
{
    public override HeroPath Path => HeroPath.Monk;

    public PalmStrike() : base(0, CardType.Attack, CardRarity.Uncommon, TargetType.AnyEnemy)
    {
        WithDamage(5, 2);
        WithPower<StaggerPower>(2, 1);
        WithStamina(1);
    }

    protected override async Task OnPlay(PlayerChoiceContext ctx, CardPlay play)
    {
        ArgumentNullException.ThrowIfNull(play.Target);
        await Hit(ctx, play.Target, "vfx/vfx_attack_blunt");
        if (play.Target.IsAlive) await Apply<StaggerPower>(ctx, play.Target, Var(nameof(StaggerPower)));
    }
}

/// <summary>Град ударов: 1⚡ 2◆ — 4 раза по 3 урона.</summary>
[Pool(typeof(PathCardPool))]
public sealed class FlurryOfBlows : PathCard
{
    public override HeroPath Path => HeroPath.Monk;

    public FlurryOfBlows() : base(1, CardType.Attack, CardRarity.Rare, TargetType.AnyEnemy)
    {
        WithDamage(3, 1);
        WithStamina(2);
    }

    protected override async Task OnPlay(PlayerChoiceContext ctx, CardPlay play)
    {
        ArgumentNullException.ThrowIfNull(play.Target);
        for (var i = 0; i < 4 && play.Target.IsAlive; i++) await Hit(ctx, play.Target, "vfx/vfx_attack_blunt");
    }
}

/// <summary>Внутренний покой: 1⚡ — 7 Блока и Уклонение 1.</summary>
[Pool(typeof(PathCardPool))]
public sealed class InnerCalm : PathCard
{
    public override HeroPath Path => HeroPath.Monk;

    public InnerCalm() : base(1, CardType.Skill, CardRarity.Uncommon, TargetType.Self)
    {
        WithBlock(7, 3);
        WithPower<DodgePower>(1);
    }

    protected override async Task OnPlay(PlayerChoiceContext ctx, CardPlay play)
    {
        await Block(play);
        await Apply<DodgePower>(ctx, Owner.Creature, Var(nameof(DodgePower)));
    }
}

// ═════════════ Ассасин ═════════════

/// <summary>
/// Удавка ассасина: 1⚡ 1◆ — 7 урона и Кровотечение 3. Класс назван PathGarrote, а не Garrote: id модели строится
/// из имени класса, и одноимённая карта Безмолвной (Cards/Silent) молча затирала бы эту в ModelDb (ревью R-07).
/// </summary>
[Pool(typeof(PathCardPool))]
public sealed class PathGarrote : PathCard
{
    public override HeroPath Path => HeroPath.Assassin;

    public PathGarrote() : base(1, CardType.Attack, CardRarity.Uncommon, TargetType.AnyEnemy)
    {
        WithDamage(7, 2);
        WithPower<BleedPower>(3, 1);
        WithStamina(1);
    }

    protected override async Task OnPlay(PlayerChoiceContext ctx, CardPlay play)
    {
        ArgumentNullException.ThrowIfNull(play.Target);
        await Hit(ctx, play.Target);
        if (play.Target.IsAlive && !EnemySetup.IsBleedImmune(play.Target))
            await Apply<BleedPower>(ctx, play.Target, Var(nameof(BleedPower)));
    }
}

/// <summary>Исчезновение: 1⚡, Изгнание — Уклонение 2.</summary>
[Pool(typeof(PathCardPool))]
public sealed class Vanish : PathCard
{
    public override HeroPath Path => HeroPath.Assassin;

    public Vanish() : base(1, CardType.Skill, CardRarity.Uncommon, TargetType.Self)
    {
        WithPower<DodgePower>(2, 1);
        WithKeyword(CardKeyword.Exhaust);
    }

    protected override Task OnPlay(PlayerChoiceContext ctx, CardPlay play) =>
        Apply<DodgePower>(ctx, Owner.Creature, Var(nameof(DodgePower)));
}

/// <summary>Метка смерти: 1⚡ — Уязвимость 2 и Сломлен.</summary>
[Pool(typeof(PathCardPool))]
public sealed class DeathMark : PathCard
{
    public override HeroPath Path => HeroPath.Assassin;

    public DeathMark() : base(1, CardType.Skill, CardRarity.Rare, TargetType.AnyEnemy)
    {
        WithPower<VanillaVulnerable>(2, 1);
    }

    protected override async Task OnPlay(PlayerChoiceContext ctx, CardPlay play)
    {
        ArgumentNullException.ThrowIfNull(play.Target);
        await Apply<VanillaVulnerable>(ctx, play.Target, Var("VulnerablePower"));
        await Apply<BrokenPower>(ctx, play.Target, 1);
    }
}

// ═════════════ Губитель ═════════════

/// <summary>Касание чумы: 1⚡ 1◆ — 5 урона, Яд 4 и Хворь 1.</summary>
[Pool(typeof(PathCardPool))]
public sealed class PlagueTouch : PathCard
{
    public override HeroPath Path => HeroPath.Blightbringer;

    public PlagueTouch() : base(1, CardType.Attack, CardRarity.Uncommon, TargetType.AnyEnemy)
    {
        WithDamage(5, 2);
        WithPower<VanillaPoison>(4, 2);
        WithStamina(1);
    }

    protected override async Task OnPlay(PlayerChoiceContext ctx, CardPlay play)
    {
        ArgumentNullException.ThrowIfNull(play.Target);
        await Hit(ctx, play.Target);
        if (!play.Target.IsAlive) return;
        await Apply<VanillaPoison>(ctx, play.Target, Var("PoisonPower"));
        if (!EnemySetup.IsBlightImmune(play.Target)) await Apply<BlightPower>(ctx, play.Target, 1);
    }
}

/// <summary>Миазмы: 2⚡ — Яд 5 всем врагам.</summary>
[Pool(typeof(PathCardPool))]
public sealed class Miasma : PathCard
{
    public override HeroPath Path => HeroPath.Blightbringer;

    public Miasma() : base(2, CardType.Skill, CardRarity.Uncommon, TargetType.AllEnemies)
    {
        WithPower<VanillaPoison>(5, 2);
    }

    protected override Task OnPlay(PlayerChoiceContext ctx, CardPlay play) =>
        ApplyAll<VanillaPoison>(ctx, Var("PoisonPower"));
}

/// <summary>Иссушение: 1⚡, Изгнание — удвоить Яд на цели.</summary>
[Pool(typeof(PathCardPool))]
public sealed class Wither : PathCard
{
    public override HeroPath Path => HeroPath.Blightbringer;

    public Wither() : base(1, CardType.Skill, CardRarity.Rare, TargetType.AnyEnemy)
    {
        WithKeyword(CardKeyword.Exhaust);
        WithCostUpgradeBy(-1);
    }

    protected override async Task OnPlay(PlayerChoiceContext ctx, CardPlay play)
    {
        ArgumentNullException.ThrowIfNull(play.Target);
        if (play.Target.GetPower<VanillaPoison>() is { Amount: > 0 } poison)
            await Apply<VanillaPoison>(ctx, play.Target, poison.Amount);
    }
}

// ═════════════ Мистический лучник ═════════════

/// <summary>Чародейский выстрел: 1⚡ 1◆ — 8 урона магией.</summary>
[Pool(typeof(PathCardPool))]
public sealed class ArcaneShot : PathCard
{
    public override HeroPath Path => HeroPath.MysticArcher;
    public override DamageType DamageType => DamageType.Magic;

    public ArcaneShot() : base(1, CardType.Attack, CardRarity.Uncommon, TargetType.AnyEnemy)
    {
        WithDamage(8, 3);
        WithStamina(1);
    }

    protected override Task OnPlay(PlayerChoiceContext ctx, CardPlay play)
    {
        ArgumentNullException.ThrowIfNull(play.Target);
        return Hit(ctx, play.Target, "vfx/vfx_flying_slash");
    }
}

/// <summary>Стихийный залп: 2⚡ 2◆ — 6 урона огнём всем и Горение 2.</summary>
[Pool(typeof(PathCardPool))]
public sealed class ElementalVolley : PathCard
{
    public override HeroPath Path => HeroPath.MysticArcher;
    public override DamageType DamageType => DamageType.Fire;

    public ElementalVolley() : base(2, CardType.Attack, CardRarity.Rare, TargetType.AllEnemies)
    {
        WithDamage(6, 3);
        WithPower<BurningPower>(2, 1);
        WithStamina(2);
    }

    protected override async Task OnPlay(PlayerChoiceContext ctx, CardPlay play)
    {
        await HitAll(ctx, "vfx/vfx_attack_fire");
        await ApplyAll<BurningPower>(ctx, Var(nameof(BurningPower)));
    }
}

/// <summary>Прицел: 0⚡, Изгнание — +2 ✦.</summary>
[Pool(typeof(PathCardPool))]
public sealed class FocusedAim : PathCard
{
    public override HeroPath Path => HeroPath.MysticArcher;

    public FocusedAim() : base(0, CardType.Skill, CardRarity.Uncommon, TargetType.Self)
    {
        WithVar("Mana", 2, 1);
        WithKeyword(CardKeyword.Exhaust);
    }

    protected override Task OnPlay(PlayerChoiceContext ctx, CardPlay play) => Mana.Gain(Owner, (int)Var("Mana"));
}

// ═════════════ Повелитель стихии ═════════════
// Его три карты — подписные заклинания этапа 2 (Spells/DestructionSpells.cs: Meteor, IceTomb, ChainLightning):
// одно выдаёт Сущность по выбору стихии, все три попадают в награды как карты Пути (PathRewardRules).

// ═════════════ Боевой маг ═════════════

/// <summary>Клинок-заклинание: 1⚡ 1◆ 1✦ — 7 урона и ещё 5 огнём.</summary>
[Pool(typeof(PathCardPool))]
public sealed class SpellBlade : PathCard
{
    public override HeroPath Path => HeroPath.Battlemage;
    public override int CanonicalStarCost => 1;

    public SpellBlade() : base(1, CardType.Attack, CardRarity.Uncommon, TargetType.AnyEnemy)
    {
        WithDamage(7, 2);
        WithVar("Fire", 5, 2);
        WithStamina(1);
    }

    protected override async Task OnPlay(PlayerChoiceContext ctx, CardPlay play)
    {
        ArgumentNullException.ThrowIfNull(play.Target);
        await Hit(ctx, play.Target);
        if (!play.Target.IsAlive) return;
        using (DamageTyping.As(DamageType.Fire))
            await CreatureCmd.Damage(ctx, play.Target, Var("Fire"), ValueProp.Unpowered, Owner.Creature, this);
    }
}

/// <summary>Рунная броня: 1⚡ 1✦ — 8 Блока и Плащ холода 2.</summary>
[Pool(typeof(PathCardPool))]
public sealed class RuneArmor : PathCard
{
    public override HeroPath Path => HeroPath.Battlemage;
    public override int CanonicalStarCost => 1;

    public RuneArmor() : base(1, CardType.Skill, CardRarity.Uncommon, TargetType.Self)
    {
        WithBlock(8, 3);
        WithPower<FrostCloakPower>(2, 1);
    }

    protected override async Task OnPlay(PlayerChoiceContext ctx, CardPlay play)
    {
        await Block(play);
        await Apply<FrostCloakPower>(ctx, Owner.Creature, Var(nameof(FrostCloakPower)));
    }
}

/// <summary>Чародейский рывок: 2⚡ 2◆ — 13 урона разрядом.</summary>
[Pool(typeof(PathCardPool))]
public sealed class ArcaneCharge : PathCard
{
    public override HeroPath Path => HeroPath.Battlemage;
    public override DamageType DamageType => DamageType.Shock;

    public ArcaneCharge() : base(2, CardType.Attack, CardRarity.Rare, TargetType.AnyEnemy)
    {
        WithDamage(13, 4);
        WithStamina(2);
    }

    protected override Task OnPlay(PlayerChoiceContext ctx, CardPlay play)
    {
        ArgumentNullException.ThrowIfNull(play.Target);
        return Hit(ctx, play.Target, "vfx/vfx_attack_lightning");
    }
}

// ═════════════ Отражатель ═════════════

/// <summary>Зеркальный оберег: 1⚡ 1✦ — Оберег 12.</summary>
[Pool(typeof(PathCardPool))]
public sealed class MirrorWard : PathCard
{
    public override HeroPath Path => HeroPath.Reflector;
    public override int CanonicalStarCost => 1;

    public MirrorWard() : base(1, CardType.Skill, CardRarity.Uncommon, TargetType.Self)
    {
        WithPower<SpellShieldPower>(12, 4);
    }

    protected override Task OnPlay(PlayerChoiceContext ctx, CardPlay play) =>
        Apply<SpellShieldPower>(ctx, Owner.Creature, Var(nameof(SpellShieldPower)));
}

/// <summary>Отдача: 1⚡ — 6 Блока и Поглощение заклинаний 1.</summary>
[Pool(typeof(PathCardPool))]
public sealed class Rebound : PathCard
{
    public override HeroPath Path => HeroPath.Reflector;

    public Rebound() : base(1, CardType.Skill, CardRarity.Uncommon, TargetType.Self)
    {
        WithBlock(6, 3);
        WithPower<SpellAbsorptionPower>(1);
    }

    protected override async Task OnPlay(PlayerChoiceContext ctx, CardPlay play)
    {
        await Block(play);
        await Apply<SpellAbsorptionPower>(ctx, Owner.Creature, Var(nameof(SpellAbsorptionPower)));
    }
}

/// <summary>Призменная стрела: 1⚡ 2✦ — 6 урона магией + ваш Оберег.</summary>
[Pool(typeof(PathCardPool))]
public sealed class PrismBolt : PathCard
{
    public override HeroPath Path => HeroPath.Reflector;
    public override DamageType DamageType => DamageType.Magic;
    public override int CanonicalStarCost => 2;

    public PrismBolt() : base(1, CardType.Attack, CardRarity.Rare, TargetType.AnyEnemy)
    {
        WithDamage(6, 3);
    }

    protected override Task OnPlay(PlayerChoiceContext ctx, CardPlay play)
    {
        ArgumentNullException.ThrowIfNull(play.Target);
        var ward = Owner.Creature.GetPower<SpellShieldPower>()?.Amount ?? 0;
        return Hit(ctx, play.Target, "vfx/vfx_attack_light", DynamicVars.Damage.BaseValue + ward);
    }
}

// ═════════════ Медиум ═════════════

/// <summary>Зов духов: 1⚡ 2✦ — призвать волка-духа (10 ♥).</summary>
[Pool(typeof(PathCardPool))]
public sealed class SpiritCall : PathCard
{
    public override HeroPath Path => HeroPath.Medium;
    public override int CanonicalStarCost => 2;

    public SpiritCall() : base(1, CardType.Skill, CardRarity.Uncommon, TargetType.Self)
    {
        WithVar("Hp", 10, 4);
    }

    protected override Task OnPlay(PlayerChoiceContext ctx, CardPlay play) =>
        SummonPower.Summon<SpiritWolfPower>(ctx, Owner.Creature, (int)Var("Hp"), this);
}

/// <summary>Сеанс: 1⚡ — 6 Блока и +2 ✦.</summary>
[Pool(typeof(PathCardPool))]
public sealed class Seance : PathCard
{
    public override HeroPath Path => HeroPath.Medium;

    public Seance() : base(1, CardType.Skill, CardRarity.Uncommon, TargetType.Self)
    {
        WithBlock(6, 3);
        WithVar("Mana", 2);
    }

    protected override async Task OnPlay(PlayerChoiceContext ctx, CardPlay play)
    {
        await Block(play);
        await Mana.Gain(Owner, (int)Var("Mana"));
    }
}

/// <summary>Эфирная связь: 1⚡ — все ваши призывы +6 ♥.</summary>
[Pool(typeof(PathCardPool))]
public sealed class EtherealLink : PathCard
{
    public override HeroPath Path => HeroPath.Medium;

    public EtherealLink() : base(1, CardType.Skill, CardRarity.Rare, TargetType.Self)
    {
        WithVar("Hp", 6, 3);
    }

    protected override async Task OnPlay(PlayerChoiceContext ctx, CardPlay play)
    {
        foreach (var summon in Owner.Creature.Powers.OfType<SummonPower>().ToList())
            await PowerCmd.ModifyAmount(ctx, summon, Var("Hp"), Owner.Creature, this);
    }
}

// ═════════════ выбор Пути и стихии (карты-варианты) ═════════════

[Pool(typeof(TokenCardPool))] public sealed class PickPathPaladin : PickCard { }
[Pool(typeof(TokenCardPool))] public sealed class PickPathBlackKnight : PickCard { }
[Pool(typeof(TokenCardPool))] public sealed class PickPathChampion : PickCard { }
[Pool(typeof(TokenCardPool))] public sealed class PickPathBarbarian : PickCard { }
[Pool(typeof(TokenCardPool))] public sealed class PickPathMonk : PickCard { }
[Pool(typeof(TokenCardPool))] public sealed class PickPathAssassin : PickCard { }
[Pool(typeof(TokenCardPool))] public sealed class PickPathBlightbringer : PickCard { }
[Pool(typeof(TokenCardPool))] public sealed class PickPathMysticArcher : PickCard { }
[Pool(typeof(TokenCardPool))] public sealed class PickPathElementLord : PickCard { }
[Pool(typeof(TokenCardPool))] public sealed class PickPathBattlemage : PickCard { }
[Pool(typeof(TokenCardPool))] public sealed class PickPathReflector : PickCard { }
[Pool(typeof(TokenCardPool))] public sealed class PickPathMedium : PickCard { }
[Pool(typeof(TokenCardPool))] public sealed class PickPathReborn : PickCard { }
