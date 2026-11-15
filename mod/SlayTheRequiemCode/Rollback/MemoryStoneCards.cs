using BaseLib.Utils;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Models.CardPools;
using SlayTheRequiem.SlayTheRequiemCode.Cards;
using SlayTheRequiem.SlayTheRequiemCode.Core;

namespace SlayTheRequiem.SlayTheRequiemCode.Rollback;

/// <summary>
/// «Предсмертное эхо» (docs/design/15 §3, Шрам души): неиграбельное проклятие, вес 2 (как все проклятия в Грузе).
/// В пуле жетонов — случайно не выпадает (ванильные события с «случайным проклятием» его не дадут).
/// </summary>
[Pool(typeof(TokenCardPool))]
public sealed class DeathEcho : RequiemCard
{
    public override int MaxUpgradeLevel => 0;

    public DeathEcho() : base(-1, CardType.Curse, CardRarity.Curse, TargetType.None)
    {
        WithKeyword(CardKeyword.Unplayable);
    }
}

/// <summary>Карта-выбор цены возврата на экране поражения. В колоду не попадает.</summary>
public abstract class ScarChoiceCard() : RequiemCard(-1, CardType.Status, CardRarity.Status, TargetType.None)
{
    public abstract Scar Scar { get; }
    public override int MaxUpgradeLevel => 0;
}

[Pool(typeof(TokenCardPool))]
public sealed class ScarOfFlesh : ScarChoiceCard
{
    public override Scar Scar => Scar.Flesh;
    public ScarOfFlesh() { WithKeyword(CardKeyword.Unplayable); }
}

[Pool(typeof(TokenCardPool))]
public sealed class ScarOfSoul : ScarChoiceCard
{
    public override Scar Scar => Scar.Soul;
    public ScarOfSoul() { WithKeyword(CardKeyword.Unplayable); }
}

[Pool(typeof(TokenCardPool))]
public sealed class ScarOfPurse : ScarChoiceCard
{
    public override Scar Scar => Scar.Purse;
    public ScarOfPurse() { WithKeyword(CardKeyword.Unplayable); }
}
