using BaseLib.Abstracts;
using MegaCrit.Sts2.Core.Animation;
using MegaCrit.Sts2.Core.Bindings.MegaSpine;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Acts;
using MegaCrit.Sts2.Core.Models.Monsters;
using MegaCrit.Sts2.Core.MonsterMoves.Intents;
using MegaCrit.Sts2.Core.MonsterMoves.MonsterMoveStateMachine;
using MegaCrit.Sts2.Core.Rooms;
using SlayTheRequiem.SlayTheRequiemCode.Powers;
using SlayTheRequiem.SlayTheRequiemCode.Rules;

namespace SlayTheRequiem.SlayTheRequiemCode.Monsters;

/// <summary>
/// Элита «Хранитель Гробницы» (Морокей; docs/design/11 акт 2а). Маг-нежить: посох высасывает ✦ героя (а без ✦ бьёт
/// вдвое), аура −1 ✦ героя в начале хода, на 1-м ходу поднимает Рыцарей-духов (<see cref="SpectralGuardPower"/>),
/// пока они стоят — Хранитель получает вдвое меньше урона. Уязвим к свету (нежить) и огню (таблица врагов).
/// Облик — заимствованный у ванильного Призрачного рыцаря, пока нет своих спрайтов (этап 6).
/// </summary>
public sealed class TombGuardian : CustomMonsterModel
{
    public const int StaffDamage = 10;
    public const int StaffManaDrain = 3;
    public const int SoulFlameDamage = 4;
    public const int SoulFlameHits = 3;
    public const int KnightsHp = 45;
    public const int AuraManaDrain = 1;

    public override int MinInitialHp => 125;
    public override int MaxInitialHp => 125;

    public override string? CustomVisualPath => SceneHelper.GetScenePath("creature_visuals/spectral_knight");

    public override CreatureAnimator? SetupCustomAnimationStates(MegaSprite controller) =>
        ModelDb.Monster<SpectralKnight>().GenerateAnimator(controller);

    protected override MonsterMoveStateMachine GenerateMoveStateMachine()
    {
        var raise = new MoveState("RAISE_KNIGHTS", RaiseKnights, new BuffIntent());
        var staff = new MoveState("STAFF_DRAIN", StaffDrain, new SingleAttackIntent(StaffDamage), new DebuffIntent());
        var flame = new MoveState("SOUL_FLAME", SoulFlame, new MultiAttackIntent(SoulFlameDamage, SoulFlameHits));
        var reraise = new MoveState("RERAISE_KNIGHTS", RaiseKnights, new BuffIntent());
        raise.FollowUpState = staff;
        staff.FollowUpState = flame;
        flame.FollowUpState = reraise;
        reraise.FollowUpState = staff;
        return new MonsterMoveStateMachine([raise, staff, flame, reraise], raise);
    }

    private async Task RaiseKnights(IReadOnlyList<Creature> targets)
    {
        await CreatureCmd.TriggerAnim(Creature, "Cast", 0.3f);
        var current = Creature.GetPowerAmount<SpectralGuardPower>();
        if (current < KnightsHp)
            await PowerCmd.Apply<SpectralGuardPower>(new ThrowingPlayerChoiceContext(), Creature, KnightsHp - current, Creature, null);
    }

    private async Task StaffDrain(IReadOnlyList<Creature> targets)
    {
        // Без ✦ у цели посох пьёт ♥: удар вдвое сильнее.
        foreach (var target in targets)
        {
            var empty = target.IsPlayer && Mana.Amount(target) <= 0;
            await DamageCmd.Attack(empty ? StaffDamage * 2 : StaffDamage).FromMonster(this).Targeting(target)
                .WithAttackerAnim("AttackSword", 0.25f).WithHitFx("vfx/vfx_attack_slash").Execute(null);
            if (target.IsPlayer && !empty) await Mana.Lose(target.Player, StaffManaDrain);
        }
    }

    private Task SoulFlame(IReadOnlyList<Creature> targets) =>
        DamageCmd.Attack(SoulFlameDamage).WithHitCount(SoulFlameHits).FromMonster(this).OnlyPlayAnimOnce()
            .WithAttackerAnim("AttackFlame", 0.25f).Execute(null);

    /// <summary>Аура: −1 ✦ каждому герою в начале его хода.</summary>
    public override async Task AfterPlayerTurnStart(PlayerChoiceContext choiceContext, Player player)
    {
        if (Creature == null || !Creature.IsAlive || !RequiemMode.IsInSystem(player)) return;
        await Mana.Lose(player, AuraManaDrain);
    }
}

/// <summary>Энкаунтер элиты: акт 2 (в ванильной раскладке — Улей; в пяти актах Реквиема — 2а «Курганы»).</summary>
public sealed class TombGuardianElite() : CustomEncounterModel(RoomType.Elite)
{
    public override bool IsValidForAct(ActModel act) => act is Hive;

    public override IEnumerable<MonsterModel> AllPossibleMonsters => [ModelDb.Monster<TombGuardian>()];

    protected override IReadOnlyList<(MonsterModel, string?)> GenerateMonsters() =>
        [(ModelDb.Monster<TombGuardian>().ToMutable(), null)];
}
