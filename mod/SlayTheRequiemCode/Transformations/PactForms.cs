using BaseLib.Utils;
using Godot;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.Entities.Relics;
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
using SlayTheRequiem.SlayTheRequiemCode.Equipment;
using SlayTheRequiem.SlayTheRequiemCode.Pantheon;
using SlayTheRequiem.SlayTheRequiemCode.Powers;
using SlayTheRequiem.SlayTheRequiemCode.Relics;
using SlayTheRequiem.SlayTheRequiemCode.Rules;
using SlayTheRequiem.SlayTheRequiemCode.Spells;
using VanillaPoison = MegaCrit.Sts2.Core.Models.Powers.PoisonPower;
using VanillaWeak = MegaCrit.Sts2.Core.Models.Powers.WeakPower;
using VanillaStrength = MegaCrit.Sts2.Core.Models.Powers.StrengthPower;

namespace SlayTheRequiem.SlayTheRequiemCode.Transformations;

/// <summary>
/// Ступень цепочки Тёмный договор → Вестник → Лич (docs/design/06 §1.4). Каждая ступень включает плату Договора:
/// +4 пул ✦, некромантия (заклинания призыва) −1 ✦, −8 макс. ♥. Следующая ступень заменяет предыдущую.
/// </summary>
public abstract class PactStage : FormRelic, ITransformationForm
{
    public bool IsActiveForm => true;
    public override Color FlashColor => new(0.35f, 0.9f, 0.45f);

    protected virtual Boon StageBoon => Boon.None;

    protected sealed override Boon ComputeBoon() => new Boon
    {
        ManaPool = TransformationRules.PactManaPool,
        MaxHp = -TransformationRules.PactMaxHpLoss,
    } + StageBoon;

    /// <summary>Некромантия — заклинания призыва (SummonSpell&lt;T&gt;).</summary>
    public static bool IsNecromancy(CardModel card)
    {
        for (var t = card.GetType(); t != null; t = t.BaseType)
            if (t.IsGenericType && t.GetGenericTypeDefinition() == typeof(SummonSpell<>)) return true;
        return false;
    }

    public override bool TryModifyStarCost(CardModel card, decimal originalCost, out decimal modifiedCost)
    {
        modifiedCost = originalCost;
        if (!Owned || card.Owner != Owner || originalCost <= 0 || !IsNecromancy(card) || !RequiemMode.IsInSystem(Owner)) return false;
        modifiedCost = Math.Max(0, originalCost - TransformationRules.PactNecromancyDiscount);
        return true;
    }
}

/// <summary>Тёмный договор (событие «Шёпот из урны», акт 1–2). Элита «Хранитель Гробницы» с Договором — Вестник.</summary>
[Pool(typeof(SharedRelicPool))]
public sealed class DarkPactForm : PactStage
{
    public override FormKind Kind => FormKind.DarkPact;
    public override Color? Tint => new Color(0.88f, 0.95f, 0.88f);

    private int _eliteVictories;
    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public int EliteVictories { get => _eliteVictories; set { AssertMutable(); _eliteVictories = Math.Max(0, value); } }

    /// <summary>The themed elite is an early shortcut, not a mandatory random encounter/door.
    /// Two elite wins after accepting the pact work on either branch and on short 1→3→5.</summary>
    public override async Task AfterCombatVictory(CombatRoom room)
    {
        if (!Owned) return;
        var tombGuardian = room.Encounter is Monsters.TombGuardianElite;
        if (room.RoomType == RoomType.Elite || tombGuardian) EliteVictories++;
        if (!BuildAccessRules.HeraldDue(tombGuardian, EliteVictories)) return;
        Flash();
        await Forms.Grant(Owner, FormKind.Herald);
    }
}

/// <summary>
/// Вестник (элита «Хранитель Гробницы»): карта-переключатель «Стойка» — Чума (заклинания накладывают Хворь 2),
/// Раздор (−1 Сила), Распад (Раскол 1). Победы в форме Вестника копятся — 10 открывают Лича.
/// </summary>
[Pool(typeof(SharedRelicPool))]
public sealed class HeraldForm : PactStage
{
    public override FormKind Kind => FormKind.Herald;
    protected override IEnumerable<Type> FormCards => [typeof(HeraldStanceCard)];
    public override Color? Tint => new Color(0.8f, 0.95f, 0.8f);
    public override float? Hue => 0.3f;

    public override bool ShowCounter => true;
    public override int DisplayAmount => Owned ? _wins : 0;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
        [new DynamicVar("Wins", 0), new DynamicVar("Required", TransformationRules.LichWinsRequired)];

    public int RequiredWins => BuildAccessRules.LichWinsRequired(BuildAccessRules.IsShortRoute(
        Config.RunSettings.FiveActs, Config.RunSettings.ShortRun, Owner.RunState.Acts.Count));

    private int _wins;
    private int _stance;

    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public int Wins
    {
        get => _wins;
        set
        {
            AssertMutable();
            _wins = Math.Max(0, value);
            if (DynamicVars.TryGetValue("Wins", out var v)) v.BaseValue = _wins;
            if (Owner != null && DynamicVars.TryGetValue("Required", out var needed)) needed.BaseValue = RequiredWins;
            InvokeDisplayAmountChanged();
        }
    }

    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public int StanceIndex
    {
        get => _stance;
        set
        {
            AssertMutable();
            _stance = Math.Clamp(value, 0, 2);
        }
    }

    public HeraldStance Stance => (HeraldStance)_stance;

    public void NextStance()
    {
        StanceIndex = (int)TransformationRules.NextStance(Stance);
        Flash();
    }

    public override async Task AfterCardPlayed(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        if (!Owned || cardPlay.Card is not SpellCard || cardPlay.Card.Owner != Owner || !cardPlay.IsLastInSeries) return;
        var combat = Owner.Creature.CombatState;
        if (combat == null) return;
        var targets = cardPlay.Target is { IsAlive: true, IsPlayer: false } t
            ? [t]
            : combat.HittableEnemies.Where(e => e.IsAlive).ToList();
        foreach (var enemy in targets)
        {
            switch (Stance)
            {
                case HeraldStance.Plague when !EnemySetup.IsBlightImmune(enemy):
                    await PowerCmd.Apply<BlightPower>(choiceContext, enemy, TransformationRules.HeraldBlight, Owner.Creature, null);
                    break;
                case HeraldStance.Discord:
                    await PowerCmd.Apply<VanillaStrength>(choiceContext, enemy, -TransformationRules.HeraldWeaken, Owner.Creature, null);
                    break;
                case HeraldStance.Decay:
                    await PowerCmd.Apply<SunderPower>(choiceContext, enemy, TransformationRules.HeraldSunder, Owner.Creature, null);
                    break;
            }
        }
    }

    public override Task AfterCombatVictory(CombatRoom room)
    {
        if (Owned) Wins = _wins + 1;
        return Task.CompletedTask;
    }

    protected override Task OnFormGained()
    {
        DynamicVars["Required"].BaseValue = RequiredWins;
        return Task.CompletedTask;
    }

    public override async Task AfterRoomEntered(AbstractRoom room)
    {
        await base.AfterRoomEntered(room);
        DynamicVars["Required"].BaseValue = RequiredWins;
    }

    public bool CanBecomeLich => Kind == FormKind.Herald && _wins >= RequiredWins;
}

/// <summary>Стойка Вестника: 0⚡ — сменить Стойку (Чума → Раздор → Распад).</summary>
[Pool(typeof(TokenCardPool))]
public sealed class HeraldStanceCard : RequiemCard
{
    /// <summary>Карта облика: растёт ступенями формы (06), а не улучшением — решение card spec §3.4 п. 3, док 18.</summary>
    public override int MaxUpgradeLevel => 0;

    public HeraldStanceCard() : base(0, CardType.Skill, CardRarity.Event, TargetType.Self)
    {
    }

    protected override Task OnPlay(PlayerChoiceContext ctx, CardPlay play)
    {
        Forms.Of<HeraldForm>(Owner)?.NextStance();
        return Task.CompletedTask;
    }
}

/// <summary>
/// Лич (событие «Филактерия», акт 4–5; 10 побед Вестником). ♥ — «кость»: макс. ♥ −30 %, физический урон по вам −25 %,
/// иммунитет к Яду/Хвори/Кровотечению; лечение — только поглощением/вампиризмом. Взрыв смерти: гибель призыва —
/// 5 урона магией всем врагам. Вместе с формой — Филактерия. Нежить Лича (M6 п. 2): в начале каждого боя встаёт Костяной
/// прислужник (<see cref="BoneThrallPower"/>, 10 ♥, бьёт 3 магией) — первый, чья гибель даст Взрыв смерти.
/// </summary>
[Pool(typeof(SharedRelicPool))]
public sealed class LichForm : PactStage, ISummonListener
{
    public override FormKind Kind => FormKind.Lich;
    public override Color? Tint => new Color(0.75f, 0.85f, 1f);
    public override float? Hue => 0.55f;
    public override Color FlashColor => new(0.5f, 0.75f, 1f);

    private int _boneLoss;

    /// <summary>Сколько ♥ «окостенело» при превращении (фиксируется один раз).</summary>
    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public int BoneLoss
    {
        get => _boneLoss;
        set
        {
            AssertMutable();
            _boneLoss = Math.Max(0, value);
        }
    }

    protected override Boon StageBoon => new() { MaxHp = -_boneLoss };

    protected override async Task OnFormGained()
    {
        BoneLoss = TransformationRules.LichMaxHpLoss(Owner.Creature.MaxHp);
        await SyncMaxHp();
        if (!Owner.Relics.OfType<Phylactery>().Any())
            await RelicCmd.Obtain(Forms.Create(typeof(Phylactery)), Owner);
    }

    public override bool BlocksHealing(HealSource source) => source != HealSource.Feeding;

    public override bool ImmuneTo(PowerModel canonicalPower) =>
        canonicalPower is VanillaPoison or WeaponPoisonPower or BlightPower or BleedPower;

    public override decimal ModifyDamageMultiplicative(Creature? target, decimal amount, ValueProp props, Creature? dealer, CardModel? cardSource)
    {
        if (!Owned || target != Owner.Creature || amount <= 0) return 1m;
        return DamageTyping.Resolve(props, dealer, cardSource) == DamageType.Physical
            ? TransformationRules.LichPhysicalTakenMultiplier
            : 1m;
    }

    public override async Task AfterPlayerTurnStart(PlayerChoiceContext choiceContext, Player player)
    {
        if (!Owned || player != Owner || Owner.Creature.CombatState is not { RoundNumber: 1 }) return;
        Flash();
        await SummonPower.Summon<BoneThrallPower>(choiceContext, Owner.Creature, SummonRules.BoneThrallHp, null);
    }

    public async Task AfterSummonDestroyed(SummonPower summon)
    {
        var combat = Owner.Creature.CombatState;
        if (!Owned || combat == null) return;
        Flash();
        foreach (var enemy in combat.HittableEnemies.Where(e => e.IsAlive).ToList())
            using (DamageTyping.As(DamageType.Magic))
                await CreatureCmd.Damage(new ThrowingPlayerChoiceContext(), enemy, TransformationRules.LichDeathBlast, ValueProp.Unpowered, Owner.Creature, null);
    }
}

/// <summary>Филактерия: 3 веса Груза. При смерти — воскрешение с 30 % ♥ один раз за забег, затем разрушается.</summary>
[Pool(typeof(SharedRelicPool))]
public sealed class Phylactery : RequiemRelic, Equipment.IWeighted
{
    public override RelicRarity Rarity => RelicRarity.Ancient;
    public override bool IsAllowedInShops => false;
    public override bool IsAllowed(MegaCrit.Sts2.Core.Runs.IRunState runState) => false;
    public int Weight => TransformationRules.PhylacteryWeight;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
        [new DynamicVar("Percent", TransformationRules.PhylacteryRevivePercent)];

    private bool _used;

    public override bool IsUsedUp => _used;

    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public bool Used
    {
        get => _used;
        set
        {
            AssertMutable();
            _used = value;
            if (_used) Status = RelicStatus.Disabled;
        }
    }

    public override bool ShouldDieLate(Creature creature) =>
        !IsMutable || Owner == null || creature != Owner.Creature || _used;

    public override async Task AfterPreventingDeath(Creature creature)
    {
        Flash();
        Used = true;
        using (Healing.As(HealSource.Revival))
            await CreatureCmd.Heal(creature, TransformationRules.PhylacteryReviveHp(creature.MaxHp));
        var owner = Owner;
        await RelicCmd.Remove(this);
        FormVisuals.Refresh(owner, flash: new Color(0.5f, 0.75f, 1f));
    }
}
