using BaseLib.Utils;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.RelicPools;
using MegaCrit.Sts2.Core.MonsterMoves.Intents;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Saves.Runs;
using MegaCrit.Sts2.Core.ValueProps;
using SlayTheRequiem.SlayTheRequiemCode.Core;
using SlayTheRequiem.SlayTheRequiemCode.Powers;
using SlayTheRequiem.SlayTheRequiemCode.Rules;
using SlayTheRequiem.SlayTheRequiemCode.Shops;
using SlayTheRequiem.SlayTheRequiemCode.Spells;
using SlayTheRequiem.SlayTheRequiemCode.Transformations;
using VanillaPoison = MegaCrit.Sts2.Core.Models.Powers.PoisonPower;

namespace SlayTheRequiem.SlayTheRequiemCode.Equipment;

// Бижутерия docs/design/07 §5 (M4 п. 3): кольца §5.2, амулеты §5.1 и скрытые сочетания колец §5.3.
// Числа — в Core/JewelryRules (покрыты тестами). Кольца/амулеты лёгкие (вес 0–1): слоты бижутерии — арена сочетаний.
// Кольцо Ярого Удара, Железной Кожи и Ожерелье Колючих Шипов — в Items.cs (пришли вместе с переработкой ванильных реликвий).

/// <summary>
/// Кольцо, участвующее в сочетании §5.3. Бонус пары работает, пока надеты оба кольца; подсказка о сочетании
/// появляется на кольцах только после первого совпадения (маска <see cref="CombosFound"/> сохраняется на кольце).
/// </summary>
public abstract class ComboRing : EquipmentRelic
{
    public override EquipSlot Slot => EquipSlot.Ring;

    private int _combosFound;

    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public int CombosFound
    {
        get => _combosFound;
        set
        {
            AssertMutable();
            _combosFound = value;
        }
    }

    /// <summary>Работает ли сочетание у героя (оба кольца надеты).</summary>
    public static bool Active(Player? player, RingCombo combo) =>
        player != null && RingComboRules.Active(EquippedRings(player)).Contains(combo);

    private static IEnumerable<string> EquippedRings(Player player) =>
        Loadout.Equipped(player).Where(r => r.Slot == EquipSlot.Ring).Select(r => r.GetType().Name);

    /// <summary>Отметить открытые сочетания на всех надетых кольцах пары; новое — вспышка.</summary>
    public static void Discover(Player? player)
    {
        if (player == null) return;
        var active = RingComboRules.Active(EquippedRings(player));
        if (active.Count == 0) return;
        foreach (var ring in Loadout.Equipped(player).OfType<ComboRing>())
        {
            var mine = active.Where(c => RingComboRules.Involving(ring.GetType().Name).Contains(c)).ToList();
            var mask = RingComboRules.Mark(ring._combosFound, mine);
            if (mask == ring._combosFound) continue;
            ring.CombosFound = mask;
            ring.Flash();
        }
    }

    public override async Task AfterObtained()
    {
        await base.AfterObtained();
        Discover(Owner);
    }

    public override Task BeforeCombatStart()
    {
        Discover(Owner);
        return Task.CompletedTask;
    }

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
        RingComboRules.Involving(GetType().Name)
            .Where(c => RingComboRules.Known(_combosFound, c))
            .Select(c => (IHoverTip)new HoverTip(UI.RequiemUi.Tip($"RING_COMBO_{c.ToString().ToUpperInvariant()}.title"),
                UI.RequiemUi.Tip($"RING_COMBO_{c.ToString().ToUpperInvariant()}.description")))
            .ToList();
}

internal static class JewelryCombat
{
    /// <summary>
    /// «Намерение-заклинание» (Кольцо Охотника на Ведьм): враг-маг (не физическая атака по таблице монстров), который
    /// собирается атаковать, проклинать или призывать.
    /// </summary>
    public static bool IsCasting(Creature? target)
    {
        if (target?.Monster is not { } monster) return false;
        if (RequiemData.MonsterEntry(monster.GetType().Name).AttackType == DamageType.Physical) return false;
        return monster.NextMove.Intents.Any(i => i.IntentType is IntentType.Attack or IntentType.DeathBlow or IntentType.Debuff
            or IntentType.DebuffStrong or IntentType.Summon or IntentType.CardDebuff or IntentType.StatusCard);
    }
}

// ───────────── кольца (07 §5.2) ─────────────

/// <summary>Кольцо Скорого Дыхания: +1 прирост ◆. Сочетание с Ярым Ударом — атаки за 0 ◆ +2 урона.</summary>
[Pool(typeof(SharedRelicPool))]
public sealed class RingOfQuickBreath : ComboRing, IResourceSource
{
    public override RelicRarity Rarity => RelicRarity.Uncommon;
    public override int Weight => 0;
    public int StaminaRegenBonus => IsEquipped ? JewelryRules.QuickBreathRegen : 0;

    public override decimal ModifyDamageAdditive(Creature? target, decimal amount, ValueProp props, Creature? dealer, CardModel? cardSource) =>
        IsEquipped && target != null && cardSource is { Type: CardType.Attack } && dealer == Owner.Creature && props.IsPoweredAttack()
        && StaminaCosts.CostFor(cardSource) == 0 && Active(Owner, RingCombo.QuickFury)
            ? JewelryRules.QuickFuryDamage
            : 0m;
}

/// <summary>Кольцо Ясного Ума: +1 прирост ✦. Сочетание с Наместником — повтор дважды за бой.</summary>
[Pool(typeof(SharedRelicPool))]
public sealed class RingOfClearMind : ComboRing, IResourceSource
{
    public override RelicRarity Rarity => RelicRarity.Uncommon;
    public override int Weight => 0;
    public int ManaRegenBonus => IsEquipped ? JewelryRules.ClearMindRegen : 0;
}

/// <summary>Кольцо Змеиной Улыбки: +1 карта в первой руке боя.</summary>
[Pool(typeof(SharedRelicPool))]
public sealed class RingOfTheSerpentSmile : EquipmentRelic
{
    public override RelicRarity Rarity => RelicRarity.Common;
    public override EquipSlot Slot => EquipSlot.Ring;
    public override int Weight => 0;

    public override decimal ModifyHandDraw(Player player, decimal count) =>
        IsEquipped && player == Owner && Owner.PlayerCombatState?.TurnNumber == 1 ? count + JewelryRules.SerpentSmileDraw : count;
}

/// <summary>Кольцо Пиромана: накладываемое вами Горение +2. Сочетание с Изморозью — реакция «Пар».</summary>
[Pool(typeof(SharedRelicPool))]
public sealed class RingOfThePyromancer : ComboRing
{
    public override RelicRarity Rarity => RelicRarity.Uncommon;
    public override int Weight => 0;

    public override bool TryModifyPowerAmountReceived(PowerModel canonicalPower, Creature target, decimal amount, Creature? applier, out decimal modifiedAmount)
    {
        modifiedAmount = amount;
        if (!IsEquipped || canonicalPower is not BurningPower || amount <= 0 || applier != Owner.Creature || target == Owner.Creature) return false;
        modifiedAmount = amount + JewelryRules.PyromancerBurning;
        return true;
    }

    /// <summary>Пар (03, «Реакции стихий»): Горение и Озноб на одной цели снимаются, цель получает их сумму без сопротивлений.</summary>
    public override async Task AfterPowerAmountChanged(PlayerChoiceContext choiceContext, PowerModel power, decimal amount, Creature? applier, CardModel? cardSource)
    {
        if (!IsEquipped || amount <= 0 || power is not (BurningPower or ChillPower) || applier != Owner.Creature) return;
        var target = power.Owner;
        if (target == null || target == Owner.Creature || !target.IsAlive || !Active(Owner, RingCombo.Steam)) return;
        var damage = JewelryRules.SteamDamage(target.GetPowerAmount<BurningPower>(), target.GetPowerAmount<ChillPower>());
        if (damage <= 0) return;
        Flash();
        if (target.GetPower<BurningPower>() is { } burning) await PowerCmd.Remove(burning);
        if (target.GetPower<ChillPower>() is { } chill) await PowerCmd.Remove(chill);
        using (DamageTyping.As(DamageType.True))
            await CreatureCmd.Damage(choiceContext, target, damage, ValueProp.Unpowered, Owner.Creature, null);
    }
}

/// <summary>Кольцо Изморози: накладываемый вами Озноб +1. Сочетание с Пироманом — реакция «Пар» (считает Кольцо Пиромана).</summary>
[Pool(typeof(SharedRelicPool))]
public sealed class RingOfHoarfrost : ComboRing
{
    public override RelicRarity Rarity => RelicRarity.Uncommon;
    public override int Weight => 0;

    public override bool TryModifyPowerAmountReceived(PowerModel canonicalPower, Creature target, decimal amount, Creature? applier, out decimal modifiedAmount)
    {
        modifiedAmount = amount;
        if (!IsEquipped || canonicalPower is not ChillPower || amount <= 0 || applier != Owner.Creature || target == Owner.Creature) return false;
        modifiedAmount = amount + JewelryRules.FrostbiteChill;
        return true;
    }
}

/// <summary>Кольцо Громоотвода: сопротивление разряду II; половина полученного урона разрядом возвращается ✦.</summary>
[Pool(typeof(SharedRelicPool))]
public sealed class RingOfTheLightningRod : EquipmentRelic
{
    public override RelicRarity Rarity => RelicRarity.Rare;
    public override EquipSlot Slot => EquipSlot.Ring;
    public override int Weight => 1;
    public override int Tier => 3;

    public override async Task AfterSideTurnStart(CombatSide side, IReadOnlyList<Creature> participants, ICombatState combatState)
    {
        if (!IsEquipped || side != CombatSide.Player || combatState.RoundNumber != 1) return;
        await PowerCmd.Apply<ShockResistancePower>(new ThrowingPlayerChoiceContext(), Owner.Creature,
            JewelryRules.LightningRodResistance, Owner.Creature, null, silent: true);
    }

    public override async Task AfterDamageReceived(PlayerChoiceContext choiceContext, Creature target, DamageResult result, ValueProp props, Creature? dealer, CardModel? cardSource)
    {
        if (!IsEquipped || target != Owner.Creature || result.TotalDamage <= 0) return;
        if (DamageTyping.Resolve(props, dealer, cardSource) != DamageType.Shock) return;
        var mana = JewelryRules.LightningRodMana(result.TotalDamage);
        if (mana <= 0) return;
        Flash();
        await Mana.Gain(Owner, mana);
    }
}

/// <summary>Кольцо Охотника на Ведьм: ваши атаки по врагу с намерением-заклинанием +4 урона.</summary>
[Pool(typeof(SharedRelicPool))]
public sealed class RingOfTheWitchHunter : EquipmentRelic
{
    public override RelicRarity Rarity => RelicRarity.Uncommon;
    public override EquipSlot Slot => EquipSlot.Ring;
    public override int Weight => 0;
    public override int Tier => 2;

    public override decimal ModifyDamageAdditive(Creature? target, decimal amount, ValueProp props, Creature? dealer, CardModel? cardSource) =>
        IsEquipped && dealer == Owner.Creature && cardSource != null && props.IsPoweredAttack() && JewelryCombat.IsCasting(target)
            ? JewelryRules.WitchHunterDamage
            : 0m;
}

/// <summary>Кольцо Регенерации: в начале хода лечит 1 ♥, пока ♥ ниже половины.</summary>
[Pool(typeof(SharedRelicPool))]
public sealed class RingOfRegeneration : EquipmentRelic
{
    public override RelicRarity Rarity => RelicRarity.Uncommon;
    public override EquipSlot Slot => EquipSlot.Ring;
    public override int Weight => 0;
    public override int Tier => 2;

    public override async Task AfterPlayerTurnStart(PlayerChoiceContext choiceContext, Player player)
    {
        if (!IsEquipped || player != Owner) return;
        var hero = Owner.Creature;
        if (!JewelryRules.RegenerationActive(hero.CurrentHp, hero.MaxHp)) return;
        Flash();
        using (Healing.As(HealSource.Regeneration))
            await CreatureCmd.Heal(hero, JewelryRules.RegenerationHeal);
    }
}

/// <summary>Кольцо Пьяницы: зелья мода дают +50 % эффекта (<see cref="Potions.RequiemPotion"/>); −1 пул ◆.</summary>
[Pool(typeof(SharedRelicPool))]
public sealed class RingOfTheDrunkard : EquipmentRelic, IResourceSource
{
    public override RelicRarity Rarity => RelicRarity.Uncommon;
    public override EquipSlot Slot => EquipSlot.Ring;
    public override int Weight => 0;
    public override int Tier => 2;
    public int StaminaPoolBonus => IsEquipped ? JewelryRules.DrunkardStaminaPool : 0;

    /// <summary>Сила зелья у героя с надетым кольцом.</summary>
    public static int Potency(Player? player, int potency) =>
        player?.Relics.OfType<RingOfTheDrunkard>().Any(r => r.IsEquipped) == true ? JewelryRules.DrunkardPotency(potency) : potency;
}

/// <summary>Кольцо Безликого Вора: кража в лавках и одна лишняя попытка за визит (сочетается со Знамением Ловкача).</summary>
[Pool(typeof(SharedRelicPool))]
public sealed class RingOfTheFacelessThief : EquipmentRelic, ITheftEnabler
{
    public override RelicRarity Rarity => RelicRarity.Rare;
    public override EquipSlot Slot => EquipSlot.Ring;
    public override int Weight => 0;
    public override int Tier => 3;
    public bool EnablesTheft => IsEquipped;
    public int ExtraTheftAttempts => IsEquipped ? JewelryRules.FacelessExtraTheft : 0;
}

/// <summary>Кольцо Призывателя: +1 слот призыва.</summary>
[Pool(typeof(SharedRelicPool))]
public sealed class RingOfTheSummoner : EquipmentRelic, ISummonModifier
{
    public override RelicRarity Rarity => RelicRarity.Rare;
    public override EquipSlot Slot => EquipSlot.Ring;
    public override int Weight => 0;
    public override int Tier => 3;
    public int SummonSlotBonus => IsEquipped ? JewelryRules.SummonerSlots : 0;
    public int SummonBonusDamage => 0;
    public int SummonDamageTakenPercent => 0;
}

/// <summary>
/// Кольцо Наместника: раз в бой первая сыгранная карта-заклинание срабатывает ещё раз (как ванильный Throwing Axe,
/// но для ✦). Сочетание с Ясным Умом — дважды за бой.
/// </summary>
[Pool(typeof(SharedRelicPool))]
public sealed class RingOfTheViceroy : ComboRing
{
    public override RelicRarity Rarity => RelicRarity.Rare;
    public override int Weight => 0;
    public override int Tier => 3;

    private int _replaysUsed;

    private int ReplaysUsed
    {
        get => _replaysUsed;
        set
        {
            AssertMutable();
            _replaysUsed = value;
        }
    }

    private bool Ready => IsEquipped && _replaysUsed < JewelryRules.ViceroyReplays(Active(Owner, RingCombo.ClearViceroy));

    public override Task AfterRoomEntered(AbstractRoom room)
    {
        if (room is not CombatRoom) return Task.CompletedTask;
        ReplaysUsed = 0;
        Status = Ready ? RelicStatus.Active : RelicStatus.Normal;
        return Task.CompletedTask;
    }

    public override int ModifyCardPlayCount(CardModel card, Creature? target, int playCount) =>
        card is SpellCard && card.Owner == Owner && Ready ? playCount + 1 : playCount;

    public override Task AfterModifyingCardPlayCount(CardModel card)
    {
        if (card is not SpellCard || card.Owner != Owner) return Task.CompletedTask;
        ReplaysUsed = _replaysUsed + 1;
        Flash();
        Status = Ready ? RelicStatus.Active : RelicStatus.Normal;
        return Task.CompletedTask;
    }

    public override Task AfterCombatEnd(CombatRoom room)
    {
        ReplaysUsed = 0;
        Status = RelicStatus.Normal;
        return Task.CompletedTask;
    }
}

// ───────────── амулеты (07 §5.1; Амулеты богов — Pantheon/Celestials.cs) ─────────────

/// <summary>Амулет Ловчего: ваши атаки по зверям +3; Обращение оборотня длится на 1 ход дольше.</summary>
[Pool(typeof(SharedRelicPool))]
public sealed class AmuletOfTheHunter : EquipmentRelic
{
    public override RelicRarity Rarity => RelicRarity.Uncommon;
    public override EquipSlot Slot => EquipSlot.Amulet;
    public override int Weight => 0;
    public override int Tier => 2;

    public override decimal ModifyDamageAdditive(Creature? target, decimal amount, ValueProp props, Creature? dealer, CardModel? cardSource) =>
        IsEquipped && target != null && dealer == Owner.Creature && cardSource != null && props.IsPoweredAttack() && EnemySetup.IsBeast(target)
            ? JewelryRules.HunterBeastDamage
            : 0m;

    public override bool TryModifyPowerAmountReceived(PowerModel canonicalPower, Creature target, decimal amount, Creature? applier, out decimal modifiedAmount)
    {
        modifiedAmount = amount;
        if (!IsEquipped || canonicalPower is not BeastFormPower || amount <= 0 || target != Owner.Creature || target.HasPower<BeastFormPower>()) return false;
        modifiedAmount = amount + JewelryRules.HunterFormTurns;
        return true;
    }
}

/// <summary>Амулет Матери Очага: после победы в бою лечит 4 ♥ (Burning Blood для всех).</summary>
[Pool(typeof(SharedRelicPool))]
public sealed class AmuletOfTheHearth : EquipmentRelic
{
    public override RelicRarity Rarity => RelicRarity.Common;
    public override EquipSlot Slot => EquipSlot.Amulet;
    public override int Weight => 0;

    public override async Task AfterCombatVictory(CombatRoom room)
    {
        if (!IsEquipped || Owner.Creature.IsDead) return;
        Flash();
        await CreatureCmd.Heal(Owner.Creature, JewelryRules.HearthHeal);
    }
}

/// <summary>Амулет Говорящего с Мёртвыми: призывы-нежить («Поднять мертвеца» и т. п.) получают +3 ♥.</summary>
[Pool(typeof(SharedRelicPool))]
public sealed class AmuletOfTheDeadspeaker : EquipmentRelic
{
    public override RelicRarity Rarity => RelicRarity.Uncommon;
    public override EquipSlot Slot => EquipSlot.Amulet;
    public override int Weight => 0;
    public override int Tier => 2;

    public override bool TryModifyPowerAmountReceived(PowerModel canonicalPower, Creature target, decimal amount, Creature? applier, out decimal modifiedAmount)
    {
        modifiedAmount = amount;
        if (!IsEquipped || target != Owner.Creature || canonicalPower is not SummonPower { IsUndead: true } || amount <= 0) return false;
        modifiedAmount = amount + JewelryRules.NecromancerUndeadHp;
        return true;
    }
}

/// <summary>Амулет Ткачихи: ваши Подлые атаки накладывают Яд 2.</summary>
[Pool(typeof(SharedRelicPool))]
public sealed class AmuletOfTheWeaver : EquipmentRelic
{
    public override RelicRarity Rarity => RelicRarity.Uncommon;
    public override EquipSlot Slot => EquipSlot.Amulet;
    public override int Weight => 0;
    public override int Tier => 2;

    // Создаётся лениво: каноническая модель копируется в мутабельные — общий набор на всех был бы ошибкой.
    private HashSet<Creature>? _sneakTargets;

    protected override void DeepCloneFields()
    {
        base.DeepCloneFields();
        _sneakTargets = null; // a copied amulet never owns another attack's in-flight targets
    }

    // Подлость проверяем до удара: Невидимость снимается атакой.
    public override Task BeforeDamageReceived(PlayerChoiceContext choiceContext, Creature target, decimal amount, ValueProp props, Creature? dealer, CardModel? cardSource)
    {
        if (IsEquipped && dealer == Owner.Creature && cardSource != null && props.IsPoweredAttack() && SneakRules.IsSneak(dealer, target))
            (_sneakTargets ??= new HashSet<Creature>()).Add(target);
        return Task.CompletedTask;
    }

    public override async Task AfterDamageGiven(PlayerChoiceContext choiceContext, Creature? dealer, DamageResult result, ValueProp props, Creature target, CardModel? cardSource)
    {
        if (_sneakTargets == null || !_sneakTargets.Remove(target) || dealer != Owner.Creature || !target.IsAlive) return;
        Flash();
        await PowerCmd.Apply<VanillaPoison>(choiceContext, target, JewelryRules.WeaverPoison, Owner.Creature, cardSource);
    }

    public override Task AfterCombatEnd(CombatRoom room)
    {
        _sneakTargets?.Clear();
        return Task.CompletedTask;
    }
}

/// <summary>Ожерелье Пустоты: Поглощение заклинаний 1 в начале боя; −1 пул ✦.</summary>
[Pool(typeof(SharedRelicPool))]
public sealed class NecklaceOfTheVoid : EquipmentRelic, IResourceSource
{
    public override RelicRarity Rarity => RelicRarity.Uncommon;
    public override EquipSlot Slot => EquipSlot.Amulet;
    public override int Weight => 0;
    public override int Tier => 2;
    public int ManaPoolBonus => IsEquipped ? JewelryRules.VoidManaPool : 0;

    public override async Task AfterSideTurnStart(CombatSide side, IReadOnlyList<Creature> participants, ICombatState combatState)
    {
        if (!IsEquipped || side != CombatSide.Player || combatState.RoundNumber != 1) return;
        await PowerCmd.Apply<SpellAbsorptionPower>(new ThrowingPlayerChoiceContext(), Owner.Creature,
            JewelryRules.VoidAbsorption, Owner.Creature, null, silent: true);
    }
}

/// <summary>Ожерелье Звёздного Камня: раз в бой, когда ✦ кончилась, — восстановить 4 ✦.</summary>
[Pool(typeof(SharedRelicPool))]
public sealed class NecklaceOfStarstone : EquipmentRelic
{
    public override RelicRarity Rarity => RelicRarity.Uncommon;
    public override EquipSlot Slot => EquipSlot.Amulet;
    public override int Weight => 0;
    public override int Tier => 2;

    private bool _used;

    public override Task AfterRoomEntered(AbstractRoom room)
    {
        if (room is CombatRoom)
        {
            _used = false;
            Status = IsEquipped ? RelicStatus.Active : RelicStatus.Normal;
        }
        return Task.CompletedTask;
    }

    public override async Task AfterCardPlayed(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        if (!IsEquipped || cardPlay.Card.Owner != Owner || !JewelryRules.StarstoneTriggers(Mana.Amount(Owner), _used)) return;
        _used = true;
        Flash();
        Status = RelicStatus.Normal;
        await Mana.Gain(Owner, JewelryRules.StarstoneMana);
    }

    public override Task AfterCombatEnd(CombatRoom room)
    {
        _used = false;
        Status = RelicStatus.Normal;
        return Task.CompletedTask;
    }
}
