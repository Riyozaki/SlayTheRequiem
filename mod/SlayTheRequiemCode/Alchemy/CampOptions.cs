using HarmonyLib;
using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Gold;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.RestSite;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Rewards;
using SlayTheRequiem.SlayTheRequiemCode.Config;
using SlayTheRequiem.SlayTheRequiemCode.Core;
using SlayTheRequiem.SlayTheRequiemCode.Equipment;
using SlayTheRequiem.SlayTheRequiemCode.Pantheon;
using SlayTheRequiem.SlayTheRequiemCode.Potions;
using SlayTheRequiem.SlayTheRequiemCode.Progression;
using SlayTheRequiem.SlayTheRequiemCode.Relics;
using SlayTheRequiem.SlayTheRequiemCode.Rules;
using SlayTheRequiem.SlayTheRequiemCode.Spells;

namespace SlayTheRequiem.SlayTheRequiemCode.Alchemy;

/// <summary>
/// Очки лагеря (docs/design/10 §1). Привал даёт 2 очка (+ узлы Выживания), каждое действие стоит 0–2; действие можно
/// выбрать один раз за привал (так устроен экран игры: выбранная кнопка исчезает), неистраченные очки сгорают.
/// Привал заканчивается, когда не осталось ни одного доступного действия (хук ShouldDisableRemainingRestSiteOptions
/// Летописи героя). Чужие действия (ванильные «Отдых»/«Кузница», реликвии вроде Лопаты, другие моды) оборачиваются
/// в <see cref="ForeignRestSiteOption"/> с ценой 2 — см. <see cref="RestSiteGeneratePatch"/>.
/// </summary>
public static class Camp
{
    private static readonly Dictionary<ulong, List<RestSiteOption>> Lists = new();
    private static readonly Dictionary<ulong, RestSiteOption> LastChosen = new();

    public static bool InSystem(Player player) => RequiemMode.IsInSystem(player) && HeroRecord.Of(player) != null;

    public static int Budget(Player player)
    {
        var hero = HeroRecord.Of(player);
        return CampRules.Budget(hero?.Has(Skill.Wayfarer) == true, hero?.Has(Skill.CampMaster) == true);
    }

    public static int Points(Player player) => HeroRecord.Of(player)?.CampPoints ?? 0;

    public static int CostOf(Player player, CampAction action) =>
        CampRules.Cost(action, HeroRecord.Of(player)?.Has(Skill.CampMaster) == true, CampMeta.Has(CampBuilding.Anvil));

    /// <summary>Вне системы очков нет — всё «по карману» (ванильное поведение не трогаем).</summary>
    public static bool CanAfford(Player player, int cost) =>
        !InSystem(player) || CampRules.CanAfford(Points(player), cost);

    /// <summary>Начало привала: полный запас очков; запомнить список кнопок игрока.</summary>
    public static void Begin(Player player, List<RestSiteOption> options)
    {
        var hero = HeroRecord.Of(player);
        if (hero == null) return;
        hero.CampPoints = Budget(player);
        hero.ArtisanUsed = false;
        hero.Refresh();
        Lists[player.NetId] = options;
        LastChosen.Remove(player.NetId);
    }

    public static void Choosing(Player player, RestSiteOption option) => LastChosen[player.NetId] = option;

    /// <summary>Списать очки; «Мастер-ремесленник» раз за привал возвращает 1 очко за ремесло.</summary>
    public static void Spend(Player player, CampAction action, int cost)
    {
        var hero = HeroRecord.Of(player);
        if (hero == null) return;
        hero.CampPoints -= cost;
        if (CampRules.IsCraft(action) && cost > 0 && hero.Has(Skill.CraftArtisan) && !hero.ArtisanUsed)
        {
            hero.ArtisanUsed = true;
            hero.CampPoints += 1;
            hero.Flash();
        }
        hero.Refresh();
    }

    /// <summary>Осталось ли доступное действие, кроме только что выбранного (оно сейчас уйдёт из списка).</summary>
    public static bool AnyRemaining(Player player)
    {
        if (!Lists.TryGetValue(player.NetId, out var list)) return false;
        LastChosen.TryGetValue(player.NetId, out var last);
        return list.Any(o => !ReferenceEquals(o, last) && o.IsEnabled);
    }

    /// <summary>Обернуть чужие действия ценой в очки (после всех хуков ModifyRestSiteOptions).</summary>
    public static void Wrap(Player player, List<RestSiteOption> options)
    {
        for (var i = 0; i < options.Count; i++)
        {
            var option = options[i];
            if (option is RequiemRestSiteOption or ForeignRestSiteOption) continue;
            var action = option.OptionId switch
            {
                "HEAL" => CampAction.Rest,
                "SMITH" => CampAction.Smith,
                _ => CampAction.Other,
            };
            options[i] = new ForeignRestSiteOption(player, option, action);
        }
    }

    // ───── постройки лагеря (10 §5: без метапрогресса — в половине лагерей; открытые победами — в каждом, CampMeta) ─────

    private static uint Hash(Player player, string salt) =>
        ShopRules.Fnv1a($"{player.RunState.Rng.StringSeed}|camp|{salt}", player.RunState.TotalFloor);

    /// <summary>Зачаровальный стол: в половине лагерей; Зачарователю — в любом.</summary>
    public static bool HasEnchantingTable(Player player) =>
        player.Relics.OfType<EnchanterBackground>().Any() || CampMeta.Has(CampBuilding.EnchantingTable) || Hash(player, "table") % 2 == 0;

    /// <summary>Таверна с Наставником (14 §2.9): в каждом третьем лагере по зерну.</summary>
    public static bool HasTavern(Player player) => Hash(player, "tavern") % 3 == 0;

    /// <summary>Алтарь: в половине лагерей.</summary>
    public static bool HasAltar(Player player) => CampMeta.Has(CampBuilding.Altar) || Hash(player, "altar") % 2 == 0;

    /// <summary>Готовка открыта: «Походный котёл», Котёл метапрогресса или Отшельник (Грибная похлёбка).</summary>
    public static bool HasKettle(Player player) =>
        HeroRecord.Of(player)?.Has(Skill.CampKettle) == true || CampMeta.Has(CampBuilding.Kettle);

    /// <summary>Бог алтаря этого лагеря.</summary>
    public static Celestial AltarGod(Player player) =>
        (Celestial)(int)(Hash(player, "god") % (uint)CelestialBlessing.Blessings.Length);

    /// <summary>Действия Реквиема (добавляются хуком Походного мешка).</summary>
    public static void AddOptions(Player player, ICollection<RestSiteOption> options)
    {
        var hero = HeroRecord.Of(player);
        options.Add(new ShortRestRestSiteOption(player));
        options.Add(new BrewRestSiteOption(player));
        if (hero != null && (HasKettle(player) || player.Relics.OfType<HermitBackground>().Any()))
            options.Add(new CookingRestSiteOption(player));
        options.Add(new ForgeRestSiteOption(player));
        if (hero != null && hero.TotalStones > 0 && HasEnchantingTable(player))
        {
            options.Add(new EnchantRestSiteOption(player));
            options.Add(new CardEnchantRestSiteOption(player));
        }
        if (hero != null) options.Add(new MeditateRestSiteOption(player));
        if (RequiemConfig.EnablePantheon && HasAltar(player)) options.Add(new PrayRestSiteOption(player));
        options.Add(new ChestRestSiteOption(player));
        if (Acts.WarmRestSiteOption.Offered(player)) options.Add(new Acts.WarmRestSiteOption(player));
        if (hero != null && hero.HasUnspentPoints) options.Add(new TrainRestSiteOption(player));
        if (hero != null && HasTavern(player))
        {
            options.Add(new TavernRestSiteOption(player));
            Acts.ActLedger.Of(player)?.RecordVisit(player.RunState, NodeSubtype.Tavern); // значок Таверны на карте (M6)
        }
        // Оборотень (этап 6): узлы веток за Обращения.
        if (Transformations.Forms.Of<Transformations.WerewolfForm>(player) is { PendingNodes: > 0 })
            options.Add(new Transformations.BeastRitesRestSiteOption(player));
    }
}

/// <summary>Генерация кнопок привала: обернуть чужие действия и выдать очки лагеря.</summary>
[HarmonyPatch(typeof(RestSiteOption), nameof(RestSiteOption.Generate))]
public static class RestSiteGeneratePatch
{
    [HarmonyPostfix]
    public static void Postfix(Player player, List<RestSiteOption> __result)
    {
        if (!Camp.InSystem(player)) return;
        Camp.Wrap(player, __result);
        Camp.Begin(player, __result);
    }
}

/// <summary>
/// Действие лагеря Реквиема: цена в очках, доступность, описание с ценой. Значки — ванильные заглушки
/// (своих картинок пока нет): подменяются патчем <see cref="Patches.RestSiteIconPatch"/>.
/// </summary>
public abstract class RequiemRestSiteOption(Player owner) : RestSiteOption(owner)
{
    /// <summary>Ванильный значок-заглушка (OptionId ванильного действия: HEAL, SMITH, COOK, DIG...).</summary>
    public abstract string FallbackIcon { get; }

    public abstract CampAction Action { get; }

    public int Cost => Camp.CostOf(Owner, Action);

    public string IconPathOverride
    {
        get
        {
            var own = $"{MainFile.ResPath}/images/ui/rest_site/option_{OptionId.ToLowerInvariant()}.png";
            return Godot.ResourceLoader.Exists(own) ? own : $"res://images/ui/rest_site/option_{FallbackIcon.ToLowerInvariant()}.png";
        }
    }

    public override IEnumerable<string> AssetPaths => [IconPathOverride];

    /// <summary>Своё условие действия (есть реагенты, есть что улучшить...).</summary>
    protected virtual bool Available => true;

    public override bool IsEnabled => Available && Camp.CanAfford(Owner, Cost);

    public override LocString Description
    {
        get
        {
            var loc = new LocString("rest_site_ui", Available ? $"OPTION_{OptionId}.description" : $"OPTION_{OptionId}.descriptionDisabled");
            loc.Add("Cost", Cost);
            loc.Add("Points", Camp.Points(Owner));
            Fill(loc);
            return loc;
        }
    }

    /// <summary>Доп. переменные описания.</summary>
    protected virtual void Fill(LocString loc) { }

    public sealed override async Task<bool> OnSelect()
    {
        Camp.Choosing(Owner, this);
        var cost = Cost;
        var success = await Perform();
        if (success) Camp.Spend(Owner, Action, cost);
        return success;
    }

    protected abstract Task<bool> Perform();
}

/// <summary>
/// Чужое действие привала (ванильные «Отдых», «Кузница», «Помочь» в коопе, Лопата, Гиря...) с ценой в очках лагеря.
/// Название, значок и описание — исходного действия (по его OptionId). После ванильного «Отдыха» снимается Усталость
/// похода и выдаётся Бодрость «Отдохнувшего».
/// </summary>
public sealed class ForeignRestSiteOption(Player owner, RestSiteOption inner, CampAction action) : RestSiteOption(owner)
{
    public RestSiteOption Inner => inner;
    public CampAction Action => action;
    public int Cost => Camp.CostOf(Owner, action);

    public override string OptionId => inner.OptionId;
    public override LocString Description => inner.Description;
    public override IEnumerable<string> AssetPaths => inner.AssetPaths;
    public override bool IsEnabled => inner.IsEnabled && Camp.CanAfford(Owner, Cost);

    public override async Task<bool> OnSelect()
    {
        Camp.Choosing(Owner, this);
        var cost = Cost;
        var success = await inner.OnSelect();
        if (!success) return false;
        Camp.Spend(Owner, action, cost);
        if (action == CampAction.Rest) HeroRecord.Of(Owner)?.AfterRest();
        return true;
    }

    public override Task DoLocalPostSelectVfx(CancellationToken ct = default) => inner.DoLocalPostSelectVfx(ct);
    public override Task DoRemotePostSelectVfx() => inner.DoRemotePostSelectVfx();
}

/// <summary>Короткий привал (10 §1): 1 очко, лечит 15 % макс. ♥ («Первая помощь» — 20 %).</summary>
public sealed class ShortRestRestSiteOption(Player owner) : RequiemRestSiteOption(owner)
{
    public override string OptionId => "SLAYTHEREQUIEM_SHORT_REST";
    public override string FallbackIcon => "HEAL";
    public override CampAction Action => CampAction.ShortRest;

    private int Percent => CampRules.ShortRestPercent(HeroRecord.Of(Owner)?.Has(Skill.FirstAid) == true);

    private decimal Amount => Hook.ModifyRestSiteHealAmount(Owner.RunState, Owner.Creature,
        CampRules.HealAmount(Owner.Creature.MaxHp, Percent));

    protected override void Fill(LocString loc)
    {
        loc.Add("Percent", Percent);
        loc.Add("Heal", (int)Amount);
    }

    protected override async Task<bool> Perform()
    {
        await CreatureCmd.Heal(Owner.Creature, Amount);
        return true;
    }

    public override Task DoLocalPostSelectVfx(CancellationToken ct = default)
    {
        HealRestSiteOption.PlayRestSiteHealSfx();
        return Task.CompletedTask;
    }
}

/// <summary>
/// Варка (docs/design/09 §2, 10): 1 очко. Из Сумки травника — выбор из до 3 сильнейших доступных рецептов (экран
/// наград); реагенты списываются за взятое зелье, взять можно одно. Ступень выше: «Алхимия» (Ремесло), «Знаток трав»
/// (Выживание); яды — ещё и «Мастер ядов».
/// </summary>
public sealed class BrewRestSiteOption(Player owner) : RequiemRestSiteOption(owner)
{
    public const int MaxOffers = 3;
    public override string OptionId => "SLAYTHEREQUIEM_BREW";
    public override string FallbackIcon => "COOK";
    public override CampAction Action => CampAction.Brew;

    public static int PotionSteps(Player player)
    {
        var hero = HeroRecord.Of(player);
        if (hero == null) return 0;
        return (hero.Has(Skill.CraftAlchemy) ? 1 : 0) + (hero.Has(Skill.HerbLore) ? 1 : 0)
            + (CampMeta.Has(CampBuilding.HerbalistTent) ? 1 : 0);
    }

    public static int PoisonSteps(Player player) =>
        PotionSteps(player) + (HeroRecord.Of(player)?.Has(Skill.VenomMaster) == true ? 1 : 0);

    /// <summary>Доступные рецепты: зелья и яды со своими ступенями перков, по силе.</summary>
    public static List<AlchemyRules.BrewOption> Brewable(Player player)
    {
        if (HerbalistPouch.Of(player) is not { } pouch) return [];
        var potions = AlchemyRules.Available(pouch.Reagents, PotionSteps(player)).Where(o => !o.Recipe.IsPoison);
        var poisons = AlchemyRules.Available(pouch.Reagents, PoisonSteps(player)).Where(o => o.Recipe.IsPoison);
        return potions.Concat(poisons).OrderByDescending(o => o.Tier).ToList();
    }

    private List<AlchemyRules.BrewOption> Options => Brewable(Owner);

    protected override bool Available => Options.Count > 0;

    protected override void Fill(LocString loc) => loc.Add("Count", HerbalistPouch.Of(Owner)?.Total ?? 0);

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

    protected override async Task<bool> Perform()
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
        return brewed;
    }
}

/// <summary>
/// Готовка (10 §3): 1 очко, открывается «Походным котлом» (Выживание I); Отшельник готовит Грибную похлёбку и без котла.
/// Сытость длится 3 боя (Пир — 1), одна за раз (Хмельной Князь — две); Грибная похлёбка совместима с любой едой.
/// </summary>
public sealed class CookingRestSiteOption(Player owner) : RequiemRestSiteOption(owner)
{
    public override string OptionId => "SLAYTHEREQUIEM_COOK";
    public override string FallbackIcon => "COOK";
    public override CampAction Action => CampAction.Cook;

    public static List<Dish> Cookable(Player player)
    {
        var pouch = HerbalistPouch.Of(player);
        var hero = HeroRecord.Of(player);
        if (pouch == null || hero == null) return [];
        var kettle = Camp.HasKettle(player);
        var hermit = player.Relics.OfType<HermitBackground>().Any();
        return FoodRules.Dishes
            .Where(d => d == Dish.MushroomStew ? hermit : kettle)
            .Where(d => FoodRules.IngredientsFor(d, pouch.Reagents) != null)
            .ToList();
    }

    protected override bool Available => Cookable(Owner).Count > 0;

    protected override async Task<bool> Perform()
    {
        var pouch = HerbalistPouch.Of(Owner);
        var hero = HeroRecord.Of(Owner);
        if (pouch == null || hero == null) return false;
        var choices = Cookable(Owner)
            .Select(d => new CampMenu.Choice(CampMenu.DishCard(d), FoodRules.DurationOf(d),
                FoodRules.IngredientsFor(d, pouch.Reagents)?.Length ?? 0, d))
            .ToList();
        if (await CampMenu.Pick(Owner, choices, "SLAYTHEREQUIEM_COOK_PROMPT") is not Dish dish) return false;
        var ingredients = FoodRules.IngredientsFor(dish, pouch.Reagents);
        if (ingredients == null || !pouch.TrySpend(ingredients)) return false;
        await hero.Eat(dish);
        return true;
    }
}

/// <summary>
/// Кузня (docs/design/07 §4, 10): 2 очка («Мастер привала» или Наковальня — 1): +1 к улучшению одной надетой вещи
/// (до +2; «Кузнечное дело» — до +3) или подковать вьючного зверя (+5 груза).
/// </summary>
public sealed class ForgeRestSiteOption(Player owner) : RequiemRestSiteOption(owner)
{
    public override string OptionId => "SLAYTHEREQUIEM_FORGE";
    public override string FallbackIcon => "SMITH";
    public override CampAction Action => CampAction.Forge;

    /// <summary>Надетые вещи, которые можно улучшить, и неподкованный вьючный зверь (10 §6).</summary>
    private List<RelicModel> Upgradable
    {
        get
        {
            var list = Loadout.Equipped(Owner).Where(r => r.CanUpgrade).Cast<RelicModel>().ToList();
            if (PackMule.Of(Owner) is { Shod: false } mule) list.Add(mule);
            return list;
        }
    }

    protected override bool Available => Upgradable.Count > 0;

    protected override async Task<bool> Perform()
    {
        var options = Upgradable;
        if (options.Count == 0) return false;
        var pick = options.Count == 1 ? options[0] : await RelicSelectCmd.FromChooseARelicScreen(Owner, options);
        if (pick is PackMule mule) return mule.Shoe();
        if (pick is not EquipmentRelic item || !item.CanUpgrade) return false;
        item.Upgrades += 1;
        item.Flash();
        return true;
    }
}

/// <summary>
/// Зачарование (07 §6.1, 10): 2 очка («Мастер привала» — 1), нужен Камень душ и Зачаровальный стол.
/// Камень → предмет → чары; сила чар = сила камня. Такие же чары заменяются (у оружейных — новые заряды).
/// </summary>
public sealed class EnchantRestSiteOption(Player owner) : RequiemRestSiteOption(owner)
{
    public override string OptionId => "SLAYTHEREQUIEM_ENCHANT";
    public override string FallbackIcon => "LIFT";
    public override CampAction Action => CampAction.Enchant;

    protected override bool Available =>
        HeroRecord.Of(Owner) is { TotalStones: > 0 } && Enchanting.Enchantable(Owner).Count > 0;

    protected override void Fill(LocString loc) => loc.Add("Stones", HeroRecord.Of(Owner)?.TotalStones ?? 0);

    protected override async Task<bool> Perform()
    {
        var hero = HeroRecord.Of(Owner);
        if (hero == null) return false;

        var powers = new[] { 1, 2, 3 }.Where(p => hero.Stones(p) > 0).ToList();
        if (powers.Count == 0) return false;
        var power = powers[0];
        if (powers.Count > 1)
        {
            var stones = powers.Select(p => new CampMenu.Choice(CampMenu.StoneCard(p), hero.Stones(p), p, p)).ToList();
            if (await CampMenu.Pick(Owner, stones, "SLAYTHEREQUIEM_STONE_PROMPT") is not int chosen) return false;
            power = chosen;
        }

        return await EnchantFlow(Owner, power, null, () => Task.FromResult(hero.SpendStone(power)));
    }

    /// <summary>
    /// Зачаровать надетый предмет (лагерь, Чародей, Гравировка Ювелира): выбрать предмет (синхронно в коопе), выбрать чары
    /// силы <paramref name="power"/>, оплатить (<paramref name="commit"/> — камень или золото; false — отмена) и наложить.
    /// </summary>
    public static async Task<bool> EnchantFlow(Player owner, int power, Func<EquipmentRelic, bool>? filter, Func<Task<bool>> commit)
    {
        var items = Enchanting.Enchantable(owner).Where(i => filter?.Invoke(i) ?? true).ToList();
        if (items.Count == 0) return false;
        var picked = items.Count == 1 ? items[0] : await RelicSelectCmd.FromChooseARelicScreen(owner, items.Cast<RelicModel>().ToList());
        if (picked is not EquipmentRelic item) return false;

        var choices = Enchanting.AllowedFor(item)
            .Select(e => new CampMenu.Choice(CampMenu.EnchantCard(e), EnchantRules.Value(e, power),
                EnchantRules.IsWeaponBrand(e) ? EnchantRules.Charges(power) : 0, e))
            .ToList();
        if (await CampMenu.Pick(owner, choices, "SLAYTHEREQUIEM_ENCHANT_PROMPT") is not Enchant enchant) return false;
        if (!await commit()) return false;
        Enchanting.Apply(owner, item, enchant, power);
        var hero = HeroRecord.Of(owner);
        if (hero != null) await hero.SyncMaxHp();
        TravelPack.Of(owner)?.Refresh();
        hero?.Refresh();
        return true;
    }
}

/// <summary>Медитация (10 §1): 1 очко — +1 к ступени ◆ или ✦ (не выше III) до конца следующего боя.</summary>
public sealed class MeditateRestSiteOption(Player owner) : RequiemRestSiteOption(owner)
{
    public override string OptionId => "SLAYTHEREQUIEM_MEDITATE";
    public override string FallbackIcon => "HEAL";
    public override CampAction Action => CampAction.Meditate;

    private static readonly StatAxis[] Axes = [StatAxis.Stamina, StatAxis.Mana];

    protected override bool Available =>
        HeroRecord.Of(Owner) is { Meditation: 0 } hero && Axes.Any(a => hero.BaseTier(a) < ProgressionRules.MaxTier);

    protected override async Task<bool> Perform()
    {
        var hero = HeroRecord.Of(Owner);
        if (hero == null) return false;
        var choices = Axes.Where(a => hero.BaseTier(a) < ProgressionRules.MaxTier)
            .Select(a => new CampMenu.Choice(a == StatAxis.Stamina ? typeof(PickMeditateStamina) : typeof(PickMeditateMana),
                hero.BaseTier(a) + 1, 0, a))
            .ToList();
        if (await CampMenu.Pick(Owner, choices, "SLAYTHEREQUIEM_MEDITATE_PROMPT") is not StatAxis axis) return false;
        hero.Meditation = axis == StatAxis.Stamina ? 1 : 2;
        hero.Flash();
        hero.Refresh();
        return true;
    }
}

/// <summary>
/// Молитва (05 §3, 10 §1): 1 очко, у алтаря лагеря. Сменить благословение на бога алтаря или искупиться
/// (+1 ступень праведности, раз в акт — без пожертвования).
/// </summary>
public sealed class PrayRestSiteOption(Player owner) : RequiemRestSiteOption(owner)
{
    public override string OptionId => "SLAYTHEREQUIEM_PRAY";
    public override string FallbackIcon => "HEAL";
    public override CampAction Action => CampAction.Pray;

    private bool CanSwitch => CelestialBlessing.Of(Owner) is { } blessing && blessing.God != Camp.AltarGod(Owner);

    protected override bool Available => Faith.CanRedeem(Owner) || CanSwitch;

    protected override void Fill(LocString loc) => loc.Add("God", (int)Camp.AltarGod(Owner));

    protected override async Task<bool> Perform()
    {
        var choices = new List<CampMenu.Choice>();
        if (Faith.CanRedeem(Owner)) choices.Add(new CampMenu.Choice(typeof(PickPrayRedeem), 0, 0, "redeem"));
        if (CanSwitch) choices.Add(new CampMenu.Choice(typeof(PickPraySwitch), (int)Camp.AltarGod(Owner), 0, "switch"));
        var tag = await CampMenu.Pick(Owner, choices, "SLAYTHEREQUIEM_PRAY_PROMPT") as string;
        switch (tag)
        {
            case "redeem":
                if (!Faith.Redeem(Owner)) return false;
                Faith.RefreshAll(Owner);
                return true;
            case "switch":
                await CelestialBlessing.Grant(Owner, Camp.AltarGod(Owner));
                Faith.RefreshAll(Owner);
                return true;
            default:
                return false;
        }
    }
}

/// <summary>
/// Развитие (02 §5, 04): 0 очков — потратить очки характеристик и навыков (экран-карта за экраном, пока есть что
/// брать или пока игрок не закроет выбор). Владение школой — 1 очко для родной, 2 для чужой.
/// </summary>
public sealed class TrainRestSiteOption(Player owner) : RequiemRestSiteOption(owner)
{
    public override string OptionId => "SLAYTHEREQUIEM_TRAIN";
    public override string FallbackIcon => "LIFT";
    public override CampAction Action => CampAction.Train;

    protected override bool Available => HeroRecord.Of(Owner) is { HasUnspentPoints: true };

    protected override void Fill(LocString loc)
    {
        var hero = HeroRecord.Of(Owner);
        loc.Add("StatPoints", hero?.UnspentStatPoints ?? 0);
        loc.Add("SkillPoints", hero?.UnspentSkillPoints ?? 0);
        loc.Add("Level", hero?.Level ?? 1);
    }

    private static readonly StatAxis[] Axes = [StatAxis.Health, StatAxis.Stamina, StatAxis.Mana, StatAxis.Burden];

    private static int PerPoint(StatAxis axis) => axis switch
    {
        StatAxis.Health => ProgressionRules.HealthPerPoint,
        StatAxis.Stamina => ProgressionRules.StaminaPoolPerPoint,
        StatAxis.Mana => ProgressionRules.ManaPoolPerPoint,
        StatAxis.Burden => ProgressionRules.BurdenPerPoint,
        _ => 1,
    };

    private List<SpellSchool> RaisableSchools(HeroRecord hero) =>
        Mastery.Schools.Where(s => (Mastery.GrimoireOf(Owner)?.Level(s) ?? SpellAffinity.MaxMastery) < SpellAffinity.MaxMastery
                                   && hero.MasteryCost(s) <= hero.UnspentStatPoints).ToList();

    protected override async Task<bool> Perform()
    {
        var hero = HeroRecord.Of(Owner);
        if (hero == null) return false;
        var changed = false;
        while (true)
        {
            var choices = new List<CampMenu.Choice>();
            if (hero.UnspentStatPoints > 0)
            {
                foreach (var axis in Axes)
                    choices.Add(new CampMenu.Choice(CampMenu.StatCard(axis), PerPoint(axis), hero.Points(axis), axis));
                if (RaisableSchools(hero).Count > 0)
                    choices.Add(new CampMenu.Choice(CampMenu.StatCard(StatAxis.Mastery), 1, hero.UnspentStatPoints, StatAxis.Mastery));
            }
            if (hero.UnspentSkillPoints > 0)
                foreach (var node in hero.Learnable())
                    choices.Add(new CampMenu.Choice(CampMenu.NodeCard(node.Skill), node.Rank, hero.CostOf(node), node.Skill));
            if (choices.Count == 0) break;

            var tag = await CampMenu.Pick(Owner, choices, "SLAYTHEREQUIEM_TRAIN_PROMPT");
            switch (tag)
            {
                case Skill skill:
                    changed |= await hero.Learn(skill);
                    break;
                case StatAxis.Mastery:
                {
                    var schools = RaisableSchools(hero)
                        .Select(s => new CampMenu.Choice(CampMenu.SchoolCard(s), Mastery.GrimoireOf(Owner)?.Level(s) ?? 0, hero.MasteryCost(s), s))
                        .ToList();
                    if (await CampMenu.Pick(Owner, schools, "SLAYTHEREQUIEM_SCHOOL_PROMPT") is SpellSchool school)
                        changed |= await hero.SpendStat(StatAxis.Mastery, school);
                    break;
                }
                case StatAxis axis:
                    changed |= await hero.SpendStat(axis);
                    break;
                default:
                    return changed;
            }
        }
        return changed;
    }
}

/// <summary>
/// Сундук лагеря (docs/design/02 §3, 10): 0 очков. Убрать карты из колоды (они перестают весить) и забрать обратно;
/// переложить одну вещь снаряжения между рюкзаком и сундуком («Ловкая укладка» — сколько угодно). Надетое не кладётся.
/// </summary>
public sealed class ChestRestSiteOption(Player owner) : RequiemRestSiteOption(owner)
{
    public override string OptionId => "SLAYTHEREQUIEM_CHEST";
    public override string FallbackIcon => "DIG";
    public override CampAction Action => CampAction.Chest;

    protected override void Fill(LocString loc)
    {
        var pack = TravelPack.Of(Owner);
        loc.Add("Weight", pack == null ? 0 : Loadout.TotalWeight(Owner));
        loc.Add("Capacity", pack == null ? 0 : Loadout.Capacity(Owner));
        loc.Add("Stored", pack?.StoredCount ?? 0);
    }

    protected override Task<bool> Perform() => ChestFlow(Owner);

    /// <summary>
    /// Сундук: карты и снаряжение туда и обратно. Лагерь, а со вьючным зверем — и прилавок любой лавки (действие лавки,
    /// выполняется у всех игроков коопа; выбор — синхронные экраны игры).
    /// </summary>
    public static async Task<bool> ChestFlow(Player owner)
    {
        var pack = TravelPack.Of(owner);
        if (pack == null) return false;
        var changed = false;

        // 1. Карты из колоды → в сундук.
        var storePrefs = new CardSelectorPrefs(new LocString("rest_site_ui", "SLAYTHEREQUIEM_CHEST_STORE_PROMPT"), 0, 99)
        {
            Cancelable = true,
            RequireManualConfirmation = true,
        };
        var toStore = (await CardSelectCmd.FromDeckGeneric(owner, storePrefs, c => c.IsRemovable)).ToList();
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
            var chosen = (await CardSelectCmd.FromSimpleGrid(new MegaCrit.Sts2.Core.GameActions.Multiplayer.ThrowingPlayerChoiceContext(), takeable, owner, takePrefs)).ToList();
            if (chosen.Count > 0)
            {
                await pack.TakeCards(chosen.Select(c => takeable.IndexOf(c)).ToList());
                changed = true;
            }
        }

        // 3. Снаряжение: снятая из рюкзака → в сундук, или из сундука → в рюкзак (одна вещь; «Ловкая укладка» — пока не закроете).
        var unlimited = HeroRecord.Of(owner)?.Has(Skill.DeftPacking) == true;
        do
        {
            var stowed = Loadout.All(owner).Where(r => r.IsStowed).Cast<RelicModel>().ToList();
            var stored = pack.StoredItemRelics();
            if (stowed.Count + stored.Count == 0) break;
            var pick = await RelicSelectCmd.FromChooseARelicScreen(owner, [.. stowed, .. stored]);
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
            else break;
        } while (unlimited);

        return changed;
    }
}

/// <summary>
/// Таверна (14 §2.9, M5): в части лагерей по зерну живёт Наставник. За золото — очко навыка (100 / 150 / 200, не больше 3
/// за забег; узел чужой школы — +50 %) сразу на выбранный узел дерева или урок владения оружием (90): снимает
/// «Неумелый хват» для одного класса. Очков лагеря не тратит. Свой значок — в M6.
/// </summary>
public sealed class TavernRestSiteOption(Player owner) : RequiemRestSiteOption(owner)
{
    public override string OptionId => "SLAYTHEREQUIEM_TAVERN";
    public override string FallbackIcon => "LIFT";
    public override CampAction Action => CampAction.Tavern;

    private int? SkillPrice(bool native) =>
        HeroRecord.Of(Owner) is { } hero ? ShopRules.MentorSkillPrice(hero.MentorPoints, native) : null;

    private bool CanBuySkill(HeroRecord hero) =>
        hero.MentorLearnable().Any(n => SkillPrice(hero.IsNativeNode(n)) is { } price && price <= Owner.Gold);

    private bool CanBuyLesson(HeroRecord hero) =>
        hero.LessonsAvailable().Count > 0 && ShopRules.WeaponLessonPrice <= Owner.Gold;

    protected override bool Available => HeroRecord.Of(Owner) is { } hero && (CanBuySkill(hero) || CanBuyLesson(hero));

    protected override void Fill(LocString loc)
    {
        loc.Add("SkillPrice", SkillPrice(true) ?? 0);
        loc.Add("LessonPrice", ShopRules.WeaponLessonPrice);
        loc.Add("Bought", HeroRecord.Of(Owner)?.MentorPoints ?? 0);
    }

    private static Type LessonCard(WeaponKind kind) => kind switch
    {
        WeaponKind.Crossbow => typeof(PickLessonCrossbow),
        WeaponKind.Shield => typeof(PickLessonShield),
        _ => typeof(PickLessonTwoHanded),
    };

    protected override async Task<bool> Perform()
    {
        var hero = HeroRecord.Of(Owner);
        if (hero == null) return false;
        var changed = false;
        for (var guard = 0; guard < 8; guard++)
        {
            var choices = new List<CampMenu.Choice>();
            if (CanBuySkill(hero))
                choices.Add(new CampMenu.Choice(typeof(PickMentorSkill), SkillPrice(true) ?? 0, SkillPrice(false) ?? 0, "skill"));
            if (CanBuyLesson(hero))
                foreach (var kind in hero.LessonsAvailable())
                    choices.Add(new CampMenu.Choice(LessonCard(kind), ShopRules.WeaponLessonPrice, 0, kind));
            if (choices.Count == 0) break;

            switch (await CampMenu.Pick(Owner, choices, "SLAYTHEREQUIEM_TAVERN_PROMPT"))
            {
                case "skill":
                {
                    var nodes = hero.MentorLearnable()
                        .Where(n => SkillPrice(hero.IsNativeNode(n)) is { } p && p <= Owner.Gold)
                        .Select(n => new CampMenu.Choice(CampMenu.NodeCard(n.Skill), n.Rank, hero.CostOf(n), n))
                        .ToList();
                    if (await CampMenu.Pick(Owner, nodes, "SLAYTHEREQUIEM_MENTOR_PROMPT") is not SkillNode node) break;
                    if (SkillPrice(hero.IsNativeNode(node)) is not { } price || price > Owner.Gold) break;
                    hero.MentorPoints += 1;
                    if (!await hero.Learn(node.Skill))
                    {
                        hero.MentorPoints -= 1;
                        break;
                    }
                    await PlayerCmd.LoseGold(price, Owner, GoldLossType.Spent);
                    changed = true;
                    break;
                }
                case WeaponKind kind:
                    await PlayerCmd.LoseGold(ShopRules.WeaponLessonPrice, Owner, GoldLossType.Spent);
                    hero.WeaponLessons = ProgressionRules.WithLesson(hero.WeaponLessons, kind);
                    hero.Refresh();
                    hero.Flash();
                    changed = true;
                    break;
                default:
                    return changed;
            }
        }
        return changed;
    }
}
