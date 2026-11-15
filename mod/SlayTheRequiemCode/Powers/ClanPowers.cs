using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Commands.Builders;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;
using SlayTheRequiem.SlayTheRequiemCode.Core;
using SlayTheRequiem.SlayTheRequiemCode.Rules;

namespace SlayTheRequiem.SlayTheRequiemCode.Powers;

// Силы предысторий кланов Латоносца (docs/design/04 «Латоносец», completion plan M4 п. 1).

/// <summary>
/// Дух света («Брат Рассвета» Дюнных клинков): бесплатно поддерживаемый дух — в начале каждого хода героя
/// {Amount} урона светом случайному врагу. Держится весь бой.
/// </summary>
public sealed class LightSpiritPower : RequiemPower
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    public override async Task AfterPlayerTurnStart(PlayerChoiceContext choiceContext, Player player)
    {
        if (player.Creature != Owner || Amount <= 0 || Owner.CombatState is not { } combat) return;
        var enemies = combat.HittableEnemies.Where(e => e.IsAlive).ToList();
        if (enemies.Count == 0) return;
        var target = enemies[combat.RunState.Rng.CombatTargets.NextInt(enemies.Count)];
        Flash();
        using (DamageTyping.As(DamageType.Light))
            await CreatureCmd.Damage(choiceContext, target, Amount, ValueProp.Unpowered, Owner, null);
    }
}

/// <summary>
/// Дух клинка («Клинок-ансей» Дюнных клинков, карта-переключатель <see cref="Cards.Platebearer.BladeSpirit"/>):
/// пока дух призван, каждая не-заклинательная атака героя тратит 1 ✦ и добавляет урон магией ⌊✦/2⌋ по своим целям
/// (✦ считается до траты). Без ✦ дух молчит.
/// </summary>
public sealed class BladeSpiritPower : RequiemPower
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Single;

    public override async Task AfterAttack(PlayerChoiceContext choiceContext, AttackCommand command)
    {
        if (command.Attacker != Owner || command.ModelSource is not CardModel card || card.Type != CardType.Attack
            || card is Spells.SpellCard || Owner.Player is not { } player) return;
        var mana = Mana.Amount(player);
        if (mana < 1) return;
        var bonus = OriginRules.BladeSpiritBonus(mana);
        await Mana.Lose(player, 1);
        if (bonus <= 0) return;
        Flash();
        foreach (var target in command.Results.SelectMany(h => h).Select(r => r.Receiver).Distinct().ToList())
        {
            if (target == null || !target.IsAlive) continue;
            using (DamageTyping.As(DamageType.Magic))
                await CreatureCmd.Damage(choiceContext, target, bonus, ValueProp.Unpowered, Owner, null);
        }
    }
}

/// <summary>
/// «Верный-до-Конца» (Посланник Цитадельцев): страж со щитом. Принимает удары по герою первым, как Ости у
/// Некробиндер, и бьёт в конце хода. Растёт с уровнем героя (<see cref="OriginRules.LoyalGuard"/>). Это спутник, а не
/// призыв: слот призыва не занимает и другие призывы его не вытесняют. С M6 — существо на поле (<see cref="Monsters.LoyalGuardSummon"/>).
/// </summary>
public sealed class LoyalGuardPower : SummonPower
{
    private int _damage = 2;
    public override SummonKind Kind => SummonKind.LoyalGuard;
    public override int AttackDamage => _damage;
    protected override MonsterModel CreatureModel => ModelDb.Monster<Monsters.LoyalGuardSummon>();
    public void SetDamage(int damage) => _damage = Math.Max(0, damage);
}
