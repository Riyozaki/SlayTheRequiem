using BaseLib.Utils;
using Godot;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.CardPools;
using WeakPower = MegaCrit.Sts2.Core.Models.Powers.WeakPower;
using MegaCrit.Sts2.Core.Models.RelicPools;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Saves.Runs;
using MegaCrit.Sts2.Core.ValueProps;
using SlayTheRequiem.SlayTheRequiemCode.Alchemy;
using SlayTheRequiem.SlayTheRequiemCode.Cards;
using SlayTheRequiem.SlayTheRequiemCode.Core;
using SlayTheRequiem.SlayTheRequiemCode.Pantheon;
using SlayTheRequiem.SlayTheRequiemCode.Powers;
using SlayTheRequiem.SlayTheRequiemCode.Progression;
using SlayTheRequiem.SlayTheRequiemCode.Rules;

namespace SlayTheRequiem.SlayTheRequiemCode.Transformations;

/// <summary>
/// Носитель Зверя — оборотень (docs/design/06 §1.3, путь Ловчего). Особая карта «Зов луны» — Обращение на 3 хода
/// (<see cref="BeastFormPower"/>). Минус: вне формы Регенерация недоступна, отдых лечит на 25 % меньше («зверь не спит»).
/// Ветки по 3 узла — Охотник (урон, Кровотечение), Ловчий (ходы формы), Альфа (волк при Обращении): каждые
/// <see cref="TransformationRules.TransformationsPerNode"/> Обращения в выигранных боях — узел, выбирается в лагере
/// («Зов крови», бесплатно). Пока форма есть — Отступник.
/// </summary>
[Pool(typeof(SharedRelicPool))]
public sealed class WerewolfForm : FormRelic, ITransformationForm
{
    public override FormKind Kind => FormKind.Werewolf;
    public bool IsActiveForm => true;
    protected override IEnumerable<Type> FormCards => [typeof(MoonCall)];
    public override float? Hue => 0.03f;
    public override Color FlashColor => new(0.55f, 0.6f, 0.95f);

    public override bool ShowCounter => true;
    public override int DisplayAmount => Owned ? _hunter + _stalker + _alpha : 0;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DynamicVar("Hunter", 0), new DynamicVar("Stalker", 0), new DynamicVar("Alpha", 0), new DynamicVar("Pending", 0),
    ];

    private int _transformations, _hunter, _stalker, _alpha;

    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public int Transformations { get => _transformations; set { AssertMutable(); _transformations = Math.Max(0, value); SyncVars(); } }

    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public int Hunter { get => _hunter; set { AssertMutable(); _hunter = Math.Clamp(value, 0, TransformationRules.NodesPerBranch); SyncVars(); } }

    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public int Stalker { get => _stalker; set { AssertMutable(); _stalker = Math.Clamp(value, 0, TransformationRules.NodesPerBranch); SyncVars(); } }

    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public int Alpha { get => _alpha; set { AssertMutable(); _alpha = Math.Clamp(value, 0, TransformationRules.NodesPerBranch); SyncVars(); } }

    public int Nodes(BeastBranch branch) => branch switch
    {
        BeastBranch.Hunter => _hunter,
        BeastBranch.Stalker => _stalker,
        _ => _alpha,
    };

    public int PendingNodes => Math.Max(0, TransformationRules.BeastNodesEarned(_transformations) - (_hunter + _stalker + _alpha));

    public void Learn(BeastBranch branch)
    {
        switch (branch)
        {
            case BeastBranch.Hunter: Hunter = _hunter + 1; break;
            case BeastBranch.Stalker: Stalker = _stalker + 1; break;
            default: Alpha = _alpha + 1; break;
        }
        Flash();
    }

    private void SyncVars()
    {
        if (DynamicVars.TryGetValue("Hunter", out var h)) h.BaseValue = _hunter;
        if (DynamicVars.TryGetValue("Stalker", out var s)) s.BaseValue = _stalker;
        if (DynamicVars.TryGetValue("Alpha", out var a)) a.BaseValue = _alpha;
        if (DynamicVars.TryGetValue("Pending", out var p)) p.BaseValue = PendingNodes;
        InvokeDisplayAmountChanged();
    }

    public bool InBeastForm => Owned && Owner.Creature.HasPower<BeastFormPower>();

    protected override Boon ComputeBoon() => new()
    {
        RestHealPercent = TransformationRules.BeastRestHealPercent,
        SummonSlots = TransformationRules.AlphaSummonSlots(_alpha),
    };

    /// <summary>Вне формы Регенерация не действует.</summary>
    public override bool BlocksHealing(HealSource source) => source == HealSource.Regeneration && !InBeastForm;

    private bool _transformedThisCombat;

    public override Task BeforeCombatStart()
    {
        _transformedThisCombat = false;
        return base.BeforeCombatStart();
    }

    public void NoteTransformation() => _transformedThisCombat = true;

    public override Task AfterCombatVictory(CombatRoom room)
    {
        if (Owned && _transformedThisCombat) Transformations = _transformations + 1;
        return Task.CompletedTask;
    }
}

/// <summary>Звериная карта: играется только в форме зверя (и только они — в форме).</summary>
public interface IBeastCard { }

/// <summary>Зов луны: 2⚡, Изгнание — Обращение на 3 хода (Ловчий — дольше).</summary>
[Pool(typeof(TokenCardPool))]
public sealed class MoonCall : RequiemCard
{
    /// <summary>Карта облика: длительность растёт узлами Ловчего (06), а не улучшением — card spec §3.4 п. 3, док 18.</summary>
    public override int MaxUpgradeLevel => 0;

    public MoonCall() : base(TransformationRules.MoonCallEnergy, CardType.Skill, CardRarity.Event, TargetType.Self)
    {
        WithKeyword(CardKeyword.Exhaust);
        WithVar("Turns", TransformationRules.MoonCallTurns);
    }

    protected override Task OnPlay(PlayerChoiceContext ctx, CardPlay play)
    {
        var stalker = Forms.Of<WerewolfForm>(Owner)?.Stalker ?? 0;
        return Apply<BeastFormPower>(ctx, Owner.Creature, TransformationRules.MoonCallDuration(stalker));
    }
}

/// <summary>
/// Обращение (06 §1.3): N — ходов формы. На входе рука сбрасывается, +15 Блока, (Альфа) волк; каждый ход вместо добора —
/// «Звериная» рука (Когти ×2, Прыжок, Рык, Пожрать — если есть труп). Атаки +4 (Охотник — больше), чужие карты не играются,
/// заклинания и снаряжение рук не работают. Каждые 3 удара по врагам (Ловчий II — 2) — +1 ход формы.
/// </summary>
public sealed class BeastFormPower : RequiemPower
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    private bool _entered;
    private int _hits;
    private int _corpses;

    public int Corpses => _corpses;

    private WerewolfForm? Form => Forms.Of<WerewolfForm>(Owner.Player);

    public override async Task AfterPowerAmountChanged(PlayerChoiceContext choiceContext, PowerModel power, decimal amount, Creature? applier, CardModel? cardSource)
    {
        if (power != this || amount <= 0 || _entered || Owner.Player == null) return;
        _entered = true;
        var player = Owner.Player;
        Form?.NoteTransformation();
        FormVisuals.Refresh(player, flash: new Color(0.55f, 0.6f, 0.95f));
        // Phase answers must remain reachable when entering Beast form. They are not the ordinary deck,
        // and the boss grants them without Draw, so suppressing Draw cannot softlock a cocoon/meteor answer.
        var hand = PileType.Hand.GetPile(player).Cards.Where(c => c is not EncounterCounterCard).ToList();
        if (hand.Count > 0) await CardCmd.Discard(choiceContext, hand);
        await CreatureCmd.GainBlock(Owner, TransformationRules.BeastEntryBlock, ValueProp.Move, null);
        var wolfHp = TransformationRules.AlphaWolfHp(Form?.Alpha ?? 0);
        if (wolfHp > 0) await SummonPower.Summon<SpiritWolfPower>(choiceContext, Owner, wolfHp, null);
        await FillBeastHand(player);
    }

    private async Task FillBeastHand(Player player)
    {
        var combat = Owner.CombatState;
        if (combat == null) return;
        var cards = new List<CardModel>
        {
            combat.CreateCard<Claws>(player), combat.CreateCard<Claws>(player),
            combat.CreateCard<Pounce>(player), combat.CreateCard<Roar>(player),
        };
        if (_corpses > 0) cards.Add(combat.CreateCard<Devour>(player));
        foreach (var card in cards) await CardPileCmd.AddGeneratedCardToCombat(card, PileType.Hand, player);
    }

    public override async Task AfterPlayerTurnStart(PlayerChoiceContext choiceContext, Player player)
    {
        if (player.Creature != Owner || !_entered) return;
        await FillBeastHand(player);
    }

    /// <summary>В форме обычная колода не добирается.</summary>
    public override bool ShouldDraw(Player player, bool fromHandDraw) => player.Creature != Owner;

    /// <summary>В форме играются звериные карты и узкое исключение для контрмер текущего боя.</summary>
    public override bool ShouldPlay(CardModel card, AutoPlayType autoPlayType) =>
        card.Owner?.Creature != Owner || card is IBeastCard or Cards.EncounterCounterCard;

    public override decimal ModifyDamageAdditive(Creature? target, decimal amount, ValueProp props, Creature? dealer, CardModel? cardSource, CardPlay? cardPlay)
    {
        if (dealer != Owner || target == Owner || !props.IsPoweredAttack() || cardSource is not IBeastCard) return 0m;
        return TransformationRules.BeastAttackBonusFor(Form?.Hunter ?? 0);
    }

    public override async Task AfterDamageGiven(PlayerChoiceContext choiceContext, Creature? dealer, DamageResult result, ValueProp props, Creature target, CardModel? cardSource)
    {
        if (dealer != Owner || target == Owner || target.IsPlayer || !props.IsPoweredAttack()) return;
        var bleed = TransformationRules.BeastBleed(Form?.Hunter ?? 0);
        if (bleed > 0 && target.IsAlive && cardSource is IBeastCard)
            await PowerCmd.Apply<BleedPower>(choiceContext, target, bleed, Owner, null);
        var before = _hits++;
        var extra = TransformationRules.ExtensionTurns(before, _hits, TransformationRules.HitsPerExtensionFor(Form?.Stalker ?? 0));
        if (extra > 0)
        {
            Flash();
            await PowerCmd.ModifyAmount(choiceContext, this, extra, null, null, silent: true);
        }
    }

    public override Task AfterDeath(PlayerChoiceContext choiceContext, Creature creature, CardModel? source, bool wasRemovalPrevented, float deathAnimLength)
    {
        if (!wasRemovalPrevented && creature.Monster != null && creature != Owner) _corpses++;
        return Task.CompletedTask;
    }

    /// <summary>Пожрать: съесть труп.</summary>
    public async Task Devoured(PlayerChoiceContext ctx)
    {
        if (_corpses <= 0) return;
        _corpses--;
        await CreatureCmd.Heal(Owner, TransformationRules.DevourHeal);
        await PowerCmd.ModifyAmount(ctx, this, TransformationRules.DevourTurns, null, null, silent: true);
    }

    public override async Task AfterSideTurnEnd(PlayerChoiceContext choiceContext, CombatSide side, IEnumerable<Creature> participants)
    {
        if (!participants.Contains(Owner)) return;
        if (Amount <= 1)
        {
            var player = Owner.Player;
            await PowerCmd.Remove(this);
            FormVisuals.Refresh(player);
        }
        else await PowerCmd.ModifyAmount(choiceContext, this, -1, null, null, silent: true);
    }
}

// ───────────── «Звериная» колода (жетоны, исчезают в конце хода) ─────────────

public abstract class BeastCard(int cost, CardType type, TargetType target)
    : RequiemCard(cost, type, CardRarity.Token, target), IBeastCard
{
    public override int MaxUpgradeLevel => 0;

    protected void Init()
    {
        WithKeyword(CardKeyword.Ethereal);
        WithKeyword(CardKeyword.Exhaust);
    }
}

/// <summary>Когти: 1⚡ 1◆ — 6 урона.</summary>
[Pool(typeof(TokenCardPool))]
public sealed class Claws : BeastCard
{
    public Claws() : base(1, CardType.Attack, TargetType.AnyEnemy)
    {
        Init();
        WithDamage(6);
        WithStamina(1);
    }

    protected override Task OnPlay(PlayerChoiceContext ctx, CardPlay play)
    {
        ArgumentNullException.ThrowIfNull(play.Target);
        return Hit(ctx, play.Target);
    }
}

/// <summary>Прыжок: 1⚡ 2◆ — 9 урона и 5 Блока.</summary>
[Pool(typeof(TokenCardPool))]
public sealed class Pounce : BeastCard
{
    public Pounce() : base(1, CardType.Attack, TargetType.AnyEnemy)
    {
        Init();
        WithDamage(9);
        WithBlock(5);
        WithStamina(2);
    }

    protected override async Task OnPlay(PlayerChoiceContext ctx, CardPlay play)
    {
        ArgumentNullException.ThrowIfNull(play.Target);
        await Hit(ctx, play.Target);
        await Block(play);
    }
}

/// <summary>Рык: 1⚡ — Слабость 1 и Шаткость 3 всем врагам.</summary>
[Pool(typeof(TokenCardPool))]
public sealed class Roar : BeastCard
{
    public Roar() : base(1, CardType.Skill, TargetType.AllEnemies)
    {
        Init();
        WithPower<WeakPower>(1);
        WithPower<StaggerPower>(3);
    }

    protected override async Task OnPlay(PlayerChoiceContext ctx, CardPlay play)
    {
        await ApplyAll<WeakPower>(ctx, Var(nameof(WeakPower)));
        await ApplyAll<StaggerPower>(ctx, Var(nameof(StaggerPower)));
    }
}

/// <summary>Пожрать: 1⚡ — съесть труп врага: +6 ♥ и +1 ход формы.</summary>
[Pool(typeof(TokenCardPool))]
public sealed class Devour : BeastCard
{
    public Devour() : base(1, CardType.Skill, TargetType.Self)
    {
        Init();
        WithVar("Heal", TransformationRules.DevourHeal);
    }

    protected override bool IsPlayable => Owner?.Creature.GetPower<BeastFormPower>() is { Corpses: > 0 };

    protected override async Task OnPlay(PlayerChoiceContext ctx, CardPlay play)
    {
        if (Owner.Creature.GetPower<BeastFormPower>() is { } form) await form.Devoured(ctx);
    }
}

// ───────────── лагерь: «Зов крови» — выбор узла ветки ─────────────

/// <summary>Зов крови (лагерь, бесплатно): выбрать узел ветки Охотник/Ловчий/Альфа за накопленные Обращения.</summary>
public sealed class BeastRitesRestSiteOption(Player owner) : RequiemRestSiteOption(owner)
{
    public override string OptionId => "SLAYTHEREQUIEM_BEAST_RITES";
    public override string FallbackIcon => "LIFT";
    public override CampAction Action => CampAction.Train;

    private WerewolfForm? Form => Forms.Of<WerewolfForm>(Owner);

    protected override bool Available => Form is { PendingNodes: > 0 };

    protected override void Fill(LocString loc) => loc.Add("Pending", Form?.PendingNodes ?? 0);

    protected override async Task<bool> Perform()
    {
        var form = Form;
        if (form == null || form.PendingNodes <= 0) return false;
        var choices = Enum.GetValues<BeastBranch>()
            .Where(b => form.Nodes(b) < TransformationRules.NodesPerBranch)
            .Select(b => new CampMenu.Choice(b switch
            {
                BeastBranch.Hunter => typeof(PickBeastHunter),
                BeastBranch.Stalker => typeof(PickBeastStalker),
                _ => typeof(PickBeastAlpha),
            }, form.Nodes(b) + 1, 0, b))
            .ToList();
        if (await CampMenu.Pick(Owner, choices, "SLAYTHEREQUIEM_BEAST_PROMPT") is not BeastBranch branch) return false;
        form.Learn(branch);
        await form.SyncMaxHp();
        return true;
    }
}

[Pool(typeof(TokenCardPool))] public sealed class PickBeastHunter : PickCard { }
[Pool(typeof(TokenCardPool))] public sealed class PickBeastStalker : PickCard { }
[Pool(typeof(TokenCardPool))] public sealed class PickBeastAlpha : PickCard { }
