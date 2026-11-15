using BaseLib.Utils;
using Godot;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.CardPools;
using MegaCrit.Sts2.Core.Models.RelicPools;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Saves.Runs;
using MegaCrit.Sts2.Core.ValueProps;
using SlayTheRequiem.SlayTheRequiemCode.Cards;
using SlayTheRequiem.SlayTheRequiemCode.Core;
using SlayTheRequiem.SlayTheRequiemCode.Pantheon;
using SlayTheRequiem.SlayTheRequiemCode.Powers;
using SlayTheRequiem.SlayTheRequiemCode.Rules;

namespace SlayTheRequiem.SlayTheRequiemCode.Transformations;

/// <summary>
/// Праведник (docs/design/06 §1.1) — только при праведности «Праведник». Сущность «Ореол»: при ♥ &lt; 50 % раз за бой —
/// +10 ♥ и Оберег 10. Особая карта «Кара». Ступени за обеты: босс акта побеждён Праведником без Бездонных вещей —
/// 1 «Аура стойкости» (Латы +1), 2 «Небесный щит» (открывает Дух божества, ставится на Знак веры); дальше — Путь Паладина.
/// Обет: Бездонная вещь снимает форму. Потеряв праведность (грех), форма молчит до Искупления.
/// Не делает Отступником (не <see cref="ITransformationForm"/>).
/// </summary>
[Pool(typeof(SharedRelicPool))]
public sealed class RighteousForm : FormRelic
{
    public override FormKind Kind => FormKind.Righteous;
    protected override IEnumerable<Type> FormCards => [typeof(Smite)];
    public override Color? Tint => new Color(1f, 0.97f, 0.86f);
    public override Color FlashColor => new(1f, 0.9f, 0.4f);

    public override bool ShowCounter => true;
    public override int DisplayAmount => Owned ? _step : 0;

    protected override IEnumerable<DynamicVar> CanonicalVars => [new DynamicVar("Step", 0)];

    private int _step;

    /// <summary>Ступень: 0 Ореол, 1 Аура стойкости, 2 Небесный щит.</summary>
    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public int Step
    {
        get => _step;
        set
        {
            AssertMutable();
            _step = Math.Clamp(value, 0, TransformationRules.MaxRighteousStep);
            if (DynamicVars.TryGetValue("Step", out var v)) v.BaseValue = _step;
            InvokeDisplayAmountChanged();
        }
    }

    /// <summary>Действует, пока герой — Праведник.</summary>
    public bool Active => Owned && Faith.Effective(Owner) == Piety.Righteous;

    protected override Boon ComputeBoon() =>
        Active && _step >= 1 ? new Boon { Plating = TransformationRules.AuraPlating } : Boon.None;

    private bool _haloUsed;

    public override Task BeforeCombatStart()
    {
        _haloUsed = false;
        return base.BeforeCombatStart();
    }

    public override async Task AfterDamageReceived(PlayerChoiceContext choiceContext, Creature target, DamageResult result, ValueProp props, Creature? dealer, CardModel? cardSource)
    {
        if (!Active || target != Owner.Creature || !target.IsAlive) return;
        if (!TransformationRules.HaloTriggers(target.CurrentHp, target.MaxHp, _haloUsed)) return;
        _haloUsed = true;
        Flash();
        FormVisuals.Refresh(Owner, flash: FlashColor);
        await CreatureCmd.Heal(target, TransformationRules.HaloHeal);
        await PowerCmd.Apply<SpellShieldPower>(choiceContext, target, TransformationRules.HaloSpellShield, target, null);
    }

    public override async Task AfterCombatVictory(CombatRoom room)
    {
        if (room.RoomType != RoomType.Boss || !Owned) return;
        var kept = Active && !Faith.HoldsAbyssal(Owner);
        var next = TransformationRules.RighteousStepAfterAct(_step, kept);
        if (next == _step) return;
        Step = next;
        Flash();
        if (_step >= TransformationRules.MaxRighteousStep && Faith.SigilOf(Owner) is { } sigil)
        {
            sigil.HeavenlyShield = true;
            sigil.Refresh();
        }
        if (CelestialBlessing.Of(Owner) is { } blessing) await blessing.SyncMaxHp();
        await SyncMaxHp();
    }

    /// <summary>Обет: Бездонная вещь у героя — форма снимается.</summary>
    public override async Task AfterRoomEntered(AbstractRoom room)
    {
        await base.AfterRoomEntered(room);
        if (Owned && Faith.HoldsAbyssal(Owner)) await Shed();
    }

    protected override async Task OnFormLost()
    {
        if (Faith.SigilOf(Owner) is { HeavenlyShield: true } sigil)
        {
            sigil.HeavenlyShield = false;
            sigil.Refresh();
            if (CelestialBlessing.Of(Owner) is { } blessing) await blessing.SyncMaxHp();
        }
    }
}

/// <summary>Кара: 1⚡ 1✦ — 8 урона светом; по нежити и Бездне ещё Ошеломление 5. У Паладина — без ✦.</summary>
[Pool(typeof(TokenCardPool))]
public sealed class Smite : RequiemCard
{
    public override DamageType DamageType => DamageType.Light;
    public override int CanonicalStarCost => 1;

    public Smite() : base(1, CardType.Attack, CardRarity.Event, TargetType.AnyEnemy)
    {
        WithDamage(TransformationRules.SmiteDamage, TransformationRules.SmiteDamageUpgrade);
        WithPower<StaggerPower>(TransformationRules.SmiteStagger);
    }

    protected override async Task OnPlay(PlayerChoiceContext ctx, CardPlay play)
    {
        ArgumentNullException.ThrowIfNull(play.Target);
        await Hit(ctx, play.Target, "vfx/vfx_attack_light");
        if (play.Target.IsAlive && EnemySetup.IsUndeadOrAbyss(play.Target))
            await Apply<StaggerPower>(ctx, play.Target, Var(nameof(StaggerPower)));
    }
}
