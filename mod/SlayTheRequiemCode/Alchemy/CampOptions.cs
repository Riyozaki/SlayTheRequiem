using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.RestSite;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Rewards;
using SlayTheRequiem.SlayTheRequiemCode.Core;
using SlayTheRequiem.SlayTheRequiemCode.Equipment;
using SlayTheRequiem.SlayTheRequiemCode.Potions;

namespace SlayTheRequiem.SlayTheRequiemCode.Alchemy;

/// <summary>
/// Действия лагеря Реквиема (docs/design/10). Пока очков лагеря нет (этап 5), Варка и Сундук — «свободные»
/// действия: не завершают привал (см. <see cref="TravelPack.ShouldDisableRemainingRestSiteOptions"/>), но каждое —
/// один раз за привал. Значки — ванильные (своих картинок пока нет): подменяются патчем
/// <see cref="Patches.RestSiteIconPatch"/>.
/// </summary>
public abstract class RequiemRestSiteOption(Player owner) : RestSiteOption(owner)
{
    /// <summary>Ванильный значок-заглушка (OptionId ванильного действия: HEAL, SMITH, COOK, DIG...).</summary>
    public abstract string FallbackIcon { get; }

    public string IconPathOverride
    {
        get
        {
            var own = $"{MainFile.ResPath}/images/ui/rest_site/option_{OptionId.ToLowerInvariant()}.png";
            return Godot.ResourceLoader.Exists(own) ? own : $"res://images/ui/rest_site/option_{FallbackIcon.ToLowerInvariant()}.png";
        }
    }

    public override IEnumerable<string> AssetPaths => [IconPathOverride];

    /// <summary>Отметить свободное действие — привал после него не завершается.</summary>
    protected Task<bool> Done(bool success)
    {
        if (success) TravelPack.Of(Owner)?.MarkFreeAction();
        return Task.FromResult(success);
    }
}

/// <summary>
/// Варка (docs/design/09 §2, 10): из Сумки травника — выбор из до 3 сильнейших доступных рецептов (экран наград);
/// реагенты списываются за взятое зелье, взять можно одно.
/// </summary>
public sealed class BrewRestSiteOption(Player owner) : RequiemRestSiteOption(owner)
{
    public const int MaxOffers = 3;
    public override string OptionId => "SLAYTHEREQUIEM_BREW";
    public override string FallbackIcon => "COOK";

    private List<AlchemyRules.BrewOption> Options =>
        HerbalistPouch.Of(Owner) is { } pouch ? AlchemyRules.Available(pouch.Reagents, HerbalismSteps) : [];

    /// <summary>Ступени «Знатока трав» (ветка Выживания — этап 5).</summary>
    private int HerbalismSteps => 0;

    public override bool IsEnabled => Options.Count > 0;

    public override LocString Description
    {
        get
        {
            if (!IsEnabled) return new LocString("rest_site_ui", $"OPTION_{OptionId}.descriptionDisabled");
            var loc = base.Description;
            loc.Add("Count", HerbalistPouch.Of(Owner)?.Total ?? 0);
            return loc;
        }
    }

    /// <summary>Выбор вариантов: лучшее зелье, лучший яд, затем следующий по силе — чтобы было из чего выбрать.</summary>
    public static List<AlchemyRules.BrewOption> PickOffers(List<AlchemyRules.BrewOption> available)
    {
        var picked = new List<AlchemyRules.BrewOption>();
        var potion = available.FirstOrDefault(o => !o.Recipe.IsPoison);
        var poison = available.FirstOrDefault(o => o.Recipe.IsPoison);
        if (potion.Ingredients != null) picked.Add(potion);
        if (poison.Ingredients != null) picked.Add(poison);
        foreach (var o in available)
        {
            if (picked.Count >= MaxOffers) break;
            if (!picked.Contains(o)) picked.Add(o);
        }
        return picked;
    }

    public override async Task<bool> OnSelect()
    {
        var pouch = HerbalistPouch.Of(Owner);
        if (pouch == null) return false;
        var offers = new List<(PotionModel, Reagent[])>();
        var rewards = new List<Reward>();
        foreach (var option in PickOffers(Options))
        {
            if (RequiemPotion.Create(option.Recipe.Brew, option.Tier) is not { } potion) continue;
            offers.Add((potion, option.Ingredients));
            rewards.Add(new PotionReward(potion, Owner));
        }
        if (rewards.Count == 0) return false;
        pouch.BeginBrew(offers);
        await RewardsCmd.OfferCustom(Owner, rewards);
        var brewed = pouch.BrewTaken;
        pouch.EndBrew();
        return await Done(brewed);
    }
}

/// <summary>
/// Сундук лагеря (docs/design/02 §3, 10): убрать карты из колоды (они перестают весить) и забрать обратно;
/// переложить одну вещь снаряжения между рюкзаком и сундуком. Надетое в сундук не кладётся.
/// </summary>
public sealed class ChestRestSiteOption(Player owner) : RequiemRestSiteOption(owner)
{
    public override string OptionId => "SLAYTHEREQUIEM_CHEST";
    public override string FallbackIcon => "DIG";

    public override LocString Description
    {
        get
        {
            var loc = base.Description;
            var pack = TravelPack.Of(Owner);
            loc.Add("Weight", pack == null ? 0 : Loadout.TotalWeight(Owner));
            loc.Add("Capacity", pack == null ? 0 : Loadout.Capacity(Owner));
            loc.Add("Stored", pack?.StoredCount ?? 0);
            return loc;
        }
    }

    public override async Task<bool> OnSelect()
    {
        var pack = TravelPack.Of(Owner);
        if (pack == null) return false;
        var changed = false;

        // 1. Карты из колоды → в сундук.
        var storePrefs = new CardSelectorPrefs(new LocString("rest_site_ui", "SLAYTHEREQUIEM_CHEST_STORE_PROMPT"), 0, 99)
        {
            Cancelable = true,
            RequireManualConfirmation = true,
        };
        var toStore = (await CardSelectCmd.FromDeckGeneric(Owner, storePrefs, c => c.IsRemovable)).ToList();
        if (toStore.Count > 0)
        {
            await pack.StoreCards(toStore);
            changed = true;
        }

        // 2. Карты из сундука → в колоду (кроме только что убранных).
        var preview = pack.PreviewStoredCards();
        var fresh = toStore.Count;
        var takeable = preview.Take(preview.Count - fresh).ToList();
        if (takeable.Count > 0)
        {
            var takePrefs = new CardSelectorPrefs(new LocString("rest_site_ui", "SLAYTHEREQUIEM_CHEST_TAKE_PROMPT"), 0, takeable.Count)
            {
                Cancelable = true,
                RequireManualConfirmation = true,
            };
            var chosen = (await CardSelectCmd.FromSimpleGrid(new MegaCrit.Sts2.Core.GameActions.Multiplayer.ThrowingPlayerChoiceContext(), takeable, Owner, takePrefs)).ToList();
            if (chosen.Count > 0)
            {
                await pack.TakeCards(chosen.Select(c => takeable.IndexOf(c)).ToList());
                changed = true;
            }
        }

        // 3. Одна вещь снаряжения: снятая из рюкзака → в сундук, или из сундука → в рюкзак.
        var stowed = Loadout.All(Owner).Where(r => r.IsStowed).Cast<RelicModel>().ToList();
        var stored = pack.StoredItemRelics();
        if (stowed.Count + stored.Count > 0)
        {
            var pick = await RelicSelectCmd.FromChooseARelicScreen(Owner, [.. stowed, .. stored]);
            if (pick is EquipmentRelic item && stowed.Contains(item))
            {
                await pack.StoreItem(item);
                changed = true;
            }
            else if (pick != null && stored.Contains(pick))
            {
                await pack.TakeItem(pick.GetType().Name);
                changed = true;
            }
        }

        return await Done(changed);
    }
}
