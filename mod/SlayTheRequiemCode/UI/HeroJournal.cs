using Godot;
using MegaCrit.Sts2.Core.HoverTips;
using SlayTheRequiem.SlayTheRequiemCode.Powers;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;
using SlayTheRequiem.SlayTheRequiemCode.Alchemy;
using SlayTheRequiem.SlayTheRequiemCode.Cards;
using SlayTheRequiem.SlayTheRequiemCode.Core;
using SlayTheRequiem.SlayTheRequiemCode.Equipment;
using SlayTheRequiem.SlayTheRequiemCode.Pantheon;
using SlayTheRequiem.SlayTheRequiemCode.Progression;
using SlayTheRequiem.SlayTheRequiemCode.Relics;
using SlayTheRequiem.SlayTheRequiemCode.Resources;
using SlayTheRequiem.SlayTheRequiemCode.Rules;
using SlayTheRequiem.SlayTheRequiemCode.Shops;
using SlayTheRequiem.SlayTheRequiemCode.Spells;
using SlayTheRequiem.SlayTheRequiemCode.Transformations;
using Entry = SlayTheRequiem.SlayTheRequiemCode.UI.NRequiemSheet.Entry;
using Page = SlayTheRequiem.SlayTheRequiemCode.UI.NRequiemSheet.Page;

namespace SlayTheRequiem.SlayTheRequiemCode.UI;

/// <summary>Actual hero/inventory, not a second editable copy or a catalog of promised features.</summary>
public static class HeroJournal
{
    public static bool Show(Player owner) => NRequiemSheet.Show(owner, T("heading"), () => Pages(owner));
    public static bool ShowShelf(Player owner) => NRequiemSheet.Show(owner, T("shouts"), () => [new Page(T("shouts"), Shelf(owner))]);
    private static string T(string key, params (string, object)[] args) => RequiemUi.TipText("UI.journal." + key + ".title", args);
    private static Entry Relic(RelicModel relic) => new(relic.Title.GetFormattedText(), relic.DynamicDescription.GetFormattedText(), relic.Icon);

    private static IReadOnlyList<Page> Pages(Player owner)
    {
        var hero = HeroRecord.Of(owner);
        if (hero == null) return [new(T("hero"), [new(T("unavailable"), T("unavailable"))])];
        return [new(T("hero"), Hero(owner, hero)), new(T("skills"), Skills(owner, hero)),
            new(T("equipment"), Equipment(owner)), new(T("magic"), Magic(owner, hero)),
            new(T("burden"), Burden(owner)), new(T("faith"), FaithAndForms(owner)), new(T("shouts"), Shelf(owner)), new(T("battle"), Battle(owner))];
    }
    private static List<Entry> Hero(Player owner, HeroRecord hero)
    {
        var stats = RequiemMode.StatsFor(owner); var gear = Loadout.Bonuses(owner);
        var resource = RequiemResource.Of<StaminaResource>(owner);
        var staminaPool = resource?.Pool ?? Math.Max(1, stats.StaminaPool + gear.StaminaPoolBonus + Mana.ManaSources(owner).Sum(s => s.StaminaPoolBonus));
        var regen = resource?.Regen ?? Math.Max(0, stats.StaminaRegen + gear.StaminaRegenBonus
            + Mana.ManaSources(owner).Sum(s => s.StaminaRegenBonus) - Loadout.Overload(owner).StaminaRegenPenalty);
        var entries = new List<Entry>
        {
            new(T("level", ("Level", hero.Level)), T("progress", ("Renown", hero.Renown), ("Next", ProgressionRules.RenownToNext(hero.Renown)),
                ("Stats", hero.UnspentStatPoints), ("Skills", hero.UnspentSkillPoints))),
            new(T("resources"), T("resourceValues", ("Hp", owner.Creature.CurrentHp), ("MaxHp", owner.Creature.MaxHp),
                ("Stamina", resource?.Amount ?? staminaPool), ("StaminaPool", staminaPool), ("StaminaRegen", regen),
                ("Mana", Mana.Amount(owner)), ("ManaPool", Mana.Pool(owner)), ("ManaRegen", Mana.Regen(owner)), ("Poise", HeroStatRules.Poise(owner)))),
            new(T("stats"), T("statValues", ("Health", hero.StatHealth), ("Stamina", hero.StatStamina), ("Mana", hero.StatMana),
                ("Burden", hero.StatBurden), ("Mastery", hero.StatMastery)) + "\n" + T("trainHint")),
        };
        entries.AddRange(owner.Relics.Where(r => r is OriginRelic or BackgroundRelic).Select(Relic));
        return entries;
    }
    private static List<Entry> Skills(Player owner, HeroRecord hero)
    {
        var entries = new List<Entry> { new(T("skills"), T("trainHint")) };
        var branches = SkillTree.BranchesFor(owner.Character.GetType().Name);
        foreach (var branch in branches)
            foreach (var node in SkillTree.InBranch(branch))
            {
                var token = (PickCard)ModelDb.GetById<CardModel>(ModelDb.GetId(CampMenu.NodeCard(node.Skill))).ToMutable();
                token.SetDisplay(node.Rank, hero.CostOf(node));
                var state = hero.Has(node.Skill) ? T("learned")
                    : !SkillTree.CanLearn(node, hero.Learned.ToArray(), branches) ? T("prerequisite")
                    : hero.CostOf(node) > hero.UnspentSkillPoints ? T("noPoints") : T("learnable");
                entries.Add(new(RequiemText.Branch(branch) + " · " + token.Title.GetFormattedText(),
                    RequiemText.Plain(token.GetDescriptionForPile(PileType.None)) + "\n" + T("skillCost", ("Cost", hero.CostOf(node)), ("State", state))));
            }
        return entries;
    }
    private static List<Entry> Equipment(Player owner)
    {
        var entries = new List<Entry> { new(T("equipment"), T("equipmentHint")) };
        foreach (var item in Loadout.All(owner).OrderBy(i => i.Slot).ThenBy(i => i.IsStowed))
        {
            var key = InventoryIdentity.RelicKey(item); var stow = !item.IsStowed;
            var can = ShopActionPolicy.TryQuote(owner, ShopAction.ChangeEquipment, 0, stow ? 1 : 0, key, out _, out _)
                && !InventoryGate.For(owner).Blocked;
            IReadOnlyList<EquipItem> displaced = item.IsStowed ? EquipmentRules.Displaced(Loadout.Equipped(owner).Select(i => i.Describe()).ToArray(), item.Describe()) : [];
            entries.Add(new(item.Title.GetFormattedText(), RequiemUi.TipText($"UI.slot.{item.Slot}.title") + " · "
                + T(item.IsStowed ? "stowed" : "equipped") + "\n" + T("itemValues", ("Tier", item.Tier), ("Upgrades", item.Upgrades),
                    ("Weight", Loadout.RelicWeight(owner, item))) + "\n" + item.DynamicDescription.GetFormattedText()
                + (displaced.Count > 0 ? "\n" + T("displace", ("Count", displaced.Count)) : ""), item.Icon,
                T(stow ? "stow" : "equip"), async () => { await ShopActions.Perform(owner, ShopAction.ChangeEquipment, 0, stow ? 1 : 0, key); },
                can, can ? "" : T(CombatManager.Instance.IsInProgress ? "combatReadOnly" : "operationBlocked")));
        }
        return entries;
    }
    private static List<Entry> Magic(Player owner, HeroRecord hero)
    {
        var entries = new List<Entry>();
        foreach (var school in Mastery.Schools)
            entries.Add(new(RequiemUi.TipText($"CREATION.school.{school}.title"), T("schoolValues", ("Level", Mastery.Of(owner, school)),
                ("Cost", hero.MasteryCost(school)), ("Native", T(hero.IsNative(school) ? "native" : "foreign")))));
        foreach (var spell in owner.Deck.Cards.OfType<SpellCard>())
            entries.Add(new(spell.Title.GetFormattedText(), T("castValues", ("Cost", spell.Affinity.ManaCost), ("Factor", spell.EffectMultiplier),
                ("State", RequiemUi.TipText($"UI.cast.{spell.Affinity.State}.title"))) + "\n" + spell.GetDescriptionForPile(PileType.Deck)));
        return entries;
    }
    private static List<Entry> Burden(Player owner)
    {
        var overload = Loadout.Overload(owner);
        var entries = new List<Entry> { new(T("burden"), T("burdenValues", ("Weight", Loadout.TotalWeight(owner)), ("Capacity", Loadout.Capacity(owner)),
            ("Excess", overload.Excess), ("Regen", overload.StaminaRegenPenalty), ("Hand", overload.OpeningHandPenalty),
            ("Stored", TravelPack.Of(owner)?.StoredCount ?? 0)) + "\n" + T("unloadHint")) };
        entries.AddRange(owner.Deck.Cards.Select(card => new Entry(card.Title.GetFormattedText(), T("weight", ("Weight", Loadout.CardWeight(card))))));
        return entries;
    }
    private static List<Entry> FaithAndForms(Player owner)
    {
        var entries = new List<Entry> { new(T("faith"), T("pathTiming")) };
        entries.AddRange(owner.Relics.Where(r => r is FaithSigil or CelestialBlessing or FormRelic or PathEssence).Select(Relic));
        return entries;
    }
    private static List<Entry> Battle(Player owner)
    {
        var entries = new List<Entry> { new(T("battle"), T("battleHint")) };
        var reveal = EnemyTraits.Reveal; EnemyTraits.Reveal = true;
        try
        {
            foreach (var creature in owner.Creature.CombatState?.Creatures ?? [])
            {
                if (!creature.IsAlive) continue;
                var name = creature.Monster?.Title.GetFormattedText() ?? T("hero");
                entries.Add(new(name, T("battleValues", ("Hp", creature.CurrentHp), ("MaxHp", creature.MaxHp),
                    ("Cost", creature.IsEnemy ? EnemyActionBudget.CostOf(creature) : 0))));
                foreach (var power in creature.Powers)
                    foreach (var tip in power.HoverTips.OfType<HoverTip>())
                        entries.Add(new(name + " · " + tip.Title, tip.Description, tip.Icon));
            }
        }
        finally { EnemyTraits.Reveal = reveal; }
        return entries;
    }
    private static List<Entry> Shelf(Player owner)
    {
        var entries = new List<Entry> { new(T("shouts"), T("shelfHint", ("Capacity", Shouts.Capacity(owner)))) };
        if (owner.Creature.GetPower<ShoutShelfPower>() is { } shelf)
            entries.AddRange(shelf.Entries.Select(e => new Entry(e.Card.Title.GetFormattedText(),
                T(e.Turns == 0 ? "returnPending" : "cooldown", ("Turns", e.Turns)), RequiemUi.Texture(e.Card.PortraitPath))));
        return entries;
    }
}
