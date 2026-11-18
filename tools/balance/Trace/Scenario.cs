using SlayTheRequiem.SlayTheRequiemCode.Core;

namespace SlayTheRequiem.Balance;

/// <summary>DELIBERATELY LIMITED ledger workload. Every fight is ASSUMED won; there are no turns, AI, winrate,
/// real map generation, generated card choices, native offers, HP loss or death. Use actual Core arithmetic
/// to compare the SAME synthetic workloads before/after two policy changes, then compare native observations separately.</summary>
public static class Scenario
{
    public static readonly string[] Heroes = ["Ironclad", "Silent", "Regent"];
    public const int SeedCount = 10;
    public static string Seed(int n) => "STRB2B" + n.ToString("0000", System.Globalization.CultureInfo.InvariantCulture);

    private static int Roll(string seed, string field, int act, int ordinal, int min, int max) =>
        min + (int)(ShopRules.Fnv1a(seed + "|" + field, act * 1000 + ordinal) % (uint)(max - min + 1));

    public static Run RunLedger(string hero, int seedIndex, int revision, bool shortRun = false, int requiemLevel = 1)
    {
        if (!Heroes.Contains(hero)) throw new ArgumentOutOfRangeException(nameof(hero));
        if (seedIndex is < 0 or >= SeedCount) throw new ArgumentOutOfRangeException(nameof(seedIndex));
        if (revision is < 0 or > EconomyRules.CurrentRevision) throw new ArgumentOutOfRangeException(nameof(revision));
        if (requiemLevel is < RequiemLevelRules.Min or > RequiemLevelRules.Max) throw new ArgumentOutOfRangeException(nameof(requiemLevel));
        var seed = Seed(seedIndex);
        var stats = HeroStats.ForCharacterTypeName(hero);
        // Native 0.107.1 starting counts/gold (no identity gifts or Neow). Rotate the approved independent bonus.
        var bonus = seedIndex % 3;
        var gold = 99 + (bonus == 0 ? HeroCreationPlan.GoldGift : 0);
        var weight = (hero == "Silent" ? 12 : 10) - (bonus == 2 ? 1 : 0);
        var burdenPoints = 0;
        var renown = 0;
        var removals = 0;
        var treatises = 0;
        var gearPrice = 0;
        var gearWeight = 0;
        var rows = new List<ActRow>();
        var slots = ActRules.Slots(shortRun);
        for (var index = 0; index < slots.Length; index++)
        {
            var slot = slots[index];
            var kind = slot switch
            {
                0 => ActKind.Foothills,
                1 => seedIndex % 2 == 0 ? ActKind.FrozenBarrows : ActKind.ClockworkDepths,
                2 => ActKind.Hive,
                3 => seedIndex / 2 % 2 == 0 ? ActKind.BloodCourt : ActKind.StarTower,
                _ => ActKind.Glory,
            };
            var route = new EconomyRoute(revision, index, slots.Length, true, shortRun);
            var rooms = ActRules.Rooms(kind);
            var elites = Roll(seed, "elite-count", index, 0, 1, 2);
            var shops = Roll(seed, "shop-count", index, 0, 1, 2);
            const int camps = 2, events = 2, treasures = 1;
            var normals = rooms - elites - shops - camps - events - treasures;
            var budgetOpen = gold;
            var points = ProgressionRules.StatPointsFor(ProgressionRules.LevelFor(renown));
            // Explicit assumed management policy: spend at most two AVAILABLE stat points on Burden next act.
            var spentThisAct = 0;
            while (weight > stats.BurdenCapacity + burdenPoints * ProgressionRules.BurdenPerPoint
                && burdenPoints < points && spentThisAct < 2)
            { burdenPoints++; spentThisAct++; }
            var capacity = stats.BurdenCapacity + burdenPoints * ProgressionRules.BurdenPerPoint;
            var preBossGold = 0;
            for (var n = 0; n < normals; n++) preBossGold += ShopRules.CombatGold(Roll(seed, "normal-gold", index, n, 10, 20));
            for (var n = 0; n < elites; n++) preBossGold += ShopRules.CombatGold(Roll(seed, "elite-gold", index, n, 35, 45));
            gold += preBossGold;
            var budgetAtAssumedShop = gold;
            // A hypothetical basket, NOT generated stock: top specialist weapon/staff, one brew, removal,
            // Regent's treatise in route slots 2/4. Every shop is assumed available after non-boss wins.
            var tiers = hero == "Regent" ? EconomyRules.StaffTiers(route) : EconomyRules.EquipmentTiers(ShopType.Armorer, route);
            var targetTier = tiers.Max;
            var targetPrice = hero == "Regent" ? ShopRules.StaffPrice(targetTier) : ShopRules.EquipmentPrice(EquipCategory.Weapon, targetTier);
            var newGearWeight = hero == "Silent" ? 1 : 2; // representative dagger / sword or staff, NOT all equipment
            var spent = 0;
            var sold = 0;
            var gearBought = false;
            if (gold + ShopRules.SellValue(gearPrice, 0) >= targetPrice
                && BurdenRules.Evaluate(weight - gearWeight + newGearWeight, capacity).CanPickUp)
            {
                sold = ShopRules.SellValue(gearPrice, 0); gold += sold;
                gold -= targetPrice; spent += targetPrice;
                weight = weight - gearWeight + newGearWeight; gearWeight = newGearWeight; gearPrice = targetPrice;
                gearBought = true;
            }
            var brewTier = EconomyRules.BrewTier(route);
            var brewPrice = ShopRules.BrewPrice(brewTier, poison: false);
            var brewBought = gold >= brewPrice;
            if (brewBought) { gold -= brewPrice; spent += brewPrice; }
            var removalPrice = ShopRules.RemovalPrice(removals);
            var removal = gold >= removalPrice && removals < 5;
            if (removal) { gold -= removalPrice; spent += removalPrice; removals++; weight--; }
            var treatise = hero == "Regent" && slot is 1 or 3 && gold >= ShopRules.TreatisePrice(treatises);
            // TreatiseRelic.AfterObtained consumes the tome after raising mastery; it is not permanent Burden.
            if (treatise) { var cost = ShopRules.TreatisePrice(treatises++); gold -= cost; spent += cost; }
            var accepts = 0;
            var skipped = 0;
            // NOT a native rarity/card generator. Model half the normal victories + every elite as desired
            // card acquisitions, 1/5 synthetic Rare probability; one representative 1-weight relic per elite.
            var demand = normals / 2 + elites;
            for (var n = 0; n < demand; n++)
            {
                var rarity = Roll(seed, "sample-rarity", index, n, 0, 4) == 0 ? "Rare" : "Common";
                var nextWeight = BurdenRules.DefaultCardWeight(rarity, false, false);
                var load = BurdenRules.Evaluate(weight + nextWeight, capacity);
                if (load.CanPickUp && load.Excess <= 5) { weight += nextWeight; accepts++; } else skipped++;
            }
            for (var n = 0; n < elites; n++)
                if (BurdenRules.Evaluate(weight + 1, capacity) is { CanPickUp: true, Excess: <= 5 }) weight++;
            // Boss gold is collected AFTER this act's shopping, and can only fund the next act.
            var bossGold = ShopRules.CombatGold(100);
            gold += bossGold;
            renown += normals * ProgressionRules.RenownFor(EnemyRank.Normal) + elites * ProgressionRules.RenownFor(EnemyRank.Elite)
                + ProgressionRules.RenownFor(EnemyRank.Boss);
            var level = ProgressionRules.LevelFor(renown);
            var overload = BurdenRules.Evaluate(weight, capacity);
            var forgeCost = CampRules.Cost(CampAction.Forge, false, false);
            var shortRestCost = CampRules.Cost(CampAction.ShortRest, false);
            var brewCost = CampRules.Cost(CampAction.Brew, false);
            // Merely an opportunity-cost schedule: camp one Forge, camp two Short Rest + Brew (if materials).
            var campSpent = (gearBought ? forgeCost : CampRules.Cost(CampAction.Rest, false)) + shortRestCost + brewCost;
            rows.Add(new(index, slot, kind.ToString(), rooms, normals, elites, shops, camps, events, treasures,
                budgetOpen, preBossGold, bossGold, budgetAtAssumedShop, spent, sold, gold,
                targetTier, targetPrice, gearBought, brewTier, brewPrice, brewBought, removal, treatise,
                demand, accepts, skipped, weight, capacity, burdenPoints, overload.Excess, overload.StaminaRegenPenalty,
                overload.OpeningHandPenalty, renown, level, ProgressionRules.StatPointsFor(level), ProgressionRules.SkillPointsFor(level),
                TransformationRules.PathOfferDue(level, index, slots.Length, false, shortRun),
                campSpent, camps * CampRules.BasePoints, gearBought ? ShopRules.SharpenPrice(targetTier) : 0,
                EconomyRules.ReagentKitPrice(route) * (decimal)AlchemyRules.Ingredients(AlchemyRules.RecipeFor(Brew.Health), false).Length
                    / ShopRules.ReagentKitSize + ShopRules.BrewServicePrice,
                EconomyRules.EnemyHpMultiplier(110, requiemLevel, EconomyRules.ActHpBonusPercent(kind, !shortRun, EnemyRank.Normal, false, false, revision)),
                EconomyRules.EnemyHpMultiplier(110, requiemLevel, EconomyRules.ActHpBonusPercent(kind, !shortRun, EnemyRank.Boss, ActRules.IsNewAct(kind), false, revision)),
                EconomyRules.EnemyHpMultiplier(110, requiemLevel, EconomyRules.ActHpBonusPercent(kind, !shortRun, EnemyRank.Boss, true, false, revision))));
        }
        return new(hero, seed, revision, shortRun, requiemLevel, bonus switch { 0 => "gold60", 1 => "common-potion", _ => "remove-basic" }, rows);
    }

    public static Batch FullBatch() => new(1, "core-scenario", [
        "Not a native map, shop, reward or combat simulation. Every fight is assumed won; no damage/deaths/winrate.",
        "Ten fixed seeds per hero, same workloads in revision 0 vs 1, no identity/Neow/event/treasure/bonus-relic effects.",
        "Room counts from ActRules; synthetic allocation: 1–2 elites, 1–2 shops, 2 camps, 2 events, 1 treasure; remaining normal fights, then boss. Act 2/4 variants cover all four branch combinations, not native door probabilities.",
        "Native 0.107.1 default gold ranges 10–20 normal, 35–45 elite, 100 boss; no Poverty ascension or encounter overrides. Boss gold cannot fund same-act shopping.",
        "Hypothetical top-specialist basket, representative dagger/sword/staff weight 1/2/2, instant sale of old weapon at trust 0. Not availability of a particular item. Purchased treatises are consumed, not extra Burden.",
        "Half normal wins plus each elite wants a sample card; 1/5 synthetic Rare weight; one sample 1-weight relic per elite. Skip over excess 5; no chest/mule free-weight assumption.",
        "Spend up to 2 earned stat points on Burden next act if overloaded. No automatic stat refunds/mastery grants; other points remain unspent in this ledger.",
        "Camp plan is only opportunity cost (Forge; Short Rest+Brew if materials), not actual material drops, potion crafting or HP sustain.",
        "Primary boss reference: authored in new act variants 2/4, borrowed native boss in 1/3/5. Separate authored-boss factor is a hypothetical optional-superboss reference, not an extra battle or a win.",
        "Baseline requiem level 1 / HP config 110. Short/difficulty smoke regressions are separate, not calibrated winrates."
    ], (from hero in Heroes from seed in Enumerable.Range(0, SeedCount) from revision in new[] { 0, EconomyRules.CurrentRevision }
        select RunLedger(hero, seed, revision)).ToArray());

    public sealed record Batch(int Schema, string Producer, string[] Assumptions, Run[] Runs);
    public sealed record Run(string Hero, string Seed, int Revision, bool ShortRun, int RequiemLevel, string Bonus, IReadOnlyList<ActRow> Acts);
    public sealed record ActRow(int ActIndex, int RouteSlot, string Kind, int Rooms, int Normals, int Elites, int Shops, int Camps, int Events, int Treasures,
        int BudgetOpen, int PreBossGold, int BossGold, int BudgetAtAssumedShop, int Spent, int Sold, int BudgetEnd,
        int TargetTier, int TargetPrice, bool GearBought, int BrewTier, int BrewPrice, bool BrewBought, bool Removal, bool Treatise,
        int DesiredCards, int AcceptedCards, int SkippedCards, int Weight, int Capacity, int BurdenPoints, int Excess, int StaminaRegenPenalty,
        int OpeningHandPenalty, int Renown, int Level, int StatPointsEarned, int SkillPointsEarned, bool PathDue,
        int PlannedCampSpent, int CampBudget, int SharpenAlternativePrice, decimal ReagentBrewIReferenceCost,
        decimal BorrowedHpMultiplier, decimal PrimaryBossHpMultiplier, decimal AuthoredBossHpMultiplier);
}
