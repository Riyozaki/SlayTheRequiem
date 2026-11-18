using MegaCrit.Sts2.Core.Entities.Players;
using SlayTheRequiem.SlayTheRequiemCode.Alchemy;
using SlayTheRequiem.SlayTheRequiemCode.Config;
using SlayTheRequiem.SlayTheRequiemCode.Core;
using SlayTheRequiem.SlayTheRequiemCode.Pantheon;
using SlayTheRequiem.SlayTheRequiemCode.Rules;
using SlayTheRequiem.SlayTheRequiemCode.UI;

namespace SlayTheRequiem.SlayTheRequiemCode.Transformations;

/// <summary>Optional, deterministic camp access, independent of '?' / Pantheon RNG. It never grants an unearned
/// chain step, bypasses incompatibility, repeats starter gifts or edits an already selected worldview.</summary>
public static class FormRites
{
    private static readonly FormKind[] Kinds = [FormKind.Werewolf, FormKind.DarkPact, FormKind.Righteous,
        FormKind.Clockwork, FormKind.Vampire, FormKind.Herald, FormKind.Lich];

    private static bool Short(Player player) => BuildAccessRules.IsShortRoute(RunSettings.FiveActs, RunSettings.ShortRun, player.RunState.Acts.Count);
    internal static bool Earned(Player player, FormKind kind) => kind switch
    {
        FormKind.Herald => Forms.Of<DarkPactForm>(player) is { } pact
            && BuildAccessRules.HeraldDue(false, pact.EliteVictories),
        FormKind.Lich => Forms.Of<HeraldForm>(player)?.CanBecomeLich == true,
        _ => true,
    };

    internal static bool Available(Player player, FormKind kind) =>
        RequiemMode.IsInSystem(player) && Forms.CanTake(player, kind) && Earned(player, kind)
        && BuildAccessRules.FormAvailableAtCamp(kind, player.RunState.CurrentActIndex, player.RunState.Acts.Count, Short(player));

    public static bool HasOffer(Player player) => Kinds.Any(kind => Available(player, kind));

    internal static List<NHeroCreationScreen.Choice> Choices(Player player) => Kinds.Select(kind =>
    {
        var model = (FormRelic)Forms.Create(Forms.TypeFor(kind)); model.Owner = player;
        if (model is HeraldForm herald) herald.Wins = 0; // initializes the route-specific requirement in this preview
        var reason = Available(player, kind) ? "" : !Earned(player, kind)
            ? RequiemUi.TipText(kind == FormKind.Herald ? "UI.formHeraldLocked.title" : "UI.formLichLocked.title",
                ("Count", kind == FormKind.Herald ? BuildAccessRules.PactEliteWinsForHerald : BuildAccessRules.LichWinsRequired(Short(player))))
            : !BuildAccessRules.FormAvailableAtCamp(kind, player.RunState.CurrentActIndex, player.RunState.Acts.Count, Short(player))
                ? RequiemUi.TipText("UI.formLater.title")
                : kind == FormKind.Righteous && Faith.Effective(player) != Piety.Righteous
                    ? RequiemUi.TipText("UI.formPietyLocked.title") : RequiemUi.TipText("UI.formIncompatible.title");
        return new NHeroCreationScreen.Choice(kind.ToString(), model.Title.GetFormattedText(),
            model.DynamicDescription.GetFormattedText() + (reason.Length > 0 ? "\n" + reason : ""), Available(player, kind));
    }).ToList();
}

public sealed class FormRitesRestSiteOption(Player owner) : RequiemRestSiteOption(owner)
{
    public override string OptionId => "SLAYTHEREQUIEM_FORM_RITES";
    public override string FallbackIcon => "COOK";
    public override CampAction Action => CampAction.FormRite;
    protected override bool Available => FormRites.HasOffer(Owner);

    protected override async Task<bool> Perform()
    {
        var room = Owner.RunState.CurrentRoom;
        var picked = await NHeroCreationScreen.Pick(Owner, RequiemUi.TipText("UI.formRites.title"),
            RequiemUi.TipText("UI.formRites.description"), FormRites.Choices(Owner), cancelable: true);
        if (picked.Length == 0 || room != Owner.RunState.CurrentRoom || !Enum.TryParse<FormKind>(picked, out var kind)
            || !FormRites.Available(Owner, kind)) return false;
        return await Forms.Grant(Owner, kind, InventoryLease);
    }
}
