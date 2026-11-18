using BaseLib.Utils;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Saves.Runs;
using SlayTheRequiem.SlayTheRequiemCode.Config;
using SlayTheRequiem.SlayTheRequiemCode.Core;
using SlayTheRequiem.SlayTheRequiemCode.Spells;
using SlayTheRequiem.SlayTheRequiemCode.Transformations;

namespace SlayTheRequiem.SlayTheRequiemCode.Progression;

/// <summary>Full route: boss 3 / level 8. Short 1→3→5: boss 1, no level-8 gate. Offer survives Cancel/Continue.</summary>
public sealed partial class HeroRecord
{
    private string _pathOfferData = "";
    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public string PathOfferData
    {
        get => _pathOfferData;
        set { AssertMutable(); if (!string.IsNullOrEmpty(value)) _ = PathOfferState.Read(value); _pathOfferData = value ?? ""; }
    }

    private void NoteCombatStart(ICombatState combatState) => FormVisuals.Refresh(Owner);

    private async Task AfterVictoryPaths(CombatRoom room)
    {
        if (room.RoomType != RoomType.Boss) return;
        var run = Owner.RunState;
        var shortRoute = RunSettings.FiveActs && RunSettings.ShortRun && run.Acts.Count == 3;
        if (!TransformationRules.PathOfferDue(Level, run.CurrentActIndex, run.Acts.Count, Paths.Of(Owner) != null, shortRoute)) return;
        if (PathOfferData.Length == 0)
        {
            var offer = Paths.Offer(Owner);
            if (offer.Count == 0) return;
            PathOfferData = PathOfferState.Write(offer); // persist the entitlement/RNG BEFORE awaiting a choice
        }
        await ResumePendingPaths();
    }

    internal async Task ResumePendingPaths(OperationGate.Lease? held = null)
    {
        if (PathOfferData.Length == 0) return;
        if (Paths.Of(Owner) != null) { PathOfferData = ""; return; }
        if (Mastery.GrimoireOf(Owner) is not { StartingChoicesComplete: true, RebirthData.Length: 0 }) return;
        var offer = PathOfferState.Read(PathOfferData).Where(p => TransformationRules.PathAllowed(p, Forms.Worldview(Owner),
            Paths.Apostate(Owner), Paths.Unarmed(Owner), Forms.IsOrdinator(Owner))).ToList();
        if (offer.Count == 0) return; // retain the original entitlement, never reroll a newly incompatible offer
        var choices = offer.Select(p => new CampMenu.Choice(Paths.PickCardFor(p), 0, 0, p)).ToList();
        if (await CampMenu.Pick(Owner, choices, "SLAYTHEREQUIEM_PATH_PROMPT") is not HeroPath path) return;
        await Paths.Grant(Owner, path, held);
        if (Paths.Of(Owner) != null) Flash();
    }
}
