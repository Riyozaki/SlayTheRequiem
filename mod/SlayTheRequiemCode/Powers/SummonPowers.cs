using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;
using SlayTheRequiem.SlayTheRequiemCode.Core;
using SlayTheRequiem.SlayTheRequiemCode.Rules;

namespace SlayTheRequiem.SlayTheRequiemCode.Powers;

/// <summary>
/// Призыв (docs/design/08 §1, §5) — MVP без отдельного существа на поле: сила на герое, Amount = ♥ призыва.
/// Призыв «стоит между героем и врагами»: урон атак врагов по ♥ героя сначала идёт в ♥ призыва; в конце хода героя
/// призыв бьёт. Слот призыва один — новый призыв прогоняет прежний (<see cref="Summon{T}"/>).
/// TODO(этап 6): настоящие существа через BaseLib CustomPetModel, когда будут спрайты.
/// </summary>
public abstract class SummonPower : RequiemPower
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    public abstract int AttackDamage { get; }
    public virtual DamageType AttackType => DamageType.Physical;
    public virtual bool HitsAll => false;

    private int _pending;
    private Creature? _pendingAttacker;

    public static async Task Summon<T>(PlayerChoiceContext ctx, Creature hero, int hp, CardModel? source) where T : SummonPower
    {
        foreach (var other in hero.Powers.OfType<SummonPower>().Where(p => p is not T).ToList())
            await PowerCmd.Remove(other);
        await PowerCmd.Apply<T>(ctx, hero, hp, hero, source);
    }

    public override decimal ModifyHpLostBeforeOsty(Creature target, decimal amount, ValueProp props, Creature? dealer, CardModel? cardSource)
    {
        if (target != Owner || amount <= 0 || dealer == null || dealer == Owner || !props.IsCardOrMonsterMove()) return amount;
        var absorbed = Math.Min((int)amount, Amount - _pending);
        if (absorbed <= 0) return amount;
        _pending += absorbed;
        _pendingAttacker = dealer;
        return amount - absorbed;
    }

    public override async Task AfterDamageReceived(PlayerChoiceContext choiceContext, Creature target, DamageResult result, ValueProp props, Creature? dealer, CardModel? cardSource)
    {
        if (target != Owner || _pending <= 0) return;
        var taken = _pending;
        var attacker = _pendingAttacker;
        _pending = 0;
        _pendingAttacker = null;
        Flash();
        if (attacker != null && attacker.IsAlive) await OnStruck(choiceContext, attacker);
        if (taken >= Amount) await PowerCmd.Remove(this);
        else await PowerCmd.ModifyAmount(choiceContext, this, -taken, null, null, silent: true);
    }

    public override async Task AfterSideTurnEnd(PlayerChoiceContext choiceContext, CombatSide side, IEnumerable<Creature> participants)
    {
        if (!participants.Contains(Owner) || Owner.CombatState == null) return;
        var enemies = Owner.CombatState.HittableEnemies.Where(e => e.IsAlive).ToList();
        if (enemies.Count == 0) return;
        var targets = HitsAll ? enemies : enemies.Take(1).ToList();
        foreach (var enemy in targets)
        {
            using (DamageTyping.As(AttackType))
                await CreatureCmd.Damage(choiceContext, enemy, AttackDamage + SummonBonusDamage, ValueProp.Unpowered, Owner, null);
            if (enemy.IsAlive) await OnHit(choiceContext, enemy);
        }
    }

    /// <summary>Надбавка урона призывов («Призыватель» у Туманных).</summary>
    public int SummonBonusDamage => Owner.Player?.Relics.OfType<Relics.SummonerBackground>().Any() == true
        ? Relics.SummonerBackground.BonusDamage : 0;

    protected virtual Task OnHit(PlayerChoiceContext ctx, Creature enemy) => Task.CompletedTask;
    protected virtual Task OnStruck(PlayerChoiceContext ctx, Creature attacker) => Task.CompletedTask;
}

/// <summary>Волк-дух: бьёт на 4.</summary>
public sealed class SpiritWolfPower : SummonPower
{
    public override int AttackDamage => 4;
}

/// <summary>Огненный атронах: 5 огнём + Горение 2.</summary>
public sealed class FlameAtronachPower : SummonPower
{
    public override int AttackDamage => 5;
    public override DamageType AttackType => DamageType.Fire;
    protected override Task OnHit(PlayerChoiceContext ctx, Creature enemy) => PowerCmd.Apply<BurningPower>(ctx, enemy, 2, Owner, null);
}

/// <summary>Ледяной атронах: 4 холодом; принявший удар — Озноб 2 атакующему.</summary>
public sealed class FrostAtronachPower : SummonPower
{
    public override int AttackDamage => 4;
    public override DamageType AttackType => DamageType.Frost;
    protected override Task OnStruck(PlayerChoiceContext ctx, Creature attacker) => PowerCmd.Apply<ChillPower>(ctx, attacker, 2, Owner, null);
}

/// <summary>Грозовой атронах: 6 разрядом всем.</summary>
public sealed class StormAtronachPower : SummonPower
{
    public override int AttackDamage => 6;
    public override DamageType AttackType => DamageType.Shock;
    public override bool HitsAll => true;
}

/// <summary>
/// Дух зверя («Зверобой-духовидец» Инеерождённых): волк → саблезуб → медведь по числу побеждённых элит.
/// Урон задаёт предыстория при призыве.
/// </summary>
public sealed class BeastSpiritPower : SummonPower
{
    private int _damage = 3;
    public override int AttackDamage => _damage;
    public void SetDamage(int damage) => _damage = damage;
}
