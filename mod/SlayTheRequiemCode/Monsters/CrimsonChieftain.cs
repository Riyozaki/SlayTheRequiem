using BaseLib.Abstracts;
using MegaCrit.Sts2.Core.Animation;
using MegaCrit.Sts2.Core.Bindings.MegaSpine;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Acts;
using MegaCrit.Sts2.Core.Models.Monsters;
using MegaCrit.Sts2.Core.MonsterMoves.Intents;
using MegaCrit.Sts2.Core.MonsterMoves.MonsterMoveStateMachine;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.ValueProps;
using SlayTheRequiem.SlayTheRequiemCode.Core;
using SlayTheRequiem.SlayTheRequiemCode.Powers;
using SlayTheRequiem.SlayTheRequiemCode.Resources;
using SlayTheRequiem.SlayTheRequiemCode.Rules;

namespace SlayTheRequiem.SlayTheRequiemCode.Monsters;

/// <summary>
/// Элита «Атаманша Багряных Ножей» (Диджа; docs/design/11 акт 1). Сабля с вампиризмом и кинжал (Истощение 3 ◆,
/// Раскол 1, Кровотечение 2). <b>Шайка</b>: два разбойника пассивны (<see cref="GangPower"/>), пока вы не ударите
/// их или атаманшу — тогда все встают. Если первым из шайки убит Подлым ударом — остальные паникуют
/// (<see cref="PanicPower"/>: следующая атака без урона). При ♥ &lt; 50 % — <b>Адреналин</b>: −50 % физического урона на 1 ход (раз за бой).
/// Облик — ванильные Рубиновые налётчики (своих спрайтов пока нет).
/// </summary>
public sealed class CrimsonChieftain : CustomMonsterModel
{
    public const int SaberDamage = 11;
    public const int SaberLifestealPercent = 50;
    public const int DaggerDamage = 6;
    public const int DaggerExhaustion = 3;
    public const int DaggerSunder = 1;
    public const int DaggerBleed = 2;
    public const int RallyBlock = 10;

    public override int MinInitialHp => 72;
    public override int MaxInitialHp => 72;

    public override string? CustomVisualPath => SceneHelper.GetScenePath("creature_visuals/assassin_ruby_raider");

    public override CreatureAnimator? SetupCustomAnimationStates(MegaSprite controller) =>
        ModelDb.Monster<AssassinRubyRaider>().GenerateAnimator(controller);

    private bool _adrenalineUsed;
    /// <summary>Первый павший из шайки уже был (паника — только если первым убит Подлым ударом).</summary>
    private bool _firstBanditDown;

    protected override MonsterMoveStateMachine GenerateMoveStateMachine()
    {
        var dagger = new MoveState("DAGGER", Dagger, new SingleAttackIntent(DaggerDamage), new DebuffIntent());
        var saber = new MoveState("SABER", Saber, new SingleAttackIntent(SaberDamage), new HealIntent());
        var rally = new MoveState("RALLY", Rally, new DefendIntent(), new BuffIntent());
        dagger.FollowUpState = saber;
        saber.FollowUpState = rally;
        rally.FollowUpState = dagger;
        return new MonsterMoveStateMachine([dagger, saber, rally], dagger);
    }

    private async Task Dagger(IReadOnlyList<Creature> targets)
    {
        var ctx = new ThrowingPlayerChoiceContext();
        await DamageCmd.Attack(DaggerDamage).FromMonster(this).WithAttackerAnim("Attack", 0.4f).WithHitFx("vfx/vfx_attack_slash").Execute(null);
        foreach (var target in targets.Where(t => t.IsAlive))
        {
            if (RequiemResource.Of<StaminaResource>(target.Player) is { } stamina)
                stamina.Amount = Math.Max(0, stamina.Amount - DaggerExhaustion);
            await PowerCmd.Apply<SunderPower>(ctx, target, DaggerSunder, Creature, null);
            await PowerCmd.Apply<BleedPower>(ctx, target, DaggerBleed, Creature, null);
        }
    }

    private async Task Saber(IReadOnlyList<Creature> targets)
    {
        var before = targets.Sum(t => t.CurrentHp);
        await DamageCmd.Attack(SaberDamage).FromMonster(this).WithAttackerAnim("Attack", 0.4f).WithHitFx("vfx/vfx_attack_slash").Execute(null);
        var dealt = Math.Max(0, before - targets.Sum(t => t.CurrentHp));
        var heal = dealt * SaberLifestealPercent / 100;
        if (heal > 0 && Creature.IsAlive) await CreatureCmd.Heal(Creature, heal);
    }

    /// <summary>«Ко мне, ножи!»: Блок и будит шайку.</summary>
    private async Task Rally(IReadOnlyList<Creature> targets)
    {
        await CreatureCmd.GainBlock(Creature, RallyBlock, ValueProp.Move, null);
        await GangPower.WakeAll(Creature.CombatState);
    }

    public override async Task AfterDamageReceived(PlayerChoiceContext choiceContext, Creature target, DamageResult result, ValueProp props, Creature? dealer, CardModel? cardSource)
    {
        if (!Creature.IsAlive) return;
        if (target != Creature)
        {
            // Паника: первым из шайки убит Подлым ударом — остальные теряют атаку (docs/design/11).
            if (!_firstBanditDown && target.Monster is CrimsonBandit && result.WasTargetKilled)
            {
                _firstBanditDown = true;
                if (dealer is { IsPlayer: true } && SneakRules.IsSneak(dealer, target))
                    foreach (var ally in Creature.CombatState?.Enemies.Where(e => e.IsAlive && e != target).ToList() ?? [])
                        await PowerCmd.Apply<PanicPower>(choiceContext, ally, 1, Creature, null);
            }
            return;
        }
        if (dealer is { IsPlayer: true }) await GangPower.WakeAll(Creature.CombatState);
        if (!_adrenalineUsed && Creature.CurrentHp * 2 < Creature.MaxHp)
        {
            _adrenalineUsed = true;
            await PowerCmd.Apply<AdrenalinePower>(choiceContext, Creature, 2, Creature, null);
        }
    }
}

/// <summary>Разбойник шайки Багряных Ножей: пассивен под «Шайкой», потом — засада и удары ножом.</summary>
public sealed class CrimsonBandit : CustomMonsterModel
{
    public const int AmbushDamage = 8;
    public const int StabDamage = 5;
    public const int StabBleed = 1;

    public override int MinInitialHp => 22;
    public override int MaxInitialHp => 25;

    public override string? CustomVisualPath => SceneHelper.GetScenePath("creature_visuals/axe_ruby_raider");

    public override CreatureAnimator? SetupCustomAnimationStates(MegaSprite controller) =>
        ModelDb.Monster<AxeRubyRaider>().GenerateAnimator(controller);

    public override async Task AfterAddedToRoom()
    {
        await base.AfterAddedToRoom();
        await PowerCmd.Apply<GangPower>(new ThrowingPlayerChoiceContext(), Creature, 1, Creature, null);
    }

    protected override MonsterMoveStateMachine GenerateMoveStateMachine()
    {
        var lurk = new MoveState("LURK", _ => Task.CompletedTask, new SleepIntent());
        var ambush = new MoveState("AMBUSH", Ambush, new SingleAttackIntent(AmbushDamage));
        var stab = new MoveState("STAB", Stab, new SingleAttackIntent(StabDamage), new DebuffIntent());
        var wake = new ConditionalBranchState("LURK_NEXT");
        wake.AddState(lurk, () => Creature.HasPower<GangPower>());
        wake.AddState(ambush, () => !Creature.HasPower<GangPower>());
        lurk.FollowUpState = wake;
        ambush.FollowUpState = stab;
        stab.FollowUpState = ambush;
        return new MonsterMoveStateMachine([lurk, wake, ambush, stab], lurk);
    }

    private Task Ambush(IReadOnlyList<Creature> targets) =>
        DamageCmd.Attack(AmbushDamage).FromMonster(this).WithAttackerAnim("Attack", 0.4f).WithHitFx("vfx/vfx_attack_slash").Execute(null);

    private async Task Stab(IReadOnlyList<Creature> targets)
    {
        await DamageCmd.Attack(StabDamage).FromMonster(this).WithAttackerAnim("Attack", 0.4f).WithHitFx("vfx/vfx_attack_slash").Execute(null);
        foreach (var target in targets.Where(t => t.IsAlive))
            await PowerCmd.Apply<BleedPower>(new ThrowingPlayerChoiceContext(), target, StabBleed, Creature, null);
    }
}

/// <summary>Энкаунтер элиты: акт 1 (Заросли или Подземные доки).</summary>
public sealed class CrimsonChieftainElite() : CustomEncounterModel(RoomType.Elite)
{
    public override bool IsValidForAct(ActModel act) => act is Overgrowth or Underdocks;

    public override IEnumerable<MonsterModel> AllPossibleMonsters =>
        [ModelDb.Monster<CrimsonBandit>(), ModelDb.Monster<CrimsonChieftain>()];

    protected override IReadOnlyList<(MonsterModel, string?)> GenerateMonsters() =>
    [
        (ModelDb.Monster<CrimsonBandit>().ToMutable(), null),
        (ModelDb.Monster<CrimsonChieftain>().ToMutable(), null),
        (ModelDb.Monster<CrimsonBandit>().ToMutable(), null),
    ];
}
