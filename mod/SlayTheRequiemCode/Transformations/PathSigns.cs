using BaseLib.Utils;
using Godot;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models.RelicPools;
using MegaCrit.Sts2.Core.Runs;
using SlayTheRequiem.SlayTheRequiemCode.Core;
using SlayTheRequiem.SlayTheRequiemCode.Equipment;
using SlayTheRequiem.SlayTheRequiemCode.Powers;
using SlayTheRequiem.SlayTheRequiemCode.Rules;

namespace SlayTheRequiem.SlayTheRequiemCode.Transformations;

/// <summary>
/// Знак Пути (docs/design/06 §2) — снаряжение-реликвия, выдаётся вместе с Сущностью. Ступень 4 (не продаётся, не выпадает).
/// Визуал MVP: в начале боя короткая вспышка цвета Пути на модели героя; спрайт-накладки (знамя, маска, обмотки) — со спрайтами.
/// </summary>
public abstract class PathSign : EquipmentRelic, IResourceSource
{
    public abstract HeroPath Path { get; }
    public override RelicRarity Rarity => RelicRarity.Ancient;
    public override bool IsAllowedInShops => false;
    public override bool IsAllowed(IRunState runState) => false;
    public override int Tier => 4;

    protected abstract Color Glow { get; }

    public override async Task AfterSideTurnStart(CombatSide side, IReadOnlyList<Creature> participants, ICombatState combatState)
    {
        if (!IsEquipped || side != CombatSide.Player || combatState.RoundNumber != 1 || Owner?.Creature == null) return;
        FormVisuals.Refresh(Owner, flash: Glow);
        await OnCombatOpened();
    }

    protected virtual Task OnCombatOpened() => Task.CompletedTask;

    protected Task ApplySelf<T>(int amount) where T : MegaCrit.Sts2.Core.Models.PowerModel =>
        PowerCmd.Apply<T>(new ThrowingPlayerChoiceContext(), Owner.Creature, amount, Owner.Creature, null, silent: true);
}

/// <summary>Сияющий нагрудник (Паладин): тяжёлое тело IV; Оберег 4 в начале боя.</summary>
[Pool(typeof(SharedRelicPool))]
public sealed class RadiantCuirass : PathSign
{
    public override HeroPath Path => HeroPath.Paladin;
    public override EquipSlot Slot => EquipSlot.Body;
    public override ArmorClass Armor => ArmorClass.Heavy;
    public override int Weight => 4;
    protected override Color Glow => new(1f, 0.9f, 0.4f);
    protected override Task OnCombatOpened() => ApplySelf<SpellShieldPower>(4);
}

/// <summary>Шипастый шлем (Чёрный рыцарь): тяжёлая голова IV; Ответный удар 2.</summary>
[Pool(typeof(SharedRelicPool))]
public sealed class SpikedHelm : PathSign
{
    public override HeroPath Path => HeroPath.BlackKnight;
    public override EquipSlot Slot => EquipSlot.Head;
    public override ArmorClass Armor => ArmorClass.Heavy;
    public override int Weight => 3;
    protected override Color Glow => new(0.35f, 0.05f, 0.1f);
    protected override Task OnCombatOpened() => ApplySelf<RipostePower>(2);
}

/// <summary>Знамя полка (Чемпион): плащ; Стойкость +1.</summary>
[Pool(typeof(SharedRelicPool))]
public sealed class RegimentBanner : PathSign
{
    public override HeroPath Path => HeroPath.Champion;
    public override EquipSlot Slot => EquipSlot.Cloak;
    public override int Weight => 2;
    protected override Color Glow => new(0.9f, 0.2f, 0.15f);
    public int PoiseBonus => IsEquipped ? 1 : 0;
}

/// <summary>Рогатый шлем (Варвар): лёгкая голова; пул ◆ +1.</summary>
[Pool(typeof(SharedRelicPool))]
public sealed class HornedHelm : PathSign
{
    public override HeroPath Path => HeroPath.Barbarian;
    public override EquipSlot Slot => EquipSlot.Head;
    public override ArmorClass Armor => ArmorClass.Light;
    public override int Weight => 1;
    protected override Color Glow => new(0.85f, 0.45f, 0.15f);
    public int StaminaPoolBonus => IsEquipped ? 1 : 0;
}

/// <summary>Обмотки (Монах): руки, вес 0; Уклонение 1 в начале боя.</summary>
[Pool(typeof(SharedRelicPool))]
public sealed class MonkWraps : PathSign
{
    public override HeroPath Path => HeroPath.Monk;
    public override EquipSlot Slot => EquipSlot.Hands;
    public override int Weight => 0;
    protected override Color Glow => new(0.95f, 0.85f, 0.6f);
    protected override Task OnCombatOpened() => ApplySelf<DodgePower>(1);
}

/// <summary>Маска без лица (Ассасин): голова, вес 0; Уклонение 1 в начале боя, прирост ◆ +1.</summary>
[Pool(typeof(SharedRelicPool))]
public sealed class FacelessMask : PathSign
{
    public override HeroPath Path => HeroPath.Assassin;
    public override EquipSlot Slot => EquipSlot.Head;
    public override int Weight => 0;
    protected override Color Glow => new(0.2f, 0.2f, 0.25f);
    protected override Task OnCombatOpened() => ApplySelf<DodgePower>(1);
    public int StaminaRegenBonus => IsEquipped ? 1 : 0;
}

/// <summary>Флакон-кулон (Губитель): амулет; сопротивление яду I.</summary>
[Pool(typeof(SharedRelicPool))]
public sealed class VialPendant : PathSign
{
    public override HeroPath Path => HeroPath.Blightbringer;
    public override EquipSlot Slot => EquipSlot.Amulet;
    public override int Weight => 0;
    protected override Color Glow => new(0.4f, 0.85f, 0.2f);
    protected override Task OnCombatOpened() => ApplySelf<PoisonResistancePower>(1);
}

/// <summary>Рунный колчан (Мистический лучник): плащ; пул ✦ +2.</summary>
[Pool(typeof(SharedRelicPool))]
public sealed class RunicQuiver : PathSign
{
    public override HeroPath Path => HeroPath.MysticArcher;
    public override EquipSlot Slot => EquipSlot.Cloak;
    public override int Weight => 1;
    protected override Color Glow => new(0.5f, 0.7f, 1f);
    public int ManaPoolBonus => IsEquipped ? 2 : 0;
}

/// <summary>Посох стихии (Повелитель стихии): посох в правой руке; пул ✦ +3, прирост ✦ +1.</summary>
[Pool(typeof(SharedRelicPool))]
public sealed class ElementStaff : PathSign
{
    public override HeroPath Path => HeroPath.ElementLord;
    public override EquipSlot Slot => EquipSlot.MainHand;
    public override WeaponKind Weapon => WeaponKind.Staff;
    public override int Weight => 2;
    protected override Color Glow => new(1f, 0.5f, 0.2f);
    public int ManaPoolBonus => IsEquipped ? 3 : 0;
    public int ManaRegenBonus => IsEquipped ? 1 : 0;
}

/// <summary>Рунные наручи (Боевой маг): руки; Плащ огня 2 в начале боя (Сущность накидывает и остальные).</summary>
[Pool(typeof(SharedRelicPool))]
public sealed class RunicBracers : PathSign
{
    public override HeroPath Path => HeroPath.Battlemage;
    public override EquipSlot Slot => EquipSlot.Hands;
    public override ArmorClass Armor => ArmorClass.Light;
    public override int Weight => 1;
    protected override Color Glow => new(0.7f, 0.4f, 1f);
    protected override Task OnCombatOpened() => ApplySelf<FireCloakPower>(2);
}

/// <summary>Зеркальный щит (Отражатель): щит левой руки; Оберег 6 в начале боя.</summary>
[Pool(typeof(SharedRelicPool))]
public sealed class MirrorShield : PathSign
{
    public override HeroPath Path => HeroPath.Reflector;
    public override EquipSlot Slot => EquipSlot.OffHand;
    public override WeaponKind Weapon => WeaponKind.Shield;
    public override int Weight => 2;
    protected override Color Glow => new(0.8f, 0.95f, 1f);
    protected override Task OnCombatOpened() => ApplySelf<SpellShieldPower>(6);
}

/// <summary>Колокольчик духов (Медиум): амулет; в начале боя призывает волка-духа (8 ♥).</summary>
[Pool(typeof(SharedRelicPool))]
public sealed class SpiritBell : PathSign
{
    public override HeroPath Path => HeroPath.Medium;
    public override EquipSlot Slot => EquipSlot.Amulet;
    public override int Weight => 0;
    protected override Color Glow => new(0.6f, 0.9f, 0.85f);
    protected override Task OnCombatOpened() =>
        SummonPower.Summon<SpiritWolfPower>(new ThrowingPlayerChoiceContext(), Owner.Creature, 8, null);
}
