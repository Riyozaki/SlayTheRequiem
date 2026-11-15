using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Rooms;
using SlayTheRequiem.SlayTheRequiemCode.Core;
using SlayTheRequiem.SlayTheRequiemCode.Transformations;

namespace SlayTheRequiem.SlayTheRequiemCode.Progression;

/// <summary>
/// Путь Героя (docs/design/06 §2): после победы над боссом 3-го акта (в обычном забеге — предпоследнего) при уровне ≥ 8
/// герою предлагается 1 из 3 Путей (2 «своих» + 1 чужой). Отказ (отмена экрана) — предложение повторится после следующего босса.
/// </summary>
public sealed partial class HeroRecord
{
    /// <summary>Начало боя: перекраска формы (визуал MVP).</summary>
    private void NoteCombatStart(ICombatState combatState) => FormVisuals.Refresh(Owner);

    private async Task AfterVictoryPaths(CombatRoom room)
    {
        if (room.RoomType != RoomType.Boss) return;
        var run = Owner.RunState;
        if (!TransformationRules.PathOfferDue(Level, run.CurrentActIndex, run.Acts.Count, Paths.Of(Owner) != null)) return;
        var offer = Paths.Offer(Owner);
        if (offer.Count == 0) return;
        var choices = offer.Select(p => new CampMenu.Choice(Paths.PickCardFor(p), 0, 0, p)).ToList();
        if (await CampMenu.Pick(Owner, choices, "SLAYTHEREQUIEM_PATH_PROMPT") is not HeroPath path) return;
        await Paths.Grant(Owner, path);
        Flash();
    }
}
