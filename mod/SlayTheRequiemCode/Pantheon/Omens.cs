using BaseLib.Utils;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Commands.Builders;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.RelicPools;
using MegaCrit.Sts2.Core.ValueProps;
using SlayTheRequiem.SlayTheRequiemCode.Core;
using SlayTheRequiem.SlayTheRequiemCode.Powers;
using SlayTheRequiem.SlayTheRequiemCode.Rules;
using SlayTheRequiem.SlayTheRequiemCode.Shops;
using SlayTheRequiem.SlayTheRequiemCode.Spells;
using VanillaPoison = MegaCrit.Sts2.Core.Models.Powers.PoisonPower;

namespace SlayTheRequiem.SlayTheRequiemCode.Pantheon;

/// <summary>
/// Знамение (Камень-Хранитель, docs/design/05 §1): выбирается у Нео — одно на забег, сменить нельзя.
/// Числа — <see cref="OmenRules.OmenBoon"/>, особые свойства — у наследников.
/// </summary>
public abstract class OmenRelic : BoonRelic
{
    public abstract Omen Kind { get; }
    protected override Boon ComputeBoon() => OmenRules.OmenBoon(Kind);

    public static OmenRelic? Of(Player? player) => player?.Relics.OfType<OmenRelic>().FirstOrDefault();

    public static readonly Type[] All =
    [
        typeof(OmenOfTheBlade), typeof(OmenOfTheBastion), typeof(OmenOfTheEye), typeof(OmenOfTheSponge),
        typeof(OmenOfTheRite), typeof(OmenOfTheBurden), typeof(OmenOfTheTrickster), typeof(OmenOfTheVoice),
        typeof(OmenOfTheShade), typeof(OmenOfTheSerpent),
    ];

    public static RelicModel Create(Omen omen) => ModelDb.GetById<RelicModel>(ModelDb.GetId(All[(int)omen])).ToMutable();
}

/// <summary>Знамение Клинка: +6 ♥; атаки +1; +1 прирост ◆; «Устал» не снижает урон.</summary>
[Pool(typeof(SharedRelicPool))]
public sealed class OmenOfTheBlade : OmenRelic, ITirelessSource
{
    public override Omen Kind => Omen.Blade;
    public bool IgnoresWeariness => true;
}

/// <summary>Знамение Бастиона: Латы +1; сопротивление I огню/холоду/разряду; Стойкость +3; первое Оглушение за бой — мимо.</summary>
[Pool(typeof(SharedRelicPool))]
public sealed class OmenOfTheBastion : OmenRelic
{
    public override Omen Kind => Omen.Bastion;

    private bool _stunIgnored;

    protected override Task OnCombatStarted(ICombatState combatState)
    {
        _stunIgnored = false;
        return Task.CompletedTask;
    }

    public override async Task AfterPowerAmountChanged(PlayerChoiceContext choiceContext, PowerModel power, decimal amount, Creature? applier, CardModel? cardSource)
    {
        if (_stunIgnored || power is not ConcussedPower || power.Owner != Owner.Creature || amount <= 0) return;
        _stunIgnored = true;
        Flash();
        await PowerCmd.Remove(power);
    }
}

/// <summary>Знамение Ока: +2 пул ✦, +1 прирост ✦, +1 ко всем школам; враги — уязвимость к магии I.</summary>
[Pool(typeof(SharedRelicPool))]
public sealed class OmenOfTheEye : OmenRelic
{
    public override Omen Kind => Omen.Eye;
}

/// <summary>Знамение Губки: +6 пул ✦; Поглощение 1; Покров 2. Минус: прирост ✦ −1.</summary>
[Pool(typeof(SharedRelicPool))]
public sealed class OmenOfTheSponge : OmenRelic
{
    public override Omen Kind => Omen.Sponge;
}

/// <summary>Знамение Обряда: +1 слот призыва; заклинания Колдовства −1 ✦. Некро-ауры — с некромантией этапа 6.</summary>
[Pool(typeof(SharedRelicPool))]
public sealed class OmenOfTheRite : OmenRelic
{
    public override Omen Kind => Omen.Rite;

    public override bool TryModifyStarCost(CardModel card, decimal originalCost, out decimal modifiedCost)
    {
        modifiedCost = originalCost;
        if (card.Owner != Owner || card is not SpellCard { School: SpellSchool.Conjuration } || originalCost <= 0) return false;
        modifiedCost = originalCost - 1;
        return true;
    }
}

/// <summary>Знамение Ноши: +10 груза; иммунитет к Обледенению/Параличу/Шоку; атаки стоят не больше 1 ◆.</summary>
[Pool(typeof(SharedRelicPool))]
public sealed class OmenOfTheBurden : OmenRelic, IAttackStaminaModifier
{
    public override Omen Kind => Omen.Burden;
    public int ModifyAttackStamina(CardModel card, int cost) => Math.Min(cost, 1);
}

/// <summary>Знамение Ловкача: Кража в лавках (раз за визит, −праведность); +15 % золота.</summary>
[Pool(typeof(SharedRelicPool))]
public sealed class OmenOfTheTrickster : OmenRelic, ITheftEnabler
{
    public override Omen Kind => Omen.Trickster;
}

/// <summary>Знамение Гласа: Кличи −1 перезарядка, +3 урона; карты в лавках на ступень редкости выше.</summary>
[Pool(typeof(SharedRelicPool))]
public sealed class OmenOfTheVoice : OmenRelic
{
    public override Omen Kind => Omen.Voice;

    public override CardRarity ModifyMerchantCardRarity(Player player, CardRarity rarity)
    {
        if (player != Owner) return rarity;
        return rarity switch
        {
            CardRarity.Common => CardRarity.Uncommon,
            CardRarity.Uncommon => CardRarity.Rare,
            _ => rarity,
        };
    }
}

/// <summary>
/// Знамение Тени: первый ход каждого боя — Невидимость; первая атака врага по вам в Невидимости промахивается;
/// ваши атаки из Невидимости ×2.
/// </summary>
[Pool(typeof(SharedRelicPool))]
public sealed class OmenOfTheShade : OmenRelic
{
    public override Omen Kind => Omen.Shade;

    private bool _evaded;

    public override async Task BeforeCombatStart()
    {
        await base.BeforeCombatStart();
        _evaded = false;
        // 2: спадает на 1 в начале каждого хода героя — держится весь первый ход и ход врагов после него.
        await PowerCmd.Apply<InvisibilityPower>(new ThrowingPlayerChoiceContext(), Owner.Creature, 2, Owner.Creature, null, silent: true);
    }

    public override decimal ModifyDamageMultiplicative(Creature? target, decimal amount, ValueProp props, Creature? dealer, CardModel? cardSource)
    {
        if (!props.IsPoweredAttack() || Owner?.Creature == null || !Owner.Creature.HasPower<InvisibilityPower>()) return 1m;
        if (dealer == Owner.Creature && cardSource?.Owner == Owner) return 2m;
        if (target == Owner.Creature && dealer != null && dealer != Owner.Creature && !_evaded) return 0m;
        return 1m;
    }

    public override Task AfterAttack(PlayerChoiceContext choiceContext, AttackCommand command)
    {
        if (!_evaded && command.Attacker != Owner.Creature && Owner.Creature.HasPower<InvisibilityPower>()
            && command.Results.SelectMany(h => h).Any(r => r.Receiver == Owner.Creature))
        {
            _evaded = true;
            Flash();
        }
        return base.AfterAttack(choiceContext, command);
    }
}

/// <summary>Знамение Змея: накладываемый Яд +1; сопротивление ядам II; враги −I к ядам; заряды ядов на оружии до 5.</summary>
[Pool(typeof(SharedRelicPool))]
public sealed class OmenOfTheSerpent : OmenRelic, IPoisonChargeSource
{
    public override Omen Kind => Omen.Serpent;
    public int ExtraPoisonCharges => OmenRules.SerpentExtraCharges;

    public override decimal ModifyPowerAmountGivenAdditive(PowerModel power, Creature giver, decimal amount, Creature? target, CardModel? cardSource) =>
        power is VanillaPoison && giver == Owner?.Creature && amount > 0 && target != Owner.Creature ? 1m : 0m;
}
