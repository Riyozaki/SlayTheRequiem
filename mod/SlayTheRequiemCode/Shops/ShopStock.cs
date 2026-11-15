using MegaCrit.Sts2.Core.Entities.Merchant;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;
using SlayTheRequiem.SlayTheRequiemCode.Core;
using SlayTheRequiem.SlayTheRequiemCode.Equipment;
using SlayTheRequiem.SlayTheRequiemCode.Potions;
using SlayTheRequiem.SlayTheRequiemCode.Spells;

namespace SlayTheRequiem.SlayTheRequiemCode.Shops;

/// <summary>
/// Подбор товаров для лавок Реквиема (docs/design/14 §2). Все случайности — из <c>PlayerRng.Shops</c>, как у ванильной
/// витрины, поэтому витрина воспроизводится после перезагрузки.
/// </summary>
public static class ShopStock
{
    // ───── снаряжение ─────

    private static bool Owns(Player player, RelicModel relic) => player.Relics.Any(r => r.GetType() == relic.GetType());

    /// <summary>Снаряжение, которое можно выставить: не куплено, разрешено в лавках, ступень в пределах.</summary>
    public static List<EquipmentRelic> EquipmentCandidates(Player player, int minTier, int maxTier, Func<EquipmentRelic, bool>? filter = null) =>
        ModelDb.AllRelics.OfType<EquipmentRelic>()
            .Where(e => e.IsAllowedInShops && e is not MageStaff && !Owns(player, e) && e.Tier >= minTier && e.Tier <= maxTier && (filter?.Invoke(e) ?? true))
            .ToList();

    /// <summary>
    /// Один предмет снаряжения: сначала по фильтру и ступеням, затем без фильтра, затем любой ступени не выше максимума.
    /// <paramref name="exclude"/> — уже выставленные на этой витрине.
    /// </summary>
    public static EquipmentRelic? PickEquipment(Player player, int minTier, int maxTier, Func<EquipmentRelic, bool>? filter, ISet<Type> exclude)
    {
        var rng = player.PlayerRng.Shops;
        foreach (var pool in new[]
                 {
                     EquipmentCandidates(player, minTier, maxTier, filter),
                     EquipmentCandidates(player, minTier, maxTier),
                     EquipmentCandidates(player, 1, maxTier),
                 })
        {
            var options = pool.Where(e => !exclude.Contains(e.GetType())).ToList();
            if (options.Count == 0) continue;
            var pick = rng.NextItem(options);
            if (pick == null) continue;
            exclude.Add(pick.GetType());
            return pick;
        }
        return null;
    }

    public static MerchantRelicEntry RelicEntry(RelicModel canonical, Player player) => new(canonical.ToMutable(), player);

    // ───── заклинания ─────

    /// <summary>Заклинания, которые Чародейская лавка может выставить этому герою (08 + 14 §2.3).</summary>
    public static List<CardModel> SpellCandidates(Player player) =>
        ModelDb.AllCardPools.OfType<SpellCardPool>().SelectMany(p => p.AllCards).OfType<SpellCard>()
            .Where(s => SpellAffinity.AllowedInShop(s.Tier, Mastery.Of(player, s.School)))
            .Cast<CardModel>()
            .Distinct()
            .ToList();

    /// <summary>Трактат владения: школа выбирается среди тех, где владение уже есть (иначе — любая).</summary>
    public static RelicModel? PickTreatise(Player player)
    {
        var all = ModelDb.AllRelics.OfType<TreatiseRelic>().ToList();
        if (all.Count == 0) return null;
        var known = all.Where(t => Mastery.Of(player, t.School) >= 1).ToList();
        return player.PlayerRng.Shops.NextItem(known.Count > 0 ? known : all);
    }

    // ───── зелья ─────

    /// <summary>Зелье или яд Реквиема ступени по акту (09 §3–4). null — если подходящих классов нет.</summary>
    public static PotionModel? PickBrew(Player player, bool poison, int tier)
    {
        var options = Enum.GetValues<Brew>()
            .Where(b => AlchemyRules.IsPoison(b) == poison && b != Brew.Panacea)
            .Select(b => RequiemPotion.Canonical(b, tier) ?? RequiemPotion.Canonical(b, 1))
            .OfType<PotionModel>()
            .Distinct()
            .ToList();
        if (options.Count == 0) return null;
        return player.PlayerRng.Shops.NextItem(options)?.ToMutable();
    }

    // ───── посохи и свитки (14 §2.3) ─────

    /// <summary>Посох ступени в пределах (иначе — любой не выше максимума); null — все уже есть.</summary>
    public static MageStaff? PickStaff(Player player, int minTier, int maxTier, ISet<Type> exclude)
    {
        var all = ModelDb.AllRelics.OfType<MageStaff>().Where(s => !Owns(player, s) && !exclude.Contains(s.GetType()))
            .OrderBy(s => s.Id.Entry, StringComparer.Ordinal).ToList();
        var inRange = all.Where(s => s.Tier >= minTier && s.Tier <= maxTier).ToList();
        var pick = player.PlayerRng.Shops.NextItem(inRange.Count > 0 ? inRange : all.Where(s => s.Tier <= maxTier).ToList());
        if (pick != null) exclude.Add(pick.GetType());
        return pick;
    }

    /// <summary>Свиток не выше ступени; первый на витрине — ровно этой ступени (если есть). Повторы на витрине исключены.</summary>
    public static PotionModel? PickScroll(Player player, int maxTier, bool exactTier, ISet<Type> exclude)
    {
        var all = RequiemScroll.All().Where(s => !exclude.Contains(s.GetType())).ToList();
        var options = all.Where(s => exactTier ? s.Tier == maxTier : s.Tier <= maxTier).ToList();
        var pick = player.PlayerRng.Shops.NextItem(options.Count > 0 ? options : all.Where(s => s.Tier <= maxTier).ToList());
        if (pick == null) return null;
        exclude.Add(pick.GetType());
        return pick.ToMutable();
    }

    // ───── Храм и Ювелир: облачения бога (14 §2.5–2.6) ─────

    private static RelicModel? CanonicalOf(Type type) => ModelDb.GetByIdOrNull<RelicModel>(ModelDb.GetId(type));

    /// <summary>Амулет бога (null — нет или уже куплен).</summary>
    public static RelicModel? GodAmulet(Player player, Celestial god) =>
        CanonicalOf(Pantheon.CelestialBlessing.Amulets[(int)god]) is { } a && !Owns(player, a) ? a : null;

    /// <summary>Облачение бога (null — нет или уже куплено).</summary>
    public static RelicModel? GodRobe(Player player, Celestial god) =>
        CanonicalOf(Pantheon.CelestialBlessing.Robes[(int)god]) is { } r && !Owns(player, r) ? r : null;
}
