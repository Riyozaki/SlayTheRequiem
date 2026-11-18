using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.ValueProps;
using SlayTheRequiem.SlayTheRequiemCode.Core;
using SlayTheRequiem.SlayTheRequiemCode.Monsters;
using SlayTheRequiem.SlayTheRequiemCode.Rules;

namespace SlayTheRequiem.SlayTheRequiemCode.Powers;

/// <summary>
/// Призыв (docs/design/08 §1, §5; M6 п. 2) — сила на герое (Amount = ♥ призыва) плюс настоящее существо на поле
/// (<see cref="SummonCreature"/>, питомец героя, как Ости у Некробиндер). Сила — источник правил и ♥ (её видят реликвии,
/// Амулет Говорящего с Мёртвыми, «Эфирная связь»), существо — её тело: показывает ♥, намерение, принимает удары и умирает.
/// <list type="bullet">
/// <item>Урон атак врагов по ♥ героя сначала уходит в призыв (<see cref="SummonRules.Absorb"/>), удар виден на существе.</item>
/// <item>В конце хода героя существо бьёт (намерение над ним показано весь ход).</item>
/// <item>♥ существа и стаки силы сверяются в обе стороны: прибавка стаков лечит существо, урон по существу снимает стаки.</item>
/// <item>Смерть существа снимает силу и будит <see cref="ISummonListener"/> (Взрыв смерти Лича, Обет Сумеречной Звезды);
/// вытеснение из слота (<see cref="Summon{T}"/>) — нет: призыв просто развеивается.</item>
/// </list>
/// Если существо поставить нельзя (не идёт бой, тест), призыв работает по-старому — одной силой.
/// </summary>
public abstract class SummonPower : RequiemPower
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    public abstract SummonKind Kind { get; }
    public abstract int AttackDamage { get; }
    public virtual DamageType AttackType => DamageType.Physical;
    public virtual bool HitsAll => false;
    /// <summary>Спутник («Верный-до-Конца»): слот призыва не занимает, другие призывы его не вытесняют.</summary>
    public bool IsCompanion => SummonRules.IsCompanion(Kind);
    /// <summary>Нежить («Поднять мертвеца», прислужник Лича): +3 ♥ от Амулета Говорящего с Мёртвыми (07 §5.1).</summary>
    public bool IsUndead => SummonRules.IsUndead(Kind);

    /// <summary>Модель существа на поле (питомец героя).</summary>
    protected abstract MonsterModel CreatureModel { get; }

    private int _pending;
    private Creature? _pet;
    private int _turnsOnField;
    private bool _destroyed;
    private bool _syncing;
    private Task? _spawning;

    /// <summary>Существо этого призыва, если оно стоит на поле.</summary>
    public Creature? Pet => _pet is { IsAlive: true } ? _pet : null;

    /// <summary>Что призыв сделает в конце этого хода.</summary>
    public SummonMove NextMove => SummonRules.MoveFor(Kind, _turnsOnField);

    /// <summary>Урон удара с надбавками — его же показывает намерение существа.</summary>
    public int StrikeDamage => AttackDamage + SummonBonusDamage;

    /// <summary>
    /// Призвать: лишние по слотам — самые старые — развеиваются, сила получает стаки, на поле встаёт (или лечится) существо.
    /// <paramref name="setup"/> — донастройка силы до появления существа (урон и облик «Духа зверя», «Верного-до-Конца»).
    /// </summary>
    public static async Task Summon<T>(PlayerChoiceContext ctx, Creature hero, int hp, CardModel? source, Action<T>? setup = null) where T : SummonPower
    {
        var others = hero.Powers.OfType<SummonPower>().Where(p => p is not T && !p.IsCompanion).ToList();
        if (!ModelDb.Power<T>().IsCompanion)
            foreach (var other in others.Take(SummonRules.Dismissals(others.Count, Slots(hero))).ToList()) await other.Dismiss();
        await PowerCmd.Apply<T>(ctx, hero, hp, hero, source);
        if (hero.GetPower<T>() is not { } power) return;
        setup?.Invoke(power);
        await power.EnsurePet();
    }

    /// <summary>Слоты призыва героя.</summary>
    public static int Slots(Creature hero) => SummonRules.Slots(Modifiers(hero).Sum(m => m.SummonSlotBonus));

    private static IEnumerable<ISummonModifier> Modifiers(Creature? hero) =>
        hero?.Player?.Relics.OfType<ISummonModifier>() ?? Enumerable.Empty<ISummonModifier>();

    /// <summary>Надбавка урона призывов («Призыватель» у Туманных, дар Разрушителя Врат).</summary>
    public int SummonBonusDamage => (Owner.Player?.Relics.OfType<Relics.SummonerBackground>().Any() == true
        ? Relics.SummonerBackground.BonusDamage : 0) + Modifiers(Owner).Sum(m => m.SummonBonusDamage);

    // ───────────── существо ─────────────

    /// <summary>Поставить существо (если его нет) и сверить его ♥ со стаками.</summary>
    public async Task EnsurePet()
    {
        if (Pet == null)
        {
            if (_spawning == null) _spawning = SpawnPet();
            try { await _spawning; }
            finally { _spawning = null; }
        }
        await SyncPet();
    }

    private async Task SpawnPet()
    {
        var player = Owner.Player;
        var state = Owner.CombatState;
        if (player == null || state == null || Amount <= 0 || !CombatManager.Instance.IsInProgress) return;
        Creature? pet = null;
        try
        {
            pet = state.CreateCreature((MonsterModel)CreatureModel.ToMutable(), Owner.Side, null);
            await PlayerCmd.AddPet(pet, player);
            _pet = pet;
            _turnsOnField = 0;
            if (pet.Monster is SummonCreature body) body.Bond = this;
            await CreatureCmd.SetMaxHp(pet, Amount);
            await CreatureCmd.SetCurrentHp(pet, Amount);
            SummonLayout.Arrange(player);
            ShowIntent();
            UI.RequiemVfx.Summon(pet, Kind);
        }
        catch (Exception e)
        {
            // AddPet callbacks may throw AFTER insertion. Keep that real body/bond; never create an
            // orphan duplicate on the next EnsurePet. Without an inserted body the power stays playable.
            _pet = pet != null && state.ContainsCreature(pet) && pet.IsAlive ? pet : null;
            if (_pet?.Monster is SummonCreature body) body.Bond = this;
            MainFile.Logger.Warn($"Summon {Kind}: body={(_pet == null ? "fallback" : "observed-inserted")} after callback failure: {e.Message}");
        }
    }

    /// <summary>♥ существа = стакам силы (прибавка стаков — лечение и рост макс. ♥).</summary>
    private async Task SyncPet()
    {
        var pet = Pet;
        if (pet == null || _syncing) return;
        _syncing = true;
        try
        {
            if (pet.MaxHp < Amount) await CreatureCmd.SetMaxHp(pet, Amount);
            if (pet.CurrentHp != Amount && Amount > 0) await CreatureCmd.SetCurrentHp(pet, Amount);
        }
        finally { _syncing = false; }
    }

    /// <summary>Урон пришёл по самому существу — стаки следуют за его ♥.</summary>
    private async Task SyncFromPet(PlayerChoiceContext ctx)
    {
        if (_pet == null || _syncing) return;
        if (_pet.IsDead) { await Destroyed(); return; }
        var delta = _pet.CurrentHp - Amount;
        if (delta == 0) return;
        _syncing = true;
        try { await PowerCmd.ModifyAmount(ctx, this, delta, null, null, silent: true); }
        finally { _syncing = false; }
    }

    /// <summary>Показать намерение существа на этот ход.</summary>
    public void ShowIntent()
    {
        if (Pet?.Monster is SummonCreature body) body.Show(NextMove);
    }

    /// <summary>Призыв пал: сила снимается, слушатели (Лич, Обет Сумеречной Звезды) узнают об этом.</summary>
    private async Task Destroyed()
    {
        if (_destroyed) return;
        _destroyed = true;
        await PowerCmd.Remove(this);
        if (Owner.Player != null)
            foreach (var listener in Owner.Player.Relics.OfType<ISummonListener>().ToList())
                await listener.AfterSummonDestroyed(this);
    }

    /// <summary>Вытеснен новым призывом: развеивается без «гибели».</summary>
    private async Task Dismiss()
    {
        _destroyed = true;
        await PowerCmd.Remove(this);
    }

    public override async Task AfterRemoved(Creature oldOwner)
    {
        await base.AfterRemoved(oldOwner);
        var pet = Pet;
        _pet = null;
        if (pet != null && CombatManager.Instance.IsInProgress) await CreatureCmd.Kill(pet);
        if (oldOwner.Player != null) SummonLayout.Arrange(oldOwner.Player);
    }

    public override async Task AfterPowerAmountChanged(PlayerChoiceContext choiceContext, PowerModel power, decimal amount, Creature? applier, CardModel? cardSource)
    {
        if (power == this && amount > 0) await SyncPet();
    }

    public override Task AfterDeath(PlayerChoiceContext choiceContext, Creature creature, bool wasRemovalPrevented, float deathAnimLength) =>
        creature == _pet && !wasRemovalPrevented ? Destroyed() : Task.CompletedTask;

    // ───────────── удары по герою ─────────────

    internal decimal MitigateHit(Creature target, decimal amount, ValueProp props, Creature? dealer, CardModel? cardSource)
    {
        if (target != Owner || amount <= 0 || dealer == null || dealer == Owner || !props.IsCardOrMonsterMove()) return amount;
        var absorbed = SummonRules.Absorb((int)amount, Amount, _pending);
        if (absorbed <= 0) return amount;
        if (!HitDefensePipeline.WillCommit) return amount - absorbed;
        _pending += absorbed;
        HitDefensePipeline.Defer(async () =>
        {
            _pending -= absorbed;
            Flash();
            if (dealer.IsAlive) await OnStruck(new ThrowingPlayerChoiceContext(), dealer);
            var loss = SummonRules.ScaledLoss(absorbed, Modifiers(Owner).Sum(m => m.SummonDamageTakenPercent));
            if (Pet is { } pet)
            {
                // Already resisted/blocked at the hero. Do NOT charge the hero's Block a second time via
                // native pet-owner Block, reapply elemental modifiers, or drop the packet if its dealer died.
                using var transfer = DamageTyping.As(DamageType.True);
                await CreatureCmd.Damage(new ThrowingPlayerChoiceContext(), pet, loss,
                    ValueProp.Unpowered | ValueProp.Unblockable, null, null);
            }
            else if (loss >= Amount) await Destroyed();
            else await PowerCmd.ModifyAmount(new ThrowingPlayerChoiceContext(), this, -loss, null, null, silent: true);
        }, () => _pending -= absorbed);
        return amount - absorbed;
    }

    public override Task AfterDamageReceived(PlayerChoiceContext choiceContext, Creature target, DamageResult result, ValueProp props, Creature? dealer, CardModel? cardSource) =>
        target == _pet && target != Owner ? SyncFromPet(choiceContext) : Task.CompletedTask;

    // ───────────── ход призыва ─────────────

    public override async Task AfterSideTurnStart(CombatSide side, IReadOnlyList<Creature> participants, ICombatState combatState)
    {
        if (side == CombatSide.Player && participants.Contains(Owner)) ShowIntent();
        await Task.CompletedTask;
    }

    public override async Task AfterSideTurnEnd(PlayerChoiceContext choiceContext, CombatSide side, IEnumerable<Creature> participants)
    {
        if (!participants.Contains(Owner) || Owner.CombatState == null) return;
        var move = NextMove;
        _turnsOnField++;
        var node = Pet == null ? null : NCombatRoom.Instance?.GetCreatureNode(Pet);
        if (node != null) await node.PerformIntent();
        if (move == SummonMove.Slumber) return;
        var enemies = Owner.CombatState.HittableEnemies.Where(e => e.IsAlive).ToList();
        if (enemies.Count == 0) return;
        if (Pet is { } pet) await CreatureCmd.TriggerAnim(pet, "Attack", 0.25f);
        var targets = HitsAll ? enemies : enemies.Take(1).ToList();
        foreach (var enemy in targets)
        {
            using (DamageTyping.As(AttackType))
                await CreatureCmd.Damage(choiceContext, enemy, StrikeDamage, ValueProp.Unpowered, Owner, null);
            if (enemy.IsAlive) await OnHit(choiceContext, enemy);
        }
    }

    protected virtual Task OnHit(PlayerChoiceContext ctx, Creature enemy) => Task.CompletedTask;
    protected virtual Task OnStruck(PlayerChoiceContext ctx, Creature attacker) => Task.CompletedTask;
}

/// <summary>Реликвия, меняющая призывы: слоты, урон, получаемый урон (Знамение Обряда, дары Князей).</summary>
public interface ISummonModifier
{
    int SummonSlotBonus { get; }
    int SummonBonusDamage { get; }
    /// <summary>+N % урона по ♥ призыва.</summary>
    int SummonDamageTakenPercent { get; }
}

/// <summary>Реакция на гибель призыва.</summary>
public interface ISummonListener
{
    Task AfterSummonDestroyed(SummonPower summon);
}

/// <summary>Волк-дух: бьёт на 4.</summary>
public sealed class SpiritWolfPower : SummonPower
{
    public override SummonKind Kind => SummonKind.SpiritWolf;
    public override int AttackDamage => 4;
    protected override MonsterModel CreatureModel => ModelDb.Monster<SpiritWolfSummon>();
}

/// <summary>Огненный атронах: 5 огнём + Горение 2.</summary>
public sealed class FlameAtronachPower : SummonPower
{
    public override SummonKind Kind => SummonKind.FlameAtronach;
    public override int AttackDamage => 5;
    public override DamageType AttackType => DamageType.Fire;
    protected override MonsterModel CreatureModel => ModelDb.Monster<FlameAtronachSummon>();
    protected override Task OnHit(PlayerChoiceContext ctx, Creature enemy) => PowerCmd.Apply<BurningPower>(ctx, enemy, 2, Owner, null);
}

/// <summary>Ледяной атронах: 4 холодом; принявший удар — Озноб 2 атакующему.</summary>
public sealed class FrostAtronachPower : SummonPower
{
    public override SummonKind Kind => SummonKind.FrostAtronach;
    public override int AttackDamage => 4;
    public override DamageType AttackType => DamageType.Frost;
    protected override MonsterModel CreatureModel => ModelDb.Monster<FrostAtronachSummon>();
    protected override Task OnStruck(PlayerChoiceContext ctx, Creature attacker) => PowerCmd.Apply<ChillPower>(ctx, attacker, 2, Owner, null);
}

/// <summary>Грозовой атронах: 6 разрядом всем.</summary>
public sealed class StormAtronachPower : SummonPower
{
    public override SummonKind Kind => SummonKind.StormAtronach;
    public override int AttackDamage => 6;
    public override DamageType AttackType => DamageType.Shock;
    public override bool HitsAll => true;
    protected override MonsterModel CreatureModel => ModelDb.Monster<StormAtronachSummon>();
}

/// <summary>
/// Дух зверя («Зверобой-духовидец» Инеерождённых): волк → саблезуб → медведь по числу побеждённых элит
/// (<see cref="SummonRules.BeastKind"/>). Урон и облик задаёт предыстория при призыве (<see cref="SetStage"/>).
/// </summary>
public sealed class BeastSpiritPower : SummonPower
{
    private int _damage = 3;
    private SummonKind _kind = SummonKind.BeastWolf;
    public override SummonKind Kind => _kind;
    public override int AttackDamage => _damage;

    protected override MonsterModel CreatureModel => _kind switch
    {
        SummonKind.BeastSabertooth => ModelDb.Monster<BeastSabertoothSummon>(),
        SummonKind.BeastBear => ModelDb.Monster<BeastBearSummon>(),
        _ => ModelDb.Monster<BeastWolfSummon>(),
    };

    public void SetStage(int elitesDefeated, int damage)
    {
        _kind = SummonRules.BeastKind(elitesDefeated);
        _damage = Math.Max(0, damage);
    }
}

/// <summary>Поднятый мертвец («Поднять мертвеца»): нежить, бьёт на 4.</summary>
public sealed class RaisedDeadPower : SummonPower
{
    public override SummonKind Kind => SummonKind.RaisedDead;
    public override int AttackDamage => SummonRules.RaisedDeadDamage;
    protected override MonsterModel CreatureModel => ModelDb.Monster<RaisedDeadSummon>();
}

/// <summary>Дремлющий страж («Страж Бездны»): первый ход дремлет, затем бьёт 10 магией.</summary>
public sealed class AbyssGuardianPower : SummonPower
{
    public override SummonKind Kind => SummonKind.AbyssGuardian;
    public override int AttackDamage => SummonRules.AbyssGuardianDamage;
    public override DamageType AttackType => DamageType.Magic;
    protected override MonsterModel CreatureModel => ModelDb.Monster<AbyssGuardianSummon>();
}

/// <summary>Призрачный рыцарь («Зов Рыцаря-Духа»): бьёт на 5; пока стоит — атаки героя +2.</summary>
public sealed class SpectralKnightPower : SummonPower
{
    public override SummonKind Kind => SummonKind.SpectralKnight;
    public override int AttackDamage => SummonRules.SpectralKnightDamage;
    protected override MonsterModel CreatureModel => ModelDb.Monster<SpectralKnightSummon>();

    public override decimal ModifyDamageAdditive(Creature? target, decimal amount, ValueProp props, Creature? dealer, CardModel? cardSource) =>
        dealer == Owner && cardSource != null && props.IsPoweredAttack() && Amount > 0 ? SummonRules.SpectralKnightAttackBonus : 0m;
}

/// <summary>Костяной прислужник Лича: нежить, бьёт 3 магией; его гибель — Взрыв смерти.</summary>
public sealed class BoneThrallPower : SummonPower
{
    public override SummonKind Kind => SummonKind.BoneThrall;
    public override int AttackDamage => SummonRules.BoneThrallDamage;
    public override DamageType AttackType => DamageType.Magic;
    protected override MonsterModel CreatureModel => ModelDb.Monster<BoneThrallSummon>();
}
