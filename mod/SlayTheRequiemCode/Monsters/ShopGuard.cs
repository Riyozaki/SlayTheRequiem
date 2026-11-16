using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.MonsterMoves.Intents;
using MegaCrit.Sts2.Core.MonsterMoves.MonsterMoveStateMachine;
using MegaCrit.Sts2.Core.Rooms;
using V = MegaCrit.Sts2.Core.Models.Powers;

namespace SlayTheRequiem.SlayTheRequiemCode.Monsters;

/// <summary>
/// Стража лавки (docs/design/14 §1, M5): облегчённая элита, которая выходит на пойманного вора. 64 ♥: алебарда, удар щитом со Слабостью, «Стоять!» — Блок и Сила. Свой спрайт (<c>images/monsters/shop_guard.png</c>).
/// </summary>
public sealed class ShopGuard : OwnArtMonster
{
    public const int BaseHp = 64;
    public const int HalberdDamage = 11;
    public const int BashDamage = 7;
    public const int BashWeak = 1;
    public const int HaltBlock = 10;
    public const int HaltStrength = 2;

    protected override int Hp => BaseHp;
    public override string ArtKey => "shop_guard";
    protected override MonsterModel LookOf => ModelDb.Monster<MegaCrit.Sts2.Core.Models.Monsters.FlailKnight>();
    protected override string? CastAnim => null;

    protected override IReadOnlyList<MoveState> Cycle() =>
    [
        new MoveState("GUARD_HALT", async _ =>
        {
            await Block(HaltBlock);
            await Buff<V.StrengthPower>(HaltStrength);
        }, new DefendIntent(), new BuffIntent()),
        new MoveState("GUARD_HALBERD", _ => Hit(HalberdDamage), Atk(HalberdDamage)),
        new MoveState("GUARD_BASH", async _ =>
        {
            await Hit(BashDamage, fx: "vfx/vfx_attack_blunt");
            await Debuff<V.WeakPower>(BashWeak);
        }, Atk(BashDamage), new DebuffIntent()),
    ];
}

/// <summary>
/// Бой со Стражей при пойманной краже: случайно не выпадает (<see cref="RequiemEncounter"/> без актов). Награды нет —
/// победа лишь выпускает вора из лавки (после боя открывается карта).
/// </summary>
public sealed class ShopGuardElite() : RequiemEncounter(RoomType.Elite)
{
    protected override IReadOnlyList<MonsterModel> Lineup => [ModelDb.Monster<ShopGuard>()];
    public override bool ShouldGiveRewards => false;
}
