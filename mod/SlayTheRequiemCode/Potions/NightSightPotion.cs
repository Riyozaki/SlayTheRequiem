using BaseLib.Abstracts;
using BaseLib.Extensions;
using BaseLib.Utils;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Potions;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models.PotionPools;
using SlayTheRequiem.SlayTheRequiemCode.Acts;
using SlayTheRequiem.SlayTheRequiemCode.Extensions;

namespace SlayTheRequiem.SlayTheRequiemCode.Potions;

/// <summary>
/// Зелье Ночного зрения (13 §3, M6 п. 1): до конца акта Тьма курганов отступает до 4 рядов, Засады и Тайные тропы видны
/// всему отряду. Пьётся когда угодно (на карте); продаётся у Травницы в Курганах.
/// </summary>
[Pool(typeof(TokenPotionPool))]
public sealed class NightSightPotion : CustomPotionModel
{
    public override PotionRarity Rarity => PotionRarity.Uncommon;
    public override PotionUsage Usage => PotionUsage.AnyTime;
    public override TargetType TargetType => TargetType.Self;

    private string ImageName => $"{Id.Entry.RemovePrefix().ToLowerInvariant()}.png";
    public override string? CustomPackedImagePath => ImageName.PotionImagePath("essence_of_darkness");
    public override string? CustomPackedOutlinePath => ImageName.PotionImagePath("essence_of_darkness", outline: true);

    protected override Task OnUse(PlayerChoiceContext ctx, Creature? target)
    {
        if (ActLedger.Of(Owner) is { } ledger && Owner.RunState is { } run)
        {
            ledger.NightSightAct = run.CurrentActIndex + 1;
            ledger.Flash();
            UI.MapVisuals.RefreshSoon();
        }
        return Task.CompletedTask;
    }
}
