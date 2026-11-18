using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;
using SlayTheRequiem.SlayTheRequiemCode.Core;
using SlayTheRequiem.SlayTheRequiemCode.Equipment;
using SlayTheRequiem.SlayTheRequiemCode.Rules;

namespace SlayTheRequiem.SlayTheRequiemCode.Powers;

// Силы этапа 7: механики элит и супербоссов (docs/design/11 §5–6) и правила актов (13 §3).

/// <summary>Чистые правила сил этапа 7 (зеркало в tools/sim).</summary>
public static class ActPowerRules
{
    /// <summary>Призрачность (docs/design/03): физический урон −75 %.</summary>
    public const int GhostlyReduction = 75;
    /// <summary>Зеркальный Страж: отражает 50 % урона заклинаний, физический урон по нему ×1.5.</summary>
    public const int MirrorReflectPercent = 50;
    public const decimal MirrorPhysicalMultiplier = 1.5m;
    /// <summary>Полёт: физические атаки ближнего боя по нему −50 %.</summary>
    public const decimal FlightMultiplier = BossCounterplayRules.AerialMeleeMultiplier;
    /// <summary>Слово подчинения: −I сопротивления магии за стак, максимум −III. Запрос — ResistancePower.</summary>
    public const int BindingMaxStacks = BossCounterplayRules.BindingMaxStacks;

    /// <summary>Призрачность не замечает: заклинаний, нефизического урона, серебряного оружия.</summary>
    public static bool BypassesGhostly(bool isSpell, DamageType type, bool silver) => isSpell || type != DamageType.Physical || silver;
}

/// <summary>Призрачность: физический урон не от заклинаний по носителю −75 %, серебро и заклинания — полностью.</summary>
public sealed class GhostlyPower : RequiemPower
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Single;

    public override decimal ModifyDamageMultiplicative(Creature? target, decimal amount, ValueProp props, Creature? dealer, CardModel? cardSource)
    {
        if (target != Owner || dealer == null || dealer == Owner) return 1m;
        var silver = dealer.Player?.Relics.OfType<SilverSaber>().Any() == true;
        return ActPowerRules.BypassesGhostly(MagicDamage.IsSpell(props, dealer, cardSource), DamageTyping.Resolve(props, dealer, cardSource), silver)
            ? 1m
            : (100 - ActPowerRules.GhostlyReduction) / 100m;
    }
}

/// <summary>Неуязвимость (кокон Владыки, щит Чемпиона, Тень Без Имени): урон по носителю — 0, пока сила стоит.</summary>
public sealed class InvulnerablePower : RequiemPower
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Single;

    public override decimal ModifyDamageMultiplicative(Creature? target, decimal amount, ValueProp props, Creature? dealer, CardModel? cardSource) =>
        target == Owner && dealer != Owner ? 0m : 1m;
}

/// <summary>Зеркало заклинаний: урон заклинаниями по носителю вдвое, половина возвращается заклинателю; физический ×1.5.</summary>
public sealed class SpellMirrorPower : RequiemPower
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Single;

    public override decimal ModifyDamageMultiplicative(Creature? target, decimal amount, ValueProp props, Creature? dealer, CardModel? cardSource)
    {
        if (target != Owner || dealer == null || dealer == Owner) return 1m;
        if (MagicDamage.IsSpell(props, dealer, cardSource)) return (100 - ActPowerRules.MirrorReflectPercent) / 100m;
        return DamageTyping.Resolve(props, dealer, cardSource) == DamageType.Physical ? ActPowerRules.MirrorPhysicalMultiplier : 1m;
    }

    public override async Task AfterDamageReceived(PlayerChoiceContext choiceContext, Creature target, DamageResult result, ValueProp props, Creature? dealer, CardModel? cardSource)
    {
        if (target != Owner || dealer is not { IsPlayer: true, IsAlive: true } || !MagicDamage.IsSpell(props, dealer, cardSource)) return;
        // Отражённая половина: столько же, сколько прошло по Стражу.
        var reflected = result.TotalDamage;
        if (reflected <= 0) return;
        Flash();
        using var _ = DamageTyping.As(DamageType.Magic);
        await CreatureCmd.Damage(choiceContext, dealer, reflected, ValueProp.Unpowered, Owner, null);
    }
}

/// <summary>Окаменение (Гаргулья днём): Латы растут, но носитель не атакует (машина ходов выбирает «Камень»); Свет снимает.</summary>
public sealed class PetrifiedPower : RequiemPower
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Single;

    public override async Task AfterDamageReceived(PlayerChoiceContext choiceContext, Creature target, DamageResult result, ValueProp props, Creature? dealer, CardModel? cardSource)
    {
        if (target == Owner && DamageTyping.Resolve(props, dealer, cardSource) == DamageType.Light) await PowerCmd.Remove(this);
    }
}

/// <summary>Полёт: физические атаки ближнего боя по носителю −50 %. Длительность/приземление управляются конкретным боссом.</summary>
public sealed class FlightPower : RequiemPower
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Single;

    public override decimal ModifyDamageMultiplicative(Creature? target, decimal amount, ValueProp props, Creature? dealer, CardModel? cardSource)
    {
        if (target != Owner || dealer is not { IsPlayer: true }) return 1m;
        var ranged = cardSource != null && EquipmentCombatRules.IsWeaponAttack(cardSource)
            && Progression.HeroRecord.IsRanged(Progression.HeroRecord.Of(dealer.Player)?.MainHand ?? WeaponKind.None);
        return BossCounterplayRules.FlightMultiplier(props.IsPoweredAttack(), MagicDamage.IsSpell(props, dealer, cardSource),
            ranged, DamageTyping.Resolve(props, dealer, cardSource));
    }
}

/// <summary>Туман Владыки: удары промахиваются, кроме Света и площадных заклинаний. −1 в конце хода героев.</summary>
public sealed class MistFormPower : RequiemPower
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    public override decimal ModifyDamageMultiplicative(Creature? target, decimal amount, ValueProp props, Creature? dealer, CardModel? cardSource)
    {
        if (target != Owner || dealer is not { IsPlayer: true }) return 1m;
        return BossCounterplayRules.MistMultiplier(props.IsCardOrMonsterMove(), MagicDamage.IsSpell(props, dealer, cardSource),
            cardSource?.TargetType == TargetType.AllEnemies, DamageTyping.Resolve(props, dealer, cardSource));
    }

    public override async Task AfterSideTurnEnd(PlayerChoiceContext choiceContext, CombatSide side, IEnumerable<Creature> participants)
    {
        if (side == CombatSide.Player) await PowerCmd.Decrement(this);
    }
}

/// <summary>Слово подчинения N: сопротивление магии героя −N (максимум 3), без вычитания постоянной силы.
/// Величину ограничивает глобальный TryModifyPowerAmountReceived; численный запрос — ResistancePower.TierOf.</summary>
public sealed class WordOfBindingPower : RequiemPower
{
    public override PowerType Type => PowerType.Debuff;
    public override PowerStackType StackType => PowerStackType.Counter;

}

/// <summary>Проклятый клич (Жнец Курганов): Латы героя −N до конца боя (складывается).</summary>
public sealed class CursedCryPower : RequiemPower
{
    public override PowerType Type => PowerType.Debuff;
    public override PowerStackType StackType => PowerStackType.Counter;
}

/// <summary>Соли душ (Чёрный Странник): заряды призыва призраков; Подлый удар крадёт заряд.</summary>
public sealed class SoulSaltsPower : RequiemPower
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    public override async Task AfterDamageReceived(PlayerChoiceContext choiceContext, Creature target, DamageResult result, ValueProp props, Creature? dealer, CardModel? cardSource)
    {
        if (target != Owner || dealer is not { IsPlayer: true } || result.UnblockedDamage <= 0) return;
        if (!SneakRules.IsSneak(dealer, target)) return;
        Flash();
        await PowerCmd.Decrement(this);
    }
}

/// <summary>Перегрев (Заводные Глубины, 13 §3): маркер на героях — сколько ходов до парового выброса.</summary>
public sealed class OverheatPower : RequiemPower
{
    public override PowerType Type => PowerType.Debuff;
    public override PowerStackType StackType => PowerStackType.Counter;
}

/// <summary>Стужа (Ледяные Курганы): маркер боя — в начале каждого боя Озноб; «Отогреться» снимает на 2 боя.</summary>
public sealed class FrostbiteAuraPower : RequiemPower
{
    public override PowerType Type => PowerType.Debuff;
    public override PowerStackType StackType => PowerStackType.Single;
}

/// <summary>Лей-линия (Звёздная Башня): маркер боя на лей-линии — +3 ✦ всем в начале боя.</summary>
public sealed class LeyLinePower : RequiemPower
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Single;
}

/// <summary>Порождение Бездны из Врат: +1 реликвия/Камень душ за бой — маркер на враге.</summary>
public sealed class AbyssSpawnPower : RequiemPower
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Single;
}
